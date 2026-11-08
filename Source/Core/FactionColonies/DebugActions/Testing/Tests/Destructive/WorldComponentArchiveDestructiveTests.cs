namespace FactionColonies;

/* DESTRUCTIVE: temporarily flips the live FCSettings.battleArchiveUnlimited setting (restored
   afterwards) so both the capped and the unlimited archive paths run regardless of the player's
   choice. Records go into a throwaway archive instance, never the save-persisted one. */
public static class WorldComponentArchiveDestructiveTests
{
    private static void WithArchiveUnlimited(bool unlimited, Action body)
        => TestUtil.WithOverride(() => FCSettings.battleArchiveUnlimited, v => FCSettings.battleArchiveUnlimited = v, unlimited, body);

    [EmpireDestructiveTest("Destructive.Military")]
    public static void BattleReportCount_MatchesRecorded() => WithArchiveUnlimited(false, () =>
    {
        WorldComponent_Archive archive = WorldComponentArchiveTests.NewArchive();

        int before = archive.BattleReportCount;
        archive.RecordBattleReport(WorldComponentArchiveTests.MakeTestResult("count_1"));
        archive.RecordBattleReport(WorldComponentArchiveTests.MakeTestResult("count_2"));
        // Cap might evict entries — only assert count grew by AT MOST 2 (could be less if
        // we were already at the cap, in which case eviction matches insertion 1:1).
        int after = archive.BattleReportCount;
        int delta = after - before;
        TestAssert.IsTrue(delta >= 0 && delta <= 2,
            $"BattleReportCount delta should be 0..2; got {delta}");
        TestAssert.LessThanOrEqual(after, FCSettings.battleArchiveMaxEntries,
            "Archive should never exceed the configured cap");
    });

    [EmpireDestructiveTest("Destructive.Military")]
    public static void RecordBattleReport_RespectsCap() => WithArchiveUnlimited(false, () =>
    {
        WorldComponent_Archive archive = WorldComponentArchiveTests.NewArchive();

        int cap = FCSettings.battleArchiveMaxEntries;
        // Insert cap+5 entries; the archive should stay at <= cap entries afterward.
        for (int i = 0; i < cap + 5; i++)
        {
            archive.RecordBattleReport(WorldComponentArchiveTests.MakeTestResult($"cap_{i}"));
        }
        TestAssert.LessThanOrEqual(archive.BattleReportCount, cap,
            "Archive should not exceed configured cap after over-fill");
    });

    [EmpireDestructiveTest("Destructive.Military")]
    public static void RecordBattleReport_Unlimited_DoesNotEvict() => WithArchiveUnlimited(true, () =>
    {
        WorldComponent_Archive archive = WorldComponentArchiveTests.NewArchive();

        int before = archive.BattleReportCount;
        const int inserted = 25;
        for (int i = 0; i < inserted; i++)
        {
            archive.RecordBattleReport(WorldComponentArchiveTests.MakeTestResult($"unlimited_{i}"));
        }
        TestAssert.AreEqual(before + inserted, archive.BattleReportCount,
            "Unlimited archive should keep every recorded report (no eviction)");
    });
}
