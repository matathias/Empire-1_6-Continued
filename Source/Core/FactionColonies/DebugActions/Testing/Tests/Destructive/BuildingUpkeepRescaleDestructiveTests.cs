namespace FactionColonies;

/* DESTRUCTIVE: exact building-upkeep values need an identity upkeep transform, so these tests clear
   policies and traits, revoke every edict and remove faction-wide stat events, and may create a
   transient settlement with no upkeep modifiers. Policies, traits and the difficulty setting are
   restored; edicts, events and created settlements are not. */
public static class BuildingUpkeepRescaleDestructiveTests
{
    /// <summary>
    /// Runs <paramref name="action"/> against a settlement whose upkeep transform is the identity
    /// (base 0, multipliers 1, no policy behaviors, difficulty factor 1), and returns the faction.
    /// The transform is checked with GetBuildingUpkeep itself: a 0-upkeep def must yield 0 and a
    /// 1000-upkeep def 1000, which catches any base offset or non-1 multiplier.
    /// </summary>
    private static FactionFC WithDefaultUpkeepTransform(Action<WorldSettlementFC> action)
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
        try
        {
            WorldSettlementFC settlement = ForcedStateTestUtilA.EnsureDefaultUpkeepSettlement(faction);
            ForcedStateTestUtilA.WithBuildingUpkeepDifficultyMult(1f, () =>
            {
                var comp = settlement.BuildingsComp;
                TestAssert.AreEqual(0, comp.GetBuildingUpkeep(new BuildingFCDef { upkeep = 0, postRework = true }),
                    "Forced upkeep transform should map a 0-upkeep def to 0");
                TestAssert.AreEqual(1000, comp.GetBuildingUpkeep(new BuildingFCDef { upkeep = 1000, postRework = true }),
                    "Forced upkeep transform should map a 1000-upkeep def to 1000");
                action(settlement);
            });
        }
        finally
        {
            PolicyTestHelper.RestorePolicies(faction, snapshot);
        }
        return faction;
    }

    // A postRework def's base term is taken as-is (no /5); a legacy def is divided by LEGACY_UPKEEP_DIVISOR.
    [EmpireDestructiveTest("Destructive.BuildingUpkeep")]
    public static void PostReworkDef_NotDivided_LegacyDef_Divided()
    {
        FactionFC faction = WithDefaultUpkeepTransform(settlement =>
        {
            var modern = new BuildingFCDef { upkeep = 20, postRework = true };
            var legacy = new BuildingFCDef { upkeep = 100, postRework = false };

            int modernUpkeep = settlement.BuildingsComp.GetBuildingUpkeep(modern);
            int legacyUpkeep = settlement.BuildingsComp.GetBuildingUpkeep(legacy);

            // With default stats (base 0, mult 1, difficultyMult 1): modern == 20, legacy == 100/5 == 20.
            TestAssert.AreEqual(20, modernUpkeep, message: "postRework def base term should not be divided");
            TestAssert.AreEqual(20, legacyUpkeep, message: "legacy def base term should be divided by 5");
        });
        DestructiveTestUtil.AssertEmpireInvariants(faction, "PostReworkDef_NotDivided_LegacyDef_Divided");
    }

    // A designed-income building (negative authored upkeep) yields a negative (income) value, not floored to 0.
    [EmpireDestructiveTest("Destructive.BuildingUpkeep")]
    public static void NegativeUpkeepDef_StaysSigned()
    {
        FactionFC faction = WithDefaultUpkeepTransform(settlement =>
        {
            var income = new BuildingFCDef { upkeep = -10, postRework = true };
            int value = settlement.BuildingsComp.GetBuildingUpkeep(income);
            TestAssert.LessThan(value, 0, "designed-income building should return a negative (income) value");
        });
        DestructiveTestUtil.AssertEmpireInvariants(faction, "NegativeUpkeepDef_StaysSigned");
    }
}
