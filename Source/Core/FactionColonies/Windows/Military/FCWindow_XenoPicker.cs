using FactionColonies.util;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class FCWindow_XenoPicker : Window
    {
        private readonly MilUnitFC unit;
        private string searchTerm = "";
        private Vector2 scrollPos;

        // Selection state — exactly one of these is non-null
        private XenotypeDef selectedDef;
        private string selectedCustomName;

        private const float RowHeight = 30f;
        private const float IconSize = 24f;
        private const float SearchBarHeight = 28f;
        private const float ButtonHeight = 35f;
        private const float margin = 5f;

        public override Vector2 InitialSize => new Vector2(450f, 550f);

        public FCWindow_XenoPicker(MilUnitFC unit)
        {
            this.unit = unit;
            if (unit.IsCustomXenotype)
                selectedCustomName = unit.customXenotypeName;
            else
                selectedDef = unit.xenotype;
            draggable = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            forcePause = false;

            // Rebuild cache so newly created xenotypes (including disk-only) appear
            FactionCache.InvalidateCustomXenotypeCache();
        }

        private struct XenoOption
        {
            public XenotypeDef def;
            public CustomXenotype custom;
            public string label;
            public Texture2D icon;
            public float costFactor;

            public bool IsCustom => custom != null;
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            // Title
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            UIUtil.ClampedLabel(new Rect(0, 0, inRect.width, 35f), "FCChangeUnitXenoButton".Translate());

            // Search bar
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect searchRect = new Rect(0, 40f, inRect.width, SearchBarHeight);
            searchTerm = Widgets.TextField(searchRect, searchTerm);

            // Build unified xeno list
            List<XenoOption> xenoOptions = new List<XenoOption>();

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
            if (unit.pawnKind == null || unit.pawnKind.race == ThingDefOf.Human)
            {
                List<CustomXenotype> violentCustom = FactionCache.ViolentCustomXenotypes;
                if (violentCustom != null)
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

            // Filter by search
            List<XenoOption> filtered = string.IsNullOrEmpty(searchTerm)
                ? xenoOptions
                : xenoOptions.Where(x => x.label.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            // Scroll view
            float listTop = searchRect.yMax + margin;
            float listHeight = inRect.height - listTop - ButtonHeight - 15f;
            Rect scrollOutRect = new Rect(0, listTop, inRect.width, listHeight);
            Widgets.DrawMenuSection(scrollOutRect);

            float viewHeight = filtered.Count * RowHeight;
            Rect scrollViewRect = ScrollUtil.BeginScrollView(scrollOutRect, ref scrollPos, viewHeight);

            for (int i = 0; i < filtered.Count; i++)
            {
                XenoOption opt = filtered[i];
                Rect row = new Rect(0, i * RowHeight, scrollViewRect.width, RowHeight);

                bool isSelected = opt.IsCustom
                    ? opt.custom.name == selectedCustomName
                    : opt.def == selectedDef;

                if (isSelected)
                    Widgets.DrawHighlightSelected(row);
                else if (i % 2 == 0)
                    Widgets.DrawHighlight(row);

                Rect iconRect = new Rect(row.x + 2f, row.y + 3f, IconSize, IconSize);
                if (opt.icon != null)
                    GUI.DrawTexture(iconRect, opt.icon);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect labelRect = new Rect(iconRect.xMax + 5f, row.y, row.width - IconSize - 10f, RowHeight);
                string costText = opt.costFactor != 1f ? " (x" + opt.costFactor.ToString("F2") + $" {"FCCost".Translate()})" : "";
                UIUtil.ClampedLabel(labelRect, opt.label + costText);

                if (Widgets.ButtonInvisible(row))
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
            }

            if (filtered.Count == 0)
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.ClampedLabel(scrollOutRect, "FCChangeUnitXenoNoXenos".Translate());
            }

            ScrollUtil.EndScrollView();

            // Bottom buttons
            float buttonWidth = 120f;
            Rect buttonBar = new Rect(0, inRect.height - ButtonHeight - 5f, inRect.width, ButtonHeight);

            Rect cancelRect = new Rect(buttonBar.xMax - buttonWidth, buttonBar.y, buttonWidth, buttonBar.height);
            if (UIUtil.ClampedButtonText(cancelRect, "CancelButton".Translate()))
            {
                Close();
            }

            bool canConfirm = selectedDef != null || selectedCustomName != null;
            Rect confirmRect = new Rect(cancelRect.x - buttonWidth - 10f, buttonBar.y, buttonWidth, buttonBar.height);
            if (UIUtil.ClampedButtonText(confirmRect, "FCConfirm".Translate(), active: canConfirm))
            {
                if (canConfirm)
                {
                    if (selectedCustomName != null)
                    {
                        var decoder = FactionCache.CustomXenotypesDecoder;
                        CustomXenotype custom;
                        if (decoder != null && decoder.TryGetValue(selectedCustomName, out custom))
                            unit.SetCustomXenotype(custom);
                    }
                    else
                    {
                        unit.SetXenotype(selectedDef);
                    }
                    Close();
                }
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }
    }
}
