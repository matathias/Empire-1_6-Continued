namespace FactionColonies;

/* DESTRUCTIVE: exact policy stat values need a clean faction slate, so these tests revoke every edict
   and remove faction-wide stat events (neither is restored), and may create a transient settlement.
   Policies and traits are snapshotted and restored. */
public static class PolicyDestructiveTests
{
    // ============================
    // Stat Aggregation Tests
    // ============================

    [EmpireDestructiveTest("Destructive.Policy")]
    public static void StatAggregation_MultiplicativeDefault_IsOne()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();

        var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
        try
        {
            ForcedStateTestUtilA.ForceCleanFactionStatSlate(faction);
            // happinessGainedMultiplier is multiplicative with IdentityValue 1
            double val = faction.GetFactionStatValue(FCStatDefOf.happinessGainedMultiplier);
            TestAssert.AreEqual(1.0, val, 0.001, "Multiplicative stat with no policies, edicts or events should be 1");
        }
        finally
        {
            PolicyTestHelper.RestorePolicies(faction, snapshot);
        }
        DestructiveTestUtil.AssertEmpireInvariants(faction, "StatAggregation_MultiplicativeDefault_IsOne");
    }

    [EmpireDestructiveTest("Destructive.Policy")]
    public static void StatAggregation_PolicyModifiers_Applied()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();

        var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
        try
        {
            ForcedStateTestUtilA.ForceCleanFactionStatSlate(faction);

            // A policy with known statModifiers
            FCPolicyDef testDef = FCPolicyDefOf.militaristic;
            TestAssert.IsNotEmpty(testDef.statModifiers, "militaristic should ship XML stat modifiers");

            PolicyTestHelper.EnactPolicy(faction, testDef);

            // Check each stat modifier is reflected
            foreach (FCStatModifier mod in testDef.statModifiers)
            {
                double expected;
                if (mod.stat.aggregation == FCStatAggregation.Additive)
                    expected = mod.stat.IdentityValue + mod.value;
                else
                    expected = mod.stat.IdentityValue * mod.value;

                double actual = faction.GetFactionStatValue(mod.stat);
                TestAssert.AreEqual(expected, actual, 0.001,
                    $"Stat {mod.stat.defName} should reflect policy modifier");
            }
        }
        finally
        {
            PolicyTestHelper.RestorePolicies(faction, snapshot);
        }
        DestructiveTestUtil.AssertEmpireInvariants(faction, "StatAggregation_PolicyModifiers_Applied");
    }

    [EmpireDestructiveTest("Destructive.Policy")]
    public static void StatAggregation_MultiplePolicies_Stack()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();

        // Two standalone policy defs sharing an additive stat, so the test does not depend on
        // which shipped policies happen to modify it.
        FCStatDef sharedStat = FCStatDefOf.militaryBaseLevel;
        TestAssert.IsTrue(sharedStat.aggregation == FCStatAggregation.Additive, "militaryBaseLevel should be additive");
        FCPolicyDef def0 = new()
        {
            defName = "EmpireTest_StackPolicyA",
            statModifiers = [new() { stat = sharedStat, value = 2 }]
        };
        FCPolicyDef def1 = new()
        {
            defName = "EmpireTest_StackPolicyB",
            statModifiers = [new() { stat = sharedStat, value = 3 }]
        };

        var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
        try
        {
            ForcedStateTestUtilA.ForceCleanFactionStatSlate(faction);

            PolicyTestHelper.EnactPolicy(faction, def0);
            PolicyTestHelper.EnactPolicy(faction, def1);

            double expected = sharedStat.IdentityValue + 2 + 3;
            double actual = faction.GetFactionStatValue(sharedStat);
            TestAssert.AreEqual(expected, actual, 0.001,
                "Two additive policy modifiers should stack: 2 + 3");
        }
        finally
        {
            PolicyTestHelper.RestorePolicies(faction, snapshot);
        }
        DestructiveTestUtil.AssertEmpireInvariants(faction, "StatAggregation_MultiplePolicies_Stack");
    }

    // ============================
    // Specific Behavior Tests
    // ============================

    [EmpireDestructiveTest("Destructive.Policy")]
    public static void Egalitarian_WorkerProductionMultiplier_10Pct()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();

        var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
        try
        {
            ForcedStateTestUtilA.ForceCleanFactionStatSlate(faction);
            // A settlement with no settlement-scope worker-production modifiers of its own.
            WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement(s =>
                ForcedStateTestUtilA.SettlementStatsAtIdentity(s, FCStatDefOf.workerProductionMultiplier));

            PolicyTestHelper.EnactPolicy(faction, FCPolicyDefOf.egalitarian);

            double mult = faction.GetStatValue(FCStatDefOf.workerProductionMultiplier, settlement);
            TestAssert.AreEqual(1.1, mult, 0.01,
                $"Egalitarian should boost worker production by 10% (got {mult})");
        }
        finally
        {
            PolicyTestHelper.RestorePolicies(faction, snapshot);
        }
        DestructiveTestUtil.AssertEmpireInvariants(faction, "Egalitarian_WorkerProductionMultiplier_10Pct");
    }

    [EmpireDestructiveTest("Destructive.Policy")]
    public static void Militaristic_BuildingUpkeep_Discount()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();

        // The Militaristic discount is the buildingUpkeepBase_Military stat (-10 post-rework), applied
        // via the live GetBuildingUpkeep path. Military classification is the explicit isMilitary flag.
        BuildingFCDef milBuilding = TestAssert.NotNull(DefDatabase<BuildingFCDef>.AllDefsListForReading
                .FirstOrDefault(b => b.isMilitary && b.Upkeep > 0),
            "No military building with upkeep is defined");
        BuildingFCDef civBuilding = TestAssert.NotNull(DefDatabase<BuildingFCDef>.AllDefsListForReading
                .FirstOrDefault(b => !b.isMilitary && b.Upkeep > 0
                    && b != BuildingFCDefOf.Empty && b != BuildingFCDefOf.Construction),
            "No civilian building with upkeep is defined");

        var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
        try
        {
            // Militaristic must be the only upkeep modifier: clean faction slate, a settlement with no
            // upkeep modifiers of its own, and a neutral difficulty factor.
            WorldSettlementFC settlement = ForcedStateTestUtilA.EnsureDefaultUpkeepSettlement(faction);
            var policy = PolicyTestHelper.EnactPolicy(faction, FCPolicyDefOf.militaristic);
            TestAssert.IsNotNull(policy.behavior as FCPolicyBehavior_Militaristic, "Militaristic should have a behavior");

            ForcedStateTestUtilA.WithBuildingUpkeepDifficultyMult(1f, () =>
            {
                // GetBuildingUpkeep truncates to int and floors regular buildings at 0.
                TestAssert.AreEqual(Math.Max(Math.Truncate(milBuilding.Upkeep - 10), 0.0),
                    settlement.BuildingsComp.GetBuildingUpkeep(milBuilding), 0.001,
                    $"Military building '{milBuilding.defName}' upkeep should be discounted by 10 (base {milBuilding.Upkeep})");

                // Non-military (unflagged) building should not be discounted
                TestAssert.AreEqual(Math.Truncate(civBuilding.Upkeep),
                    settlement.BuildingsComp.GetBuildingUpkeep(civBuilding), 0.001,
                    $"Civilian building '{civBuilding.defName}' upkeep should not be discounted");
            });
        }
        finally
        {
            PolicyTestHelper.RestorePolicies(faction, snapshot);
        }
        DestructiveTestUtil.AssertEmpireInvariants(faction, "Militaristic_BuildingUpkeep_Discount");
    }
}
