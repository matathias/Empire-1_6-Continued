using System;
using System.Collections.Generic;

namespace FactionColonies
{
    /* Coverage for CompareUtil's comparators. The def comparator is pure; the settlement
     * comparators need live settlements, so they are validated by sorting a copy of the live
     * settlement list and asserting the result is monotonic in the comparator's intended
     * direction (skipped when fewer than two settlements exist). */
    public static class CompareUtilTests
    {
        // --- Pure: BuildingFCDef by label ---

        [EmpireTest("Util")]
        public static void CompareBuildingDef_OrdersByLabel()
        {
            var a = new BuildingFCDef { label = "alpha" };
            var b = new BuildingFCDef { label = "beta" };
            TestAssert.LessThan(CompareUtil.CompareBuildingDef(a, b), 0, "alpha should sort before beta");
            TestAssert.GreaterThan(CompareUtil.CompareBuildingDef(b, a), 0, "beta should sort after alpha");
            TestAssert.AreEqual(0, CompareUtil.CompareBuildingDef(a, a), "equal labels compare equal");
        }

        // --- Live settlements: sort-direction contract ---

        [EmpireTest("Util")]
        public static void CompareSettlementLevel_SortsDescending()
        {
            AssertSortedDescending(CompareUtil.CompareSettlementLevel, s => s.settlementLevel, "Level");
        }

        [EmpireTest("Util")]
        public static void CompareSettlementLoyalty_SortsDescending()
        {
            AssertSortedDescending(CompareUtil.CompareSettlementLoyalty, s => s.loyalty, "Loyalty");
        }

        [EmpireTest("Util")]
        public static void CompareSettlementHappiness_SortsDescending()
        {
            AssertSortedDescending(CompareUtil.CompareSettlementHappiness, s => s.happiness, "Happiness");
        }

        [EmpireTest("Util")]
        public static void CompareSettlementProsperity_SortsDescending()
        {
            AssertSortedDescending(CompareUtil.CompareSettlementProsperity, s => s.prosperity, "Prosperity");
        }

        [EmpireTest("Util")]
        public static void CompareSettlementUnrest_SortsAscending()
        {
            AssertSortedAscending(CompareUtil.CompareSettlementUnrest, s => s.unrest, "Unrest");
        }

        // --- Helpers ---

        private static void AssertSortedDescending(Comparison<WorldSettlementFC> cmp,
            Func<WorldSettlementFC, double> key, string name)
        {
            var list = GetSettlementsOrSkip();
            list.Sort(cmp);
            for (int i = 0; i + 1 < list.Count; i++)
                TestAssert.IsTrue(key(list[i]) >= key(list[i + 1]),
                    $"{name}: expected descending order at index {i} ({key(list[i])} vs {key(list[i + 1])})");
        }

        private static void AssertSortedAscending(Comparison<WorldSettlementFC> cmp,
            Func<WorldSettlementFC, double> key, string name)
        {
            var list = GetSettlementsOrSkip();
            list.Sort(cmp);
            for (int i = 0; i + 1 < list.Count; i++)
                TestAssert.IsTrue(key(list[i]) <= key(list[i + 1]),
                    $"{name}: expected ascending order at index {i} ({key(list[i])} vs {key(list[i + 1])})");
        }

        private static List<WorldSettlementFC> GetSettlementsOrSkip()
        {
            var settlements = FindFC.Settlements;
            if (settlements == null || settlements.Count < 2) TestAssert.Skip("Need >= 2 settlements");
            return new List<WorldSettlementFC>(settlements);
        }
    }
}
