using LudeonTK;
using System.Reflection;

namespace FactionColonies.VPE;

/// <summary>
/// Debug-menu entry points for the Vanilla Psycasts Expanded compatibility module's tests.
/// Discovery, running and reporting live in the base mod's <see cref="EmpireTestRunner"/>;
/// this class only names the suite.
/// </summary>
public static class VPETestRunner
{
    private const string DebugCategory = "Empire Compat: Vanilla Psycasts Expanded";

    private static readonly EmpireTestSuite Suite = new(
        Assembly.GetExecutingAssembly(),
        "VPE Compat",
        "DESTRUCTIVE VPE compatibility tests mutate live game state. They are NOT cleaned up afterward.",
        "Destructive VPE compatibility tests may have left residue. Reload your pre-test save to restore the prior state.",
        LogUtil.Message, LogUtil.Error, LogUtil.MessageForce);

    /*-*-*- Standard (non-destructive) tier -*-*-*/

    [DebugAction(DebugCategory, "Run All Tests", allowedGameStates = AllowedGameStates.Invalid)]
    public static void RunAll() => EmpireTestRunner.RunTests(Suite, null, destructive: false);

    [DebugAction(DebugCategory, "Run Tests by Category", allowedGameStates = AllowedGameStates.Invalid)]
    public static void RunByCategory() => EmpireTestRunner.ShowCategoryMenu(Suite, destructive: false);

    /*-*-*- Destructive tier (save-first, mutates live state) -*-*-*/

    [DebugAction(DebugCategory, "Run Destructive Tests", allowedGameStates = AllowedGameStates.Invalid)]
    public static void RunAllDestructive() =>
        EmpireTestRunner.ConfirmDestructive(Suite, () => EmpireTestRunner.RunTests(Suite, null, destructive: true));

    [DebugAction(DebugCategory, "Run Destructive Tests by Category", allowedGameStates = AllowedGameStates.Invalid)]
    public static void RunDestructiveByCategory() =>
        EmpireTestRunner.ConfirmDestructive(Suite, () => EmpireTestRunner.ShowCategoryMenu(Suite, destructive: true));
}
