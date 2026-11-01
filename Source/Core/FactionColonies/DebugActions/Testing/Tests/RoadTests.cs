using System.Collections.Generic;

namespace FactionColonies
{
    /* Coverage for the road system's pure key type. The road builder/queue/path themselves
     * are heavily world-coupled, but TileEdgeKey is the deterministic core that backs the
     * MST edge caches (and the long-packed save format), so it is unit-testable in isolation. */
    public static class RoadTests
    {
        // --- Constructor normalization ---

        [EmpireTest("Road")]
        public static void TileEdgeKey_Constructor_NormalizesLoHi()
        {
            var k = new TileEdgeKey(5, 3);
            TestAssert.AreEqual(3, k.lo, "lo should be the smaller tile");
            TestAssert.AreEqual(5, k.hi, "hi should be the larger tile");
        }

        [EmpireTest("Road")]
        public static void TileEdgeKey_Constructor_AlreadyOrdered_Unchanged()
        {
            var k = new TileEdgeKey(3, 5);
            TestAssert.AreEqual(3, k.lo);
            TestAssert.AreEqual(5, k.hi);
        }

        [EmpireTest("Road")]
        public static void TileEdgeKey_SelfEdge_LoEqualsHi()
        {
            var k = new TileEdgeKey(7, 7);
            TestAssert.AreEqual(7, k.lo);
            TestAssert.AreEqual(7, k.hi);
        }

        // --- Equality / hashing (undirected pair) ---

        [EmpireTest("Road")]
        public static void TileEdgeKey_Equals_IsSymmetric()
        {
            var a = new TileEdgeKey(3, 5);
            var b = new TileEdgeKey(5, 3);
            TestAssert.IsTrue(a.Equals(b), "(3,5) should equal (5,3)");
            TestAssert.IsTrue(b.Equals(a), "(5,3) should equal (3,5)");
        }

        [EmpireTest("Road")]
        public static void TileEdgeKey_Equals_DifferentEdges_NotEqual()
        {
            TestAssert.IsFalse(new TileEdgeKey(3, 5).Equals(new TileEdgeKey(3, 6)),
                "Different edges should not be equal");
        }

        [EmpireTest("Road")]
        public static void TileEdgeKey_GetHashCode_MatchesForReversedPair()
        {
            TestAssert.AreEqual(new TileEdgeKey(3, 5).GetHashCode(),
                new TileEdgeKey(5, 3).GetHashCode(),
                "Reversed pairs must hash identically");
        }

        [EmpireTest("Road")]
        public static void TileEdgeKey_WorksAsDictionaryKey_ReversedLookup()
        {
            var dict = new Dictionary<TileEdgeKey, float>();
            dict[new TileEdgeKey(3, 5)] = 1.5f;
            TestAssert.IsTrue(dict.TryGetValue(new TileEdgeKey(5, 3), out float v),
                "Lookup with the reversed pair should hit the same entry");
            TestAssert.AreEqual(1.5, v);
        }

        // --- Pack / Unpack save-format bridge ---

        [EmpireTest("Road")]
        public static void TileEdgeKey_PackUnpack_RoundTrips()
        {
            var k = new TileEdgeKey(17, 42);
            var round = TileEdgeKey.Unpack(k.Pack());
            TestAssert.IsTrue(k.Equals(round), "Unpack(Pack(k)) should equal k");
            TestAssert.AreEqual(17, round.lo);
            TestAssert.AreEqual(42, round.hi);
        }

        [EmpireTest("Road")]
        public static void TileEdgeKey_PackUnpack_LargeTileIds_RoundTrip()
        {
            var k = new TileEdgeKey(100000, 250000);
            var round = TileEdgeKey.Unpack(k.Pack());
            TestAssert.IsTrue(k.Equals(round), "Large tile ids should survive a pack/unpack round-trip");
        }
    }
}
