using System;

namespace FactionColonies.util
{
    /// <summary>
    /// Pure calculation methods for settlement stats and economy.
    /// These methods have zero RimWorld dependencies, making them unit-testable.
    /// </summary>
    public static class SettlementFormulas
    {
        /// <summary>
        /// Clamps a stat value after applying a change, rounding to 1 decimal place.
        /// </summary>
        public static double ClampStat(double current, double change, double min = 0, double max = 100)
        {
            return Math.Round(Math.Clamp(current + change, min, max), 1);
        }

        /// <summary>
        /// Calculates worker upkeep including overwork penalty.
        /// Overwork occurs when workers exceed workersMax, adding a penalty of (overwork / 20) * base upkeep.
        /// <paramref name="overworkPenaltyMult"/> (workerOverworkPenaltyMultiplier stat) scales that penalty.
        /// </summary>
        public static double CalculateWorkerUpkeep(double workers, double workersMax, double baseWorkerCost, double overworkPenaltyMult = 1)
        {
            double overWork = workers > workersMax ? (int)(workers - workersMax) : 0;
            return (workers * baseWorkerCost) + ((workers * baseWorkerCost) * (overWork / 20) * overworkPenaltyMult);
        }

        /// <summary>
        /// Calculates the number of building slots available at a given settlement level.
        /// </summary>
        public static int CalculateBuildingSlots(int settlementLevel, int maxBuildingCount, int baseSlots, float perLevelSlots)
        {
            return Math.Min(baseSlots + (int)Math.Floor(perLevelSlots * settlementLevel), maxBuildingCount);
        }

        /// <summary>
        /// Returns the minimum settlement level required to unlock a given building slot index.
        /// Returns 0 if the slot is available at level 0, or -1 if the slot can never be unlocked via leveling.
        /// </summary>
        public static int CalculateLevelForSlot(int slotIndex, int baseSlots, float perLevelSlots)
        {
            int needed = slotIndex - baseSlots + 1;
            if (needed <= 0) return 0;
            if (perLevelSlots <= 0f) return -1;
            return (int)Math.Ceiling(needed / (double)perLevelSlots);
        }

        /// <summary>
        /// Calculates the XP goal for the next faction level.
        /// </summary>
        public static float CalculateFactionLevelGoalXP(int currentLevel)
        {
            return 100 + (currentLevel * 150);
        }

        /// <summary>
        /// Calculates base stat penalties when a settlement loses a battle.
        /// Policy-specific modifiers (e.g. feudal, resilient) are applied via FCStatDef stats.
        /// </summary>
        public static (double prosperity, double happiness, double loyalty) CalculateBattleLossPenalties(
            double happinessLostMultiplier, double loyaltyLostMultiplier,
            double prosperityBase = 0, double happinessBase = 0, double loyaltyBase = 0)
        {
            return (20 + prosperityBase, (25 + happinessBase) * happinessLostMultiplier, (15 + loyaltyBase) * loyaltyLostMultiplier);
        }

        /// <summary>
        /// Signed daily prosperity drift toward <paramref name="target"/>. Magnitude scales with distance
        /// (1 extra point per <paramref name="driftStep"/> points of distance), floored at
        /// <paramref name="driftFloor"/> and capped at the distance so it never overshoots.
        /// Positive when below target, negative when above, 0 when equal.
        /// </summary>
        public static double CalculateProsperityDrift(double prosperity, double target, double driftFloor, double driftStep)
        {
            double distance = Math.Abs(prosperity - target);
            double magnitude = Math.Min(Math.Max(distance / driftStep, driftFloor), distance);
            if (prosperity < target) return magnitude;
            if (prosperity > target) return -magnitude;
            return 0;
        }

        /// <summary>
        /// Base happiness/loyalty reward granted to the winning squad's home settlement
        /// after an Overwhelming Victory.
        /// </summary>
        public static (double happiness, double loyalty) CalculateBattleVictoryRewards()
        {
            return (5, 3);
        }

        /// <summary>
        /// Calculates the silver cost to upgrade a settlement to the next level.
        /// </summary>
        public static int CalculateUpgradeCost(int settlementLevel, int baseUpgradeCost)
        {
            return baseUpgradeCost + (settlementLevel * 1000);
        }

        /// <summary>
        /// Calculates the duration in ticks for a settlement upgrade to complete.
        /// </summary>
        public static int CalculateUpgradeTime(int settlementLevel, double buildTimeMultiplier)
        {
            return (int)((settlementLevel + 1) * 60000 * 2 * buildTimeMultiplier);
        }
    }
}
