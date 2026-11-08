using UnityEngine;

namespace FactionColonies;

public class FCWindow_ProjectilePicker : FCWindow_SearchablePicker<ThingDef>
{
    private readonly List<ThingDef> projectiles;
    private readonly Action<ThingDef> onSelect;

    public FCWindow_ProjectilePicker(List<ThingDef> projectiles, Action<ThingDef> onSelect)
    {
        this.projectiles = projectiles;
        this.onSelect = onSelect;
    }

    protected override string Title => "FCAddNewProjectile".Translate();
    protected override string EmptyText => "FCNoProjectilesFound".Translate();

    protected override List<ThingDef> Items() => projectiles;

    protected override string SearchText(ThingDef def) => def.LabelCap.ToString();

    // Row layout: Icon+Name | Cost
    protected override void DrawRow(Rect row, ThingDef def)
    {
        Rect costRect = new(row.xMax - margin - 80f, row.y, 70f, RowHeight);
        Rect iconNameRect = new(row.x + margin, row.y, costRect.x - row.x - margin * 2, RowHeight);

        Widgets.DefLabelWithIcon(iconNameRect, def);

        Text.Anchor = TextAnchor.MiddleRight;
        UIUtil.ClampedLabel(costRect, "$" + Math.Round(def.BaseMarketValue * 1.5, 2));
    }

    protected override void Pick(ThingDef def) => onSelect(def);
}
