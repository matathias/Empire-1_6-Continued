namespace FactionColonies;

/* LogUtil.Message($"...") binds to the DebugLogHandler overload, which skips evaluating the string's
   holes when debug logging is off. Each test counts how many times a hole runs. */
public static class LogUtilTests
{
    private static int holeEvaluations;

    private static int CountEvaluation() => ++holeEvaluations;

    [EmpireTest("Util")]
    public static void Message_Interpolated_LoggingOff_SkipsHoles()
    {
        // Suppression drives the same check as the debug setting, without touching the player's setting.
        bool wasSuppressed = LogUtil.SuppressOnThisThread;
        holeEvaluations = 0;
        try
        {
            LogUtil.SuppressOnThisThread = true;
            LogUtil.Message($"LogUtilTests probe (should not appear) {CountEvaluation()}");
            TestAssert.AreEqual(0, holeEvaluations, "holes are not evaluated when logging is off");
        }
        finally { LogUtil.SuppressOnThisThread = wasSuppressed; }
    }
}
