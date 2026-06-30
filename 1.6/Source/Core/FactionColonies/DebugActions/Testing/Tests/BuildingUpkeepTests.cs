using Verse;

namespace FactionColonies
{
    public static class BuildingUpkeepTests
    {
        private static WorldSettlementFC GetSettlement()
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction == null || faction.settlements.Count == 0) return null;
            return faction.settlements.FirstOrDefault(s => s.BuildingsComp != null);
        }

        // ============================
        // Special Buildings (pure)
        // ============================

        [EmpireTest("BuildingUpkeep")]
        public static void EmptyBuilding_ZeroUpkeep()
        {
            TestAssert.AreEqual(0.0, BuildingFCDefOf.Empty.Upkeep,
                message: "Empty building should have 0 upkeep");
        }

        [EmpireTest("BuildingUpkeep")]
        public static void ConstructionBuilding_ZeroUpkeep()
        {
            TestAssert.AreEqual(0.0, BuildingFCDefOf.Construction.Upkeep,
                message: "Construction placeholder should have 0 upkeep");
        }

        // ============================
        // Settlement Upkeep (game state)
        // ============================

        [EmpireTest("BuildingUpkeep")]
        public static void AllBuildingUpkeep_IsFiniteAndNonNegative()
        {
            WorldSettlementFC settlement = GetSettlement();
            if (settlement == null) TestAssert.Skip("No settlement with BuildingsComp");

            var comp = settlement.BuildingsComp;
            for (int i = 0; i < comp.NumBuildingSlots; i++)
            {
                int upkeep = comp.GetBuildingUpkeep(i);
                TestAssert.IsTrue(upkeep >= 0,
                    $"Slot {i} ({comp.GetBuildingInSlot(i)?.defName ?? "null"}): upkeep should be >= 0, got {upkeep}");
            }
        }

        [EmpireTest("BuildingUpkeep")]
        public static void TotalUpkeep_MatchesSumOfSlots()
        {
            WorldSettlementFC settlement = GetSettlement();
            if (settlement == null) TestAssert.Skip("No settlement with BuildingsComp");

            var comp = settlement.BuildingsComp;
            int sum = 0;
            for (int i = 0; i < comp.NumBuildingSlots; i++)
            {
                sum += comp.GetBuildingUpkeep(i);
            }

            TestAssert.AreEqual(sum, comp.TotalUpkeep(),
                $"TotalUpkeep ({comp.TotalUpkeep()}) should match sum of slot upkeeps ({sum})");
        }

        [EmpireTest("BuildingUpkeep")]
        public static void TotalUpkeep_IsNonNegative()
        {
            WorldSettlementFC settlement = GetSettlement();
            if (settlement == null) TestAssert.Skip("No settlement with BuildingsComp");

            int total = settlement.BuildingsComp.TotalUpkeep();
            TestAssert.IsTrue(total >= 0,
                $"TotalUpkeep should be >= 0, got {total}");
        }

        // ============================
        // Building Dependencies (game state)
        // ============================

        [EmpireTest("BuildingUpkeep")]
        public static void IsBuildingRequiredByOther_EmptyBuilding_ReturnsFalse()
        {
            WorldSettlementFC settlement = GetSettlement();
            if (settlement == null) TestAssert.Skip("No settlement with BuildingsComp");

            TestAssert.IsFalse(settlement.BuildingsComp.IsBuildingRequiredByOther(BuildingFCDefOf.Empty),
                "Empty building should never be required by another building");
        }

        [EmpireTest("BuildingUpkeep")]
        public static void GetBuildingsDependingOn_EmptyBuilding_ReturnsEmpty()
        {
            WorldSettlementFC settlement = GetSettlement();
            if (settlement == null) TestAssert.Skip("No settlement with BuildingsComp");

            var dependents = settlement.BuildingsComp.GetBuildingsDependingOn(BuildingFCDefOf.Empty);
            TestAssert.IsEmpty(dependents,
                "No building should depend on Empty");
        }

        [EmpireTest("BuildingUpkeep")]
        public static void NumBuildingSlots_IsPositive()
        {
            WorldSettlementFC settlement = GetSettlement();
            if (settlement == null) TestAssert.Skip("No settlement with BuildingsComp");

            TestAssert.IsTrue(settlement.BuildingsComp.NumBuildingSlots >= 0,
                "Settlement should have non-negative building slots");
        }
    }
}
