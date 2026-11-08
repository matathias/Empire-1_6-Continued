using LudeonTK;
using System.Reflection;

namespace FactionColonies.GiddyUpCompat;

/// <summary>
/// Debug-menu entry points for the Giddy-Up 2 compatibility module's tests.
/// Discovery, running and reporting live in the base mod's <see cref="EmpireTestRunner"/>;
/// this class only names the suite.
/// </summary>
public static class GiddyUpTestRunner
{
    private const string DebugCategory = "Empire Compat: Giddy-Up 2";

    private static readonly EmpireTestSuite Suite = new(
        Assembly.GetExecutingAssembly(),
        "Giddy-Up 2 Compat",
        "DESTRUCTIVE Giddy-Up 2 compatibility tests generate world pawns. They are NOT cleaned up afterward.",
        "Destructive Giddy-Up 2 compatibility tests left generated pawns in the world. Reload your pre-test save to restore the prior state.",
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
