using RimWorld;
using Verse;

namespace FactionColonies
{
    [DefOf]
    public class FCStatDefOf
    {
        /* Military */
        public static FCStatDef militaryBaseLevel;
        public static FCStatDef militaryCombatEfficiency;
        public static FCStatDef militaryLevelBonusDefending;
        public static FCStatDef militaryLevelBonusAttacking;
        public static FCStatDef militaryEfficiencyBonusAttacking;
        public static FCStatDef militaryEfficiencyBonusDefending;
        public static FCStatDef militaryCooldownOffset;
        public static FCStatDef raidCooldownOffset;
        public static FCStatDef mercHealRateMultiplier;
        public static FCStatDef mercenaryDeathChanceMultiplier;
        public static FCStatDef mercenaryCasualtyRateMultiplier;
        public static FCStatDef mercenaryDeathHappinessPenalty;
        public static FCStatDef squadCapPerSettlement;
        public static FCStatDef maxSquadSize;
        public static FCStatDef fireSupportCostMultiplier;
        public static FCStatDef policyActionCooldownMultiplier;
        public static FCStatDef casualtyWoundSeverityMultiplier;

        /* Threat Scaling */
        public static FCStatDef threatScalingBase;
        public static FCStatDef threatScalingMultiplier;
        public static FCStatDef threatAdaptationGrowthMultiplier;

        /* Battle Penalties */
        public static FCStatDef battleProsperityLossMultiplier;
        public static FCStatDef battleHappinessLossMultiplier;
        public static FCStatDef battleLoyaltyLossMultiplier;
        public static FCStatDef buildingDestructionChance;
        public static FCStatDef battleLossProsperityBase;
        public static FCStatDef battleLossHappinessBase;
        public static FCStatDef battleLossLoyaltyBase;
        public static FCStatDef victoryHappinessBonus;
        public static FCStatDef victoryLoyaltyBonus;

        /* Economy */
        public static FCStatDef taxBasePercentage;
        public static FCStatDef taxBaseRandomModifier;
        public static FCStatDef taxBonusFlat;
        public static FCStatDef titheValueMultiplier;
        public static FCStatDef lootMultiplier;
        public static FCStatDef settlementCostMultiplier;
        public static FCStatDef buildTimeMultiplier;
        public static FCStatDef buildingCostBase;
        public static FCStatDef buildingCostBase_Military;
        public static FCStatDef buildingCostBase_Civilian;
        public static FCStatDef buildingCostMultiplier;
        public static FCStatDef buildingCostMultiplier_Military;
        public static FCStatDef buildingCostMultiplier_Civilian;
        public static FCStatDef buildingUpkeepBase;
        public static FCStatDef buildingUpkeepBase_Military;
        public static FCStatDef buildingUpkeepBase_Civilian;
        public static FCStatDef buildingUpkeepMultiplier;
        public static FCStatDef buildingUpkeepMultiplier_Military;
        public static FCStatDef buildingUpkeepMultiplier_Civilian;
        public static FCStatDef createSettlementBaseCost;
        public static FCStatDef createSettlementMultiplier;
        public static FCStatDef researchContributionMultiplier;
        public static FCStatDef settlementUpgradeCostBase;
        public static FCStatDef settlementUpgradeCostMultiplier;
        public static FCStatDef settlementExpansionCostPerSettlement;
        public static FCStatDef buildingSlotsPerLevelBonus;

        /* Workers */
        public static FCStatDef workerBaseCost;
        public static FCStatDef workerBaseMax;
        public static FCStatDef workerBaseOverMax;
        public static FCStatDef extraWorkersSoftcap;
        public static FCStatDef overMaxWorkersAdjustment;
        public static FCStatDef workerOverworkPenaltyMultiplier;
        public static FCStatDef workerProductionBase;
        public static FCStatDef workerProductionMultiplier;

        /* Prosperity */
        public static FCStatDef prosperityGainedBase;
        public static FCStatDef prosperityLostBase;

        /* Happiness (base) */
        public static FCStatDef happinessLostBase;
        public static FCStatDef happinessGainedBase;

        /* Happiness (multipliers) */
        public static FCStatDef happinessLostMultiplier;
        public static FCStatDef happinessGainedMultiplier;

        /* Loyalty (base) */
        public static FCStatDef loyaltyLostBase;
        public static FCStatDef loyaltyGainedBase;

        /* Loyalty (multipliers) */
        public static FCStatDef loyaltyLostMultiplier;
        public static FCStatDef loyaltyGainedMultiplier;

        /* Unrest (base) */
        public static FCStatDef unrestLostBase;
        public static FCStatDef unrestGainedBase;

        /* Unrest (multipliers) */
        public static FCStatDef unrestLostMultiplier;
        public static FCStatDef unrestGainedMultiplier;

        static FCStatDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FCStatDefOf));
        }
    }
}
