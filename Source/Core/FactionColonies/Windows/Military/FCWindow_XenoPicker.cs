using UnityEngine;

namespace FactionColonies;

public class FCWindow_XenoPicker : FCWindow_SearchablePicker<FCWindow_XenoPicker.XenoOption>
{
    private readonly MilUnitFC unit;

    // Selection state — exactly one of these is non-null
    private XenotypeDef selectedDef;
    private string selectedCustomName;

    public FCWindow_XenoPicker(MilUnitFC unit)
    {
        this.unit = unit;
        if (unit.IsCustomXenotype)
            selectedCustomName = unit.customXenotypeName;
        else
            selectedDef = unit.xenotype;

        // Rebuild cache so newly created xenotypes (including disk-only) appear
        FactionCache.InvalidateCustomXenotypeCache();
    }

    public struct XenoOption
    {
        public XenotypeDef def;
        public CustomXenotype custom;
        public string label;
        public Texture2D icon;
        public float costFactor;

        public bool IsCustom => custom is not null;
    }

    protected override string Title => "FCChangeUnitXenoButton".Translate();
    protected override string EmptyText => "FCChangeUnitXenoNoXenos".Translate();

    protected override List<XenoOption> Items()
    {
        List<XenoOption> xenoOptions = [];

        foreach (XenotypeDef def in FactionCache.ViolentXenotypeDefs)
        {
            xenoOptions.Add(new XenoOption
            {
                def = def,
                label = (def.label ?? def.defName).CapitalizeFirst(),
                icon = def.Icon,
                costFactor = GeneValuationUtil.XenotypeFactor(def)
            });
        }

        // Custom xenotypes — only for Human race
        if (unit.pawnKind is null || unit.pawnKind.race == ThingDefOf.Human)
        {
            List<CustomXenotype> violentCustom = FactionCache.ViolentCustomXenotypes;
            if (violentCustom is not null)
            {
                foreach (CustomXenotype custom in violentCustom)
                {
                    xenoOptions.Add(new XenoOption
                    {
                        custom = custom,
                        label = (custom.name ?? "???").CapitalizeFirst() + " (" + "Custom".Translate() + ")",
                        icon = custom.IconDef.Icon,
                        costFactor = GeneValuationUtil.XenotypeFactor(custom)
                    });
                }
            }
        }

        xenoOptions.Sort((a, b) => string.Compare(a.label, b.label, StringComparison.OrdinalIgnoreCase));
        return xenoOptions;
    }

    protected override string SearchText(XenoOption opt) => opt.label;

    protected override void DrawRow(Rect row, XenoOption opt)
    {
        Rect iconRect = new(row.x + 2f, row.y + 3f, IconSize, IconSize);
        if (opt.icon != null)
            GUI.DrawTexture(iconRect, opt.icon);

        Rect labelRect = new(iconRect.xMax + 5f, row.y, row.width - IconSize - 10f, RowHeight);
        string costText = opt.costFactor != 1f ? " (x" + opt.costFactor.ToString("F2") + $" {"FCCost".Translate()})" : "";
        UIUtil.ClampedLabel(labelRect, opt.label + costText);
    }

    protected override bool IsSelected(XenoOption opt) => opt.IsCustom
        ? opt.custom.name == selectedCustomName
        : opt.def == selectedDef;

    protected override void Select(XenoOption opt)
    {
        if (opt.IsCustom)
        {
            selectedCustomName = opt.custom.name;
            selectedDef = null;
        }
        else
        {
            selectedDef = opt.def;
            selectedCustomName = null;
        }
    }

    protected override bool CanConfirm => selectedDef is not null || selectedCustomName is not null;

    // The selection is kept by name, so look the custom xenotype up again on confirm.
    protected override void Confirm()
    {
        if (selectedCustomName is not null)
        {
            var decoder = FactionCache.CustomXenotypesDecoder;
            if (decoder is not null && decoder.TryGetValue(selectedCustomName, out CustomXenotype custom))
                unit.SetCustomXenotype(custom);
        }
        else
        {
            unit.SetXenotype(selectedDef);
        }
    }

    protected override void Pick(XenoOption opt)
    {
        if (opt.IsCustom) unit.SetCustomXenotype(opt.custom);
        else unit.SetXenotype(opt.def);
    }
}
