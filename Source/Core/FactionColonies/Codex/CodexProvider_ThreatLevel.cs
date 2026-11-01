using System;
using System.Linq;
using Verse;
using RimWorld;

namespace FactionColonies
{
    /// <summary>
    /// Dynamic provider for the raid-strength Codex entry. Raids are sized by each attacking
    /// faction's own defined power (no empire-wide multiplier); this shows the player's
    /// average military level — which biases which factions attack — and the early-game raid
    /// cap while it is still active.
    /// </summary>
    public class CodexProvider_ThreatLevel : ICodexDynamicProvider
    {
        public string GetDynamicContent(FactionFC faction)
        {
            if (!faction.settlements.Any())
                return "FCCodexRaidNoSettlements".Translate();

            double avgMilitaryLevel = faction.settlements.Average(s => (double)s.settlementMilitaryLevel);

            double raidsBeganTick = faction.timeStart + GenDate.TicksPerSeason;
            double daysSinceRaidsBegan = (Find.TickManager.TicksGame - raidsBeganTick)
                                       / (double)GenDate.TicksPerDay;

            string result = "FCCodexRaidAvgMilLevel".Translate(Math.Round(avgMilitaryLevel, 1)) + "\n";
            result += "FCCodexRaidTargetBand".Translate(
                Math.Round(Math.Max(0.0, avgMilitaryLevel - 2.0), 1),
                Math.Round(avgMilitaryLevel + 2.0, 1)) + "\n\n";

            if (daysSinceRaidsBegan < 0.0)
            {
                result += "FCCodexRaidNotStarted".Translate(
                    Math.Max(0, (int)Math.Ceiling(-daysSinceRaidsBegan)));
            }
            else if (daysSinceRaidsBegan < 30.0)
            {
                double earlyCap = ThreatScalingUtil.ComputeEarlyGameRaidCap(faction);
                result += "FCCodexRaidEarlyCapActive".Translate(
                    Math.Round(earlyCap, 1),
                    Math.Max(0, (int)Math.Ceiling(30.0 - daysSinceRaidsBegan)));
            }
            else
            {
                result += "FCCodexRaidEarlyCapInactive".Translate();
            }

            return result;
        }
    }
}
