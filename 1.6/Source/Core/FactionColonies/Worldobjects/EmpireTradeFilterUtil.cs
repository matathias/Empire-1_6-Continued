using RimWorld;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Shared buy-side filtering for Empire traders. Provides a blocklist of items that
    /// should never be traded, a common essentials filter that any trader will accept, and
    /// resource-matching helpers for specialized traders.
    /// </summary>
    public static class EmpireTradeFilterUtil
    {
        private static ThingFilter _commonEssentials;
        private static ThingFilter _allResourceThingDefs;

        /// <summary>
        /// Returns true if the item should be categorically rejected by all Empire traders.
        /// </summary>
        public static bool ShouldReject(ThingDef thingDef)
        {
            if (thingDef is null)
                return true;

            if (thingDef.destroyOnDrop)
                return true;

            if (!thingDef.genericMarketSellable)
                return true;

            if (thingDef.category != ThingCategory.Item
                && thingDef.category != ThingCategory.Pawn)
                return true;

            if (thingDef.BaseMarketValue <= 0f)
                return true;

            if (thingDef.thingCategories is object
                && (thingDef.thingCategories.Contains(ThingCategoryDefOf.Chunks)
                    || thingDef.thingCategories.Contains(ThingCategoryDefOf.StoneChunks)))
                return true;

            return false;
        }

        /// <summary>
        /// Returns true if the item is a common essential that any Empire trader would accept:
        /// food, medicine, or non-armor apparel.
        /// </summary>
        public static bool IsCommonEssential(ThingDef thingDef)
        {
            if (_commonEssentials is null)
                _commonEssentials = BuildCommonEssentials();
            return _commonEssentials.Allows(thingDef);
        }

        /// <summary>
        /// Returns true if the item belongs to any non-pool resource type.
        /// Used by the settlement trader which deals in all resources.
        /// </summary>
        public static bool MatchesAnyResource(ThingDef thingDef)
        {
            if (_allResourceThingDefs is null)
                _allResourceThingDefs = BuildAllResourceThingDefs();
            return _allResourceThingDefs.Allows(thingDef);
        }

        private static ThingFilter BuildCommonEssentials()
        {
            ThingFilter filter = new ThingFilter();
            filter.SetAllow(ThingCategoryDefOf.Foods, true);
            filter.SetAllow(ThingCategoryDefOf.Medicine, true);
            filter.SetAllow(ThingCategoryDefOf.Apparel, true);
            filter.SetAllow(ThingCategoryDefOf.ApparelArmor, false);
            return filter;
        }

        private static ThingFilter BuildAllResourceThingDefs()
        {
            ThingFilter filter = new ThingFilter();
            foreach (ResourceTypeDef rtd in FactionCache.NonPoolResourceTypeDefs)
            {
                rtd.FilterResourceForTrade(filter);
            }
            return filter;
        }
    }
}
