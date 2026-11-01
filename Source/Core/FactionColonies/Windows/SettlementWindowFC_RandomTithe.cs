using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class SettlementWindowFC_RandomTithe : Window
    {
        private ResourceFC resource;
        private WorldSettlementFC settlement;
        private Vector2 scrollBar = new Vector2();

        private const int margin = 5;
        private const int smallMargin = 3;
        private const int rowHeight = 23;
        private const int scrollSpacing = 16;
        private const float SearchBarHeight = 28f;

        private string thingSearchTerm = "";

        private int sortIndex = 0;
        private static readonly string[] sortLabelKeys = { "FCTitheSortNameAZ", "FCTitheSortNameZA", "FCTitheSortPriceLow", "FCTitheSortPriceHigh" };

        private int statusFilter = 0; // 0=All, 1=Enabled, 2=Disabled
        private static readonly string[] statusFilterKeys = { "FCTitheShowAll", "FCTitheShowEnabled", "FCTitheShowDisabled" };

        public override Vector2 InitialSize
        {
            get { return new Vector2(450f, 500f); }
        }

        public SettlementWindowFC_RandomTithe(WorldSettlementFC settlement, ResourceFC resource)
        {
            if (resource == null || settlement == null)
            {
                Close();
            }
            this.settlement = settlement;
            forcePause = false;
            draggable = true;
            doCloseX = true;
            preventCameraMotion = false;
            resizeable = true;
            this.resource = resource;
        }

        public override void DoWindowContents(Rect boundingBox)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect header = new Rect(boundingBox.x, boundingBox.y, boundingBox.width, 35f);
            UIUtil.ClampedLabel(header, "FCRandomTitheSelectionHeader".Translate());
            Widgets.DrawLineHorizontal(header.x, header.yMax, header.width);

            Text.Font = GameFont.Small;
            Rect subHeader = new Rect(boundingBox.x, header.yMax, boundingBox.width, 30f);
            UIUtil.ClampedLabel(subHeader, settlement.Name);

            Rect iconBox = new Rect(boundingBox.x, subHeader.yMax, 30f, 30f);
            Rect labelHighlight = new Rect(iconBox.xMax + margin, iconBox.y, boundingBox.width - margin - iconBox.width, 30f);
            Rect labelText = new Rect(labelHighlight.x + smallMargin, labelHighlight.y + smallMargin, labelHighlight.width - (smallMargin * 2), labelHighlight.height - (smallMargin * 2));

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.DrawHighlight(iconBox);
            Widgets.Label(iconBox, new GUIContent(resource.def.Icon));
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.DrawHighlight(labelHighlight);
            UIUtil.ClampedLabel(labelText, resource.def.LabelCap);
            Rect iconAccent = new Rect(iconBox.x, iconBox.y, iconBox.width, 3f);
            Rect labelAccent = new Rect(labelHighlight.x, labelHighlight.y, labelHighlight.width, 3f);
            Widgets.DrawBoxSolid(iconAccent, resource.def.color);
            Widgets.DrawBoxSolid(labelAccent, resource.def.color);

            /* Enable All / Disable All buttons */
            Rect enableAllBox = new Rect(boundingBox.x, iconBox.yMax + margin, boundingBox.width / 2f, 30f);
            Rect disableAllBox = new Rect(enableAllBox.xMax, enableAllBox.y, boundingBox.width / 2f, 30f);
            if (UIUtil.ClampedButtonText(enableAllBox, "FCTitheEnableAll".Translate()))
            {
                resource.SetAllRandomTitheFilter();
            }
            if (UIUtil.ClampedButtonText(disableAllBox, "FCTitheDisableAll".Translate()))
            {
                resource.ClearRandomTitheFilter();
            }

            float curY = enableAllBox.yMax + margin;

            // Enabled count + status filter row
            List<ThingDef> allThings = resource.GenerateThingDefList();
            int enabledCount = allThings.Count(t => resource.GetRandomTitheFilterAllow(t));
            int totalCount = allThings.Count;

            Rect countRow = new Rect(boundingBox.x, curY, boundingBox.width, 22f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.ClampedLabel(new Rect(countRow.x + smallMargin, countRow.y, 120f, countRow.height),
                "FCTitheEnabledCount".Translate(enabledCount, totalCount));

            Text.Font = GameFont.Small;
            float filterBtnW = 110f;
            Rect filterBtn = new Rect(countRow.xMax - filterBtnW - margin, countRow.y, filterBtnW, countRow.height);
            if (UIUtil.ClampedButtonText(filterBtn, "FCTitheShowFilter".Translate(statusFilterKeys[statusFilter].Translate())))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                for (int f = 0; f < statusFilterKeys.Length; f++)
                {
                    int captured = f;
                    options.Add(new FloatMenuOption(statusFilterKeys[f].Translate(), () =>
                    {
                        statusFilter = captured;
                        scrollBar = Vector2.zero;
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            curY = countRow.yMax + margin;

            // Column header with sort
            Rect headerRow = new Rect(boundingBox.x, curY, boundingBox.width, SearchBarHeight);
            Widgets.DrawHighlight(headerRow);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.ClampedLabel(new Rect(headerRow.x + margin, headerRow.y, 60f, headerRow.height), "FCItem".Translate());
            float sortBtnW = 120f;
            Rect sortBtn = new Rect(headerRow.xMax - margin - 65f - margin - 65f - margin - sortBtnW, headerRow.y + 2, sortBtnW, headerRow.height - 4);
            if (UIUtil.ClampedButtonText(sortBtn, "FCSortBy".Translate(sortLabelKeys[sortIndex].Translate())))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                for (int s = 0; s < sortLabelKeys.Length; s++)
                {
                    int captured = s;
                    options.Add(new FloatMenuOption(sortLabelKeys[s].Translate(), () =>
                    {
                        sortIndex = captured;
                        scrollBar = Vector2.zero;
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            UIUtil.ClampedLabel(new Rect(headerRow.xMax - margin - 65f - margin - 75f, headerRow.y, 60f, headerRow.height), "FCTitheBasePrice".Translate());
            Text.Anchor = TextAnchor.MiddleCenter;
            UIUtil.ClampedLabel(new Rect(headerRow.xMax - margin - 75f, headerRow.y, 65f, headerRow.height), "FCIsTithe".Translate());
            Text.Font = GameFont.Small;
            curY = headerRow.yMax;

            // Search bar
            Rect searchRect = new Rect(boundingBox.x, curY, boundingBox.width, SearchBarHeight);
            thingSearchTerm = Widgets.TextField(searchRect, thingSearchTerm);
            if (string.IsNullOrEmpty(thingSearchTerm))
            {
                Color prevColor = GUI.color;
                GUI.color = Color.gray;
                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.ClampedLabel(new Rect(searchRect.x + 5f, searchRect.y, searchRect.width - 10f, searchRect.height),
                    "FCSearchItems".Translate());
                GUI.color = prevColor;
            }
            curY = searchRect.yMax;

            // Build filtered + sorted list
            List<ThingDef> thingsList = string.IsNullOrEmpty(thingSearchTerm)
                ? allThings
                : allThings.Where(t => (t.label ?? t.defName).IndexOf(thingSearchTerm, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            thingsList = ApplySort(thingsList, sortIndex);

            // Apply status filter
            if (statusFilter == 1)
                thingsList = thingsList.Where(t => resource.GetRandomTitheFilterAllow(t)).ToList();
            else if (statusFilter == 2)
                thingsList = thingsList.Where(t => !resource.GetRandomTitheFilterAllow(t)).ToList();

            // Scroll list
            Rect drawBox = new Rect(boundingBox.x, curY + margin, boundingBox.width, boundingBox.yMax - curY - margin);
            Rect outerListBox = new Rect(drawBox.x + 2, drawBox.y + 2, drawBox.width - 4, drawBox.height - 4);
            float listHeight = thingsList.Count * rowHeight;
            Widgets.DrawMenuSection(drawBox);

            Rect innerScrollBox = ScrollUtil.BeginScrollView(outerListBox, ref scrollBar, listHeight);

            for (int i = 0; i < thingsList.Count; i++)
            {
                ThingDef iThing = thingsList[i];
                Rect row = new Rect(innerScrollBox.x, innerScrollBox.y + (i * rowHeight), innerScrollBox.width, rowHeight);
                Rect icon = new Rect(row.x + margin, row.y, rowHeight, rowHeight);
                Rect info = new Rect(icon.xMax, row.y + 2, rowHeight - 4, rowHeight - 4);
                Rect enableBox = new Rect(row.xMax - margin - 65f, row.y, 65f, rowHeight);
                Rect valueLabel = new Rect(enableBox.x - margin - 60f, enableBox.y, 60f, rowHeight);
                Rect label = new Rect(info.xMax + margin, row.y, valueLabel.x - info.xMax - (margin * 2), rowHeight);

                if (i % 2 == 0)
                {
                    Widgets.DrawHighlight(row);
                }
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(icon, new GUIContent(iThing.uiIcon));
                bool allowed = resource.GetRandomTitheFilterAllow(iThing);
                Color buttonColor = allowed ? Color.green : Color.red;
                GUI.color = buttonColor;
                if (UIUtil.ClampedButtonText(enableBox, IsAllowedTranslation(allowed)))
                {
                    resource.SetRandomTitheFilterAllow(iThing, !allowed);
                }
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.ClampedLabel(label, iThing.LabelCap);
                Text.Anchor = TextAnchor.MiddleRight;
                UIUtil.ClampedLabel(valueLabel, $"${Math.Round(iThing.BaseMarketValue)}");
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.InfoCardButton(info, iThing);
            }

            ScrollUtil.EndScrollView();

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        /// <summary>
        /// Transforms the given bool <paramref name="var"/> into it's keyed translation
        /// </summary>
        /// <param name="var"></param>
        /// <returns></returns>
        private string IsAllowedTranslation(bool var)
        {
            if (var) return "FCIsAllowed".Translate();
            return "FCIsNotAllowed".Translate();
        }

        private List<ThingDef> ApplySort(List<ThingDef> list, int sortIndex)
        {
            switch (sortIndex)
            {
                case 1: // Name Z-A
                    return list.OrderByDescending(t => t.label, StringComparer.OrdinalIgnoreCase).ToList();
                case 2: // Price Low-High
                    return list.OrderBy(t => t.BaseMarketValue).ToList();
                case 3: // Price High-Low
                    return list.OrderByDescending(t => t.BaseMarketValue).ToList();
                default: // 0: Name A-Z
                    return list.OrderBy(t => t.label, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }
    }
}
