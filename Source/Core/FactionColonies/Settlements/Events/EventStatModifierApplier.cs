using System.Collections.Generic;
using System.Linq;
using FactionColonies.util;

namespace FactionColonies
{
    /* Owns the four-way loop pattern (location-targeted vs faction-wide, add vs remove)
     * and the "event_" + defName source-id literal used by FCEventDef stat application.
     *
     * IMPORTANT: must pass evt.def.statModifiers directly (never a copy) so
     * WorldSettlementFC.RemoveStatModifiers's reference-equality match holds. */
    internal static class EventStatModifierApplier
    {
        private static string SourceId(FCEvent evt) => "event_" + evt.def.defName;

        /// <summary>Applies an event's stat + permanent stat modifiers to its targeted
        /// settlements (or all settlements if untargeted) and invalidates the faction
        /// stat cache once at the end.</summary>
        public static void Apply(FCEvent evt, FactionFC faction)
        {
            if (evt?.def is null || faction is null) return;

            /* Freeze the settlement cohort this event applies to, so Remove/reload reverse
             * exactly this set. Recorded even for prosperity-only events (which have no
             * stat/permanent modifiers) so Remove's prosperityLost hits only the cohort. */
            List<WorldSettlementFC> cohort = evt.settlementTraitLocations.Count > 0
                ? evt.settlementTraitLocations.Where(s => s != null).ToList()
                : faction.settlements.Where(s => s != null).ToList();
            evt.appliedStatSettlements = cohort;

            string sourceId = SourceId(evt);
            string label = evt.def.label;
            bool hasStats = evt.def.statModifiers != null && evt.def.statModifiers.Count > 0;
            bool hasPermanent = evt.def.permanentStatModifiers != null && evt.def.permanentStatModifiers.Count > 0;
            if (!hasStats && !hasPermanent) return;

            foreach (WorldSettlementFC settlement in cohort)
            {
                if (hasStats) settlement.AddStatModifiers(evt.def.statModifiers, sourceId, label);
                if (hasPermanent) settlement.AddPermanentModifiers(evt.def.permanentStatModifiers, sourceId, label);
            }

            faction.InvalidateFactionStatCache();
        }

        /// <summary>Removes the event's stat modifiers from its targeted settlements
        /// (or all settlements) and applies prosperityLost. Permanent modifiers are NOT
        /// removed. Invalidates the faction stat cache once at the end.</summary>
        public static void Remove(FCEvent evt, FactionFC faction)
        {
            if (evt?.def is null || faction is null) return;

            string sourceId = SourceId(evt);
            bool hasStats = evt.def.statModifiers != null && evt.def.statModifiers.Count > 0;
            double prosperityLost = evt.def.prosperityLost;

            /* Reverse exactly the cohort Apply recorded. Legacy in-flight events (recorded
             * before appliedStatSettlements existed) fall back to targeted-or-all. Nulls are
             * skipped in the loop so a since-removed settlement can't NRE it. */
            List<WorldSettlementFC> cohort = ResolveRemovalCohort(evt, faction);

            foreach (WorldSettlementFC settlement in cohort)
            {
                if (settlement is null) continue;
                if (hasStats) settlement.RemoveStatModifiers(evt.def.statModifiers, sourceId);
                settlement.prosperity -= prosperityLost;
            }

            faction.InvalidateFactionStatCache();
        }

        /// <summary>Selects the settlement list that Remove reverses: the frozen apply cohort
        /// when recorded, else the legacy targeted-or-all fallback. Pure seam for testing.</summary>
        internal static List<WorldSettlementFC> ResolveRemovalCohort(FCEvent evt, FactionFC faction)
        {
            if (evt.appliedStatSettlements != null) return evt.appliedStatSettlements;
            if (evt.settlementTraitLocations.Count > 0) return evt.settlementTraitLocations;
            return faction?.settlements;
        }

        /// <summary>Settlement-scoped reapply used by WorldSettlementFC.PostLoadInit.
        /// Does NOT touch the faction stat cache — the load path owns its own cascade.</summary>
        public static void ApplyForSettlement(FCEvent evt, WorldSettlementFC settlement)
        {
            if (evt?.def?.statModifiers is null || evt.def.statModifiers.Count == 0) return;
            if (settlement is null) return;
            /* Reapply only to the recorded cohort so reload matches the original apply set.
             * Legacy events (no cohort) fall back to the targeted-or-all gate. */
            if (evt.appliedStatSettlements != null)
            {
                if (!evt.appliedStatSettlements.Contains(settlement)) return;
            }
            else if (evt.settlementTraitLocations.Count != 0 && !evt.settlementTraitLocations.Contains(settlement))
            {
                return;
            }

            settlement.AddStatModifiers(evt.def.statModifiers, SourceId(evt), evt.def.label);
        }
    }
}
