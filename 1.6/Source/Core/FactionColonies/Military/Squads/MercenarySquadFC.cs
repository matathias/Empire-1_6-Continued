using FactionColonies.util;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace FactionColonies
{

    public class MercenarySquadFC : IExposable, ILoadReferenceable
    {
        public int loadID = -1;
        private string name;
        public List<Mercenary> mercenaries = new List<Mercenary>();
        // Sub-pawns (companion animals + bonded mechs) are now deep-owned by each Mercenary
        // (Mercenary.animals / Mercenary.mechs). Use AllSubPawns() to iterate every sub-pawn in
        // the squad. The old squad-level animals/mechs lists migrate via the legacy buffers below.
        public WorldSettlementFC settlement;
        public bool isExtraSquad;
        public int dead;
        private MilSquadFC _outfit;
        /// <summary>
        /// The design template this squad follows. Assigning it re-seeds this squad's design-sourced
        /// stat modifiers from the template (squad scope). Setting it null (template deleted) keeps the
        /// last copied modifiers.
        /// </summary>
        public MilSquadFC outfit
        {
            get => _outfit;
            set { _outfit = value; SyncDesignStatModifiers(value); }
        }
        public SquadEquipmentTracker Equipment;
        public SquadDeploymentState Deployment;

        /// <summary>Source tag for design modifiers copied from the outfit template.</summary>
        public const string DesignModifierSource = "__design";

        /// <summary>
        /// Effective per-squad stat modifiers read in combat (squad scope). Holds a flattened copy of the
        /// outfit template's modifiers (sourceId == DesignModifierSource) plus any earned accolades (other sources).
        /// </summary>
        public List<PermanentStatModifier> statModifiers = new List<PermanentStatModifier>();

        public void AddStatModifier(PermanentStatModifier mod) { statModifiers.Add(mod); }
        public void RemoveStatModifiersBySource(string sourceId) { statModifiers.RemoveAll(m => m.sourceId == sourceId); }

        /// <summary>
        /// Re-seed design-sourced modifiers from the given template, preserving non-design (e.g. accolades, upgrades)
        /// entries. A null source keeps the existing copy (squad no longer has a backing template).
        /// </summary>
        private void SyncDesignStatModifiers(MilSquadFC source)
        {
            if (source is null) return;
            if (statModifiers is null) statModifiers = new List<PermanentStatModifier>();
            statModifiers.RemoveAll(m => m.sourceId == DesignModifierSource);
            if (source.statModifiers != null)
            {
                foreach (PermanentStatModifier m in source.statModifiers)
                {
                    PermanentStatModifier copy = m.Clone();
                    copy.sourceId = DesignModifierSource;
                    statModifiers.Add(copy);
                }
            }
        }

        /* -*-*-*-*- Squad-first refactor fields -*-*-*-*-
         * nextAvailableTick: per-squad cooldown expiry. Updated in MilitaryOperation.EnterCooldown.
         * hiredAtTick: tick at which the squad was hired (analytics + future age hooks).
         * autoDefend: per-squad opt-in to foreign-defender candidate selection. Replaces the
         * old per-settlement comp.autoDefend flag — auto-defend now lives on the squad. */
        public int nextAvailableTick;
        public int hiredAtTick;
        public bool autoDefend;

        /* Migration buffers — pre-refactor saves wrote these as top-level Scribe nodes.
           Read in LoadingVars; drained into the matching sub-object in PostLoadInit. */
        [Unsaved] private List<ThingWithComps> _legacyUsedWeaponList;
        [Unsaved] private List<Apparel> _legacyUsedApparelList;
        [Unsaved] private Lord _legacyLord;
        [Unsaved] private Map _legacyMap;
        [Unsaved] private MilitaryOrder _legacyMilitaryOrder = MilitaryOrder.Undefined;
        [Unsaved] private IntVec3 _legacyOrderLocation;
        /* Pre-merc-ownership saves wrote companion animals as a squad-level deep list. Read in
           LoadingVars, redistributed onto their handler merc in PostLoadInit (see RedistributeLegacyAnimals).
           Mechs need no migration — mechanitor handling never shipped, so no save contains squad-level mechs. */
        [Unsaved] private List<Mercenary> _legacyAnimals;

        public MercenarySquadFC()
        {
            Equipment = CreateEquipment();
            Deployment = CreateDeployment();
        }

        /* Factory hooks so subclasses can install custom sub-objects. */
        protected virtual SquadEquipmentTracker CreateEquipment() => new SquadEquipmentTracker(this);
        protected virtual SquadDeploymentState CreateDeployment() => new SquadDeploymentState(this);

        /* Raw squad name, or null if unset. Use DisplayName for UI; only use Name when
           the caller explicitly needs the raw value (e.g., seeding a rename text box). */
        public string Name => name;

        /* User-facing name. Falls back to the source template's name when this squad's
           Name wasn't set, and finally to FCUnnamedSquad. Use this for any UI/message
           that identifies a specific deployed squad — never outfit.name directly, since
           outfit is a Scribe_References link and can resolve to null after load. */
        public string DisplayName => name ?? outfit?.name ?? "FCUnnamedSquad".Translate();

        public void SetName(string newName) => name = newName;

        public virtual void ExposeData()
        {
            // Pre-save: sever bonds to dead mechs so no mechanitor saves a relation to an unsaved pawn.
            if (Scribe.mode == LoadSaveMode.Saving) CleanupMechBonds();

            Scribe_Values.Look(ref loadID, "loadID", -1);
            Scribe_Values.Look(ref name, "name");
            Scribe_Collections.Look(ref mercenaries, "mercenaries", LookMode.Deep);
            Scribe_Values.Look(ref isExtraSquad, "isExtraSquad");
            Scribe_References.Look(ref _outfit, "outfit");
            Scribe_Collections.Look(ref statModifiers, "statModifiers", LookMode.Deep);
            Scribe_Values.Look(ref dead, "dead");
            Scribe_Deep.Look(ref Equipment, "equipment", new object[] { this });
            Scribe_Deep.Look(ref Deployment, "deployment", new object[] { this });
            Scribe_References.Look(ref settlement, "Settlement");
            Scribe_Values.Look(ref nextAvailableTick, "nextAvailableTick", 0);
            Scribe_Values.Look(ref hiredAtTick, "hiredAtTick", 0);
            Scribe_Values.Look(ref autoDefend, "autoDefend", false);

            /* Reference-mode legacy reads MUST run in both LoadingVars (register the loadID) and
               ResolvingCrossRefs (resolve/consume it) — that two-phase handshake is how Scribe
               references work. Gating them to LoadingVars alone leaks the loadIDs (the "Not all
               loadIDs which were read were consumed" warning) AND leaves the buffers null (the
               migration silently no-ops). Excluded from Saving/PostLoadInit so these pre-refactor
               keys are never re-emitted into new saves.
               Pre-refactor saves stored these as top-level fields on the squad; captured into
               [Unsaved] buffers here and drained in PostLoadInit. */
            if (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.ResolvingCrossRefs)
            {
                Scribe_Collections.Look(ref _legacyUsedWeaponList, "UsedWeaponList", LookMode.Reference);
                Scribe_Collections.Look(ref _legacyUsedApparelList, "UsedApparelList", LookMode.Reference);
                Scribe_References.Look(ref _legacyLord, "lord");
                Scribe_References.Look(ref _legacyMap, "map");
            }

            /* Value/deep legacy reads only make sense in LoadingVars. Companion animals were deep-owned
               here; deep-load them so their handler refs resolve, then redistribute onto the owning
               merc in PostLoadInit. */
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Collections.Look(ref _legacyAnimals, "animals", LookMode.Deep);
                Scribe_Values.Look(ref _legacyMilitaryOrder, "militaryOrder", MilitaryOrder.Undefined);
                Scribe_Values.Look(ref _legacyOrderLocation, "orderLocation");
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (statModifiers is null) statModifiers = new List<PermanentStatModifier>();
                RedistributeLegacyAnimals(_legacyAnimals);
                _legacyAnimals = null;
                // Repair any dangling Overseer relations a broken save left in memory (and reap dead mechs).
                CleanupMechBonds();
                if (Equipment is null) Equipment = CreateEquipment();
                Equipment.AdoptLegacyLists(_legacyUsedWeaponList, _legacyUsedApparelList);
                _legacyUsedWeaponList = null;
                _legacyUsedApparelList = null;

                if (Deployment is null) Deployment = CreateDeployment();
                /* Conditional drain: only pre-refactor saves carry non-default legacy buffers.
                   Post-refactor saves loaded the real state into the nested "deployment" node, so
                   AdoptLegacyValues skips (all buffers default) and preserves that live state. */
                Deployment.AdoptLegacyValues(_legacyLord, _legacyMap,
                                             _legacyMilitaryOrder, _legacyOrderLocation);
                _legacyLord = null;
                _legacyMap = null;
                _legacyMilitaryOrder = MilitaryOrder.Undefined;
                _legacyOrderLocation = default(IntVec3);
            }
        }

        public string GetUniqueLoadID()
        {
            return $"MercenarySquadFC_{loadID}";
        }

        /// <summary>Every sub-pawn wrapper (companion animals + bonded mechs) owned by this squad's
        /// mercs, live or placeholder. Replaces the old squad-level animals/mechs lists for flat iteration.</summary>
        public IEnumerable<Mercenary> AllSubPawns()
        {
            if (mercenaries is null) yield break;
            foreach (Mercenary m in mercenaries)
            {
                if (m is null) continue;
                foreach (Mercenary sub in m.SubPawns()) yield return sub;
            }
        }

        private IEnumerable<Mercenary> SubPawnsOfType(Mercenary.SubPawnType type) =>
            AllSubPawns().Where(s => s != null && s.subPawnType == type);

        /// <summary>Reattach legacy squad-level companion animals (pre-merc-ownership saves) to their
        /// handler merc. Mechs need no equivalent — mechanitor handling never shipped.</summary>
        private static void RedistributeLegacyAnimals(List<Mercenary> legacy)
        {
            if (legacy is null) return;
            foreach (Mercenary sub in legacy)
            {
                if (sub?.handler is null) continue;
                if (sub.subPawnType == Mercenary.SubPawnType.None) sub.subPawnType = Mercenary.SubPawnType.Animal;
                if (sub.handler.animals is null) sub.handler.animals = new List<Mercenary>();
                sub.handler.animals.Add(sub);
            }
        }

        public IEnumerable<Mercenary> EquippedMercenaries =>
            mercenaries.Where(merc => merc?.pawn?.apparel != null
                                       && merc.pawn.equipment != null
                                       && (merc.pawn.apparel.WornApparel.Any()
                                           || merc.pawn.equipment.AllEquipmentListForReading.Any()
                                           || merc.animals.Any())
                                       && merc.deployable);

        public IEnumerable<Pawn> EquippedMercenaryPawns =>
            EquippedMercenaries.Select(merc => merc.pawn);

        public IEnumerable<Pawn> EquippedAnimalMercenaries =>
            SubPawnsOfType(Mercenary.SubPawnType.Animal).Where(a => a?.pawn != null).Select(a => a.pawn);

        public IEnumerable<Pawn> EquippedMechMercenaries =>
            SubPawnsOfType(Mercenary.SubPawnType.Mech).Where(mech => mech?.pawn != null).Select(mech => mech.pawn);

        /// <summary>Mount animals (Giddy Up 2). They must spawn into the battle/deploy map like companion
        /// animals so a rider can be mounted on them; they're filtered to riders in the battle/deploy
        /// wiring (BattlefieldContext / LordJob_DeployMilitary), not here.</summary>
        public IEnumerable<Pawn> EquippedMountMercenaries =>
            SubPawnsOfType(Mercenary.SubPawnType.Mount).Where(m => m?.pawn != null).Select(m => m.pawn);

        public IEnumerable<Pawn> AllEquippedMercenaryPawns =>
            EquippedMercenaries.Select(merc => merc.pawn)
                .Concat(EquippedAnimalMercenaries)
                .Concat(EquippedMechMercenaries)
                .Concat(EquippedMountMercenaries);

        /// <summary>Equipped mercs and animals that are eligible to be spawned into a battle map,
        /// excluding pawns that are currently downed, dead, destroyed, or already on a map (a
        /// stale pawn from a prior deploy that wasn't cleaned up would otherwise fail to spawn and
        /// break the deployed lord). Used by Deploy, defense initial spawn, and reinforcement.</summary>
        public IEnumerable<Pawn> SpawnableMercenaryPawns =>
            AllEquippedMercenaryPawns.Where(p => p is object && !p.Downed && !p.Dead && !p.Destroyed && !p.Spawned);

        public IEnumerable<Pawn> AllDeployedMercenaryPawns =>
            DeployedMercenaries.Select(merc => merc.pawn)
                .Concat(DeployedMercenaryAnimals.Select(merc => merc.pawn))
                .Concat(DeployedMercenaryMechs.Select(merc => merc.pawn))
                .Concat(DeployedMercenaryMounts.Select(merc => merc.pawn));

        public IEnumerable<Mercenary> DeployedMercenaries =>
            mercenaries.Where(merc => merc?.pawn?.Map != null);

        public IEnumerable<Mercenary> DeployedMercenaryAnimals =>
            SubPawnsOfType(Mercenary.SubPawnType.Animal).Where(merc => merc?.pawn?.Map != null);

        public IEnumerable<Mercenary> DeployedMercenaryMechs =>
            SubPawnsOfType(Mercenary.SubPawnType.Mech).Where(merc => merc?.pawn?.Map != null);

        public IEnumerable<Mercenary> DeployedMercenaryMounts =>
            SubPawnsOfType(Mercenary.SubPawnType.Mount).Where(merc => merc?.pawn?.Map != null);

        /// <summary>The <see cref="MilitaryOperation"/> this squad is currently part of, if any.
        /// Returned via the <see cref="MilitaryOperationManager"/>'s squad index, so this is O(1)
        /// and reflects the canonical op state.</summary>
        public MilitaryOperation Operation => FindFC.MilitaryManager?.GetOpForSquad(this);

        /// <summary>True when this squad is in any active op (offensive, defensive, deploy, or cooldown).</summary>
        public bool IsBusy => Operation is object;

        /// <summary>True when the squad's <see cref="SquadCostExtensions.DeploymentCost"/> exceeds
        /// its assigned settlement's max deploy cost. An underfunded squad stays assigned but
        /// can't take part in military operations. Distinct from <see cref="IsBusy"/>: this is a
        /// structural (cost) constraint, not a temporary deployment state.</summary>
        public bool IsUnderfunded
            => settlement is object
               && MilitaryFC.SquadExceedsSettlementBudget(this, settlement, out _, out _);

        /// <summary>True when this squad is currently assigned to a billet (settlement). False
        /// when the squad sits in the unassigned hire pool.</summary>
        public bool IsAssigned => settlement is object;

        /// <summary>True when the squad is assigned, not in any active op, not underfunded
        /// at its settlement, and past its post-op cooldown. Canonical "can launch a new
        /// op" gate.</summary>
        public bool IsAvailable
            => IsAssigned && !IsBusy && !IsUnderfunded && nextAvailableTick <= Find.TickManager.TicksGame;

        /* The squad's billet. settlement is the canonical source of truth in the squad-first
         * model; the property exists only as a stable accessor for external callers. */
        public WorldSettlementFC getSettlement => settlement;

        /// <summary>
        /// Initial population pass: creates 30 mercs (one per template slot, or blank if no
        /// outfit) and equips them via <see cref="SquadEquipmentTracker.OutfitSquad"/>. The canonical first call
        /// is from <see cref="MilitaryFC.HireSquad"/>, where a freshly-
        /// constructed squad has <c>mercenaries.Count == 0</c> so the early-return guard
        /// does not fire.
        /// <para>Strict-manual outfit policy: the guard exists for post-load reentry
        /// (<see cref="CheckInitialization"/>) so a squad with all-dead placeholder mercs
        /// is NOT silently regenerated. The player must explicitly Fill those slots.</para>
        /// </summary>
        public void InitiateSquad()
        {
            if (mercenaries != null && mercenaries.Count > 0) return;

            mercenaries = new List<Mercenary>();

            int cap = MilSquadFC.MaxSquadSize;
            int templateCount = (outfit?.Units != null) ? outfit.Units.Count : 0;
            int slotCount = (outfit == null) ? cap : Math.Min(cap, templateCount);

            for (int k = 0; k < slotCount; k++)
            {
                Mercenary placeholder = new Mercenary(true);
                MilUnitFC slot = (outfit != null && k < templateCount) ? outfit.Units[k] : null;

                // Generate a pawn only for slots that have a real (non-blank) unit assignment.
                // Blank slots stay as empty placeholders (pawn == null), refillable via
                // FillEmptySlots / Upgrade when the player assigns a real unit later.
                if (slot != null && !slot.isBlank)
                {
                    MercenaryPawnFactory.CreateNewPawn(this, ref placeholder, slot.pawnKind, slot.xenotype, slot.customXenotypeName, slot);
                    if (placeholder.pawn == null)
                    {
                        LogUtil.Warning($"Failed to create mercenary {k + 1}/{slotCount} for unit {slot.name ?? "unknown"}; leaving slot empty.");
                    }
                }

                // Always add the placeholder so list.Count == slotCount and slot indices align with
                // outfit.Units indices. OutfitSquad / FillEmptySlots will fill empty placeholders
                // when there's a non-blank loadout to assign.
                mercenaries.Add(placeholder);
            }

            LogUtil.Message($"InitiateSquad mercenary count : {mercenaries.Count()}");
            if (loadID == -1)
            {
                loadID = FindFC.Military.NextMercenarySquadId();
            }

            if (outfit != null)
            {
                Equipment.OutfitSquad(outfit);
            }
            else
            {
                FindFC.Military.RebuildMercenaryPawnSet();
            }
        }
        /// <summary>
        /// Ensures the squad's mercenary list exists. Does NOT re-outfit, refill, or
        /// regenerate dead pawns — under strict-manual outfit policy, gear / pawn changes
        /// only happen on explicit player action (Hire / Fill / Upgrade / Edit). A list of
        /// all-empty placeholder slots is a valid state post-battle and stays that way until
        /// the player calls Fill.
        /// </summary>
        public void CheckInitialization()
        {
            if (mercenaries is null) InitiateSquad();
        }

        public void UpdateSquadStats(int level)
        {
            foreach (Mercenary merc in mercenaries)
            {
                if (merc?.pawn?.skills == null) continue;

                var shooting = merc.pawn.skills.GetSkill(SkillDefOf.Shooting);
                var melee = merc.pawn.skills.GetSkill(SkillDefOf.Melee);
                var medicine = merc.pawn.skills.GetSkill(SkillDefOf.Medicine);

                if (shooting != null) shooting.Level = Math.Min(level * 2, 20);
                if (melee != null) melee.Level = Math.Min(level * 2, 20);
                if (medicine != null) medicine.Level = Math.Min(level * 1, 20);
            }
        }

        /// <summary>Number of empty slots that <see cref="FillEmptySlots"/> would actually fill
        /// — pawn is null AND <see cref="Mercenary.BlueprintLoadout"/> is non-null and not blank,
        /// plus every "Missing" (assigned-but-absent) sub-pawn of a live merc. Pure placeholder
        /// slots (blank blueprint, kept to align indices with the template) are excluded so the
        /// UI count matches the action's effect.</summary>
        public int EmptySlotCount
        {
            get
            {
                int n = 0;
                if (mercenaries is null) return 0;
                foreach (Mercenary m in mercenaries)
                {
                    if (m is null) continue;
                    if (m.IsEmptySlot)
                    {
                        MilUnitFC blueprint = m.BlueprintLoadout;
                        if (blueprint is null || blueprint.isBlank) continue;
                        n++;
                    }
                    else
                    {
                        foreach (Mercenary sub in m.SubPawns())
                            if (sub.IsMissingSubPawn) n++;
                    }
                }
                return n;
            }
        }

        /// <summary>Missing (assigned-but-absent) sub-pawns of every live merc — the paid-replace
        /// targets folded into <see cref="FillEmptySlots"/> and its cost.</summary>
        public IEnumerable<Mercenary> MissingSubPawns()
        {
            if (mercenaries is null) yield break;
            foreach (Mercenary m in mercenaries)
            {
                if (m?.pawn is null) continue; // owner must be alive to restore its sub-pawns
                foreach (Mercenary sub in m.SubPawns())
                    if (sub.IsMissingSubPawn) yield return sub;
            }
        }

        /// <summary>Recreates the pawn for a sub-pawn into its existing wrapper (preserving handler +
        /// list membership), tearing down the old pawn first — whether it's a Missing placeholder, a
        /// corpse, or a live but injured/downed pawn the player chose to replace at full cost. Caller
        /// handles payment. Returns true if a fresh, alive pawn was created.</summary>
        public bool ReplaceSubPawn(Mercenary sub)
        {
            if (sub is null || sub.subPawnType == Mercenary.SubPawnType.None) return false;
            // Tear down the existing pawn before recreating. Sever a mech's Overseer bond first (else the
            // mechanitor keeps a dangling relation), then destroy a still-live pawn we're discarding (a
            // dead/destroyed one is left to vanilla cleanup). Leaves a clean null on failure.
            if (sub.pawn is object)
            {
                bool gone = sub.pawn.Dead || sub.pawn.Destroyed;
                if (sub.subPawnType == Mercenary.SubPawnType.Mech && sub.handler?.pawn != null)
                    MercenaryPawnFactory.UnbondMech(sub.handler.pawn, sub.pawn);
                if (!gone && !sub.pawn.Destroyed) sub.pawn.Destroy();
                sub.pawn = null;
            }
            Mercenary slot = sub;
            // Mounts are animals too — recreate them via CreateNewAnimal (which tags Animal) and restore
            // the Mount tag so the rider gets re-mounted on next deploy/reconcile.
            if (sub.subPawnType == Mercenary.SubPawnType.Animal || sub.subPawnType == Mercenary.SubPawnType.Mount)
            {
                Mercenary.SubPawnType originalType = sub.subPawnType;
                MercenaryPawnFactory.CreateNewAnimal(this, ref slot, sub.subPawnKind);
                slot.subPawnType = originalType;
            }
            else
            {
                Pawn overseer = sub.handler?.pawn;
                if (overseer is null) return false; // mech needs a live mechanitor overseer
                MercenaryPawnFactory.CreateNewMech(this, ref slot, sub.subPawnKind, overseer, sub.subPawnMechGroup, sub.subPawnWorkMode);
            }
            return slot.pawn is object && !slot.pawn.Dead;
        }

        /// <summary>Pays <see cref="SquadCostExtensions.FillEmptySlotsCost"/> silver and generates fresh
        /// pawns into every empty slot, equipping each from its resolved loadout. Slots with no
        /// loadout are skipped. Returns false (no payment) if the player can't afford the total.
        /// Pass <paramref name="silent"/> true to suppress the "insufficient silver" reject message
        /// (used by the automated auto-replace path, which retries every tax tick).</summary>
        public bool FillEmptySlots(bool silent = false)
        {
            int total = this.FillEmptySlotsCost();
            if (total > 0 && !PaymentUtil.TryPaySilver(total, PaymentUtil.Reason_SquadFillSlot, settlement))
            {
                if (!silent)
                    Messages.Message("FCSquadFillSlotsInsufficient".Translate(total),
                        MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (mercenaries != null)
            {
                foreach (Mercenary m in mercenaries)
                {
                    if (m is null || !m.IsEmptySlot) continue;
                    MilUnitFC blueprint = m.BlueprintLoadout;
                    if (blueprint is null || blueprint.isBlank) continue;
                    Mercenary slot = m;
                    MercenaryPawnFactory.CreateNewPawn(this, ref slot, blueprint.pawnKind, blueprint.xenotype, blueprint.customXenotypeName, blueprint);
                    if (slot.pawn != null)
                    {
                        Equipment.EquipPawn(slot, blueprint);
                        // Refilling a dead merc rebuilds its whole unit from the blueprint (whose cost
                        // already covers the animal + mechs), so reconcile both — not just mechs.
                        Equipment.ReconcileAnimal(slot, blueprint);
                        Equipment.ReconcileMechs(slot, blueprint);
                    }
                    // Sync currentLoadout with what we just equipped — re-snap from the blueprint.
                    slot.currentLoadout = blueprint.Clone();
                }
            }

            // Restore every Missing sub-pawn of a (now-)live merc. Snapshot first since
            // ReplaceSubPawn repopulates the wrapper's pawn, dropping it out of MissingSubPawns.
            foreach (Mercenary sub in MissingSubPawns().ToList())
            {
                ReplaceSubPawn(sub);
            }

            FindFC.Military?.RebuildMercenaryPawnSet();
            LifecycleRegistry.InvokeOnSquadUpgraded(this);
            return true;
        }

        /// <summary>Dismisses a single mercenary: strips and destroys the pawn (and any animal
        /// handler), and clears the slot to an empty placeholder so the player can refill it
        /// later via <see cref="FillEmptySlots"/>. No silver is returned. The slot is preserved
        /// (not removed from the list) so its blueprint stays available. Returns false if the
        /// squad is busy or the slot is already empty.</summary>
        public bool DismissMercenary(Mercenary merc)
        {
            if (merc is null || merc.IsEmptySlot) return false;
            if (IsBusy)
            {
                Messages.Message("FCCannotDismissBusyMerc".Translate(merc.pawn?.LabelShortCap ?? "?"),
                    MessageTypeDefOf.RejectInput, false);
                return false;
            }

            /* Strip + destroy. Mirrors SquadUpgradeUtil.UpgradeToTemplate's fire-pass cleanup. */
            Equipment.StripPawn(merc);
            if (merc.pawn != null && !merc.pawn.Destroyed) merc.pawn.Destroy();
            merc.pawn = null;
            // Fully clear sub-pawns: the slot becomes a clean refill target and Fill recreates them.
            if (merc.animals != null)
            {
                foreach (Mercenary a in merc.animals)
                    if (a?.pawn != null && !a.pawn.Destroyed) a.pawn.Destroy();
                merc.animals.Clear();
            }
            RemoveMechsFor(merc);

            /* Reset transient/personalization state so the empty slot is a clean refill target.
               Keep `loadout` (pool reference) so Fill can reuse it. */
            merc.ownedLoadout = null;
            merc.currentLoadout = null;
            FindFC.Military?.RebuildMercenaryPawnSet();
            Messages.Message("FCMercDismissed".Translate(), MessageTypeDefOf.NeutralEvent, false);
            return true;
        }

        /// <summary>Drops an empty slot from <see cref="mercenaries"/>, shrinking the squad's
        /// max slot count by one. Returns false if the slot is missing, filled, or the squad is busy.
        /// </summary>
        public bool RemoveEmptySlot(Mercenary merc)
        {
            if (merc is null) return false;
            if (merc.pawn is object) return false;
            if (mercenaries is null) return false;
            if (IsBusy) return false;
            if (!mercenaries.Remove(merc)) return false;
            FindFC.Military?.RebuildMercenaryPawnSet();
            return true;
        }

        public void DebugMercenarySquad()
        {
            LogUtil.MessageForce("Debug Mercenary Squad");
            foreach (Mercenary merc in mercenaries)
            {
                if (merc?.pawn == null)
                {
                    LogUtil.MessageForce("\t[empty]");
                    continue;
                }
                LogUtil.MessageForce($"\t{merc.pawn} \t{merc.pawn.health.Dead.ToString()} \t{merc.pawn.apparel.WornApparelCount} \t{merc.pawn.equipment.AllEquipmentListForReading.Count()}");
            }
        }

        public Mercenary ReturnPawn(Pawn pawn)
        {
            foreach (Mercenary merc in mercenaries)
            {
                if (merc?.pawn == pawn) return merc;
            }
            return null;
        }

        /// <summary>Destroys and drops every bonded mech owned by <paramref name="owner"/>. A
        /// mechanitor can own several mechs, so this clears them all. Used by Dismiss and the upgrade
        /// fire-pass so a removed/replaced mechanitor never leaves orphaned mech pawns.</summary>
        public void RemoveMechsFor(Mercenary owner)
        {
            if (owner?.mechs is null) return;
            for (int i = owner.mechs.Count - 1; i >= 0; i--)
            {
                Mercenary m = owner.mechs[i];
                if (m is null) continue;
                // Sever the Overseer bond before discarding the mech so the mechanitor never keeps a
                // dangling relation to a destroyed pawn.
                if (owner.pawn != null && m.pawn != null) MercenaryPawnFactory.UnbondMech(owner.pawn, m.pawn);
                if (m.pawn != null && !m.pawn.Destroyed) m.pawn.Destroy();
                owner.mechs.RemoveAt(i);
            }
        }

        /// <summary>Severs Overseer bonds to dead/destroyed mechs (reaping the wrappers to clean Missing
        /// placeholders) and scrubs any leftover dangling Overseer relations from mechanitor mercs. Keeps
        /// a mechanitor from saving a relation to a no-longer-saved mech, which NREs in
        /// <c>Pawn_RelationsTracker.ExposeData</c> on load. Run before save and after load.</summary>
        public void CleanupMechBonds()
        {
            if (!ModsConfig.BiotechActive || mercenaries is null) return;
            foreach (Mercenary merc in mercenaries)
            {
                Pawn overseer = merc?.pawn;
                if (overseer is null) continue;

                // Reap dead/destroyed mechs into clean Missing placeholders: unassign from control
                // groups (best-effort) and null the wrapper. The Overseer relation is stripped below.
                if (merc.mechs != null)
                {
                    foreach (Mercenary mech in merc.mechs)
                    {
                        if (mech?.pawn is null) continue;
                        if (mech.pawn.Dead || mech.pawn.Destroyed)
                        {
                            try { overseer.mechanitor?.UnassignPawnFromAnyControlGroup(mech.pawn); }
                            catch (Exception e) { LogUtil.Warning($"Failed to unassign dead mech control group: {e.Message}"); }
                            mech.pawn = null;
                        }
                    }
                }

                // Strip broken / dangling Overseer relations DIRECTLY from the list. We can't use
                // TryRemoveDirectRelation here: it dereferences otherPawn.relations, which is null for a
                // destroyed mech → NRE (the whole reason this cleanup exists). Live mechs keep their
                // relation (otherPawn alive, tracker present).
                List<DirectPawnRelation> rels = overseer.relations?.DirectRelations;
                if (rels is null) continue;
                for (int i = rels.Count - 1; i >= 0; i--)
                {
                    DirectPawnRelation r = rels[i];
                    if (r is null || r.def is null) { rels.RemoveAt(i); continue; }
                    if (r.def != PawnRelationDefOf.Overseer) continue;
                    Pawn other = r.otherPawn;
                    if (other is null || other.Dead || other.Destroyed || other.relations is null)
                        rels.RemoveAt(i);
                }
            }
        }

    }
}