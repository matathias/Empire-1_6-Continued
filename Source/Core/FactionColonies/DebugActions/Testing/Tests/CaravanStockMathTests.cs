using UnityEngine;

namespace FactionColonies
{
    /// <summary>
    /// Pure-math tests for <see cref="CaravanStockMath"/> — the ware-stack budgeting that keeps
    /// Empire caravans near vanilla line-item counts (and therefore vanilla pack-animal counts).
    /// No game state required. Baseline assumptions mirror the defaults: baseThingCount 30,
    /// BaseExtraScale 3, silver stackLimit 500.
    /// </summary>
    public static class CaravanStockMathTests
    {
        // --- TargetStacks: deterministic scaling, clamped to [0.5x, 4x] ---

        [EmpireTest("Caravan")]
        public static void TargetStacks_AtBaseScale_EqualsBaseline()
        {
            // extraScale == baseExtraScale -> 1x
            TestAssert.AreEqual(30, CaravanStockMath.TargetStacks(30, 3f, 3f));
        }

        [EmpireTest("Caravan")]
        public static void TargetStacks_DoubleScale_DoublesBaseline()
        {
            TestAssert.AreEqual(60, CaravanStockMath.TargetStacks(30, 6f, 3f));
        }

        [EmpireTest("Caravan")]
        public static void TargetStacks_TripleScale_Triples()
        {
            // 9x / 3x base = 3x, within the [0.5x, 4x] band.
            TestAssert.AreEqual(90, CaravanStockMath.TargetStacks(30, 9f, 3f));
        }

        [EmpireTest("Caravan")]
        public static void TargetStacks_HighScale_CappedAt4x()
        {
            // Archotech-tier extraScale (10x base) must not exceed the 4x ceiling.
            TestAssert.AreEqual(120, CaravanStockMath.TargetStacks(30, 30f, 3f));
        }

        [EmpireTest("Caravan")]
        public static void TargetStacks_LowScale_FlooredAtHalf()
        {
            // Below 0.5x clamps to 0.5x -> 15, not lower.
            TestAssert.AreEqual(15, CaravanStockMath.TargetStacks(30, 0.3f, 3f));
        }

        [EmpireTest("Caravan")]
        public static void TargetStacks_HalfScale_HalvesBaseline()
        {
            TestAssert.AreEqual(15, CaravanStockMath.TargetStacks(30, 1.5f, 3f));
        }

        [EmpireTest("Caravan")]
        public static void TargetStacks_NeverBelowOne()
        {
            // Tiny baseline at the 0.5x floor rounds toward 0 but is clamped up to 1.
            TestAssert.AreEqual(1, CaravanStockMath.TargetStacks(1, 0.3f, 3f));
        }

        [EmpireTest("Caravan")]
        public static void TargetStacks_ZeroReferenceScale_DoesNotThrowOrDivideByZero()
        {
            // Defensive guard: a 0 reference must not produce NaN/Infinity or throw.
            int result = CaravanStockMath.TargetStacks(30, 6f, 0f);
            TestAssert.GreaterThan(result, 0, "TargetStacks must stay positive with a 0 reference scale");
        }

        // --- SilverStacks: bounded to at most ~half the target, at least 1 ---

        [EmpireTest("Caravan")]
        public static void SilverStacks_SmallSilver_IsOneStack()
        {
            TestAssert.AreEqual(1, CaravanStockMath.SilverStacks(400, 20, 500));
        }

        [EmpireTest("Caravan")]
        public static void SilverStacks_FitsUnderHalf_UsesActualStacks()
        {
            // 5000 silver / 500 = 10 stacks; half of 20 = 10, so it fits exactly.
            TestAssert.AreEqual(10, CaravanStockMath.SilverStacks(5000, 20, 500));
        }

        [EmpireTest("Caravan")]
        public static void SilverStacks_HugeSilver_CappedAtHalfTarget()
        {
            // 50000 silver would be 100 stacks; capped to half the 20-stack target = 10.
            TestAssert.AreEqual(10, CaravanStockMath.SilverStacks(50000, 20, 500));
        }

        [EmpireTest("Caravan")]
        public static void SilverStacks_TinyTarget_StillAtLeastOne()
        {
            TestAssert.AreEqual(1, CaravanStockMath.SilverStacks(50000, 1, 500));
        }

        [EmpireTest("Caravan")]
        public static void SilverStacks_ZeroStackLimit_DoesNotThrow()
        {
            int result = CaravanStockMath.SilverStacks(5000, 20, 0);
            TestAssert.GreaterThan(result, 0, "SilverStacks must stay positive with a 0 stack limit");
        }

        // --- PerTypeStackCap: item-stack budget spread across candidates, plus +1 headroom ---

        [EmpireTest("Caravan")]
        public static void PerTypeStackCap_EvenSpread_IsBaselinePlusHeadroom()
        {
            // 15 stacks across 15 types -> ceil(1) = 1, +1 headroom = 2
            TestAssert.AreEqual(2, CaravanStockMath.PerTypeStackCap(15, 15));
        }

        [EmpireTest("Caravan")]
        public static void PerTypeStackCap_FewerCandidates_AllowsMorePerType()
        {
            // 15 stacks across 10 types -> ceil(1.5) = 2, +1 headroom = 3
            TestAssert.AreEqual(3, CaravanStockMath.PerTypeStackCap(15, 10));
        }

        [EmpireTest("Caravan")]
        public static void PerTypeStackCap_ManyCandidates_FloorsAtBaselinePlusHeadroom()
        {
            // 15 stacks across 30 types -> ceil(0.5) = 1 (floor), +1 headroom = 2
            TestAssert.AreEqual(2, CaravanStockMath.PerTypeStackCap(15, 30));
        }

        [EmpireTest("Caravan")]
        public static void PerTypeStackCap_ZeroCandidates_DoesNotDivideByZero()
        {
            int result = CaravanStockMath.PerTypeStackCap(15, 0);
            TestAssert.GreaterThan(result, 0, "PerTypeStackCap must stay positive with 0 candidates");
        }

        // --- ClampItemUnits: the core fix — non-stacking goods can't explode ---

        [EmpireTest("Caravan")]
        public static void ClampItemUnits_NonStacking_HeldToPerTypeCap()
        {
            // A cheap weapon (stackLimit 1) the budget could buy 400 of is capped to the per-type
            // stack share. This is the regression guard against the herd-of-pack-animals bug.
            TestAssert.AreEqual(2, CaravanStockMath.ClampItemUnits(400, 2, 1));
        }

        [EmpireTest("Caravan")]
        public static void ClampItemUnits_Stackable_AllowsNearFullStacks()
        {
            // perTypeCap 2 x stackLimit 75 = 150 units max -> 2 line-item stacks, not 4.
            TestAssert.AreEqual(150, CaravanStockMath.ClampItemUnits(300, 2, 75));
        }

        [EmpireTest("Caravan")]
        public static void ClampItemUnits_UnderCap_Unchanged()
        {
            TestAssert.AreEqual(10, CaravanStockMath.ClampItemUnits(10, 2, 75));
        }

        [EmpireTest("Caravan")]
        public static void ClampItemUnits_AtLeastOne()
        {
            TestAssert.AreEqual(1, CaravanStockMath.ClampItemUnits(0, 2, 1));
        }

        [EmpireTest("Caravan")]
        public static void ClampItemUnits_ZeroStackLimit_TreatedAsOne()
        {
            TestAssert.AreEqual(2, CaravanStockMath.ClampItemUnits(400, 2, 0));
        }

        // --- Composition: the defining property — stack count can't grow with the budget ---

        [EmpireTest("Caravan")]
        public static void Composition_StackCountIsBudgetIndependentPastTheCap()
        {
            // The original bug: caravan stack count (and thus pack animals) grew without bound as
            // the empire's production budget rose. Verify that two wildly different per-type budgets
            // — both far above the per-type cap — yield the SAME total non-pawn ware stacks. This is
            // robust to retuning baseThingCount / MaxStackScale / the per-type headroom.
            int modest = SimulateWeaponsCaravanStacks(500);
            int astronomical = SimulateWeaponsCaravanStacks(int.MaxValue / 2);
            TestAssert.AreEqual(modest, astronomical,
                "Caravan stack count must not scale with production budget once past the per-type cap");

            // Absolute boundedness: total stays proportional to the (clamped) target, never the
            // budget. Analytical worst case is ~3.5x target; 4x is a safe, retune-proof ceiling.
            int targetStacks = CaravanStockMath.TargetStacks(30, 30f, 3f);
            TestAssert.LessThanOrEqual(astronomical, 4 * targetStacks,
                "Total non-pawn stacks must stay proportional to the target, not the budget");
        }

        /// <summary>
        /// Runs the full ware-stack accounting <see cref="EmpireCaravanStockGenerator.GenerateThings"/>
        /// performs for an archotech weapons caravan (stackLimit-1 goods, production scaling maxed),
        /// returning the total non-pawn stack count for a given per-type unit budget. Private and
        /// un-attributed so the test runner does not pick it up as a test.
        /// </summary>
        private static int SimulateWeaponsCaravanStacks(int desiredUnitsPerType)
        {
            const int baseThingCount = 30;
            int targetStacks = CaravanStockMath.TargetStacks(baseThingCount, 30f, 3f);
            int silverStacks = CaravanStockMath.SilverStacks(100000, targetStacks, 500);
            int targetItemStacks = Mathf.Max(0, targetStacks - silverStacks);

            // GenerateThings trims item *types* to the item-stack budget (each type is >= 1 stack).
            int candidateTypes = 50;
            int itemTypes = (targetItemStacks > 0) ? Mathf.Min(candidateTypes, targetItemStacks) : candidateTypes;
            int perTypeCap = CaravanStockMath.PerTypeStackCap(targetItemStacks, itemTypes);

            int itemStacks = 0;
            for (int i = 0; i < itemTypes; i++)
                itemStacks += CaravanStockMath.ClampItemUnits(desiredUnitsPerType, perTypeCap, 1); // stackLimit 1
            return silverStacks + itemStacks;
        }
    }
}
