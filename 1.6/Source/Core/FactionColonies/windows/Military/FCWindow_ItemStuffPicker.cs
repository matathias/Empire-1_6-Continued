using FactionColonies.util;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// A modal picker window that shows items on the left and stuff materials
    /// on the right. Used for weapon and apparel selection in the unit designer.
    /// </summary>
    public class FCWindow_ItemStuffPicker : Window
    {
        private readonly List<ThingDef> items;
        private readonly Action<ThingDef, ThingDef, QualityCategory?> onConfirm;
        private readonly Action<ThingDef, ThingDef, int, QualityCategory?> onConfirmWithCount;
        private readonly Action onUnequip;
        private readonly string titleKey;
        private readonly Func<ThingDef, string> conflictTooltipFunc;
        private readonly bool showCount;

        private ThingDef selectedItem;
        private ThingDef selectedStuff;
        private QualityCategory? selectedQuality = null;
        private QualityCategory SelectedQuality => selectedQuality ?? QualityCategory.Normal;
        private List<ThingDef> currentStuffs = new List<ThingDef>();
        private int selectedCount = 1;
        private string countBuffer;

        private string itemSearchTerm = "";
        private string stuffSearchTerm = "";

        private int itemSortIndex = 0;
        private int stuffSortIndex = 0;

        // Filtered+sorted view caches, rebuilt only when their inputs change (not every frame). Without
        // these the panels re-sort and re-allocate the whole list every frame, which could make the
        // designer lag with thousands of modded defs. Cache keys track the inputs each view depends on.
        private List<ThingDef> itemFilteredCache;
        private string itemFilterCacheTerm;
        private int itemFilterCacheSort = -1;

        private List<ThingDef> stuffFilteredCache;
        private string stuffFilterCacheTerm;
        private int stuffFilterCacheSort = -1;
        private ThingDef stuffCacheItem;
        private QualityCategory? stuffCacheQuality;
        private static readonly string[] sortLabelKeys = { "FCTitheSortNameAZ", "FCTitheSortNameZA", "FCTitheSortPriceLow", "FCTitheSortPriceHigh" };

        private Vector2 itemScrollPos;
        private Vector2 stuffScrollPos;

        private const float RowHeight = 30f;
        private const float IconSize = 24f;
        private const float SearchBarHeight = 28f;
        private const float ButtonHeight = 35f;
        private const float PanelGap = 10f;
        private const float margin = 5f;

        public override Vector2 InitialSize => new Vector2(700f, 550f);

        public FCWindow_ItemStuffPicker(
            List<ThingDef> items,
            Action<ThingDef, ThingDef, QualityCategory?> onConfirm,
            Action onUnequip = null,
            string titleKey = "fcPickItem",
            ThingDef initialItem = null,
            ThingDef initialStuff = null,
            Func<ThingDef, string> conflictTooltipFunc = null,
            bool showCount = false,
            int initialCount = 1,
            Action<ThingDef, ThingDef, int, QualityCategory?> onConfirmWithCount = null,
            QualityCategory? initialQuality = null)
        {
            this.items = items;
            this.onConfirm = onConfirm;
            this.onConfirmWithCount = onConfirmWithCount;
            this.onUnequip = onUnequip;
            this.titleKey = titleKey;
            this.conflictTooltipFunc = conflictTooltipFunc;
            this.showCount = showCount;
            this.selectedCount = Mathf.Max(1, initialCount);
            this.countBuffer = this.selectedCount.ToString();
            this.selectedQuality = initialQuality;

            if (initialItem != null)
            {
                selectedItem = initialItem;
                selectedStuff = initialStuff;
                if (initialItem.MadeFromStuff)
                {
                    currentStuffs.AddRange(FindFC.FactionComp.GetStuffListForThingDef(initialItem));
                    currentStuffs.SortBy(s => s.label);
                }
            }

            forcePause = false;
            draggable = true;
            doCloseX = true;
            preventCameraMotion = false;
            absorbInputAroundWindow = true;
            resizeable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            // Title
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            UIUtil.ClampedLabel(new Rect(0, 0, inRect.width, 35f), titleKey.Translate());

            float contentTop = 40f;
            float summaryHeight = 25f;
            float contentHeight = inRect.height - contentTop - ButtonHeight - summaryHeight - 20f;
            float panelWidth = (inRect.width - PanelGap) / 2f;

            // Left panel: Items
            Rect itemPanelRect = new Rect(0, contentTop, panelWidth, contentHeight);
            DrawItemPanel(itemPanelRect);

            // Right panel: Stuff
            Rect stuffPanelRect = new Rect(panelWidth + PanelGap, contentTop, panelWidth, contentHeight);
            if (selectedItem != null && selectedItem.MadeFromStuff)
            {
                DrawStuffPanel(stuffPanelRect);
            }
            else if (selectedItem != null)
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.ClampedLabel(stuffPanelRect, "fcNoMaterialNeeded".Translate());
            }

            // Selection summary
            Rect summaryBox = new Rect(inRect.x, contentTop + contentHeight + 5f, inRect.width, summaryHeight);
            Rect summaryRect = new Rect(summaryBox.x + margin, contentTop + contentHeight + 5f, inRect.width - (margin * 2), summaryHeight);
            Widgets.DrawHighlight(summaryBox);
            DrawSummary(summaryRect);

            // Bottom buttons
            Rect buttonBar = new Rect(0, inRect.height - ButtonHeight - 5f, inRect.width, ButtonHeight);
            DrawButtons(buttonBar);

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        private void DrawItemPanel(Rect panelRect)
        {
            Text.Font = GameFont.Small;

            // Title bar with sort + price label
            Rect titleBox = new Rect(panelRect.x, panelRect.y, panelRect.width, SearchBarHeight);
            Widgets.DrawHighlight(titleBox);
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.ClampedLabel(new Rect(titleBox.x + margin, titleBox.y, 60f, titleBox.height), "FCItem".Translate());
            float sortBtnW = 120f;
            Rect sortBtn = new Rect(titleBox.xMax - margin - 75f - margin - sortBtnW, titleBox.y + 2, sortBtnW, titleBox.height - 4);
            if (UIUtil.ClampedButtonText(sortBtn, "FCSortBy".Translate(sortLabelKeys[itemSortIndex].Translate())))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                for (int s = 0; s < sortLabelKeys.Length; s++)
                {
                    int captured = s;
                    options.Add(new FloatMenuOption(sortLabelKeys[s].Translate(), () =>
                    {
                        itemSortIndex = captured;
                        itemScrollPos = Vector2.zero;
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            // Search bar
            Rect searchRect = new Rect(panelRect.x, titleBox.yMax + margin, panelRect.width, SearchBarHeight);
            itemSearchTerm = Widgets.TextField(searchRect, itemSearchTerm);
            if (string.IsNullOrEmpty(itemSearchTerm))
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.DrawColoredLabel(
                    new Rect(searchRect.x + 5f, searchRect.y, searchRect.width - 10f, searchRect.height),
                    "FCSearchItems".Translate(),
                    Color.gray);
            }

            // Scroll view
            Rect scrollOutRect = new Rect(panelRect.x, searchRect.yMax + 5f,
                panelRect.width, panelRect.height - (SearchBarHeight * 2) - (margin * 2));
            Widgets.DrawMenuSection(scrollOutRect);

            EnsureItemFilter();
            List<ThingDef> filtered = itemFilteredCache;

            float viewHeight = filtered.Count * RowHeight;
            float scrollMargin = viewHeight > scrollOutRect.height ? ScrollUtil.ScrollbarWidth : 0f;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            UIUtil.ClampedLabel(new Rect(titleBox.xMax - margin - 65f - scrollMargin, titleBox.y, 60f, titleBox.height), "FCTitheBasePrice".Translate());
            Text.Font = GameFont.Small;
            Rect scrollViewRect = ScrollUtil.BeginScrollView(scrollOutRect, ref itemScrollPos, viewHeight);

            // Cull to the visible viewport so per-frame draw is bounded regardless of pool size.
            int firstRow = Mathf.Max(0, Mathf.FloorToInt(itemScrollPos.y / RowHeight));
            int lastRow = Mathf.Min(filtered.Count, Mathf.CeilToInt((itemScrollPos.y + scrollOutRect.height) / RowHeight));
            for (int i = firstRow; i < lastRow; i++)
            {
                ThingDef item = filtered[i];
                Rect row = new Rect(scrollViewRect.x, scrollViewRect.y + (i * RowHeight), scrollViewRect.width, RowHeight);

                string conflictTip = conflictTooltipFunc != null ? conflictTooltipFunc(item) : null;
                bool hasConflict = !string.IsNullOrEmpty(conflictTip);

                if (item == selectedItem)
                    Widgets.DrawHighlightSelected(row);
                else if (hasConflict)
                    Widgets.DrawBoxSolid(row, new Color(0.45f, 0.22f, 0.22f, 0.35f));
                else if (i % 2 == 0)
                    Widgets.DrawHighlight(row);

                if (hasConflict)
                    TooltipHandler.TipRegion(row, conflictTip);

                // Row layout: Icon | Info | Label | Cost
                Rect iconRect = new Rect(row.x + margin, row.y, RowHeight, RowHeight);
                Widgets.ThingIcon(iconRect, item);

                Rect infoRect = new Rect(iconRect.xMax, row.y + 2, RowHeight - 4, RowHeight - 4);
                Widgets.InfoCardButton(infoRect, item);

                Rect costRect = new Rect(row.xMax - margin - 70f, row.y, 60f, RowHeight);
                Rect labelRect = new Rect(infoRect.xMax + margin, row.y,
                    costRect.x - infoRect.xMax - (margin * 2), RowHeight);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.ClampedLabel(labelRect, item.LabelCap);

                Text.Anchor = TextAnchor.MiddleRight;
                UIUtil.ClampedLabel(costRect, "$" + item.BaseMarketValue.ToString("F0"));

                // Click to select
                if (Widgets.ButtonInvisible(row))
                {
                    selectedItem = item;

                    currentStuffs.Clear();
                    if (item.MadeFromStuff)
                    {
                        currentStuffs.AddRange(FindFC.FactionComp.GetStuffListForThingDef(item));
                        currentStuffs.SortBy(s => s.label);
                    }

                    // Keep selectedStuff if it's still valid for the new item
                    if (selectedStuff != null && (currentStuffs.Count == 0 || !currentStuffs.Contains(selectedStuff)))
                    {
                        selectedStuff = null;
                    }
                }
            }

            ScrollUtil.EndScrollView();
        }

        private void DrawStuffPanel(Rect panelRect)
        {
            Text.Font = GameFont.Small;

            // Title bar with sort + price label
            Rect titleBox = new Rect(panelRect.x, panelRect.y, panelRect.width, SearchBarHeight);
            Widgets.DrawHighlight(titleBox);
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.ClampedLabel(new Rect(titleBox.x + margin, titleBox.y, 60f, titleBox.height), "FCStuff".Translate());
            float sortBtnW = 120f;
            Rect sortBtn = new Rect(titleBox.xMax - margin - 75f - margin - sortBtnW, titleBox.y + 2, sortBtnW, titleBox.height - 4);
            if (UIUtil.ClampedButtonText(sortBtn, "FCSortBy".Translate(sortLabelKeys[stuffSortIndex].Translate())))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                for (int s = 0; s < sortLabelKeys.Length; s++)
                {
                    int captured = s;
                    options.Add(new FloatMenuOption(sortLabelKeys[s].Translate(), () =>
                    {
                        stuffSortIndex = captured;
                        stuffScrollPos = Vector2.zero;
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            // Search bar
            Rect searchRect = new Rect(panelRect.x, titleBox.yMax + margin, panelRect.width, SearchBarHeight);
            stuffSearchTerm = Widgets.TextField(searchRect, stuffSearchTerm);
            if (string.IsNullOrEmpty(stuffSearchTerm))
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.DrawColoredLabel(
                    new Rect(searchRect.x + 5f, searchRect.y, searchRect.width - 10f, searchRect.height),
                    "FCSearchMaterials".Translate(),
                    Color.gray);
            }

            // Scroll view
            Rect scrollOutRect = new Rect(panelRect.x, searchRect.yMax + 5f,
                panelRect.width, panelRect.height - (SearchBarHeight * 2) - (margin * 2));
            Widgets.DrawMenuSection(scrollOutRect);

            EnsureStuffFilter();
            List<ThingDef> filtered = stuffFilteredCache;

            float viewHeight = filtered.Count * RowHeight;
            float scrollMargin = viewHeight > scrollOutRect.height ? ScrollUtil.ScrollbarWidth : 0f;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            UIUtil.ClampedLabel(new Rect(titleBox.xMax - margin - 65f - scrollMargin, titleBox.y, 60f, titleBox.height), "FCTitheMaterialPrice".Translate());
            Text.Font = GameFont.Small;
            Rect scrollViewRect = ScrollUtil.BeginScrollView(scrollOutRect, ref stuffScrollPos, viewHeight);

            // Cull to the visible viewport so per-frame draw is bounded regardless of pool size.
            int firstRow = Mathf.Max(0, Mathf.FloorToInt(stuffScrollPos.y / RowHeight));
            int lastRow = Mathf.Min(filtered.Count, Mathf.CeilToInt((stuffScrollPos.y + scrollOutRect.height) / RowHeight));
            for (int i = firstRow; i < lastRow; i++)
            {
                ThingDef stuff = filtered[i];
                Rect row = new Rect(scrollViewRect.x, scrollViewRect.y + (i * RowHeight), scrollViewRect.width, RowHeight);

                if (stuff == selectedStuff)
                    Widgets.DrawHighlightSelected(row);
                else if (i % 2 == 0)
                    Widgets.DrawHighlight(row);

                // Row layout: Icon | Info | Label | Cost
                Rect iconRect = new Rect(row.x + margin, row.y, RowHeight, RowHeight);
                Widgets.ThingIcon(iconRect, stuff);

                Rect infoRect = new Rect(iconRect.xMax, row.y + 2, RowHeight - 4, RowHeight - 4);
                Widgets.InfoCardButton(infoRect, stuff);

                Rect costRect = new Rect(row.xMax - margin - 70f, row.y, 60f, RowHeight);
                Rect labelRect = new Rect(infoRect.xMax + margin, row.y,
                    costRect.x - infoRect.xMax - (margin * 2), RowHeight);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.ClampedLabel(labelRect, stuff.LabelCap);

                Text.Anchor = TextAnchor.MiddleRight;
                float totalValue = CraftUtil.ThingValue(selectedItem, stuff, SelectedQuality);
                UIUtil.ClampedLabel(costRect, "$" + totalValue.ToString("F0"));

                // Click to select
                if (Widgets.ButtonInvisible(row))
                {
                    selectedStuff = stuff;
                }
            }

            ScrollUtil.EndScrollView();
        }

        private void DrawSummary(Rect rect)
        {
            if (selectedItem == null) return;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;

            string itemName = selectedItem.LabelCap;
            if (selectedStuff != null)
                itemName += " (" + selectedStuff.LabelCap + ")";

            float perUnit = CraftUtil.ThingValue(selectedItem, selectedStuff, SelectedQuality);

            // Count picker (inventory only): "Count: [ - ][ 12 ][ + ]" on the right.
            Rect labelRect = rect;
            if (showCount)
            {
                const float countBlockW = 170f;
                labelRect = new Rect(rect.x, rect.y, rect.width - countBlockW - 8f, rect.height);

                float x = rect.xMax - countBlockW;
                Rect lblRect = new Rect(x, rect.y, 50f, rect.height);
                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.ClampedLabel(lblRect, "fcInventoryCount".Translate());
                x += 52f;

                Rect minusRect = new Rect(x, rect.y + 1f, 24f, rect.height - 2f);
                if (UIUtil.ClampedButtonText(minusRect, "-"))
                {
                    selectedCount = Mathf.Max(1, selectedCount - 1);
                    countBuffer = selectedCount.ToString();
                }
                x += 26f;

                Rect fieldRect = new Rect(x, rect.y + 1f, 40f, rect.height - 2f);
                Widgets.TextFieldNumeric(fieldRect, ref selectedCount, ref countBuffer, 1, 99999);
                x += 42f;

                Rect plusRect = new Rect(x, rect.y + 1f, 24f, rect.height - 2f);
                if (UIUtil.ClampedButtonText(plusRect, "+"))
                {
                    selectedCount++;
                    countBuffer = selectedCount.ToString();
                }
            }

            // Quality picker (left) for items that support quality. Items without CompQuality
            // keep quality == null (Normal), so the control is hidden for them.
            if (CraftUtil.ThingHasQuality(selectedItem))
            {
                const float qualityBlockW = 130f;
                Rect qualityRect = new Rect(labelRect.x, labelRect.y + 1f, qualityBlockW, labelRect.height - 2f);
                if (UIUtil.ClampedButtonText(qualityRect, TextUtil.GetQualityLabelCap(SelectedQuality)))
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    foreach (QualityCategory cat in Enum.GetValues(typeof(QualityCategory)))
                    {
                        QualityCategory captured = cat;
                        options.Add(new FloatMenuOption(TextUtil.GetQualityLabelCap(captured),
                            () => selectedQuality = captured));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                labelRect = new Rect(qualityRect.xMax + 8f, labelRect.y,
                    labelRect.width - qualityBlockW - 8f, labelRect.height);
            }

            float cost = perUnit * (showCount ? Mathf.Max(1, selectedCount) : 1);
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.ClampedLabel(labelRect, "fcPickerSummary".Translate(itemName, cost.ToString("F0")));
        }

        private void DrawButtons(Rect bar)
        {
            float buttonWidth = 120f;

            // Unequip (left)
            if (onUnequip != null)
            {
                Rect unequipRect = new Rect(bar.x, bar.y, buttonWidth, bar.height);
                if (UIUtil.ClampedButtonText(unequipRect, "FCUnitActionUnequipThing".Translate()))
                {
                    onUnequip();
                    Close();
                }
            }

            // Cancel (right)
            Rect cancelRect = new Rect(bar.xMax - buttonWidth, bar.y, buttonWidth, bar.height);
            if (UIUtil.ClampedButtonText(cancelRect, "CancelButton".Translate()))
            {
                Close();
            }

            // Confirm (left of cancel)
            Rect confirmRect = new Rect(cancelRect.x - buttonWidth - 10f, bar.y, buttonWidth, bar.height);
            if (UIUtil.ClampedButtonText(confirmRect, "FCConfirm".Translate()))
            {
                if (selectedItem == null)
                {
                    Messages.Message("fcPickerSelectItem".Translate(), MessageTypeDefOf.RejectInput, false);
                }
                else if (selectedItem.MadeFromStuff && selectedStuff == null)
                {
                    Messages.Message("fcPickerSelectStuff".Translate(), MessageTypeDefOf.RejectInput, false);
                }
                else
                {
                    // Items without CompQuality store null (Normal) — unchanged behavior.
                    QualityCategory? qualityToEmit =
                        CraftUtil.ThingHasQuality(selectedItem) ? (QualityCategory?)SelectedQuality : null;
                    if (onConfirmWithCount != null)
                        onConfirmWithCount(selectedItem, selectedStuff, Mathf.Max(1, selectedCount), qualityToEmit);
                    else
                        onConfirm(selectedItem, selectedStuff, qualityToEmit);
                    Close();
                }
            }
        }

        /// <summary>Rebuilds the item view (filter + sort) only when the search term or sort index has
        /// changed since the last build. The draw loop reads <see cref="itemFilteredCache"/>, so the
        /// full sort/allocation no longer runs every frame.</summary>
        private void EnsureItemFilter()
        {
            if (itemFilteredCache is object
                && itemFilterCacheTerm == itemSearchTerm
                && itemFilterCacheSort == itemSortIndex)
            {
                return;
            }

            List<ThingDef> filtered = string.IsNullOrEmpty(itemSearchTerm)
                ? items
                : items.Where(t => (t.label ?? t.defName).IndexOf(itemSearchTerm, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            itemFilteredCache = ApplySort(filtered, itemSortIndex);
            itemFilterCacheTerm = itemSearchTerm;
            itemFilterCacheSort = itemSortIndex;
        }

        /// <summary>Rebuilds the stuff view only when its inputs change. The key also tracks the selected
        /// item and quality because the price sort keys off <see cref="CraftUtil.ThingValue"/> and the
        /// source list (<see cref="currentStuffs"/>) is rebuilt whenever a new item is selected.</summary>
        private void EnsureStuffFilter()
        {
            if (stuffFilteredCache is object
                && stuffFilterCacheTerm == stuffSearchTerm
                && stuffFilterCacheSort == stuffSortIndex
                && stuffCacheItem == selectedItem
                && stuffCacheQuality == selectedQuality)
            {
                return;
            }

            List<ThingDef> filtered = string.IsNullOrEmpty(stuffSearchTerm)
                ? currentStuffs
                : currentStuffs.Where(s => (s.label ?? s.defName).IndexOf(stuffSearchTerm, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            stuffFilteredCache = ApplySort(filtered, stuffSortIndex, isStuffList: true);
            stuffFilterCacheTerm = stuffSearchTerm;
            stuffFilterCacheSort = stuffSortIndex;
            stuffCacheItem = selectedItem;
            stuffCacheQuality = selectedQuality;
        }

        private List<ThingDef> ApplySort(List<ThingDef> list, int sortIndex, bool isStuffList = false)
        {
            switch (sortIndex)
            {
                case 1: // Name Z-A
                    return list.OrderByDescending(t => t.label, StringComparer.OrdinalIgnoreCase).ToList();
                case 2: // Price Low-High
                    if (isStuffList && selectedItem != null)
                        return list.OrderBy(t => CraftUtil.ThingValue(selectedItem, t, SelectedQuality)).ToList();
                    return list.OrderBy(t => t.BaseMarketValue).ToList();
                case 3: // Price High-Low
                    if (isStuffList && selectedItem != null)
                        return list.OrderByDescending(t => CraftUtil.ThingValue(selectedItem, t, SelectedQuality)).ToList();
                    return list.OrderByDescending(t => t.BaseMarketValue).ToList();
                default: // 0: Name A-Z
                    return list.OrderBy(t => t.label, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }
    }
}
