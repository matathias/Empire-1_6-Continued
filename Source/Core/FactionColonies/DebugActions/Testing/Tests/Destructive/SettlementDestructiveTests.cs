namespace FactionColonies;

/* DESTRUCTIVE: per-settlement sanity checks. Creates a transient settlement when the player has none.
   Morale values forced by the deployment-lockout test are restored; created settlements are not. */
public static class SettlementDestructiveTests
{
    [EmpireDestructiveTest("Destructive.Settlement")]
    public static void Settlement_HappinessGain_IsFiniteNumber()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        double gain = settlement.GetHappinessGain();
        TestAssert.IsFalse(double.IsNaN(gain), "Happiness gain should not be NaN");
        TestAssert.IsFalse(double.IsInfinity(gain), "Happiness gain should not be infinite");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "Settlement_HappinessGain_IsFiniteNumber");
    }

    [EmpireDestructiveTest("Destructive.Settlement")]
    public static void Settlement_LoyaltyGain_IsFiniteNumber()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        double gain = settlement.GetLoyaltyGain();
        TestAssert.IsFalse(double.IsNaN(gain), "Loyalty gain should not be NaN");
        TestAssert.IsFalse(double.IsInfinity(gain), "Loyalty gain should not be infinite");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "Settlement_LoyaltyGain_IsFiniteNumber");
    }

    [EmpireDestructiveTest("Destructive.Settlement")]
    public static void Settlement_TotalUpkeep_IsNonNegative()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        double upkeep = settlement.GetTotalUpkeep();
        TestAssert.IsTrue(upkeep >= 0, $"Total upkeep should be >= 0, got {upkeep}");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "Settlement_TotalUpkeep_IsNonNegative");
    }

    [EmpireDestructiveTest("Destructive.Settlement")]
    public static void Settlement_BuildingSlots_MatchesFormula()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement(s => s.BuildingsComp is not null);

        int actual = settlement.BuildingsComp.NumBuildingSlots;
        int expected = settlement.GetBuildingSlots();
        TestAssert.AreEqual(expected, actual, "NumBuildingSlots should match GetBuildingSlots");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "Settlement_BuildingSlots_MatchesFormula");
    }

    [EmpireDestructiveTest("Destructive.Settlement")]
    public static void Settlement_Happiness_IsClamped()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        TestAssert.IsTrue(settlement.happiness >= 0 && settlement.happiness <= 100,
            $"Happiness should be in [0, 100], got {settlement.happiness}");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "Settlement_Happiness_IsClamped");
    }

    [EmpireDestructiveTest("Destructive.Settlement")]
    public static void Settlement_Loyalty_IsClamped()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        TestAssert.IsTrue(settlement.loyalty >= 0 && settlement.loyalty <= 100,
            $"Loyalty should be in [0, 100], got {settlement.loyalty}");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "Settlement_Loyalty_IsClamped");
    }

    [EmpireDestructiveTest("Destructive.Settlement")]
    public static void Settlement_Prosperity_IsClamped()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        TestAssert.IsTrue(settlement.prosperity >= 0 && settlement.prosperity <= 100,
            $"Prosperity should be in [0, 100], got {settlement.prosperity}");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "Settlement_Prosperity_IsClamped");
    }

    [EmpireDestructiveTest("Destructive.Settlement")]
    public static void Settlement_AllBuildingSlots_MatchGetBuildingSlots()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        DestructiveTestUtil.EnsureSettlement(s => s.BuildingsComp is not null);
        List<WorldSettlementFC> settlements = faction.settlements;

        foreach (var settlement in settlements)
        {
            if (settlement.BuildingsComp is null) continue;
            int actual = settlement.BuildingsComp.NumBuildingSlots;
            int expected = settlement.GetBuildingSlots();
            TestAssert.AreEqual(expected, actual,
                $"{settlement.Name} ({settlement.settlementDef.defName}): NumBuildingSlots should match GetBuildingSlots");
        }
        DestructiveTestUtil.AssertEmpireInvariants(faction, "Settlement_AllBuildingSlots_MatchGetBuildingSlots");
    }

    [EmpireDestructiveTest("Destructive.Settlement")]
    public static void Settlement_SquadDeploymentLockout_FlipsAtThresholds()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        // Snapshot and restore the morale stats so the forced values do not linger.
        double h = settlement.happiness, l = settlement.loyalty, u = settlement.unrest;
        try
        {
            // Healthy morale -> not locked.
            settlement.happiness = 100; settlement.loyalty = 100; settlement.unrest = 0;
            TestAssert.IsFalse(settlement.SquadDeploymentLocked, "Healthy settlement should not be deploy-locked");
            TestAssert.IsFalse(settlement.TryGetSquadDeploymentBlock(out _), "Healthy settlement should report no block");

            // Happiness below floor -> locked with a reason.
            settlement.happiness = WorldSettlementFC.SquadDeployHappinessFloor - 5;
            TestAssert.IsTrue(settlement.SquadDeploymentLocked, "Low happiness should lock deployment");
            TestAssert.IsTrue(settlement.TryGetSquadDeploymentBlock(out string hReason) && hReason is not null,
                "Low happiness should produce a non-null block reason");

            // Restore happiness; loyalty below floor -> locked.
            settlement.happiness = 100;
            settlement.loyalty = WorldSettlementFC.SquadDeployLoyaltyFloor - 5;
            TestAssert.IsTrue(settlement.SquadDeploymentLocked, "Low loyalty should lock deployment");

            // Restore loyalty; unrest above ceiling -> locked.
            settlement.loyalty = 100;
            settlement.unrest = WorldSettlementFC.SquadDeployUnrestCeiling + 5;
            TestAssert.IsTrue(settlement.SquadDeploymentLocked, "High unrest should lock deployment");
        }
        finally
        {
            settlement.happiness = h; settlement.loyalty = l; settlement.unrest = u;
        }
        DestructiveTestUtil.AssertEmpireInvariants(faction, "Settlement_SquadDeploymentLockout_FlipsAtThresholds");
    }
}
