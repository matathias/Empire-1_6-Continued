using FactionColonies.util;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    class FCBuildingWindow : Window
    {
        readonly WorldSettlementFC settlement;
        readonly int buildingSlot;
        readonly BuildingFCDef buildingDef;
        readonly List<BuildingFCDef> buildingList;
        readonly List<BuildingFCDef> filteredBuildingList;
        readonly FactionFC factionfc;
        readonly TaggedString buildingDesc;

        // Selection state
        private BuildingFCDef selectedBuilding = null;
        private Vector2 scrollPosition = Vector2.zero;
        private Vector2 rightPanelScroll = Vector2.zero;

        private float fullScrollHeight = 90f;

        // Slot upgrade list (upgrades available for the current slot's building)
        private readonly List<BuildingUpgradeEntry> slotUpgradeList = new List<BuildingUpgradeEntry>();

        // Locked buildings (above current tech level but otherwise valid)
        private bool showLockedBuildings = false;
        private readonly List<BuildingFCDef> lockedBuildingList;
        private readonly HashSet<BuildingFCDef> lockedBuildingSet;

        // Collapsible section state
        private bool slotUpgradesExpanded = true;
        private bool upgradesExpanded = true;
        private bool requiredByExpanded = true;

        // Layout cache — only recomputed when width changes or filter is changed
        private float lastLayoutWidth = -1f;
        private bool layoutDirty = true;
        private readonly List<float> cachedRowHeights = new List<float>();
        private float slotUpgradesHeight = 0f;
        private string buildingSearchTerm = "";

        /* To deal with a variable number of resources (and variable resources in general), we use an int for
         * the filter. The value of the filter, and the corresponding label, are set in WorldObjectComp_SettlementBuildings
         */
        private int currentFilter = 0;
        private int filterSize = 0;
        private readonly Dictionary<string, string> filterTruncateCache = new Dictionary<string, string>();
        private int filterRows = 2;
        private const int filterButtonsPerRow = 4;
        private static readonly int filterButtonHeight = 25;
        private static readonly int filterRowHeight = 30;

        // Layout constants
        private const float margin = 5f;
        private const float smallMargin = 3f;
        private const float panelGap = 10f;
        private const float leftPanelRatio = 0.45f;
        private const float listIconSize = 48f;
        private const float detailIconSize = 64f;
        private const float actionButtonHeight = 35f;
        private const float actionButtonWidth = 200f;
        private const float minWindowWidth = 600f;
        private const float headerHeight = 30f;
        private static readonly Color selectionColor = new Color(0.2f, 0.5f, 0.8f, 0.8f);
        private const float indentWidth = 20f;
        private const float collapsibleHeaderHeight = 22f;
        private const float SearchBarHeight = 28f;
        private const float toggleHeight = 24f;

        Rect FilterArea;
        Rect SearchBarArea;
        Rect ToggleArea;

        public override Vector2 InitialSize => new Vector2(
            Math.Max(FCSettings.buildingWindowWidth, minWindowWidth),
            Math.Max(FCSettings.buildingWindowHeight, 400f)
        );

        public override void PreClose()
        {
            base.PreClose();
            FCSettings.buildingWindowWidth = windowRect.width;
            FCSettings.buildingWindowHeight = windowRect.height;
            LoadedModManager.GetMod<FactionColoniesMod>().WriteSettings();
        }

        public override void PreOpen()
        {
            base.PreOpen();
            if (settlement.BuildingsComp is null)
            {
                LogUtil.Warning($"Attempted to open buildings window for settlement {settlement.Name} with NULL BuildingsComp");
                this.Close();
            }
        }

        #region Layout Calculation

        private void CalculateLayout(float leftPanelWidth)
        {
            if (leftPanelWidth == lastLayoutWidth && !layoutDirty) return;
            lastLayoutWidth = leftPanelWidth;
            layoutDirty = false;
            filterTruncateCache.Clear();

            slotUpgradesHeight = CalculateSlotUpgradesHeight(leftPanelWidth);
            FilterArea = new Rect(margin, margin + headerHeight + slotUpgradesHeight, leftPanelWidth - (margin * 2), filterRowHeight * filterRows);
            SearchBarArea = new Rect(0, FilterArea.yMax + smallMargin, leftPanelWidth, SearchBarHeight);
            if (lockedBuildingList.Count > 0)
                ToggleArea = new Rect(margin, SearchBarArea.yMax + smallMargin, leftPanelWidth - (margin * 2), toggleHeight);
            CalculateScrollHeight(leftPanelWidth);
        }

        private float CalculateBuildingCardHeight(BuildingFCDef building, float cardWidth)
        {
            float descWidth = cardWidth - listIconSize - margin * 3;
            TaggedString desc = settlement.BuildingsComp?.GetBuildingDesc(building) ?? TaggedString.Empty;
            GameFont tmp = Text.Font;
            Text.Font = GameFont.Tiny;
            float textHeight = Text.CalcHeight(desc.RawText, descWidth);
            Text.Font = tmp;
            float bodyHeight = Math.Max(listIconSize, textHeight);
            return 22f + smallMargin + bodyHeight + margin;
        }

        private void CalculateScrollHeight(float panelWidth)
        {
            float cardWidth = panelWidth - 16f; // account for scrollbar
            cachedRowHeights.Clear();
            fullScrollHeight = 0;
            for (int i = 0; i < filteredBuildingList.Count; i++)
            {
                float rowH = CalculateBuildingCardHeight(filteredBuildingList[i], cardWidth) + smallMargin;
                cachedRowHeights.Add(rowH);
                fullScrollHeight += rowH;
            }
        }

        private float CalculateSlotUpgradesHeight(float panelWidth)
        {
            if (slotUpgradeList.Count == 0) return 0;
            float h = collapsibleHeaderHeight;
            if (slotUpgradesExpanded)
            {
                float cardWidth = panelWidth - 16f;
                foreach (var entry in slotUpgradeList)
                {
                    h += CalculateBuildingCardHeight(entry.def, cardWidth) + smallMargin;
                }
            }
            h += margin * 2;
            return h;
        }

        private void DrawCollapsibleHeader(float x, float curY, float width, string label, ref bool expanded)
        {
            Rect headerRect = new Rect(x, curY, width, collapsibleHeaderHeight);
            Widgets.DrawHighlight(headerRect);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            string arrow = expanded ? "▼ " : "▶ ";
            UIUtil.DrawColoredLabel(
                new Rect(x + margin, curY, width - margin * 2, collapsibleHeaderHeight),
                arrow + label,
                new Color(1f, 1f, 1f, 0.7f));
            if (Widgets.ButtonInvisible(headerRect))
            {
                expanded = !expanded;
                layoutDirty = true;
            }
        }

        private bool HasUnmetRequirements(BuildingFCDef building)
        {
            if (building.requiredBuildings.Count == 0) return false;
            foreach (BuildingFCDef req in building.requiredBuildings)
            {
                if (!settlement.BuildingsComp.HasBuildingOrUpgrade(req)) return true;
                if (req == buildingDef) return true;
            }
            return false;
        }

        private static string GetResearchRequirementForTechLevel(TechLevel level)
        {
            TechLevelBarrier barrier = FactionCache.GetTechBarrier(level);
            string label = barrier?.DisplayLabel;
            if (!label.NullOrEmpty()) return label;
            return level.ToStringHuman();
        }

        #endregion

        #region Filter Buttons

        private void DrawFilterButtons()
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;
            Text.Font = GameFont.Tiny;

            float buttonWidth = (FilterArea.width - (smallMargin * filterButtonsPerRow - 1)) / filterButtonsPerRow;
            float buttonHeight = filterButtonHeight;

            for (int i = 0; i < filterSize; i++)
            {
                int row = i / filterButtonsPerRow;
                int col = i % filterButtonsPerRow;

                Rect buttonRect = new Rect(
                    FilterArea.x + (col * buttonWidth + smallMargin),
                    FilterArea.y + (row * (buttonHeight + smallMargin)),
                    buttonWidth - smallMargin,
                    buttonHeight
                );

                bool isSelected = currentFilter == i;

                if (isSelected)
                {
                    Widgets.DrawBoxSolid(buttonRect, selectionColor);
                    Widgets.DrawBox(buttonRect, 1);
                }
                else
                {
                    if (Widgets.ButtonInvisible(buttonRect))
                    {
                        currentFilter = i;
                        ApplyFilter();
                    }
                    Widgets.DrawAtlas(buttonRect, Widgets.ButtonBGAtlas);
                }

                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.white;
                Texture2D filterIcon = settlement.BuildingsComp?.GetIconForFilter(i);
                string filterLabel = settlement.BuildingsComp?.GetLabelForFilter(i) ?? "";
                if (filterIcon != null)
                {
                    float iconSize = buttonRect.height - 4f;
                    Rect iconRect = new Rect(buttonRect.x + 2f, buttonRect.y + 2f, iconSize, iconSize);
                    GUI.DrawTexture(iconRect, filterIcon);
                    Rect labelRect = new Rect(iconRect.xMax + 2f, buttonRect.y, buttonRect.width - iconSize - 6f, buttonRect.height);
                    string truncatedLabel = filterLabel.Truncate(labelRect.width, filterTruncateCache);
                    UIUtil.ClampedLabel(labelRect, truncatedLabel);
                    if (truncatedLabel != filterLabel)
                    {
                        TooltipHandler.TipRegion(buttonRect, filterLabel);
                    }
                }
                else
                {
                    string truncatedLabel = filterLabel.Truncate(buttonRect.width, filterTruncateCache);
                    UIUtil.ClampedLabel(buttonRect, truncatedLabel);
                    if (truncatedLabel != filterLabel)
                    {
                        TooltipHandler.TipRegion(buttonRect, filterLabel);
                    }
                }
                GUI.color = Color.white;

                if (isSelected && Widgets.ButtonInvisible(buttonRect))
                {
                    currentFilter = 0;
                    ApplyFilter();
                }
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        #endregion

        #region Filter Logic

        private void ApplyFilter()
        {
            filteredBuildingList.Clear();
            foreach (var building in buildingList)
            {
                if (ShouldShowBuilding(building))
                {
                    filteredBuildingList.Add(building);
                }
            }

            if (showLockedBuildings)
            {
                foreach (var building in lockedBuildingList)
                {
                    if (ShouldShowBuilding(building))
                    {
                        filteredBuildingList.Add(building);
                    }
                }
            }

            if (selectedBuilding != null && !filteredBuildingList.Contains(selectedBuilding)
                && !slotUpgradeList.Any(e => e.def == selectedBuilding))
            {
                selectedBuilding = null;
            }

            layoutDirty = true;
        }

        private bool ShouldShowBuilding(BuildingFCDef building)
        {
            if (!string.IsNullOrEmpty(buildingSearchTerm)
                && (building.label ?? building.defName).IndexOf(buildingSearchTerm, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
            return settlement.BuildingsComp?.FilterBuilding(currentFilter, building) ?? true;
        }

        #endregion

        #region Main DoWindowContents

        public override void DoWindowContents(Rect inRect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            // Window header
            Rect headerRect = new Rect(inRect.x, inRect.y, inRect.width, headerHeight);
            Widgets.DrawHighlight(headerRect);
            Widgets.DrawHighlight(headerRect);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            UIUtil.ClampedLabel(headerRect, "Empire_BuildingWindow_Header".Translate(settlement.Name));

            float bodyTop = headerRect.yMax + margin;
            float bodyHeight = inRect.height - headerHeight - margin;

            float leftWidth = (inRect.width - panelGap) * leftPanelRatio;
            float rightWidth = inRect.width - leftWidth - panelGap;

            Rect leftPanel = new Rect(inRect.x, bodyTop, leftWidth, bodyHeight);
            Rect rightPanel = new Rect(leftPanel.xMax + panelGap, bodyTop, rightWidth, bodyHeight);

            CalculateLayout(leftWidth);

            DrawLeftPanel(leftPanel);
            DrawRightPanel(rightPanel);

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        #endregion

        #region Left Panel

        private void DrawLeftPanel(Rect panel)
        {
            // Slot upgrades section (above filters)
            if (slotUpgradeList.Count > 0)
            {
                float upgradeY = panel.y;
                DrawCollapsibleHeader(panel.x, upgradeY, panel.width,
                    "Empire_BuildingWindow_SlotUpgrades".Translate(), ref slotUpgradesExpanded);
                upgradeY += collapsibleHeaderHeight;

                if (slotUpgradesExpanded)
                {
                    float cardWidth = panel.width - 16f;
                    for (int i = 0; i < slotUpgradeList.Count; i++)
                    {
                        upgradeY += smallMargin;
                        BuildingUpgradeEntry entry = slotUpgradeList[i];
                        float cardHeight = CalculateBuildingCardHeight(entry.def, cardWidth);
                        Rect cardRect = new Rect(panel.x, upgradeY, cardWidth, cardHeight);

                        bool isSelected = selectedBuilding == entry.def;
                        Widgets.DrawHighlight(cardRect);
                        if (isSelected)
                            Widgets.DrawBoxSolid(cardRect, selectionColor);
                        else if (i % 2 == 0)
                            Widgets.DrawHighlight(cardRect);
                        if (Mouse.IsOver(cardRect))
                            Widgets.DrawHighlight(cardRect);
                        if (Widgets.ButtonInvisible(cardRect))
                        {
                            if (selectedBuilding != entry.def)
                            {
                                selectedBuilding = entry.def;
                                rightPanelScroll = Vector2.zero;
                            }
                        }

                        DrawBuildingCard(cardRect, entry.def);
                        upgradeY += cardHeight;
                    }
                }

            }

            DrawFilterButtons();

            // Recalculate if filter changed mid-frame
            if (layoutDirty)
            {
                CalculateLayout(panel.width);
            }

            // Search bar
            string prevSearch = buildingSearchTerm;
            Text.Font = GameFont.Small;
            buildingSearchTerm = Widgets.TextField(SearchBarArea, buildingSearchTerm);
            if (buildingSearchTerm != prevSearch)
                ApplyFilter();
            if (string.IsNullOrEmpty(buildingSearchTerm))
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.DrawColoredLabel(new Rect(SearchBarArea.x + 5f, SearchBarArea.y,
                    SearchBarArea.width - 10f, SearchBarArea.height),
                    "FCSearchBuildings".Translate(), Color.gray);
            }

            // Locked building toggle
            if (lockedBuildingList.Count > 0)
            {
                bool prevShow = showLockedBuildings;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.CheckboxLabeled(ToggleArea, "Empire_BuildingWindow_ShowLocked".Translate(), ref showLockedBuildings);
                if (showLockedBuildings != prevShow)
                    ApplyFilter();
            }

            // Recalculate if filter changed mid-frame
            if (layoutDirty)
            {
                CalculateLayout(panel.width);
            }

            float scrollTop = (lockedBuildingList.Count > 0 ? ToggleArea.yMax : SearchBarArea.yMax) + margin;
            Rect outRect = new Rect(panel.x, scrollTop, panel.width, panel.yMax - scrollTop);

            Rect viewRect = ScrollUtil.BeginScrollView(outRect, ref scrollPosition, fullScrollHeight);
            var ls = new Listing_Standard();
            ls.Begin(viewRect);

            for (int i = 0; i < filteredBuildingList.Count; i++)
            {
                DrawBuildingListItem(ls, filteredBuildingList[i], i);
            }

            ls.End();
            ScrollUtil.EndScrollView();
        }

        /// <summary>
        /// Draws a building card: full-width name row (highlighted, name left, cost right),
        /// icon below left, description to the right of the icon.
        /// </summary>
        private void DrawBuildingCard(Rect row, BuildingFCDef building)
        {
            // Full-width name row with cost right-aligned
            Rect nameRect = new Rect(row.x, row.y, row.width, 22f);
            Rect nameText = new Rect(nameRect.x + margin, nameRect.y, nameRect.width - (margin * 2), nameRect.height);
            Widgets.DrawHighlight(nameRect);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.ClampedLabel(nameText, building.LabelCap);
            Text.Anchor = TextAnchor.MiddleRight;
            UIUtil.ClampedLabel(nameText, "FCCost".Translate() + ": " + (settlement.BuildingsComp?.GetBuildingCost(building) ?? (int)building.cost));

            // Icon below the name row
            float contentY = nameRect.yMax + smallMargin;
            Rect iconRect = new Rect(row.x + margin, contentY, listIconSize, listIconSize);
            Widgets.DrawMenuSection(iconRect);
            Widgets.DrawLightHighlight(iconRect);
            Widgets.ButtonImage(iconRect, building.Icon);

            // Description to the right of the icon
            float descX = iconRect.xMax + margin;
            float descWidth = row.xMax - descX - margin;
            TaggedString desc = settlement.BuildingsComp?.GetBuildingDesc(building) ?? TaggedString.Empty;
            Rect descRect = new Rect(descX, contentY, descWidth, row.yMax - contentY - margin);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.Label(descRect, desc);
        }

        private void DrawBuildingListItem(Listing_Standard ls, BuildingFCDef building, int index)
        {
            float thisRowHeight = cachedRowHeights[index];
            Rect fullrow = ls.GetRect(thisRowHeight);
            Rect row = new Rect(fullrow.x, fullrow.y + smallMargin, fullrow.width, fullrow.height - smallMargin);

            bool isSelected = selectedBuilding == building;
            bool unmetReqs = HasUnmetRequirements(building);
            bool isLocked = lockedBuildingSet.Contains(building);

            // Background layers
            Widgets.DrawHighlight(row);
            if (isSelected)
            {
                Widgets.DrawBoxSolid(row, selectionColor);
            }
            else if (index % 2 == 0)
            {
                Widgets.DrawHighlight(row);
            }

            // Hover highlight
            if (Mouse.IsOver(row))
            {
                Widgets.DrawHighlight(row);
            }

            // Click handler — locked buildings are still selectable for inspection
            if (Widgets.ButtonInvisible(row))
            {
                if (selectedBuilding != building)
                {
                    selectedBuilding = building;
                    rightPanelScroll = Vector2.zero;
                }
            }

            if (isLocked || unmetReqs)
                GUI.color = new Color(1f, 1f, 1f, 0.4f);
            DrawBuildingCard(row, building);
            GUI.color = Color.white;

            // Lock icon overlay
            if (isLocked)
            {
                float lockSize = 20f;
                Rect lockRect = new Rect(row.xMax - lockSize - margin, row.y + margin, lockSize, lockSize);
                GUI.DrawTexture(lockRect, TexLoad.buildingLocked);
            }
        }

        #endregion

        #region Right Panel

        private void DrawRightPanel(Rect panel)
        {
            // Section A: Current Slot (dynamic height)
            float slotHeight = CalculateCurrentSlotHeight(panel.width);
            Rect currentSlotRect = new Rect(panel.x, panel.y, panel.width, slotHeight);
            DrawCurrentSlot(currentSlotRect);

            float nextY = currentSlotRect.yMax;

            // Demolish button (only for actual buildings, not Empty or Construction)
            bool canDemolish = buildingDef != BuildingFCDefOf.Empty && buildingDef != BuildingFCDefOf.Construction;
            if (canDemolish)
            {
                nextY += smallMargin;
                Rect demolishRect = new Rect(
                    panel.x + (panel.width - actionButtonWidth) / 2f,
                    nextY,
                    actionButtonWidth,
                    actionButtonHeight
                );
                bool isRequired = settlement.BuildingsComp?.IsBuildingRequiredByOther(buildingDef) == true;
                if (isRequired)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.4f);
                    UIUtil.ClampedButtonText(demolishRect, "FCDemolish".Translate());
                    GUI.color = Color.white;
                    List<BuildingFCDef> dependents = settlement.BuildingsComp.GetBuildingsDependingOn(buildingDef);
                    string depNames = string.Join(", ", dependents.Select(d => d.LabelCap.ToString()));
                    TooltipHandler.TipRegion(demolishRect, "Empire_BuildingWindow_CannotDemolishRequired".Translate(depNames));
                }
                else if (UIUtil.ClampedButtonText(demolishRect, "FCDemolish".Translate()))
                {
                    int demolishCost = (int)Math.Round(buildingDef.cost * 0.5);
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "FCDemolishConfirmation".Translate(buildingDef.LabelCap, demolishCost),
                        delegate
                        {
                            if (!PaymentUtil.TryPaySilver(demolishCost, PaymentUtil.Reason_BuildingDemolition, settlement))
                            {
                                Messages.Message("FCNotEnoughSilverDemolish".Translate(), MessageTypeDefOf.RejectInput);
                                return;
                            }
                            settlement.DeconstructBuilding(buildingSlot);
                            Messages.Message("FCBuildingDemolished".Translate(buildingDef.LabelCap), MessageTypeDefOf.PositiveEvent);
                            Find.WindowStack.TryRemove(this);
                        }
                    ));
                }
                nextY = demolishRect.yMax;
            }

            // Section B: Selected Building Detail (scrollable)
            float detailTop = nextY + margin * 2;
            Rect detailRect = new Rect(panel.x, detailTop, panel.width, panel.yMax - detailTop);
            DrawSelectedBuildingDetail(detailRect);
        }

        private float CalculateCurrentSlotHeight(float panelWidth)
        {
            float inner = margin * 2;
            float contentWidth = panelWidth - inner * 2;
            // label(18) + smallMargin + max(icon, name(30) + smallMargin + descHeight) + bottom padding
            float nameWidth = contentWidth - detailIconSize - margin;
            GameFont tmp = Text.Font;
            Text.Font = GameFont.Tiny;
            float descHeight = Text.CalcHeight(buildingDesc.RawText, nameWidth);
            Text.Font = tmp;
            float rightSide = 30f + smallMargin + descHeight;
            float bodyHeight = Math.Max(detailIconSize, rightSide);
            return margin + 18f + smallMargin + bodyHeight + inner;
        }

        private void DrawCurrentSlot(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Widgets.DrawHighlight(rect);

            float inner = margin * 2;
            float contentX = rect.x + inner;
            float contentWidth = rect.width - inner * 2;

            // Section label
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            string slotLabel = (buildingDef == BuildingFCDefOf.Empty)
                ? "Empire_BuildingWindow_EmptySlot".Translate()
                : "Empire_BuildingWindow_CurrentBuilding".Translate();
            Rect labelRect = new Rect(contentX, rect.y + margin, contentWidth, 18f);
            UIUtil.DrawColoredLabel(labelRect, slotLabel, new Color(1f, 1f, 1f, 0.7f));

            // Icon
            float iconY = labelRect.yMax + smallMargin;
            Rect iconRect = new Rect(contentX, iconY, detailIconSize, detailIconSize);
            Widgets.DrawMenuSection(iconRect);
            Widgets.DrawLightHighlight(iconRect);
            Widgets.ButtonImage(iconRect, buildingDef.Icon);

            // Name
            float nameX = iconRect.xMax + margin;
            float nameWidth = rect.xMax - inner - nameX;
            Rect nameRect = new Rect(nameX, iconY, nameWidth, 30f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.ClampedLabel(nameRect, buildingDef.LabelCap);

            // Description (compact)
            Rect descRect = new Rect(nameX, nameRect.yMax + smallMargin, nameWidth, rect.yMax - nameRect.yMax - smallMargin - inner);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.Label(descRect, buildingDesc);
        }

        private void DrawSelectedBuildingDetail(Rect rect)
        {
            if (selectedBuilding == null)
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.DrawColoredLabel(rect,
                    "Empire_BuildingWindow_SelectBuilding".Translate(),
                    new Color(1f, 1f, 1f, 0.5f));
                return;
            }

            // Calculate total content height for scrolling
            float contentHeight = CalculateDetailContentHeight(rect.width - ScrollUtil.ScrollbarWidth);

            // Reserve space for the button at the bottom (outside scroll)
            Rect buttonArea = new Rect(rect.x, rect.yMax - actionButtonHeight - margin, rect.width, actionButtonHeight + margin);
            Rect scrollOutRect = new Rect(rect.x, rect.y, rect.width, rect.height - buttonArea.height);

            Rect scrollViewRect = ScrollUtil.BeginScrollView(scrollOutRect, ref rightPanelScroll, contentHeight);

            float curY = scrollViewRect.y;
            float w = scrollViewRect.width;

            // C1: Building name
            Rect nameRect = new Rect(scrollViewRect.x, curY, w, 30f);
            Rect nameText = new Rect(nameRect.x + margin, nameRect.y, nameRect.width - (margin * 2), nameRect.height);
            Widgets.DrawHighlight(nameRect);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.ClampedLabel(nameText, selectedBuilding.LabelCap);
            curY = nameRect.yMax + margin;

            // C2: Icon + stats
            Rect iconRect = new Rect(scrollViewRect.x, curY, detailIconSize, detailIconSize);
            Widgets.DrawMenuSection(iconRect);
            Widgets.DrawLightHighlight(iconRect);
            Widgets.ButtonImage(iconRect, selectedBuilding.Icon);

            float statsX = iconRect.xMax + margin;
            float statsW = w - detailIconSize - margin;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;

            Rect costRect = new Rect(statsX, curY, statsW, 22f);
            UIUtil.ClampedLabel(costRect, "FCCost".Translate() + ": " + (settlement.BuildingsComp?.GetBuildingCost(selectedBuilding) ?? (int)selectedBuilding.cost));

            int buildTime = settlement.BuildingsComp?.GetBuildingConstructionTime(selectedBuilding) ?? 0;
            Rect timeRect = new Rect(statsX, costRect.yMax + smallMargin, statsW, 22f);
            UIUtil.ClampedLabel(timeRect, "FCBuildTime".Translate(buildTime.ToTimeString()));

            float statsBottom = timeRect.yMax;

            int upkeep = settlement.BuildingsComp?.GetBuildingUpkeep(selectedBuilding) ?? 0;
            if (upkeep > 0)
            {
                Rect upkeepRect = new Rect(statsX, timeRect.yMax + smallMargin, statsW, 22f);
                UIUtil.ClampedLabel(upkeepRect, "FCBuildingUpkeep".Translate(upkeep.ToString()));
                statsBottom = upkeepRect.yMax;
            }
            else if (upkeep < 0)
            {
                Rect upkeepRect = new Rect(statsX, timeRect.yMax + smallMargin, statsW, 22f);
                UIUtil.ClampedLabel(upkeepRect, "FCBuildingIncome".Translate(Math.Abs(upkeep).ToString()));
                statsBottom = upkeepRect.yMax;
            }

            curY = Math.Max(iconRect.yMax, statsBottom) + margin;

            // C3: Description (def text only)
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            float descHeight = Text.CalcHeight(selectedBuilding.FormattedDesc, w);
            Rect descRect = new Rect(scrollViewRect.x, curY, w, descHeight);
            Widgets.Label(descRect, selectedBuilding.FormattedDesc);
            curY = descRect.yMax + margin;

            // C3.25: Required buildings prerequisite status
            if (selectedBuilding.requiredBuildings.Count > 0)
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                foreach (BuildingFCDef req in selectedBuilding.requiredBuildings)
                {
                    bool has = settlement.BuildingsComp?.HasBuildingOrUpgrade(req) == true;
                    bool isSlotBuilding = req == buildingDef;
                    bool satisfied = has && !isSlotBuilding;
                    Rect statusRect = new Rect(scrollViewRect.x + margin, curY, w - margin * 2, 18f);
                    string checkmark = satisfied ? "✓ " : "✗ ";
                    UIUtil.DrawColoredLabel(statusRect,
                        checkmark + "Empire_BuildingWindow_Requires".Translate(req.LabelCap),
                        satisfied ? Color.green : Color.red);
                    curY += 18f;
                }
                curY += smallMargin;
            }

            // C3.3: Tech lock status
            if (lockedBuildingSet.Contains(selectedBuilding))
            {
                float lockIconSize = 18f;
                Rect lockIconRect = new Rect(scrollViewRect.x + margin, curY + 2f, lockIconSize, lockIconSize);
                GUI.DrawTexture(lockIconRect, TexLoad.buildingLocked);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                string researchName = GetResearchRequirementForTechLevel(selectedBuilding.techLevel);
                Rect techTextRect = new Rect(lockIconRect.xMax + smallMargin, curY, w - lockIconSize - margin * 2 - smallMargin, 22f);
                UIUtil.DrawColoredLabel(techTextRect,
                    "Empire_BuildingWindow_TechLocked".Translate(
                        selectedBuilding.techLevel.ToStringHuman(), researchName),
                    new Color(1f, 0.7f, 0.2f, 1f));
                curY += 22f + smallMargin;
            }

            // Centered width for Modifiers + Settlement Impact
            float impactWidth = w * 0.8f;
            float impactMargin = w - impactWidth;

            // C3.5: Modifiers
            curY = DrawModifiers(scrollViewRect.x + (impactMargin / 2f), curY, impactWidth);

            // C3.75: Extension sections (IBuildingDetailSection from modExtensions)
            curY = DrawExtensionSections(scrollViewRect.x + (impactMargin / 2f), curY, impactWidth);

            // C4: Settlement Impact
            curY = DrawSettlementImpact(scrollViewRect.x + (impactMargin / 2f), curY, impactWidth);

            // C5: Upgrades
            curY = DrawUpgrades(scrollViewRect.x, curY, w);

            // C6: Required By
            curY = DrawRequiredBy(scrollViewRect.x, curY, w);

            ScrollUtil.EndScrollView();

            // C7: Build/Destroy button
            DrawBuildButton(buttonArea);
        }

        private float CalculateDetailContentHeight(float width)
        {
            if (selectedBuilding == null) return 0;

            float h = 0;
            // Name
            h += 30f + margin;
            // Icon + stats
            h += detailIconSize + margin;
            // Description
            GameFont tmp = Text.Font;
            Text.Font = GameFont.Small;
            h += Text.CalcHeight(selectedBuilding.FormattedDesc, width) + margin;
            Text.Font = tmp;
            // Required buildings prereqs
            if (selectedBuilding.requiredBuildings.Count > 0)
                h += selectedBuilding.requiredBuildings.Count * 18f + smallMargin;
            // Tech lock status
            if (lockedBuildingSet.Contains(selectedBuilding))
                h += 22f + smallMargin;
            // Modifiers
            h += CalculateModifiersHeight(width * 0.8f);
            // Extension sections
            h += CalculateExtensionSectionsHeight(width * 0.8f);
            // Settlement impact
            h += CalculateImpactHeight();
            // Upgrades
            h += CalculateUpgradesHeight(width);
            // Required By
            h += CalculateRequiredByHeight(width);

            return h;
        }

        #endregion

        #region Modifiers

        private float DrawModifiers(float x, float curY, float width)
        {
            TaggedString modifiers = selectedBuilding.AttributeDesc;
            if (modifiers.RawText.NullOrEmpty()) return curY;

            curY += smallMargin;
            Text.Font = GameFont.Small;
            float contentWidth = width - (smallMargin * 2);
            float textHeight = Text.CalcHeight(modifiers, contentWidth);
            float blockHeight = 22f + smallMargin + textHeight;

            // Block background
            Rect blockRect = new Rect(x, curY, width, blockHeight);
            Widgets.DrawHighlight(blockRect);

            // Header (double highlight)
            Rect headerRect = new Rect(x, curY, width, 22f);
            Widgets.DrawHighlight(headerRect);
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.DrawColoredLabel(
                new Rect(x + smallMargin, curY, contentWidth, 22f),
                "Empire_BuildingWindow_Modifiers".Translate(),
                new Color(1f, 1f, 1f, 0.7f));
            curY = headerRect.yMax + smallMargin;

            // Content
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.Label(new Rect(x + smallMargin, curY, contentWidth, textHeight), modifiers);
            curY = blockRect.yMax + margin;

            return curY;
        }

        private float CalculateModifiersHeight(float width)
        {
            TaggedString modifiers = selectedBuilding.AttributeDesc;
            if (modifiers.RawText.NullOrEmpty()) return 0;
            GameFont tmp = Text.Font;
            Text.Font = GameFont.Small;
            float h = smallMargin + 22f + smallMargin + Text.CalcHeight(modifiers, width - (smallMargin * 2)) + margin;
            Text.Font = tmp;
            return h;
        }

        #endregion

        #region Extension Sections

        private float DrawExtensionSections(float x, float curY, float width)
        {
            if (selectedBuilding.modExtensions == null) return curY;
            foreach (IBuildingDetailSection section in selectedBuilding.modExtensions.OfType<IBuildingDetailSection>())
            {
                float contentWidth = width - (smallMargin * 2);
                float contentHeight = section.GetSectionHeight(selectedBuilding, contentWidth);
                if (contentHeight <= 0) continue;

                curY += smallMargin;
                float blockHeight = 22f + smallMargin + contentHeight;

                Rect blockRect = new Rect(x, curY, width, blockHeight);
                Widgets.DrawHighlight(blockRect);

                Rect headerRect = new Rect(x, curY, width, 22f);
                Widgets.DrawHighlight(headerRect);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.DrawColoredLabel(
                    new Rect(x + smallMargin, curY, contentWidth, 22f),
                    section.SectionLabel,
                    new Color(1f, 1f, 1f, 0.7f));

                Rect contentRect = new Rect(x + smallMargin, headerRect.yMax + smallMargin, contentWidth, contentHeight);
                section.DrawSection(selectedBuilding, contentRect);

                curY = blockRect.yMax + margin;
            }
            return curY;
        }

        private float CalculateExtensionSectionsHeight(float width)
        {
            if (selectedBuilding.modExtensions == null) return 0;
            float h = 0;
            float contentWidth = width - (smallMargin * 2);
            foreach (IBuildingDetailSection section in selectedBuilding.modExtensions.OfType<IBuildingDetailSection>())
            {
                float contentHeight = section.GetSectionHeight(selectedBuilding, contentWidth);
                if (contentHeight <= 0) continue;
                h += smallMargin + 22f + smallMargin + contentHeight + margin;
            }
            return h;
        }

        #endregion

        #region Settlement Impact

        private float DrawSettlementImpact(float x, float curY, float width)
        {
            if (selectedBuilding.statModifiers == null || !selectedBuilding.statModifiers.Any(m => m.stat != null && m.stat.linkedResource != null)) return curY;

            // Gather all affected resources from both old and new building
            HashSet<ResourceTypeDef> affectedResources = new HashSet<ResourceTypeDef>();
            foreach (FCStatModifier mod in selectedBuilding.statModifiers)
                if (mod.stat != null && mod.stat.linkedResource != null)
                    affectedResources.Add(mod.stat.linkedResource);

            bool isReplacing = buildingDef != BuildingFCDefOf.Empty && buildingDef != BuildingFCDefOf.Construction;
            if (isReplacing && buildingDef.statModifiers != null)
            {
                foreach (FCStatModifier mod in buildingDef.statModifiers)
                    if (mod.stat != null && mod.stat.linkedResource != null)
                        affectedResources.Add(mod.stat.linkedResource);
            }

            if (affectedResources.Count == 0) return curY;

            curY += smallMargin;
            Text.Font = GameFont.Small;
            float contentHeight = affectedResources.Count * (22f + smallMargin);
            float blockHeight = 22f + smallMargin + contentHeight;

            // Block background
            Rect blockRect = new Rect(x, curY, width, blockHeight);
            Widgets.DrawHighlight(blockRect);

            // Header (double highlight)
            Rect headerRect = new Rect(x, curY, width, 22f);
            Widgets.DrawHighlight(headerRect);
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.DrawColoredLabel(
                new Rect(x + smallMargin, curY, width - smallMargin * 2, 22f),
                "Empire_BuildingWindow_SettlementImpact".Translate(),
                new Color(1f, 1f, 1f, 0.7f));
            curY = headerRect.yMax + smallMargin;

            // Draw each affected resource
            foreach (ResourceTypeDef resDef in affectedResources)
            {
                ResourceFC resource = settlement.GetResource(resDef);
                if (resource == null) continue;

                double currentProd = resource.production;
                double projectedBase = resource.productionBase;
                double projectedMult = resource.productionMult;

                // Subtract old building contributions
                if (isReplacing && buildingDef.statModifiers != null)
                {
                    foreach (FCStatModifier mod in buildingDef.statModifiers)
                    {
                        if (mod.stat == null || mod.stat.linkedResource != resDef) continue;
                        if (mod.stat.aggregation == FCStatAggregation.Additive)
                            projectedBase -= mod.value;
                        else if (mod.value != 0)
                            projectedMult /= mod.value;
                    }
                }

                // Add new building contributions
                foreach (FCStatModifier mod in selectedBuilding.statModifiers)
                {
                    if (mod.stat == null || mod.stat.linkedResource != resDef) continue;
                    if (mod.stat.aggregation == FCStatAggregation.Additive)
                        projectedBase += mod.value;
                    else
                        projectedMult *= mod.value;
                }

                double projectedProd = projectedBase * projectedMult;
                double delta = projectedProd - currentProd;

                // Draw row: [icon] Label: current -> projected (delta)
                Rect rowRect = new Rect(x, curY, width, 22f);

                // Resource color accent
                Widgets.DrawBoxSolid(new Rect(x, curY, 3f, 22f), resDef.color);
                float contentX = x + 3f + smallMargin;

                // Resource icon
                Rect iconRect = new Rect(contentX, curY, 20f, 20f);
                GUI.DrawTexture(iconRect, resource.getIcon);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;

                string currentStr = currentProd.ToString("F1");
                string projectedStr = projectedProd.ToString("F1");
                string deltaStr = (delta >= 0 ? "+" : "") + delta.ToString("F1");
                Color deltaColor = delta >= 0 ? Color.green : Color.red;

                Rect textRect = new Rect(iconRect.xMax + smallMargin, curY, width - (contentX - x) - 20f - smallMargin, 22f);
                string label = resource.label + ": " + currentStr + " → " + projectedStr + " (";
                UIUtil.ClampedLabel(textRect, label);

                // Draw delta with color
                float labelWidth = Text.CalcSize(label).x;
                Rect deltaRect = new Rect(textRect.x + labelWidth, curY, Text.CalcSize(deltaStr).x, 22f);
                UIUtil.DrawColoredLabel(deltaRect, deltaStr, deltaColor);

                Rect closeParenRect = new Rect(deltaRect.xMax, curY, 20f, 22f);
                UIUtil.ClampedLabel(closeParenRect, ")");

                curY = rowRect.yMax + smallMargin;
            }

            return blockRect.yMax + margin;
        }

        private float CalculateImpactHeight()
        {
            if (selectedBuilding?.statModifiers == null || !selectedBuilding.statModifiers.Any(m => m.stat != null && m.stat.linkedResource != null)) return 0;

            HashSet<ResourceTypeDef> affected = new HashSet<ResourceTypeDef>();
            foreach (FCStatModifier mod in selectedBuilding.statModifiers)
                if (mod.stat != null && mod.stat.linkedResource != null)
                    affected.Add(mod.stat.linkedResource);

            bool isReplacing = buildingDef != BuildingFCDefOf.Empty && buildingDef != BuildingFCDefOf.Construction;
            if (isReplacing && buildingDef.statModifiers != null)
                foreach (FCStatModifier mod in buildingDef.statModifiers)
                    if (mod.stat != null && mod.stat.linkedResource != null)
                        affected.Add(mod.stat.linkedResource);

            if (affected.Count == 0) return 0;

            // header line + header text + per-resource rows + bottom margin
            return smallMargin + 22f + smallMargin + affected.Count * (22f + smallMargin) + margin;
        }

        #endregion

        #region Upgrades

        private float DrawUpgrades(float x, float curY, float width)
        {
            if (!FactionCache.UpgradeTrees.TryGetValue(selectedBuilding, out List<BuildingUpgradeEntry> tree) || tree.Count == 0)
                return curY;

            List<BuildingUpgradeEntry> filtered = tree.Where(
                e => e.def.CanBeBuiltForSettlementType(settlement.settlementDef)).ToList();
            if (filtered.Count == 0) return curY;

            curY += smallMargin;

            // Block background
            float blockHeight = CalculateUpgradesHeight(width) - smallMargin - margin;
            Rect blockRect = new Rect(x, curY, width, blockHeight);
            Widgets.DrawHighlight(blockRect);

            // Header (double highlight via DrawCollapsibleHeader)
            DrawCollapsibleHeader(x, curY, width,
                "Empire_BuildingWindow_Upgrades".Translate(), ref upgradesExpanded);
            curY += collapsibleHeaderHeight + smallMargin;

            if (!upgradesExpanded) return blockRect.yMax + margin;

            for (int i = 0; i < filtered.Count; i++)
            {
                BuildingUpgradeEntry entry = filtered[i];
                float indent = entry.depth * indentWidth;
                float cardWidth = width - indent;

                // "Requires: parent" label for non-direct upgrades
                if (entry.depth > 0)
                {
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Rect reqRect = new Rect(x + indent + margin, curY, cardWidth, 18f);
                    UIUtil.DrawColoredLabel(reqRect,
                        "Empire_BuildingWindow_Requires".Translate(entry.parent.LabelCap),
                        new Color(1f, 1f, 1f, 0.5f));
                    curY += 18f;
                }

                float cardHeight = CalculateBuildingCardHeight(entry.def, cardWidth);
                Rect cardRect = new Rect(x + indent, curY, cardWidth, cardHeight);

                bool isSelected = selectedBuilding == entry.def;
                Widgets.DrawHighlight(cardRect);
                if (isSelected)
                    Widgets.DrawBoxSolid(cardRect, selectionColor);
                else if (i % 2 == 0)
                    Widgets.DrawHighlight(cardRect);

                DrawBuildingCard(cardRect, entry.def);
                curY = cardRect.yMax + smallMargin;
            }

            return blockRect.yMax + margin;
        }

        private float CalculateUpgradesHeight(float width)
        {
            if (selectedBuilding == null) return 0;
            if (!FactionCache.UpgradeTrees.TryGetValue(selectedBuilding, out List<BuildingUpgradeEntry> tree) || tree.Count == 0)
                return 0;

            List<BuildingUpgradeEntry> filtered = tree.Where(
                e => e.def.CanBeBuiltForSettlementType(settlement.settlementDef)).ToList();
            if (filtered.Count == 0) return 0;

            float h = smallMargin + collapsibleHeaderHeight + smallMargin;

            if (!upgradesExpanded) return h + margin;

            for (int i = 0; i < filtered.Count; i++)
            {
                BuildingUpgradeEntry entry = filtered[i];
                float indent = entry.depth * indentWidth;
                float cardWidth = width - indent;
                if (entry.depth > 0) h += 18f;
                h += CalculateBuildingCardHeight(entry.def, cardWidth) + smallMargin;
            }

            h += margin;
            return h;
        }

        #endregion

        #region Required By

        private float DrawRequiredBy(float x, float curY, float width)
        {
            if (!FactionCache.RequiredByBuildingMap.TryGetValue(selectedBuilding, out List<BuildingFCDef> requiredBy) || requiredBy.Count == 0)
                return curY;

            curY += smallMargin;

            // Block background
            float blockHeight = CalculateRequiredByHeight(width) - smallMargin - margin;
            Rect blockRect = new Rect(x, curY, width, blockHeight);
            Widgets.DrawHighlight(blockRect);

            // Header (double highlight via DrawCollapsibleHeader)
            DrawCollapsibleHeader(x, curY, width,
                "Empire_BuildingWindow_RequiredBy".Translate(), ref requiredByExpanded);
            curY += collapsibleHeaderHeight + smallMargin;

            if (!requiredByExpanded) return blockRect.yMax + margin;

            for (int i = 0; i < requiredBy.Count; i++)
            {
                BuildingFCDef building = requiredBy[i];
                bool canBuild = building.techLevel <= factionfc.techLevel
                    && building.CanBeBuiltForSettlementType(settlement.settlementDef)
                    && (building.applicableBiomes.Count == 0
                        || building.applicableBiomes.Contains(settlement.biome));

                float cardHeight = CalculateBuildingCardHeight(building, width);
                Rect cardRect = new Rect(x, curY, width, cardHeight);

                if (!canBuild)
                    GUI.color = new Color(1f, 1f, 1f, 0.4f);

                Widgets.DrawHighlight(cardRect);
                if (i % 2 == 0) Widgets.DrawHighlight(cardRect);

                DrawBuildingCard(cardRect, building);
                GUI.color = Color.white;
                curY = cardRect.yMax;

                // Prerequisite status (checkmark/cross)
                if (building.requiredBuildings.Count > 0)
                {
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    foreach (BuildingFCDef req in building.requiredBuildings)
                    {
                        bool has = settlement.BuildingsComp?.HasBuildingOrUpgrade(req) == true;
                        Rect statusRect = new Rect(x + margin * 2, curY, width - margin * 4, 18f);
                        string checkmark = has ? "✓ " : "✗ ";
                        UIUtil.DrawColoredLabel(statusRect, checkmark + req.LabelCap,
                            has ? Color.green : Color.red);
                        curY += 18f;
                    }
                }

                curY += smallMargin;
            }

            return blockRect.yMax + margin;
        }

        private float CalculateRequiredByHeight(float width)
        {
            if (selectedBuilding == null) return 0;
            if (!FactionCache.RequiredByBuildingMap.TryGetValue(selectedBuilding, out List<BuildingFCDef> requiredBy) || requiredBy.Count == 0)
                return 0;

            float h = smallMargin + collapsibleHeaderHeight + smallMargin;

            if (!requiredByExpanded) return h + margin;

            for (int i = 0; i < requiredBy.Count; i++)
            {
                h += CalculateBuildingCardHeight(requiredBy[i], width);
                if (requiredBy[i].requiredBuildings.Count > 0)
                    h += requiredBy[i].requiredBuildings.Count * 18f;
                h += smallMargin;
            }

            h += margin;
            return h;
        }

        #endregion

        #region Build / Destroy Actions

        private void DrawBuildButton(Rect area)
        {
            Rect buttonRect = new Rect(
                area.x + (area.width - actionButtonWidth) / 2f,
                area.y + margin,
                actionButtonWidth,
                actionButtonHeight
            );

            bool isSameBuilding = selectedBuilding == buildingDef;
            bool isLocked = lockedBuildingSet.Contains(selectedBuilding);

            if (isSameBuilding)
            {
                // Already built in this slot; removal is handled by the Demolish button above.
                GUI.color = new Color(1f, 1f, 1f, 0.4f);
                UIUtil.ClampedButtonText(buttonRect, "Empire_BuildingWindow_CurrentBuilding".Translate());
                GUI.color = Color.white;
            }
            else if (isLocked)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.4f);
                UIUtil.ClampedButtonText(buttonRect, "Empire_BuildingWindow_LockedButton".Translate());
                GUI.color = Color.white;
                string researchName = GetResearchRequirementForTechLevel(selectedBuilding.techLevel);
                TooltipHandler.TipRegion(buttonRect,
                    "Empire_BuildingWindow_TechLockedTooltip".Translate(researchName));
            }
            else
            {
                bool canBuild = !HasUnmetRequirements(selectedBuilding)
                    && !settlement.BuildingsComp.HasBuildingOrUpgrade(selectedBuilding);
                if (!canBuild)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.4f);
                    UIUtil.ClampedButtonText(buttonRect, "FCBuild".Translate());
                    GUI.color = Color.white;
                }
                else if (UIUtil.ClampedButtonText(buttonRect, "FCBuild".Translate()))
                {
                    ExecuteBuild();
                }
            }
        }

        private void ExecuteBuild()
        {
            if (selectedBuilding == null) return;
            if (settlement.BuildingsComp?.ValidConstructBuilding(selectedBuilding, buildingSlot) != true) return;

            // Pay first (atomic). ValidConstructBuilding already validated affordability and showed
            // any rejection message; this is the actual deduction and aborts cleanly before the
            // construction event is queued if the balance somehow shifted.
            if (!PaymentUtil.TryPaySilver(settlement.BuildingsComp.GetBuildingCost(selectedBuilding), PaymentUtil.Reason_BuildingConstruction, settlement))
                return;

            FCEvent tmpEvt = new FCEvent(true)
            {
                def = FCEventDefOf.constructBuilding,
                tickStarted = Find.TickManager.TicksGame,
                source = settlement.Tile,
                building = selectedBuilding,
                buildingSlot = buildingSlot
            };

            int triggerTime = settlement.BuildingsComp.GetBuildingConstructionTime(selectedBuilding);

            tmpEvt.timeTillTrigger = Find.TickManager.TicksGame + triggerTime;
            tmpEvt.customDescription = "FCBuildingEventDesc".Translate(
                selectedBuilding.LabelCap,
                settlement.Name,
                (tmpEvt.timeTillTrigger - Find.TickManager.TicksGame).ToTimeString());
            tmpEvt.hasCustomDescription = true;
            FindFC.EventManager.AddEvent(tmpEvt);

            Messages.Message(selectedBuilding.label + " " + "FCWillBeConstructedIn".Translate() + " " + (tmpEvt.timeTillTrigger - Find.TickManager.TicksGame).ToTimeString(), MessageTypeDefOf.PositiveEvent);
            settlement.BuildingsComp.StartConstruction(selectedBuilding, buildingSlot, tmpEvt.timeTillTrigger);
            Find.WindowStack.TryRemove(this);
        }

        #endregion

        #region Constructor

        public FCBuildingWindow(WorldSettlementFC settlement, int buildingSlot)
        {
            factionfc = FindFC.FactionComp;
            this.settlement = settlement;
            this.buildingSlot = buildingSlot;
            buildingDef = settlement.BuildingsComp?.GetBuildingInSlot(buildingSlot);

            buildingList = new List<BuildingFCDef>();
            filteredBuildingList = new List<BuildingFCDef>();
            lockedBuildingList = new List<BuildingFCDef>();
            lockedBuildingSet = new HashSet<BuildingFCDef>();

            TechLevel maxReachableTech = FCSettings.medievalTechOnly
                ? TechLevel.Medieval
                : TechLevel.Archotech;

            foreach (BuildingFCDef building in DefDatabase<BuildingFCDef>.AllDefsListForReading)
            {
                if (building.defName != "Empty" && building.defName != "Construction" && building.baseBuilding)
                {
                    if (building.applicableBiomes.Count == 0
                        || building.applicableBiomes.Contains(settlement.biome))
                    {
                        if (building.CanBeBuiltForSettlementType(settlement.settlementDef))
                        {
                            if (building.techLevel <= factionfc.techLevel)
                            {
                                buildingList.Add(building);
                            }
                            else if (building.techLevel <= maxReachableTech)
                            {
                                lockedBuildingList.Add(building);
                                lockedBuildingSet.Add(building);
                            }
                        }
                    }
                }
            }

            buildingList.Sort(CompareUtil.CompareBuildingDef);
            lockedBuildingList.Sort(CompareUtil.CompareBuildingDef);
            filteredBuildingList.AddRange(buildingList);

            // Populate slot upgrade list
            if (buildingDef != null && buildingDef != BuildingFCDefOf.Empty
                && buildingDef != BuildingFCDefOf.Construction)
            {
                if (FactionCache.UpgradeTrees.TryGetValue(buildingDef, out List<BuildingUpgradeEntry> tree))
                {
                    foreach (var entry in tree)
                    {
                        if (entry.depth == 0 && entry.def.CanBeBuiltForSettlementType(settlement.settlementDef))
                            slotUpgradeList.Add(entry);
                    }
                }
            }

            forcePause = false;
            draggable = true;
            doCloseX = true;
            preventCameraMotion = false;
            resizeable = true;

            if (buildingDef == BuildingFCDefOf.Construction && settlement.BuildingsComp != null)
            {
                buildingDesc = "Empire_BuildingWindow_ConstructionDesc".Translate(settlement.BuildingsComp.Buildings[buildingSlot].underConstructionDef.label);
            }
            else
            {
                buildingDesc = settlement.BuildingsComp?.GetBuildingDesc(buildingDef) ?? TaggedString.Empty;
            }

            filterSize = settlement.BuildingsComp?.GetFilterSize() ?? 0;
            filterRows = (int)Math.Ceiling((double)filterSize / (double)filterButtonsPerRow);
            fullScrollHeight = filteredBuildingList.Count * 90f;
        }

        #endregion
    }
}
