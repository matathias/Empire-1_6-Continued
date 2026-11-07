using LudeonTK;
using System.Reflection;

namespace FactionColonies;

public static class EmpireTestRunner
{
    // The debug actions use AllowedGameStates.Invalid (no constraint) so they are listed both on
    // the main menu and in a loaded game; the runner then skips tests whose TestGameState does
    // not match. AllowedGameStates flags are ANDed, so Entry | Playing would never be allowed.

    /*-*-*- Standard (non-destructive) tier -*-*-*/

    [DebugAction("Empire", "Run All Tests", allowedGameStates = AllowedGameStates.Invalid)]
    public static void RunAll() => RunTests(null, destructive: false);

    [DebugAction("Empire", "Run Tests by Category", allowedGameStates = AllowedGameStates.Invalid)]
    public static void RunByCategory() => ShowCategoryMenu(destructive: false);

    /*-*-*- Destructive tier (save-first, mutates live state) -*-*-*/

    [DebugAction("Empire", "Run Destructive Tests", allowedGameStates = AllowedGameStates.Invalid)]
    public static void RunAllDestructive() =>
        ConfirmDestructive(() => RunTests(null, destructive: true));

    [DebugAction("Empire", "Run Destructive Tests by Category", allowedGameStates = AllowedGameStates.Invalid)]
    public static void RunDestructiveByCategory() =>
        ConfirmDestructive(() => ShowCategoryMenu(destructive: true));

    private static void ConfirmDestructive(Action confirmedAct)
    {
        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
            "DESTRUCTIVE TESTS mutate live game state (create/destroy settlements, run tax "
            + "cycles, hire/dismiss squads, create & resolve battles, fire events, level the "
            + "faction, enact/revoke edicts). They are NOT cleaned up afterward.\n\n"
            + "SAVE FIRST. The runner will not crash, but your game state will be thrashed.\n\n"
            + "Continue?",
            confirmedAct, destructive: true, title: "Run Destructive Empire Tests"));
    }

    private static void ShowCategoryMenu(bool destructive)
    {
        var categories = DiscoverTests()
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
                () => RunTests(local, destructive)));
        }
        Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
    }

    /// <summary>
    /// The skip reason for running a test in the current program state, or null if it may run.
    /// A game-state mismatch is the only valid reason to skip a test; the submod runners call this
    /// too so the rule is enforced in one place.
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
    /// suite entirely. The submod runners' discovery calls this too.</summary>
    public static bool IsInSuite(EmpireTestAttribute attr) =>
        attr.RequiresPackageId is null || ModsConfig.IsActive(attr.RequiresPackageId);

    public static void RunTests(string category, bool destructive = false)
    {
        var tests = DiscoverTests().Where(t => t.attr.Destructive == destructive);
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
                LogUtil.Message($"SKIP: {testName} -- {skipReason}");
                skipDetails.Add($"  SKIP: {testName} -- {skipReason}");
                continue;
            }
            try
            {
                method.Invoke(null, null);
                passed++;
                LogUtil.Message($"PASS: {testName}");
            }
            catch (TargetInvocationException tie) when (tie.InnerException is TestFailedException tfe)
            {
                failed++;
                LogUtil.Error($"FAIL: {testName} -- {tfe.Message}");
                failDetails.Add($"  FAIL: {testName} -- {tfe.Message}");
            }
            catch (Exception ex)
            {
                errors++;
                var inner = ex is TargetInvocationException t ? t.InnerException : ex;
                LogUtil.Error($"ERROR: {testName} -- {inner.GetType().Name}: {inner.Message}");
                errorDetails.Add($"  ERROR: {testName} -- {inner.GetType().Name}: {inner.Message}");
            }
        }

        string label = (category is not null ? $"[{category}]" : "[All]")
            + (destructive ? " DESTRUCTIVE" : "");
        LogUtil.MessageForce($"Test results {label}: {passed} passed, {failed} failed, {errors} errors, {skipped} skipped (of {list.Count} total)");
        if (failDetails.Count > 0)
        {
            LogUtil.MessageForce("Failed tests:\n" + string.Join("\n", failDetails));
        }
        if (errorDetails.Count > 0)
        {
            LogUtil.MessageForce("Errored tests:\n" + string.Join("\n", errorDetails));
        }
        if (skipDetails.Count > 0)
        {
            LogUtil.MessageForce("Skipped tests:\n" + string.Join("\n", skipDetails));
        }
        if (destructive && list.Count > 0)
        {
            LogUtil.MessageForce("Destructive tests left residue that is NOT auto-reverted: "
                + "silver spent, faction XP/level gained, bills/events created, letters & "
                + "messages fired, and any settlements/squads created if teardown was skipped. "
                + "Reload your pre-test save to restore the prior state.");
        }
    }

    private static List<(MethodInfo method, EmpireTestAttribute attr)> DiscoverTests()
    {
        return Assembly.GetExecutingAssembly()
            .GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Select(m => (method: m, attr: m.GetCustomAttribute<EmpireTestAttribute>()))
            .Where(pair => pair.attr is not null && IsInSuite(pair.attr))
            .OrderBy(pair => pair.attr.Category)
            .ThenBy(pair => pair.method.Name)
            .ToList();
    }
}
