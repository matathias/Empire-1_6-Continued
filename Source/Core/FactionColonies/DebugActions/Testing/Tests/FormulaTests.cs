using FactionColonies.util;

namespace FactionColonies
{
    public static class FormulaTests
    {
        // --- ClampStat ---

        [EmpireTest("Formula")]
        public static void ClampStat_WithinRange_ReturnsSum()
        {
            TestAssert.AreEqual(50.0, SettlementFormulas.ClampStat(48.0, 2.0));
        }

        [EmpireTest("Formula")]
        public static void ClampStat_ExceedsMax_ClampsToMax()
        {
            TestAssert.AreEqual(100.0, SettlementFormulas.ClampStat(99.0, 5.0));
        }

        [EmpireTest("Formula")]
        public static void ClampStat_BelowMin_ClampsToMin()
        {
            TestAssert.AreEqual(0.0, SettlementFormulas.ClampStat(2.0, -5.0));
        }

        [EmpireTest("Formula")]
        public static void ClampStat_RoundsToOneDecimal()
        {
            TestAssert.AreEqual(50.3, SettlementFormulas.ClampStat(50.0, 0.333));
        }

        // --- CalculateWorkerUpkeep ---

        [EmpireTest("Formula")]
        public static void WorkerUpkeep_NoWorkers_ReturnsZero()
        {
            TestAssert.AreEqual(0.0, SettlementFormulas.CalculateWorkerUpkeep(
                workers: 0, workersMax: 10, baseWorkerCost: 100));
        }

        [EmpireTest("Formula")]
        public static void WorkerUpkeep_UnderMax_IsLinear()
        {
            TestAssert.AreEqual(500.0, SettlementFormulas.CalculateWorkerUpkeep(
                workers: 5, workersMax: 10, baseWorkerCost: 100));
        }

        [EmpireTest("Formula")]
        public static void WorkerUpkeep_AtMax_NoPenalty()
        {
            TestAssert.AreEqual(1000.0, SettlementFormulas.CalculateWorkerUpkeep(
                workers: 10, workersMax: 10, baseWorkerCost: 100));
        }

        [EmpireTest("Formula")]
        public static void WorkerUpkeep_OverMax_IncludesOverworkPenalty()
        {
            // base: 12 * 100 = 1200, overWork: 2, penalty: 1200 * (2/20) = 120, total: 1320
            TestAssert.AreEqual(1320.0, SettlementFormulas.CalculateWorkerUpkeep(
                workers: 12, workersMax: 10, baseWorkerCost: 100));
        }

        [EmpireTest("Formula")]
        public static void WorkerUpkeep_LargeOverwork_DoublesTheCost()
        {
            // base: 30 * 100 = 3000, overWork: 20, penalty: 3000 * (20/20) = 3000, total: 6000
            TestAssert.AreEqual(6000.0, SettlementFormulas.CalculateWorkerUpkeep(
                workers: 30, workersMax: 10, baseWorkerCost: 100));
        }

        // --- CalculateBuildingSlots ---

        [EmpireTest("Formula")]
        public static void BuildingSlots_Level0_Returns3()
        {
            TestAssert.AreEqual(3, SettlementFormulas.CalculateBuildingSlots(0, 10, 3, 0.5f));
        }

        [EmpireTest("Formula")]
        public static void BuildingSlots_Level4_Returns5()
        {
            TestAssert.AreEqual(5, SettlementFormulas.CalculateBuildingSlots(4, 10, 3, 0.5f));
        }

        [EmpireTest("Formula")]
        public static void BuildingSlots_Level10_Returns8()
        {
            TestAssert.AreEqual(8, SettlementFormulas.CalculateBuildingSlots(10, 10, 3, 0.5f));
        }

        [EmpireTest("Formula")]
        public static void BuildingSlots_CappedByMaxCount()
        {
            TestAssert.AreEqual(4, SettlementFormulas.CalculateBuildingSlots(10, 4, 3, 0.5f));
        }

        [EmpireTest("Formula")]
        public static void BuildingSlots_ZeroBase_Level0_Returns0()
        {
            TestAssert.AreEqual(0, SettlementFormulas.CalculateBuildingSlots(0, 10, 0, 0.5f));
        }

        [EmpireTest("Formula")]
        public static void BuildingSlots_ZeroPerLevel_AlwaysReturnsBase()
        {
            TestAssert.AreEqual(2, SettlementFormulas.CalculateBuildingSlots(10, 10, 2, 0f));
        }

        [EmpireTest("Formula")]
        public static void BuildingSlots_CustomProgression()
        {
            // base 1 + floor(1.0 * 3) = 4
            TestAssert.AreEqual(4, SettlementFormulas.CalculateBuildingSlots(3, 10, 1, 1f));
        }

        // --- CalculateLevelForSlot ---

        [EmpireTest("Formula")]
        public static void LevelForSlot_BaseSlot_Returns0()
        {
            TestAssert.AreEqual(0, SettlementFormulas.CalculateLevelForSlot(0, 3, 0.5f));
        }

        [EmpireTest("Formula")]
        public static void LevelForSlot_FirstLocked_Returns2()
        {
            TestAssert.AreEqual(2, SettlementFormulas.CalculateLevelForSlot(3, 3, 0.5f));
        }

        [EmpireTest("Formula")]
        public static void LevelForSlot_ZeroPerLevel_ReturnsNeg1()
        {
            TestAssert.AreEqual(-1, SettlementFormulas.CalculateLevelForSlot(3, 3, 0f));
        }

        // --- CalculateBattleLossPenalties ---

        [EmpireTest("Formula")]
        public static void BattleLoss_BaseValues()
        {
            var (prosperity, happiness, loyalty) = SettlementFormulas.CalculateBattleLossPenalties(happinessLostMultiplier: 1.0, loyaltyLostMultiplier: 1.0);
            TestAssert.AreEqual(20.0, prosperity);
            TestAssert.AreEqual(25.0, happiness);
            TestAssert.AreEqual(15.0, loyalty);
        }

        [EmpireTest("Formula")]
        public static void BattleLoss_WithMultipliers_ScalesHappinessAndLoyalty()
        {
            var (prosperity, happiness, loyalty) = SettlementFormulas.CalculateBattleLossPenalties(happinessLostMultiplier: 2.0, loyaltyLostMultiplier: 1.5);
            TestAssert.AreEqual(20.0, prosperity);
            TestAssert.AreEqual(50.0, happiness); // 25 * 2.0
            TestAssert.AreEqual(22.5, loyalty);    // 15 * 1.5
        }

        // --- CalculateProsperityDrift ---

        [EmpireTest("Formula")]
        public static void ProsperityDrift_FarAboveTarget_AcceleratesNegative()
        {
            // distance 80 -> 80/5 = 16, floor 1, cap 80; above target -> negative
            TestAssert.AreEqual(-16.0, SettlementFormulas.CalculateProsperityDrift(100, 20, 1, 5));
        }

        [EmpireTest("Formula")]
        public static void ProsperityDrift_FarBelowTarget_AcceleratesPositive()
        {
            // distance 80 -> 16, below target -> positive
            TestAssert.AreEqual(16.0, SettlementFormulas.CalculateProsperityDrift(20, 100, 1, 5));
        }

        [EmpireTest("Formula")]
        public static void ProsperityDrift_NearTarget_FlooredAtOne()
        {
            // distance 1 -> 0.2 floored to 1, below target -> +1
            TestAssert.AreEqual(1.0, SettlementFormulas.CalculateProsperityDrift(99, 100, 1, 5));
        }

        [EmpireTest("Formula")]
        public static void ProsperityDrift_WithinFloor_CappedAtDistance_NoOvershoot()
        {
            // distance 0.4 -> floor 1 capped to distance 0.4; above target -> -0.4
            TestAssert.AreEqual(-0.4, SettlementFormulas.CalculateProsperityDrift(100.4, 100, 1, 5));
        }

        [EmpireTest("Formula")]
        public static void ProsperityDrift_AtTarget_IsZero()
        {
            TestAssert.AreEqual(0.0, SettlementFormulas.CalculateProsperityDrift(50, 50, 1, 5));
        }

        // --- CalculateUpgradeCost ---

        [EmpireTest("Formula")]
        public static void UpgradeCost_Level0_ReturnsBaseCost()
        {
            TestAssert.AreEqual(1000, SettlementFormulas.CalculateUpgradeCost(0, 1000));
        }

        [EmpireTest("Formula")]
        public static void UpgradeCost_Level3_AddsLevelScaling()
        {
            TestAssert.AreEqual(4000, SettlementFormulas.CalculateUpgradeCost(3, 1000));
        }

        // --- CalculateUpgradeTime ---

        [EmpireTest("Formula")]
        public static void UpgradeTime_Level0_ReturnsBaseTime()
        {
            // (0 + 1) * 60000 * 2 * 1.0 = 120000
            TestAssert.AreEqual(120000, SettlementFormulas.CalculateUpgradeTime(0, 1.0));
        }

        [EmpireTest("Formula")]
        public static void UpgradeTime_Level2_ScalesWithLevel()
        {
            // (2 + 1) * 60000 * 2 * 1.0 = 360000
            TestAssert.AreEqual(360000, SettlementFormulas.CalculateUpgradeTime(2, 1.0));
        }

        [EmpireTest("Formula")]
        public static void UpgradeTime_WithMultiplier_ScalesTime()
        {
            // (1 + 1) * 60000 * 2 * 0.5 = 120000
            TestAssert.AreEqual(120000, SettlementFormulas.CalculateUpgradeTime(1, 0.5));
        }
    }
}
