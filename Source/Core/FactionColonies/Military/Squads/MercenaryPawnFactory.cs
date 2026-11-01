using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;
using FactionColonies.util;

namespace FactionColonies
{
    /// <summary>
    /// Mercenary pawn generation. Three-tier xenotype/race fallback for combatants
    /// and a simple animal generator. Handles biotech custom-xenotype lookups and
    /// security-guard auto-assignment for non-violent xenotypes.
    /// </summary>
    public static class MercenaryPawnFactory
    {
        /// <summary>Generates an animal pawn for the slot and writes squad/settlement back-refs plus
        /// sub-pawn identity onto <paramref name="merc"/>. Caller is responsible for adding the merc to
        /// the owning merc's <c>animals</c> list and setting <c>merc.handler</c>.</summary>
        public static void CreateNewAnimal(MercenarySquadFC squad, ref Mercenary merc, PawnKindDef race)
        {
            Pawn newPawn = PawnGenerator.GeneratePawn(FCPawnGenerator.AnimalRequest(race));

            merc.squad = squad;
            merc.settlement = squad?.settlement;
            merc.pawn = newPawn;
            merc.subPawnType = Mercenary.SubPawnType.Animal;
            merc.subPawnKind = race;
        }

        /// <summary>Generates a mechanoid pawn for a mechanitor merc and bonds it to <paramref name="overseer"/>:
        /// an Overseer direct relation (for bandwidth accounting) plus a direct assignment to control group
        /// <paramref name="groupIndex"/> with <paramref name="workMode"/>. The mech is set to the Empire faction.
        /// Caller adds the merc to the squad's <c>mechs</c> list and sets <c>merc.handler</c>. No-op (leaves
        /// merc.pawn null) when Biotech is absent, the kind is invalid, or the overseer isn't a mechanitor —
        /// so a non-mechanitor overseer never produces stray unbonded mechs.</summary>
        public static void CreateNewMech(MercenarySquadFC squad, ref Mercenary merc, PawnKindDef mechKind, Pawn overseer, int groupIndex, MechWorkModeDef workMode)
        {
            if (!ModsConfig.BiotechActive || mechKind is null) return;
            if (overseer?.mechanitor is null) return;

            Pawn mech = null;
            try
            {
                mech = PawnGenerator.GeneratePawn(FCPawnGenerator.MechRequest(mechKind));
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"Failed to generate mech {mechKind.defName}: {ex.Message}");
            }
            if (mech is null) return;

            Faction empireFaction = FindFC.EmpireFaction;
            if (empireFaction != null && mech.Faction != empireFaction)
                mech.SetFaction(empireFaction);

            // Mechanoids aren't flesh, so PawnComponentsUtility only creates a relations tracker for a mech
            // once it's a PLAYER overseer subject — a freshly generated Empire mech has mech.relations == null.
            // AddDirectRelation dereferences otherPawn.relations (mech.relations) to add the reflexive entry,
            // so without this it NREs ("Failed to bond mech ... NRE") and the mech is never actually bonded.
            // Create the tracker up front, exactly as vanilla does (PawnComponentsUtility).
            if (mech.relations is null) mech.relations = new Pawn_RelationsTracker(mech);

            try
            {
                // Bond the mech to its overseer with the Overseer direct relation. This drives bandwidth
                // accounting and makes the mech follow its overseer on a faction change
                // (Pawn_MechanitorTracker.Notify_ChangedFaction iterates the overseer's Overseer relations).
                // Empire mechanitors aren't the player faction, so PawnRelationWorker_Overseer.OnRelationCreated
                // won't auto-assign a control group (IsMechanitor gates on player faction).
                overseer.relations.AddDirectRelation(PawnRelationDefOf.Overseer, mech);

                // Assign the mech to its designed control group + work mode now, so the design's grouping is
                // honored from creation rather than being clobbered by the vanilla auto-assign-to-group-0 that
                // fires when the merc is later drafted. This is safe off-map: an unspawned mech's think tree
                // enters the Despawned subtree (JobGiver_IdleWhileDespawned), so the job re-evaluation inside
                // AssignPawnControlGroup never touches the (null) map.
                AssignMechToControlGroup(overseer.mechanitor, mech, groupIndex, workMode);
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"Failed to bond mech {mechKind.defName} to {overseer.LabelShortCap}: {ex}");
            }

            merc.squad = squad;
            merc.settlement = squad?.settlement;
            merc.pawn = mech;
            merc.subPawnType = Mercenary.SubPawnType.Mech;
            merc.subPawnKind = mechKind;
            merc.subPawnMechGroup = groupIndex;
            merc.subPawnWorkMode = workMode;
        }

        /// <summary>Assigns <paramref name="mech"/> to control group <paramref name="groupIndex"/> on
        /// <paramref name="tracker"/> with <paramref name="workMode"/>, lazily creating the groups and
        /// clamping the index into range. This runs vanilla job-determination (CheckForJobOverride)
        /// internally, so it is only safe for a SPAWNED mech whose overseer is spawned — it NREs in the
        /// think-tree's error-recovery path otherwise. Callers gate on Spawned.</summary>
        private static void AssignMechToControlGroup(Pawn_MechanitorTracker tracker, Pawn mech, int groupIndex, MechWorkModeDef workMode)
        {
            if (tracker is null || mech is null) return;
            MechWorkModeDef mode = workMode ?? MechWorkModeDefOf.Escort;
            EnsureLordDutyAssigned(mech);

            // The first call lazily creates the control groups from the MechControlGroups stat.
            tracker.AssignPawnControlGroup(mech, mode);
            if (tracker.controlGroups.Count > 0)
            {
                int idx = groupIndex;
                if (idx < 0) idx = 0;
                if (idx > tracker.controlGroups.Count - 1) idx = tracker.controlGroups.Count - 1;
                MechanitorControlGroup group = tracker.controlGroups[idx];
                group.SetWorkMode(mode);
                group.Assign(mech);
            }
        }

        /// <summary>Eagerly assigns a spawned mech its pending lord duty when it's in a duty-assigning lord
        /// but its duty is currently null. Any control-group (re)assignment forces a job re-evaluation
        /// (CheckForJobOverride); if the mech is in this transient lord-without-duty state, vanilla's
        /// ThinkNode_Duty logs a hard error ("doing ThinkNode_Duty with no duty", which pops the dev
        /// console). The state arises during a draft/undraft: Pawn.SetFaction clears the mind (duty -> null)
        /// while Empire's LordJob_DefendColony.Notify_PawnLost re-adds the still-spawned pawn to the defense
        /// lord, so for one frame the mech sits in a duty-assigning lord with no duty until the next
        /// LordJobTick reassigns duties. We do that reassignment now so the forced re-eval sees a consistent
        /// lord+duty state. ThinkNode_ConditionalHasLordDuty gates only on lord + AssignsDuties, which is
        /// exactly the condition checked here.</summary>
        private static void EnsureLordDutyAssigned(Pawn mech)
        {
            if (mech is null) return;
            Lord lord = mech.GetLord();
            if (lord?.CurLordToil != null && lord.CurLordToil.AssignsDuties && mech.mindState?.duty is null)
                lord.CurLordToil.UpdateAllDuties();
        }

        /// <summary>Reverses <see cref="CreateNewMech"/>'s bond: unassigns <paramref name="mech"/> from the
        /// overseer's control groups and removes the Overseer direct relation. Must run whenever a bonded
        /// mech dies, is destroyed, or is replaced — otherwise the (still-saved) mechanitor keeps a relation
        /// to a no-longer-saved mech, which NREs in Pawn_RelationsTracker.ExposeData on the next load.</summary>
        public static void UnbondMech(Pawn overseer, Pawn mech)
        {
            if (overseer is null || mech is null) return;
            try { overseer.mechanitor?.UnassignPawnFromAnyControlGroup(mech); }
            catch (Exception ex) { LogUtil.Warning($"Failed to unassign mech control group: {ex.Message}"); }

            Pawn_RelationsTracker rel = overseer.relations;
            if (rel is null) return;
            try
            {
                // A destroyed mech has a null relations tracker; TryRemoveDirectRelation dereferences
                // otherPawn.relations and would NRE, so strip the relation directly from the overseer's
                // list. For a live mech, use the proper API (handles the reverse link + caches).
                if (mech.relations != null)
                {
                    rel.TryRemoveDirectRelation(PawnRelationDefOf.Overseer, mech);
                }
                else
                {
                    List<DirectPawnRelation> rels = rel.DirectRelations;
                    for (int i = rels.Count - 1; i >= 0; i--)
                    {
                        DirectPawnRelation r = rels[i];
                        if (r != null && r.def == PawnRelationDefOf.Overseer && r.otherPawn == mech)
                            rels.RemoveAt(i);
                    }
                }
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"Failed to unbond mech from {overseer.LabelShortCap}: {ex.Message}");
            }
        }

        /// <summary>Re-establishes mechanitor control over a merc's mechs after a faction change.
        /// Drafting an Empire mechanitor to the player (or undrafting back) runs vanilla
        /// <c>Pawn_MechanitorTracker.Notify_BandwidthChanged</c>, which can drop mechs to "uncontrolled"
        /// because the control-group/bandwidth bookkeeping wasn't maintained while it was a non-player
        /// mechanitor. Re-adding the Overseer relation (if lost) and re-assigning each mech to its
        /// control group reconnects them. Mechs that genuinely exceed the overseer's bandwidth will still
        /// be dropped by the subsequent bandwidth recalc — that's a real design constraint.</summary>
        public static void RebindMechs(Mercenary merc)
        {
            Pawn overseer = merc?.pawn;
            if (!ModsConfig.BiotechActive || overseer?.mechanitor is null || merc.mechs is null) return;
            Pawn_MechanitorTracker tracker = overseer.mechanitor;
            foreach (Mercenary mw in merc.mechs)
            {
                Pawn m = mw?.pawn;
                // Only rebind mechs that are actually in the battle (spawned, alive). Missing placeholders
                // (pawn null) and off-map mechs aren't part of the current draft/undraft fight.
                if (m is null || m.Dead || m.Destroyed || !m.Spawned) continue;
                try
                {
                    // Settle the mech's lord-duty state before anything forces a job re-evaluation. Adding
                    // the Overseer relation to a player-faction overseer auto-assigns a control group
                    // (OnRelationCreated -> AssignPawnControlGroup, vanilla, bypassing our helper), which
                    // would hit the same ThinkNode_Duty error this guards against, so run it up front.
                    EnsureLordDutyAssigned(m);

                    // Test the relation on the OVERSEER's side (authoritative) rather than m.GetOverseer()
                    // (the mech's reflexive entry can be out of sync after a faction swap), so we never
                    // re-add an existing relation — that logs "Tried to add the same relation twice".
                    bool relationExists = overseer.relations != null
                        && overseer.relations.DirectRelationExists(PawnRelationDefOf.Overseer, m);
                    if (!relationExists && overseer.relations != null && m.relations != null)
                    {
                        // For a player-faction overseer, AddDirectRelation -> OnRelationCreated already
                        // assigns a control group, so the GetControlGroup check below short-circuits.
                        overseer.relations.AddDirectRelation(PawnRelationDefOf.Overseer, m);
                    }
                    // Assign a control group only if the mech isn't already in one. This both restores
                    // mechs that were bonded off-map but never grouped (the replacement case) and avoids
                    // re-assigning ones the relation-add just grouped (double SetWorkModeForPawn churn).
                    if (tracker.GetControlGroup(m) is null)
                        AssignMechToControlGroup(tracker, m, mw.subPawnMechGroup, mw.subPawnWorkMode);
                }
                catch (Exception e)
                {
                    LogUtil.Warning($"Failed to rebind mech {m.LabelShortCap} to {overseer.LabelShortCap}: {e.Message}");
                }
            }

            // Rebuild controlledPawns from the (design-grouped) assignments. AssignPawnControlGroup does
            // this internally, but we skip it for mechs already in their group (to preserve the design
            // grouping set at creation), so trigger the recalc explicitly — otherwise the mechs can stay
            // flagged "uncontrolled" after a faction change even though they're correctly bonded and grouped.
            // Mechs whose combined bandwidth exceeds the overseer's MechBandwidth stat are genuinely dropped
            // here; that's a real design constraint, not a bug.
            try { tracker.Notify_BandwidthChanged(); }
            catch (Exception e) { LogUtil.Warning($"Bandwidth recalc failed for {overseer.LabelShortCap}: {e.Message}"); }
        }

        /// <summary>
        /// If the mercenary's xenotype is non-violent and has security guards configured,
        /// auto-assigns a random guard animal from the xenotype's SecurityGuardList.
        /// Currently unreferenced; kept here for resurrection rather than re-extraction.
        /// </summary>
        public static void TryAssignSecurityGuard(MercenarySquadFC squad, Mercenary merc)
        {
            if (squad is null || merc?.pawn?.genes == null) return;
            // Don't overwrite a manually-assigned animal
            if (merc.animals != null && merc.animals.Any()) return;

            FactionFC factionFc = FindFC.FactionComp;
            if (factionFc?.xenotypeFilter == null) return;

            XenotypeFilter xenoFilter = factionFc.xenotypeFilter;
            List<PawnKindDef> guardOptions = null;

            XenotypeDef mercXenotype = merc.pawn.genes.Xenotype;
            if (mercXenotype != null && FactionCache.XenotypeIsNonViolent(mercXenotype))
            {
                guardOptions = xenoFilter.GetSecurityGuardsForXenotype(mercXenotype);
            }
            else if (merc.pawn.genes.CustomXenotype != null)
            {
                string customName = merc.pawn.genes.CustomXenotype.name;
                if (FactionCache.CustomXenotypeIsNonViolent(customName))
                {
                    guardOptions = xenoFilter.GetSecurityGuardsForCustomXenotype(customName);
                }
            }

            if (guardOptions != null && guardOptions.Any())
            {
                PawnKindDef guardKind = guardOptions.RandomElement();
                Mercenary guardAnimal = new Mercenary(true);
                CreateNewAnimal(squad, ref guardAnimal, guardKind);
                guardAnimal.handler = merc;
                if (merc.animals is null) merc.animals = new List<Mercenary>();
                merc.animals.Add(guardAnimal);
            }
        }

        public static void CreateNewPawn(MercenarySquadFC squad, ref Mercenary merc, PawnKindDef race, XenotypeDef _xenotype, string _customXenotypeName = null, MilUnitFC loadout = null)
        {
            XenotypeDef xenotypeChoice = _xenotype;
            PawnKindDef raceChoice = race;
            Gender? fixedGender = loadout?.forcedGender;
            FactionFC factionFc = FindFC.FactionComp;

            // Fall back to Human only when no race was requested. Race weight controls
            // random race selection (e.g. for default fighters); designed loadouts are
            // explicit player choices and must be respected even at weight 0.
            if (race == null)
            {
                raceChoice = PawnKindTemplateUtil.GetFighterForRace(ThingDefOf.Human);
            }

            // Try to generate pawn with the requested kind
            Pawn newPawn = null;
            try
            {
                PawnGenerationRequest request;
                if (_customXenotypeName != null)
                {
                    CustomXenotype custom = null;
                    FactionCache.CustomXenotypesDecoder?.TryGetValue(_customXenotypeName, out custom);

                    if (custom != null)
                        request = FCPawnGenerator.WorkerOrMilitaryRequest(raceChoice, custom, fixedGender);
                    else
                    {
                        LogUtil.Warning($"Custom xenotype '{_customXenotypeName}' not found, falling back to Baseliner");
                        request = FCPawnGenerator.WorkerOrMilitaryRequest(raceChoice, XenotypeDefOf.Baseliner, fixedGender);
                    }
                }
                else
                {
                    request = FCPawnGenerator.WorkerOrMilitaryRequest(raceChoice, xenotypeChoice, fixedGender);
                }
                newPawn = FCPawnGenerator.GenerateWithForcedXenotype(request);

                // Set faction after generation (since we generate without faction to avoid xenotype forcing)
                if (newPawn != null && newPawn.Faction == null)
                {
                    var empireFaction = FindFC.EmpireFaction;
                    if (empireFaction != null)
                    {
                        newPawn.SetFaction(empireFaction);
                    }
                }
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"Failed to generate pawn with kind {raceChoice?.defName}: {ex.Message}");
            }

            // Fallback 1: Try with Baseliner xenotype and NO faction (avoids faction xenotype forcing)
            if (newPawn == null)
            {
                LogUtil.Warning($"Pawn generation failed for {raceChoice?.defName}. Trying Baseliner fallback without faction.");
                try
                {
                    var simpleRequest = new PawnGenerationRequest(
                        kind: PawnKindDefOf.Colonist,
                        faction: null, // NO faction - this prevents faction xenotype forcing
                        context: PawnGenerationContext.NonPlayer,
                        tile: -1,
                        forceGenerateNewPawn: false,
                        allowDead: false,
                        allowDowned: false,
                        canGeneratePawnRelations: false, // No relations for factionless pawns
                        mustBeCapableOfViolence: true,
                        colonistRelationChanceFactor: 0,
                        forceAddFreeWarmLayerIfNeeded: false,
                        allowGay: true,
                        allowFood: true,
                        allowAddictions: false,
                        forcedXenotype: XenotypeDefOf.Baseliner // Force Baseliner - guaranteed violence capable
                    );
                    newPawn = PawnGenerator.GeneratePawn(simpleRequest);

                    // Set the faction after generation
                    if (newPawn != null)
                    {
                        var empireFaction = FindFC.EmpireFaction;
                        if (empireFaction != null)
                        {
                            newPawn.SetFaction(empireFaction);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.Warning($"Baseliner fallback also failed: {ex.Message}");
                }
            }

            // Fallback 2: Absolute minimal request - no faction, no xenotype, no violence requirement
            if (newPawn == null)
            {
                LogUtil.Warning("All standard generation failed. Trying minimal fallback.");
                try
                {
                    var fallbackRequest = new PawnGenerationRequest(
                        kind: PawnKindDefOf.Colonist,
                        faction: null, // NO faction
                        context: PawnGenerationContext.NonPlayer,
                        tile: -1,
                        forceGenerateNewPawn: false,
                        allowDead: false,
                        allowDowned: false,
                        canGeneratePawnRelations: false,
                        mustBeCapableOfViolence: false, // Allow non-violent as absolute last resort
                        colonistRelationChanceFactor: 0,
                        forceAddFreeWarmLayerIfNeeded: false,
                        allowGay: true,
                        allowFood: true,
                        allowAddictions: false
                    );
                    newPawn = PawnGenerator.GeneratePawn(fallbackRequest);

                    // Set the faction after generation
                    if (newPawn != null)
                    {
                        var empireFaction = FindFC.EmpireFaction;
                        if (empireFaction != null)
                        {
                            newPawn.SetFaction(empireFaction);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.Error($"Critical - all pawn generation attempts failed: {ex.Message}");
                }
            }

            // Final check - if still null, we cannot proceed
            if (newPawn == null)
            {
                LogUtil.Error("Critical error - could not generate any pawn for mercenary squad. Skipping this mercenary.");
                return;
            }

            if (newPawn.kindDef == null)
            {
                newPawn.kindDef = raceChoice ?? PawnKindDefOf.Colonist;
                LogUtil.Warning($"MercenaryPawnFactory.CreateNewPawn: detected null kindDef, setting to default");
            }

            newPawn.apparel?.DestroyAll();
            newPawn.equipment?.DestroyAllEquipment();

            // Install the designed implants (bionics/prosthetics/etc.) before the pawn is used.
            // Skipped on the degraded fallbacks only if the loadout is absent.
            if (loadout != null)
            {
                MilUnitFC.ApplyImplantsToPawn(newPawn, loadout);
                MilUnitFC.ApplyPsycastsToPawn(newPawn, loadout);
                MilUnitFC.ApplyMechanitorToPawn(newPawn, loadout);
            }

            // Anti-exploit: implant a death acidifier so the merc's gear dissolves on death (vanilla
            // mechanic). Replaces Empire's old bespoke corpse/gear destruction. Humanlike mercs only.
            TryApplyDeathAcidifier(newPawn);

            merc.squad = squad;
            merc.settlement = squad?.settlement;
            merc.pawn = newPawn;
        }

        /// <summary>
        /// Implants a vanilla death acidifier on humanlike mercs (gated by <see cref="FCSettings.antiExploit"/>),
        /// so their weapons and apparel dissolve on death instead of being lootable. No-op for animals/mechs,
        /// when anti-exploit is off, if the def is missing, or if the pawn already has the hediff (e.g. a
        /// legacy unit design installed it).
        /// </summary>
        private static void TryApplyDeathAcidifier(Pawn pawn)
        {
            if (!FCSettings.antiExploit) return;
            if (pawn?.health is null || pawn.RaceProps is null || !pawn.RaceProps.Humanlike) return;

            RecipeDef recipe = FCRecipeDefOf.InstallDeathAcidifier;
            if (recipe?.Worker is null) return;

            // Nothing to install if another mod stripped/failed to resolve the acidifier hediff
            // (leaving addsHediff null). ApplyOnPawn would otherwise pass null to HediffMaker.MakeHediff
            // and NRE, aborting the whole squad hire. Guarded here just as MilUnitFC.IsImplantApplicable does.
            if (recipe.addsHediff is null) return;

            // Install through the game's own surgery worker (Recipe_InstallImplant.ApplyOnPawn with a
            // null bill doer), exactly as MilUnitFC.ApplyImplantsToPawn does. GetPartsToApplyOn resolves
            // the recipe's fixed part (Torso) and returns nothing if the pawn already has the acidifier,
            // so a legacy unit design that already installed it is a no-op.
            try
            {
                BodyPartRecord part = recipe.Worker.GetPartsToApplyOn(pawn, recipe).FirstOrFallback();
                if (part is null) return;
                recipe.Worker.ApplyOnPawn(pawn, part, null, null, null);
            }
            catch (Exception ex)
            {
                // Best-effort: a surgery-worker failure (e.g. mod interaction) must not abort squad hire.
                LogUtil.Warning($"Failed to apply death acidifier to {pawn.LabelShortCap}: {ex.Message}");
            }
        }
    }
}
