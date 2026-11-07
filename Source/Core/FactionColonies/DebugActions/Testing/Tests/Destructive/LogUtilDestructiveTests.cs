using System.Runtime.CompilerServices;

namespace FactionColonies;

/* DESTRUCTIVE: temporarily turns the player's debug-logging setting on (restored afterwards) so the
   LoggingOn path of the DebugLogHandler is exercised regardless of the player's choice. */
public static class LogUtilDestructiveTests
{
    private static int holeEvaluations;

    private static int CountEvaluation() => ++holeEvaluations;

    /// <summary><c>FCSettings.printDebug</c>, the backing field of the read-only <see cref="FCSettings.PrintDebug"/>.</summary>
    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "printDebug")]
    private static extern ref bool PrintDebugSetting(FCSettings settings);

    [EmpireDestructiveTest("Destructive.Util")]
    public static void Message_Interpolated_LoggingOn_EvaluatesHolesOnce()
    {
        // Turn the debug setting on for the duration of the test, then put the player's choice back.
        ref bool printDebug = ref PrintDebugSetting(null);
        bool wasPrintDebug = printDebug;
        bool wasSuppressed = LogUtil.SuppressOnThisThread;
        holeEvaluations = 0;
        try
        {
            printDebug = true;
            LogUtil.SuppressOnThisThread = false;
            LogUtil.Message($"LogUtilTests probe (expected in log) {CountEvaluation()}");
            TestAssert.AreEqual(1, holeEvaluations, "each hole is evaluated exactly once when logging is on");
        }
        finally
        {
            LogUtil.SuppressOnThisThread = wasSuppressed;
            printDebug = wasPrintDebug;
        }
    }
}
