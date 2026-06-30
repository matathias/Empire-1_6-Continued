using FactionColonies.util;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public static class AccentUtil
    {
        // === Profit/Loss (Overview, Bills) ===
        public static readonly Color Income = new Color(0.2f, 0.85f, 0.3f);
        public static readonly Color Expense = new Color(1.0f, 0.35f, 0.3f);

        // === Military Status ===
        public static readonly Color MilUnderAttack = new Color(1.0f, 0.25f, 0.25f);
        public static readonly Color MilActiveMission = new Color(1.0f, 0.65f, 0.1f);
        public static readonly Color MilCooldown = new Color(1.0f, 0.85f, 0.1f);
        public static readonly Color MilReady = new Color(0.2f, 0.85f, 0.3f);
        // Blue — for a unit holding a passive watch (e.g. a defensive outpost projecting its aura).
        // Reads as "standing by / shielding", distinct from MilReady's "armed & active" green.
        public static readonly Color MilDefensive = new Color(0.3f, 0.6f, 0.9f);
        public static readonly Color MilInactive = new Color(0.65f, 0.65f, 0.65f);
        // Bright amber — for squads whose DeploymentCost exceeds settlement budget. Red is
        // reserved for under-attack so this needs to read as "attention" without "danger".
        public static readonly Color MilUnderfunded = new Color(1.0f, 0.75f, 0.0f);

        // === Stat Thresholds ===
        public static readonly Color StatGood = new Color(0.2f, 0.85f, 0.3f);
        public static readonly Color StatMedGood = Color.yellow;
        public static readonly Color StatMedBad = new Color(1f, 0.7f, 0.2f);
        public static readonly Color StatBad = Color.red; //new Color(1f, 0.35f, 0.3f);

        // === Generic Color settings ===
        public static readonly Color Military = new Color(1.0f, 0.25f, 0.25f);

        public static Color GetSettlementAccent(WorldSettlementFC s)
        {
            // ProjectedProfit reflects the forecast for the next tax tick;
            // falls back to live profit (totalIncome - totalUpkeep) when no accrual data exists.
            return s.settlementDef.accentColor ?? (s.ProjectedProfit >= 0 ? Income : Expense);
        }

        public static Color GetStatColor(float value, bool inverted)
        {
            if (inverted)
            {
                if (value <= 10f) return StatGood;
                if (value <= 40f) return StatMedGood;
                if (value <= 80f) return StatMedBad;
                return StatBad;
            }
            if (value >= 80f) return StatGood;
            if (value >= 50f) return StatMedGood;
            if (value >= 20f) return StatMedBad;
            return StatBad;
        }

        public static FCEventCategoryDef GetEventCategory(FCEvent evt)
        {
            return evt.def?.category ?? FCEventCategoryDefOf.EC_Other;
        }

        public static Color GetEventCategoryColor(FCEvent evt)
        {
            return GetEventCategory(evt).color;
        }

        public static Color GetMilitaryAccent(WorldObjectComp_SettlementMilitary milComp)
        {
            if (milComp == null) return MilInactive;
            if (milComp.isUnderAttack) return MilUnderAttack;
            if (milComp.militaryBusy && (!milComp.militaryJob.isState || milComp.militaryJob == MilitaryJobDefOf.DefendFriendlySettlement))
                return MilActiveMission;
            if (milComp.militaryJob == MilitaryJobDefOf.Cooldown) return MilCooldown;
            if (!milComp.militaryBusy && AnyStationedSquadHasOutfit(milComp.WorldSettlement))
                return MilReady;
            return MilInactive;
        }

        /// <summary>Accent color for a squad row, driven by the squad's own state — NOT its
        /// settlement's. Red is reserved for squads that are part of an active defending force
        /// (their home settlement is the actual defender of a defensive op). Squads merely
        /// billeted at a settlement that's under attack but not part of the defending force
        /// fall through to their own state color.</summary>
        public static Color GetSquadAccent(MercenarySquadFC squad)
        {
            if (squad is null || squad.settlement is null) return MilInactive;

            // Red: this squad's settlement is the active defender of a defensive op. The
            // squad is part of the defending force as a stationed unit. We check defender
            // homeSettlement (not isUnderAttack on the target) so a squad billeted at the
            // attack target but with the defender swapped elsewhere doesn't show red.
            MilitaryOperationManager manager = FindFC.MilitaryManager;
            if (manager is object)
            {
                IReadOnlyList<MilitaryOperation> ops = manager.GetOpsForSettlement(squad.settlement);
                for (int i = 0; i < ops.Count; i++)
                {
                    MilitaryOperation op = ops[i];
                    if (op.IsDefensive && op.defender?.homeSettlement == squad.settlement)
                        return MilUnderAttack;
                }
            }

            MilitaryOperation own = squad.Operation;
            if (own is object)
            {
                if (own.kind == MilitaryJobDefOf.Cooldown) return MilCooldown;
                return MilActiveMission;
            }

            if (squad.outfit != null) return MilReady;
            return MilInactive;
        }

        private static bool AnyStationedSquadHasOutfit(WorldSettlementFC settlement)
        {
            if (settlement is null) return false;
            List<MercenarySquadFC> stationed = settlement.StationedSquads;
            for (int i = 0; i < stationed.Count; i++)
            {
                if (stationed[i]?.outfit != null) return true;
            }
            return false;
        }

        public static string GetMilitaryStatusLabel(WorldObjectComp_SettlementMilitary milComp, WorldSettlementFC settlement = null)
        {
            if (milComp == null) return "FCMilStatusNoSquad".Translate();
            if (milComp.isUnderAttack) return "FCMilStatusUnderAttack".Translate();
            if (milComp.militaryBusy)
            {
                if (milComp.militaryJob == MilitaryJobDefOf.Cooldown)
                    return GetCooldownLabel(settlement);
                if (milComp.militaryJob == MilitaryJobDefOf.DefendFriendlySettlement
                    && milComp.militaryLocation.Valid)
                {
                    WorldObject target = Find.WorldObjects.WorldObjectAt<WorldObject>(milComp.militaryLocation);
                    if (target != null)
                        return "FCMilStatusDefendingTarget".Translate(target.LabelCap);
                }
                return milComp.militaryJob.statusLabelKey != null
                    ? milComp.militaryJob.statusLabelKey.Translate()
                    : "FCMilStatusBusy".Translate();
            }
            if (AnyStationedSquadHasOutfit(milComp.WorldSettlement)) return "FCMilStatusReady".Translate();
            return "FCMilStatusNoSquad".Translate();
        }

        private static string GetCooldownLabel(WorldSettlementFC settlement)
        {
            // The op-level cooldown is now the squad's travel-home window, not a long heal-out
            // gate, so the label says "Traveling" to match the squad-level status pipeline.
            string label = "FCMilStatusTraveling".Translate();
            if (settlement == null) return label;

            FCEvent cooldownEvent = FindFC.FactionComp?.FindEventByDefAndLocation(FCEventDefOf.cooldownMilitary, settlement.Tile);
            if (cooldownEvent != null)
            {
                int ticksLeft = Math.Max(0, cooldownEvent.timeTillTrigger - Find.TickManager.TicksGame);
                label += " " + ticksLeft.ToTimeString();
            }
            return label;
        }
    }
}
