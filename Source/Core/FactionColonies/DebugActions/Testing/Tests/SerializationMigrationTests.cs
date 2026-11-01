using System.Collections.Generic;

namespace FactionColonies
{
    /* Coverage for the legacy save-migration seeders that run during FactionFC.ExposeData's
     * one-shot migration shim. WorldComponentArchiveTests covers a full FactionFC round-trip;
     * these target the seeders' pure logic directly (no Scribe context required). */
    public static class SerializationMigrationTests
    {
        // ============================
        // MilitaryFC.SeedNextIds (non-decreasing ID-counter advance)
        // ============================

        [EmpireTest("Serialization")]
        public static void SeedNextIds_AdvancesCountersWhenHigher()
        {
            var mil = new MilitaryFC(); // fresh: all next*Id counters start at 1
            mil.SeedNextIds(50, 40, 30, 20, 10);
            // Next*Id() pre-increments, so the next handed-out id is (seed + 1).
            TestAssert.AreEqual(51, mil.NextUnitId());
            TestAssert.AreEqual(41, mil.NextSquadId());
            TestAssert.AreEqual(31, mil.NextMercenaryId());
            TestAssert.AreEqual(21, mil.NextMercenarySquadId());
            TestAssert.AreEqual(11, mil.NextMilitaryFireSupportId());
        }

        [EmpireTest("Serialization")]
        public static void SeedNextIds_LowerValues_AreNoOp()
        {
            var mil = new MilitaryFC(); // counters = 1
            mil.SeedNextIds(0, 0, 0, 0, 0); // all below current -> counters unchanged
            TestAssert.AreEqual(2, mil.NextUnitId(), "Counter should stay at 1, so next id = 2");
        }

        // ============================
        // PolicyManager.SeedFromLegacy (null-preserving assignment)
        // ============================

        [EmpireTest("Serialization")]
        public static void SeedFromLegacy_NonNullArgs_Replace()
        {
            var pm = new PolicyManager();
            var legacyPolicies = new List<FCPolicy>();
            pm.SeedFromLegacy(legacyPolicies, null, null);
            TestAssert.IsTrue(ReferenceEquals(legacyPolicies, pm.policies),
                "Non-null legacy policies should replace the live list");
        }

        [EmpireTest("Serialization")]
        public static void SeedFromLegacy_NullArgs_LeaveExisting()
        {
            var pm = new PolicyManager();
            var originalTraits = pm.factionTraits;
            var originalEdicts = pm.edicts;
            pm.SeedFromLegacy(null, null, null);
            TestAssert.IsTrue(ReferenceEquals(originalTraits, pm.factionTraits),
                "Null legacy traits should leave the existing traits untouched");
            TestAssert.IsTrue(ReferenceEquals(originalEdicts, pm.edicts),
                "Null legacy edicts should leave the existing edicts untouched");
        }
    }
}
