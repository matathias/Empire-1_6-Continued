using HarmonyLib;
using LudeonTK;
using System.Reflection;
#if WDEXP
using TSA_WorldDomination;
#else
using WorldDomination;
#endif

#if WDEXP
namespace FactionColonies.WDExp;
#else
namespace FactionColonies.WD;
#endif

/// <summary>
/// Debug-menu entry points for the World Domination compatibility module's tests. Compiled into
/// both World Domination builds, like the patches; each lists its own suite. Discovery, running
/// and reporting live in the base mod's <see cref="EmpireTestRunner"/>.
/// </summary>
public static class WorldDominationTestRunner
{
#if WDEXP
    private const string DebugCategory = "Empire Compat: World Domination (Experimental)";
    private const string SuiteName = "World Domination (Experimental) Compat";
#else
    private const string DebugCategory = "Empire Compat: World Domination";
    private const string SuiteName = "World Domination Compat";
#endif

    private static readonly EmpireTestSuite Suite = new(
        Assembly.GetExecutingAssembly(),
        SuiteName,
        "DESTRUCTIVE World Domination compatibility tests may create the empire faction and a settlement. They are NOT cleaned up afterward.",
        "Destructive World Domination compatibility tests may have created the empire faction or a settlement. Reload your pre-test save to restore the prior state.",
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

/// <summary>
/// The World Domination strength modifier (WD settlement strength to Empire enemy level), the
/// world-power-stats filter that keeps the empire out of WD's leader/underdog maths, and the
/// faction-exclusion postfix. Settlements here are bare objects that are never added to the world,
/// so these tests leave no trace. Private Harmony methods are invoked by reflection.
/// </summary>
public static class WorldDominationTests
{
    private const string Category = "World Domination";

    /* A detached settlement carrying a CompViralSpread with the given WD strength. */
    private static Settlement MakeSettlementWithStrength(float strength)
    {
        var settlement = new Settlement();
        var comp = new CompViralSpread { parent = settlement };
#if WDEXP
        // The Experimental build reads the settlement's total local defense power.
        comp.offensiveStrength = strength / 2f;
        comp.defensiveStrength = strength / 2f;
#else
        comp.strength = strength;
#endif
        settlement.AllComps.Add(comp);
        return settlement;
    }

    /* Runs the modifier on a power that starts at level 7 and returns the resulting level. */
    private static double ModifiedLevel(Settlement settlement)
    {
        var power = new EnemyPower { level = 7, efficiency = 1.0 };
        new WDStrengthSettlementModifier().ModifySettlementPower(settlement, power);
        return power.level;
    }

    [EmpireTest(Category)]
    public static void StrengthModifier_ScalesStrengthByOneHundred()
    {
        TestAssert.AreEqual(2.5, ModifiedLevel(MakeSettlementWithStrength(250f)), 0.0001, "Strength 250 is level 2.5");
        TestAssert.AreEqual(10.0, ModifiedLevel(MakeSettlementWithStrength(1000f)), 0.0001, "Strength 1000 is level 10");
    }

    [EmpireTest(Category)]
    public static void StrengthModifier_FloorsAtLevelOne()
    {
        TestAssert.AreEqual(1.0, ModifiedLevel(MakeSettlementWithStrength(40f)), 0.0001,
            "Weak WD settlements map to Empire's minimum level, as battles never roll below 1");
        TestAssert.AreEqual(1.0, ModifiedLevel(MakeSettlementWithStrength(100f)), 0.0001, "Strength 100 is exactly level 1");
    }

    [EmpireTest(Category)]
    public static void StrengthModifier_NoStrengthOrNoComp_LeavesPowerUnchanged()
    {
        TestAssert.AreEqual(7.0, ModifiedLevel(MakeSettlementWithStrength(0f)), 0.0001, "Zero strength is not meaningful yet");
        TestAssert.AreEqual(7.0, ModifiedLevel(MakeSettlementWithStrength(-50f)), 0.0001, "Negative strength is ignored");
        TestAssert.AreEqual(7.0, ModifiedLevel(new Settlement()), 0.0001, "A settlement without the WD comp is ignored");

        var power = new EnemyPower { level = 7, efficiency = 1.0 };
        TestAssert.DoesNotThrow(() => new WDStrengthSettlementModifier().ModifySettlementPower(null, power), "Null settlement");
        TestAssert.AreEqual(7.0, power.level, 0.0001);
    }

    // ============================
    // GetWorldPowerStats filter
    // ============================

    private static readonly MethodInfo StatsPostfix = AccessTools.Method(typeof(Patch_GetWorldPowerStats), "Postfix");

    private static void RunStatsPostfix(SpreadLogEntry.GlobalWorldStats stats) =>
        TestAssert.NotNull(StatsPostfix, "Patch_GetWorldPowerStats.Postfix not found").Invoke(null, [stats]);

    private static SpreadLogEntry.FactionStat MakeStat(Faction faction, float tier1, float tier3)
    {
        var stat = new SpreadLogEntry.FactionStat { faction = faction };
        stat.strength[1] = tier1;
        stat.strength[3] = tier3;
        return stat;
    }

    [EmpireTest(Category)]
    public static void WorldPowerStats_NullResult_DoesNotThrow()
    {
        TestAssert.DoesNotThrow(() => RunStatsPostfix(null));
    }

    [EmpireDestructiveTest(Category)]
    public static void WorldPowerStats_RemovesTheEmpireAndItsStrength()
    {
        Faction empire = DestructiveTestUtil.EnsureEmpireFaction();
        Faction other = TestAssert.NotNull(Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => f != empire && !f.IsPlayer),
            "A non-player, non-empire faction should exist");

        SpreadLogEntry.FactionStat otherStat = MakeStat(other, 30f, 70f);
        SpreadLogEntry.FactionStat empireStat = MakeStat(empire, 10f, 40f);
        var stats = new SpreadLogEntry.GlobalWorldStats();
        stats.FactionStats.Add(otherStat);
        stats.FactionStats.Add(empireStat);
        stats.GlobalTotalStr = otherStat.TotalStr + empireStat.TotalStr;
        for (int t = 1; t <= 4; t++)
            stats.GlobalTierStr[t] = otherStat.strength[t] + empireStat.strength[t];

        RunStatsPostfix(stats);

        TestAssert.AreEqual(1, stats.FactionStats.Count, "The empire's entry is removed");
        TestAssert.IsTrue(ReferenceEquals(otherStat, stats.FactionStats[0]), "Other factions stay");
        TestAssert.AreEqual(100.0, stats.GlobalTotalStr, 0.001, "The world total no longer counts the empire");
#if WDEXP
        // Only the Experimental build tracks per-tier totals that the filter must reduce too.
        TestAssert.AreEqual(30.0, stats.GlobalTierStr[1], 0.001, "Tier 1 total no longer counts the empire");
        TestAssert.AreEqual(70.0, stats.GlobalTierStr[3], 0.001, "Tier 3 total no longer counts the empire");
#endif
    }

    // ============================
    // IsExcludedFaction
    // ============================

    private static readonly MethodInfo ExcludedPostfix = AccessTools.Method(typeof(Patch_IsExcludedFaction), "Postfix");

    private static bool RunExcludedPostfix(Faction faction, bool wdResult)
    {
        object[] args = [faction, wdResult];
        TestAssert.NotNull(ExcludedPostfix, "Patch_IsExcludedFaction.Postfix not found").Invoke(null, args);
        return (bool)args[1];
    }

    [EmpireDestructiveTest(Category)]
    public static void IsExcludedFaction_ExcludesOnlyTheEmpire()
    {
        Faction empire = DestructiveTestUtil.EnsureEmpireFaction();
        Faction other = TestAssert.NotNull(Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => f != empire && !f.IsPlayer),
            "A non-player, non-empire faction should exist");

        TestAssert.IsTrue(RunExcludedPostfix(empire, false), "The empire is kept out of WD's daily actions");
        TestAssert.IsFalse(RunExcludedPostfix(other, false), "Other factions keep WD's verdict");
        TestAssert.IsTrue(RunExcludedPostfix(other, true), "WD's own exclusions stand");
        TestAssert.IsFalse(RunExcludedPostfix(null, false), "A null faction is not the empire");
    }
}
