namespace FactionColonies
{
    public static class SettingsRescaleTests
    {
        // Verifies round-to-nearest with min-1 clamp matches the fresh-install per-day constants.
        [EmpireTest("SettingsRescale")]
        public static void Rescale_AdventureStory_SilverMatchesFreshInstall()
        {
            // legacy 100 / interval 5 = 20 (fresh DEFAULT_SILVER_PER_RESOURCE_ADVENTURESTORY)
            TestAssert.AreEqual(FCSettings.DEFAULT_SILVER_PER_RESOURCE_ADVENTURESTORY,
                FCSettings.RescalePerDay(100, 5));
        }

        [EmpireTest("SettingsRescale")]
        public static void Rescale_Peaceful_WorkerCost_RoundsToFresh()
        {
            // legacy 75 / interval 2 = 37.5 → round-away → 38 (fresh DEFAULT_WORKER_COST_PEACEFUL)
            TestAssert.AreEqual(FCSettings.DEFAULT_WORKER_COST_PEACEFUL,
                FCSettings.RescalePerDay(75, 2));
        }

        [EmpireTest("SettingsRescale")]
        public static void Rescale_StriveToSurvive_WorkerCost_RoundsToFresh()
        {
            // legacy 125 / interval 10 = 12.5 → round-away → 13 (fresh DEFAULT_WORKER_COST_STRIVETOSURVIVE)
            TestAssert.AreEqual(FCSettings.DEFAULT_WORKER_COST_STRIVETOSURVIVE,
                FCSettings.RescalePerDay(125, 10));
        }

        [EmpireTest("SettingsRescale")]
        public static void Rescale_LosingIsFun_TitheMod_ClampsToOne()
        {
            // legacy 10 / interval 30 = 0.33 → round → 0 → clamp → 1
            TestAssert.AreEqual(FCSettings.DEFAULT_PRODUCTION_TITHE_MOD_LOSINGISFUN,
                FCSettings.RescalePerDay(10, 30));
            TestAssert.AreEqual(1, FCSettings.RescalePerDay(10, 30));
        }

        [EmpireTest("SettingsRescale")]
        public static void Rescale_ZeroInterval_DoesNotDivideByZero()
        {
            TestAssert.AreEqual(70, FCSettings.RescalePerDay(70, 0)); // interval clamped to 1
        }
    }
}
