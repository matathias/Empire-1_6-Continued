using System;

namespace FactionColonies.util
{
    /* Shared scaling rules for event mechanics that key off "how many settlements does this
     * event affect." Used by both FCOptionCostUtil (silver-cost multiplier) and FCEventMaker
     * (item-reward multiplier) so the two stay in lockstep. */
    public static class FCEventScalingUtil
    {
        /// <summary>
        /// Count of non-null entries in <c>evt.settlementTraitLocations</c>, floored at 1.
        /// The floor ensures untargeted faction-scope events still apply the base value rather
        /// than collapsing to zero.
        /// </summary>
        public static int CountAffectedSettlements(FCEvent evt)
        {
            if (evt is null) return 1;
            int count = 0;
            if (evt.settlementTraitLocations is object)
            {
                foreach (WorldSettlementFC s in evt.settlementTraitLocations)
                {
                    if (s is object) count++;
                }
            }
            return Math.Max(1, count);
        }
    }
}
