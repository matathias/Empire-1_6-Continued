using FactionColonies.util;

namespace FactionColonies
{
    /* Coverage for WeightedSet<TKey> — the generic weighted-dictionary utility (cached total,
     * onChanged hook, chance lookups). Pure, no game state required. */
    public static class WeightedSetTests
    {
        // --- Get / Set ---

        [EmpireTest("Util")]
        public static void WeightedSet_Get_ReturnsSetWeight()
        {
            var ws = new WeightedSet<string>();
            ws.Set("a", 2.5f);
            TestAssert.AreEqual(2.5, ws.Get("a"));
        }

        [EmpireTest("Util")]
        public static void WeightedSet_Get_MissingKey_ReturnsZero()
        {
            var ws = new WeightedSet<string>();
            TestAssert.AreEqual(0.0, ws.Get("missing"));
        }

        [EmpireTest("Util")]
        public static void WeightedSet_NullKey_IsIgnored()
        {
            var ws = new WeightedSet<string>();
            ws.Set(null, 5f);
            TestAssert.AreEqual(0, ws.Count, "Null key Set should be a no-op");
        }

        // --- TotalWeight caching / invalidation ---

        [EmpireTest("Util")]
        public static void WeightedSet_TotalWeight_SumsEntries()
        {
            var ws = new WeightedSet<string>();
            ws.Set("a", 1f);
            ws.Set("b", 3f);
            TestAssert.AreEqual(4.0, ws.TotalWeight);
        }

        [EmpireTest("Util")]
        public static void WeightedSet_TotalWeight_RecomputesAfterChange()
        {
            var ws = new WeightedSet<string>();
            ws.Set("a", 1f);
            TestAssert.AreEqual(1.0, ws.TotalWeight); // first read caches the total
            ws.Set("b", 2f);                          // mutation must invalidate the cache
            TestAssert.AreEqual(3.0, ws.TotalWeight);
            ws.Remove("a");                           // removal must invalidate too
            TestAssert.AreEqual(2.0, ws.TotalWeight);
        }

        // --- ChanceOf ---

        [EmpireTest("Util")]
        public static void WeightedSet_ChanceOf_IsWeightOverTotal()
        {
            var ws = new WeightedSet<string>();
            ws.Set("a", 1f);
            ws.Set("b", 3f);
            TestAssert.AreEqual(0.25, ws.ChanceOf("a"));
            TestAssert.AreEqual(0.75, ws.ChanceOf("b"));
        }

        [EmpireTest("Util")]
        public static void WeightedSet_ChanceOf_NonPositiveDivisor_ReturnsZero()
        {
            var ws = new WeightedSet<string>();
            ws.Set("a", 5f);
            TestAssert.AreEqual(0.0, ws.ChanceOf("a", 0f));
            TestAssert.AreEqual(0.0, ws.ChanceOf("a", -1f));
        }

        [EmpireTest("Util")]
        public static void WeightedSet_ChanceOf_EmptySet_ReturnsZero()
        {
            var ws = new WeightedSet<string>();
            TestAssert.AreEqual(0.0, ws.ChanceOf("a"), message: "TotalWeight 0 -> chance 0");
        }

        // --- Remove / Clear / Count ---

        [EmpireTest("Util")]
        public static void WeightedSet_Remove_ReturnsTrueThenFalse()
        {
            var ws = new WeightedSet<string>();
            ws.Set("a", 1f);
            TestAssert.IsTrue(ws.Remove("a"), "First remove should return true");
            TestAssert.IsFalse(ws.Remove("a"), "Second remove should return false");
        }

        [EmpireTest("Util")]
        public static void WeightedSet_Clear_EmptiesSet()
        {
            var ws = new WeightedSet<string>();
            ws.Set("a", 1f);
            ws.Set("b", 2f);
            ws.Clear();
            TestAssert.AreEqual(0, ws.Count);
            TestAssert.AreEqual(0.0, ws.TotalWeight);
        }

        // --- Cull ---

        [EmpireTest("Util")]
        public static void WeightedSet_Cull_RemovesOnlyZeroWeights()
        {
            var ws = new WeightedSet<string>();
            ws.Set("keep", 2f);
            ws.Set("drop", 0f);
            ws.Cull();
            TestAssert.IsTrue(ws.ContainsKey("keep"), "Non-zero entry should remain");
            TestAssert.IsFalse(ws.ContainsKey("drop"), "Zero-weight entry should be culled");
            TestAssert.AreEqual(1, ws.Count);
        }

        // --- onChanged hook ---

        [EmpireTest("Util")]
        public static void WeightedSet_OnChanged_FiresOnMutations()
        {
            int calls = 0;
            var ws = new WeightedSet<string>(() => calls++);
            ws.Set("a", 1f);   // mutation 1
            ws.Set("a", 2f);   // mutation 2 (re-set is still a mutation)
            ws.Remove("a");    // mutation 3
            ws.Set("b", 1f);   // mutation 4
            ws.Clear();        // mutation 5 (non-empty -> fires)
            TestAssert.AreEqual(5, calls, "onChanged should fire once per mutation");
        }

        [EmpireTest("Util")]
        public static void WeightedSet_OnChanged_NoOpsDoNotFire()
        {
            int calls = 0;
            var ws = new WeightedSet<string>(() => calls++);
            ws.Remove("absent"); // not present -> no change
            ws.Clear();          // already empty -> no change
            TestAssert.AreEqual(0, calls, "No-op operations should not fire onChanged");
        }
    }
}
