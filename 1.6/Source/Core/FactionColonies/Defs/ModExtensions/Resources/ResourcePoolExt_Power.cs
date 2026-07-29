using System;

namespace FactionColonies
{
    public class ResourcePoolExt_Power : ResourcePoolExtension
    {
        public override double CreatePool(double production, WorldSettlementFC settlement = null)
        {
            // Scale power by the economy's per-day resource value so it inherits the same daily-cadence
            // normalization as silver income, instead of a raw constant that ignores the tax interval.
            return Math.Round(production * FCSettings.silverPerResource);
        }
        public override bool ResetAtTaxTime()
        {
            return true;
        }
    }
}
