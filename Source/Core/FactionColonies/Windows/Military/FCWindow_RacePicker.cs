using UnityEngine;

namespace FactionColonies;

public class FCWindow_RacePicker : FCWindow_SearchablePicker<PawnKindDef>
{
    private readonly MilUnitFC unit;
    private readonly FactionFC faction;

    public FCWindow_RacePicker(MilUnitFC unit, FactionFC faction)
    {
        this.unit = unit;
        this.faction = faction;
        if (unit.pawnKind is not null) Select(unit.pawnKind);
    }

    protected override string Title => "FCChangeUnitRaceButton".Translate();
    protected override string EmptyText => "FCChangeUnitRaceNoRaces".Translate();

    private static string RaceLabel(PawnKindDef def) => (def.race.label ?? def.race.defName).CapitalizeFirst();

    // One entry per humanlike race (Human represented by the Colonist kind)
    protected override List<PawnKindDef> Items()
    {
        HashSet<string> seenRaces = [];
        List<PawnKindDef> races = [];
        foreach (PawnKindDef def in FactionCache.AllPawnKindDefs)
        {
            if (!def.IsHumanLikeRace()) continue;
            if (def.race == ThingDefOf.Human && def != PawnKindDefOf.Colonist) continue;
            if (seenRaces.Add(def.race.label ?? def.race.defName))
                races.Add(def);
        }
        races.Sort((a, b) => string.Compare(RaceLabel(a), RaceLabel(b), StringComparison.OrdinalIgnoreCase));
        return races;
    }

    protected override string SearchText(PawnKindDef def) => RaceLabel(def);

    protected override void DrawRow(Rect row, PawnKindDef def)
    {
        Rect iconRect = new(row.x + 2f, row.y + 3f, IconSize, IconSize);
        Widgets.ThingIcon(iconRect, def.race);

        double cost = Math.Floor(def.race.BaseMarketValue * FCSettings.militaryRaceCostMultiplier);
        Rect labelRect = new(iconRect.xMax + 5f, row.y, row.width - IconSize - 10f, RowHeight);
        UIUtil.ClampedLabel(labelRect, RaceLabel(def) + " - Cost: " + cost);
    }

    protected override void Pick(PawnKindDef def)
    {
        unit.pawnKind = def;
        // Custom xenotypes only apply to humans
        if (def?.race != ThingDefOf.Human && unit.customXenotypeName is not null)
        {
            unit.customXenotypeName = null;
            unit.xenotype ??= XenotypeDefOf.Baseliner;
        }
        unit.RerollPreviewPawn();
        // Drop implants that the new race's body can no longer accept.
        unit.OnRaceChanged();
    }
}
