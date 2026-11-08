using UnityEngine;

namespace FactionColonies;

public class FCWindow_UnitPicker : FCWindow_SearchablePicker<MilUnitFC>
{
    private readonly List<MilUnitFC> units;
    private readonly Action<MilUnitFC> onSelect;

    public FCWindow_UnitPicker(MilitaryFC mfc, Action<MilUnitFC> onSelect)
    {
        this.units = mfc.units.Where(u => !u.isBlank).ToList();
        this.onSelect = onSelect;
    }

    protected override string Title => "FCAddUnit".Translate();
    protected override string EmptyText => "FCSelectAUnitButton".Translate();
    protected override bool EmptyTextInScrollView => true;

    protected override List<MilUnitFC> Items() => units;

    protected override string SearchText(MilUnitFC unit) => unit.name;

    // Row layout: Icon | Label | Cost
    protected override void DrawRow(Rect row, MilUnitFC unit)
    {
        Rect iconRect = new(row.x + margin, row.y, RowHeight, RowHeight);
        Rect costRect = new(row.xMax - margin - 70f, row.y, 60f, RowHeight);
        Rect labelRect = new(iconRect.xMax + margin, row.y,
            costRect.x - iconRect.xMax - (margin * 2), RowHeight);

        // Icon: weapon icon if available, else race icon
        if (unit.HasWeapon && unit.weapons[0].thing is not null)
            Widgets.ThingIcon(iconRect, unit.weapons[0].thing);
        else if (unit.pawnKind?.race is not null)
            Widgets.ThingIcon(iconRect, unit.pawnKind.race);

        UIUtil.ClampedLabel(labelRect, unit.name);

        Text.Anchor = TextAnchor.MiddleRight;
        UIUtil.ClampedLabel(costRect, "$" + unit.getTotalCost);
    }

    protected override void Pick(MilUnitFC unit) => onSelect(unit);
}
