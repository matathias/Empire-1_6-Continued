using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionColonies.util
{
    /// <summary>
    /// Single source of truth for the squad-status label rendered across the squad picker,
    /// the squads tab, and the settlement card. Priority order (first match wins):
    /// Unassigned -> Busy in op -> Traveling (travel cooldown) -> Healing (injuries) ->
    /// Empty Slots -> Ready. The Traveling state is the new shrunken cooldown; the Healing
    /// state replaces the old multi-day cooldown's role and is informational only (it does
    /// NOT gate selection; the squad picker still allows deploying an injured squad).
    /// </summary>
    public static class SquadStatusUtil
    {
        /// <summary>Resolves the squad's current display status. <paramref name="isReady"/> is
        /// true only when the squad is fully available with no travel/healing/empty-slot
        /// caveats. UI uses it to color the row green vs amber.</summary>
        public static void Resolve(MercenarySquadFC squad, out string label, out Color color, out bool isReady)
        {
            isReady = false;
            label = "?";
            color = AccentUtil.MilInactive;
            if (squad is null) return;

            if (!squad.IsAssigned)
            {
                label = "FCSquadStatusUnassigned".Translate();
                color = AccentUtil.MilInactive;
                return;
            }

            int now = Find.TickManager.TicksGame;
            MilitaryOperation op = squad.Operation;
            if (op is object && op.kind != MilitaryJobDefOf.Cooldown
                && op.phase != MilitaryOperationPhase.CooldownPending)
            {
                int ticksLeft = Math.Max(0, op.nextPhaseTick - now);
                // Prefer the short status verb ("Defending", "Raiding") over the verbose
                // op.kind.label ("defend friendly settlement") so the badge stays one line.
                // Mirrors AccentUtil.GetMilitaryStatusLabel's statusLabelKey fallback.
                string opLabel = op.kind?.statusLabelKey != null
                    ? (string)op.kind.statusLabelKey.Translate()
                    : (op.kind?.label ?? "?");
                label = "FCSquadStatusBusyOp".Translate(opLabel, ticksLeft.ToTimeString());
                color = AccentUtil.MilActiveMission;
                return;
            }

            // Travel cooldown: squad is post-op and still returning home. Distinct from the
            // old long cooldown; this is just travel time (24h defense / TravelUtil offense).
            if (squad.nextAvailableTick > now)
            {
                int ticksLeft = squad.nextAvailableTick - now;
                label = "FCSquadStatusTraveling".Translate(ticksLeft.ToTimeString());
                color = AccentUtil.MilCooldown;
                return;
            }

            // Healing: informational only. Squad is back at home but injured; combat power
            // is reduced and a healing-time estimate is shown so the player can decide
            // whether to deploy now or wait. Empty slots are surfaced separately below.
            int healTicks = SquadHealingEstimator.TicksToFullEffectiveness(squad);
            if (healTicks > 0)
            {
                label = "FCSquadStatusHealing".Translate(healTicks.ToTimeString());
                color = AccentUtil.MilCooldown;
                return;
            }

            int emptySlots = squad.EmptySlotCount;
            if (emptySlots > 0)
            {
                label = "FCSquadStatusEmptySlots".Translate(emptySlots);
                color = AccentUtil.MilCooldown;
                return;
            }

            label = "FCSquadStatusReady".Translate();
            color = AccentUtil.MilReady;
            isReady = true;
        }

        /// <summary>
        /// Appends every reason the squad currently fails <see cref="MercenarySquadFC.IsAvailable"/>
        /// (all applicable, not first-wins) as translated, player-facing lines. Mirrors <see cref="Resolve"/>'s
        /// gate order. Callers append context-specific blocks (morale lockout, destination budget) themselves.
        /// </summary>
        public static void AppendIntrinsicUnavailReasons(MercenarySquadFC squad, List<string> reasons)
        {
            if (squad is null || reasons is null) return;

            if (!squad.IsAssigned)
            {
                // The remaining gates are settlement-relative and moot without a billet.
                reasons.Add("FCSquadUnavailUnassigned".Translate());
                return;
            }

            int now = Find.TickManager.TicksGame;
            MilitaryOperation op = squad.Operation;
            if (op is object && op.kind != MilitaryJobDefOf.Cooldown
                && op.phase != MilitaryOperationPhase.CooldownPending)
            {
                reasons.Add(BusyReason(squad));
            }
            if (squad.nextAvailableTick > now)
            {
                reasons.Add("FCSquadUnavailTraveling".Translate((squad.nextAvailableTick - now).ToTimeString()));
            }
            if (squad.IsUnderfunded && squad.settlement is object
                && MilitaryFC.SquadExceedsSettlementBudget(squad, squad.settlement, out int squadDeploy, out int maxDeploy))
            {
                reasons.Add(OverBudgetReason(squadDeploy, maxDeploy, squad.settlement.Name));
            }
        }

        /// <summary>
        /// Single-line "occupied by op X" reason. Op label + remaining time mirror the
        /// busy-op status badge (see <see cref="Resolve"/>).
        /// </summary>
        public static string BusyReason(MercenarySquadFC squad)
        {
            MilitaryOperation op = squad?.Operation;
            if (op is null) return "FCSquadUnavailBusy".Translate("?", 0.ToTimeString());
            int ticksLeft = Math.Max(0, op.nextPhaseTick - Find.TickManager.TicksGame);
            string opLabel = op.kind?.statusLabelKey != null
                ? (string)op.kind.statusLabelKey.Translate()
                : (op.kind?.label ?? "?");
            return "FCSquadUnavailBusy".Translate(opLabel, ticksLeft.ToTimeString());
        }

        /// <summary>
        /// Single-line "deploy cost exceeds budget" reason. Used both for the intrinsic
        /// (own-settlement) underfunded gate and the assign picker's destination-budget gate.
        /// </summary>
        public static string OverBudgetReason(int deployCost, int maxCost, string settlementName)
        {
            return "FCSquadUnavailUnderfunded".Translate(deployCost, settlementName ?? "?", maxCost);
        }

        /// <summary>
        /// Wraps reason lines into a titled tooltip; returns null when there are none so
        /// callers can gate the <c>TipRegion</c> registration on a non-empty result.
        /// </summary>
        public static string FormatUnavailTooltip(List<string> reasons)
        {
            if (reasons is null || reasons.Count == 0) return null;
            string title = "FCSquadUnavailTitle".Translate();
            return title + "\n- " + string.Join("\n- ", reasons.ToArray());
        }
    }
}
