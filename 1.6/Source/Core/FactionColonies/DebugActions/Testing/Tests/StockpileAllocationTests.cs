namespace FactionColonies
{
    public static class StockpileAllocationTests
    {
        // Creates a ResourceFC with known production, without requiring game state.
        // productionBase = productionPerWorker (one additive, no multipliers → mult=1)
        // rawTotalProduction = productionPerWorker * workers
        private static ResourceFC MakeResource(double productionPerWorker, int workers)
        {
            var res = new ResourceFC();
            // A resolved def is required: AccumulateDailyProduction reads `canTithe` (=> def.canTithe),
            // so a null def NREs. Use a fresh in-memory, non-pool, non-tithing def so the accumulate
            // path stays pure (canTithe == false skips the tithe block's faction/settlement derefs) and
            // no game state is needed — matching this suite's "without requiring game state" design.
            res.def = new ResourceTypeDef { defName = "TestResource", isPoolResource = false, canTithe = false };
            // A non-empty desc is required to avoid a null-settlement warning branch in AddProductionAdditive
            res.AddProductionAdditive("test.production", productionPerWorker, "test bonus");
            res.assignedWorkers = workers;
            return res;
        }

        // --- Zero-allocation regression ---
        // Verify the property chain is identical to the pre-stockpile state when no
        // allocations are registered (empty dictionary must behave like the old literal 0).

        [EmpireTest("StockpileAllocation")]
        public static void NoAllocations_TotalStockpileAllocation_IsZero()
        {
            var res = MakeResource(10.0, 3);
            TestAssert.AreEqual(0.0, res.totalStockpileAllocation);
        }

        [EmpireTest("StockpileAllocation")]
        public static void NoAllocations_EffectiveRawEquals_RawTotalProduction()
        {
            var res = MakeResource(10.0, 3);
            TestAssert.AreEqual(res.rawTotalProduction, res.effectiveRawTotalProduction);
        }

        [EmpireTest("StockpileAllocation")]
        public static void NoAllocations_TaxableMarketValue_EqualsEffectiveTimesRate()
        {
            var res = MakeResource(10.0, 3);
            double expected = res.effectiveRawTotalProduction * FCSettings.silverPerResource;
            TestAssert.AreEqual(expected, res.taxableProductionMarketValue);
        }

        // --- SetStockpileAllocation: acceptance/rejection ---

        [EmpireTest("StockpileAllocation")]
        public static void SetAllocation_WithinCapacity_ReturnsTrue()
        {
            var res = MakeResource(10.0, 3); // rawTotalProduction = 30
            TestAssert.IsTrue(res.SetStockpileAllocation("mod.a", 20.0));
        }

        [EmpireTest("StockpileAllocation")]
        public static void SetAllocation_ExactlyAtCapacity_ReturnsTrue()
        {
            var res = MakeResource(10.0, 3); // rawTotalProduction = 30
            TestAssert.IsTrue(res.SetStockpileAllocation("mod.a", 30.0));
        }

        [EmpireTest("StockpileAllocation")]
        public static void SetAllocation_ExceedsCapacity_ReturnsFalse()
        {
            var res = MakeResource(10.0, 3); // rawTotalProduction = 30
            TestAssert.IsFalse(res.SetStockpileAllocation("mod.a", 31.0));
        }

        [EmpireTest("StockpileAllocation")]
        public static void SetAllocation_ExceedsCapacity_NotRegistered()
        {
            var res = MakeResource(10.0, 3);
            res.SetStockpileAllocation("mod.a", 31.0); // rejected
            TestAssert.AreEqual(0.0, res.totalStockpileAllocation);
        }

        // --- Multi-mod composition ---

        [EmpireTest("StockpileAllocation")]
        public static void MultipleAllocations_SumCorrectly()
        {
            var res = MakeResource(10.0, 5); // rawTotalProduction = 50
            res.SetStockpileAllocation("mod.a", 10.0);
            res.SetStockpileAllocation("mod.b", 15.0);
            TestAssert.AreEqual(25.0, res.totalStockpileAllocation);
        }

        [EmpireTest("StockpileAllocation")]
        public static void SecondAllocation_ExceedsCombinedCapacity_Rejected()
        {
            var res = MakeResource(10.0, 5); // rawTotalProduction = 50
            res.SetStockpileAllocation("mod.a", 40.0);
            TestAssert.IsFalse(res.SetStockpileAllocation("mod.b", 15.0));
        }

        [EmpireTest("StockpileAllocation")]
        public static void UpdateExistingKey_TreatsOldAmountAsReplaced()
        {
            var res = MakeResource(10.0, 3); // rawTotalProduction = 30
            res.SetStockpileAllocation("mod.a", 20.0);
            // Re-register the same key with a smaller amount: the old amount should not
            // count twice in the capacity check, so this should succeed.
            TestAssert.IsTrue(res.SetStockpileAllocation("mod.a", 10.0));
            TestAssert.AreEqual(10.0, res.totalStockpileAllocation);
        }

        // --- Property chain with an active allocation ---

        [EmpireTest("StockpileAllocation")]
        public static void WithAllocation_EffectiveRaw_ReducedByAllocation()
        {
            var res = MakeResource(10.0, 3); // rawTotalProduction = 30
            res.SetStockpileAllocation("mod.a", 12.0);
            TestAssert.AreEqual(18.0, res.effectiveRawTotalProduction);
        }

        // --- ClearStockpileAllocation ---

        [EmpireTest("StockpileAllocation")]
        public static void Clear_RemovesAllocation()
        {
            var res = MakeResource(10.0, 3);
            res.SetStockpileAllocation("mod.a", 10.0);
            res.ClearStockpileAllocation("mod.a");
            TestAssert.AreEqual(0.0, res.totalStockpileAllocation);
        }

        [EmpireTest("StockpileAllocation")]
        public static void Clear_DoesNotInvokeCallback()
        {
            var res = MakeResource(10.0, 3);
            bool callbackFired = false;
            res.SetStockpileAllocation("mod.a", 10.0, (req, act) => callbackFired = true);
            res.ClearStockpileAllocation("mod.a");
            TestAssert.IsFalse(callbackFired, "Callback should not fire on voluntary clear");
        }

        // --- realize callback via AccumulateDailyProduction ---

        [EmpireTest("StockpileAllocation")]
        public static void Realize_CallbackInvokedOnAccumulate()
        {
            var res = MakeResource(10.0, 3); // rawTotalProduction = 30
            bool callbackFired = false;
            res.SetStockpileAllocation("mod.a", 10.0, (req, act) => callbackFired = true);
            res.AccumulateDailyProduction();
            TestAssert.IsTrue(callbackFired, "Realize callback should fire during AccumulateDailyProduction");
        }

        [EmpireTest("StockpileAllocation")]
        public static void Realize_ActualClampedToAvailable()
        {
            // Production = 10, allocation = 20 (over capacity). Actual should be clamped to 10.
            var res = MakeResource(10.0, 1); // rawTotalProduction = 10
            double actualReceived = -1;
            res.SetStockpileAllocation("mod.a", 20.0, (req, act) => actualReceived = act);
            res.AccumulateDailyProduction();
            TestAssert.AreEqual(10.0, actualReceived, "Actual diversion should be clamped to available production");
        }

        [EmpireTest("StockpileAllocation")]
        public static void Realize_NeverNegative()
        {
            // Two allocations totaling 30; production = 20. First gets 20, second gets 0.
            var res = MakeResource(10.0, 2); // rawTotalProduction = 20
            double secondActual = -99;
            res.SetStockpileAllocation("mod.a", 20.0);
            res.SetStockpileAllocation("mod.b", 10.0, (req, act) => secondActual = act);
            res.AccumulateDailyProduction();
            TestAssert.AreEqual(0.0, secondActual, "Second diversion should be 0 when first exhausts production");
        }
    }
}
