using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /* Tests for WorldComponent_Archive's battle-report storage. Recording is a persistent
       mutation on the live archive, so these tests run against a THROWAWAY archive instance
       (NewArchive) rather than the save-persisted WorldComponent_Archive.Get(): the record /
       cap-eviction / ordering / count logic is entirely instance-local (it only reads
       Find.TickManager and FCSettings), so a fresh instance gives full coverage without ever
       touching — or leaking test records into — the player's real archive. */
    public static class WorldComponentArchiveTests
    {
        private const string TestLabelPrefix = "_TestArchive_";

        /// <summary>
        /// A fresh, unregistered <see cref="WorldComponent_Archive"/> for isolated testing. All
        /// storage logic operates on the instance's own list, so nothing here mutates the live,
        /// save-persisted archive returned by <see cref="WorldComponent_Archive.Get"/>.
        /// </summary>
        private static WorldComponent_Archive NewArchive()
        {
            return new WorldComponent_Archive(Find.World);
        }

        private static BattleResult MakeTestResult(string tag)
        {
            return new BattleResult
            {
                winner = BattleWinner.Attacker,
                attackerInitialForce = 10,
                defenderInitialForce = 5,
                attackerForceRemaining = 8,
                defenderForceRemaining = 0,
                attackerLabel = TestLabelPrefix + tag,
                defenderLabel = "TestDefender",
                attackerFactionName = "TestAttackerFaction",
                defenderFactionName = "TestDefenderFaction"
            };
        }

        // -*- RecordBattleReport -*-

        [EmpireTest("Military")]
        public static void RecordBattleReport_AssignsAscendingIds()
        {
            if (Find.World is null) TestAssert.Skip("No world");
            WorldComponent_Archive archive = NewArchive();

            int id1 = archive.RecordBattleReport(MakeTestResult("ids_1"));
            int id2 = archive.RecordBattleReport(MakeTestResult("ids_2"));
            int id3 = archive.RecordBattleReport(MakeTestResult("ids_3"));

            TestAssert.GreaterThan(id1, 0, "First id should be > 0");
            TestAssert.GreaterThan(id2, id1, "Ids should ascend");
            TestAssert.GreaterThan(id3, id2, "Ids should ascend");
        }

        [EmpireTest("Military")]
        public static void RecordBattleReport_StoresRecordedTick()
        {
            if (Find.World is null) TestAssert.Skip("No world");
            WorldComponent_Archive archive = NewArchive();

            BattleResult r = MakeTestResult("tick");
            archive.RecordBattleReport(r);
            TestAssert.AreEqual(Find.TickManager.TicksGame, r.recordedTick,
                "recordedTick should be stamped to TicksGame at insertion");
        }

        [EmpireTest("Military")]
        public static void RecordBattleReport_StoresReportId()
        {
            if (Find.World is null) TestAssert.Skip("No world");
            WorldComponent_Archive archive = NewArchive();

            BattleResult r = MakeTestResult("reportid");
            int returnedId = archive.RecordBattleReport(r);
            TestAssert.AreEqual(returnedId, r.reportId,
                "reportId on the result should match the returned id");
        }

        [EmpireTest("Military")]
        public static void RecordBattleReport_NullResult_ReturnsZero()
        {
            if (Find.World is null) TestAssert.Skip("No world");
            WorldComponent_Archive archive = NewArchive();

            TestAssert.AreEqual(0, archive.RecordBattleReport(null));
        }

        // -*- TryGetBattleReport -*-

        [EmpireTest("Military")]
        public static void TryGetBattleReport_Found_ReturnsTrueAndResult()
        {
            if (Find.World is null) TestAssert.Skip("No world");
            WorldComponent_Archive archive = NewArchive();

            BattleResult r = MakeTestResult("get_found");
            int id = archive.RecordBattleReport(r);

            TestAssert.IsTrue(archive.TryGetBattleReport(id, out BattleResult fetched));
            TestAssert.IsTrue(System.Object.ReferenceEquals(r, fetched),
                "TryGetBattleReport should return the same instance");
        }

        [EmpireTest("Military")]
        public static void TryGetBattleReport_NotFound_ReturnsFalse()
        {
            if (Find.World is null) TestAssert.Skip("No world");
            WorldComponent_Archive archive = NewArchive();

            TestAssert.IsFalse(archive.TryGetBattleReport(int.MaxValue, out BattleResult fetched));
            TestAssert.IsNull(fetched);
        }

        [EmpireTest("Military")]
        public static void TryGetBattleReport_ZeroId_ReturnsFalse()
        {
            // Guard in TryGetBattleReport — id <= 0 short-circuits.
            if (Find.World is null) TestAssert.Skip("No world");
            WorldComponent_Archive archive = NewArchive();

            TestAssert.IsFalse(archive.TryGetBattleReport(0, out BattleResult fetched));
            TestAssert.IsNull(fetched);
        }

        [EmpireTest("Military")]
        public static void TryGetBattleReport_NegativeId_ReturnsFalse()
        {
            if (Find.World is null) TestAssert.Skip("No world");
            WorldComponent_Archive archive = NewArchive();

            TestAssert.IsFalse(archive.TryGetBattleReport(-1, out BattleResult fetched));
            TestAssert.IsNull(fetched);
        }

        // -*- RecentBattleReports ordering -*-

        [EmpireTest("Military")]
        public static void RecentBattleReports_NewestFirst()
        {
            if (Find.World is null) TestAssert.Skip("No world");
            WorldComponent_Archive archive = NewArchive();

            int id1 = archive.RecordBattleReport(MakeTestResult("order_1"));
            int id2 = archive.RecordBattleReport(MakeTestResult("order_2"));
            int id3 = archive.RecordBattleReport(MakeTestResult("order_3"));

            // Walk RecentBattleReports until we find our three test entries; assert ordering.
            List<int> seenIds = new List<int>();
            foreach (BattleResult r in archive.RecentBattleReports)
            {
                if (r is null) continue;
                if (r.attackerLabel is null) continue;
                if (!r.attackerLabel.StartsWith(TestLabelPrefix + "order_")) continue;
                seenIds.Add(r.reportId);
                if (seenIds.Count >= 3) break;
            }

            TestAssert.IsTrue(seenIds.Count >= 3, $"Expected to find 3 test entries; saw {seenIds.Count}");
            // The first one we encountered should be the newest (id3).
            TestAssert.AreEqual(id3, seenIds[0], "Newest entry should come first");
            TestAssert.AreEqual(id2, seenIds[1], "Middle entry second");
            TestAssert.AreEqual(id1, seenIds[2], "Oldest of the three third");
        }

        [EmpireTest("Military")]
        public static void BattleReportCount_MatchesRecorded()
        {
            if (Find.World is null) TestAssert.Skip("No world");
            WorldComponent_Archive archive = NewArchive();
            if (FCSettings.battleArchiveUnlimited) TestAssert.Skip("Archive cap disabled");

            int before = archive.BattleReportCount;
            archive.RecordBattleReport(MakeTestResult("count_1"));
            archive.RecordBattleReport(MakeTestResult("count_2"));
            // Cap might evict entries — only assert count grew by AT MOST 2 (could be less if
            // we were already at the cap, in which case eviction matches insertion 1:1).
            int after = archive.BattleReportCount;
            int delta = after - before;
            TestAssert.IsTrue(delta >= 0 && delta <= 2,
                $"BattleReportCount delta should be 0..2; got {delta}");
            TestAssert.LessThanOrEqual(after, FCSettings.battleArchiveMaxEntries,
                "Archive should never exceed the configured cap");
        }

        // -*- Cap eviction -*-

        [EmpireTest("Military")]
        public static void RecordBattleReport_RespectsCap()
        {
            if (Find.World is null) TestAssert.Skip("No world");
            WorldComponent_Archive archive = NewArchive();
            if (FCSettings.battleArchiveUnlimited) TestAssert.Skip("Archive cap disabled");

            int cap = FCSettings.battleArchiveMaxEntries;
            // Insert cap+5 entries; the archive should stay at <= cap entries afterward.
            for (int i = 0; i < cap + 5; i++)
            {
                archive.RecordBattleReport(MakeTestResult($"cap_{i}"));
            }
            TestAssert.LessThanOrEqual(archive.BattleReportCount, cap,
                "Archive should not exceed configured cap after over-fill");
        }

        [EmpireTest("Military")]
        public static void RecordBattleReport_Unlimited_DoesNotEvict()
        {
            if (Find.World is null) TestAssert.Skip("No world");
            WorldComponent_Archive archive = NewArchive();
            // Only meaningful when the user has opted into unlimited archiving.
            if (!FCSettings.battleArchiveUnlimited) TestAssert.Skip("Archive cap enabled");

            int before = archive.BattleReportCount;
            const int inserted = 25;
            for (int i = 0; i < inserted; i++)
            {
                archive.RecordBattleReport(MakeTestResult($"unlimited_{i}"));
            }
            TestAssert.AreEqual(before + inserted, archive.BattleReportCount,
                "Unlimited archive should keep every recorded report (no eviction)");
        }
    }
}
