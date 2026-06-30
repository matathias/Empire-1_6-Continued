using FactionColonies.util;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class SettlementWindowFC_AddTithe : Window
    {
        private WorldSettlementFC settlement;
        private ResourceFC resource;
        private Vector2 scrollBarLeft = new Vector2();
        private Vector2 scrollBarRight = new Vector2();

        private ThingDef selectedThing = null;
        private QualityCategory? selectedQuality = null;
        private QualityCategory SelectedQuality => selectedQuality ?? QualityCategory.Normal;
        private ThingDef selectedStuff = null;
        private List<ThingDef> currentStuffs = new List<ThingDef>();

        private const int margin = 5;
        private const int smallMargin = 3;
        private const int rowHeight = 23;
        private const int scrollSpacing = 16;
        private const float SearchBarHeight = 28f;
        private const float choiceBox = 45f;

        private string thingSearchTerm = "";
        private string stuffSearchTerm = "";

        private int itemSortIndex = 0;
        private int stuffSortIndex = 0;
        private static readonly string[] sortLabelKeys = { "FCTitheSortNameAZ", "FCTitheSortNameZA", "FCTitheSortPriceLow", "FCTitheSortPriceHigh" };

        public override Vector2 InitialSize
        {
            get { return new Vector2(780f, 560f); }
        }

        public SettlementWindowFC_AddTithe(WorldSettlementFC settlement, ResourceFC resource)
        {
            if (resource is null || settlement is null)
            {
                Close();
                return;
            }
            this.settlement = settlement;
            this.resource = resource;
            forcePause = false;
            draggable = true;
            doCloseX = true;
            preventCameraMotion = false;
            resizeable = true;
        }

        public override void DoWindowContents(Rect boundingBox)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            bool thingHasQuality = false;
            bool thingIsStuffable = false;
            float selectionPanelHeight = 0;
            if (selectedThing != null)
            {
                thingHasQuality = CraftUtil.ThingHasQuality(selectedThing);
                thingIsStuffable = CraftUtil.ThingIsStuffable(selectedThing);
                selectionPanelHeight = rowHeight + smallMargin + margin;
                if (thingHasQuality)
                {
                    selectionPanelHeight += rowHeight + smallMargin;
                }

                selectionPanelHeight += choiceBox;
            }

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect header = new Rect(boundingBox.x, boundingBox.y, boundingBox.width, 35f);
            Widgets.Label(header, "FCAddTitheItemHeader".Translate());
            Widgets.DrawLineHorizontal(header.x, header.yMax, header.width);

            Text.Font = GameFont.Small;
            Rect subHeader = new Rect(boundingBox.x, header.yMax, boundingBox.width, 30f);
            Widgets.Label(subHeader, settlement.Name);

            Rect iconBox = new Rect(boundingBox.x, subHeader.yMax, 30f, 30f);
            Rect labelHighlight = new Rect(iconBox.xMax + margin, iconBox.y, boundingBox.width - margin - iconBox.width, 30f);
            Rect labelText = new Rect(labelHighlight.x + smallMargin, labelHighlight.y + smallMargin, labelHighlight.width - (smallMargin * 2), labelHighlight.height - (smallMargin * 2));

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.DrawHighlight(iconBox);
            Widgets.Label(iconBox, new GUIContent(resource.def.Icon));
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.DrawHighlight(labelHighlight);
            Widgets.Label(labelText, resource.def.LabelCap);
            Rect iconAccent = new Rect(iconBox.x, iconBox.y, iconBox.width, 3f);
            Rect labelAccent = new Rect(labelHighlight.x, labelHighlight.y, labelHighlight.width, 3f);
            Widgets.DrawBoxSolid(iconAccent, resource.def.color);
            Widgets.DrawBoxSolid(labelAccent, resource.def.color);

            // Budget indicator
            Rect budgetRow = new Rect(boundingBox.x, iconBox.yMax + margin, boundingBox.width, 22f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            double totalBudget = Math.Round(resource.GetTitheIncome(), 2);
            double usedBudget = resource.autoMaxRandomTithe
                ? Math.Round(resource.titheTotalValueNoRandom, 2)
                : Math.Round(resource.titheTotalValue, 2);
            double remaining = Math.Round(totalBudget - usedBudget, 2);
            string remainingStr;
            if (remaining < 0)
                remainingStr = $"${remaining}".Colorize(Color.red);
            else if (remaining <= 0)
                remainingStr = $"${remaining}".Colorize(Color.yellow);
            else
                remainingStr = $"${remaining}".Colorize(Color.green);
            Widgets.DrawHighlight(budgetRow);
            Widgets.Label(new Rect(budgetRow.x + smallMargin, budgetRow.y, budgetRow.width - (smallMargin * 2), budgetRow.height),
                "FCTitheBudgetRemaining".Translate(remainingStr, $"${totalBudget}"));
            Text.Font = GameFont.Small;

            float panelTopY = budgetRow.yMax + margin;
            Rect drawBox = new Rect(boundingBox.x, panelTopY, boundingBox.width, boundingBox.yMax - panelTopY - selectionPanelHeight);
            Rect leftPanel = new Rect(boundingBox.x, panelTopY, (boundingBox.width - margin) / 2f, boundingBox.yMax - panelTopY - selectionPanelHeight);
            Rect rightPanel = new Rect(leftPanel.xMax + margin, leftPanel.y, leftPanel.width, leftPanel.height);
            DrawLeftPanel(leftPanel);

            if (selectedThing is null || currentStuffs is null || currentStuffs.Count == 0)
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rightPanel, "fcNoMaterialNeeded".Translate());
            }
            else
            {
                DrawRightPanel(rightPanel);
            }

            if (selectedThing != null)
            {
                Rect selectionPanel = new Rect(boundingBox.x, boundingBox.yMax - selectionPanelHeight, boundingBox.width, selectionPanelHeight);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;

                float panelY = selectionPanel.y + margin;

                if (thingHasQuality)
                {
                    QualityCategory maxQuality = QualityCategory.Legendary;
                    resource.CanSetTitheQuality(out maxQuality);
                    List<QualityCategory> categoryList = resource.GetValidTitheQualities(maxQuality);

                    Rect qualityLabel = new Rect(selectionPanel.x + margin, panelY, 60f, rowHeight);
                    Rect qualityButton = new Rect(qualityLabel.xMax + smallMargin, panelY, 120f, rowHeight);
                    Widgets.Label(qualityLabel, "Quality".Translate() + ":");
                    if (Widgets.ButtonText(qualityButton, TextUtil.GetQualityLabelCap(selectedQuality)))
                    {
                        List<FloatMenuOption> options = new List<FloatMenuOption>();
                        foreach (QualityCategory cat in categoryList)
                        {
                            options.Add(new FloatMenuOption(TextUtil.GetQualityLabelCap(cat), delegate
                            {
                                selectedQuality = cat;
                            }));
                        }
                        Find.WindowStack.Add(new FloatMenu(options));
                    }
                    panelY += rowHeight + smallMargin;
                }

                Text.Anchor = TextAnchor.MiddleCenter;
                Rect selectedLabel = new Rect(selectionPanel.x + margin, panelY, selectionPanel.width - (margin * 2), choiceBox);
                Widgets.DrawHighlight(selectedLabel);
                string stuffStr = selectedStuff is null ? "" : $"{selectedStuff.LabelCap} ";
                string qualityStr = (thingHasQuality && !(selectedQuality is null)) ? $" ({TextUtil.GetQualityLabelCap(selectedQuality)})" : "";
                string totalCost;
                if (!(selectedThing is null) && (!thingIsStuffable || !(selectedStuff is null)) && (!thingHasQuality || !(selectedQuality is null)))
                {
                    totalCost = $"${Math.Round(resource.TitheThingValue(selectedThing, selectedStuff, selectedQuality ?? QualityCategory.Normal))}";
                }
                else
                {
                    totalCost = "  ";
                }
                Widgets.Label(selectedLabel, $"{stuffStr}{(selectedThing?.LabelCap ?? "null")}{qualityStr}\n{totalCost}");
                panelY += selectedLabel.height + margin;

                float buttonWidth = (selectionPanel.width - (margin * 3)) / 2f;
                Rect cancelButton = new Rect(selectionPanel.x + margin, panelY, buttonWidth, rowHeight);
                Rect confirmButton = new Rect(cancelButton.xMax + margin, panelY, buttonWidth, rowHeight);

                if (Widgets.ButtonText(cancelButton, "FCClearSelection".Translate()))
                {
                    selectedThing = null;
                    selectedStuff = null;
                    selectedQuality = null;
                }

                bool canConfirm = true;
                string confirmTooltip = "";

                if (thingIsStuffable && selectedStuff == null)
                {
                    canConfirm = false;
                    confirmTooltip = "FCMustSelectStuff".Translate();
                }

                ThingQualityTuple tuple = new ThingQualityTuple
                {
                    thingDef = selectedThing,
                    quality = thingHasQuality ? SelectedQuality : QualityCategory.Normal,
                    stuffDef = thingIsStuffable ? selectedStuff : null
                };
                if (!canConfirm)
                {
                    GUI.color = Color.gray;
                }
                if (Widgets.ButtonText(confirmButton, "Confirm".Translate()))
                {
                    if (canConfirm)
                    {
                        // The same thing may appear multiple times in the priority list, so always
                        // append a fresh entry at lowest priority. No affordability gate — over-budget
                        // entries are allowed and persist across cycles.
                        resource.AddTitheEntry(tuple, 1);
                        selectedThing = null;
                        selectedStuff = null;
                        selectedQuality = null;
                    }
                }
                if (!canConfirm)
                {
                    GUI.color = Color.white;
                    TooltipHandler.TipRegion(confirmButton, confirmTooltip);
                }
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }
        private void DrawLeftPanel(Rect boundingBox)
        {
            float curY = boundingBox.y;

            // Panel header
            Rect headerRow = new Rect(boundingBox.x, curY, boundingBox.width, SearchBarHeight);
            Widgets.DrawHighlight(headerRow);
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(headerRow.x + margin, headerRow.y, 60f, headerRow.height), "FCTitheItems".Translate());
            float sortBtnW = 120f;
            Rect sortBtn = new Rect(headerRow.xMax - margin - 65f - margin - sortBtnW, headerRow.y + 2, sortBtnW, headerRow.height - 4);
            if (Widgets.ButtonText(sortBtn, "FCSortBy".Translate(sortLabelKeys[itemSortIndex].Translate())))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                for (int s = 0; s < sortLabelKeys.Length; s++)
                {
                    int captured = s;
                    options.Add(new FloatMenuOption(sortLabelKeys[s].Translate(), () =>
                    {
                        itemSortIndex = captured;
                        scrollBarLeft = Vector2.zero;
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            curY = headerRow.yMax;

            // Search bar
            Rect searchRect = new Rect(boundingBox.x, curY, boundingBox.width, SearchBarHeight);
            thingSearchTerm = Widgets.TextField(searchRect, thingSearchTerm);
            if (string.IsNullOrEmpty(thingSearchTerm))
            {
                Color prevColor = GUI.color;
                GUI.color = Color.gray;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(searchRect.x + 5f, searchRect.y, searchRect.width - 10f, searchRect.height),
                    "FCSearchItems".Translate());
                GUI.color = prevColor;
            }
            curY = searchRect.yMax;

            List<ThingDef> thingsList = string.IsNullOrEmpty(thingSearchTerm)
                ? resource.GenerateThingDefList()
                : resource.GenerateThingDefList().Where(t => (t.label ?? t.defName).IndexOf(thingSearchTerm, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            // Apply sort
            thingsList = ApplySort(thingsList, itemSortIndex);

            // Actual scrollbox
            Rect drawBox = new Rect(boundingBox.x, curY + margin, boundingBox.width, boundingBox.yMax - curY - margin);
            Rect outerListBox = new Rect(drawBox.x + 2, drawBox.y + 2, drawBox.width - 4, drawBox.height - 4);
            float listHeight = thingsList.Count * rowHeight;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(headerRow.xMax - margin - 65f - (listHeight > outerListBox.height ? scrollSpacing : 0), headerRow.y, 60f, headerRow.height), "FCTitheBasePrice".Translate());
            Text.Font = GameFont.Small;
            Widgets.DrawMenuSection(drawBox);

            Rect innerScrollBox = ScrollUtil.BeginScrollView(outerListBox, ref scrollBarLeft, listHeight);

            for (int i = 0; i < thingsList.Count; i++)
            {
                ThingDef iThing = thingsList[i];
                Rect row = new Rect(innerScrollBox.x, innerScrollBox.y + (i * rowHeight), innerScrollBox.width, rowHeight);
                Rect icon = new Rect(row.x + margin, row.y, rowHeight, rowHeight);
                Rect info = new Rect(icon.xMax, row.y + 2, rowHeight - 4, rowHeight - 4);
                Rect valueLabel = new Rect(row.xMax - margin - 70f, row.y, 60f, rowHeight);
                Rect label = new Rect(info.xMax + margin, row.y, valueLabel.x - info.xMax - (margin * 2), rowHeight);

                if (selectedThing == iThing)
                {
                    Widgets.DrawHighlightSelected(row);
                }
                else if (i % 2 == 0)
                {
                    Widgets.DrawHighlight(row);
                }

                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(icon, new GUIContent(iThing.uiIcon));
                Widgets.InfoCardButton(info, iThing);
                if (Widgets.ButtonInvisible(row))
                {
                    selectedThing = iThing;

                    currentStuffs.Clear();
                    if (iThing.MadeFromStuff)
                    {
                        currentStuffs.AddRange(resource.GetStuffListForThingDef(iThing));
                        currentStuffs.SortBy(s => s.label);
                    }

                    // Keep selectedStuff if it's still valid for the new item
                    if (selectedStuff != null && (currentStuffs.Count == 0 || !currentStuffs.Contains(selectedStuff)))
                    {
                        selectedStuff = null;
                    }
                    if (CraftUtil.ThingHasQuality(iThing))
                    {
                        if (selectedQuality == null)
                        {
                            selectedQuality = QualityCategory.Normal;
                        }
                    }
                    else
                    {
                        selectedQuality = null;
                    }
                }
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(label, iThing.LabelCap);
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(valueLabel, $"${Math.Round(iThing.BaseMarketValue)}");
                Text.Anchor = TextAnchor.MiddleLeft;
            }

            ScrollUtil.EndScrollView();
        }
        private void DrawRightPanel(Rect boundingBox)
        {
            float curY = boundingBox.y;

            // Panel header
            Rect headerRow = new Rect(boundingBox.x, curY, boundingBox.width, SearchBarHeight);
            Widgets.DrawHighlight(headerRow);
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(headerRow.x + margin, headerRow.y, 80f, headerRow.height), "FCTitheStuff".Translate());
            float sortBtnW = 120f;
            Rect sortBtn = new Rect(headerRow.xMax - margin - 75f - margin - sortBtnW, headerRow.y + 2, sortBtnW, headerRow.height - 4);
            if (Widgets.ButtonText(sortBtn, "FCSortBy".Translate(sortLabelKeys[stuffSortIndex].Translate())))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                for (int s = 0; s < sortLabelKeys.Length; s++)
                {
                    int captured = s;
                    options.Add(new FloatMenuOption(sortLabelKeys[s].Translate(), () =>
                    {
                        stuffSortIndex = captured;
                        scrollBarRight = Vector2.zero;
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            curY = headerRow.yMax;

            // Search bar
            Rect searchRect = new Rect(boundingBox.x, curY, boundingBox.width, SearchBarHeight);
            stuffSearchTerm = Widgets.TextField(searchRect, stuffSearchTerm);
            if (string.IsNullOrEmpty(stuffSearchTerm))
            {
                Color prevColor = GUI.color;
                GUI.color = Color.gray;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(searchRect.x + 5f, searchRect.y, searchRect.width - 10f, searchRect.height),
                    "FCSearchMaterials".Translate());
                GUI.color = prevColor;
            }
            curY = searchRect.yMax;

            List<ThingDef> stuffList = string.IsNullOrEmpty(stuffSearchTerm)
                ? currentStuffs
                : currentStuffs.Where(t => (t.label ?? t.defName).IndexOf(stuffSearchTerm, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            // Apply sort
            stuffList = ApplySort(stuffList, stuffSortIndex, isStuffList: true);

            // Actual scrollbox
            Rect drawBox = new Rect(boundingBox.x, curY + margin, boundingBox.width, boundingBox.yMax - curY - margin);
            Rect outerListBox = new Rect(drawBox.x + 2, drawBox.y + 2, drawBox.width - 4, drawBox.height - 4);
            float listHeight = stuffList.Count * rowHeight;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(headerRow.xMax - margin - 65f - (listHeight > outerListBox.height ? scrollSpacing : 0), headerRow.y, 60f, headerRow.height), "FCTitheMaterialPrice".Translate());
            Text.Font = GameFont.Small;
            Widgets.DrawMenuSection(drawBox);

            Rect innerScrollBox = ScrollUtil.BeginScrollView(outerListBox, ref scrollBarRight, listHeight);

            for (int i = 0; i < stuffList.Count; i++)
            {
                ThingDef iStuff = stuffList[i];
                Rect row = new Rect(innerScrollBox.x, innerScrollBox.y + (i * rowHeight), innerScrollBox.width, rowHeight);
                Rect icon = new Rect(row.x + margin, row.y, rowHeight, rowHeight);
                Rect info = new Rect(icon.xMax, row.y + 2, rowHeight - 4, rowHeight - 4);
                Rect valueLabel = new Rect(row.xMax - margin - 70f, row.y, 60f, rowHeight);
                Rect label = new Rect(info.xMax + margin, row.y, valueLabel.x - info.xMax - (margin * 2), rowHeight);

                if (selectedStuff == iStuff)
                {
                    Widgets.DrawHighlightSelected(row);
                }
                else if (i % 2 == 0)
                {
                    Widgets.DrawHighlight(row);
                }

                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(icon, new GUIContent(iStuff.uiIcon));
                Widgets.InfoCardButton(info, iStuff);
                if (Widgets.ButtonInvisible(row))
                {
                    selectedStuff = iStuff;
                }
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(label, iStuff.LabelCap);
                Text.Anchor = TextAnchor.MiddleRight;
                float stuffPrice = CraftUtil.ThingValue(selectedThing, iStuff, SelectedQuality);
                Widgets.Label(valueLabel, $"${Math.Round(stuffPrice)}");
                Text.Anchor = TextAnchor.MiddleLeft;
            }

            ScrollUtil.EndScrollView();
        }

        private List<ThingDef> ApplySort(List<ThingDef> list, int sortIndex, bool isStuffList = false)
        {
            switch (sortIndex)
            {
                case 1: // Name Z-A
                    return list.OrderByDescending(t => t.label, StringComparer.OrdinalIgnoreCase).ToList();
                case 2: // Price Low-High
                    if (isStuffList && selectedThing != null)
                        return list.OrderBy(t => CraftUtil.ThingValue(selectedThing, t, SelectedQuality)).ToList();
                    return list.OrderBy(t => t.BaseMarketValue).ToList();
                case 3: // Price High-Low
                    if (isStuffList && selectedThing != null)
                        return list.OrderByDescending(t => CraftUtil.ThingValue(selectedThing, t, SelectedQuality)).ToList();
                    return list.OrderByDescending(t => t.BaseMarketValue).ToList();
                default: // 0: Name A-Z
                    return list.OrderBy(t => t.label, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }
    }
}
