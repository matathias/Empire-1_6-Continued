namespace FactionColonies;

[DefOf]
public static class FCTraderKindDefOf
{
    public static TraderKindDef FCResearchTrader;
    static FCTraderKindDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(FCTraderKindDefOf));
    }
}
