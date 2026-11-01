using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Race + xenotype-aware re-template workflow for a hired squad. Owns the cost
    /// preview properties (UpgradeCost / HasUpgradeWork / etc.), the SwapTemplate
    /// pointer-shuffle, and the bulk UpgradeToTemplate commit path.
    /// </summary>
    public static class SquadUpgradeUtil
    {
        /* Per-slot decision in a re-template plan. Each non-null template slot resolves
         * to either a claim (existing merc reused) or a fresh-hire (new pawn needed).
         * Slot index is the position in outfit.Units. */
        private struct SlotDecision
        {
            public int slotIndex;
            public MilUnitFC slotUnit;     // the (possibly newly-swapped) template's slot unit
            public MilUnitFC target;       // resolved gear target: claim.ownedLoadout ?? slotUnit
            public Mercenary claim;        // null when this is a fresh-hire
        }

        /* Cost decomposition + per-slot decisions for a re-template against the current
         * outfit. Used by both the cost preview and the commit path. */
        private struct UpgradePlan
        {
            public List<SlotDecision> Slots;   // claims and fresh-hires, in slot order
            public List<Mercenary> Fires;      // mercs to be fired (unclaimed by any slot)
            public int UpgradeSilver;          // sum of positive diffs on claims (times upgradeMult)
            public int FreshHireSilver;        // sum of fresh-hire costs (times hireMult)
            public int ReassignCount;          // claimed slots whose target differs from the merc's currentLoadout

            /* True when committing the plan would actually do something — re-equip a claimed
               merc, fresh-hire a slot, or fire an unclaimed merc — even when the net silver
               cost rounds to zero (a same-price-or-cheaper re-equip still needs applying). */
            public bool HasWork =>
                ReassignCount > 0
                || (Fires != null && Fires.Count > 0)
                || (Slots != null && Slots.Any(s => s.claim is null));
        }

        /* Race + xenotype identity tuple comparison. With Biotech off, race alone is
         * enough; with Biotech on, xenotype (or custom xenotype name) must match too.
         * Returns true if the merc's pawn can stand in for a slot of the given unit. */
        internal static bool IsRaceXenoMatch(Mercenary merc, MilUnitFC slot)
        {
            if (merc?.pawn?.kindDef?.race == null || slot?.pawnKind?.race == null) return false;
            if (merc.pawn.kindDef.race != slot.pawnKind.race) return false;

            if (!ModsConfig.BiotechActive) return true;

            if (slot.customXenotypeName != null)
            {
                string mercCustom = merc.pawn.genes?.CustomXenotype?.name;
                return mercCustom == slot.customXenotypeName;
            }
            XenotypeDef mercXeno = merc.pawn.genes?.Xenotype;
            return mercXeno == slot.xenotype;
        }

        private static UpgradePlan BuildUpgradePlan(MercenarySquadFC squad)
        {
            UpgradePlan plan = new UpgradePlan
            {
                Slots = new List<SlotDecision>(),
                Fires = new List<Mercenary>(),
            };
            if (squad?.outfit?.Units is null || squad.mercenaries is null) return plan;

            List<Mercenary> claimable = new List<Mercenary>();
            foreach (Mercenary m in squad.mercenaries)
            {
                if (m?.pawn?.kindDef != null) claimable.Add(m);
            }

            IReadOnlyList<MilUnitFC> templateUnits = squad.outfit.Units;
            int slotCount = Math.Min(templateUnits.Count, MilSquadFC.MaxSquadSize);

            double upgradeSum = 0;
            double freshHireSum = 0;
            for (int i = 0; i < slotCount; i++)
            {
                MilUnitFC slotUnit = templateUnits[i];
                if (slotUnit is null || slotUnit.isBlank) continue;

                int idx = claimable.FindIndex(m => IsRaceXenoMatch(m, slotUnit));
                if (idx >= 0)
                {
                    Mercenary picked = claimable[idx];
                    claimable.RemoveAt(idx);

                    /* Personalization wins; otherwise conform to the live template slot.
                       This is the merc's BlueprintLoadout with its (possibly stale, post-
                       SwapTemplate) `loadout` pointer replaced by the current slot unit. */
                    MilUnitFC target = picked.ownedLoadout ?? slotUnit;
                    plan.Slots.Add(new SlotDecision
                    {
                        slotIndex = i,
                        slotUnit = slotUnit,
                        target = target,
                        claim = picked
                    });

                    /* Accumulate the UNSCALED diff; squadUpgradeCostMultiplier is applied
                       once at the end so the bulk total stays round-of-sum, and the
                       difference test mirrors the per-pawn button exactly. */
                    upgradeSum += LoadoutUpgradeUtil.RawEquipmentDiff(target, picked.currentLoadout);
                    if (LoadoutUpgradeUtil.LoadoutsDiffer(target, picked.currentLoadout))
                        plan.ReassignCount++;
                }
                else
                {
                    plan.Slots.Add(new SlotDecision
                    {
                        slotIndex = i,
                        slotUnit = slotUnit,
                        target = slotUnit,
                        claim = null
                    });
                    freshHireSum += slotUnit.getTotalCost;
                }
            }

            // Mercs not claimed are fired.
            plan.Fires.AddRange(claimable);

            plan.UpgradeSilver = (int)Math.Round(upgradeSum * FCSettings.squadUpgradeCostMultiplier);
            plan.FreshHireSilver = (int)Math.Round(freshHireSum * FCSettings.squadHireCostMultiplier);
            return plan;
        }

        /// <summary>Net silver cost of running <see cref="UpgradeToTemplate"/> right now.
        /// Computed as upgrade-diff (claimed mercs) + hire-cost (fresh slots). Fired mercs
        /// produce no silver. Zero when no template, no changes, or the squad is missing/busy.</summary>
        public static int UpgradeCost(MercenarySquadFC squad)
        {
            if (squad?.outfit?.Units is null || squad.mercenaries is null) return 0;
            UpgradePlan plan = BuildUpgradePlan(squad);
            return plan.UpgradeSilver + plan.FreshHireSilver;
        }

        /// <summary>Component breakdown of <see cref="UpgradeCost"/>: gross upgrade silver and
        /// fresh-hire silver. Useful for inspection-window tooltips that explain where the
        /// displayed total came from.</summary>
        public static (int upgrade, int hire) UpgradeCostBreakdown(MercenarySquadFC squad)
        {
            if (squad?.outfit?.Units is null || squad.mercenaries is null)
                return (0, 0);
            UpgradePlan plan = BuildUpgradePlan(squad);
            return (plan.UpgradeSilver, plan.FreshHireSilver);
        }

        /// <summary>True when <see cref="UpgradeToTemplate"/> would actually do something —
        /// re-equip a claimed merc, fresh-hire a slot, or fire an unclaimed merc — even when
        /// the net silver cost rounds to zero. Gate the Upgrade-All button on this, not on
        /// <see cref="UpgradeCost"/>: a same-price-or-cheaper re-equip is real work at zero cost.</summary>
        public static bool HasUpgradeWork(MercenarySquadFC squad)
        {
            if (squad?.outfit?.Units is null || squad.mercenaries is null) return false;
            return BuildUpgradePlan(squad).HasWork;
        }

        /// <summary>True when <see cref="UpgradeToTemplate"/> would fire (destroy) at least one
        /// merc the player has personalized via the per-pawn loadout editor. Lets the UI warn
        /// before a re-template silently discards a personalized pawn.</summary>
        public static bool UpgradeWouldFirePersonalized(MercenarySquadFC squad)
        {
            if (squad?.outfit?.Units is null || squad.mercenaries is null) return false;
            return BuildUpgradePlan(squad).Fires.Any(m => m?.ownedLoadout is object);
        }

        /// <summary>Sets the squad's template reference to <paramref name="newTemplate"/> without
        /// touching gear on existing mercenaries.
        /// Empty slots whose blueprint came from the prior template (i.e. <see cref="Mercenary.loadout"/>
        /// is not present in <paramref name="newTemplate"/>'s <see cref="MilSquadFC.Units"/>) are pruned
        /// from the mercenary list so the player isn't offered a Fill that would inject gear from a
        /// template the squad no longer follows. Pass null to clear the template association — all
        /// orphan empty slots are pruned in that case. The player can then run
        /// <see cref="UpgradeToTemplate"/> to conform filled mercs to the new template.</summary>
        public static void SwapTemplate(MercenarySquadFC squad, MilSquadFC newTemplate)
        {
            if (squad is null) return;
            squad.outfit = newTemplate;
            if (squad.mercenaries is null) return;

            HashSet<MilUnitFC> retainable = null;
            if (newTemplate?.Units != null)
            {
                retainable = new HashSet<MilUnitFC>();
                foreach (MilUnitFC u in newTemplate.Units)
                {
                    if (u is object && !u.isBlank) retainable.Add(u);
                }
            }

            for (int i = squad.mercenaries.Count - 1; i >= 0; i--)
            {
                Mercenary m = squad.mercenaries[i];
                if (m is null) continue;
                if (m.pawn is object) continue;                  // only prune empty slots
                MilUnitFC bp = m.loadout;
                if (bp is null || bp.isBlank) continue;          // already a blank placeholder
                if (retainable != null && retainable.Contains(bp)) continue; // template still owns this slot
                squad.mercenaries.RemoveAt(i);
            }
            FindFC.Military?.RebuildMercenaryPawnSet();
        }

        /// <summary>Race + xenotype-aware re-template. For each template slot:
        /// reuse an existing merc whose pawn race (and Biotech xenotype) matches; otherwise
        /// fresh-hire a new pawn. Mercs unclaimed by any slot are fired.
        /// Net cost = upgrade-diff + fresh-hire.</summary>
        public static bool UpgradeToTemplate(MercenarySquadFC squad)
        {
            if (squad?.outfit?.Units is null || squad.mercenaries is null) return false;
            if (squad.IsBusy)
            {
                Messages.Message("FCSquadCannotUpgradeBusy".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            UpgradePlan plan = BuildUpgradePlan(squad);
            if (!plan.HasWork) return false;
            int net = plan.UpgradeSilver + plan.FreshHireSilver;

            if (net > 0 && !PaymentUtil.TryPaySilver(net, PaymentUtil.Reason_SquadUpgrade, squad.settlement))
            {
                Messages.Message("FCSquadUpgradeInsufficientSilver".Translate(net), MessageTypeDefOf.RejectInput, false);
                return false;
            }

            // Reset the equipment tracker's snapshots; Equipment.EquipPawn will repopulate
            // them as we re-equip each slot below.
            squad.Equipment.UsedWeaponList = new List<ThingWithComps>();
            squad.Equipment.UsedApparelList = new List<Apparel>();

            // Fire pass: strip + destroy each fired merc's pawn. Merc instances are dropped
            // from the rebuilt mercenaries list below.
            foreach (Mercenary m in plan.Fires)
            {
                if (m is null) continue;
                squad.Equipment.StripPawn(m);
                if (m.pawn != null && !m.pawn.Destroyed) m.pawn.Destroy();
                m.pawn = null;
                squad.Equipment.ClearSubPawns(m);      // destroy + drop this merc's animals + bonded mechs
            }

            // Slot pass: re-equip claims, fresh-hire fresh slots. The slot order in
            // plan.Slots matches outfit.Units traversal order, so the rebuilt list stays
            // in slot order.
            List<Mercenary> rebuilt = new List<Mercenary>();
            foreach (SlotDecision dec in plan.Slots)
            {
                MilUnitFC slotUnit = dec.slotUnit;
                MilUnitFC target = dec.target;
                Mercenary merc;

                if (dec.claim != null)
                {
                    merc = dec.claim;
                    MilUnitFC prior = merc.currentLoadout;
                    bool implantsChanged = LoadoutUpgradeUtil.ImplantsChanged(target, prior);
                    bool psycastsChanged = LoadoutUpgradeUtil.PsycastsChanged(target, prior);
                    bool mechanitorChanged = LoadoutUpgradeUtil.MechanitorChanged(target, prior);
                    squad.Equipment.StripPawn(merc);
                    squad.Equipment.EquipPawn(merc, target);
                    // Implants are surgical and psycasts are provider-managed — reconcile each in place
                    // (no regeneration) when changed.
                    if (implantsChanged)
                        MilUnitFC.ReconcileImplantsOnPawn(merc.pawn, target, prior);
                    if (psycastsChanged)
                        MilUnitFC.ReconcilePsycastsOnPawn(merc.pawn, target);
                    // A reused pawn was generated under its prior loadout, so it may lack the mechlink
                    // when the target newly makes it a mechanitor — apply it (idempotent) before mechs
                    // get reconciled below so pawn.mechanitor exists for bonding.
                    if (mechanitorChanged)
                        MilUnitFC.ApplyMechanitorToPawn(merc.pawn, target);
                    /* Re-sync the pool pointer to the live template slot (repairs a stale
                       'loadout' after a SwapTemplate). ownedLoadout is deliberately left
                       intact — a bulk upgrade APPLIES personalization, it doesn't discard it. */
                    merc.loadout = slotUnit;
                    merc.currentLoadout = target.Clone();
                }
                else
                {
                    merc = new Mercenary(true);
                    MercenaryPawnFactory.CreateNewPawn(squad, ref merc, slotUnit.pawnKind, slotUnit.xenotype, slotUnit.customXenotypeName, slotUnit);
                    if (merc.pawn == null)
                    {
                        LogUtil.Warning($"UpgradeToTemplate: failed to generate fresh pawn for slot {dec.slotIndex}");
                        continue;
                    }
                    squad.Equipment.EquipPawn(merc, target);
                    merc.squad = squad;
                    merc.settlement = squad.settlement;
                    merc.loadout = slotUnit;
                    merc.ownedLoadout = null;            // fresh pawn — no personalization
                    merc.currentLoadout = target.Clone();
                }

                squad.Equipment.ReconcileAnimal(merc, target);
                squad.Equipment.ReconcileMechs(merc, target);

                rebuilt.Add(merc);
            }
            squad.mercenaries = rebuilt;
            FindFC.Military?.RebuildMercenaryPawnSet();
            LifecycleRegistry.InvokeOnSquadUpgraded(squad);
            return true;
        }
    }
}
