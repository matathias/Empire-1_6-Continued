using System;
using System.Collections.Generic;
using System.Linq;

namespace FactionColonies
{
    public static class TitheIncomeTests
    {
        // ============================
        // Helpers
        // ============================

        private static WorldSettlementFC GetFirstSettlement()
        {
            var settlements = FindFC.Settlements;
            if (settlements == null || settlements.Count == 0)
                return null;
            return settlements[0];
        }

        private static ResourceFC GetFirstNonPoolResource(WorldSettlementFC settlement)
        {
            return settlement.Resources.FirstOrDefault(r => r.canTithe);
        }

        private static void WithFactionModifier(FCStatDef stat, double value, Action action)
        {
            FactionFC faction = FindFC.FactionComp;
            var settlement = faction.settlements[0];
            var mods = new List<FCStatModifier> { new FCStatModifier { stat = stat, value = value } };
            settlement.AddStatModifiers(mods, "titheTest");
            try
            {
                action();
            }
            finally
            {
                settlement.RemoveStatModifiersBySource("titheTest");
            }
        }

        // ============================
        // Tests
        // ============================

        [EmpireTest("TitheIncome")]
        public static void TitheModifierPerWorker_MatchesStatPlusSetting()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlement");

            ResourceFC resource = GetFirstNonPoolResource(settlement);
            if (resource == null) TestAssert.Skip("No non-pool resource");

            double expected = settlement.GetStatValue(FCStatDefOf.taxBaseRandomModifier)
                            + FCSettings.productionTitheMod;
            double actual = resource.GetTitheModifierPerWorker();

            TestAssert.AreEqual(expected, actual,
                message: "GetTitheModifierPerWorker should equal taxBaseRandomModifier stat + productionTitheMod setting");
        }

        [EmpireTest("TitheIncome")]
        public static void TitheIncome_ZeroWorkers_EqualsBaseTimesMultiplier()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlement");

            ResourceFC resource = GetFirstNonPoolResource(settlement);
            if (resource == null) TestAssert.Skip("No non-pool resource");

            int savedWorkers = resource.assignedWorkers;
            try
            {
                resource.assignedWorkers = 0;
                resource.SetDirtyCache();

                double multForTotal = FindFC.FactionComp.GetStatValue(FCStatDefOf.titheValueMultiplier, settlement);
                double expected = resource.taxableProductionMarketValue * multForTotal + resource.DailyExternalTitheBudget;
                double actual = resource.GetTitheIncome();

                TestAssert.AreEqual(expected, actual,
                    message: "With 0 workers, tithe income should equal taxableProductionMarketValue * titheValueMultiplier + DailyExternalTitheBudget");
            }
            finally
            {
                resource.assignedWorkers = savedWorkers;
                resource.SetDirtyCache();
            }
        }

        [EmpireTest("TitheIncome")]
        public static void TitheIncome_FormulaConsistency()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlement");

            ResourceFC resource = GetFirstNonPoolResource(settlement);
            if (resource == null) TestAssert.Skip("No non-pool resource");

            // Manually compute using the same formula that GetTitheIncome should use
            double workerMod = resource.GetTitheModifierPerWorker() * resource.assignedWorkers;
            double multForTotal = FindFC.FactionComp.GetStatValue(FCStatDefOf.titheValueMultiplier, settlement);
            double expected = (resource.taxableProductionMarketValue + workerMod) * multForTotal + resource.DailyExternalTitheBudget;
            double actual = resource.GetTitheIncome();

            TestAssert.AreEqual(expected, actual,
                message: "GetTitheIncome should equal (taxableMarketValue + workerMod) * titheValueMultiplier + DailyExternalTitheBudget");
        }

        [EmpireTest("TitheIncome")]
        public static void TitheIncome_MultForTotal_ChangesWithStat()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlement");

            ResourceFC resource = GetFirstNonPoolResource(settlement);
            if (resource == null) TestAssert.Skip("No non-pool resource");
            if (resource.assignedWorkers == 0) TestAssert.Skip("Resource has 0 workers");

            double incomeBefore = resource.GetTitheIncome();

            // titheValueMultiplier is multiplicative, so adding 1.5 means multiplying by 1.5
            WithFactionModifier(FCStatDefOf.titheValueMultiplier, 1.5, () =>
            {
                resource.SetDirtyCache();
                double incomeAfter = resource.GetTitheIncome();

                if (incomeBefore != 0)
                {
                    TestAssert.GreaterThan(incomeAfter, incomeBefore,
                        $"Tithe income should increase with higher titheValueMultiplier (before={incomeBefore:F2}, after={incomeAfter:F2})");
                }
            });

            resource.SetDirtyCache();
            double incomeRestored = resource.GetTitheIncome();
            TestAssert.AreEqual(incomeBefore, incomeRestored,
                message: "Tithe income should restore after removing modifier");
        }

        [EmpireTest("TitheIncome")]
        public static void GetTitheValueMultiplier_MatchesStat()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlement");

            ResourceFC resource = GetFirstNonPoolResource(settlement);
            if (resource == null) TestAssert.Skip("No non-pool resource");

            double expected = FindFC.FactionComp.GetStatValue(FCStatDefOf.titheValueMultiplier, settlement);
            double actual = resource.GetTitheValueMultiplier();

            TestAssert.AreEqual(expected, actual,
                message: "GetTitheValueMultiplier should equal the titheValueMultiplier stat for the settlement");
        }

        [EmpireTest("TitheIncome")]
        public static void TitheIncome_PostMultiplierComponents_Reconcile()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlement");

            ResourceFC resource = GetFirstNonPoolResource(settlement);
            if (resource == null) TestAssert.Skip("No non-pool resource");

            // Mirrors how the tithing screen header now displays its three numbers:
            // each shown post-multiplier, summing (with external budget) to the total tithe budget.
            double mult = resource.GetTitheValueMultiplier();
            double prodComponent = resource.taxableProductionMarketValue * mult;
            double workerComponent = resource.GetTotalTitheModifierForWorkers() * mult;
            double expected = prodComponent + workerComponent + resource.DailyExternalTitheBudget;
            double actual = resource.GetTitheIncome();

            TestAssert.AreEqual(expected, actual,
                message: "Post-multiplier production + worker components + external budget should reconcile to GetTitheIncome");
        }

        [EmpireTest("TitheIncome")]
        public static void TitheIncome_AllResources_NonNegative()
        {
            var faction = FindFC.FactionComp;
            if (faction == null || faction.settlements.Count == 0)
                TestAssert.Skip("No faction/settlements");

            foreach (var settlement in faction.settlements)
            {
                foreach (var resource in settlement.Resources)
                {
                    double tithe = resource.GetTitheIncome();
                    TestAssert.IsTrue(tithe >= 0,
                        $"{settlement.Name}/{resource.def?.defName}: tithe income should be >= 0, got {tithe:F2}");
                    TestAssert.IsFalse(double.IsNaN(tithe),
                        $"{settlement.Name}/{resource.def?.defName}: tithe income should not be NaN");
                }
            }
        }

        [EmpireTest("TitheIncome")]
        public static void TitheList_AddAndReorder_PreservesOrder()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlement");
            ResourceFC resource = GetFirstNonPoolResource(settlement);
            if (resource == null) TestAssert.Skip("No non-pool resource");

            int before = resource.Tithes.Count;
            var a = new ThingQualityTuple { thingDef = RimWorld.ThingDefOf.MealSimple };
            var b = new ThingQualityTuple { thingDef = RimWorld.ThingDefOf.Silver };
            try
            {
                resource.AddTitheEntry(a, 1);
                resource.AddTitheEntry(b, 1);
                int idxA = System.Array.FindIndex(System.Linq.Enumerable.ToArray(resource.Tithes), e => e.thing == a);
                int idxB = System.Array.FindIndex(System.Linq.Enumerable.ToArray(resource.Tithes), e => e.thing == b);
                TestAssert.IsTrue(idxA < idxB, "first-added entry should be higher priority (lower index)");

                resource.MoveTitheEntry(idxB, idxA); // move b above a
                int newIdxB = System.Array.FindIndex(System.Linq.Enumerable.ToArray(resource.Tithes), e => e.thing == b);
                TestAssert.AreEqual(idxA, newIdxB, message: "reorder should move b to a's index");
            }
            finally
            {
                resource.RemoveTitheAt(before); // remove the two appended entries (regardless of order)
                resource.RemoveTitheAt(before);
                TestAssert.AreEqual(before, resource.Tithes.Count, message: "cleanup should restore the list");
            }
        }

        [EmpireTest("TitheIncome")]
        public static void TitheList_AllowsDuplicateEntries()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlement");
            ResourceFC resource = GetFirstNonPoolResource(settlement);
            if (resource == null) TestAssert.Skip("No non-pool resource");

            int before = resource.Tithes.Count;
            var a = new ThingQualityTuple { thingDef = RimWorld.ThingDefOf.MealSimple };
            try
            {
                // The same thing may appear multiple times; each AddTitheEntry appends an independent entry.
                resource.AddTitheEntry(a, 2);
                resource.AddTitheEntry(a, 100);
                TestAssert.AreEqual(before + 2, resource.Tithes.Count, message: "both duplicate entries should persist");
                TestAssert.AreEqual(2, resource.Tithes[before].quantity, message: "first duplicate keeps its quantity");
                TestAssert.AreEqual(100, resource.Tithes[before + 1].quantity, message: "second duplicate keeps its quantity");

                // Editing one duplicate by index must not touch the other.
                resource.SetTitheQuantityAt(before, 5);
                TestAssert.AreEqual(5, resource.Tithes[before].quantity, message: "first duplicate updated");
                TestAssert.AreEqual(100, resource.Tithes[before + 1].quantity, message: "second duplicate unchanged");
            }
            finally
            {
                resource.RemoveTitheAt(before);
                resource.RemoveTitheAt(before);
                TestAssert.AreEqual(before, resource.Tithes.Count, message: "cleanup should restore the list");
            }
        }
    }
}
