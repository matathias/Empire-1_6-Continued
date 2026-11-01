using FactionColonies.util;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class MilUnitFC : IExposable, ILoadReferenceable
    {
        public int loadID;
        public string name;
        public bool isBlank;
        public double equipmentTotalCost;
        public int tickChanged = -1;

        // Companion animals (Animals tab). Each row is a (kind, count) stack; the total is capped by
        // FCSettings.maxAnimalSubpawns at add time. Companions fight on foot alongside the merc.
        public List<SavedAnimal> animals = new List<SavedAnimal>();

        // Rideable mount (gear-panel slot, Giddy Up 2 only). When GU2 is active and this is set, the
        // merc spawns already mounted in manual battles / on deployment. Inert when GU2 is absent.
        public PawnKindDef mount;

        public PawnKindDef pawnKind;
        public XenotypeDef xenotype;
        public string customXenotypeName;

        // Race hint that survives clone-defName remapping by BackCompatPatches.
        // RandomPawnKind() returns a runtime clone (e.g. PColony_Fighter_Milira) that
        // isn't registered in DefDatabase, so on load Scribe collapses it to the base
        // template (race=Human). Saving the race here lets PostLoadInit restore the
        // correct race-specific clone via PawnKindTemplateUtil.
        private ThingDef pawnKindRaceHint;

        // Def-based equipment storage
        public List<SavedThing> weapons = new List<SavedThing>();
        public List<SavedThing> apparel = new List<SavedThing>();
        public List<SavedThing> inventory = new List<SavedThing>();
        public List<SavedImplant> implants = new List<SavedImplant>();
        public bool HasWeapon => weapons.Any(w => w.thing != null);

        // Psycast design (Psycasts tab). psylinkLevel gates which psycasts are pickable
        // and is applied the "neuroformer way" at spawn; psycasts carry their owning psycast-system
        // provider's Key so they apply through the right system (base game / VPE) on load.
        public int psylinkLevel;
        public List<SavedPsycast> psycasts = new List<SavedPsycast>();

        // Mechanitor design (Mechs tab, Biotech only). isMechanitor auto-applies a mechlink at spawn
        // (see ApplyMechanitorToPawn); mechs lists the mechanoids bonded to the unit at deploy time,
        // each tagged with a control-group index. mechGroupWorkModes holds the work mode per control
        // group (index = group); all mechs in a group share that mode.
        public bool isMechanitor;
        public List<SavedMech> mechs = new List<SavedMech>();
        public List<MechWorkModeDef> mechGroupWorkModes = new List<MechWorkModeDef>();

        /// <summary>True when this unit is a mechanitor design — flagged as one or carrying
        /// at least one assigned mech. Gates the mechlink apply, the mech spawn block, and cost.</summary>
        public bool IsMechanitorDesign =>
            ModsConfig.BiotechActive && (isMechanitor || (mechs != null && mechs.Count > 0));

        // Forced gender for spawned pawns (null = any).
        public Gender? forcedGender;

        /// <summary>
        /// Design-level stat modifiers for this unit template (unit scope). Copied into a clone's modifier
        /// list, and seeded onto a mercenary's own statModifiers when set as their currentLoadout.
        /// Groundwork for a unit-accolade system.
        /// </summary>
        public List<PermanentStatModifier> statModifiers = new List<PermanentStatModifier>();

        public void AddStatModifier(PermanentStatModifier mod) { statModifiers.Add(mod); }
        public void RemoveStatModifiersBySource(string sourceId) { statModifiers.RemoveAll(m => m.sourceId == sourceId); }

        // Lazy preview pawn for UI rendering only — not serialized
        private Pawn previewPawn;
        private bool pawnIdentityDirty = true;    // Needs new PawnGenerator call (race/xeno change)
        private bool pawnEquipmentDirty = true;   // Needs equipment refresh on same pawn

        /* Monotonic edit counter bumped on every ChangeTick(). Tick-independent so
         * same-tick edits (and edits made while paused) are still detected by UI
         * code that wants to refresh derived state. */
        public int editVersion;

        public MilUnitFC()
        {
        }

        public MilUnitFC(bool blank)
        {
            // Defensive: FindFC.Military can be null if this is constructed mid-load before
            // FactionFC's legacy-military migration has populated it. Fall back to 0 rather than NRE.
            MilitaryFC mil = FindFC.Military;
            loadID = mil is object ? mil.NextUnitId() : 0;
            isBlank = blank;
            equipmentTotalCost = 0;

            try
            {
                Faction playerFaction = FindFC.EmpireFaction;
                if (playerFaction != null && playerFaction.def.pawnGroupMakers.Any() &&
                    playerFaction.def.pawnGroupMakers.Any(pgm => pgm.options?.Any() == true))
                {
                    pawnKind = playerFaction.RandomPawnKind();
                }
                else
                {
                    var pColonyDef = DefDatabase<FactionDef>.GetNamed("PColony");
                    if (pColonyDef?.pawnGroupMakers?.Any(pgm => pgm.options?.Any() == true) == true)
                    {
                        pawnKind = pColonyDef.pawnGroupMakers.RandomElement().options.RandomElement().kind;
                    }
                    else
                    {
                        pawnKind = PawnKindDefOf.Colonist;
                    }
                }

                if (!pawnKind.ValidPawnKindDef())
                {
                    LogUtil.Warning($"MilUnitFC: selected pawnKind failed validation. Falling back to Colonist.");
                    pawnKind = PawnKindDefOf.Colonist;
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"Error creating MilUnitFC: {ex.Message}");
                pawnKind = PawnKindDefOf.Colonist;
            }

            if (!isBlank)
            {
                xenotype = XenotypeDefOf.Baseliner;
            }
        }

        public string GetUniqueLoadID()
        {
            return $"MilUnitFC_{loadID}";
        }

        // --- Xenotype Helpers ---

        public bool IsCustomXenotype => customXenotypeName != null;

        public CustomXenotype ResolveCustomXenotype()
        {
            if (customXenotypeName == null) return null;
            CustomXenotype result = null;
            FactionCache.CustomXenotypesDecoder?.TryGetValue(customXenotypeName, out result);
            return result;
        }

        public List<GeneDef> GetXenotypeGenes()
        {
            if (xenotype != null) return xenotype.genes;
            CustomXenotype custom = ResolveCustomXenotype();
            if (custom != null) return custom.genes;
            return null;
        }

        public string GetXenotypeLabel()
        {
            if (xenotype != null) return xenotype.label.CapitalizeFirst();
            if (customXenotypeName != null) return customXenotypeName.CapitalizeFirst();
            return "None";
        }

        public Texture2D GetXenotypeIcon()
        {
            if (xenotype != null) return xenotype.Icon;
            CustomXenotype custom = ResolveCustomXenotype();
            if (custom != null) return custom.IconDef.Icon;
            return null;
        }

        public void SetXenotype(XenotypeDef def)
        {
            xenotype = def;
            customXenotypeName = null;
            pawnIdentityDirty = true;
            pawnEquipmentDirty = true;
            RevalidateImplants();
            ChangeTick();
            MilSquadFC.UpdateEquipmentTotalCostOfSquadsContaining(this);
        }

        public void SetCustomXenotype(CustomXenotype custom)
        {
            xenotype = null;
            customXenotypeName = custom.name;
            FactionCache.EnsureInGameDatabase(custom);
            pawnIdentityDirty = true;
            pawnEquipmentDirty = true;
            RevalidateImplants();
            ChangeTick();
            MilSquadFC.UpdateEquipmentTotalCostOfSquadsContaining(this);
        }

        /// <summary>
        /// Marks the preview pawn for a full regeneration (new PawnGenerator call). Use when
        /// identity-affecting state changes (race, xenotype, gender, implants) — distinct from
        /// <see cref="MarkEquipmentDirty"/>, which only re-applies apparel/weapons.
        /// </summary>
        public void MarkIdentityDirty()
        {
            pawnIdentityDirty = true;
            pawnEquipmentDirty = true;
        }

        public virtual void ExposeData()
        {
            Scribe_Values.Look(ref loadID, "loadID");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref isBlank, "blank");
            Scribe_Values.Look(ref equipmentTotalCost, "equipmentTotalCost", -1);
            Scribe_Values.Look(ref tickChanged, "tickChanged");
            Scribe_Defs.Look(ref pawnKind, "PawnKind");
            Scribe_Collections.Look(ref animals, "animals", LookMode.Deep);
            Scribe_Defs.Look(ref mount, "mount");
            // Migrate pre-multi-animal saves: the old single companion lived under "animal".
            // It was a companion (not a mount), so it migrates into the companions list.
            PawnKindDef legacyAnimal = null;
            Scribe_Defs.Look(ref legacyAnimal, "animal");
            if (Scribe.mode == LoadSaveMode.LoadingVars && legacyAnimal != null)
            {
                if (animals == null) animals = new List<SavedAnimal>();
                if (animals.Count == 0) animals.Add(new SavedAnimal(legacyAnimal, 1));
            }
            Scribe_Defs.Look(ref xenotype, "xenotype");
            Scribe_Values.Look(ref customXenotypeName, "customXenotypeName");

            if (Scribe.mode == LoadSaveMode.Saving)
                pawnKindRaceHint = pawnKind?.race;
            Scribe_Defs.Look(ref pawnKindRaceHint, "pawnKindRace");

            // Def-based equipment storage
            Scribe_Collections.Look(ref weapons, "weapons", LookMode.Deep);
            Scribe_Collections.Look(ref apparel, "apparel", LookMode.Deep);
            Scribe_Collections.Look(ref inventory, "inventory", LookMode.Deep);
            Scribe_Collections.Look(ref implants, "implants", LookMode.Deep);
            Scribe_Collections.Look(ref statModifiers, "statModifiers", LookMode.Deep);

            // Psycast design
            Scribe_Values.Look(ref psylinkLevel, "psylinkLevel", 0);
            Scribe_Collections.Look(ref psycasts, "psycasts", LookMode.Deep);

            // Mechanitor design
            Scribe_Values.Look(ref isMechanitor, "isMechanitor", false);
            Scribe_Collections.Look(ref mechs, "mechs", LookMode.Deep);
            Scribe_Collections.Look(ref mechGroupWorkModes, "mechGroupWorkModes", LookMode.Def);

            // forcedGender nullable — save only if set
            bool hasGender = forcedGender.HasValue;
            Gender genderVal = forcedGender ?? Gender.None;
            Scribe_Values.Look(ref hasGender, "hasForcedGender", false);
            if (hasGender)
                Scribe_Values.Look(ref genderVal, "forcedGender", Gender.None);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                forcedGender = hasGender ? genderVal : (Gender?)null;

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (weapons == null) weapons = new List<SavedThing>();
                if (apparel == null) apparel = new List<SavedThing>();
                if (inventory == null) inventory = new List<SavedThing>();
                if (implants == null) implants = new List<SavedImplant>();
                if (psycasts == null) psycasts = new List<SavedPsycast>();
                if (mechs == null) mechs = new List<SavedMech>();
                if (mechGroupWorkModes == null) mechGroupWorkModes = new List<MechWorkModeDef>();
                if (animals == null) animals = new List<SavedAnimal>();
                if (statModifiers == null) statModifiers = new List<PermanentStatModifier>();
                // Mutual exclusivity: prefer XenotypeDef if both are set
                if (xenotype != null && customXenotypeName != null)
                    customXenotypeName = null;

                RestoreClonedPawnKindIfRemapped();
            }
        }

        /// <summary>
        /// If pawnKind was a runtime clone (e.g. PColony_Fighter_Milira) when saved,
        /// Scribe's cross-ref resolution will have collapsed it to the base template
        /// (PColony_Fighter, race=Human) via BackCompatPatches. Use the saved race
        /// hint to re-fetch the correct race-specific clone.
        /// </summary>
        private void RestoreClonedPawnKindIfRemapped()
        {
            if (pawnKind is null || pawnKindRaceHint is null) return;
            if (pawnKind.race == pawnKindRaceHint) return;
            if (!PawnKindTemplateUtil.TryRestoreRaceSpecificClone(ref pawnKind, pawnKindRaceHint))
            {
                LogUtil.Warning($"MilUnitFC '{name}': could not restore race-specific clone for race '{pawnKindRaceHint.defName}' on template '{pawnKind.defName}'.");
            }
        }

        // --- Preview Pawn (UI only) ---

        public Pawn PreviewPawn
        {
            get
            {
                if (previewPawn == null || pawnIdentityDirty)
                    RebuildPreviewPawn();
                else if (pawnEquipmentDirty)
                    RefreshPreviewEquipment();
                return previewPawn;
            }
        }

        public void MarkEquipmentDirty()
        {
            pawnEquipmentDirty = true;
        }

        private void RebuildPreviewPawn()
        {
            try
            {
                if (previewPawn != null)
                {
                    previewPawn.apparel?.DestroyAll();
                    previewPawn.equipment?.DestroyAllEquipment();
                    previewPawn.Destroy();
                }

                previewPawn = FCPawnGenerator.GenerateWithForcedXenotype(FCPawnGenerator.WorkerOrMilitaryRequestForUnit(this));

                if (previewPawn != null && previewPawn.Faction == null)
                {
                    Faction empireFaction = FindFC.EmpireFaction;
                    if (empireFaction != null)
                        previewPawn.SetFaction(empireFaction);
                }

                // Implants change the pawn's health/identity, so they are applied here at
                // generation time (not in RefreshPreviewEquipment, which runs on equipment-only
                // changes and would otherwise double-install).
                if (previewPawn != null)
                {
                    ApplyImplantsToPawn(previewPawn, this);
                    ApplyPsycastsToPawn(previewPawn, this);
                    ApplyMechanitorToPawn(previewPawn, this);
                }
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"Failed to generate preview pawn for {name}: {ex.Message}");
                previewPawn = null;
            }

            pawnIdentityDirty = false;

            if (previewPawn == null)
            {
                pawnEquipmentDirty = false;
                return;
            }

            previewPawn.mindState.canFleeIndividual = false;
            RefreshPreviewEquipment();
        }

        protected virtual void RefreshPreviewEquipment()
        {
            if (previewPawn == null) return;
            ApplyEquipmentToPawn(previewPawn, this);
            pawnEquipmentDirty = false;
        }

        /* Strips and re-equips the target pawn from this unit's apparel/weapons.
         * Used by both the lazy preview pawn and Dialog_PawnLoadout's cloned-pawn
         * preview. Apparel colors are resolved via the player faction's color rules. */
        public static void ApplyEquipmentToPawn(Pawn target, MilUnitFC source)
        {
            if (target is null || source is null) return;

            target.apparel?.DestroyAll();
            target.equipment?.DestroyAllEquipment();

            FactionFC factionComp = FindFC.FactionComp;
            if (source.apparel != null && target.apparel != null)
            {
                foreach (SavedThing a in source.apparel)
                {
                    Thing t = a.CreateThing();
                    if (t is Apparel ap)
                    {
                        Color resolved = factionComp != null ? factionComp.ResolveApparelColor(a) : Color.white;
                        t.SetColor(resolved, reportFailure: false);
                        target.apparel.Wear(ap);
                    }
                }
            }

            if (source.weapons != null && target.equipment != null)
            {
                foreach (SavedThing w in source.weapons)
                {
                    Thing wt = w.CreateThing();
                    if (wt is ThingWithComps twc)
                        target.equipment.AddEquipment(twc);
                }
            }
        }

        /* Installs this unit's chosen implants on the target pawn. Surgery implants reuse the base
         * game's own validation/application (Recipe_InstallImplant.ApplyOnPawn with a null billDoer
         * skips the fail/tale path and adds the hediff); the concrete BodyPartRecord is resolved from
         * the stored (recipe, index) against the target's body via GetPartsToApplyOn. Self-install
         * implants (control sublinks etc., CompUseEffect_InstallImplant items) replicate that comp's
         * leveled DoEffect — add the hediff, or upgrade an existing leveled one. Entries are applied in
         * list order, so a leveled implant added N times reaches level N. Invalid entries are silently
         * skipped. Used by the preview pawn and the real spawned pawn. */
        public static void ApplyImplantsToPawn(Pawn target, MilUnitFC source)
        {
            if (target is null || source?.implants is null) return;
            if (target.health is null || target.Dead || target.Destroyed) return;

            foreach (SavedImplant im in source.implants)
            {
                try
                {
                    if (im.recipe is object)
                    {
                        BodyPartRecord part;
                        if (TryResolveImplant(target, im, out part))
                            im.recipe.Worker.ApplyOnPawn(target, part, null, null, null);
                    }
                    else if (im.selfInstallThing is object)
                    {
                        ApplySelfInstallImplant(target, im.selfInstallThing);
                    }
                }
                catch (Exception ex)
                {
                    string label = im.recipe?.defName ?? im.selfInstallThing?.defName;
                    LogUtil.Warning($"Failed to apply implant {label} to {target.LabelShortCap}: {ex.Message}");
                }
            }
        }

        /* Installs a self-install implant by invoking the item's own
         * CompUseEffect_InstallImplant.DoEffect — the same add/leveled-upgrade path the base game uses
         * when a pawn uses the item — so our behavior stays in lockstep with it. A throwaway (unspawned)
         * item instance is enough: the comp only reads its Props and mutates the pawn. (DoEffect has no
         * colonist/faction gate, unlike CanBeUsedBy, so it works on Empire's NPC pawns.) */
        private static void ApplySelfInstallImplant(Pawn target, ThingDef thing)
        {
            ThingWithComps item = ThingMaker.MakeThing(thing) as ThingWithComps;
            CompUseEffect_InstallImplant comp = item?.TryGetComp<CompUseEffect_InstallImplant>();
            if (comp is null)
            {
                if (item is object && !item.Destroyed) item.Destroy();
                return;
            }
            try { comp.DoEffect(target); }
            catch (Exception ex) { LogUtil.Error("ApplySelfInstallImplant: error in CompUseEffect_InstallImplant: " + ex); }
            finally { if (!item.Destroyed) item.Destroy(); }
        }

        /* Applies this unit's psylink level + chosen psycasts to the target pawn, dispatching to the
         * psycast-system provider each psycast was designed under (base game / VPE). Psylink is granted
         * the "neuroformer way" (a PsychicAmplifier hediff), which is what lets VPE's own Harmony patches
         * pick up the level and attach its psycast tracker. Each entry is wrapped so an absent provider
         * (e.g. template made with VPE, loaded without it) or an invalid def is skipped, not fatal.
         * Used by the preview pawn and the real spawned pawn. */
        public static void ApplyPsycastsToPawn(Pawn target, MilUnitFC source)
        {
            if (target?.health is null || target.Dead || target.Destroyed) return;
            if (source is null) return;

            if (source.psylinkLevel > 0)
            {
                IPsycastSystemProvider active = PsycastSystemRegistry.Active;
                if (active is object)
                {
                    try { active.ApplyPsylink(target, source.psylinkLevel); }
                    catch (Exception ex)
                    {
                        LogUtil.Warning($"Failed to apply psylink {source.psylinkLevel} to {target.LabelShortCap}: {ex.Message}");
                    }
                }
            }

            if (source.psycasts is null) return;
            foreach (SavedPsycast a in source.psycasts)
            {
                IPsycastSystemProvider provider = PsycastSystemRegistry.ByKey(a.systemKey);
                if (provider is null) continue; // originating system not loaded — skip silently
                try { provider.GrantPsycast(target, a); }
                catch (Exception ex)
                {
                    LogUtil.Warning($"Failed to grant psycast {a.psycastDef} ({a.systemKey}) to {target.LabelShortCap}: {ex.Message}");
                }
            }
        }

        /* Makes the target pawn a mechanitor for a mechanitor design: installs the mechlink
         * (MechlinkImplant hediff) and refreshes the pawn's dynamic components so pawn.mechanitor
         * exists. The bonded mechs themselves are NOT created here (no mechs exist at preview/generate
         * time): SquadEquipmentTracker creates and bonds them at outfit time, once this pawn already
         * has a mechanitor tracker. No-op when Biotech is absent, the unit isn't a mechanitor design,
         * or the pawn can't be a mechanitor (non-humanlike). Used by the preview pawn and the real
         * spawned pawn. */
        public static void ApplyMechanitorToPawn(Pawn target, MilUnitFC source)
        {
            if (!ModsConfig.BiotechActive) return;
            if (target?.health is null || target.Dead || target.Destroyed) return;
            if (source is null || !source.IsMechanitorDesign) return;
            if (target.RaceProps is null || !target.RaceProps.Humanlike) return;

            try
            {
                if (!target.health.hediffSet.HasHediff(HediffDefOf.MechlinkImplant))
                    target.health.AddHediff(HediffDefOf.MechlinkImplant);
                // Empire is an allied NPC faction, not the player, so MechanitorUtility.ShouldBeMechanitor
                // is false for these pawns — PawnComponentsUtility.AddAndRemoveDynamicComponents would
                // refuse to create the tracker (and would even null an existing one). Create it directly.
                if (target.mechanitor is null)
                    target.mechanitor = new Pawn_MechanitorTracker(target);
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"Failed to apply mechlink to {target.LabelShortCap}: {ex.Message}");
            }
        }

        /* Resolves the body part a stored implant should install on, and whether it's currently
         * installable. The part is located by its STABLE occurrence index within body.AllParts —
         * NOT an index into the filtered GetPartsToApplyOn list, which shifts with the pawn's
         * health state (battle injuries, other implants). This is what lets an implant chosen in
         * the designer (against a clean preview) install reliably on a real, possibly battle-worn
         * pawn. Validity (part present, slot free, recipe-compatible, AvailableOnNow) is still
         * delegated to the base game. Returns false (skip) for whole-body implants already present
         * or parts that aren't a legal target right now. */
        private static bool TryResolveImplant(Pawn pawn, SavedImplant im, out BodyPartRecord part)
        {
            part = null;
            if (pawn?.health is null || im.recipe?.addsHediff is null) return false;

            if (!im.recipe.targetsBodyPart)
            {
                if (pawn.health.hediffSet.HasHediff(im.recipe.addsHediff)) return false;
                return im.recipe.Worker.AvailableOnNow(pawn, null);
            }

            if (im.bodyPart is null) return false;
            part = NthBodyPartOfDef(pawn, im.bodyPart, im.bodyPartIndex);
            if (part is null) return false;

            // The resolved part must be a legal target for this recipe right now (not missing,
            // slot not already filled, no incompatible hediff) — GetPartsToApplyOn enforces that.
            bool legalTarget = false;
            foreach (BodyPartRecord p in im.recipe.Worker.GetPartsToApplyOn(pawn, im.recipe))
            {
                if (p == part) { legalTarget = true; break; }
            }
            if (!legalTarget) return false;

            return im.recipe.Worker.AvailableOnNow(pawn, part);
        }

        /// <summary>Occurrence index of <paramref name="part"/> among same-def parts in the body's
        /// AllParts order. Stable regardless of installed hediffs. -1 if not found.</summary>
        public static int BodyPartOccurrenceIndex(Pawn pawn, BodyPartRecord part)
        {
            if (pawn?.RaceProps?.body is null || part is null) return -1;
            int occ = 0;
            foreach (BodyPartRecord p in pawn.RaceProps.body.AllParts)
            {
                if (p == part) return occ;
                if (p.def == part.def) occ++;
            }
            return -1;
        }

        private static BodyPartRecord NthBodyPartOfDef(Pawn pawn, BodyPartDef def, int n)
        {
            if (pawn?.RaceProps?.body is null || def is null) return null;
            int occ = 0;
            foreach (BodyPartRecord p in pawn.RaceProps.body.AllParts)
            {
                if (p.def == def)
                {
                    if (occ == n) return p;
                    occ++;
                }
            }
            return null;
        }

        /* Removes the given implants from a pawn, restoring the natural body part. Borrows the
         * base game's Pawn_HealthTracker.RestorePart (the same call behind the dev "heal" action
         * and surgery) which recursively strips every hediff off the part and its children — so
         * the added bionic part disappears and the natural part comes back. The actual installed
         * hediff is located by its addsHediff def (not by stored index, which shifts once a slot
         * is filled). Whole-body implants (no targeted part) are removed directly. */
        public static void RemoveImplantsFromPawn(Pawn target, List<SavedImplant> implants)
        {
            if (target?.health is null || implants is null) return;
            if (target.Dead || target.Destroyed) return;

            foreach (SavedImplant im in implants)
            {
                // The hediff to remove: surgery implants add recipe.addsHediff; self-install items
                // add their comp's hediffDef (a leveled hediff removed whole, so a re-apply rebuilds
                // its level from scratch).
                HediffDef addsHediff = im.recipe?.addsHediff
                    ?? im.selfInstallThing?.GetCompProperties<CompProperties_UseEffectInstallImplant>()?.hediffDef;
                if (addsHediff is null) continue;
                try
                {
                    Hediff found = null;
                    List<Hediff> hediffs = target.health.hediffSet.hediffs;
                    for (int i = 0; i < hediffs.Count; i++)
                    {
                        if (hediffs[i].def == addsHediff) { found = hediffs[i]; break; }
                    }
                    if (found is null) continue;

                    if (found.Part != null && im.recipe is object)
                        target.health.RestorePart(found.Part);
                    else
                        target.health.RemoveHediff(found);
                }
                catch (Exception ex)
                {
                    string label = im.recipe?.defName ?? im.selfInstallThing?.defName;
                    LogUtil.Warning($"Failed to remove implant {label} from {target.LabelShortCap}: {ex.Message}");
                }
            }
        }

        /* Brings a live pawn's installed implants in line with the desired loadout WITHOUT
         * regenerating the pawn (identity preserved): clears the previously-applied set back to a
         * clean body via RestorePart, then re-applies the desired set on the clean base — which is
         * exactly the index semantics the designer preview uses, so additions and removals are both
         * handled. Used by the squad-upgrade paths when only implants changed. */
        public static void ReconcileImplantsOnPawn(Pawn target, MilUnitFC desired, MilUnitFC previous)
        {
            if (target?.health is null || target.Dead || target.Destroyed) return;
            if (previous != null) RemoveImplantsFromPawn(target, previous.implants);
            if (desired != null) ApplyImplantsToPawn(target, desired);
        }

        /* Brings a live pawn's psylink + psycasts in line with the desired loadout WITHOUT regenerating
         * the pawn (identity preserved). Delegated to the active psycast system, which reconciles in the
         * way that suits it: base game adjusts the psylink level granularly (keeping existing random
         * psycasts, adding/stripping only the delta), while VPE wipes and re-applies its deterministic
         * set. Used by the squad-upgrade paths when psycasts change (see LoadoutUpgradeUtil.PsycastsChanged). */
        public static void ReconcilePsycastsOnPawn(Pawn target, MilUnitFC desired)
        {
            if (target?.health is null || target.Dead || target.Destroyed) return;
            PsycastSystemRegistry.Active?.ReconcilePsycasts(target, desired);
        }

        // --- Equipment Mutation Methods ---

        public void ChangeTick()
        {
            tickChanged = Find.TickManager.TicksGame;
            editVersion++;
            costDirty = true;
        }

        public void SetWeapon(ThingDef def, ThingDef stuff, QualityCategory? quality = null)
        {
            weapons.Clear();
            weapons.Add(new SavedThing(def, stuff, quality));
            pawnEquipmentDirty = true;
            ChangeTick();
            MilSquadFC.UpdateEquipmentTotalCostOfSquadsContaining(this);
        }

        public void ClearWeapon()
        {
            weapons.Clear();
            pawnEquipmentDirty = true;
            ChangeTick();
            MilSquadFC.UpdateEquipmentTotalCostOfSquadsContaining(this);
        }

        public void SetApparel(ThingDef def, ThingDef stuff, QualityCategory? quality = null)
        {
            // Remove conflicting apparel using RimWorld's static check
            BodyDef body = pawnKind?.race?.race?.body ?? BodyDefOf.Human;
            apparel.RemoveAll(existing =>
                !ApparelUtility.CanWearTogether(existing.thing, def, body));
            apparel.Add(new SavedThing(def, stuff, quality));
            pawnEquipmentDirty = true;
            ChangeTick();
            MilSquadFC.UpdateEquipmentTotalCostOfSquadsContaining(this);
        }

        public void RemoveApparel(ThingDef def)
        {
            apparel.RemoveAll(s => s.thing == def);
            pawnEquipmentDirty = true;
            ChangeTick();
            MilSquadFC.UpdateEquipmentTotalCostOfSquadsContaining(this);
        }

        public virtual void ClearAllEquipment()
        {
            weapons.Clear();
            apparel.Clear();
            inventory.Clear();
            implants.Clear();
            psycasts.Clear();
            psylinkLevel = 0;
            mechs.Clear();
            isMechanitor = false;
            pawnEquipmentDirty = true;
            pawnIdentityDirty = true; // implants/psylink/mechlink cleared — preview pawn must regenerate
            ChangeTick();
            MilSquadFC.UpdateEquipmentTotalCostOfSquadsContaining(this);
        }

        // --- Inventory ---

        /* Adds (or tops up) a carried inventory entry. Hard-blocks the add when it would push
         * total carried mass over the unit's 70% carry cap (see CarryCapacity) — the player can
         * never overload a designed unit. Returns false (with a message) when rejected. */
        public bool AddInventory(ThingDef def, ThingDef stuff, int count, QualityCategory? quality = null)
        {
            if (def is null || count <= 0) return false;

            float addedMass = MassOf(def, stuff) * count;
            if (CurrentInventoryMass + addedMass > CarryCapacity + 0.0001f)
            {
                Messages.Message("fcInventoryOverweight".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            // Merge with an existing matching row (same thing + stuff + quality).
            for (int i = 0; i < inventory.Count; i++)
            {
                SavedThing existing = inventory[i];
                if (existing.thing == def && existing.stuff == stuff && existing.quality == quality)
                {
                    existing.count = Mathf.Max(1, existing.count) + count;
                    inventory[i] = existing;
                    pawnEquipmentDirty = true;
                    ChangeTick();
                    MilSquadFC.UpdateEquipmentTotalCostOfSquadsContaining(this);
                    return true;
                }
            }

            inventory.Add(new SavedThing(def, stuff, count, quality));
            pawnEquipmentDirty = true;
            ChangeTick();
            MilSquadFC.UpdateEquipmentTotalCostOfSquadsContaining(this);
            return true;
        }

        public void SetInventoryCount(int index, int count)
        {
            if (index < 0 || index >= inventory.Count) return;
            if (count <= 0) { RemoveInventory(index); return; }

            SavedThing item = inventory[index];
            // Compute prospective mass excluding this row, then re-add at the requested count.
            float massWithout = CurrentInventoryMass - MassOf(item.thing, item.stuff) * Mathf.Max(1, item.count);
            float prospective = massWithout + MassOf(item.thing, item.stuff) * count;
            if (prospective > CarryCapacity + 0.0001f)
            {
                Messages.Message("fcInventoryOverweight".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            item.count = count;
            inventory[index] = item;
            pawnEquipmentDirty = true;
            ChangeTick();
            MilSquadFC.UpdateEquipmentTotalCostOfSquadsContaining(this);
        }

        public void RemoveInventory(int index)
        {
            if (index < 0 || index >= inventory.Count) return;
            inventory.RemoveAt(index);
            pawnEquipmentDirty = true;
            ChangeTick();
            MilSquadFC.UpdateEquipmentTotalCostOfSquadsContaining(this);
        }

        private static float MassOf(ThingDef def, ThingDef stuff)
        {
            if (def is null) return 0f;
            return def.GetStatValueAbstract(StatDefOf.Mass, stuff);
        }

        /// <summary>Carry cap = 70% of a typical pawn-of-this-xenotype's carrying capacity.</summary>
        public float CarryCapacity
        {
            get
            {
                Pawn p = PreviewPawn;
                return p != null ? MassUtility.Capacity(p) * 0.7f : 0f;
            }
        }

        /// <summary>Total carried mass counted against the cap: inventory items plus the equipped
        /// weapon(s). Worn apparel is excluded (it's worn, not carried).</summary>
        public float CurrentInventoryMass
        {
            get
            {
                float total = 0f;
                foreach (SavedThing i in inventory)
                    total += MassOf(i.thing, i.stuff) * Mathf.Max(1, i.count);
                foreach (SavedThing w in weapons)
                    total += MassOf(w.thing, w.stuff) * Mathf.Max(1, w.count);
                return total;
            }
        }

        // --- Implants ---

        public void AddImplant(RecipeDef recipe, BodyPartDef bodyPart, int bodyPartIndex)
        {
            if (recipe is null) return;
            implants.Add(new SavedImplant(recipe, bodyPart, bodyPartIndex));
            MarkIdentityDirty();
            ChangeTick();
            MilSquadFC.UpdateEquipmentTotalCostOfSquadsContaining(this);
        }

        /// <summary>Adds a self-install implant (e.g. a control sublink) — a CompUseEffect_InstallImplant
        /// item rather than a surgery recipe. Added once per "install"; a leveled implant added N times
        /// reaches level N at apply time.</summary>
        public void AddImplant(ThingDef selfInstallThing, BodyPartDef bodyPart, int bodyPartIndex)
        {
            if (selfInstallThing is null) return;
            implants.Add(new SavedImplant(selfInstallThing, bodyPart, bodyPartIndex));
            MarkIdentityDirty();
            ChangeTick();
            MilSquadFC.UpdateEquipmentTotalCostOfSquadsContaining(this);
        }

        public void RemoveImplant(int index)
        {
            if (index < 0 || index >= implants.Count) return;
            implants.RemoveAt(index);
            MarkIdentityDirty();
            ChangeTick();
            MilSquadFC.UpdateEquipmentTotalCostOfSquadsContaining(this);
        }

        // --- Mechanitor / mechs (Biotech) ---

        /// <summary>Number of control groups this design has available — the preview pawn's
        /// MechControlGroups stat (mechlink grants 2; control-sublink implants add more). At least 1
        /// while the unit is a mechanitor; 0 otherwise.</summary>
        public int MechGroupCount
        {
            get
            {
                if (!ModsConfig.BiotechActive) return 0;
                Pawn p = PreviewPawn;
                if (p?.mechanitor is null) return 0;
                return Mathf.Max(1, (int)p.GetStatValue(StatDefOf.MechControlGroups));
            }
        }

        /// <summary>Work mode for a given control group, defaulting to Escort
        /// (follow + fight near the mechanitor) when none was chosen.</summary>
        public MechWorkModeDef GetGroupWorkMode(int group)
        {
            if (mechGroupWorkModes != null && group >= 0 && group < mechGroupWorkModes.Count
                && mechGroupWorkModes[group] != null)
                return mechGroupWorkModes[group];
            return MechWorkModeDefOf.Escort;
        }

        public void SetGroupWorkMode(int group, MechWorkModeDef mode)
        {
            if (group < 0) return;
            if (mechGroupWorkModes is null) mechGroupWorkModes = new List<MechWorkModeDef>();
            while (mechGroupWorkModes.Count <= group) mechGroupWorkModes.Add(MechWorkModeDefOf.Escort);
            mechGroupWorkModes[group] = mode;
            ChangeTick();
        }

        /// <summary>Moves a mech row to a different control group, merging with an existing same-kind
        /// row already in that group.</summary>
        public void SetMechGroup(int index, int group)
        {
            if (index < 0 || index >= mechs.Count || group < 0) return;
            SavedMech row = mechs[index];
            if (row.group == group) return;

            // Merge into an existing same-kind row in the destination group.
            for (int i = 0; i < mechs.Count; i++)
            {
                if (i == index) continue;
                if (mechs[i].kind == row.kind && mechs[i].group == group)
                {
                    SavedMech dest = mechs[i];
                    dest.count = Mathf.Max(1, dest.count) + Mathf.Max(1, row.count);
                    mechs[i] = dest;
                    mechs.RemoveAt(index);
                    ChangeTick();
                    return;
                }
            }

            row.group = group;
            mechs[index] = row;
            ChangeTick();
        }

        /// <summary>Total mech bandwidth this design has to spend. Read from the preview pawn's
        /// MechBandwidth stat (which only exists once the mechlink is applied), so control-sublink
        /// implants and bandwidth-pack apparel already on the unit are folded in automatically.</summary>
        public float TotalMechBandwidth
        {
            get
            {
                if (!ModsConfig.BiotechActive) return 0f;
                Pawn p = PreviewPawn;
                return p?.mechanitor != null ? p.GetStatValue(StatDefOf.MechBandwidth) : 0f;
            }
        }

        /// <summary>Bandwidth consumed by the currently-assigned mechs (Σ BandwidthCost × count).</summary>
        public float UsedMechBandwidth
        {
            get
            {
                float used = 0f;
                if (mechs is null) return used;
                foreach (SavedMech m in mechs)
                    if (m.kind?.race != null)
                        used += m.kind.race.GetStatValueAbstract(StatDefOf.BandwidthCost) * Mathf.Max(1, m.count);
                return used;
            }
        }

        /// <summary>Toggles the mechanitor flag. Turning it off clears the assigned mechs. Identity
        /// changes (the mechlink hediff), so the preview pawn must regenerate.</summary>
        public void SetMechanitor(bool on)
        {
            if (!ModsConfig.BiotechActive) return;
            isMechanitor = on;
            if (!on) mechs.Clear();
            MarkIdentityDirty();
            ChangeTick();
            MilSquadFC.UpdateEquipmentTotalCostOfSquadsContaining(this);
        }

        /// <summary>Adds (or tops up) a mech assignment in the given control group. Hard-blocks the add
        /// when it would push used bandwidth over the unit's total (bandwidth is counted across all
        /// groups). Returns false (with a message) when rejected.</summary>
        public bool AddMech(PawnKindDef kind, int group = 0, int count = 1)
        {
            if (!ModsConfig.BiotechActive || kind?.race is null || count <= 0) return false;
            if (group < 0) group = 0;

            // Only mechs the player has researched can be assigned.
            if (!FactionCache.IsMechResearchUnlocked(kind))
            {
                Messages.Message("fcMechNotResearched".Translate(kind.LabelCap), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            // Flag as a mechanitor first so TotalMechBandwidth's preview pawn carries the mechlink
            // (and thus a real MechBandwidth stat) when we read it for the budget check below.
            if (!isMechanitor)
            {
                isMechanitor = true;
                MarkIdentityDirty();
            }

            float addedBandwidth = kind.race.GetStatValueAbstract(StatDefOf.BandwidthCost) * count;
            if (UsedMechBandwidth + addedBandwidth > TotalMechBandwidth + 0.0001f)
            {
                Messages.Message("fcMechBandwidthExceeded".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            // Merge with an existing row of the same kind AND group.
            for (int i = 0; i < mechs.Count; i++)
            {
                if (mechs[i].kind == kind && mechs[i].group == group)
                {
                    SavedMech existing = mechs[i];
                    existing.count = Mathf.Max(1, existing.count) + count;
                    mechs[i] = existing;
                    MarkIdentityDirty();
                    ChangeTick();
                    MilSquadFC.UpdateEquipmentTotalCostOfSquadsContaining(this);
                    return true;
                }
            }

            mechs.Add(new SavedMech(kind, count, group));
            MarkIdentityDirty();
            ChangeTick();
            MilSquadFC.UpdateEquipmentTotalCostOfSquadsContaining(this);
            return true;
        }

        public void RemoveMech(int index)
        {
            if (index < 0 || index >= mechs.Count) return;
            mechs.RemoveAt(index);
            MarkIdentityDirty();
            ChangeTick();
            MilSquadFC.UpdateEquipmentTotalCostOfSquadsContaining(this);
        }

        /// <summary>Reduces a mech row's count by one, removing the row when it reaches zero.</summary>
        public void DecrementMech(int index)
        {
            if (index < 0 || index >= mechs.Count) return;
            SavedMech row = mechs[index];
            if (row.count <= 1) { RemoveMech(index); return; }
            row.count -= 1;
            mechs[index] = row;
            MarkIdentityDirty();
            ChangeTick();
            MilSquadFC.UpdateEquipmentTotalCostOfSquadsContaining(this);
        }

        // --- Companion animals (Animals tab) ---

        /// <summary>Total companion animals across all stack rows (sum of counts).</summary>
        public int TotalAnimalCount
        {
            get
            {
                if (animals == null) return 0;
                int total = 0;
                foreach (SavedAnimal a in animals) total += Mathf.Max(1, a.count);
                return total;
            }
        }

        /// <summary>Adds (or tops up) a companion-animal stack. Hard-blocks the add when it would push
        /// the total over FCSettings.maxAnimalSubpawns. Returns false (with a message) when rejected.
        /// Unlike mechs, animals don't change the unit's own identity, so no MarkIdentityDirty.</summary>
        public bool AddAnimal(PawnKindDef kind, int count = 1)
        {
            if (kind?.race == null || count <= 0) return false;
            if (animals == null) animals = new List<SavedAnimal>();

            if (TotalAnimalCount + count > FCSettings.maxAnimalSubpawns)
            {
                Messages.Message("fcAnimalCapReached".Translate(FCSettings.maxAnimalSubpawns), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            // Merge with an existing row of the same kind.
            for (int i = 0; i < animals.Count; i++)
            {
                if (animals[i].kind == kind)
                {
                    SavedAnimal existing = animals[i];
                    existing.count = Mathf.Max(1, existing.count) + count;
                    animals[i] = existing;
                    ChangeTick();
                    MilSquadFC.UpdateEquipmentTotalCostOfSquadsContaining(this);
                    return true;
                }
            }

            animals.Add(new SavedAnimal(kind, count));
            ChangeTick();
            MilSquadFC.UpdateEquipmentTotalCostOfSquadsContaining(this);
            return true;
        }

        public void RemoveAnimal(int index)
        {
            if (animals == null || index < 0 || index >= animals.Count) return;
            animals.RemoveAt(index);
            ChangeTick();
            MilSquadFC.UpdateEquipmentTotalCostOfSquadsContaining(this);
        }

        /// <summary>Reduces a companion row's count by one, removing the row when it reaches zero.</summary>
        public void DecrementAnimal(int index)
        {
            if (animals == null || index < 0 || index >= animals.Count) return;
            SavedAnimal row = animals[index];
            if (row.count <= 1) { RemoveAnimal(index); return; }
            row.count -= 1;
            animals[index] = row;
            ChangeTick();
            MilSquadFC.UpdateEquipmentTotalCostOfSquadsContaining(this);
        }

        // --- Mount (Giddy Up 2) ---

        /// <summary>Sets (or clears, when kind is null) the rideable mount. Only meaningful when Giddy
        /// Up 2 is active; otherwise the field is inert and never spawns.</summary>
        public void SetMount(PawnKindDef kind)
        {
            mount = kind;
            ChangeTick();
            MilSquadFC.UpdateEquipmentTotalCostOfSquadsContaining(this);
        }

        // --- Psycasts ---

        public void SetPsylinkLevel(int level)
        {
            int max = PsycastSystemRegistry.Active?.MaxPsylinkLevel ?? 6;
            int clamped = Mathf.Clamp(level, 0, max);
            if (clamped == psylinkLevel) return;
            psylinkLevel = clamped;
            // Trim chosen selections that no longer fit the (possibly lowered) psylink level. The active
            // system owns the budget rule (VPE trims its ordered selections from the end to the point
            // budget for this level); the base game stores no selections, so this is a no-op there.
            if (psylinkLevel <= 0)
            {
                psycasts.Clear();
            }
            else
            {
                IPsycastSystemProvider active = PsycastSystemRegistry.Active;
                if (active != null)
                    psycasts = active.ClampSelectionsToBudget(psycasts, psylinkLevel);
            }
            MarkIdentityDirty(); // psylink hediff changes pawn identity
            ChangeTick();
            MilSquadFC.UpdateEquipmentTotalCostOfSquadsContaining(this);
        }

        /// <summary>
        /// Replaces all entries belonging to <paramref name="systemKey"/> with the given set,
        /// preserving entries from other systems. Used by a provider's custom editor (e.g. VPE) to
        /// write back the full chosen set — psycasts, meditation foci, stat upgrades — when its window
        /// closes. Empty entries (no defName and no kind) are dropped.
        /// </summary>
        public void SetPsycastsForSystem(string systemKey, IEnumerable<SavedPsycast> entries)
        {
            if (string.IsNullOrEmpty(systemKey)) return;
            psycasts.RemoveAll(a => a.systemKey == systemKey);
            if (entries is object)
            {
                foreach (SavedPsycast e in entries)
                {
                    if (e.systemKey != systemKey) continue;
                    if (e.IsInvalid()) continue;
                    psycasts.Add(e);
                }
            }
            MarkIdentityDirty();
            ChangeTick();
            MilSquadFC.UpdateEquipmentTotalCostOfSquadsContaining(this);
        }

        /* Drops stored implants that no longer resolve to a valid slot on the current body
         * (e.g. after a race/xenotype change removed or altered the target part). Validation
         * is delegated to the base game via a freshly-built preview pawn. */
        public void RevalidateImplants()
        {
            if (implants.Count == 0) return;

            // Force a clean preview pawn WITHOUT implants so we can test each one independently.
            pawnIdentityDirty = true;
            List<SavedImplant> snapshot = implants;
            implants = new List<SavedImplant>();
            Pawn testPawn = PreviewPawn; // rebuilt with no implants
            implants = snapshot;

            if (testPawn is null) return;

            List<SavedImplant> kept = new List<SavedImplant>();
            foreach (SavedImplant im in snapshot)
            {
                try
                {
                    if (im.recipe is object)
                    {
                        BodyPartRecord part;
                        if (!TryResolveImplant(testPawn, im, out part)) continue;

                        // Install on the test pawn so later implants validate against a cumulative body.
                        im.recipe.Worker.ApplyOnPawn(testPawn, part, null, null, null);
                        kept.Add(im);
                    }
                    else if (im.selfInstallThing is object)
                    {
                        // Self-install implants only need their target body part to still exist (the
                        // brain, for control sublinks). Apply cumulatively so leveled implants stack.
                        CompProperties_UseEffectInstallImplant inst =
                            im.selfInstallThing.GetCompProperties<CompProperties_UseEffectInstallImplant>();
                        if (inst?.bodyPart is null) continue;
                        if (!testPawn.RaceProps.body.GetPartsWithDef(inst.bodyPart).Any()) continue;
                        ApplySelfInstallImplant(testPawn, im.selfInstallThing);
                        kept.Add(im);
                    }
                }
                catch (Exception ex)
                {
                    string label = im.recipe?.defName ?? im.selfInstallThing?.defName;
                    LogUtil.Warning($"RevalidateImplants: dropping implant {label}: {ex.Message}");
                }
            }

            implants = kept;
            pawnIdentityDirty = true; // preview must rebuild with the surviving implants
        }

        // --- Gender ---

        public void SetForcedGender(Gender? gender)
        {
            forcedGender = gender;
            MarkIdentityDirty();
            ChangeTick();
        }

        /* Re-marks identity dirty and revalidates implants after a race change. Called by the
         * race picker, which writes pawnKind directly. */
        public void OnRaceChanged()
        {
            MarkIdentityDirty();
            RevalidateImplants();
            ChangeTick();
            MilSquadFC.UpdateEquipmentTotalCostOfSquadsContaining(this);
        }

        // --- Apparel Color ---

        public void SetApparelColor(ThingDef def, Color color)
        {
            for (int i = 0; i < apparel.Count; i++)
            {
                if (apparel[i].thing == def)
                {
                    SavedThing item = apparel[i];
                    item.color = color;
                    item.hasColor = true;
                    apparel[i] = item;
                    break;
                }
            }
            pawnEquipmentDirty = true;
            ChangeTick();
        }

        public void ClearApparelColor(ThingDef def)
        {
            for (int i = 0; i < apparel.Count; i++)
            {
                if (apparel[i].thing == def)
                {
                    SavedThing item = apparel[i];
                    item.color = Color.white;
                    item.hasColor = false;
                    apparel[i] = item;
                    break;
                }
            }
            pawnEquipmentDirty = true;
            ChangeTick();
        }

        public void SetAllApparelColors(Color color)
        {
            for (int i = 0; i < apparel.Count; i++)
            {
                SavedThing item = apparel[i];
                item.color = color;
                item.hasColor = true;
                apparel[i] = item;
            }
            pawnEquipmentDirty = true;
            ChangeTick();
        }

        public void ClearAllApparelColors()
        {
            for (int i = 0; i < apparel.Count; i++)
            {
                SavedThing item = apparel[i];
                item.color = Color.white;
                item.hasColor = false;
                apparel[i] = item;
            }
            pawnEquipmentDirty = true;
            ChangeTick();
        }

        // --- Cost ---

        protected bool costDirty = true;

        public double getTotalCost
        {
            get
            {
                if (costDirty)
                {
                    UpdateEquipmentTotalCost();
                    costDirty = false;
                }
                return equipmentTotalCost;
            }
        }

        public virtual void UpdateEquipmentTotalCost()
        {
            if (isBlank)
            {
                equipmentTotalCost = 0;
                return;
            }

            double totalCost = 0;

            if (pawnKind?.race != null)
            {
                float xenoFactor = xenotype is object
                    ? GeneValuationUtil.XenotypeFactor(xenotype)
                    : GeneValuationUtil.XenotypeFactor(ResolveCustomXenotype());
                totalCost += Math.Floor(pawnKind.race.BaseMarketValue * FCSettings.militaryRaceCostMultiplier * xenoFactor);
            }

            foreach (SavedThing a in apparel)
                totalCost += a.MarketValue;

            foreach (SavedThing w in weapons)
                totalCost += w.MarketValue;

            foreach (SavedThing inv in inventory)
                totalCost += inv.MarketValue;

            foreach (SavedImplant im in implants)
                totalCost += ImplantCost(im);

            // Psylink-level cost is owned by the active psycast system (base game charges per level;
            // VPE returns 0 and balances via per-psycast cost instead).
            totalCost += PsycastSystemRegistry.Active?.PsylinkCost(psylinkLevel) ?? 0;
            foreach (SavedPsycast a in psycasts)
                totalCost += PsycastCost(a);

            // Companion animals: each row's market value times its count. The mount (if any) reuses the
            // same multiplier. The spawned animal/mount pawns are never re-counted by squad cost/power
            // math, so their entire cost lives here on the design (mirrors the mech cost path below).
            if (animals != null)
                foreach (SavedAnimal a in animals)
                    if (a.kind?.race != null)
                        totalCost += Math.Floor(a.kind.race.BaseMarketValue * FCSettings.militaryAnimalCostMultiplier) * Mathf.Max(1, a.count);
            if (mount?.race != null)
                totalCost += Math.Floor(mount.race.BaseMarketValue * FCSettings.militaryAnimalCostMultiplier);

            // Mechanitor cost: a flat surcharge for the mechlink itself, plus each bonded mech's
            // market value. Mirrors the animal cost path — the spawned mech pawns are never re-counted
            // by squad cost/power math, so their entire cost lives here on the design.
            if (ModsConfig.BiotechActive && IsMechanitorDesign)
            {
                totalCost += FCSettings.militaryMechlinkCost;
                if (mechs != null)
                    foreach (SavedMech m in mechs)
                        if (m.kind?.race != null)
                            totalCost += Math.Floor(m.kind.race.BaseMarketValue * FCSettings.militaryMechCostMultiplier) * Mathf.Max(1, m.count);
            }

            equipmentTotalCost = Math.Ceiling(totalCost);
        }

        /* Cost of a chosen psycast, resolved from its owning provider's display entry (which already
         * folds in FCSettings.militaryPsycastCostMultiplier). Zero if the system isn't loaded. */
        public static double PsycastCost(SavedPsycast psycast)
        {
            IPsycastSystemProvider provider = PsycastSystemRegistry.ByKey(psycast.systemKey);
            PsycastPickEntry entry;
            if (provider is object && provider.TryGetDisplay(psycast, out entry))
                return entry.cost;
            return 0;
        }

        /* Approximate cost of an implant from its install recipe: the market value of the fixed
         * ingredient(s) it consumes (the implant item), falling back to the produced thing. */
        public static float ImplantCost(RecipeDef recipe)
        {
            if (recipe is null) return 0f;
            float cost = 0f;
            if (recipe.ingredients != null)
            {
                foreach (IngredientCount ing in recipe.ingredients)
                {
                    if (ing.IsFixedIngredient && ing.FixedIngredient != null)
                        cost += ing.FixedIngredient.BaseMarketValue * ing.GetBaseCount();
                }
            }
            if (cost <= 0f && recipe.ProducedThingDef != null)
                cost += recipe.ProducedThingDef.BaseMarketValue;
            return cost;
        }

        /// <summary>Cost of a saved implant — the install recipe's ingredient value for surgery
        /// implants, or the item's market value for self-install implants (control sublinks etc.).</summary>
        public static float ImplantCost(SavedImplant im)
        {
            if (im.recipe is object) return ImplantCost(im.recipe);
            if (im.selfInstallThing is object) return im.selfInstallThing.BaseMarketValue;
            return 0f;
        }

        /// <summary>The ThingDef to use for an implant's icon and info card. Surgery install recipes
        /// usually have no UIIconThing (they install a hediff, they don't produce a thing), so fall back
        /// to the recipe's fixed ingredient — the implant item itself (e.g. the bionic part).</summary>
        public static ThingDef ImplantIconThing(RecipeDef recipe)
        {
            if (recipe is null) return null;
            if (recipe.UIIconThing != null) return recipe.UIIconThing;
            if (recipe.ingredients != null)
            {
                foreach (IngredientCount ing in recipe.ingredients)
                    if (ing.IsFixedIngredient && ing.FixedIngredient != null)
                        return ing.FixedIngredient;
            }
            return null;
        }

        public static ThingDef ImplantIconThing(SavedImplant im)
        {
            if (im.selfInstallThing is object) return im.selfInstallThing;
            return ImplantIconThing(im.recipe);
        }

        // --- Subclass-Aware Export/Import ---

        /* Creates the appropriate SavedUnitFC (or subclass) snapshot of this unit.
           Subclasses override to return their own SavedUnitFC subtype carrying their extra fields. */
        public virtual SavedUnitFC ToSavedUnit() => new SavedUnitFC(this);

        /* Called after base fields have been copied into a new instance during import.
           Subclasses override to pull their extra fields out of the SavedUnitFC subclass. */
        public virtual void LoadFromSaved(SavedUnitFC saved)
        {
        }

        // --- Unit Management ---

        public void RemoveUnit()
        {
            FindFC.Military.units.Remove(this);
        }

        /* Deep copy used by per-merc owned-loadout snapshots. The clone is owned by a
         * single Mercenary (not added to the pool), so it gets a fresh load id but no
         * registration with militaryCustomizationUtil.units. Subclasses that add fields
         * should override CopyExtraFieldsTo. */
        public virtual MilUnitFC Clone()
        {
            MilUnitFC copy = MilTemplateFactory.CreateUnit(isBlank);
            copy.name = name;
            copy.pawnKind = pawnKind;
            copy.xenotype = xenotype;
            copy.customXenotypeName = customXenotypeName;
            copy.animals = new List<SavedAnimal>(animals ?? new List<SavedAnimal>());
            copy.mount = mount;
            copy.forcedGender = forcedGender;
            copy.weapons = new List<SavedThing>(weapons ?? new List<SavedThing>());
            copy.apparel = new List<SavedThing>(apparel ?? new List<SavedThing>());
            copy.inventory = new List<SavedThing>(inventory ?? new List<SavedThing>());
            copy.implants = new List<SavedImplant>(implants ?? new List<SavedImplant>());
            copy.psylinkLevel = psylinkLevel;
            copy.psycasts = new List<SavedPsycast>(psycasts ?? new List<SavedPsycast>());
            copy.isMechanitor = isMechanitor;
            copy.mechs = new List<SavedMech>(mechs ?? new List<SavedMech>());
            copy.mechGroupWorkModes = new List<MechWorkModeDef>(mechGroupWorkModes ?? new List<MechWorkModeDef>());
            copy.statModifiers = statModifiers?.Select(m => m.Clone()).ToList() ?? new List<PermanentStatModifier>();
            CopyExtraFieldsTo(copy);
            copy.ChangeTick();
            copy.UpdateEquipmentTotalCost();
            return copy;
        }

        /* Subclass hook for Clone — copy any extra fields onto the destination. */
        protected virtual void CopyExtraFieldsTo(MilUnitFC dest)
        {
        }

        /// <summary>
        /// Re-roll the preview pawn (new appearance) while keeping equipment.
        /// Used by "Roll New Pawn" and race/xeno change buttons.
        /// </summary>
        public void RerollPreviewPawn()
        {
            pawnIdentityDirty = true;
            pawnEquipmentDirty = true;
            ChangeTick();
        }
    }
}
