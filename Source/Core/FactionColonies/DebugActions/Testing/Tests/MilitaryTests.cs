namespace FactionColonies
{
    public static class MilitaryTests
    {

        // --- CalculateAccuracyCostPercentage ---

        [EmpireTest("Military")]
        public static void AccuracyCost_MaxAccuracy_ZeroSurcharge()
        {
            TestAssert.AreEqual(0f, MilitaryFireSupport.CalculateAccuracyCostPercentage(15f), 0.01f);
        }

        [EmpireTest("Military")]
        public static void AccuracyCost_ZeroAccuracy_FullSurcharge()
        {
            TestAssert.AreEqual(100f, MilitaryFireSupport.CalculateAccuracyCostPercentage(0f), 0.01f);
        }

        [EmpireTest("Military")]
        public static void AccuracyCost_Accuracy10_33Percent()
        {
            TestAssert.AreEqual(33f, MilitaryFireSupport.CalculateAccuracyCostPercentage(10f), 0.01f);
        }

        [EmpireTest("Military")]
        public static void AccuracyCost_Accuracy75_50Percent()
        {
            TestAssert.AreEqual(50f, MilitaryFireSupport.CalculateAccuracyCostPercentage(7.5f), 0.01f);
        }

        // --- CalculateTotalCost ---

        [EmpireTest("Military")]
        public static void TotalCost_SingleProjectile_PerfectAccuracy()
        {
            // marketValue * 1.5 * (1 + 0/100) = 100 * 1.5 = 150
            float cost = MilitaryFireSupport.CalculateTotalCost(15f, new[] { 100f });
            TestAssert.AreEqual(150f, cost, 0.01f);
        }

        [EmpireTest("Military")]
        public static void TotalCost_MultipleProjectiles_ImperfectAccuracy()
        {
            // accuracy=10 → surcharge=33%
            // each: 100 * 1.5 * 1.33 = 199.5, two = 399, rounded = 399
            float cost = MilitaryFireSupport.CalculateTotalCost(10f, new[] { 100f, 100f });
            TestAssert.AreEqual(399f, cost, 0.01f);
        }

        [EmpireTest("Military")]
        public static void TotalCost_EmptyList_ReturnsZero()
        {
            float cost = MilitaryFireSupport.CalculateTotalCost(15f, new float[] { });
            TestAssert.AreEqual(0f, cost, 0.01f);
        }

        // --- SquadHealingEstimator ---
        // Narrow surface: most of the formula reads real Pawn state (HealthScale, stats).
        // These tests exercise only the null/empty defensive paths.

        [EmpireTest("Military")]
        public static void Healing_TicksToFullHealth_NullPawn_ReturnsZero()
        {
            TestAssert.AreEqual(0, SquadHealingEstimator.TicksToFullHealth(null));
        }

        [EmpireTest("Military")]
        public static void Healing_TicksToFullEffectiveness_NullSquad_ReturnsZero()
        {
            TestAssert.AreEqual(0, SquadHealingEstimator.TicksToFullEffectiveness(null));
        }

        [EmpireTest("Military")]
        public static void Healing_TicksToFullEffectiveness_EmptySquad_ReturnsZero()
        {
            // Squad with mercenaries=null. The estimator iterates safely without throwing.
            var squad = new MercenarySquadFC();
            squad.mercenaries = null;
            TestAssert.AreEqual(0, SquadHealingEstimator.TicksToFullEffectiveness(squad));
        }

        [EmpireTest("Military")]
        public static void Healing_TicksToFullEffectiveness_SquadWithNoLivePawns_ReturnsZero()
        {
            // Squad with empty-slot mercenaries (all pawn=null). TicksToFullHealth(null) == 0,
            // so the squad-level worst-case stays at 0.
            var squad = new MercenarySquadFC();
            squad.mercenaries = new System.Collections.Generic.List<Mercenary>
            {
                new Mercenary(),
                new Mercenary()
            };
            TestAssert.AreEqual(0, SquadHealingEstimator.TicksToFullEffectiveness(squad));
        }
    }
}
