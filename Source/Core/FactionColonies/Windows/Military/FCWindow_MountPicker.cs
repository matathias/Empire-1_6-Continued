using UnityEngine;

namespace FactionColonies;

/// <summary>
/// Single-select picker for a merc's rideable mount (Giddy Up 2). Lists only the animal kinds GU2
/// considers mountable (via <see cref="GiddyUpUtil.IsMountable"/>); Confirm sets the mount, Unequip
/// clears it. Only opened from the gear-panel Mount slot, which itself is shown only when Giddy Up 2
/// is active. Mirrors the single-select layout of the old animal picker.
/// </summary>
public class FCWindow_MountPicker : FCWindow_SearchablePicker<PawnKindDef>
{
    private readonly Action<PawnKindDef> onConfirm;
    private readonly Action onUnequip;

    public FCWindow_MountPicker(PawnKindDef initialMount, Action<PawnKindDef> onConfirm, Action onUnequip)
    {
        if (initialMount is not null) Select(initialMount);
        this.onConfirm = onConfirm;
        this.onUnequip = onUnequip;
    }

    /* Convenience constructor: sets unit.mount directly. Used by DesignUnitsWindow. */
    public FCWindow_MountPicker(MilUnitFC unit)
        : this(unit?.mount,
              picked => { unit?.SetMount(picked); },
              () => { unit?.SetMount(null); })
    {
    }

    protected override string Title => "fcPickMount".Translate();
    protected override string EmptyText => "fcNoMountsAvailable".Translate();
    protected override bool EmptyTextInScrollView => true;

    // Mountable animal kinds (GU2 rules), gated by any submod animal filter (e.g. Herds & Fisheries
    // stocking). No filter registered => only the GU2 mountable check applies.
    protected override List<PawnKindDef> Items() => FactionCache.AllAnimalKindDefs
        .Where(a => GiddyUpUtil.IsMountable(a) && AnimalPickerFilterRegistry.IsAllowed(a))
        .OrderBy(a => a.label ?? a.defName, StringComparer.OrdinalIgnoreCase)
        .ToList();

    protected override string SearchText(PawnKindDef animal) => animal.label ?? animal.defName;

    protected override void DrawRow(Rect row, PawnKindDef animal)
    {
        double cost = MilUnitFC.AnimalUnitCost(animal);
        DrawThingRow(row, animal.race, animal.LabelCap, $"${cost:F0}", costFont: GameFont.Small);
    }

    protected override void Pick(PawnKindDef animal) => onConfirm?.Invoke(animal);

    protected override void DrawExtraButton(Rect rect)
    {
        if (UIUtil.ClampedButtonText(rect, "FCUnitActionUnequipThing".Translate()))
        {
            onUnequip?.Invoke();
            Close();
        }
    }
}
