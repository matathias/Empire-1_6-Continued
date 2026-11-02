namespace FactionColonies;

/// <summary>
/// Resolves which items a unit may carry. Blocklist model: the base pool is every ThingDef,
/// carved down by the <see cref="FCInventoryCategoryDef.excludeCategories"/>/<see
/// cref="FCInventoryCategoryDef.excludeThings"/> of all defs, with <see
/// cref="FCInventoryCategoryDef.categories"/>/<see cref="FCInventoryCategoryDef.things"/>
/// force-allowing items back. Submods restrict the pool by shipping their own def; items a mod
/// adds under any category appear automatically. The result is cached per game session (def
/// databases don't change at runtime).
/// </summary>
public static class MilitaryInventoryUtil
{
    private static HashSet<ThingDef> allowedCache;

    /// <summary>The full carry-inventory pool, ignoring research/availability (that gate is
    /// applied at list-build time via <see cref="CraftUtil.CanCraftItem"/>). Blocklist model:
    /// the base pool is every ThingDef, carved down by excludeCategories/excludeThings across
    /// all defs; categories/things force-allow back (allow wins over exclude).</summary>
    public static HashSet<ThingDef> AllowedItems()
    {
        if (allowedCache is not null) return allowedCache;

        HashSet<ThingDef> excluded = new HashSet<ThingDef>();
        HashSet<ThingDef> forceAllow = new HashSet<ThingDef>();
        foreach (FCInventoryCategoryDef def in DefDatabase<FCInventoryCategoryDef>.AllDefs)
        {
            if (def.excludeCategories is not null)
                foreach (ThingCategoryDef cat in def.excludeCategories)
                    if (cat is not null)
                        foreach (ThingDef t in cat.DescendantThingDefs)
                            excluded.Add(t);
            if (def.excludeThings is not null)
                foreach (ThingDef t in def.excludeThings)
                    if (t is not null)
                        excluded.Add(t);

            if (def.categories is not null)
                foreach (ThingCategoryDef cat in def.categories)
                    if (cat is not null)
                        foreach (ThingDef t in cat.DescendantThingDefs)
                            forceAllow.Add(t);
            if (def.things is not null)
                foreach (ThingDef t in def.things)
                    if (t is not null)
                        forceAllow.Add(t);
        }

        IEnumerable<ThingDef> basePool = DefDatabase<ThingDef>.AllDefs.Where(t => !IsNeverCarriable(t));
        allowedCache = ComputeAllowed(basePool, excluded, forceAllow);
        return allowedCache;
    }

    /// <summary>Structural rules for things a pawn is never meant to carry, independent of the
    /// category blocklist. A def force-allowed via <see cref="FCInventoryCategoryDef.categories"/>/
    /// <see cref="FCInventoryCategoryDef.things"/> still overrides these (allow wins).</summary>
    public static bool IsNeverCarriable(ThingDef t)
    {
        if (t is null) return true;
        // All buildings (walls, turrets/mortars, workbenches, minifiable furniture, power, etc.).
        // ThingDef.category is authoritative — many buildings declare no thingCategories at all,
        // so the "Buildings" ThingCategoryDef would miss them; this catches every building.
        if (t.category == ThingCategory.Building) return true;
        // Minified wrappers are category Item (not Building), so catch them by class.
        if (typeof(MinifiedThing).IsAssignableFrom(t.thingClass)) return true;
        // A market value of 0 means it isn't a tradeable/carryable good.
        if (t.BaseMarketValue <= 0f) return true;
        return false;
    }

    /// <summary>Pure set algebra: <c>(basePool - excluded) ∪ forceAllow</c>. Force-allow wins
    /// over excludes (the un-block override). Extracted from <see cref="AllowedItems"/> so the
    /// precedence rules can be unit-tested with synthetic defs.</summary>
    public static HashSet<ThingDef> ComputeAllowed(IEnumerable<ThingDef> basePool,
        IEnumerable<ThingDef> excluded, IEnumerable<ThingDef> forceAllow)
    {
        HashSet<ThingDef> excludedSet = excluded as HashSet<ThingDef> ?? new HashSet<ThingDef>(excluded);
        HashSet<ThingDef> result = new HashSet<ThingDef>();
        foreach (ThingDef t in basePool)
            if (t is not null && !excludedSet.Contains(t))
                result.Add(t);
        foreach (ThingDef t in forceAllow)
            if (t is not null)
                result.Add(t);
        return result;
    }

    /// <summary>Whether <paramref name="thing"/> is allowed in inventory at all (pool membership,
    /// before the research/availability gate).</summary>
    public static bool IsAllowed(ThingDef thing) => thing is not null && AllowedItems().Contains(thing);

    /// <summary>Whitelisted items the player can currently carry: in the whitelist, haulable, and
    /// unlocked by research (same gate the apparel/weapon pickers use).</summary>
    public static List<ThingDef> AvailableItems()
    {
        return AllowedItems()
            .Where(t => t.EverHaulable && !t.IsCorpse && CraftUtil.CanCraftItem(t))
            .OrderBy(t => t.label)
            .ToList();
    }
}
