using System;
using System.Linq;
using Verse;

namespace FactionColonies
{
    public static class SettlementTests
    {
        private static WorldSettlementFC GetFirstSettlement()
        {
            var settlements = FindFC.Settlements;
            if (settlements == null || settlements.Count == 0)
                return null;
            return settlements.First();
        }

        [EmpireTest("Settlement")]
        public static void Settlement_HappinessGain_IsFiniteNumber()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlements");

            double gain = settlement.GetHappinessGain();
            TestAssert.IsFalse(double.IsNaN(gain), "Happiness gain should not be NaN");
            TestAssert.IsFalse(double.IsInfinity(gain), "Happiness gain should not be infinite");
        }

        [EmpireTest("Settlement")]
        public static void Settlement_LoyaltyGain_IsFiniteNumber()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlements");

            double gain = settlement.GetLoyaltyGain();
            TestAssert.IsFalse(double.IsNaN(gain), "Loyalty gain should not be NaN");
            TestAssert.IsFalse(double.IsInfinity(gain), "Loyalty gain should not be infinite");
        }

        [EmpireTest("Settlement")]
        public static void Settlement_TotalUpkeep_IsNonNegative()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlements");

            double upkeep = settlement.GetTotalUpkeep();
            TestAssert.IsTrue(upkeep >= 0, $"Total upkeep should be >= 0, got {upkeep}");
        }

        [EmpireTest("Settlement")]
        public static void Settlement_BuildingSlots_MatchesFormula()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlements");
            if (settlement.BuildingsComp == null) TestAssert.Skip("No BuildingsComp");

            int actual = settlement.BuildingsComp.NumBuildingSlots;
            int expected = settlement.GetBuildingSlots();
            TestAssert.AreEqual(expected, actual, "NumBuildingSlots should match GetBuildingSlots");
        }

        [EmpireTest("Settlement")]
        public static void Settlement_Happiness_IsClamped()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlements");

            TestAssert.IsTrue(settlement.happiness >= 0 && settlement.happiness <= 100,
                $"Happiness should be in [0, 100], got {settlement.happiness}");
        }

        [EmpireTest("Settlement")]
        public static void Settlement_Loyalty_IsClamped()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlements");

            TestAssert.IsTrue(settlement.loyalty >= 0 && settlement.loyalty <= 100,
                $"Loyalty should be in [0, 100], got {settlement.loyalty}");
        }

        [EmpireTest("Settlement")]
        public static void Settlement_Prosperity_IsClamped()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlements");

            TestAssert.IsTrue(settlement.prosperity >= 0 && settlement.prosperity <= 100,
                $"Prosperity should be in [0, 100], got {settlement.prosperity}");
        }

        [EmpireTest("Settlement")]
        public static void Settlement_AllBuildingSlots_MatchGetBuildingSlots()
        {
            var settlements = FindFC.Settlements;
            if (settlements == null || settlements.Count == 0) TestAssert.Skip("No settlements");

            foreach (var settlement in settlements)
            {
                if (settlement.BuildingsComp == null) continue;
                int actual = settlement.BuildingsComp.NumBuildingSlots;
                int expected = settlement.GetBuildingSlots();
                TestAssert.AreEqual(expected, actual,
                    $"{settlement.Name} ({settlement.settlementDef.defName}): NumBuildingSlots should match GetBuildingSlots");
            }
        }

        [EmpireTest("Settlement")]
        public static void Settlement_SquadDeploymentLockout_FlipsAtThresholds()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlements");

            // Snapshot and restore the morale stats so this test leaves no side effects.
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
                TestAssert.IsTrue(settlement.TryGetSquadDeploymentBlock(out string hReason) && hReason != null,
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
        }

        [EmpireTest("Settlement")]
        public static void Settlement_AllDefs_BuildingSlotsNonDecreasingByLevel()
        {
            foreach (WorldSettlementDef def in DefDatabase<WorldSettlementDef>.AllDefsListForReading)
            {
                var ext = def.GetSettlementTypeExtension();
                int prev = 0;
                int maxLevel = Math.Min(10, def.maxSettlementLevel);
                for (int level = 0; level <= maxLevel; level++)
                {
                    int slots = ext.GetBuildingSlots(level, def.maxBuildingCount);
                    TestAssert.IsTrue(slots >= prev,
                        $"{def.defName}: building slots decreased from {prev} at level {level - 1} to {slots} at level {level}");
                    TestAssert.IsTrue(slots <= def.maxBuildingCount,
                        $"{def.defName}: building slots {slots} exceeds maxBuildingCount {def.maxBuildingCount} at level {level}");
                    prev = slots;
                }
            }
        }
    }
}
