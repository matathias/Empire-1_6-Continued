using FactionColonies.util;
using System.Linq;

namespace FactionColonies
{
    public static class FactionTests
    {
        // --- CalculateFactionLevelGoalXP ---

        [EmpireTest("Faction")]
        public static void FactionXP_Level0_Returns100()
        {
            TestAssert.AreEqual(100f, SettlementFormulas.CalculateFactionLevelGoalXP(0), 0.01f);
        }

        [EmpireTest("Faction")]
        public static void FactionXP_Level1_Returns250()
        {
            TestAssert.AreEqual(250f, SettlementFormulas.CalculateFactionLevelGoalXP(1), 0.01f);
        }

        [EmpireTest("Faction")]
        public static void FactionXP_Level5_Returns850()
        {
            TestAssert.AreEqual(850f, SettlementFormulas.CalculateFactionLevelGoalXP(5), 0.01f);
        }

        [EmpireTest("Faction")]
        public static void FactionXP_Level10_Returns1600()
        {
            TestAssert.AreEqual(1600f, SettlementFormulas.CalculateFactionLevelGoalXP(10), 0.01f);
        }

        // --- Integration tests (require game state) ---

        private static FactionFC GetFaction()
        {
            return FindFC.FactionComp;
        }

        [EmpireTest("Faction")]
        public static void Faction_TotalProfit_MatchesSettlementSum()
        {
            var faction = GetFaction();
            if (faction == null || faction.settlements.Count == 0) TestAssert.Skip("No faction/settlements");

            double sumIncome = faction.settlements.Sum(s => s.GetTotalIncome());
            double sumUpkeep = faction.settlements.Sum(s => s.GetTotalUpkeep());
            int edictUpkeep = FindFC.PolicyManager.GetEdictUpkeep();
            double expectedProfit = sumIncome - sumUpkeep - edictUpkeep;

            TestAssert.AreEqual(expectedProfit, faction.profit, tolerance: 1.0,
                message: $"Faction profit ({faction.profit}) should match sum of settlement profits minus edict upkeep ({expectedProfit})");
        }

        [EmpireTest("Faction")]
        public static void Faction_SettlementTitheIncome_IsNonNegative()
        {
            var faction = GetFaction();
            if (faction == null || faction.settlements.Count == 0) TestAssert.Skip("No faction/settlements");

            foreach (var settlement in faction.settlements)
            {
                foreach (var resource in settlement.Resources)
                {
                    double tithe = resource.GetTitheIncome();
                    TestAssert.IsTrue(tithe >= 0,
                        $"Tithe income for {resource.def?.defName ?? "null"} in {settlement.Name} should be >= 0, got {tithe}");
                }
            }
        }

        [EmpireTest("Faction")]
        public static void Faction_ResourceProduction_IsFinite()
        {
            var faction = GetFaction();
            if (faction == null || faction.settlements.Count == 0) TestAssert.Skip("No faction/settlements");

            foreach (var settlement in faction.settlements)
            {
                foreach (var resource in settlement.Resources)
                {
                    double prod = resource.production;
                    TestAssert.IsFalse(double.IsNaN(prod),
                        $"Production for {resource.def?.defName ?? "null"} in {settlement.Name} should not be NaN");
                    TestAssert.IsFalse(double.IsInfinity(prod),
                        $"Production for {resource.def?.defName ?? "null"} in {settlement.Name} should not be infinite");
                }
            }
        }
    }
}
