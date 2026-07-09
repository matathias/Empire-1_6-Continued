using LudeonTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace FactionColonies
{
    public static class EmpireTestRunner
    {
        /*-*-*- Standard (non-destructive) tier -*-*-*/

        [DebugAction("Empire", "Run All Tests", allowedGameStates = AllowedGameStates.Playing)]
        public static void RunAll() => RunTests(null, destructive: false);

        [DebugAction("Empire", "Run Tests by Category", allowedGameStates = AllowedGameStates.Playing)]
        public static void RunByCategory() => ShowCategoryMenu(destructive: false);

        /*-*-*- Destructive tier (save-first, mutates live state) -*-*-*/

        [DebugAction("Empire", "Run Destructive Tests", allowedGameStates = AllowedGameStates.Playing)]
        public static void RunAllDestructive() =>
            ConfirmDestructive(() => RunTests(null, destructive: true));

        [DebugAction("Empire", "Run Destructive Tests by Category", allowedGameStates = AllowedGameStates.Playing)]
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

        public static void RunTests(string category, bool destructive = false)
        {
            var tests = DiscoverTests().Where(t => t.attr.Destructive == destructive);
            if (category != null)
                tests = tests.Where(t => t.attr.Category == category);
            var list = tests.ToList();

            int passed = 0, failed = 0, errors = 0, skipped = 0;
            var skipDetails = new List<string>();
            var failDetails = new List<string>();
            var errorDetails = new List<string>();
            foreach (var (method, attr) in list)
            {
                string testName = $"[{attr.Category}] {method.DeclaringType.Name}.{method.Name}";
                try
                {
                    method.Invoke(null, null);
                    passed++;
                    LogUtil.Message($"PASS: {testName}");
                }
                catch (TargetInvocationException tie) when (tie.InnerException is TestSkippedException tse)
                {
                    skipped++;
                    LogUtil.Message($"SKIP: {testName} -- {tse.Message}");
                    skipDetails.Add($"  SKIP: {testName} -- {tse.Message}");
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

            string label = (category != null ? $"[{category}]" : "[All]")
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
                .Where(pair => pair.attr != null)
                .OrderBy(pair => pair.attr.Category)
                .ThenBy(pair => pair.method.Name)
                .ToList();
        }
    }
}
