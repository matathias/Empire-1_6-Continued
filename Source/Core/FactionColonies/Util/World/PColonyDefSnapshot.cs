namespace FactionColonies;

/// <summary>
/// Pristine values of the shared PColony FactionDef fields that the Empire rewrites per game
/// (UpdateFactionDef's tech level, member kind, backstories and apparel filter; caravan trader kinds;
/// the classic-ideo flag set at faction creation). The def lives for the whole session, so without a
/// restore one save's values leak into the next save loaded or started in the same session.
/// Captured once at startup, before any game touches the def.
/// </summary>
[StaticConstructorOnStartup]
public static class PColonyDefSnapshot
{
    private static readonly bool captured;
    private static readonly TechLevel techLevel;
    private static readonly PawnKindDef basicMemberKind;
    private static readonly List<BackstoryCategoryFilter> backstoryFilters;
    private static readonly ThingFilter apparelStuffFilter;
    private static readonly List<TraderKindDef> caravanTraderKinds;
    private static readonly bool classicIdeo;

    static PColonyDefSnapshot()
    {
        FactionDef def = DefDatabase<FactionDef>.GetNamedSilentFail("PColony");
        if (def is null) return;
        techLevel = def.techLevel;
        basicMemberKind = def.basicMemberKind;
        backstoryFilters = def.backstoryFilters;
        apparelStuffFilter = CopyFilter(def.apparelStuffFilter);
        caravanTraderKinds = def.caravanTraderKinds is null ? null : [.. def.caravanTraderKinds];
        classicIdeo = def.classicIdeo;
        captured = true;
    }

    /// <summary>Puts the def's per-game fields back to their startup values. Each restore hands out
    /// fresh copies of the filter and list, so later edits never reach the snapshot.</summary>
    public static void Restore(FactionDef def)
    {
        if (!captured || def is null) return;
        def.techLevel = techLevel;
        def.basicMemberKind = basicMemberKind;
        def.backstoryFilters = backstoryFilters;
        def.apparelStuffFilter = CopyFilter(apparelStuffFilter);
        def.caravanTraderKinds = caravanTraderKinds is null ? null : [.. caravanTraderKinds];
        def.classicIdeo = classicIdeo;
    }

    public static ThingFilter CopyFilter(ThingFilter source)
    {
        if (source is null) return null;
        ThingFilter copy = new();
        copy.CopyAllowancesFrom(source);
        return copy;
    }
}
