using System.Collections.Generic;

namespace FactionColonies
{
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
    /* A faction-wide event's expiry must reverse exactly the settlement cohort it
     * applied to (frozen at Apply time), so a settlement founded mid-event is neither
     * buffed nor penalized. Exercises the pure EventStatModifierApplier.ResolveRemovalCohort
     * seam so no live settlement state is mutated.
     *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
    public static class EventCohortTests
    {
        [EmpireTest("Settlement")]
        public static void ResolveRemovalCohort_PrefersRecordedCohort_OverTraitLocations()
        {
            var recorded = new List<WorldSettlementFC> { null };
            var evt = new FCEvent
            {
                appliedStatSettlements = recorded,
                // Populated but must be ignored while a recorded cohort exists.
                settlementTraitLocations = new List<WorldSettlementFC> { null, null }
            };

            // faction null is safe: the recorded cohort short-circuits before the fallback.
            List<WorldSettlementFC> cohort = EventStatModifierApplier.ResolveRemovalCohort(evt, null);

            TestAssert.AreEqual(recorded, cohort,
                "the recorded apply cohort should take precedence over trait locations");
        }

        [EmpireTest("Settlement")]
        public static void ResolveRemovalCohort_FallsBackToTraitLocations_WhenNoCohort()
        {
            var traits = new List<WorldSettlementFC> { null };
            var evt = new FCEvent
            {
                appliedStatSettlements = null,
                settlementTraitLocations = traits
            };

            List<WorldSettlementFC> cohort = EventStatModifierApplier.ResolveRemovalCohort(evt, null);

            TestAssert.AreEqual(traits, cohort,
                "a legacy event with no recorded cohort should reverse its trait locations");
        }
    }
}
