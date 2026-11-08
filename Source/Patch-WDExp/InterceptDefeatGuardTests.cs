using System.Reflection;
using HarmonyLib;

namespace FactionColonies.WDExp;

/// <summary>
/// The guard on WD Experimental's CheckDefeated prefix. WD's prefix is itself a bool prefix, so
/// for an Empire settlement the guard both skips it and supplies its return value: false, which
/// also keeps vanilla CheckDefeated away from the settlement. Any other settlement goes through
/// WD untouched. The guard is a private Harmony method, so it is invoked by reflection.
/// </summary>
public static class InterceptDefeatGuardTests
{
    private const string Category = "World Domination";

    private static readonly MethodInfo GuardPrefix = AccessTools.Method(typeof(Fix_WD_InterceptDefeat), "Prefix");

    /* Returns (runs WD's prefix, the value WD's prefix is taken to have returned). */
    private static (bool runWd, bool wdResult) RunGuard(Settlement settlement, bool wdResultBefore)
    {
        object[] args = [settlement, wdResultBefore];
        bool runWd = (bool)TestAssert.NotNull(GuardPrefix, "Fix_WD_InterceptDefeat.Prefix not found").Invoke(null, args);
        return (runWd, (bool)args[1]);
    }

    [EmpireTest(Category)]
    public static void Guard_OtherSettlement_RunsWdUnchanged()
    {
        (bool runWd, bool wdResult) = RunGuard(new Settlement(), true);
        TestAssert.IsTrue(runWd, "WD handles the defeat of non-Empire settlements");
        TestAssert.IsTrue(wdResult, "The guard leaves WD's return value alone");
    }

    [EmpireDestructiveTest(Category)]
    public static void Guard_EmpireSettlement_SkipsWdAndVanilla()
    {
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();
        (bool runWd, bool wdResult) = RunGuard(settlement, true);
        TestAssert.IsFalse(runWd, "WD's defeat intercept never runs for an Empire settlement");
        TestAssert.IsFalse(wdResult, "WD's prefix reports false, so vanilla CheckDefeated is skipped too");
    }
}
