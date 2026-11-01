using RimWorld;
using System.Linq;

namespace FactionColonies
{
    public static class LazyCacheTests
    {
        private static WorldSettlementFC GetFirstSettlement()
        {
            var settlements = FindFC.Settlements;
            if (settlements == null || settlements.Count == 0)
                return null;
            return settlements.First();
        }

        private static FactionFC GetFaction()
        {
            return FindFC.FactionComp;
        }

        // ===== WorldSettlementFC =====

        [EmpireTest("LazyCache")]
        public static void Settlement_DirtyStats_RecomputesWorkersMax()
        {
            var s = GetFirstSettlement();
            if (s == null) TestAssert.Skip("No settlements");

            double first = s.workersMax;
            s.DirtyStatsCache();
            double second = s.workersMax;

            TestAssert.IsFalse(double.IsNaN(first), "workersMax should not be NaN before dirty");
            TestAssert.IsFalse(double.IsNaN(second), "workersMax should not be NaN after dirty+recompute");
            TestAssert.AreEqual(first, second, tolerance: 0.01, message: "workersMax should be the same before and after dirtying the cache");
        }

        [EmpireTest("LazyCache")]
        public static void Settlement_DirtyProfit_RecomputesIncome()
        {
            var s = GetFirstSettlement();
            if (s == null) TestAssert.Skip("No settlements");

            double first = s.totalIncome;
            s.DirtyProfitCache();
            double second = s.totalIncome;

            TestAssert.IsFalse(double.IsNaN(first), "totalIncome should not be NaN before dirty");
            TestAssert.IsFalse(double.IsNaN(second), "totalIncome should not be NaN after dirty+recompute");
            TestAssert.AreEqual(first, second, tolerance: 0.01, message: "totalIncome should be the same before and after dirtying the cache");
        }

        [EmpireTest("LazyCache")]
        public static void Settlement_DirtyStats_CascadesToProfit()
        {
            var s = GetFirstSettlement();
            if (s == null) TestAssert.Skip("No settlements");

            double first = s.totalProfit;
            s.DirtyStatsCache();
            double second = s.totalProfit;

            TestAssert.IsFalse(double.IsNaN(first), "totalProfit should not be NaN before dirty");
            TestAssert.IsFalse(double.IsNaN(second), "totalProfit should not be NaN after stats dirty cascade");
            TestAssert.AreEqual(first, second, tolerance: 0.01, message: "totalProfit should be the same before and after dirtying the cache");
        }

        [EmpireTest("LazyCache")]
        public static void Settlement_DirtyDescription_Recomputes()
        {
            var s = GetFirstSettlement();
            if (s == null) TestAssert.Skip("No settlements");

            string first = s.description;
            s.DirtyDescriptionCache();
            string second = s.description;

            TestAssert.IsNotNull(first, "description should not be null before dirty");
            TestAssert.IsNotNull(second, "description should not be null after dirty+recompute");
            TestAssert.IsTrue(first.Length > 0, "description should not be empty before dirty");
            TestAssert.IsTrue(second.Length > 0, "description should not be empty after dirty+recompute");
        }

        [EmpireTest("LazyCache")]
        public static void Settlement_ProfitEqualsIncomeMinusUpkeep()
        {
            var s = GetFirstSettlement();
            if (s == null) TestAssert.Skip("No settlements");

            double expected = s.totalIncome - s.totalUpkeep;
            TestAssert.AreEqual(expected, s.totalProfit, tolerance: 0.01,
                message: $"totalProfit ({s.totalProfit}) should equal totalIncome - totalUpkeep ({expected})");
        }

        [EmpireTest("LazyCache")]
        public static void Settlement_Workers_NotExceedUltraMax()
        {
            var s = GetFirstSettlement();
            if (s == null) TestAssert.Skip("No settlements");

            TestAssert.LessThanOrEqual(s.workers, s.workersUltraMax, $"workers ({s.workers}) should not exceed workersUltraMax ({s.workersUltraMax})");
        }

        [EmpireTest("LazyCache")]
        public static void Settlement_WorkersMax_LessOrEqualUltraMax()
        {
            var s = GetFirstSettlement();
            if (s == null) TestAssert.Skip("No settlements");

            TestAssert.LessThanOrEqual(s.workersMax, s.workersUltraMax, $"workersMax ({s.workersMax}) should not exceed workersUltraMax ({s.workersUltraMax})");
        }

        // ===== FactionFC =====

        [EmpireTest("LazyCache")]
        public static void Faction_DirtyProfit_RecomputesIncome()
        {
            var fc = GetFaction();
            if (fc == null || fc.settlements.Count == 0) TestAssert.Skip("No faction/settlements");

            double first = fc.income;
            fc.DirtyFactionProfitCache();
            double second = fc.income;

            TestAssert.IsFalse(double.IsNaN(first), "income should not be NaN before dirty");
            TestAssert.IsFalse(double.IsNaN(second), "income should not be NaN after dirty+recompute");
            TestAssert.AreEqual(first, second, tolerance: 0.01, message: "income should be the same before and after dirtying the cache");
        }

        [EmpireTest("LazyCache")]
        public static void Faction_DirtyAverages_RecomputesHappiness()
        {
            var fc = GetFaction();
            if (fc == null || fc.settlements.Count == 0) TestAssert.Skip("No faction/settlements");

            double first = fc.averageHappiness;
            fc.DirtyAveragesCache();
            double second = fc.averageHappiness;

            TestAssert.IsFalse(double.IsNaN(first), "averageHappiness should not be NaN before dirty");
            TestAssert.IsFalse(double.IsNaN(second), "averageHappiness should not be NaN after dirty+recompute");
            TestAssert.AreEqual(first, second, tolerance: 0.01, message: "averageHappiness should be the same before and after dirtying the cache");
        }

        [EmpireTest("LazyCache")]
        public static void Faction_ProfitEqualsIncomeMinusUpkeep()
        {
            var fc = GetFaction();
            if (fc == null || fc.settlements.Count == 0) TestAssert.Skip("No faction/settlements");

            double expected = fc.income - fc.upkeep;
            TestAssert.AreEqual(expected, fc.profit, tolerance: 0.01, message: $"profit ({fc.profit}) should equal income - upkeep ({expected})");
        }

        [EmpireTest("LazyCache")]
        public static void Faction_IncomeMatchesSettlementSum()
        {
            var fc = GetFaction();
            if (fc == null || fc.settlements.Count == 0) TestAssert.Skip("No faction/settlements");

            double sum = fc.settlements.Sum(s => s.totalIncome);
            TestAssert.AreEqual(sum, fc.income, tolerance: 1.0, message: $"faction income ({fc.income}) should match settlement sum ({sum})");
        }

        [EmpireTest("LazyCache")]
        public static void Faction_UpkeepMatchesSettlementSum()
        {
            var fc = GetFaction();
            if (fc == null || fc.settlements.Count == 0) TestAssert.Skip("No faction/settlements");

            double sum = fc.settlements.Sum(s => s.totalUpkeep);
            TestAssert.AreEqual(sum, fc.upkeep, tolerance: 1.0, message: $"faction upkeep ({fc.upkeep}) should match settlement sum ({sum})");
        }

        [EmpireTest("LazyCache")]
        public static void Faction_AveragesMatchSettlementMeans()
        {
            var fc = GetFaction();
            if (fc == null || fc.settlements.Count == 0) TestAssert.Skip("No faction/settlements");

            int count = fc.settlements.Count;
            double sumH = 0, sumL = 0, sumU = 0, sumP = 0;
            foreach (var s in fc.settlements)
            {
                sumH += s.happiness;
                sumL += s.loyalty;
                sumU += s.unrest;
                sumP += s.prosperity;
            }

            TestAssert.AreEqual((sumH / count), fc.averageHappiness, tolerance: 1.0,
                message: $"averageHappiness ({fc.averageHappiness}) should match manual mean ({sumH / count})");
            TestAssert.AreEqual((sumL / count), fc.averageLoyalty, tolerance: 1.0,
                message: $"averageLoyalty ({fc.averageLoyalty}) should match manual mean ({sumL / count})");
            TestAssert.AreEqual((sumU / count), fc.averageUnrest, tolerance: 1.0,
                message: $"averageUnrest ({fc.averageUnrest}) should match manual mean ({sumU / count})");
            TestAssert.AreEqual((sumP / count), fc.averageProsperity, tolerance: 1.0,
                message: $"averageProsperity ({fc.averageProsperity}) should match manual mean ({sumP / count})");
        }

        [EmpireTest("LazyCache")]
        public static void Faction_TechLevel_IsNotUndefined()
        {
            var fc = GetFaction();
            if (fc == null) TestAssert.Skip("No faction");

            TestAssert.IsTrue(fc.techLevel != TechLevel.Undefined, $"techLevel should not be Undefined, got {fc.techLevel}");
        }

        [EmpireTest("LazyCache")]
        public static void Faction_InvalidateAllSettlementStatCaches_RecomputesCorrectly()
        {
            var fc = GetFaction();
            if (fc == null || fc.settlements.Count == 0) TestAssert.Skip("No faction/settlements");

            FCStatDef stat = FCStatDefOf.happinessGainedBase;
            double[] before = new double[fc.settlements.Count];
            for (int i = 0; i < fc.settlements.Count; i++)
                before[i] = fc.settlements[i].GetSettlementStatValue(stat);

            fc.InvalidateAllSettlementStatCaches();

            for (int i = 0; i < fc.settlements.Count; i++)
            {
                double after = fc.settlements[i].GetSettlementStatValue(stat);
                TestAssert.AreEqual(before[i], after, tolerance: 0.01,
                    message: $"Settlement {fc.settlements[i].Name}: stat should be the same after InvalidateAllSettlementStatCaches");
            }
        }

        // ===== Cascade =====

        [EmpireTest("LazyCache")]
        public static void Settlement_DirtyProfit_CascadesToFaction()
        {
            var fc = GetFaction();
            if (fc == null || fc.settlements.Count == 0) TestAssert.Skip("No faction/settlements");
            var s = fc.settlements.First();

            double first = fc.income;
            s.DirtyProfitCache();
            double second = fc.income;

            TestAssert.IsFalse(double.IsNaN(first), "faction income should not be NaN before cascade");
            TestAssert.IsFalse(double.IsNaN(second), "faction income should not be NaN after settlement dirty cascade");
            TestAssert.AreEqual(first, second, tolerance: 0.01, message: "faction income should be the same before and after dirtying the cache");
        }
    }
}
