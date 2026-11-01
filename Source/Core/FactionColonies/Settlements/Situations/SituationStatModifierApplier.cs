using System.Collections.Generic;
using FactionColonies.util;

namespace FactionColonies
{
    /// <summary>
    /// Applies / removes a situation's stage and approach <see cref="FCStatModifier"/>s on its target
    /// settlement(s), under a per-instance source-ID namespace (<c>situation_&lt;loadID&gt;_stage</c> /
    /// <c>_approach</c>). Removal is by-source, which is correct for per-instance teardown and robust
    /// across save/load (no need to hold the original list reference).
    ///
    /// This is a small parallel applier rather than a generalization of <c>EventStatModifierApplier</c>:
    /// situations share none of the event applier's orchestration (def-keyed source, four-way
    /// settlementTraitLocations targeting, permanent/prosperity baggage). Both call the same already
    /// source-agnostic <c>WorldSettlementFC</c> primitives.
    /// </summary>
    internal static class SituationStatModifierApplier
    {
        private static IEnumerable<WorldSettlementFC> Targets(FCSituation sit, FactionFC faction)
        {
            if (sit.targetSettlement != null)
            {
                yield return sit.targetSettlement;
            }
            else if (faction != null)
            {
                foreach (WorldSettlementFC s in faction.settlements)
                    if (s != null) yield return s;
            }
        }

        public static void ApplyStage(FCSituation sit, FactionFC faction, bool invalidate = true)
        {
            List<FCStatModifier> mods = sit?.currentStage?.statModifiers;
            if (mods == null || mods.Count == 0) return;
            foreach (WorldSettlementFC s in Targets(sit, faction))
                s.AddStatModifiers(mods, sit.StageSourceId, sit.def.label);
            if (invalidate) faction?.InvalidateFactionStatCache();
        }

        public static void RemoveStage(FCSituation sit, FactionFC faction, bool invalidate = true)
        {
            if (sit == null) return;
            foreach (WorldSettlementFC s in Targets(sit, faction))
                s.RemoveStatModifiersBySource(sit.StageSourceId);
            if (invalidate) faction?.InvalidateFactionStatCache();
        }

        public static void ApplyApproach(FCSituation sit, FactionFC faction, bool invalidate = true)
        {
            List<FCStatModifier> mods = sit?.activeApproach?.statModifiers;
            if (mods == null || mods.Count == 0) return;
            foreach (WorldSettlementFC s in Targets(sit, faction))
                s.AddStatModifiers(mods, sit.ApproachSourceId, sit.def.label);
            if (invalidate) faction?.InvalidateFactionStatCache();
        }

        public static void RemoveApproach(FCSituation sit, FactionFC faction, bool invalidate = true)
        {
            if (sit == null) return;
            foreach (WorldSettlementFC s in Targets(sit, faction))
                s.RemoveStatModifiersBySource(sit.ApproachSourceId);
            if (invalidate) faction?.InvalidateFactionStatCache();
        }

        /// <summary>Settlement-scoped reapply used by WorldSettlementFC.PostLoadInit (mirrors
        /// EventStatModifierApplier.ApplyForSettlement). A faction-scoped situation (null
        /// targetSettlement) applies to every settlement — each one pulls it in during its own
        /// PostLoadInit — while a settlement-scoped one applies only to its target. Does NOT touch the
        /// faction stat cache; the load path owns its own cascade.</summary>
        public static void ApplyForSettlement(FCSituation sit, WorldSettlementFC settlement)
        {
            if (sit == null || settlement == null) return;
            if (sit.targetSettlement != null && sit.targetSettlement != settlement) return;
            if (sit.currentStage?.statModifiers != null && sit.currentStage.statModifiers.Count > 0)
                settlement.AddStatModifiers(sit.currentStage.statModifiers, sit.StageSourceId, sit.def.label);
            if (sit.activeApproach?.statModifiers != null && sit.activeApproach.statModifiers.Count > 0)
                settlement.AddStatModifiers(sit.activeApproach.statModifiers, sit.ApproachSourceId, sit.def.label);
        }

        /// <summary>Strips both source namespaces in one cache invalidation (situation teardown).</summary>
        public static void RemoveAll(FCSituation sit, FactionFC faction)
        {
            if (sit == null) return;
            foreach (WorldSettlementFC s in Targets(sit, faction))
            {
                s.RemoveStatModifiersBySource(sit.StageSourceId);
                s.RemoveStatModifiersBySource(sit.ApproachSourceId);
            }
            faction?.InvalidateFactionStatCache();
        }
    }
}
