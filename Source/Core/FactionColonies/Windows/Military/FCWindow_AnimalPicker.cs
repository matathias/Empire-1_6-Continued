using UnityEngine;

namespace FactionColonies;

/// <summary>
/// Modal picker that lists every available animal kind (<see cref="FactionCache.AllAnimalKindDefs"/>)
/// a unit can take as a companion. Each row shows the animal's silver cost; clicking adds it via
/// <see cref="MilUnitFC.AddAnimal"/>, which hard-blocks the add when it would exceed
/// FCSettings.maxAnimalSubpawns. Stays open so several animals can be queued; a live "N / cap" header
/// reflects each add. Mirrors <see cref="FCWindow_MechPicker"/>. The single rideable mount is chosen
/// in a separate window (<see cref="FCWindow_MountPicker"/>), not here.
/// </summary>
public class FCWindow_AnimalPicker : FCWindow_SearchablePicker<PawnKindDef>
{
    private readonly Func<MilUnitFC> getDisplayUnit;
    private readonly Func<MilUnitFC> getEditTarget;

    public override Vector2 InitialSize => new(450f, 600f);

    public FCWindow_AnimalPicker(Func<MilUnitFC> getDisplayUnit, Func<MilUnitFC> getEditTarget)
    {
        this.getDisplayUnit = getDisplayUnit;
        this.getEditTarget = getEditTarget;
    }

    /* Convenience constructor: edits the given unit directly (no buffering). Used by callers
     * that just want to add companions to a unit. */
    public FCWindow_AnimalPicker(MilUnitFC unit)
        : this(() => unit, () => unit)
    {
    }

    protected override string Title => "fcPickAnimal".Translate();
    protected override string EmptyText => "fcNoAnimalsAvailable".Translate();
    protected override bool PicksOnClick => true;

    // Count header: "N / cap"
    protected override bool DrawHeader(Rect rect)
    {
        int total = getDisplayUnit?.Invoke()?.TotalAnimalCount ?? 0;
        int cap = FCSettings.maxAnimalSubpawns;
        if (total >= cap) GUI.color = ColorLibrary.RedReadable;
        UIUtil.ClampedLabel(rect, "fcAnimalCount".Translate(total, cap));
        return true;
    }

    // Submods (e.g. Herds & Fisheries) can gate the offered kinds via AnimalPickerFilterRegistry;
    // with no filter registered every kind passes (base behavior).
    protected override List<PawnKindDef> Items() => FactionCache.AllAnimalKindDefs
        .Where(a => AnimalPickerFilterRegistry.IsAllowed(a))
        .OrderBy(a => a.label ?? a.defName, StringComparer.OrdinalIgnoreCase)
        .ToList();

    protected override string SearchText(PawnKindDef animal) => animal.label ?? animal.defName;

    protected override void DrawRow(Rect row, PawnKindDef animal)
    {
        double cost = Math.Floor(animal.race.BaseMarketValue * FCSettings.militaryAnimalCostMultiplier);
        DrawThingRow(row, animal.race, animal.LabelCap, "$" + cost.ToString("F0"));
    }

    protected override void Pick(PawnKindDef animal) => getEditTarget?.Invoke()?.AddAnimal(animal);   // hard-blocks on cap
}
