using LudeonTK;
using System.Reflection;

namespace FactionColonies;

/// <summary>
/// One mod's test suite for <see cref="EmpireTestRunner"/>: the assembly its tests live in, how it
/// logs, and the text of its destructive-run warnings. A submod builds one and keeps only its four
/// [DebugAction] stubs, which must stay in the submod because the debug category is a constant.
/// </summary>
public sealed class EmpireTestSuite
{
    public readonly Assembly assembly;
    /// <summary>Names the suite in the destructive-run confirmation title.</summary>
    public readonly string name;
    /// <summary>What the destructive tests change; the confirmation adds the save-first advice.</summary>
    public readonly string destructiveWarning;
    /// <summary>Logged after a destructive run: what the run may have left behind.</summary>
    public readonly string residueNote;
    public readonly Action<string> log;
    public readonly Action<string> logError;
    public readonly Action<string> logForce;

    public EmpireTestSuite(Assembly assembly, string name, string destructiveWarning, string residueNote,
        Action<string> log, Action<string> logError, Action<string> logForce)
    {
        this.assembly = assembly;
        this.name = name;
        this.destructiveWarning = destructiveWarning;
        this.residueNote = residueNote;
        this.log = log;
        this.logError = logError;
        this.logForce = logForce;
    }
}

public static class EmpireTestRunner
{
    private static readonly EmpireTestSuite Suite = new(
        Assembly.GetExecutingAssembly(),
        "Empire",
        "DESTRUCTIVE TESTS mutate live game state (create/destroy settlements, run tax "
            + "cycles, hire/dismiss squads, create & resolve battles, fire events, level the "
            + "faction, enact/revoke edicts). They are NOT cleaned up afterward.",
        "Destructive tests left residue that is NOT auto-reverted: "
            + "silver spent, faction XP/level gained, bills/events created, letters & "
            + "messages fired, and any settlements/squads created if teardown was skipped. "
            + "Reload your pre-test save to restore the prior state.",
        LogUtil.Message, LogUtil.Error, LogUtil.MessageForce);

    // The debug actions use AllowedGameStates.Invalid (no constraint) so they are listed both on
    // the main menu and in a loaded game; the runner then skips tests whose TestGameState does
    // not match. AllowedGameStates flags are ANDed, so Entry | Playing would never be allowed.

    /*-*-*- Standard (non-destructive) tier -*-*-*/

    [DebugAction("Empire", "Run All Tests", allowedGameStates = AllowedGameStates.Invalid)]
    public static void RunAll() => RunTests(Suite, null, destructive: false);

    [DebugAction("Empire", "Run Tests by Category", allowedGameStates = AllowedGameStates.Invalid)]
    public static void RunByCategory() => ShowCategoryMenu(Suite, destructive: false);

    /*-*-*- Destructive tier (save-first, mutates live state) -*-*-*/

    [DebugAction("Empire", "Run Destructive Tests", allowedGameStates = AllowedGameStates.Invalid)]
    public static void RunAllDestructive() =>
        ConfirmDestructive(Suite, () => RunTests(Suite, null, destructive: true));

    [DebugAction("Empire", "Run Destructive Tests by Category", allowedGameStates = AllowedGameStates.Invalid)]
    public static void RunDestructiveByCategory() =>
        ConfirmDestructive(Suite, () => ShowCategoryMenu(Suite, destructive: true));

    /// <summary>Asks the player to confirm a destructive run of <paramref name="suite"/> first.</summary>
    public static void ConfirmDestructive(EmpireTestSuite suite, Action confirmedAct)
    {
        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
            suite.destructiveWarning + "\n\n"
            + "SAVE FIRST. The runner will not crash, but your game state may be thrashed.\n\n"
            + "Continue?",
            confirmedAct, destructive: true, title: $"Run Destructive {suite.name} Tests"));
    }

    /// <summary>Lists <paramref name="suite"/>'s test categories in the tier; picking one runs it.</summary>
    public static void ShowCategoryMenu(EmpireTestSuite suite, bool destructive)
    {
        var categories = DiscoverTests(suite.assembly)
            .Where(t => t.attr.Destructive == destructive)
            .Select(t => t.attr.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        var options = new List<DebugMenuOption>();
        foreach (string cat in categories)
        {
            string local = cat;
            options.Add(new DebugMenuOption(local, DebugMenuOptionMode.Action,
                () => RunTests(suite, local, destructive)));
        }
        Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
    }

    /// <summary>
    /// The skip reason for running a test in the current program state, or null if it may run.
    /// A game-state mismatch is the only valid reason to skip a test.
    /// </summary>
    public static string GameStateSkipReason(EmpireTestAttribute attr)
    {
        bool playing = Current.ProgramState == ProgramState.Playing;
        return attr.GameState switch
        {
            TestGameState.Playing when !playing => "needs a loaded game, but no game is running",
            TestGameState.MainMenu when playing => "needs the main menu, but a game is running",
            _ => null,
        };
    }

    /// <summary>False when the test is about a DLC or mod that is not active, so it is left out of the
    /// suite entirely.</summary>
    public static bool IsInSuite(EmpireTestAttribute attr) =>
        attr.RequiresPackageId is null || ModsConfig.IsActive(attr.RequiresPackageId);

    /// <summary>Runs <paramref name="suite"/>'s tests in the tier (all of them, or one
    /// <paramref name="category"/>) and logs a summary.</summary>
    public static void RunTests(EmpireTestSuite suite, string category, bool destructive = false)
    {
        var tests = DiscoverTests(suite.assembly).Where(t => t.attr.Destructive == destructive);
        if (category is not null)
            tests = tests.Where(t => t.attr.Category == category);
        var list = tests.ToList();

        int passed = 0, failed = 0, errors = 0, skipped = 0;
        var skipDetails = new List<string>();
        var failDetails = new List<string>();
        var errorDetails = new List<string>();
        foreach (var (method, attr) in list)
        {
            string testName = $"[{attr.Category}] {method.DeclaringType.Name}.{method.Name}";
            string skipReason = GameStateSkipReason(attr);
            if (skipReason is not null)
            {
                skipped++;
                suite.log($"SKIP: {testName} -- {skipReason}");
                skipDetails.Add($"  SKIP: {testName} -- {skipReason}");
                continue;
            }
            try
            {
                method.Invoke(null, null);
                passed++;
                suite.log($"PASS: {testName}");
            }
            catch (TargetInvocationException tie) when (tie.InnerException is TestFailedException tfe)
            {
                failed++;
                suite.logError($"FAIL: {testName} -- {tfe.Message}");
                failDetails.Add($"  FAIL: {testName} -- {tfe.Message}");
            }
            catch (Exception ex)
            {
                errors++;
                var inner = ex is TargetInvocationException t ? t.InnerException : ex;
                suite.logError($"ERROR: {testName} -- {inner.GetType().Name}: {inner.Message}");
                errorDetails.Add($"  ERROR: {testName} -- {inner.GetType().Name}: {inner.Message}");
            }
        }

        string label = (category is not null ? $"[{category}]" : "[All]")
            + (destructive ? " DESTRUCTIVE" : "");
        suite.logForce($"Test results {label}: {passed} passed, {failed} failed, {errors} errors, {skipped} skipped (of {list.Count} total)");
        if (failDetails.Count > 0)
        {
            suite.logForce("Failed tests:\n" + string.Join("\n", failDetails));
        }
        if (errorDetails.Count > 0)
        {
            suite.logForce("Errored tests:\n" + string.Join("\n", errorDetails));
        }
        if (skipDetails.Count > 0)
        {
            suite.logForce("Skipped tests:\n" + string.Join("\n", skipDetails));
        }
        if (destructive && list.Count > 0)
        {
            suite.logForce(suite.residueNote);
        }
    }

    private static List<(MethodInfo method, EmpireTestAttribute attr)> DiscoverTests(Assembly assembly)
    {
        return assembly
            .GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Select(m => (method: m, attr: m.GetCustomAttribute<EmpireTestAttribute>()))
            .Where(pair => pair.attr is not null && IsInSuite(pair.attr))
            .OrderBy(pair => pair.attr.Category)
            .ThenBy(pair => pair.method.Name)
            .ToList();
    }
}
