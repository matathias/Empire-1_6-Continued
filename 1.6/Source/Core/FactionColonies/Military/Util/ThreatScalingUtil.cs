using RimWorld;
using System;
using System.Linq;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Raid-scaling helpers. Live: the early-game raid cap, attack-frequency scaling, and
    /// weighted enemy-faction selection. Dormant (retained for a future threat-scaling submod
    /// and the test suite): the Empire Threat Level and the old handicap cap.
    /// </summary>
    public static class ThreatScalingUtil
    {
        /* Income curve: maps faction income to a 0–0.9 factor */
        private static readonly SimpleCurve IncomeCurve = new SimpleCurve
        {
            new CurvePoint(0f, 0f),
            new CurvePoint(500f, 0.1f),
            new CurvePoint(2000f, 0.3f),
            new CurvePoint(5000f, 0.5f),
            new CurvePoint(10000f, 0.7f),
            new CurvePoint(20000f, 0.9f)
        };

        /* Count curve: maps settlement count to a 0–0.6 factor */
        private static readonly SimpleCurve CountCurve = new SimpleCurve
        {
            new CurvePoint(1f, 0f),
            new CurvePoint(3f, 0.2f),
            new CurvePoint(5f, 0.3f),
            new CurvePoint(10f, 0.5f),
            new CurvePoint(15f, 0.6f)
        };

        /* Time curve for the dormant handicap cap: maps seasons elapsed to a cap */
        private static readonly SimpleCurve TimeCurve = new SimpleCurve
        {
            new CurvePoint(0f, 2f),
            new CurvePoint(1f, 3f),
            new CurvePoint(2f, 4.5f),
            new CurvePoint(4f, 8f),
            new CurvePoint(8f, 15f)
        };

        /* Early-game raid cap: softens raids for the first 30 days after raids begin, then
           rises above any faction level (max ~9 + variance ~2) so it no longer binds.
           X axis = days since raids began. */
        private static readonly SimpleCurve EarlyGameCapCurve = new SimpleCurve
        {
            new CurvePoint(0f, 2f),
            new CurvePoint(30f, 12f),
            new CurvePoint(60f, 100f)
        };

        /* Frequency curve: maps settlement count to a frequency multiplier */
        private static readonly SimpleCurve FrequencyCurve = new SimpleCurve
        {
            new CurvePoint(1f, 1.0f),
            new CurvePoint(3f, 1.2f),
            new CurvePoint(5f, 1.4f),
            new CurvePoint(10f, 1.8f),
            new CurvePoint(15f, 2.0f)
        };

        /// <summary>
        /// DORMANT — not used by the live raid path; retained for a future threat-scaling
        /// submod and the test suite. Computes the Empire Threat Level (ETL): a composite
        /// multiplier based on average settlement level, max settlement level, income,
        /// settlement count, FCStatDef modifiers, and registry contributions. Clamped between
        /// 1.0 and <see cref="FCSettings.maxThreatMultiplier"/>.
        /// </summary>
        public static double ComputeEmpireThreatLevel(FactionFC faction)
        {
            if (!faction.settlements.Any()) return 1.0;
            return Math.Max(1.0, Math.Min(FCSettings.maxThreatMultiplier, ComputeRawEmpireScale(faction)));
        }

        /// <summary>
        /// Empire-scale composite (avg/max settlement level, income, settlement count, plus
        /// FCStatDef and registry modifiers), floored at 1.0 with no upper cap. Used for cost
        /// scaling that should keep growing with the empire (e.g., policy re-pick cost).
        /// </summary>
        public static double ComputeEmpireScaleUncapped(FactionFC faction)
        {
            if (!faction.settlements.Any()) return 1.0;
            return Math.Max(1.0, ComputeRawEmpireScale(faction));
        }

        private static double ComputeRawEmpireScale(FactionFC faction)
        {
            double avgLevel = faction.settlements.Average(s => (double)s.settlementLevel);
            double avgFactor = (avgLevel - 1.0) * 0.2; // lvl 1->0, lvl 5->0.8, lvl 10->1.8

            int maxLevel = faction.settlements.Max(s => s.settlementLevel);
            double maxFactor = (maxLevel - 1.0) * 0.1; // lvl 1->0, lvl 5->0.4, lvl 10->0.9

            double incomeFactor = IncomeCurve.Evaluate((float)faction.income);

            double countFactor = CountCurve.Evaluate(faction.settlements.Count);

            double rawScore = (avgFactor * 0.35) + (maxFactor * 0.15)
                            + (incomeFactor * 0.30) + (countFactor * 0.20);

            // Stat-driven modifiers (policies/buildings)
            double statBase = faction.GetStatValue(FCStatDefOf.threatScalingBase);
            double statMult = faction.GetStatValue(FCStatDefOf.threatScalingMultiplier);

            // Registry contributions (submods)
            double registryBase = ThreatScalingRegistry.InvokeGetAdditiveContributions(faction);
            double registryMult = ThreatScalingRegistry.InvokeGetMultiplierContributions(faction);

            return (1.0 + rawScore + statBase + registryBase) * statMult * registryMult;
        }

        /// <summary>
        /// DORMANT — not used by the live raid path (incoming raids use
        /// <see cref="ComputeEarlyGameRaidCap"/>); retained for a future threat-scaling submod
        /// and the test suite. During the first year it's primarily time-based; after that it
        /// transitions to ETL-based scaling.
        /// </summary>
        public static double ComputeHandicapCap(FactionFC faction)
        {
            double seasonsElapsed = (double)(Find.TickManager.TicksGame - faction.timeStart)
                                  / GenDate.TicksPerSeason;
            double timeCap = TimeCurve.Evaluate((float)seasonsElapsed);

            double etlCap = ComputeEmpireThreatLevel(faction) * 5.0;

            double graceFade = Math.Min(1.0, seasonsElapsed / 4.0);
            return Math.Max(2.0, timeCap * (1.0 - graceFade) + etlCap * graceFade);
        }

        /// <summary>
        /// Early-game raid level cap applied to incoming raids. Softens raids for the first
        /// 30 days after raids begin (raids start at <c>timeStart + 1 season</c>), then rises
        /// above any faction's level so it stops binding.
        /// </summary>
        public static double ComputeEarlyGameRaidCap(FactionFC faction)
        {
            double raidsBeganTick = faction.timeStart + GenDate.TicksPerSeason;
            double daysSinceRaidsBegan = (Find.TickManager.TicksGame - raidsBeganTick)
                                       / (double)GenDate.TicksPerDay;
            return Math.Max(2.0, EarlyGameCapCurve.Evaluate((float)daysSinceRaidsBegan));
        }

        /// <summary>
        /// Picks a random hostile faction, weighted to favor factions whose defined power
        /// level close to the empire's average settlement military level. Factions
        /// far from the player's tier are rare but never fully excluded, so raids track the
        /// player's military development rather than empire size.
        /// </summary>
        public static Faction PickWeightedEnemyFaction(FactionFC faction)
        {
            var enemies = Find.FactionManager.AllFactionsVisible
                .Where(f => f.HostileTo(Faction.OfPlayer) && !f.defeated && !f.Hidden).ToList();
            if (!enemies.Any()) return null;

            double avgMilitaryLevel = faction.settlements.Any()
                ? faction.settlements.Average(s => (double)s.settlementMilitaryLevel)
                : 0.0;

            return enemies.RandomElementByWeight(f =>
            {
                // Extra NPC offensive levels raise each faction's effective level before weighting,
                // so a normally low-tier faction is treated (and later fielded) as if higher-level.
                double factionLevel = (FindFC.EnemyPower?.GetOrCompute(f)?.level ?? 1.0)
                                      + FCSettings.extraNPCOffensiveLevels;
                return (float)ComputeFactionSelectionWeight(factionLevel, avgMilitaryLevel);
            });
        }

        /// <summary>
        /// Selection weight for an enemy faction, favoring factions whose defined power level is
        /// close to the empire's average settlement military level. Full weight (1.0) at even
        /// level; falls off linearly by 0.25 per level of distance (+/-1 -> 0.75, +/-2 -> 0.5,
        /// +/-3 -> 0.25) and is floored at 0.05 so distant tiers are rare but never excluded.
        /// Pure seam for <see cref="PickWeightedEnemyFaction"/>.
        /// </summary>
        public static double ComputeFactionSelectionWeight(double factionLevel, double avgMilitaryLevel)
        {
            double distance = Math.Abs(factionLevel - avgMilitaryLevel);
            return Math.Max(0.05, 1.0 - (distance * 0.25));
        }

        /// <summary>
        /// Returns a frequency multiplier for attack intervals based on settlement count.
        /// The result is used to bias the random interval toward the lower bound,
        /// but the player's min/max settings are never violated.
        /// </summary>
        public static int ComputeScaledAttackInterval(FactionFC faction)
        {
            double freqMult = FrequencyCurve.Evaluate(faction.settlements.Count);

            IntRange range = FCSettings.minMaxDaysTillMilitaryAction;
            int baseDays = range.RandomInRange;
            int adjustedDays = (int)Math.Round(baseDays / freqMult);
            adjustedDays = Math.Max(range.min, Math.Min(range.max, adjustedDays));

            return adjustedDays;
        }
    }
}
