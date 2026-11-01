using System;
using System.Collections.Generic;
using FactionColonies.util;
using Verse;

namespace FactionColonies
{
    /* Coverage for the event dynamic-cost subsystem: FCEventScalingUtil.CountAffectedSettlements
     * and FCOptionCostUtil.ComputeScaledCost / BuildCostBreakdown. EventSystemTests already covers
     * event-def integrity and FCEventMaker, but not the cost-scaling math, which is pure. */
    public static class EventCostTests
    {
        /* Snapshot/restore the global event-cost multiplier so a test never leaks state into the
         * next one (mirrors StatTests.WithTestModifier's try/finally discipline). */
        private static void WithEventCostMultiplier(float value, Action action)
        {
            float original = FCSettings.eventSilverCostMultiplier;
            FCSettings.eventSilverCostMultiplier = value;
            try { action(); }
            finally { FCSettings.eventSilverCostMultiplier = original; }
        }

        // ============================
        // FCEventScalingUtil.CountAffectedSettlements
        // ============================

        [EmpireTest("EventCost")]
        public static void CountAffected_NullEvent_ReturnsOne()
        {
            TestAssert.AreEqual(1, FCEventScalingUtil.CountAffectedSettlements(null));
        }

        [EmpireTest("EventCost")]
        public static void CountAffected_NullList_ReturnsOne()
        {
            var evt = new FCEvent { settlementTraitLocations = null };
            TestAssert.AreEqual(1, FCEventScalingUtil.CountAffectedSettlements(evt));
        }

        [EmpireTest("EventCost")]
        public static void CountAffected_EmptyList_ReturnsOne()
        {
            var evt = new FCEvent { settlementTraitLocations = new List<WorldSettlementFC>() };
            TestAssert.AreEqual(1, FCEventScalingUtil.CountAffectedSettlements(evt));
        }

        [EmpireTest("EventCost")]
        public static void CountAffected_AllNullEntries_FlooredAtOne()
        {
            var evt = new FCEvent { settlementTraitLocations = new List<WorldSettlementFC> { null, null } };
            TestAssert.AreEqual(1, FCEventScalingUtil.CountAffectedSettlements(evt));
        }

        [EmpireTest("EventCost")]
        public static void CountAffected_CountsNonNullEntries()
        {
            var settlements = FindFC.Settlements;
            if (settlements == null || settlements.Count < 2) TestAssert.Skip("Need >= 2 settlements");

            var list = new List<WorldSettlementFC> { settlements[0], null, settlements[1] };
            var evt = new FCEvent { settlementTraitLocations = list };
            TestAssert.AreEqual(2, FCEventScalingUtil.CountAffectedSettlements(evt));
        }

        // ============================
        // FCOptionCostUtil.ComputeScaledCost
        // ============================

        [EmpireTest("EventCost")]
        public static void ComputeScaledCost_NullOption_ReturnsZero()
        {
            var evt = new FCEvent();
            TestAssert.AreEqual(0, FCOptionCostUtil.ComputeScaledCost(null, evt));
        }

        [EmpireTest("EventCost")]
        public static void ComputeScaledCost_NullEvent_UsesEffectiveSilverCost()
        {
            WithEventCostMultiplier(1f, () =>
            {
                var opt = new FCOptionDef { silverCost = 100 };
                TestAssert.AreEqual(opt.EffectiveSilverCost,
                    FCOptionCostUtil.ComputeScaledCost(opt, null));
            });
        }

        [EmpireTest("EventCost")]
        public static void ComputeScaledCost_BaseTimesMultiplier()
        {
            var opt = new FCOptionDef { silverCost = 100 };
            var evt = new FCEvent(); // empty trait list -> affected = 1
            WithEventCostMultiplier(1f, () =>
                TestAssert.AreEqual(100, FCOptionCostUtil.ComputeScaledCost(opt, evt)));
            WithEventCostMultiplier(2f, () =>
                TestAssert.AreEqual(200, FCOptionCostUtil.ComputeScaledCost(opt, evt)));
        }

        [EmpireTest("EventCost")]
        public static void ComputeScaledCost_NegativeSilver_ClampsBaseToZero()
        {
            var opt = new FCOptionDef { silverCost = -50 };
            var evt = new FCEvent();
            WithEventCostMultiplier(1f, () =>
                TestAssert.AreEqual(0, FCOptionCostUtil.ComputeScaledCost(opt, evt)));
        }

        [EmpireTest("EventCost")]
        public static void ComputeScaledCost_RoundsAwayFromZero()
        {
            // base 1 * 2.5 = 2.5 -> AwayFromZero rounds to 3 (banker's rounding would give 2).
            // 2.5f is exactly representable, so this is not float-fragile.
            var opt = new FCOptionDef { silverCost = 1 };
            var evt = new FCEvent();
            WithEventCostMultiplier(2.5f, () =>
                TestAssert.AreEqual(3, FCOptionCostUtil.ComputeScaledCost(opt, evt)));
        }

        /* Shared setup for the production-delta tests: the non-null current settlements, the food
         * resource def (skips the test if either is unavailable), and helpers that build the option
         * and compute the expected pre-coefficient delta from the same public ResourceFC accessors
         * the util uses (so the assertions check wiring/scope, not a hardcoded constant). */
        private static List<WorldSettlementFC> DeltaTestSettlements()
        {
            var settlements = FindFC.Settlements;
            if (settlements is null || settlements.Count < 1) TestAssert.Skip("Need >= 1 settlement");
            var list = new List<WorldSettlementFC>();
            foreach (WorldSettlementFC s in settlements)
                if (s is object) list.Add(s);
            if (list.Count == 0) TestAssert.Skip("No usable settlements");
            return list;
        }

        private static double ExpectedDelta(List<WorldSettlementFC> list, ResourceTypeDef res, float additiveDelta)
        {
            double sum = 0;
            foreach (WorldSettlementFC s in list)
            {
                ResourceFC r = s.GetResource(res);
                if (r is null) continue;
                double d = additiveDelta * r.productionMult * r.assignedWorkers;
                if (d > 0) sum += d;
            }
            return sum;
        }

        private static FCOptionDef DeltaOption(ResourceTypeDef res, float additiveDelta, float coeff, FCResourceCostFraming framing)
        {
            var ext = new FCDynamicCostExtension
            {
                costPerResourceDelta = new List<ResourceProductionDeltaCost>
                {
                    new ResourceProductionDeltaCost { resource = res, additiveDelta = additiveDelta, coefficient = coeff, framing = framing }
                }
            };
            return new FCOptionDef { silverCost = 100, modExtensions = new List<DefModExtension> { ext } };
        }

        [EmpireTest("EventCost")]
        public static void ComputeScaledCost_ResourceDelta_ChargesMarginalProduction()
        {
            // The resource term charges for the production the option changes, not total output:
            // per settlement, additiveDelta * productionMult * assignedWorkers, summed over the
            // affected settlements, times silverPerResource times the coefficient (plus base * affected).
            var list = DeltaTestSettlements();
            ResourceTypeDef food = DefDatabase<ResourceTypeDef>.GetNamedSilentFail("RTD_Food");
            if (food is null) TestAssert.Skip("RTD_Food not loaded");

            const float additive = 0.5f;
            const float coeff = 0.5f; // fraction of silver-per-unit value
            double delta = ExpectedDelta(list, food, additive);
            var evt = new FCEvent { settlementTraitLocations = list };

            WithEventCostMultiplier(1f, () =>
            {
                double raw = 100.0 * list.Count + delta * FCSettings.silverPerResource * coeff;
                int expected = Math.Max(0, (int)Math.Round(raw, MidpointRounding.AwayFromZero));
                TestAssert.AreEqual(expected, FCOptionCostUtil.ComputeScaledCost(
                    DeltaOption(food, additive, coeff, FCResourceCostFraming.Mitigated), evt));
            });
        }

        [EmpireTest("EventCost")]
        public static void ComputeScaledCost_Framing_DoesNotAffectCost()
        {
            // Framing is presentational only — Added vs Mitigated must yield identical silver.
            var list = DeltaTestSettlements();
            ResourceTypeDef food = DefDatabase<ResourceTypeDef>.GetNamedSilentFail("RTD_Food");
            if (food is null) TestAssert.Skip("RTD_Food not loaded");
            var evt = new FCEvent { settlementTraitLocations = list };

            WithEventCostMultiplier(1f, () =>
            {
                int mit = FCOptionCostUtil.ComputeScaledCost(DeltaOption(food, 0.5f, 0.5f, FCResourceCostFraming.Mitigated), evt);
                int add = FCOptionCostUtil.ComputeScaledCost(DeltaOption(food, 0.5f, 0.5f, FCResourceCostFraming.Added), evt);
                TestAssert.AreEqual(mit, add);
            });
        }

        // ============================
        // FCOptionCostUtil.BuildCostBreakdown
        // ============================

        [EmpireTest("EventCost")]
        public static void BuildCostBreakdown_Framing_PicksWording()
        {
            // The breakdown line wording follows the framing flag. Case-insensitive so it passes
            // whether the keyed string is translated ("added") or falls back to the key ("...Added").
            var list = DeltaTestSettlements();
            ResourceTypeDef food = DefDatabase<ResourceTypeDef>.GetNamedSilentFail("RTD_Food");
            if (food is null) TestAssert.Skip("RTD_Food not loaded");
            // Need a non-zero contribution (delta * silverPerResource * coeff) for the resource line to appear.
            if (ExpectedDelta(list, food, 0.5f) <= 0 || FCSettings.silverPerResource <= 0)
                TestAssert.Skip("No food production or zero silver-per-resource to scale on");
            var evt = new FCEvent { settlementTraitLocations = list };

            WithEventCostMultiplier(1f, () =>
            {
                string added = FCOptionCostUtil.BuildCostBreakdown(DeltaOption(food, 0.5f, 0.5f, FCResourceCostFraming.Added), evt);
                string mit = FCOptionCostUtil.BuildCostBreakdown(DeltaOption(food, 0.5f, 0.5f, FCResourceCostFraming.Mitigated), evt);
                TestAssert.IsTrue(added != null && added.ToLowerInvariant().Contains("added"), "Added framing should read 'added'");
                TestAssert.IsTrue(mit != null && mit.ToLowerInvariant().Contains("mitigated"), "Mitigated framing should read 'mitigated'");
            });
        }

        [EmpireTest("EventCost")]
        public static void BuildCostBreakdown_NullEvent_ReturnsNull()
        {
            var opt = new FCOptionDef { silverCost = 100 };
            TestAssert.IsNull(FCOptionCostUtil.BuildCostBreakdown(opt, null));
        }

        [EmpireTest("EventCost")]
        public static void BuildCostBreakdown_UnscaledSingleLine_ReturnsNull()
        {
            // affected = 1 and multiplier = 1 -> only the base line, so the tooltip is suppressed.
            var opt = new FCOptionDef { silverCost = 100 };
            var evt = new FCEvent();
            WithEventCostMultiplier(1f, () =>
                TestAssert.IsNull(FCOptionCostUtil.BuildCostBreakdown(opt, evt)));
        }
    }
}
