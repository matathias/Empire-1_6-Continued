namespace FactionColonies;

/* DESTRUCTIVE: building upkeep and dependency checks on a live settlement. Creates a transient
   settlement when no settlement has a buildings comp. Not reverted. */
public static class BuildingUpkeepDestructiveTests
{
    // ============================
    // Settlement Upkeep
    // ============================

    [EmpireDestructiveTest("Destructive.BuildingUpkeep")]
    public static void AllBuildingUpkeep_IsSaneMagnitude()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement(s => s.BuildingsComp is not null);

        var comp = settlement.BuildingsComp;
        for (int i = 0; i < comp.NumBuildingSlots; i++)
        {
            int upkeep = comp.GetBuildingUpkeep(i);
            // Negative upkeep is a shipped feature (income-generating buildings, e.g. Neolithic
            // buildings with upkeep -10), so only assert a sane magnitude to catch overflow/garbage.
            TestAssert.IsTrue(System.Math.Abs(upkeep) < 1000000,
                $"Slot {i} ({comp.GetBuildingInSlot(i)?.defName ?? "null"}): upkeep magnitude looks wrong, got {upkeep}");
        }
        DestructiveTestUtil.AssertEmpireInvariants(faction, "AllBuildingUpkeep_IsSaneMagnitude");
    }

    [EmpireDestructiveTest("Destructive.BuildingUpkeep")]
    public static void TotalUpkeep_MatchesSumOfSlots()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement(s => s.BuildingsComp is not null);

        var comp = settlement.BuildingsComp;
        int sum = 0;
        for (int i = 0; i < comp.NumBuildingSlots; i++)
        {
            sum += comp.GetBuildingUpkeep(i);
        }

        TestAssert.AreEqual(sum, comp.TotalUpkeep(),
            $"TotalUpkeep ({comp.TotalUpkeep()}) should match sum of slot upkeeps ({sum})");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "TotalUpkeep_MatchesSumOfSlots");
    }

    [EmpireDestructiveTest("Destructive.BuildingUpkeep")]
    public static void TotalUpkeep_IsNonNegative()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement(s => s.BuildingsComp is not null);

        int total = settlement.BuildingsComp.TotalUpkeep();
        TestAssert.IsTrue(total >= 0,
            $"TotalUpkeep should be >= 0, got {total}");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "TotalUpkeep_IsNonNegative");
    }

    // ============================
    // Building Dependencies
    // ============================

    [EmpireDestructiveTest("Destructive.BuildingUpkeep")]
    public static void IsBuildingRequiredByOther_EmptyBuilding_ReturnsFalse()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement(s => s.BuildingsComp is not null);

        TestAssert.IsFalse(settlement.BuildingsComp.IsBuildingRequiredByOther(BuildingFCDefOf.Empty),
            "Empty building should never be required by another building");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "IsBuildingRequiredByOther_EmptyBuilding_ReturnsFalse");
    }

    [EmpireDestructiveTest("Destructive.BuildingUpkeep")]
    public static void GetBuildingsDependingOn_EmptyBuilding_ReturnsEmpty()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement(s => s.BuildingsComp is not null);

        var dependents = settlement.BuildingsComp.GetBuildingsDependingOn(BuildingFCDefOf.Empty);
        TestAssert.IsEmpty(dependents,
            "No building should depend on Empty");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "GetBuildingsDependingOn_EmptyBuilding_ReturnsEmpty");
    }

    [EmpireDestructiveTest("Destructive.BuildingUpkeep")]
    public static void NumBuildingSlots_IsPositive()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement(s => s.BuildingsComp is not null);

        TestAssert.IsTrue(settlement.BuildingsComp.NumBuildingSlots >= 0,
            "Settlement should have non-negative building slots");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "NumBuildingSlots_IsPositive");
    }
}
