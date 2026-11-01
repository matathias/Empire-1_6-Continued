using System;

namespace FactionColonies
{
    /// <summary>
    /// Silver cost formula for clearing and re-picking core policies. Scales with empire
    /// size (via the uncapped threat-level composite) and average prosperity.
    /// </summary>
    public static class PolicyRepickCost
    {
        public const int BaseCost = 2000;
        private const double MinProsperityFactor = 0.25;

        public static int Compute(FactionFC faction)
        {
            if (faction is null) return BaseCost;
            double scale = ThreatScalingUtil.ComputeEmpireScaleUncapped(faction);
            double prosperityFactor = Math.Max(MinProsperityFactor, faction.averageProsperity / 100.0);
            return (int)Math.Round(BaseCost * scale * prosperityFactor);
        }
    }
}
