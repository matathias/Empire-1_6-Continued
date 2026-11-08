namespace FactionColonies;

/// <summary>
/// Filtered and sorted view of a ThingDef list for the item and stuff panes of the two-pane item
/// pickers. The view is rebuilt only when one of its inputs changes (source list, search term,
/// sort index, or the item and quality that stuff prices depend on), so a pane can ask for it every
/// frame without re-sorting thousands of modded defs.
/// </summary>
public sealed class ThingDefPickerView
{
    public static readonly string[] SortLabelKeys = ["FCTitheSortNameAZ", "FCTitheSortNameZA", "FCTitheSortPriceLow", "FCTitheSortPriceHigh"];

    /// <summary>Index into <see cref="SortLabelKeys"/>.</summary>
    public int SortIndex;

    private List<ThingDef> cache;
    private List<ThingDef> cacheSource;
    private string cacheTerm;
    private int cacheSort = -1;
    private ThingDef cachePriceItem;
    private QualityCategory cacheQuality;

    /// <summary>
    /// Returns <paramref name="source"/> filtered by <paramref name="searchTerm"/> and sorted by
    /// <see cref="SortIndex"/>. When <paramref name="priceItem"/> is set, the list holds stuffs and
    /// price sorts use the value of that item made from each stuff at <paramref name="quality"/>.
    /// A source list that is changed in place must come with a different
    /// <paramref name="priceItem"/>, or the cached view is not rebuilt.
    /// </summary>
    public List<ThingDef> Get(List<ThingDef> source, string searchTerm,
        ThingDef priceItem = null, QualityCategory quality = QualityCategory.Normal)
    {
        if (cache is not null
            && cacheSource == source
            && cacheTerm == searchTerm
            && cacheSort == SortIndex
            && cachePriceItem == priceItem
            && cacheQuality == quality)
        {
            return cache;
        }

        List<ThingDef> filtered = string.IsNullOrEmpty(searchTerm)
            ? source
            : source.Where(t => (t.label ?? t.defName).IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

        cache = Sort(filtered, SortIndex, priceItem, quality);
        cacheSource = source;
        cacheTerm = searchTerm;
        cacheSort = SortIndex;
        cachePriceItem = priceItem;
        cacheQuality = quality;
        return cache;
    }

    /// <summary>Opens the sort float menu; picking an entry sets <see cref="SortIndex"/> and then
    /// runs <paramref name="onPicked"/>.</summary>
    public void OpenSortMenu(Action onPicked)
    {
        List<FloatMenuOption> options = [];
        for (int s = 0; s < SortLabelKeys.Length; s++)
        {
            int captured = s;
            options.Add(new FloatMenuOption(SortLabelKeys[s].Translate(), () =>
            {
                SortIndex = captured;
                onPicked?.Invoke();
            }));
        }
        Find.WindowStack.Add(new FloatMenu(options));
    }

    /// <summary>Label for the current sort, for the sort button.</summary>
    public string SortButtonLabel => "FCSortBy".Translate(SortLabelKeys[SortIndex].Translate());

    private static List<ThingDef> Sort(List<ThingDef> list, int sortIndex, ThingDef priceItem, QualityCategory quality)
    {
        switch (sortIndex)
        {
            case 1: // Name Z-A
                return list.OrderByDescending(t => t.label, StringComparer.OrdinalIgnoreCase).ToList();
            case 2: // Price Low-High
                if (priceItem is not null)
                    return list.OrderBy(t => CraftUtil.ThingValue(priceItem, t, quality)).ToList();
                return list.OrderBy(t => t.BaseMarketValue).ToList();
            case 3: // Price High-Low
                if (priceItem is not null)
                    return list.OrderByDescending(t => CraftUtil.ThingValue(priceItem, t, quality)).ToList();
                return list.OrderByDescending(t => t.BaseMarketValue).ToList();
            default: // 0: Name A-Z
                return list.OrderBy(t => t.label, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
