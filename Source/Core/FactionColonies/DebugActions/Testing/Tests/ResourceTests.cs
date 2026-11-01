namespace FactionColonies
{
    public static class ResourceTests
    {
        // --- CalculateProductionBase ---

        [EmpireTest("Resource")]
        public static void ProductionBase_EmptyList_ReturnsZero()
        {
            TestAssert.AreEqual(0.0, ResourceFormulas.CalculateProductionBase(new double[] { }));
        }

        [EmpireTest("Resource")]
        public static void ProductionBase_SingleValue_ReturnsThatValue()
        {
            TestAssert.AreEqual(5.0, ResourceFormulas.CalculateProductionBase(new[] { 5.0 }));
        }

        [EmpireTest("Resource")]
        public static void ProductionBase_MultipleValues_ReturnsSum()
        {
            TestAssert.AreEqual(10.0, ResourceFormulas.CalculateProductionBase(new[] { 3.0, 2.0, 5.0 }));
        }

        // --- CalculateProductionMult ---

        [EmpireTest("Resource")]
        public static void ProductionMult_EmptyList_TaxBonusOnly()
        {
            TestAssert.AreEqual(1.0, ResourceFormulas.CalculateProductionMult(new double[] { }, 1.0));
        }

        [EmpireTest("Resource")]
        public static void ProductionMult_SingleMultiplier_ReturnsThatValue()
        {
            TestAssert.AreEqual(1.5, ResourceFormulas.CalculateProductionMult(new[] { 1.5 }, 1.0));
        }

        [EmpireTest("Resource")]
        public static void ProductionMult_MultipleMultipliers_ReturnsProduct()
        {
            TestAssert.AreEqual(3.0, ResourceFormulas.CalculateProductionMult(new[] { 1.5, 2.0 }, 1.0));
        }

        [EmpireTest("Resource")]
        public static void ProductionMult_TaxBonusApplied()
        {
            TestAssert.AreEqual(1.65, ResourceFormulas.CalculateProductionMult(new[] { 1.5 }, 1.1));
        }

        // --- CalculateProduction ---

        [EmpireTest("Resource")]
        public static void Production_BaseTimesMult()
        {
            TestAssert.AreEqual(15.0, ResourceFormulas.CalculateProduction(5.0, 3.0));
        }

        // --- CalculateRawTotalProduction ---

        [EmpireTest("Resource")]
        public static void RawTotalProduction_ProductionTimesWorkers()
        {
            TestAssert.AreEqual(30.0, ResourceFormulas.CalculateRawTotalProduction(10.0, 3));
        }

        [EmpireTest("Resource")]
        public static void RawTotalProduction_ZeroWorkers_ReturnsZero()
        {
            TestAssert.AreEqual(0.0, ResourceFormulas.CalculateRawTotalProduction(10.0, 0));
        }

        // --- CalculateMarketValue ---

        [EmpireTest("Resource")]
        public static void MarketValue_RawTimesSilverPerResource()
        {
            TestAssert.AreEqual(150.0, ResourceFormulas.CalculateMarketValue(30.0, 5.0));
        }

        // --- MaxThingCanAfford ---

        [EmpireTest("Resource")]
        public static void MaxCanAfford_ExactDivision()
        {
            TestAssert.AreEqual(10, ResourceFormulas.MaxThingCanAfford(1000.0, 100f));
        }

        [EmpireTest("Resource")]
        public static void MaxCanAfford_Truncates()
        {
            TestAssert.AreEqual(1, ResourceFormulas.MaxThingCanAfford(150.0, 100f));
        }

        [EmpireTest("Resource")]
        public static void MaxCanAfford_InsufficientBudget_ReturnsZero()
        {
            TestAssert.AreEqual(0, ResourceFormulas.MaxThingCanAfford(50.0, 100f));
        }

        // --- CanAffordThingAmount ---

        [EmpireTest("Resource")]
        public static void CanAfford_ExactlyAtBudget_ReturnsTrue()
        {
            TestAssert.IsTrue(ResourceFormulas.CanAffordThingAmount(100f, 100.0));
        }

        [EmpireTest("Resource")]
        public static void CanAfford_OverBudget_ReturnsFalse()
        {
            TestAssert.IsFalse(ResourceFormulas.CanAffordThingAmount(101f, 100.0));
        }

        [EmpireTest("Resource")]
        public static void CanAfford_UnderBudget_ReturnsTrue()
        {
            TestAssert.IsTrue(ResourceFormulas.CanAffordThingAmount(50f, 100.0));
        }
    }
}
