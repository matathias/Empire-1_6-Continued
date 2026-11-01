using FactionColonies.util;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class FCSettings : ModSettings
    {

        /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-* 
         *           ~  DEFAULTS  ~
         * for saving, reseting, and validation
         * Centralized for ease of editing, and to ensure that all references to these values
         *   are synced.
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
        /* Defaults by difficulty setting */
        public const int MINIMUM_TAX_INTERVAL_DAYS = 1;
        public const EmpireDifficultyLevel DEFAULT_DIFFICULTY_LEVEL = EmpireDifficultyLevel.AdventureStory;
        //Peaceful
        public const int DEFAULT_SILVER_PER_RESOURCE_PEACEFUL = 100;          // was 200, interval 2
        public const int DEFAULT_TAX_INTERVAL_DAYS_PEACEFUL = 2;
        public const int DEFAULT_PRODUCTION_TITHE_MOD_PEACEFUL = 25;          // was 50, interval 2
        public const int DEFAULT_WORKER_COST_PEACEFUL = 38;                   // was 75, interval 2 (75/2)
        public const float DEFAULT_WORKER_PROD_BASE_BONUS_PEACEFUL = 1.0f;
        public const float DEFAULT_WORKER_PROD_MULT_BONUS_PEACEFUL = 1.5f;
        //Community Builder
        public const int DEFAULT_SILVER_PER_RESOURCE_COMMUNITYBUILDER = 30;   // was 150, interval 5
        public const int DEFAULT_TAX_INTERVAL_DAYS_COMMUNITYBUILDER = 5;
        public const int DEFAULT_PRODUCTION_TITHE_MOD_COMMUNITYBUILDER = 5;   // was 25, interval 5
        public const int DEFAULT_WORKER_COST_COMMUNITYBUILDER = 20;           // was 100, interval 5
        public const float DEFAULT_WORKER_PROD_BASE_BONUS_COMMUNITYBUILDER = 0.5f;
        public const float DEFAULT_WORKER_PROD_MULT_BONUS_COMMUNITYBUILDER = 1.2f;
        //Adventure Story
        public const int DEFAULT_SILVER_PER_RESOURCE_ADVENTURESTORY = 20;     // was 100, interval 5
        public const int DEFAULT_TAX_INTERVAL_DAYS_ADVENTURESTORY = 5;
        public const int DEFAULT_PRODUCTION_TITHE_MOD_ADVENTURESTORY = 5;     // was 25, interval 5
        public const int DEFAULT_WORKER_COST_ADVENTURESTORY = 20;             // was 100, interval 5
        public const float DEFAULT_WORKER_PROD_BASE_BONUS_ADVENTURESTORY = 0.0f;
        public const float DEFAULT_WORKER_PROD_MULT_BONUS_ADVENTURESTORY = 1.0f;
        //Strive to Survive
        public const int DEFAULT_SILVER_PER_RESOURCE_STRIVETOSURVIVE = 10;    // was 100, interval 10
        public const int DEFAULT_TAX_INTERVAL_DAYS_STRIVETOSURVIVE = 10;
        public const int DEFAULT_PRODUCTION_TITHE_MOD_STRIVETOSURVIVE = 2;    // was 20, interval 10
        public const int DEFAULT_WORKER_COST_STRIVETOSURVIVE = 13;            // was 125, interval 10 (125/10)
        public const float DEFAULT_WORKER_PROD_BASE_BONUS_STRIVETOSURVIVE = 0.0f;
        public const float DEFAULT_WORKER_PROD_MULT_BONUS_STRIVETOSURVIVE = 0.9f;
        //Blood and Dust
        public const int DEFAULT_SILVER_PER_RESOURCE_BLOODANDDUST = 5;        // was 80, interval 15
        public const int DEFAULT_TAX_INTERVAL_DAYS_BLOODANDDUST = 15;
        public const int DEFAULT_PRODUCTION_TITHE_MOD_BLOODANDDUST = 1;       // was 15, interval 15
        public const int DEFAULT_WORKER_COST_BLOODANDDUST = 8;                // was 125, interval 15 (125/15)
        public const float DEFAULT_WORKER_PROD_BASE_BONUS_BLOODANDDUST = 0.0f;
        public const float DEFAULT_WORKER_PROD_MULT_BONUS_BLOODANDDUST = 0.75f;
        //Losing is Fun
        public const int DEFAULT_SILVER_PER_RESOURCE_LOSINGISFUN = 2;         // was 70, interval 30
        public const int DEFAULT_TAX_INTERVAL_DAYS_LOSINGISFUN = 30;
        public const int DEFAULT_PRODUCTION_TITHE_MOD_LOSINGISFUN = 1;        // was 10, interval 30 -> clamp to 1
        public const int DEFAULT_WORKER_COST_LOSINGISFUN = 5;                 // was 150, interval 30
        public const float DEFAULT_WORKER_PROD_BASE_BONUS_LOSINGISFUN = 0.0f;
        public const float DEFAULT_WORKER_PROD_MULT_BONUS_LOSINGISFUN = 0.5f;
        // Global defaults
        // The default difficulty setting is Adventure Story, so set the global defaults accordingly
        public const int DEFAULT_SILVER_PER_RESOURCE = DEFAULT_SILVER_PER_RESOURCE_ADVENTURESTORY;
        public const int DEFAULT_TAX_INTERVAL_DAYS = DEFAULT_TAX_INTERVAL_DAYS_ADVENTURESTORY;
        public const int DEFAULT_PRODUCTION_TITHE_MOD = DEFAULT_PRODUCTION_TITHE_MOD_ADVENTURESTORY;
        public const int DEFAULT_WORKER_COST = DEFAULT_WORKER_COST_ADVENTURESTORY;
        public const float DEFAULT_WORKER_PROD_BASE_BONUS = DEFAULT_WORKER_PROD_BASE_BONUS_ADVENTURESTORY; // 0.0f
        public const float DEFAULT_WORKER_PROD_MULT_BONUS = DEFAULT_WORKER_PROD_MULT_BONUS_ADVENTURESTORY; // 1.0f
        /* Legacy/external BuildingFCDef upkeep is authored at the old per-cycle scale; divide by the
         * default interval to approximate a per-day value. See postRework on BuildingFCDef. */
        public const int LEGACY_UPKEEP_DIVISOR = DEFAULT_TAX_INTERVAL_DAYS; // 5
        /* Defaults for Research settings */
        public const bool DEFAULT_MEDIEVAL_TECH_ONLY = false;
        public const bool DEFAULT_MIRROR_PLAYER_TECH_LEVEL = false;
        /* Defaults for Settlement settings */
        public const bool DEFAULT_SHOW_SETTLE_CONFIRM = true;
        public const TaxDeliveryMode DEFAULT_TAX_DELIVERY_MODE = TaxDeliveryMode.None;
        public const TaxNotificationMode DEFAULT_TAX_NOTIFICATION_MODE = TaxNotificationMode.All;
        public static double DEFAULT_SETTLEMENT_FOUNDING_COST = 1000;
        public static double DEFAULT_SETTLEMENT_BASE_UPGRADE_COST = 1000;
        public static int DEFAULT_SETTLEMENT_MAX_LEVEL = 10;
        /* Timer-duration multipliers */
        public const float DEFAULT_SETTLEMENT_UPGRADE_TIME_MULTIPLIER = 1.0f;
        public const float DEFAULT_BUILDING_CONSTRUCT_TIME_MULTIPLIER = 1.0f;
        /* Hire-Laborers action tuning */
        public const int DEFAULT_LABORER_DURATION_DAYS = 4;
        public const int DEFAULT_LABORER_COOLDOWN_DAYS = 7;
        public const int DEFAULT_LABORER_BASE_COUNT = 2;
        public const int DEFAULT_LABORER_PER_SETTLEMENT = 1;
        public const int DEFAULT_LABORER_MAX_COUNT = 8;
        public const int DEFAULT_LABORER_COST_PER_DAY = 60;
        public const int DEFAULT_LABORER_SKILL_BONUS_PER_LEVEL = 1;
        public const int DEFAULT_LABORER_SKILL_BONUS_CAP = 8;
        /* Defaults for Events & Military settings */
        public const bool DEFAULT_ENABLE_SETTLEMENT_CAPTURE = true;
        public const bool DEFAULT_DISABLE_HOSTILE_MILITARY_ACTIONS = false;
        public const bool DEFAULT_DISABLE_RANDOM_EVENTS = false;
        public const bool DEFAULT_DISABLE_EVENTS_WITH_OPTIONS = false;
        public const bool DEFAULT_BLOCK_EVENTS_DURING_CHAIN = false;
        public const float DEFAULT_EVENT_OPTION_DELAY_SECONDS = 1.0f;
        public const float DEFAULT_EVENT_SILVER_COST_MULTIPLIER = 1.0f;
        public const bool DEFAULT_USE_THREADED_ROAD_COMPUTATION = true;
        public const int DEFAULT_EDGES_PER_ROAD_TICK = 5;
        public const int DEFAULT_ROAD_BUILD_INTERVAL_DAYS = 3;
        public const BattleMode DEFAULT_BATTLE_MODE = BattleMode.Auto;
        public const bool DEFAULT_MANUAL_OFFENSE_BATTLE = false;
        public const int DEFAULT_MIN_DAYS_TIL_MILITARY_ACTION = 4;
        public const int DEFAULT_MAX_DAYS_TIL_MILITARY_ACTION = 10;
        public const int DEFAULT_MIN_DAYS_TIL_RANDOM_EVENT = 2;
        public const int DEFAULT_MAX_DAYS_TIL_RANDOM_EVENT = 8;
        public const float DEFAULT_MAX_THREAT_MULTIPLIER = 3.0f;
        public const float DEFAULT_DEFENDER_ADVANTAGE = 1.15f;
        public const float DEFAULT_EFFICIENCY_DAMPING = 0.5f;
        // Extra military levels the player can add to NPC forces, and the forceRemaining->raid-points multiplier.
        public const int DEFAULT_EXTRA_NPC_DEFENSIVE_LEVELS = 0;
        public const int DEFAULT_EXTRA_NPC_OFFENSIVE_LEVELS = 0;
        public const float DEFAULT_RAID_POINTS_MULTIPLIER = 175f;
        public const bool DEFAULT_ANTI_EXPLOIT = true;
        public const bool DEFAULT_RESTRICT_DEFENSE_MAP_LOOT = true;
        public const int DEFAULT_MAX_CONCURRENT_BATTLE_MAPS = 3;
        // Manual-defense battle map sizing. Final edge length =
        // clamp(baseSize + settlementLevel * perLevelStep, minSize, maxSize).
        public const int DEFAULT_DEFENSE_MAP_BASE_SIZE = 110;
        public const int DEFAULT_DEFENSE_MAP_PER_LEVEL_STEP = 10;
        public const int DEFAULT_DEFENSE_MAP_MIN_SIZE = 120;
        public const int DEFAULT_DEFENSE_MAP_MAX_SIZE = 250;
        // Off-map healing / repair rates and Biotech/psycast merc cost multipliers (Military & Compat tabs).
        public const float DEFAULT_MERCENARY_HEAL_RATE_PER_HOUR = 1f;
        public const float DEFAULT_MILITARY_MECH_REPAIR_RATE = 4f;
        public const double DEFAULT_MILITARY_PSYLINK_COST_MULTIPLIER = 1.0;
        public const double DEFAULT_MILITARY_PSYCAST_COST_MULTIPLIER = 1.0;
        public const double DEFAULT_MILITARY_MECH_COST_MULTIPLIER = 1.0;
        public const double DEFAULT_MILITARY_MECHLINK_COST = 1000.0;
        /* Default for debug/verbose logging */
        public const bool DEFAULT_PRINT_DEBUG = false;
        // Vanilla Psycasts Expanded per-point silver costs (compat tab; scaled by militaryPsycastCostMultiplier).
        public const int DEFAULT_VPE_PSYCAST_BASE_COST = 300;
        public const int DEFAULT_VPE_PSYCAST_PER_LEVEL_COST = 300;
        public const int DEFAULT_VPE_FOCUS_COST = 300;
        public const int DEFAULT_VPE_STAT_POINT_COST = 250;
        // Advanced-tab settlement tuning knobs (unrest/loyalty/happiness daily drift, prosperity drift, research base).
        public const double DEFAULT_UNREST_BASE_GAIN = 0;
        public const double DEFAULT_UNREST_BASE_LOST = 1;
        public const double DEFAULT_LOYALTY_BASE_GAIN = 1;
        public const double DEFAULT_LOYALTY_BASE_LOST = 0;
        public const double DEFAULT_HAPPINESS_BASE_GAIN = 1;
        public const double DEFAULT_HAPPINESS_BASE_LOST = 0;
        public const double DEFAULT_PROSPERITY_DRIFT_RATE = 1;
        public const double DEFAULT_PROSPERITY_DRIFT_STEP = 5;
        public const int DEFAULT_PRODUCTION_RESEARCH_BASE = 100;
        /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *           ~  DEFAULTS END ~
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/

        public const int updateUiTimer = 150; // UI update interval in ticks

        public static int silverPerResource = DEFAULT_SILVER_PER_RESOURCE;
        public static double silverToCreateSettlement = DEFAULT_SETTLEMENT_FOUNDING_COST;

        private static int timeBetweenTaxes_days = DEFAULT_TAX_INTERVAL_DAYS;
        public static int timeBetweenTaxes => Math.Max(1, timeBetweenTaxes_days) * GenDate.TicksPerDay;


        public static int productionTitheMod = DEFAULT_PRODUCTION_TITHE_MOD;
        public static int workerCost = DEFAULT_WORKER_COST;
        /* Per-difficulty adjustments to per-worker resource production: an additive bonus to the
         * production base and a multiplier on the production mult. Applied worker-count-independently
         * (before the engine's x assignedWorkers), so the per-worker breakdown stays comparable. */
        public static float workerProductionBaseBonus = DEFAULT_WORKER_PROD_BASE_BONUS;
        public static float workerProductionMultBonus = DEFAULT_WORKER_PROD_MULT_BONUS;
        /* Final per-difficulty multiplier on building upkeep, tuning the per-difficulty building burden
         * independently of the interval-scaling. Ratio (sign-preserving). Defaults to 1.0 everywhere. */
        public static float buildingUpkeepDifficultyMult = 1.0f;

        /* Hire-Laborers action tuning (all player-configurable). */
        public static int laborerDurationDays = DEFAULT_LABORER_DURATION_DAYS;
        public static int laborerCooldownDays = DEFAULT_LABORER_COOLDOWN_DAYS;
        public static int laborerBaseCount = DEFAULT_LABORER_BASE_COUNT;
        public static int laborerPerSettlement = DEFAULT_LABORER_PER_SETTLEMENT;
        public static int laborerMaxCount = DEFAULT_LABORER_MAX_COUNT;
        public static int laborerCostPerDay = DEFAULT_LABORER_COST_PER_DAY;
        public static int laborerSkillBonusPerLevel = DEFAULT_LABORER_SKILL_BONUS_PER_LEVEL;
        public static int laborerSkillBonusCap = DEFAULT_LABORER_SKILL_BONUS_CAP;

        public static EmpireDifficultyLevel difficultyLevel = DEFAULT_DIFFICULTY_LEVEL;

        public static double settlementBaseUpgradeCost = DEFAULT_SETTLEMENT_BASE_UPGRADE_COST;
        public static int settlementMaxLevel = DEFAULT_SETTLEMENT_MAX_LEVEL;

        public static float settlementUpgradeTimeMultiplier = DEFAULT_SETTLEMENT_UPGRADE_TIME_MULTIPLIER;
        public static float buildingConstructTimeMultiplier = DEFAULT_BUILDING_CONSTRUCT_TIME_MULTIPLIER;

        public static bool showSettleConfirm = DEFAULT_SHOW_SETTLE_CONFIRM;
        public static bool medievalTechOnly = DEFAULT_MEDIEVAL_TECH_ONLY;
        public static bool mirrorPlayerTechLevel = DEFAULT_MIRROR_PLAYER_TECH_LEVEL;
        public static bool enableSettlementCapture = DEFAULT_ENABLE_SETTLEMENT_CAPTURE;
        public static bool disableHostileMilitaryActions = DEFAULT_DISABLE_HOSTILE_MILITARY_ACTIONS;
        public static bool antiExploit = DEFAULT_ANTI_EXPLOIT;
        public static bool restrictDefenseMapLoot = DEFAULT_RESTRICT_DEFENSE_MAP_LOOT;
        public static bool disableRandomEvents = DEFAULT_DISABLE_RANDOM_EVENTS;
        public static bool disableEventsWithOptions = DEFAULT_DISABLE_EVENTS_WITH_OPTIONS;
        public static bool blockEventsDuringChain = DEFAULT_BLOCK_EVENTS_DURING_CHAIN;
        public static float eventOptionDelaySeconds = DEFAULT_EVENT_OPTION_DELAY_SECONDS;
        public static float eventSilverCostMultiplier = DEFAULT_EVENT_SILVER_COST_MULTIPLIER;
        public static bool useThreadedRoadComputation = DEFAULT_USE_THREADED_ROAD_COMPUTATION;
        public static int edgesPerRoadTick = DEFAULT_EDGES_PER_ROAD_TICK;
        public static int roadBuildIntervalDays = DEFAULT_ROAD_BUILD_INTERVAL_DAYS;
        public static BattleMode battleMode = DEFAULT_BATTLE_MODE;
        // When true, offensive ops (raid/capture/enslave) can be fought on the target enemy
        // settlement's real map instead of auto-resolving. Default false = zero behavior change
        // on upgrade; the player opts in. Shares maxConcurrentBattleMaps with manual defense.
        public static bool manualOffenseBattle = DEFAULT_MANUAL_OFFENSE_BATTLE;
        public static TaxDeliveryMode forcedTaxDeliveryMode = DEFAULT_TAX_DELIVERY_MODE;
        public static TaxNotificationMode taxNotificationMode = DEFAULT_TAX_NOTIFICATION_MODE;

        public static int minDaysTillMilitaryAction = DEFAULT_MIN_DAYS_TIL_MILITARY_ACTION;
        public static int maxDaysTillMilitaryAction = DEFAULT_MAX_DAYS_TIL_MILITARY_ACTION;
        public static IntRange minMaxDaysTillMilitaryAction = new IntRange(minDaysTillMilitaryAction, maxDaysTillMilitaryAction);

        public static int minDaysTillRandomEvent = DEFAULT_MIN_DAYS_TIL_RANDOM_EVENT;
        public static int maxDaysTillRandomEvent = DEFAULT_MAX_DAYS_TIL_RANDOM_EVENT;
        public static IntRange minMaxDaysTillRandomEvent = new IntRange(minDaysTillRandomEvent, maxDaysTillRandomEvent);

        /* Settlement tuning knobs, exposed on the Advanced settings tab. */
        public static double unrestBaseGain = DEFAULT_UNREST_BASE_GAIN;
        public static double unrestBaseLost = DEFAULT_UNREST_BASE_LOST;
        public static double loyaltyBaseGain = DEFAULT_LOYALTY_BASE_GAIN;
        public static double loyaltyBaseLost = DEFAULT_LOYALTY_BASE_LOST;
        public static double happinessBaseGain = DEFAULT_HAPPINESS_BASE_GAIN;
        public static double happinessBaseLost = DEFAULT_HAPPINESS_BASE_LOST;
        public static double prosperityDriftRate = DEFAULT_PROSPERITY_DRIFT_RATE;   // drift floor (minimum points/day)
        public static double prosperityDriftStep = DEFAULT_PROSPERITY_DRIFT_STEP;    // distance points per +1 drift/day
        public static int productionResearchBase = DEFAULT_PRODUCTION_RESEARCH_BASE;
        public static double militaryAnimalCostMultiplier = 1.5;
        public static double militaryRaceCostMultiplier = 0.075;
        public static double militaryPsylinkCostMultiplier = DEFAULT_MILITARY_PSYLINK_COST_MULTIPLIER;
        public static double militaryPsycastCostMultiplier = DEFAULT_MILITARY_PSYCAST_COST_MULTIPLIER;
        // Mechanitor merc costs (Biotech). Per-mech market-value multiplier + a flat surcharge for the
        // mechlink itself. Default mechlink cost ~ the in-game Mechlink item market value (1000).
        public static double militaryMechCostMultiplier = DEFAULT_MILITARY_MECH_COST_MULTIPLIER;
        public static double militaryMechlinkCost = DEFAULT_MILITARY_MECHLINK_COST;
        // Vanilla Psycasts Expanded point-purchase costs (configured in the Compatibility settings tab).
        public static int vpePsycastBaseCost = DEFAULT_VPE_PSYCAST_BASE_COST;
        public static int vpePsycastPerLevelCost = DEFAULT_VPE_PSYCAST_PER_LEVEL_COST;
        public static int vpeFocusCost = DEFAULT_VPE_FOCUS_COST;
        public static int vpeStatPointCost = DEFAULT_VPE_STAT_POINT_COST;
        public static float mercenaryHealRatePerHour = DEFAULT_MERCENARY_HEAL_RATE_PER_HOUR;
        // HP repaired per hourly heal tick for off-map mechs (alternate to the merc heal path,
        // which doesn't apply to mechanoids). Drives the number of 1-HP MechRepairUtility.RepairTick
        // calls made per tick.
        public static float militaryMechRepairRate = DEFAULT_MILITARY_MECH_REPAIR_RATE;

        public static float maxThreatMultiplier = DEFAULT_MAX_THREAT_MULTIPLIER;
        public static float defenderAdvantage = DEFAULT_DEFENDER_ADVANTAGE;
        public static float efficiencyDamping = DEFAULT_EFFICIENCY_DAMPING;

        // Extra levels added to NPC settlements when the player attacks them.
        public static int extraNPCDefensiveLevels = DEFAULT_EXTRA_NPC_DEFENSIVE_LEVELS;
        // Extra levels added to NPC raids targeting the player (biases faction selection and boosts the raid force).
        public static int extraNPCOffensiveLevels = DEFAULT_EXTRA_NPC_OFFENSIVE_LEVELS;
        // Multiplier converting a force's forceRemaining into vanilla raid points.
        public static float raidPointsMultiplier = DEFAULT_RAID_POINTS_MULTIPLIER;

        /* Squad hiring economy. squadHireCostMultiplier scales the up-front silver paid when
         * hiring a squad from a template (1.0 = template's full equipment cost; 0.0 = free).
         * squadUpgradeCostMultiplier scales the diff paid to bring an existing hired squad's
         * loadout up to its template's current cost. */
        public const float DEFAULT_SQUAD_HIRE_COST_MULTIPLIER = 1.0f;
        public const float DEFAULT_SQUAD_UPGRADE_COST_MULTIPLIER = 1.0f;
        public const int DEFAULT_MAX_SQUAD_SIZE = 30;
        public static float squadHireCostMultiplier = DEFAULT_SQUAD_HIRE_COST_MULTIPLIER;
        public static float squadUpgradeCostMultiplier = DEFAULT_SQUAD_UPGRADE_COST_MULTIPLIER;
        public static int maxSquadSize = DEFAULT_MAX_SQUAD_SIZE;

        /* Max companion animals a single merc design can carry (MilUnitFC.AddAnimal hard-blocks past
         * this). Slider runs 1..MAX_ANIMAL_SUBPAWNS_SLIDER; lowering it never shrinks existing designs. */
        public const int DEFAULT_MAX_ANIMAL_SUBPAWNS = 10;
        public const int MIN_ANIMAL_SUBPAWNS = 1;
        public const int MAX_ANIMAL_SUBPAWNS_SLIDER = 25;
        public static int maxAnimalSubpawns = DEFAULT_MAX_ANIMAL_SUBPAWNS;

        /* Gene valuation weights. Used by GeneValuationUtil to score a xenotype's genes
         * into a cost multiplier applied to a mercenary's base race cost. Weights are
         * applied at read time over cached unweighted components, so changing these
         * sliders is free (no cache invalidation needed). No hard cap on the final factor. */
        public const float DEFAULT_GENE_W_MVF       = 0.00f;  // marketValueFactor (disabled by default)
        public const float DEFAULT_GENE_W_MET       = 0.02f;  // metabolism
        public const float DEFAULT_GENE_W_ARC       = 0.30f;  // archites
        public const float DEFAULT_GENE_W_EFFECTS   = 0.75f;  // stat bonuses
        public const float DEFAULT_GENE_W_PAIN      = 0.20f;  // pain bonus
        public const float DEFAULT_GENE_W_DMGRESIST = 0.30f;  // damage resist
        public static float geneValueWeightMvf       = DEFAULT_GENE_W_MVF;
        public static float geneValueWeightMet       = DEFAULT_GENE_W_MET;
        public static float geneValueWeightArc       = DEFAULT_GENE_W_ARC;
        public static float geneValueWeightEffects   = DEFAULT_GENE_W_EFFECTS;
        public static float geneValueWeightPain      = DEFAULT_GENE_W_PAIN;
        public static float geneValueWeightDmgResist = DEFAULT_GENE_W_DMGRESIST;

        /* Optional hard cap on the xenotype cost factor. Defaults to unlimited so OP modded
         * xenotypes scale freely. The cap (when enabled) clamps the final 1+sum factor;
         * minimum meaningful value is 1.0 (no bonus). */
        public const bool DEFAULT_GENE_FACTOR_UNLIMITED = true;
        public const float DEFAULT_GENE_MAX_FACTOR = 5.0f;
        public const float MIN_GENE_MAX_FACTOR = 1.0f;
        public const float MAX_GENE_MAX_FACTOR = 100.0f;
        public static bool geneValueFactorUnlimited = DEFAULT_GENE_FACTOR_UNLIMITED;
        public static float geneValueMaxFactor = DEFAULT_GENE_MAX_FACTOR;

        /* Squad deployment economy. squadDeploymentCostPercentage is the fraction of a
         * squad's current equipment value billed in silver each time it is deployed
         * (offensive op or call-in to a player map). Defensive ops are free. The charge
         * is created as a BillFC against the squad's home settlement, due after
         * deploymentBillLifespan_days days; an unpaid bill incurs the standard bill
         * penalties below. */
        public const float DEFAULT_SQUAD_DEPLOYMENT_COST_PERCENTAGE = 0.20f;
        public const int DEFAULT_DEPLOYMENT_BILL_LIFESPAN_DAYS = 5;
        public static float squadDeploymentCostPercentage = DEFAULT_SQUAD_DEPLOYMENT_COST_PERCENTAGE;
        public static int deploymentBillLifespan_days = DEFAULT_DEPLOYMENT_BILL_LIFESPAN_DAYS;

        /* Squad restock economy. After a MANUAL battle, carried inventory (ammo / meds / drugs) a
         * surviving merc actually consumed is diffed against its loadout design, refilled, and billed
         * as a restock BillFC (reusing deploymentBillLifespan_days). squadRestockCostPercentage is the
         * fraction of that consumed market value charged: 1.0 = full replacement value, 0 = free.
         * Auto-resolved battles never touch inventory and so incur no restock charge. */
        public const float DEFAULT_SQUAD_RESTOCK_COST_PERCENTAGE = 1.0f;
        public static float squadRestockCostPercentage = DEFAULT_SQUAD_RESTOCK_COST_PERCENTAGE;

        /// <summary>Max simultaneous manual battle maps across all settlements. 0 = unlimited.</summary>
        public static int maxConcurrentBattleMaps = DEFAULT_MAX_CONCURRENT_BATTLE_MAPS;

        /// <summary>Manual-defense battle map size controls. See WorldSettlementFC.DefenseMapSizeFor.</summary>
        public static int defenseMapBaseSize = DEFAULT_DEFENSE_MAP_BASE_SIZE;
        public static int defenseMapPerLevelStep = DEFAULT_DEFENSE_MAP_PER_LEVEL_STEP;
        public static int defenseMapMinSize = DEFAULT_DEFENSE_MAP_MIN_SIZE;
        public static int defenseMapMaxSize = DEFAULT_DEFENSE_MAP_MAX_SIZE;

        /* Auto-resolve battle pacing. Auto-resolved battles roll one round per
         * autoResolveTicksPerRound ticks (default: 1 in-game hour). The flow:
         *   T+0      Preparing (no roll)
         *   T+1h     flip to Engaged (no roll)
         *   T+2h+    one round per hour until completion */
        public const int DEFAULT_AUTO_RESOLVE_TICKS_PER_ROUND = GenDate.TicksPerHour; // 2500
        public static int autoResolveTicksPerRound = DEFAULT_AUTO_RESOLVE_TICKS_PER_ROUND;

        /* Auto-resolve casualty translation: the abstract force decrement from an
         * auto-resolved battle is converted into real hediffs/deaths on the deployed
         * squad pawns. Deaths only fire when the casualty rate exceeds the threshold;
         * the per-casualty death roll then ramps linearly to maxDeathFraction at 100%.
         * Crushing Defeat (100% rate, the losing side wiped) follows the same ramp,
         * which lands at ~80% deaths with defaults. */
        public const float DEFAULT_AUTO_RESOLVE_CASUALTY_DEATH_THRESHOLD = 0.75f;
        public const float DEFAULT_AUTO_RESOLVE_CASUALTY_MAX_DEATH_FRACTION = 0.50f;
        public const bool DEFAULT_APPLY_AUTO_RESOLVE_INJURIES = true;
        public const bool DEFAULT_RESPECT_LETHAL_DAMAGE_THRESHOLD = true;
        public static float autoResolveCasualtyDeathThreshold = DEFAULT_AUTO_RESOLVE_CASUALTY_DEATH_THRESHOLD;
        public static float autoResolveCasualtyMaxDeathFraction = DEFAULT_AUTO_RESOLVE_CASUALTY_MAX_DEATH_FRACTION;
        public static bool applyAutoResolveInjuries = DEFAULT_APPLY_AUTO_RESOLVE_INJURIES;

        /* When true, BattleCasualtyApplicator's injury path clamps cumulative Hediff_Injury
         * severity below vanilla's lethal-damage threshold (150 * HealthScale) so the injury
         * path can't accidentally tip a pawn over into a vanilla auto-kill. Disable when running
         * mods (e.g. Death Rattle) that remove or relax vanilla's check and you want the abstract
         * damage to flow through unmediated. */
        public static bool respectLethalDamageThreshold = DEFAULT_RESPECT_LETHAL_DAMAGE_THRESHOLD;

        /* Crushing Defeat consequences. A "Crushing Defeat" is any battle the empire loses
         * without inflicting a single casualty on the winning side — the mirror of
         * Overwhelming Victory. Settlement-defense penalties (prosperity / happiness /
         * loyalty / building destruction) are multiplied by crushingDefeatPenaltyMultiplier. */
        public const float DEFAULT_CRUSHING_DEFEAT_PENALTY_MULTIPLIER = 2.0f;
        public static float crushingDefeatPenaltyMultiplier = DEFAULT_CRUSHING_DEFEAT_PENALTY_MULTIPLIER;

        /* Overwhelming Victory rewards. The mirror of Crushing Defeat: any battle the
         * empire wins without taking a single casualty on the winning side grants the
         * winning squad's home settlement a small happiness/loyalty bonus, scaled by
         * overwhelmingVictoryRewardMultiplier. Set to 0 to disable. External
         * IAutoDefender wins skip the reward (no empire home settlement to credit).
         * Base reward magnitudes live in SettlementFormulas.CalculateBattleVictoryRewards. */
        public const float DEFAULT_OVERWHELMING_VICTORY_REWARD_MULTIPLIER = 1.0f;
        public static float overwhelmingVictoryRewardMultiplier = DEFAULT_OVERWHELMING_VICTORY_REWARD_MULTIPLIER;

        /* Battle archive cap. The world-level archive (WorldComponent_Archive) keeps
         * the N most recent battle reports for the player to review via the military
         * tab. Letters that reference an evicted report fall back to a "no longer
         * available" toast on click. */
        public const int DEFAULT_BATTLE_ARCHIVE_MAX_ENTRIES = 50;
        public const int MIN_BATTLE_ARCHIVE_MAX_ENTRIES = 1;
        public const int MAX_BATTLE_ARCHIVE_MAX_ENTRIES = 500;
        public static int battleArchiveMaxEntries = DEFAULT_BATTLE_ARCHIVE_MAX_ENTRIES;

        /* When true, the battle archive keeps every report and never evicts. The
         * battleArchiveMaxEntries cap is ignored. May grow the save file over a long
         * playthrough. */
        public const bool DEFAULT_BATTLE_ARCHIVE_UNLIMITED = false;
        public static bool battleArchiveUnlimited = DEFAULT_BATTLE_ARCHIVE_UNLIMITED;

        public static int maxPolicyCount = 2;

        /* Flag for debug/verbose logging. */
        private static bool printDebug = DEFAULT_PRINT_DEBUG;
        public static bool PrintDebug => printDebug;

        // Window size settings
        public static float buildingWindowWidth = 800f;
        public static float buildingWindowHeight = 600f;

        // Patch notes version tracking — per-mod dictionary of "major.minor.patch" strings
        public static Dictionary<string, string> lastSeenVersions = new Dictionary<string, string>();

        // Settings-format version stamp (independent of the patch-notes lastSeenVersions above).
        // Semantic mod version that last wrote the settings file. Re-stamped on every save. Null pre-stamp.
        public static string settingsModVersion;

        // The running mod's version, set by FactionColoniesMod's constructor from ModMetaData.ModVersion.
        // Reliable source for the save-time stamp (GetModVersion() is unreliable during settings load). Not scribed.
        public static string activeModVersion;

        // The value read from the settings file on load, before re-stamping. Migration reads this. Not scribed.
        private static string loadedSettingsVersion;
        public static string LoadedSettingsVersion => loadedSettingsVersion;

        // Patch notes auto-open threshold
        public const PatchNoteType DEFAULT_PATCH_NOTE_AUTO_OPEN_THRESHOLD = PatchNoteType.Undefined;
        public static PatchNoteType patchNoteAutoOpenThreshold = DEFAULT_PATCH_NOTE_AUTO_OPEN_THRESHOLD;

        // Saved color picker colors (persisted across sessions)
        public static List<Color> savedPickerColors = new List<Color>();
        public const int MaxSavedPickerColors = 24;

        // Per-event disable list (defName strings)
        public static HashSet<string> disabledEventDefs = new HashSet<string>();
        public static bool IsEventDisabled(string defName) => disabledEventDefs.Contains(defName);

        // Settings tab state
        private static int settingsTab = 0;
        private static List<TabRecord> settingsTabs = new List<TabRecord>();

        public override void ExposeData()
        {
            base.ExposeData();

            // Settings-format version stamp. Re-stamp to the active version on save (using the reliable
            // constructor-captured value, since GetModVersion() is unreliable during settings load); capture
            // the previously-stored value on load so MigrateSettingsFormat() can detect a version change.
            if (Scribe.mode == LoadSaveMode.Saving) settingsModVersion = activeModVersion;
            Scribe_Values.Look(ref settingsModVersion, "settingsModVersion", null);
            if (Scribe.mode == LoadSaveMode.LoadingVars) loadedSettingsVersion = settingsModVersion;

            Scribe_Values.Look(ref silverPerResource, "silverPerResource", DEFAULT_SILVER_PER_RESOURCE);
            Scribe_Values.Look(ref timeBetweenTaxes_days, "timeBetweenTaxes_days", DEFAULT_TAX_INTERVAL_DAYS);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (timeBetweenTaxes_days < 1)
                {
                    LogUtil.Warning($"Loaded suspicious timeBetweenTaxes_days={timeBetweenTaxes_days} from settings; resetting to DEFAULT_TAX_INTERVAL_DAYS ({DEFAULT_TAX_INTERVAL_DAYS}).");
                    timeBetweenTaxes_days = DEFAULT_TAX_INTERVAL_DAYS;
                }
                else
                {
                    LogUtil.Message($"Loaded timeBetweenTaxes_days={timeBetweenTaxes_days} from settings.");
                }
            }
            Scribe_Values.Look(ref productionTitheMod, "productionTitheMod", DEFAULT_PRODUCTION_TITHE_MOD);
            Scribe_Values.Look(ref workerCost, "workerCost", DEFAULT_WORKER_COST);
            Scribe_Values.Look(ref workerProductionBaseBonus, "workerProductionBaseBonus", DEFAULT_WORKER_PROD_BASE_BONUS);
            Scribe_Values.Look(ref workerProductionMultBonus, "workerProductionMultBonus", DEFAULT_WORKER_PROD_MULT_BONUS);
            Scribe_Values.Look(ref buildingUpkeepDifficultyMult, "buildingUpkeepDifficultyMult", 1.0f);
            Scribe_Values.Look(ref settlementMaxLevel, "settlementMaxLevel", DEFAULT_SETTLEMENT_MAX_LEVEL);
            Scribe_Values.Look(ref settlementUpgradeTimeMultiplier, "settlementUpgradeTimeMultiplier", DEFAULT_SETTLEMENT_UPGRADE_TIME_MULTIPLIER);
            Scribe_Values.Look(ref buildingConstructTimeMultiplier, "buildingConstructTimeMultiplier", DEFAULT_BUILDING_CONSTRUCT_TIME_MULTIPLIER);
            Scribe_Values.Look(ref laborerDurationDays, "laborerDurationDays", DEFAULT_LABORER_DURATION_DAYS);
            Scribe_Values.Look(ref laborerCooldownDays, "laborerCooldownDays", DEFAULT_LABORER_COOLDOWN_DAYS);
            Scribe_Values.Look(ref laborerBaseCount, "laborerBaseCount", DEFAULT_LABORER_BASE_COUNT);
            Scribe_Values.Look(ref laborerPerSettlement, "laborerPerSettlement", DEFAULT_LABORER_PER_SETTLEMENT);
            Scribe_Values.Look(ref laborerMaxCount, "laborerMaxCount", DEFAULT_LABORER_MAX_COUNT);
            Scribe_Values.Look(ref laborerCostPerDay, "laborerCostPerDay", DEFAULT_LABORER_COST_PER_DAY);
            Scribe_Values.Look(ref laborerSkillBonusPerLevel, "laborerSkillBonusPerLevel", DEFAULT_LABORER_SKILL_BONUS_PER_LEVEL);
            Scribe_Values.Look(ref laborerSkillBonusCap, "laborerSkillBonusCap", DEFAULT_LABORER_SKILL_BONUS_CAP);
            Scribe_Values.Look(ref showSettleConfirm, "showSettleConfirm", DEFAULT_SHOW_SETTLE_CONFIRM);
            Scribe_Values.Look(ref medievalTechOnly, "medievalTechOnly", DEFAULT_MEDIEVAL_TECH_ONLY);
            Scribe_Values.Look(ref mirrorPlayerTechLevel, "mirrorPlayerTechLevel", DEFAULT_MIRROR_PLAYER_TECH_LEVEL);
            Scribe_Values.Look(ref enableSettlementCapture, "enableSettlementCapture", DEFAULT_ENABLE_SETTLEMENT_CAPTURE);
            Scribe_Values.Look(ref disableHostileMilitaryActions, "disableHostileMilitaryActions", DEFAULT_DISABLE_HOSTILE_MILITARY_ACTIONS);
            Scribe_Values.Look(ref antiExploit, "antiExploit", DEFAULT_ANTI_EXPLOIT);
            Scribe_Values.Look(ref restrictDefenseMapLoot, "restrictDefenseMapLoot", DEFAULT_RESTRICT_DEFENSE_MAP_LOOT);
            Scribe_Values.Look(ref disableRandomEvents, "disableRandomEvents", DEFAULT_DISABLE_RANDOM_EVENTS);
            Scribe_Values.Look(ref disableEventsWithOptions, "disableEventsWithOptions", DEFAULT_DISABLE_EVENTS_WITH_OPTIONS);
            Scribe_Values.Look(ref blockEventsDuringChain, "blockEventsDuringChain", DEFAULT_BLOCK_EVENTS_DURING_CHAIN);
            Scribe_Values.Look(ref eventOptionDelaySeconds, "eventOptionDelaySeconds", DEFAULT_EVENT_OPTION_DELAY_SECONDS);
            Scribe_Values.Look(ref eventSilverCostMultiplier, "eventSilverCostMultiplier", DEFAULT_EVENT_SILVER_COST_MULTIPLIER);
            Scribe_Values.Look(ref forcedTaxDeliveryMode, "forcedTaxDeliveryMode", DEFAULT_TAX_DELIVERY_MODE);
            Scribe_Values.Look(ref taxNotificationMode, "taxNotificationMode", DEFAULT_TAX_NOTIFICATION_MODE);
            Scribe_Values.Look(ref useThreadedRoadComputation, "useThreadedRoadComputation", DEFAULT_USE_THREADED_ROAD_COMPUTATION);
            Scribe_Values.Look(ref edgesPerRoadTick, "edgesPerRoadTick", DEFAULT_EDGES_PER_ROAD_TICK);
            Scribe_Values.Look(ref roadBuildIntervalDays, "roadBuildIntervalDays", DEFAULT_ROAD_BUILD_INTERVAL_DAYS);
            if (Scribe.mode == LoadSaveMode.LoadingVars && roadBuildIntervalDays < 1)
            {
                LogUtil.Warning($"Loaded suspicious roadBuildIntervalDays={roadBuildIntervalDays} from settings; resetting to DEFAULT_ROAD_BUILD_INTERVAL_DAYS ({DEFAULT_ROAD_BUILD_INTERVAL_DAYS}).");
                roadBuildIntervalDays = DEFAULT_ROAD_BUILD_INTERVAL_DAYS;
            }
            Scribe_Values.Look(ref battleMode, "battleMode", DEFAULT_BATTLE_MODE);
            Scribe_Values.Look(ref manualOffenseBattle, "manualOffenseBattle", DEFAULT_MANUAL_OFFENSE_BATTLE);
            Scribe_Values.Look(ref minDaysTillMilitaryAction, "minDaysTillMilitaryAction", DEFAULT_MIN_DAYS_TIL_MILITARY_ACTION);
            Scribe_Values.Look(ref maxDaysTillMilitaryAction, "maxDaysTillMilitaryAction", DEFAULT_MAX_DAYS_TIL_MILITARY_ACTION);
            Scribe_Values.Look(ref minDaysTillRandomEvent, "minDaysTillRandomEvent", DEFAULT_MIN_DAYS_TIL_RANDOM_EVENT);
            Scribe_Values.Look(ref maxDaysTillRandomEvent, "maxDaysTillRandomEvent", DEFAULT_MAX_DAYS_TIL_RANDOM_EVENT);
            Scribe_Values.Look(ref buildingWindowWidth, "buildingWindowWidth", 800f);
            Scribe_Values.Look(ref buildingWindowHeight, "buildingWindowHeight", 600f);
            Scribe_Values.Look(ref difficultyLevel, "difficultyLevel", DEFAULT_DIFFICULTY_LEVEL);
            Scribe_Values.Look(ref printDebug, "printDebug", DEFAULT_PRINT_DEBUG);
            Scribe_Values.Look(ref maxThreatMultiplier, "maxThreatMultiplier", DEFAULT_MAX_THREAT_MULTIPLIER);
            Scribe_Values.Look(ref defenderAdvantage, "defenderAdvantage", DEFAULT_DEFENDER_ADVANTAGE);
            Scribe_Values.Look(ref efficiencyDamping, "efficiencyDamping", DEFAULT_EFFICIENCY_DAMPING);
            Scribe_Values.Look(ref extraNPCDefensiveLevels, "extraNPCDefensiveLevels", DEFAULT_EXTRA_NPC_DEFENSIVE_LEVELS);
            Scribe_Values.Look(ref extraNPCOffensiveLevels, "extraNPCOffensiveLevels", DEFAULT_EXTRA_NPC_OFFENSIVE_LEVELS);
            Scribe_Values.Look(ref raidPointsMultiplier, "raidPointsMultiplier", DEFAULT_RAID_POINTS_MULTIPLIER);
            Scribe_Values.Look(ref maxConcurrentBattleMaps, "maxConcurrentBattleMaps", DEFAULT_MAX_CONCURRENT_BATTLE_MAPS);
            Scribe_Values.Look(ref defenseMapBaseSize, "defenseMapBaseSize", DEFAULT_DEFENSE_MAP_BASE_SIZE);
            Scribe_Values.Look(ref defenseMapPerLevelStep, "defenseMapPerLevelStep", DEFAULT_DEFENSE_MAP_PER_LEVEL_STEP);
            Scribe_Values.Look(ref defenseMapMinSize, "defenseMapMinSize", DEFAULT_DEFENSE_MAP_MIN_SIZE);
            Scribe_Values.Look(ref defenseMapMaxSize, "defenseMapMaxSize", DEFAULT_DEFENSE_MAP_MAX_SIZE);
            Scribe_Values.Look(ref autoResolveTicksPerRound, "autoResolveTicksPerRound", DEFAULT_AUTO_RESOLVE_TICKS_PER_ROUND);
            Scribe_Values.Look(ref autoResolveCasualtyDeathThreshold, "autoResolveCasualtyDeathThreshold", DEFAULT_AUTO_RESOLVE_CASUALTY_DEATH_THRESHOLD);
            Scribe_Values.Look(ref autoResolveCasualtyMaxDeathFraction, "autoResolveCasualtyMaxDeathFraction", DEFAULT_AUTO_RESOLVE_CASUALTY_MAX_DEATH_FRACTION);
            Scribe_Values.Look(ref applyAutoResolveInjuries, "applyAutoResolveInjuries", DEFAULT_APPLY_AUTO_RESOLVE_INJURIES);
            Scribe_Values.Look(ref crushingDefeatPenaltyMultiplier, "crushingDefeatPenaltyMultiplier", DEFAULT_CRUSHING_DEFEAT_PENALTY_MULTIPLIER);
            Scribe_Values.Look(ref overwhelmingVictoryRewardMultiplier, "overwhelmingVictoryRewardMultiplier", DEFAULT_OVERWHELMING_VICTORY_REWARD_MULTIPLIER);
            Scribe_Values.Look(ref respectLethalDamageThreshold, "respectLethalDamageThreshold", DEFAULT_RESPECT_LETHAL_DAMAGE_THRESHOLD);
            Scribe_Values.Look(ref battleArchiveMaxEntries, "battleArchiveMaxEntries", DEFAULT_BATTLE_ARCHIVE_MAX_ENTRIES);
            if (Scribe.mode == LoadSaveMode.LoadingVars
                && (battleArchiveMaxEntries < MIN_BATTLE_ARCHIVE_MAX_ENTRIES
                    || battleArchiveMaxEntries > MAX_BATTLE_ARCHIVE_MAX_ENTRIES))
            {
                LogUtil.Warning($"Loaded out-of-range battleArchiveMaxEntries={battleArchiveMaxEntries}; resetting to {DEFAULT_BATTLE_ARCHIVE_MAX_ENTRIES}.");
                battleArchiveMaxEntries = DEFAULT_BATTLE_ARCHIVE_MAX_ENTRIES;
            }
            Scribe_Values.Look(ref battleArchiveUnlimited, "battleArchiveUnlimited", DEFAULT_BATTLE_ARCHIVE_UNLIMITED);
            Scribe_Values.Look(ref mercenaryHealRatePerHour, "mercenaryHealRatePerHour", DEFAULT_MERCENARY_HEAL_RATE_PER_HOUR);
            Scribe_Values.Look(ref militaryMechRepairRate, "militaryMechRepairRate", DEFAULT_MILITARY_MECH_REPAIR_RATE);
            Scribe_Values.Look(ref militaryPsylinkCostMultiplier, "militaryPsylinkCostMultiplier", DEFAULT_MILITARY_PSYLINK_COST_MULTIPLIER);
            Scribe_Values.Look(ref militaryPsycastCostMultiplier, "militaryPsycastCostMultiplier", DEFAULT_MILITARY_PSYCAST_COST_MULTIPLIER);
            Scribe_Values.Look(ref militaryMechCostMultiplier, "militaryMechCostMultiplier", DEFAULT_MILITARY_MECH_COST_MULTIPLIER);
            Scribe_Values.Look(ref militaryMechlinkCost, "militaryMechlinkCost", DEFAULT_MILITARY_MECHLINK_COST);
            Scribe_Values.Look(ref vpePsycastBaseCost, "vpePsycastBaseCost", DEFAULT_VPE_PSYCAST_BASE_COST);
            Scribe_Values.Look(ref vpePsycastPerLevelCost, "vpePsycastPerLevelCost", DEFAULT_VPE_PSYCAST_PER_LEVEL_COST);
            Scribe_Values.Look(ref vpeFocusCost, "vpeFocusCost", DEFAULT_VPE_FOCUS_COST);
            Scribe_Values.Look(ref vpeStatPointCost, "vpeStatPointCost", DEFAULT_VPE_STAT_POINT_COST);
            Scribe_Values.Look(ref squadHireCostMultiplier, "squadHireCostMultiplier", DEFAULT_SQUAD_HIRE_COST_MULTIPLIER);
            Scribe_Values.Look(ref squadUpgradeCostMultiplier, "squadUpgradeCostMultiplier", DEFAULT_SQUAD_UPGRADE_COST_MULTIPLIER);
            Scribe_Values.Look(ref maxSquadSize, "maxSquadSize", DEFAULT_MAX_SQUAD_SIZE);
            Scribe_Values.Look(ref maxAnimalSubpawns, "maxAnimalSubpawns", DEFAULT_MAX_ANIMAL_SUBPAWNS);
            Scribe_Values.Look(ref geneValueWeightMvf,       "geneValueWeightMvf",       DEFAULT_GENE_W_MVF);
            Scribe_Values.Look(ref geneValueWeightMet,       "geneValueWeightMet",       DEFAULT_GENE_W_MET);
            Scribe_Values.Look(ref geneValueWeightArc,       "geneValueWeightArc",       DEFAULT_GENE_W_ARC);
            Scribe_Values.Look(ref geneValueWeightEffects,   "geneValueWeightEffects",   DEFAULT_GENE_W_EFFECTS);
            Scribe_Values.Look(ref geneValueWeightPain,      "geneValueWeightPain",      DEFAULT_GENE_W_PAIN);
            Scribe_Values.Look(ref geneValueWeightDmgResist, "geneValueWeightDmgResist", DEFAULT_GENE_W_DMGRESIST);
            Scribe_Values.Look(ref geneValueFactorUnlimited, "geneValueFactorUnlimited", DEFAULT_GENE_FACTOR_UNLIMITED);
            Scribe_Values.Look(ref geneValueMaxFactor,       "geneValueMaxFactor",       DEFAULT_GENE_MAX_FACTOR);
            Scribe_Values.Look(ref squadDeploymentCostPercentage, "squadDeploymentCostPercentage", DEFAULT_SQUAD_DEPLOYMENT_COST_PERCENTAGE);
            Scribe_Values.Look(ref deploymentBillLifespan_days, "deploymentBillLifespan_days", DEFAULT_DEPLOYMENT_BILL_LIFESPAN_DAYS);
            Scribe_Values.Look(ref squadRestockCostPercentage, "squadRestockCostPercentage", DEFAULT_SQUAD_RESTOCK_COST_PERCENTAGE);
            if (Scribe.mode == LoadSaveMode.LoadingVars && maxSquadSize < 1)
            {
                LogUtil.Warning($"Loaded suspicious maxSquadSize={maxSquadSize}; resetting to {DEFAULT_MAX_SQUAD_SIZE}.");
                maxSquadSize = DEFAULT_MAX_SQUAD_SIZE;
            }
            if (Scribe.mode == LoadSaveMode.LoadingVars && maxAnimalSubpawns < MIN_ANIMAL_SUBPAWNS)
            {
                LogUtil.Warning($"Loaded suspicious maxAnimalSubpawns={maxAnimalSubpawns}; resetting to {DEFAULT_MAX_ANIMAL_SUBPAWNS}.");
                maxAnimalSubpawns = DEFAULT_MAX_ANIMAL_SUBPAWNS;
            }
            Scribe_Values.Look(ref unrestBaseGain, "unrestBaseGain", DEFAULT_UNREST_BASE_GAIN);
            Scribe_Values.Look(ref unrestBaseLost, "unrestBaseLost", DEFAULT_UNREST_BASE_LOST);
            Scribe_Values.Look(ref loyaltyBaseGain, "loyaltyBaseGain", DEFAULT_LOYALTY_BASE_GAIN);
            Scribe_Values.Look(ref loyaltyBaseLost, "loyaltyBaseLost", DEFAULT_LOYALTY_BASE_LOST);
            Scribe_Values.Look(ref happinessBaseGain, "happinessBaseGain", DEFAULT_HAPPINESS_BASE_GAIN);
            Scribe_Values.Look(ref happinessBaseLost, "happinessBaseLost", DEFAULT_HAPPINESS_BASE_LOST);
            Scribe_Values.Look(ref prosperityDriftRate, "prosperityDriftRate", DEFAULT_PROSPERITY_DRIFT_RATE);
            Scribe_Values.Look(ref prosperityDriftStep, "prosperityDriftStep", DEFAULT_PROSPERITY_DRIFT_STEP);
            Scribe_Values.Look(ref productionResearchBase, "productionResearchBase", DEFAULT_PRODUCTION_RESEARCH_BASE);
            Scribe_Collections.Look(ref lastSeenVersions, "lastSeenVersions", LookMode.Value, LookMode.Value);
            if (lastSeenVersions is null) lastSeenVersions = new Dictionary<string, string>();
            Scribe_Values.Look(ref patchNoteAutoOpenThreshold, "patchNoteAutoOpenThreshold", DEFAULT_PATCH_NOTE_AUTO_OPEN_THRESHOLD);
            Scribe_Collections.Look(ref disabledEventDefs, "disabledEventDefs", LookMode.Value);
            if (disabledEventDefs is null) disabledEventDefs = new HashSet<string>();

            Scribe_Collections.Look(ref savedPickerColors, "savedPickerColors", LookMode.Value);
            if (savedPickerColors is null) savedPickerColors = new List<Color>();

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                /* Re-construct the intranges */
                minMaxDaysTillMilitaryAction = new IntRange(minDaysTillMilitaryAction, maxDaysTillMilitaryAction);
                minMaxDaysTillRandomEvent = new IntRange(minDaysTillRandomEvent, maxDaysTillRandomEvent);

                // Migrate old single-mod lastSeenVersion fields
                int oldMajor = 0, oldMinor = 0, oldPatch = 0;
                Scribe_Values.Look(ref oldMajor, "lastSeenVersionMajor", -1);
                Scribe_Values.Look(ref oldMinor, "lastSeenVersionMinor", -1);
                Scribe_Values.Look(ref oldPatch, "lastSeenVersionPatch", -1);
                if (oldMajor >= 0 && !lastSeenVersions.ContainsKey("matathias.empire"))
                {
                    SetLastSeenVersion("matathias.empire", oldMajor, oldMinor, oldPatch);
                }
            }
        }

        public static string GetModVersion()
        {
            var mod = LoadedModManager.GetMod<FactionColoniesMod>();
            string version = mod?.Content?.ModMetaData?.ModVersion;
            return version.NullOrEmpty() ? "Unknown" : version;
        }

        public static void GetLastSeenVersion(string modId, out int major, out int minor, out int patch)
        {
            if (lastSeenVersions.TryGetValue(modId, out string v))
            {
                string[] parts = v.Split('.');
                if (parts.Length == 3
                    && int.TryParse(parts[0], out major)
                    && int.TryParse(parts[1], out minor)
                    && int.TryParse(parts[2], out patch))
                    return;
            }
            major = 0;
            minor = 0;
            patch = 0;
        }

        public static void SetLastSeenVersion(string modId, int major, int minor, int patch)
        {
            lastSeenVersions[modId] = major + "." + minor + "." + patch;
        }

        /* Per-day rescale used by the 1.6.2 settings migration. Round-to-nearest (NOT floor) so a
         * migrated save lands on the same value as a fresh install of the same difficulty; clamp min 1. */
        public static int RescalePerDay(int legacyValue, int interval)
        {
            if (interval < 1) interval = 1;
            int scaled = (int)Math.Round(legacyValue / (double)interval, MidpointRounding.AwayFromZero);
            return Math.Max(1, scaled);
        }

        // Detects whether the settings file was written by a different mod version than the active one,
        // and is the single entry point for version-gated settings migrations. No migrations exist yet.
        // Returns true if the settings were stamped/migrated and should be written back to disk.
        public static bool MigrateSettingsFormat()
        {
            if (activeModVersion.NullOrEmpty()) return false; // no reliable active version to compare against

            if (loadedSettingsVersion.NullOrEmpty())
            {
                LogUtil.MessageForce($"Settings have no version stamp (pre-stamp); active version {activeModVersion}.");
                // Future: migrations for settings written before the stamp existed.
                settingsModVersion = activeModVersion;
                return true;
            }

            if (loadedSettingsVersion == activeModVersion) return false; // same version, nothing to do

            LogUtil.MessageForce($"Settings were written with Empire {loadedSettingsVersion}; active version is {activeModVersion}.");
            if (FCVersion.TryParse(loadedSettingsVersion, out FCVersion loaded))
            {
                if (loaded.IsOlderThan(new FCVersion(1, 6, 2)))
                {
                    // The three int economy settings were authored per-cycle before 1.6.2; they are now per-day.
                    // For a preset difficulty, re-apply the preset so the values land exactly on the fresh per-day
                    // defaults (no rounding drift). Only a Custom difficulty needs the per-day rescale calculation.
                    if (difficultyLevel != EmpireDifficultyLevel.Custom)
                    {
                        ApplyDifficultyPreset(difficultyLevel);
                        LogUtil.MessageForce($"1.6.2 settings migration: re-applied {difficultyLevel} preset (per-day values).");
                    }
                    else
                    {
                        int interval = timeBetweenTaxes_days < 1 ? DEFAULT_TAX_INTERVAL_DAYS : timeBetweenTaxes_days;
                        int oldSilver = silverPerResource, oldWorker = workerCost, oldTithe = productionTitheMod;
                        silverPerResource = RescalePerDay(silverPerResource, interval);
                        workerCost = RescalePerDay(workerCost, interval);
                        productionTitheMod = RescalePerDay(productionTitheMod, interval);
                        LogUtil.MessageForce($"1.6.2 settings rescale (Custom, /{interval}): silver {oldSilver}->{silverPerResource}, " +
                            $"worker {oldWorker}->{workerCost}, tithe {oldTithe}->{productionTitheMod}.");
                    }
                }
            }
            settingsModVersion = activeModVersion;
            return true;
        }

        public static bool IsModLoaded(string packageID) => LoadedModManager.RunningModsListForReading.Any(mod => mod.PackageIdPlayerFacing == packageID);


        public static void DebugMarker(ref int i)
        {
            LogUtil.Message($"DebugMarker: {i}");
            i++;
        }

        // Difficulty preset values
        public static void ApplyDifficultyPreset(EmpireDifficultyLevel difficulty)
        {
            switch (difficulty)
            {
                case EmpireDifficultyLevel.Peaceful:
                    silverPerResource = DEFAULT_SILVER_PER_RESOURCE_PEACEFUL;
                    timeBetweenTaxes_days = DEFAULT_TAX_INTERVAL_DAYS_PEACEFUL;
                    productionTitheMod = DEFAULT_PRODUCTION_TITHE_MOD_PEACEFUL;
                    workerCost = DEFAULT_WORKER_COST_PEACEFUL;
                    workerProductionBaseBonus = DEFAULT_WORKER_PROD_BASE_BONUS_PEACEFUL;
                    workerProductionMultBonus = DEFAULT_WORKER_PROD_MULT_BONUS_PEACEFUL;
                    buildingUpkeepDifficultyMult = 1.0f;
                    break;
                case EmpireDifficultyLevel.CommunityBuilder:
                    silverPerResource = DEFAULT_SILVER_PER_RESOURCE_COMMUNITYBUILDER;
                    timeBetweenTaxes_days = DEFAULT_TAX_INTERVAL_DAYS_COMMUNITYBUILDER;
                    productionTitheMod = DEFAULT_PRODUCTION_TITHE_MOD_COMMUNITYBUILDER;
                    workerCost = DEFAULT_WORKER_COST_COMMUNITYBUILDER;
                    workerProductionBaseBonus = DEFAULT_WORKER_PROD_BASE_BONUS_COMMUNITYBUILDER;
                    workerProductionMultBonus = DEFAULT_WORKER_PROD_MULT_BONUS_COMMUNITYBUILDER;
                    buildingUpkeepDifficultyMult = 1.0f;
                    break;
                case EmpireDifficultyLevel.AdventureStory:
                    silverPerResource = DEFAULT_SILVER_PER_RESOURCE_ADVENTURESTORY;
                    timeBetweenTaxes_days = DEFAULT_TAX_INTERVAL_DAYS_ADVENTURESTORY;
                    productionTitheMod = DEFAULT_PRODUCTION_TITHE_MOD_ADVENTURESTORY;
                    workerCost = DEFAULT_WORKER_COST_ADVENTURESTORY;
                    workerProductionBaseBonus = DEFAULT_WORKER_PROD_BASE_BONUS_ADVENTURESTORY;
                    workerProductionMultBonus = DEFAULT_WORKER_PROD_MULT_BONUS_ADVENTURESTORY;
                    buildingUpkeepDifficultyMult = 1.0f;
                    break;
                case EmpireDifficultyLevel.StriveToSurvive:
                    silverPerResource = DEFAULT_SILVER_PER_RESOURCE_STRIVETOSURVIVE;
                    timeBetweenTaxes_days = DEFAULT_TAX_INTERVAL_DAYS_STRIVETOSURVIVE;
                    productionTitheMod = DEFAULT_PRODUCTION_TITHE_MOD_STRIVETOSURVIVE;
                    workerCost = DEFAULT_WORKER_COST_STRIVETOSURVIVE;
                    workerProductionBaseBonus = DEFAULT_WORKER_PROD_BASE_BONUS_STRIVETOSURVIVE;
                    workerProductionMultBonus = DEFAULT_WORKER_PROD_MULT_BONUS_STRIVETOSURVIVE;
                    buildingUpkeepDifficultyMult = 1.0f;
                    break;
                case EmpireDifficultyLevel.BloodAndDust:
                    silverPerResource = DEFAULT_SILVER_PER_RESOURCE_BLOODANDDUST;
                    timeBetweenTaxes_days = DEFAULT_TAX_INTERVAL_DAYS_BLOODANDDUST;
                    productionTitheMod = DEFAULT_PRODUCTION_TITHE_MOD_BLOODANDDUST;
                    workerCost = DEFAULT_WORKER_COST_BLOODANDDUST;
                    workerProductionBaseBonus = DEFAULT_WORKER_PROD_BASE_BONUS_BLOODANDDUST;
                    workerProductionMultBonus = DEFAULT_WORKER_PROD_MULT_BONUS_BLOODANDDUST;
                    buildingUpkeepDifficultyMult = 1.0f;
                    break;
                case EmpireDifficultyLevel.LosingIsFun:
                    silverPerResource = DEFAULT_SILVER_PER_RESOURCE_LOSINGISFUN;
                    timeBetweenTaxes_days = DEFAULT_TAX_INTERVAL_DAYS_LOSINGISFUN;
                    productionTitheMod = DEFAULT_PRODUCTION_TITHE_MOD_LOSINGISFUN;
                    workerCost = DEFAULT_WORKER_COST_LOSINGISFUN;
                    workerProductionBaseBonus = DEFAULT_WORKER_PROD_BASE_BONUS_LOSINGISFUN;
                    workerProductionMultBonus = DEFAULT_WORKER_PROD_MULT_BONUS_LOSINGISFUN;
                    buildingUpkeepDifficultyMult = 1.0f;
                    break;
                case EmpireDifficultyLevel.Custom:
                    // Don't change anything for custom
                    break;
            }
            LogUtil.Message($"ApplyDifficultyPreset({difficulty}): timeBetweenTaxes_days={timeBetweenTaxes_days}");
        }

        /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *           ~  SECTION RESETS  ~
         * One method per settings section. Each section's "Reset Section" button calls its own
         * method; ResetAllToDefaults() (the universal "Reset to defaults" button) calls them all.
         * Add a field's reset here, never inline in the UI, so the section and universal resets
         * can never drift apart.
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
        public static void ResetGeneralToDefaults()
        {
            difficultyLevel = DEFAULT_DIFFICULTY_LEVEL;
            ApplyDifficultyPreset(difficultyLevel); // silverPerResource / timeBetweenTaxes_days / productionTitheMod / workerCost
            settlementMaxLevel = DEFAULT_SETTLEMENT_MAX_LEVEL;
            settlementUpgradeTimeMultiplier = DEFAULT_SETTLEMENT_UPGRADE_TIME_MULTIPLIER;
            buildingConstructTimeMultiplier = DEFAULT_BUILDING_CONSTRUCT_TIME_MULTIPLIER;
            medievalTechOnly = DEFAULT_MEDIEVAL_TECH_ONLY;
            mirrorPlayerTechLevel = DEFAULT_MIRROR_PLAYER_TECH_LEVEL;
            showSettleConfirm = DEFAULT_SHOW_SETTLE_CONFIRM;
            forcedTaxDeliveryMode = DEFAULT_TAX_DELIVERY_MODE;
            taxNotificationMode = DEFAULT_TAX_NOTIFICATION_MODE;
            printDebug = DEFAULT_PRINT_DEBUG;
            patchNoteAutoOpenThreshold = DEFAULT_PATCH_NOTE_AUTO_OPEN_THRESHOLD;
        }

        public static void ResetEventsToDefaults()
        {
            disableRandomEvents = DEFAULT_DISABLE_RANDOM_EVENTS;
            disableEventsWithOptions = DEFAULT_DISABLE_EVENTS_WITH_OPTIONS;
            blockEventsDuringChain = DEFAULT_BLOCK_EVENTS_DURING_CHAIN;
            eventOptionDelaySeconds = DEFAULT_EVENT_OPTION_DELAY_SECONDS;
            eventSilverCostMultiplier = DEFAULT_EVENT_SILVER_COST_MULTIPLIER;
            minDaysTillRandomEvent = DEFAULT_MIN_DAYS_TIL_RANDOM_EVENT;
            maxDaysTillRandomEvent = DEFAULT_MAX_DAYS_TIL_RANDOM_EVENT;
            minMaxDaysTillRandomEvent = new IntRange(minDaysTillRandomEvent, maxDaysTillRandomEvent);
            disabledEventDefs.Clear();
        }

        public static void ResetMilitaryActionToDefaults()
        {
            enableSettlementCapture = DEFAULT_ENABLE_SETTLEMENT_CAPTURE;
            disableHostileMilitaryActions = DEFAULT_DISABLE_HOSTILE_MILITARY_ACTIONS;
            antiExploit = DEFAULT_ANTI_EXPLOIT;
            restrictDefenseMapLoot = DEFAULT_RESTRICT_DEFENSE_MAP_LOOT;
            battleMode = DEFAULT_BATTLE_MODE;
            manualOffenseBattle = DEFAULT_MANUAL_OFFENSE_BATTLE;
            minDaysTillMilitaryAction = DEFAULT_MIN_DAYS_TIL_MILITARY_ACTION;
            maxDaysTillMilitaryAction = DEFAULT_MAX_DAYS_TIL_MILITARY_ACTION;
            minMaxDaysTillMilitaryAction = new IntRange(minDaysTillMilitaryAction, maxDaysTillMilitaryAction);
            maxThreatMultiplier = DEFAULT_MAX_THREAT_MULTIPLIER;
            defenderAdvantage = DEFAULT_DEFENDER_ADVANTAGE;
            extraNPCDefensiveLevels = DEFAULT_EXTRA_NPC_DEFENSIVE_LEVELS;
            extraNPCOffensiveLevels = DEFAULT_EXTRA_NPC_OFFENSIVE_LEVELS;
            raidPointsMultiplier = DEFAULT_RAID_POINTS_MULTIPLIER;
            maxConcurrentBattleMaps = DEFAULT_MAX_CONCURRENT_BATTLE_MAPS;
            defenseMapBaseSize = DEFAULT_DEFENSE_MAP_BASE_SIZE;
            defenseMapPerLevelStep = DEFAULT_DEFENSE_MAP_PER_LEVEL_STEP;
            defenseMapMinSize = DEFAULT_DEFENSE_MAP_MIN_SIZE;
            defenseMapMaxSize = DEFAULT_DEFENSE_MAP_MAX_SIZE;
            efficiencyDamping = DEFAULT_EFFICIENCY_DAMPING;
            mercenaryHealRatePerHour = DEFAULT_MERCENARY_HEAL_RATE_PER_HOUR;
            militaryMechRepairRate = DEFAULT_MILITARY_MECH_REPAIR_RATE;
        }

        public static void ResetAutoResolveToDefaults()
        {
            applyAutoResolveInjuries = DEFAULT_APPLY_AUTO_RESOLVE_INJURIES;
            autoResolveCasualtyDeathThreshold = DEFAULT_AUTO_RESOLVE_CASUALTY_DEATH_THRESHOLD;
            autoResolveCasualtyMaxDeathFraction = DEFAULT_AUTO_RESOLVE_CASUALTY_MAX_DEATH_FRACTION;
            crushingDefeatPenaltyMultiplier = DEFAULT_CRUSHING_DEFEAT_PENALTY_MULTIPLIER;
            overwhelmingVictoryRewardMultiplier = DEFAULT_OVERWHELMING_VICTORY_REWARD_MULTIPLIER;
            respectLethalDamageThreshold = DEFAULT_RESPECT_LETHAL_DAMAGE_THRESHOLD;
            autoResolveTicksPerRound = DEFAULT_AUTO_RESOLVE_TICKS_PER_ROUND;
        }

        public static void ResetSquadsAndGenesToDefaults()
        {
            maxSquadSize = DEFAULT_MAX_SQUAD_SIZE;
            maxAnimalSubpawns = DEFAULT_MAX_ANIMAL_SUBPAWNS;
            squadHireCostMultiplier = DEFAULT_SQUAD_HIRE_COST_MULTIPLIER;
            squadUpgradeCostMultiplier = DEFAULT_SQUAD_UPGRADE_COST_MULTIPLIER;
            squadDeploymentCostPercentage = DEFAULT_SQUAD_DEPLOYMENT_COST_PERCENTAGE;
            deploymentBillLifespan_days = DEFAULT_DEPLOYMENT_BILL_LIFESPAN_DAYS;
            squadRestockCostPercentage = DEFAULT_SQUAD_RESTOCK_COST_PERCENTAGE;
            militaryPsylinkCostMultiplier = DEFAULT_MILITARY_PSYLINK_COST_MULTIPLIER;
            militaryMechCostMultiplier = DEFAULT_MILITARY_MECH_COST_MULTIPLIER;
            militaryMechlinkCost = DEFAULT_MILITARY_MECHLINK_COST;
            geneValueWeightMvf       = DEFAULT_GENE_W_MVF;
            geneValueWeightMet       = DEFAULT_GENE_W_MET;
            geneValueWeightArc       = DEFAULT_GENE_W_ARC;
            geneValueWeightEffects   = DEFAULT_GENE_W_EFFECTS;
            geneValueWeightPain      = DEFAULT_GENE_W_PAIN;
            geneValueWeightDmgResist = DEFAULT_GENE_W_DMGRESIST;
            geneValueFactorUnlimited = DEFAULT_GENE_FACTOR_UNLIMITED;
            geneValueMaxFactor       = DEFAULT_GENE_MAX_FACTOR;
        }

        public static void ResetBattleArchiveToDefaults()
        {
            battleArchiveMaxEntries = DEFAULT_BATTLE_ARCHIVE_MAX_ENTRIES;
            battleArchiveUnlimited = DEFAULT_BATTLE_ARCHIVE_UNLIMITED;
        }

        public static void ResetRoadBuilderToDefaults()
        {
            useThreadedRoadComputation = DEFAULT_USE_THREADED_ROAD_COMPUTATION;
            edgesPerRoadTick = DEFAULT_EDGES_PER_ROAD_TICK;
            roadBuildIntervalDays = DEFAULT_ROAD_BUILD_INTERVAL_DAYS;
        }

        public static void ResetAdvancedToDefaults()
        {
            unrestBaseGain = DEFAULT_UNREST_BASE_GAIN;
            unrestBaseLost = DEFAULT_UNREST_BASE_LOST;
            loyaltyBaseGain = DEFAULT_LOYALTY_BASE_GAIN;
            loyaltyBaseLost = DEFAULT_LOYALTY_BASE_LOST;
            happinessBaseGain = DEFAULT_HAPPINESS_BASE_GAIN;
            happinessBaseLost = DEFAULT_HAPPINESS_BASE_LOST;
            prosperityDriftRate = DEFAULT_PROSPERITY_DRIFT_RATE;
            prosperityDriftStep = DEFAULT_PROSPERITY_DRIFT_STEP;
            productionResearchBase = DEFAULT_PRODUCTION_RESEARCH_BASE;
        }

        public static void ResetCompatToDefaults()
        {
            vpePsycastBaseCost = DEFAULT_VPE_PSYCAST_BASE_COST;
            vpePsycastPerLevelCost = DEFAULT_VPE_PSYCAST_PER_LEVEL_COST;
            vpeFocusCost = DEFAULT_VPE_FOCUS_COST;
            vpeStatPointCost = DEFAULT_VPE_STAT_POINT_COST;
            militaryPsycastCostMultiplier = DEFAULT_MILITARY_PSYCAST_COST_MULTIPLIER;
        }

        /// <summary>
        /// Universal "Reset to defaults": resets every settings section, including Compatibility.
        /// </summary>
        public static void ResetAllToDefaults()
        {
            ResetGeneralToDefaults();
            ResetEventsToDefaults();
            ResetMilitaryActionToDefaults();
            ResetAutoResolveToDefaults();
            ResetSquadsAndGenesToDefaults();
            ResetBattleArchiveToDefaults();
            ResetRoadBuilderToDefaults();
            ResetCompatToDefaults();
            ResetAdvancedToDefaults();
            ResetLaborersToDefaults();
            LogUtil.Message($"Settings reset: timeBetweenTaxes_days={timeBetweenTaxes_days}");
        }

        public static void ResetLaborersToDefaults()
        {
            laborerDurationDays = DEFAULT_LABORER_DURATION_DAYS;
            laborerCooldownDays = DEFAULT_LABORER_COOLDOWN_DAYS;
            laborerBaseCount = DEFAULT_LABORER_BASE_COUNT;
            laborerPerSettlement = DEFAULT_LABORER_PER_SETTLEMENT;
            laborerMaxCount = DEFAULT_LABORER_MAX_COUNT;
            laborerCostPerDay = DEFAULT_LABORER_COST_PER_DAY;
            laborerSkillBonusPerLevel = DEFAULT_LABORER_SKILL_BONUS_PER_LEVEL;
            laborerSkillBonusCap = DEFAULT_LABORER_SKILL_BONUS_CAP;
        }

        public static int DaysBetweenTaxesByDifficulty(EmpireDifficultyLevel difficulty)
        {
            switch (difficulty)
            {
                case EmpireDifficultyLevel.Peaceful:
                    return DEFAULT_TAX_INTERVAL_DAYS_PEACEFUL;
                case EmpireDifficultyLevel.CommunityBuilder:
                    return DEFAULT_TAX_INTERVAL_DAYS_COMMUNITYBUILDER;
                case EmpireDifficultyLevel.AdventureStory:
                    return DEFAULT_TAX_INTERVAL_DAYS_ADVENTURESTORY;
                case EmpireDifficultyLevel.StriveToSurvive:
                    return DEFAULT_TAX_INTERVAL_DAYS_STRIVETOSURVIVE;
                case EmpireDifficultyLevel.BloodAndDust:
                    return DEFAULT_TAX_INTERVAL_DAYS_BLOODANDDUST;
                case EmpireDifficultyLevel.LosingIsFun:
                    return DEFAULT_TAX_INTERVAL_DAYS_LOSINGISFUN;
                default:
                    return DEFAULT_TAX_INTERVAL_DAYS;
            }
        }
        public static int TicksBetweenTaxesByDifficulty(EmpireDifficultyLevel difficulty)
        {
            return DaysBetweenTaxesByDifficulty(difficulty) * GenDate.TicksPerDay;
        }

        string laborerCostPerDay_buffer;
        string silverPerResource_buffer;
        string timeBetweenTaxes_buffer;
        string productionTitheMod_buffer;
        string workerCost_buffer;
        string settlementMaxLevel_buffer;

        private static int timeBetweenTaxes_lastSeen = DEFAULT_TAX_INTERVAL_DAYS;

        private Vector2 scrollVectorGeneral = new Vector2();
        private Vector2 scrollVectorEvents = new Vector2();
        private Vector2 scrollVectorMilitary = new Vector2();
        private Vector2 scrollVectorRoadBuilder = new Vector2();
        private Vector2 scrollVectorCompat = new Vector2();
        private Vector2 scrollVectorAdvanced = new Vector2();

        /* Per-tab content heights, measured from the previous frame's Listing_Standard and
         * fed back into the scroll view so the scrollbar matches the real content length. */
        private float contentHeightGeneral;
        private float contentHeightEvents;
        private float contentHeightMilitary;
        private float contentHeightRoadBuilder;
        private float contentHeightCompat;
        private float contentHeightAdvanced;

        /// <summary>
        /// Creates an option for the list of ForcedTaxDeliveryOptions. Shuttles may not be used if royality is inactive
        /// </summary>
        private FloatMenuOption ShuttleOption
        {
            get
            {
                if (ModsConfig.RoyaltyActive)
                {
                    return new FloatMenuOption("FCTaxDeliveryModeShuttleDesc".Translate(), delegate () { forcedTaxDeliveryMode = TaxDeliveryMode.Shuttle; });
                }
                else
                {
                    return new FloatMenuOption("FCTaxDeliveryModeShuttleUnavailableDesc".Translate(), null);
                }
            }
        }

        /// <summary>
        /// Creates a list of options for forced tax delivery
        /// </summary>
        private List<FloatMenuOption> ForcedTaxDeliveryOptions
        {
            get
            {
                return new List<FloatMenuOption>()
                {
                    new FloatMenuOption("FCTaxDeliveryModeDefaultDesc".Translate(), delegate() {forcedTaxDeliveryMode = default;}),
                    new FloatMenuOption("FCTaxDeliveryModeTaxSpotDesc".Translate(), delegate() {forcedTaxDeliveryMode = TaxDeliveryMode.TaxSpot;}),
                    new FloatMenuOption("FCTaxDeliveryModeCaravanDesc".Translate(), delegate() {forcedTaxDeliveryMode = TaxDeliveryMode.Caravan;}),
                    new FloatMenuOption("FCTaxDeliveryModeDropPodDesc".Translate(), delegate() {forcedTaxDeliveryMode = TaxDeliveryMode.DropPod;}),
                    ShuttleOption
                };
            }
        }

        private List<FloatMenuOption> BattleModeOptions => new List<FloatMenuOption>
        {
            new FloatMenuOption("FCBattleModeAuto".Translate() + " - " + "FCBattleModeAutoDesc".Translate(), () => battleMode = BattleMode.Auto),
            new FloatMenuOption("FCBattleModeManual".Translate() + " - " + "FCBattleModeManualDesc".Translate(), () => battleMode = BattleMode.Manual),
            new FloatMenuOption("FCBattleModeHybrid".Translate() + " - " + "FCBattleModeHybridDesc".Translate(), () => battleMode = BattleMode.Hybrid)
        };

        // Offense has no Hybrid state (the caravan-on-tile heuristic doesn't map to attacking), so
        // it is a simple Auto / Manual toggle backed by the manualOffenseBattle bool.
        private List<FloatMenuOption> OffenseBattleModeOptions => new List<FloatMenuOption>
        {
            new FloatMenuOption("FCBattleModeAuto".Translate() + " - " + "FCOffenseBattleModeAutoDesc".Translate(), () => manualOffenseBattle = false),
            new FloatMenuOption("FCBattleModeManual".Translate() + " - " + "FCOffenseBattleModeManualDesc".Translate(), () => manualOffenseBattle = true)
        };

        /// <summary>
        /// Creates a list of options for tax notification mode
        /// </summary>
        private List<FloatMenuOption> TaxNotificationOptions => new List<FloatMenuOption>
        {
            new FloatMenuOption("FCTaxNotifyAll".Translate(), () => taxNotificationMode = TaxNotificationMode.All),
            new FloatMenuOption("FCTaxNotifyLetterOnly".Translate(), () => taxNotificationMode = TaxNotificationMode.LetterOnly),
            new FloatMenuOption("FCTaxNotifyMessageOnly".Translate(), () => taxNotificationMode = TaxNotificationMode.MessageOnly),
            new FloatMenuOption("FCTaxNotifyNone".Translate(), () => taxNotificationMode = TaxNotificationMode.None)
        };

        public void DoWindowContents(Rect inRect)
        {
            // Build tabs
            settingsTabs.Clear();
            settingsTabs.Add(new TabRecord("FCSettingsTabGeneral".Translate(), delegate { settingsTab = 0; }, settingsTab == 0));
            settingsTabs.Add(new TabRecord("FCSettingsTabEvents".Translate(), delegate { settingsTab = 1; }, settingsTab == 1));
            settingsTabs.Add(new TabRecord("FCSettingsTabMilitary".Translate(), delegate { settingsTab = 2; }, settingsTab == 2));
            settingsTabs.Add(new TabRecord("FCSettingsTabRoadBuilder".Translate(), delegate { settingsTab = 3; }, settingsTab == 3));
            settingsTabs.Add(new TabRecord("FCSettingsTabCompat".Translate(), delegate { settingsTab = 4; }, settingsTab == 4));
            settingsTabs.Add(new TabRecord("FCSettingsTabAdvanced".Translate(), delegate { settingsTab = 5; }, settingsTab == 5));

            Rect contentRect = new Rect(inRect.x, inRect.y + 40f, inRect.width, inRect.height - 40f);
            Widgets.DrawMenuSection(contentRect);
            TabDrawer.DrawTabs(contentRect, settingsTabs);

            // Inset the content area slightly for padding
            Rect innerRect = contentRect.ContractedBy(5f);

            switch (settingsTab)
            {
                case 0: DoGeneralTab(innerRect); break;
                case 1: DoEventsTab(innerRect); break;
                case 2: DoMilitaryTab(innerRect); break;
                case 3: DoRoadBuilderTab(innerRect); break;
                case 4: DoCompatTab(innerRect); break;
                case 5: DoAdvancedTab(innerRect); break;
            }
        }

        private void DoGeneralTab(Rect rect)
        {
            silverPerResource_buffer = silverPerResource.ToString();
            timeBetweenTaxes_buffer = timeBetweenTaxes_days.ToString();
            timeBetweenTaxes_lastSeen = timeBetweenTaxes_days;
            productionTitheMod_buffer = productionTitheMod.ToString();
            workerCost_buffer = workerCost.ToString();
            settlementMaxLevel_buffer = settlementMaxLevel.ToString();

            minMaxDaysTillMilitaryAction = new IntRange(minDaysTillMilitaryAction, maxDaysTillMilitaryAction);
            minMaxDaysTillRandomEvent = new IntRange(minDaysTillRandomEvent, maxDaysTillRandomEvent);

            Rect viewRect = ScrollUtil.BeginScrollView(rect, ref scrollVectorGeneral, contentHeightGeneral);
            Rect listRect = new Rect(viewRect.x, viewRect.y, viewRect.width, float.MaxValue);
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(listRect);
            Listing_StandardExtensions.ResetRowStripe();

            // Display mod version
            ls.Label("FCModVersion".Translate(GetModVersion()));
            ls.Gap(10f);

            // Worker-production bonuses feed the cached production base/mult, so a mid-game change here
            // must invalidate resource caches (unlike the live-read economy settings). Track the values
            // across the difficulty section and invalidate below only if they actually changed.
            float prevWorkerProdBaseBonus = workerProductionBaseBonus;
            float prevWorkerProdMultBonus = workerProductionMultBonus;

            // Empire Difficulty Selection
            ls.Label("FCSettingEmpireDifficulty".Translate());
            ls.Gap(5f);

            // Create difficulty options with descriptions
            var difficultyOptions = new List<(EmpireDifficultyLevel level, string nameKey, string descKey)>
            {
                (EmpireDifficultyLevel.Peaceful, "FCDifficultyPeaceful", "FCDifficultyPeacefulDesc"),
                (EmpireDifficultyLevel.CommunityBuilder, "FCDifficultyCommunityBuilder", "FCDifficultyCommunityBuilderDesc"),
                (EmpireDifficultyLevel.AdventureStory, "FCDifficultyAdventureStory", "FCDifficultyAdventureStoryDesc"),
                (EmpireDifficultyLevel.StriveToSurvive, "FCDifficultyStriveToSurvive", "FCDifficultyStriveToSurviveDesc"),
                (EmpireDifficultyLevel.BloodAndDust, "FCDifficultyBloodAndDust", "FCDifficultyBloodAndDustDesc"),
                (EmpireDifficultyLevel.LosingIsFun, "FCDifficultyLosingIsFun", "FCDifficultyLosingIsFunDesc"),
                (EmpireDifficultyLevel.Custom, "FCDifficultyCustom", "FCDifficultyCustomDesc")
            };

            foreach (var option in difficultyOptions)
            {
                bool isSelected = difficultyLevel == option.level;

                if (ls.RadioButton(option.nameKey.Translate(), isSelected))
                {
                    if (!isSelected) // Only change if not already selected
                    {
                        difficultyLevel = option.level;
                        if (option.level != EmpireDifficultyLevel.Custom)
                        {
                            ApplyDifficultyPreset(option.level);
                        }
                    }
                }
                // Add description as a separate indented label
                ls.Label("    " + option.descKey.Translate(), -1f);
            }

            ls.Gap(15f);

            // Show economic settings only if Custom is selected
            if (difficultyLevel == EmpireDifficultyLevel.Custom)
            {
                ls.Label("FCSettingSilverPerResource".Translate());
                ls.IntEntry(ref silverPerResource, ref silverPerResource_buffer);
                ls.Label("FCSettingDaysBetweenTax".Translate());
                ls.IntEntry(ref timeBetweenTaxes_days, ref timeBetweenTaxes_buffer);
                if (timeBetweenTaxes_days < 1)
                {
                    timeBetweenTaxes_days = 1;
                    timeBetweenTaxes_buffer = "1";
                }
                if (timeBetweenTaxes_days != timeBetweenTaxes_lastSeen)
                {
                    LogUtil.Message($"Settings UI: timeBetweenTaxes_days {timeBetweenTaxes_lastSeen} -> {timeBetweenTaxes_days}");
                    timeBetweenTaxes_lastSeen = timeBetweenTaxes_days;
                }
                ls.Label("FCSettingProductionTitheMod".Translate());
                ls.IntEntry(ref productionTitheMod, ref productionTitheMod_buffer);
                ls.Label("FCSettingWorkerCost".Translate());
                ls.IntEntry(ref workerCost, ref workerCost_buffer);
                workerProductionBaseBonus = ls.SliderTextField("FCSettingWorkerProductionBaseBonus",
                    "FCSettingWorkerProductionBaseBonus".Translate(), workerProductionBaseBonus, -1f, 5f, decimals: 1,
                    tooltip: "FCSettingWorkerProductionBaseBonusTip".Translate());
                workerProductionMultBonus = ls.SliderTextField("FCSettingWorkerProductionMultBonus",
                    "FCSettingWorkerProductionMultBonus".Translate(), workerProductionMultBonus, 0.1f, 5f, decimals: 2, unit: "x",
                    tooltip: "FCSettingWorkerProductionMultBonusTip".Translate());
            }
            else
            {
                // Show current values as read-only labels for non-custom difficulties
                ls.Label($"FCSettingSilverPerResource".Translate() + ": " + silverPerResource);
                ls.Label($"FCSettingDaysBetweenTax".Translate() + ": " + timeBetweenTaxes_days);
                ls.Label($"FCSettingProductionTitheMod".Translate() + ": " + productionTitheMod);
                ls.Label($"FCSettingWorkerCost".Translate() + ": " + workerCost);
                ls.Label("FCSettingWorkerProductionBaseBonus".Translate() + ": " + workerProductionBaseBonus.ToString("0.0"));
                ls.Label("FCSettingWorkerProductionMultBonus".Translate() + ": " + workerProductionMultBonus.ToString("0.00") + "x");
            }

            if (prevWorkerProdBaseBonus != workerProductionBaseBonus || prevWorkerProdMultBonus != workerProductionMultBonus)
            {
                FindFC.FactionComp?.InvalidateFactionStatCache();
            }

            ls.Label("FCSettingMaxSettlementLevel".Translate());
            ls.IntEntry(ref settlementMaxLevel, ref settlementMaxLevel_buffer);
            ls.CheckboxLabeled("FCMedievalTechOnly".Translate(), ref medievalTechOnly);
            bool prevMirrorPlayerTechLevel = mirrorPlayerTechLevel;
            ls.CheckboxLabeled("FCMirrorPlayerTechLevel".Translate(), ref mirrorPlayerTechLevel, "FCMirrorPlayerTechLevelDesc".Translate());
            if (prevMirrorPlayerTechLevel != mirrorPlayerTechLevel)
            {
                FindFC.FactionComp?.DirtyTechLevelCache();
            }
            ls.CheckboxLabeled("FCSettingShowSettleConfirm".Translate(), ref showSettleConfirm);
            if (ls.ButtonText("FCSelectTaxDeliveryModeButton".Translate() + forcedTaxDeliveryMode)) Find.WindowStack.Add(new FloatMenu(ForcedTaxDeliveryOptions));
            if (ls.ButtonText("FCTaxNotificationModeButton".Translate() + taxNotificationMode)) Find.WindowStack.Add(new FloatMenu(TaxNotificationOptions));

            settlementUpgradeTimeMultiplier = ls.SliderTextField("FCSettingSettlementUpgradeTime",
                "FCSettingSettlementUpgradeTime".Translate(), settlementUpgradeTimeMultiplier, 0f, 10f, decimals: 1, unit: "x");
            buildingConstructTimeMultiplier = ls.SliderTextField("FCSettingBuildingConstructTime",
                "FCSettingBuildingConstructTime".Translate(), buildingConstructTimeMultiplier, 0f, 10f, decimals: 1, unit: "x");

            ls.Gap(12f);
            ls.GapLine();
            Text.Font = GameFont.Medium;
            ls.Label("FCSettingLaborersHeader".Translate());
            Text.Font = GameFont.Small;

            laborerDurationDays = ls.SliderTextField("FCSettingLaborerDurationDays",
                "FCSettingLaborerDurationDays".Translate(), laborerDurationDays, 1, 10, unit: "d",
                tooltip: "FCSettingLaborerDurationDaysTip".Translate());
            laborerCooldownDays = ls.SliderTextField("FCSettingLaborerCooldownDays",
                "FCSettingLaborerCooldownDays".Translate(), laborerCooldownDays, 1, 15, unit: "d",
                tooltip: "FCSettingLaborerCooldownDaysTip".Translate());
            laborerBaseCount = ls.SliderTextField("FCSettingLaborerBaseCount",
                "FCSettingLaborerBaseCount".Translate(), laborerBaseCount, 1, 10,
                tooltip: "FCSettingLaborerBaseCountTip".Translate());
            laborerPerSettlement = ls.SliderTextField("FCSettingLaborerPerSettlement",
                "FCSettingLaborerPerSettlement".Translate(), laborerPerSettlement, 1, 10,
                tooltip: "FCSettingLaborerPerSettlementTip".Translate());
            laborerMaxCount = ls.SliderTextField("FCSettingLaborerMaxCount",
                "FCSettingLaborerMaxCount".Translate(), laborerMaxCount, 2, 50,
                tooltip: "FCSettingLaborerMaxCountTip".Translate());
            laborerSkillBonusPerLevel = ls.SliderTextField("FCSettingLaborerSkillBonusPerLevel",
                "FCSettingLaborerSkillBonusPerLevel".Translate(), laborerSkillBonusPerLevel, 0, 5,
                tooltip: "FCSettingLaborerSkillBonusPerLevelTip".Translate());
            laborerSkillBonusCap = ls.SliderTextField("FCSettingLaborerSkillBonusCap",
                "FCSettingLaborerSkillBonusCap".Translate(), laborerSkillBonusCap, 0, 20,
                tooltip: "FCSettingLaborerSkillBonusCapTip".Translate());

            // Per-laborer/day cost: a plain numeric entry (range is too wide for a useful slider).
            ls.Label("FCSettingLaborerCostPerDay".Translate());
            ls.IntEntry(ref laborerCostPerDay, ref laborerCostPerDay_buffer);
            if (laborerCostPerDay < 1) { laborerCostPerDay = 1; laborerCostPerDay_buffer = "1"; }
            if (laborerCostPerDay > 1000) { laborerCostPerDay = 1000; laborerCostPerDay_buffer = "1000"; }

            DrawSectionResetButton(ls, ResetLaborersToDefaults, "FCSettingResetSectionTag".Translate("FCSettingLaborersHeader".Translate()));

            ls.GapLine();

            ls.CheckboxLabeled("FCSettingEnableDebugLogging".Translate(), ref printDebug);

            if (ls.ButtonText("FCOpenPatchNotes".Translate())) DebugActionsMisc.PatchNotesDisplayWindow();

            string thresholdLabel;
            switch (patchNoteAutoOpenThreshold)
            {
                case PatchNoteType.Major: thresholdLabel = "Major only"; break;
                case PatchNoteType.Minor: thresholdLabel = "Minor and above"; break;
                case PatchNoteType.Hotfix: thresholdLabel = "Hotfix and above"; break;
                case PatchNoteType.Patch: thresholdLabel = "Patch and above"; break;
                default: thresholdLabel = "Never"; break;
            }
            if (ls.ButtonText("FCPatchNoteAutoOpenThreshold".Translate() + thresholdLabel))
            {
                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                {
                    new FloatMenuOption("Major only", () => patchNoteAutoOpenThreshold = PatchNoteType.Major),
                    new FloatMenuOption("Minor and above", () => patchNoteAutoOpenThreshold = PatchNoteType.Minor),
                    new FloatMenuOption("Hotfix and above", () => patchNoteAutoOpenThreshold = PatchNoteType.Hotfix),
                    new FloatMenuOption("Patch and above", () => patchNoteAutoOpenThreshold = PatchNoteType.Patch),
                    new FloatMenuOption("Never", () => patchNoteAutoOpenThreshold = PatchNoteType.Undefined)
                }));
            }

            ls.GapLine();

            ls.Gap(11f);

            DrawSectionResetButton(ls, ResetGeneralToDefaults, "FCSettingResetSectionTag".Translate("FCSettingsTabGeneral".Translate()));

            ls.GapLine();

            if (ls.ButtonText("FCSettingResetButton".Translate()))
            {
                ResetAllToDefaults();
            }

            contentHeightGeneral = ls.CurHeight + 12f;
            ls.End();

            ScrollUtil.EndScrollView();
        }

        private void DoAdvancedTab(Rect rect)
        {
            Rect viewRect = ScrollUtil.BeginScrollView(rect, ref scrollVectorAdvanced, contentHeightAdvanced);
            Rect listRect = new Rect(viewRect.x, viewRect.y, viewRect.width, float.MaxValue);
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(listRect);
            Listing_StandardExtensions.ResetRowStripe();

            ls.Label("FCSettingAdvancedWarn".Translate());
            ls.GapLine();

            /* Daily settlement drift. Each pair is a per-day gain/loss applied to the relevant stat; defaults match the
             * original hardcoded values, so leaving this tab untouched preserves vanilla balance. */
            unrestBaseGain = ls.SliderTextField("FCSettingUnrestBaseGain",
                "FCSettingUnrestBaseGain".Translate(), (float)unrestBaseGain, 0f, 10f, decimals: 2);
            unrestBaseLost = ls.SliderTextField("FCSettingUnrestBaseLost",
                "FCSettingUnrestBaseLost".Translate(), (float)unrestBaseLost, 0f, 10f, decimals: 2);
            loyaltyBaseGain = ls.SliderTextField("FCSettingLoyaltyBaseGain",
                "FCSettingLoyaltyBaseGain".Translate(), (float)loyaltyBaseGain, 0f, 10f, decimals: 2);
            loyaltyBaseLost = ls.SliderTextField("FCSettingLoyaltyBaseLost",
                "FCSettingLoyaltyBaseLost".Translate(), (float)loyaltyBaseLost, 0f, 10f, decimals: 2);
            happinessBaseGain = ls.SliderTextField("FCSettingHappinessBaseGain",
                "FCSettingHappinessBaseGain".Translate(), (float)happinessBaseGain, 0f, 10f, decimals: 2);
            happinessBaseLost = ls.SliderTextField("FCSettingHappinessBaseLost",
                "FCSettingHappinessBaseLost".Translate(), (float)happinessBaseLost, 0f, 10f, decimals: 2);

            ls.GapLine();

            // Prosperity drift floor + how far from target prosperity raises the drift by one point/day.
            prosperityDriftRate = ls.SliderTextField("FCSettingProsperityDriftRate",
                "FCSettingProsperityDriftRate".Translate(), (float)prosperityDriftRate, 0f, 20f, decimals: 2);
            prosperityDriftStep = ls.SliderTextField("FCSettingProsperityDriftStep",
                "FCSettingProsperityDriftStep".Translate(), (float)prosperityDriftStep, 1f, 50f, decimals: 2);

            ls.GapLine();

            productionResearchBase = ls.SliderTextField("FCSettingProductionResearchBase",
                "FCSettingProductionResearchBase".Translate(), productionResearchBase, 0, 1000);

            ls.GapLine();
            ls.Gap(11f);

            DrawSectionResetButton(ls, ResetAdvancedToDefaults, "FCSettingResetSectionTag".Translate("FCSettingsTabAdvanced".Translate()));

            contentHeightAdvanced = ls.CurHeight + 12f;
            ls.End();

            ScrollUtil.EndScrollView();
        }

        private void DoEventsTab(Rect rect)
        {
            Rect viewRect = ScrollUtil.BeginScrollView(rect, ref scrollVectorEvents, contentHeightEvents);
            Rect listRect = new Rect(viewRect.x, viewRect.y, viewRect.width, float.MaxValue);
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(listRect);
            Listing_StandardExtensions.ResetRowStripe();

            ls.CheckboxLabeled("FCSettingDisableRandomEvents".Translate(), ref disableRandomEvents);
            ls.CheckboxLabeled("FCSettingDisableEventsWithOptions".Translate(), ref disableEventsWithOptions);
            ls.CheckboxLabeled("FCSettingBlockEventsDuringChain".Translate(), ref blockEventsDuringChain,
                "FCSettingBlockEventsDuringChainDesc".Translate());
            eventOptionDelaySeconds = ls.SliderTextField("FCSettingEventOptionDelay",
                "FCSettingEventOptionDelay".Translate(), eventOptionDelaySeconds, 0f, 2f, decimals: 1, unit: "s");

            eventSilverCostMultiplier = ls.SliderTextField("FCSettingEventSilverCostMultiplier",
                "FCSettingEventSilverCostMultiplier".Translate(), eventSilverCostMultiplier, 0f, 10f, decimals: 1, unit: "x");

            ls.Gap(5f);

            minMaxDaysTillRandomEvent = new IntRange(minDaysTillRandomEvent, maxDaysTillRandomEvent);
            ls.Label("FCSettingMinMaxRandomEvent".Translate());
            ls.IntRange(ref minMaxDaysTillRandomEvent, 0, 30);
            minDaysTillRandomEvent = minMaxDaysTillRandomEvent.min;
            maxDaysTillRandomEvent = Math.Max(1, minMaxDaysTillRandomEvent.max);

            ls.GapLine();

            ls.Label("FCSettingConfigureEvents".Translate());
            ls.Gap(5f);

            List<FCEventDef> rootEvents = FactionCache.RandomRollableEvents;

            // Group by category, then sort alphabetically within each group
            var grouped = new Dictionary<string, List<FCEventDef>>();
            foreach (FCEventDef def in rootEvents)
            {
                string catLabel = def.category != null ? def.category.LabelCap.ToString() : "FCOther".Translate().ToString();
                if (!grouped.ContainsKey(catLabel))
                    grouped[catLabel] = new List<FCEventDef>();
                grouped[catLabel].Add(def);
            }

            int rowIndex = 0;
            foreach (string catLabel in grouped.Keys.OrderBy(k => k))
            {
                ls.Gap(3f);
                ls.Label(catLabel);
                foreach (FCEventDef def in grouped[catLabel].OrderBy(d => d.label))
                {
                    Rect rowRect = ls.GetRect(Text.LineHeight);

                    if (rowIndex % 2 == 1)
                    {
                        Widgets.DrawLightHighlight(rowRect);
                    }
                    rowIndex++;

                    bool enabled = !disabledEventDefs.Contains(def.defName);
                    bool prev = enabled;
                    Widgets.CheckboxLabeled(rowRect, "  " + def.label, ref enabled);
                    if (enabled != prev)
                    {
                        if (enabled) disabledEventDefs.Remove(def.defName);
                        else disabledEventDefs.Add(def.defName);
                    }
                }
            }

            if (disabledEventDefs.Count > 0)
            {
                ls.Gap(10f);
                if (ls.ButtonText("FCSettingEnableAllEvents".Translate()))
                {
                    disabledEventDefs.Clear();
                }
            }

            DrawSectionResetButton(ls, ResetEventsToDefaults);

            contentHeightEvents = ls.CurHeight + 12f;
            ls.End();

            ScrollUtil.EndScrollView();
        }

        private void DoMilitaryTab(Rect rect)
        {
            minMaxDaysTillMilitaryAction = new IntRange(minDaysTillMilitaryAction, maxDaysTillMilitaryAction);

            Rect viewRect = ScrollUtil.BeginScrollView(rect, ref scrollVectorMilitary, contentHeightMilitary);
            Rect listRect = new Rect(viewRect.x, viewRect.y, viewRect.width, float.MaxValue);
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(listRect);
            Listing_StandardExtensions.ResetRowStripe();

            ls.CheckboxLabeled("FCSettingEnableSettlementCapture".Translate(), ref enableSettlementCapture, "FCSettingEnableSettlementCaptureTip".Translate());
            ls.CheckboxLabeled("FCSettingDisableHostileMilActions".Translate(), ref disableHostileMilitaryActions);
            ls.CheckboxLabeled("FCSettingAntiExploit".Translate(), ref antiExploit, "FCSettingAntiExploitTip".Translate());
            ls.CheckboxLabeled("FCSettingRestrictDefenseMapLoot".Translate(), ref restrictDefenseMapLoot, "FCSettingRestrictDefenseMapLootTip".Translate());
            // Defense battle mode (Auto / Manual / Hybrid) -- button + tooltip.
            Rect defenseModeRect = ls.GetRect(30f);
            Widgets.DrawHighlightIfMouseover(defenseModeRect);
            TooltipHandler.TipRegion(defenseModeRect, "FCSettingBattleModeTip".Translate());
            if (Widgets.ButtonText(defenseModeRect, "FCSettingBattleMode".Translate() + battleMode))
                Find.WindowStack.Add(new FloatMenu(BattleModeOptions));

            // Offense battle mode (Auto / Manual) -- button + tooltip.
            Rect offenseModeRect = ls.GetRect(30f);
            Widgets.DrawHighlightIfMouseover(offenseModeRect);
            TooltipHandler.TipRegion(offenseModeRect, "FCSettingOffenseBattleModeTip".Translate());
            string offenseModeState = (manualOffenseBattle ? "FCBattleModeManual" : "FCBattleModeAuto").Translate();
            if (Widgets.ButtonText(offenseModeRect, "FCSettingOffenseBattleMode".Translate() + offenseModeState))
                Find.WindowStack.Add(new FloatMenu(OffenseBattleModeOptions));

            ls.Gap(10f);

            ls.Label("FCSettingMinMaxMilitaryAction".Translate());
            ls.IntRange(ref minMaxDaysTillMilitaryAction, 1, 30);
            minDaysTillMilitaryAction = minMaxDaysTillMilitaryAction.min;
            maxDaysTillMilitaryAction = Math.Max(1, minMaxDaysTillMilitaryAction.max);

            maxThreatMultiplier = ls.SliderTextField("FCSettingMaxThreatScaling",
                "FCSettingMaxThreatScaling".Translate(), maxThreatMultiplier, 1.0f, 5.0f, decimals: 1, unit: "x");

            defenderAdvantage = ls.SliderTextField("FCSettingDefenderAdvantage",
                "FCSettingDefenderAdvantage".Translate(), defenderAdvantage, 1.0f, 1.5f, decimals: 2, unit: "x");

            extraNPCDefensiveLevels = ls.SliderTextField("FCSettingExtraNPCDefensiveLevels",
                "FCSettingExtraNPCDefensiveLevels".Translate(), extraNPCDefensiveLevels, 0, 50,
                tooltip: "FCSettingExtraNPCDefensiveLevelsTip".Translate());

            extraNPCOffensiveLevels = ls.SliderTextField("FCSettingExtraNPCOffensiveLevels",
                "FCSettingExtraNPCOffensiveLevels".Translate(), extraNPCOffensiveLevels, 0, 50,
                tooltip: "FCSettingExtraNPCOffensiveLevelsTip".Translate());

            raidPointsMultiplier = ls.NumericTextField("FCSettingRaidPointsMultiplier",
                "FCSettingRaidPointsMultiplier".Translate(), raidPointsMultiplier, 1f, 100000f,
                tooltip: "FCSettingRaidPointsMultiplierTip".Translate());

            maxConcurrentBattleMaps = ls.SliderTextField("FCSettingMaxConcurrentBattleMaps",
                "FCSettingMaxConcurrentBattleMaps".Translate(), maxConcurrentBattleMaps, 0, 10,
                tooltip: "FCSettingMaxConcurrentBattleMapsTip".Translate());

            defenseMapBaseSize = ls.SliderTextField("FCSettingDefenseMapBaseSize",
                "FCSettingDefenseMapBaseSize".Translate(), defenseMapBaseSize, 50, 250,
                tooltip: "FCSettingDefenseMapBaseSizeTip".Translate());

            defenseMapPerLevelStep = ls.SliderTextField("FCSettingDefenseMapPerLevelStep",
                "FCSettingDefenseMapPerLevelStep".Translate(), defenseMapPerLevelStep, 0, 20,
                tooltip: "FCSettingDefenseMapPerLevelStepTip".Translate());

            int oldMinSize = defenseMapMinSize;
            defenseMapMinSize = ls.SliderTextField("FCSettingDefenseMapMinSize",
                "FCSettingDefenseMapMinSize".Translate(), defenseMapMinSize, 60, 250,
                tooltip: "FCSettingDefenseMapMinSizeTip".Translate());

            defenseMapMaxSize = ls.SliderTextField("FCSettingDefenseMapMaxSize",
                "FCSettingDefenseMapMaxSize".Translate(), defenseMapMaxSize, 100, 500,
                tooltip: "FCSettingDefenseMapMaxSizeTip".Translate());

            // Keep the cap >= the floor: nudge whichever slider the player didn't just move.
            if (defenseMapMaxSize < defenseMapMinSize)
            {
                if (defenseMapMinSize != oldMinSize) defenseMapMaxSize = defenseMapMinSize; // raised the floor -> raise the cap
                else defenseMapMinSize = defenseMapMaxSize;                                 // lowered the cap   -> lower the floor
            }

            efficiencyDamping = ls.SliderTextField("FCSettingEfficiencyDamping",
                "FCSettingEfficiencyDamping".Translate(), efficiencyDamping, 0.0f, 1.0f, decimals: 2,
                tooltip: "FCSettingEfficiencyDampingTooltip".Translate());

            mercenaryHealRatePerHour = ls.SliderTextField("FCSettingMercHealRate",
                "FCSettingMercHealRate".Translate(), mercenaryHealRatePerHour, 0.1f, 100f, decimals: 2, unit: "x",
                tooltip: "FCSettingMercHealRateTip".Translate());

            militaryMechRepairRate = ls.SliderTextField("FCSettingMechRepairRate",
                "FCSettingMechRepairRate".Translate(), militaryMechRepairRate, 0f, 50f, decimals: 0, unit: "HP",
                tooltip: "FCSettingMechRepairRateTip".Translate());

            DrawSectionResetButton(ls, ResetMilitaryActionToDefaults);

            ls.Gap(12f);
            ls.GapLine();
            Text.Font = GameFont.Medium;
            ls.Label("FCSettingAutoResolveCasualtiesHeader".Translate());
            Text.Font = GameFont.Small;

            ls.CheckboxLabeled("FCSettingApplyAutoResolveInjuries".Translate(), ref applyAutoResolveInjuries, "FCSettingApplyAutoResolveInjuriesTip".Translate());

            autoResolveCasualtyDeathThreshold = ls.SliderTextField("FCSettingAutoResolveDeathThreshold",
                "FCSettingAutoResolveDeathThreshold".Translate(), autoResolveCasualtyDeathThreshold, 0.0f, 1.0f, decimals: 2,
                tooltip: "FCSettingAutoResolveDeathThresholdTip".Translate());

            autoResolveCasualtyMaxDeathFraction = ls.SliderTextField("FCSettingAutoResolveMaxDeathFraction",
                "FCSettingAutoResolveMaxDeathFraction".Translate(), autoResolveCasualtyMaxDeathFraction, 0.0f, 1.0f, decimals: 2,
                tooltip: "FCSettingAutoResolveMaxDeathFractionTip".Translate());

            crushingDefeatPenaltyMultiplier = ls.SliderTextField("FCSettingCrushingDefeatPenaltyMultiplier",
                "FCSettingCrushingDefeatPenaltyMultiplier".Translate(), crushingDefeatPenaltyMultiplier, 1.0f, 5.0f, decimals: 2, unit: "x",
                tooltip: "FCSettingCrushingDefeatPenaltyMultiplierTip".Translate());

            overwhelmingVictoryRewardMultiplier = ls.SliderTextField("FCSettingOverwhelmingVictoryRewardMultiplier",
                "FCSettingOverwhelmingVictoryRewardMultiplier".Translate(), overwhelmingVictoryRewardMultiplier, 0.0f, 5.0f, decimals: 2, unit: "x",
                tooltip: "FCSettingOverwhelmingVictoryRewardMultiplierTip".Translate());

            ls.CheckboxLabeled("FCSettingRespectLethalDamageThreshold".Translate(), ref respectLethalDamageThreshold, "FCSettingRespectLethalDamageThresholdTip".Translate());

            DrawSectionResetButton(ls, ResetAutoResolveToDefaults);

            ls.Gap(12f);
            ls.GapLine();
            Text.Font = GameFont.Medium;
            ls.Label("FCSettingSquadsHeader".Translate());
            Text.Font = GameFont.Small;

            maxSquadSize = ls.SliderTextField("FCSettingMaxSquadSize",
                "FCSettingMaxSquadSize".Translate(), maxSquadSize, 1, 60,
                tooltip: "FCSettingMaxSquadSizeTip".Translate());

            string tip = FactionCompat.GiddyUp2Active ? "FCSettingMaxAnimalSubpawnsGiddyUpTip".Translate() : "FCSettingMaxAnimalSubpawnsTip".Translate();
            maxAnimalSubpawns = ls.SliderTextField("FCSettingMaxAnimalSubpawns",
                "FCSettingMaxAnimalSubpawns".Translate(), maxAnimalSubpawns, MIN_ANIMAL_SUBPAWNS, MAX_ANIMAL_SUBPAWNS_SLIDER,
                tooltip: tip);

            squadHireCostMultiplier = ls.SliderTextField("FCSettingSquadHireCostMultiplier",
                "FCSettingSquadHireCostMultiplier".Translate(), squadHireCostMultiplier, 0.0f, 5.0f, decimals: 2, unit: "x",
                tooltip: "FCSettingSquadHireCostMultiplierTip".Translate());

            squadUpgradeCostMultiplier = ls.SliderTextField("FCSettingSquadUpgradeCostMultiplier",
                "FCSettingSquadUpgradeCostMultiplier".Translate(), squadUpgradeCostMultiplier, 0.0f, 5.0f, decimals: 2, unit: "x",
                tooltip: "FCSettingSquadUpgradeCostMultiplierTip".Translate());

            squadDeploymentCostPercentage = ls.SliderTextField("FCSettingSquadDeploymentCostPercentage",
                "FCSettingSquadDeploymentCostPercentage".Translate(), squadDeploymentCostPercentage, 0.0f, 1.0f, decimals: 2,
                tooltip: "FCSettingSquadDeploymentCostPercentageTip".Translate());

            deploymentBillLifespan_days = ls.SliderTextField("FCSettingDeploymentBillLifespan",
                "FCSettingDeploymentBillLifespan".Translate(), deploymentBillLifespan_days, 1, 60, unit: "d",
                tooltip: "FCSettingDeploymentBillLifespanTip".Translate());

            squadRestockCostPercentage = ls.SliderTextField("FCSettingSquadRestockCostPercentage",
                "FCSettingSquadRestockCostPercentage".Translate(), squadRestockCostPercentage, 0.0f, 1.0f, decimals: 2,
                tooltip: "FCSettingSquadRestockCostPercentageTip".Translate());

            // Vanilla psylink cost (base-game psycasts). Hidden when VPE is active — VPE makes psylink
            // levels free and charges per chosen psycast instead (see the Compatibility tab).
            // Only shows if Royalty is active (psylinks come with Royalty, after all)
            if (!FactionCompat.VPEActive && ModsConfig.RoyaltyActive)
            {
                militaryPsylinkCostMultiplier = ls.SliderTextField("FCSettingPsylinkCostMult",
                    "FCSettingPsylinkCostMult".Translate(), (float)militaryPsylinkCostMultiplier, 0f, 5f, decimals: 2, unit: "x",
                    tooltip: "FCSettingPsylinkCostMultTip".Translate());
            }

            // Mechanitor merc costs (Biotech only).
            if (ModsConfig.BiotechActive)
            {
                militaryMechCostMultiplier = ls.SliderTextField("FCSettingMechCostMult",
                    "FCSettingMechCostMult".Translate(), (float)militaryMechCostMultiplier, 0f, 5f, decimals: 2, unit: "x",
                    tooltip: "FCSettingMechCostMultTip".Translate());

                militaryMechlinkCost = ls.SliderTextField("FCSettingMechlinkCost",
                    "FCSettingMechlinkCost".Translate(), (float)militaryMechlinkCost, 0f, 5000f, decimals: 0,
                    tooltip: "FCSettingMechlinkCostTip".Translate());
            }

            ls.Gap(8f);
            if (ModsConfig.BiotechActive)
            {
                Text.Font = GameFont.Medium;
                ls.Label("FCSettingGeneValueHeader".Translate());
                Text.Font = GameFont.Small;
                ls.Label("FCSettingGeneValueHelpText".Translate(), -1f);

                geneValueWeightMvf = ls.SliderTextField("FCSettingGeneValueWeightMvf",
                    "FCSettingGeneValueWeightMvf".Translate(), geneValueWeightMvf, 0f, 3f, decimals: 2,
                    tooltip: "FCSettingGeneValueWeightMvfTip".Translate());

                geneValueWeightMet = ls.SliderTextField("FCSettingGeneValueWeightMet",
                    "FCSettingGeneValueWeightMet".Translate(), geneValueWeightMet, 0f, 0.5f, decimals: 2,
                    tooltip: "FCSettingGeneValueWeightMetTip".Translate());

                geneValueWeightArc = ls.SliderTextField("FCSettingGeneValueWeightArc",
                    "FCSettingGeneValueWeightArc".Translate(), geneValueWeightArc, 0f, 2f, decimals: 2,
                    tooltip: "FCSettingGeneValueWeightArcTip".Translate());

                geneValueWeightEffects = ls.SliderTextField("FCSettingGeneValueWeightEffects",
                    "FCSettingGeneValueWeightEffects".Translate(), geneValueWeightEffects, 0f, 2f, decimals: 2,
                    tooltip: "FCSettingGeneValueWeightEffectsTip".Translate());

                geneValueWeightPain = ls.SliderTextField("FCSettingGeneValueWeightPain",
                    "FCSettingGeneValueWeightPain".Translate(), geneValueWeightPain, 0f, 2f, decimals: 2,
                    tooltip: "FCSettingGeneValueWeightPainTip".Translate());

                geneValueWeightDmgResist = ls.SliderTextField("FCSettingGeneValueWeightDmgResist",
                    "FCSettingGeneValueWeightDmgResist".Translate(), geneValueWeightDmgResist, 0f, 2f, decimals: 2,
                    tooltip: "FCSettingGeneValueWeightDmgResistTip".Translate());

                ls.CheckboxLabeled("FCSettingGeneValueFactorUnlimited".Translate(), ref geneValueFactorUnlimited, "FCSettingGeneValueFactorUnlimitedTip".Translate());
                if (!geneValueFactorUnlimited)
                {
                    geneValueMaxFactor = ls.SliderTextField("FCSettingGeneValueMaxFactor",
                        "FCSettingGeneValueMaxFactor".Translate(), geneValueMaxFactor, MIN_GENE_MAX_FACTOR, MAX_GENE_MAX_FACTOR, decimals: 2, unit: "x",
                        tooltip: "FCSettingGeneValueMaxFactorTip".Translate());
                }
            }

            DrawSectionResetButton(ls, ResetSquadsAndGenesToDefaults);

            ls.Gap(12f);
            ls.GapLine();
            Text.Font = GameFont.Medium;
            ls.Label("FCBattleArchiveSettingsHeader".Translate());
            Text.Font = GameFont.Small;

            ls.CheckboxLabeled("FCBattleArchiveUnlimited".Translate(), ref battleArchiveUnlimited, "FCBattleArchiveUnlimitedTip".Translate());
            if (!battleArchiveUnlimited)
            {
                battleArchiveMaxEntries = ls.SliderTextField("FCBattleArchiveMaxEntriesLabel",
                    "FCBattleArchiveMaxEntriesLabel".Translate(), battleArchiveMaxEntries, MIN_BATTLE_ARCHIVE_MAX_ENTRIES, MAX_BATTLE_ARCHIVE_MAX_ENTRIES,
                    tooltip: "FCBattleArchiveMaxEntriesTip".Translate());
            }

            DrawSectionResetButton(ls, ResetBattleArchiveToDefaults);

            contentHeightMilitary = ls.CurHeight + 12f;
            ls.End();

            ScrollUtil.EndScrollView();
        }

        private void DoRoadBuilderTab(Rect rect)
        {
            Rect viewRect = ScrollUtil.BeginScrollView(rect, ref scrollVectorRoadBuilder, contentHeightRoadBuilder);
            Rect listRect = new Rect(viewRect.x, viewRect.y, viewRect.width, float.MaxValue);
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(listRect);
            Listing_StandardExtensions.ResetRowStripe();

            // Description box
            Rect descRect = ls.GetRect(Text.CalcHeight("FCSettingRoadBuilderDesc".Translate(), listRect.width - 16f) + 16f);
            Widgets.DrawBoxSolid(descRect, new Color(0.15f, 0.15f, 0.15f, 0.5f));
            Widgets.DrawBox(descRect);
            Text.Font = GameFont.Small;
            Widgets.Label(descRect.ContractedBy(8f), "FCSettingRoadBuilderDesc".Translate());

            ls.Gap(12f);

            ls.CheckboxLabeled("FCSettingUseThreadedRoadComputation".Translate(), ref useThreadedRoadComputation, "FCSettingUseThreadedRoadComputationDesc".Translate());
            if (!useThreadedRoadComputation)
            {
                edgesPerRoadTick = ls.SliderTextField("FCSettingEdgesPerRoadTick",
                    "FCSettingEdgesPerRoadTick".Translate(), edgesPerRoadTick, 0, 50,
                    tooltip: "FCSettingEdgesPerRoadTickTip".Translate());
            }

            roadBuildIntervalDays = ls.SliderTextField("FCSettingRoadBuildInterval",
                "FCSettingRoadBuildInterval".Translate(), roadBuildIntervalDays, 1, 30, unit: "d",
                tooltip: "FCSettingRoadBuildIntervalTip".Translate());

            ls.Gap(12f);
            FCRoadQueue queue = FindFC.RoadBuilder?.roadQueue;
            if (queue is object && ls.ButtonText("FCSettingFlushRoadCache".Translate()))
            {
                queue.FlushCache();
            }

            ls.Gap(12f);
            DrawSectionResetButton(ls, ResetRoadBuilderToDefaults);

            contentHeightRoadBuilder = ls.CurHeight + 12f;
            ls.End();

            ScrollUtil.EndScrollView();
        }

        /* Settings for compatibility patches. Each supported mod gets its own section, shown only
         * when that mod is loaded. The fields live in core (FCSettings) so they persist regardless,
         * but a section is hidden unless its mod is active. */
        private void DoCompatTab(Rect rect)
        {
            Rect viewRect = ScrollUtil.BeginScrollView(rect, ref scrollVectorCompat, contentHeightCompat);
            Rect listRect = new Rect(viewRect.x, viewRect.y, viewRect.width, float.MaxValue);
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(listRect);
            Listing_StandardExtensions.ResetRowStripe();

            bool any = false;

            /* -- Vanilla Psycasts Expanded -- */
            if (FactionCompat.VPEActive)
            {
                any = true;
                Text.Font = GameFont.Medium;
                ls.Label("FCSettingsCompatVPE".Translate());
                Text.Font = GameFont.Small;
                ls.GapLine();
                ls.Label("FCSettingsCompatVPEDesc".Translate());
                ls.Gap(6f);

                vpePsycastBaseCost = ls.SliderTextField("FCSettingVPEBasePsycastCost",
                    "FCSettingVPEBasePsycastCost".Translate(), vpePsycastBaseCost, 0, 2000,
                    tooltip: "FCSettingVPEBasePsycastCostTip".Translate());

                vpePsycastPerLevelCost = ls.SliderTextField("FCSettingVPEPerLevelCost",
                    "FCSettingVPEPerLevelCost".Translate(), vpePsycastPerLevelCost, 0, 2000,
                    tooltip: "FCSettingVPEPerLevelCostTip".Translate());

                vpeFocusCost = ls.SliderTextField("FCSettingVPEFocusCost",
                    "FCSettingVPEFocusCost".Translate(), vpeFocusCost, 0, 2000,
                    tooltip: "FCSettingVPEFocusCostTip".Translate());

                vpeStatPointCost = ls.SliderTextField("FCSettingVPEStatPointCost",
                    "FCSettingVPEStatPointCost".Translate(), vpeStatPointCost, 0, 2000,
                    tooltip: "FCSettingVPEStatPointCostTip".Translate());

                militaryPsycastCostMultiplier = ls.SliderTextField("FCSettingPsycastCostMult",
                    "FCSettingPsycastCostMult".Translate(), (float)militaryPsycastCostMultiplier, 0f, 5f, decimals: 2, unit: "x",
                    tooltip: "FCSettingPsycastCostMultTip".Translate());

                DrawSectionResetButton(ls, ResetCompatToDefaults);
            }

            if (!any)
                ls.Label("FCSettingsCompatNone".Translate());

            contentHeightCompat = ls.CurHeight + 12f;
            ls.End();

            ScrollUtil.EndScrollView();
        }

        /// <summary>
        /// Draws a compact, left-aligned "Reset Section to Defaults" button into the given
        /// listing and invokes <paramref name="resetAction"/> when clicked.
        /// </summary>
        private void DrawSectionResetButton(Listing_Standard ls, Action resetAction, string label = null)
        {
            string key = label ?? "FCSettingResetSection".Translate();
            ls.Gap(4f);
            Rect row = ls.GetRect(28f);
            Rect btn = new Rect(row.x, row.y, row.width, row.height);
            if (UIUtil.ClampedButtonText(btn, key)) resetAction();
        }
    }


    public class FactionColoniesMod : Mod
    {
        public FCSettings settings = new FCSettings();

        public FactionColoniesMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<FCSettings>();

            string modVersion = content?.ModMetaData?.ModVersion;
            if (modVersion.NullOrEmpty())
            {
                LogUtil.MessageForce("Did not load a mod version");
            }
            else
            {
                LogUtil.MessageForce($"v{modVersion}");
            }

            // Reliable active version for the settings-format stamp/migration (set before any WriteSettings,
            // which stamps settingsModVersion from activeModVersion via FCSettings.ExposeData's Saving branch).
            FCSettings.activeModVersion = modVersion.NullOrEmpty() ? null : modVersion;
            if (FCSettings.MigrateSettingsFormat()) WriteSettings();

            FactionCompat.CheckForMods();
        }

        public override string SettingsCategory()
        {
            return "FCSettingsModName".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect) => settings.DoWindowContents(inRect);
    }
}
