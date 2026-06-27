using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class MainTabWindow_Colony : MainTabWindow
    {
        private const float margin = 5f;
        private const float smallMargin = 3f;
        private const float bigMargin = 8f;

        // ===== TAB STATE =====
        private string curTab = "FCOverview".Translate();
        private List<TabRecord> tabs = new List<TabRecord>();

        private List<string> overviewTabs = new List<string>
        {
            "FCOverview".Translate(),
            "FCBills".Translate(),
            "FCEvents".Translate(),
            "FCMilitary".Translate(),
            "FCEdicts".Translate(),
            "FCPrisoners".Translate(),
            "FCSituations".Translate()
        };
        private Dictionary<string, Action<Rect>> overviewFuncs = new Dictionary<string, Action<Rect>>();

        // ===== WINDOW SIZE =====
        public override Vector2 InitialSize => new Vector2(1060f, 640f);

        // ===== DATA =====
        public bool selectingColonyFC;
        public FactionFC faction;

        // ===== SCROLL POSITIONS =====
        private Vector2 settlementScroll;
        private Vector2 billsScroll;
        private Vector2 eventsScroll;
        private Vector2 productionScroll;

        // ===== EVENT FILTER STATE =====
        private static readonly HashSet<FCEventCategoryDef> hiddenEventCategories = new HashSet<FCEventCategoryDef>();

        // ===== SETTLEMENT SORT =====
        private int currentSettlementSortIndex = 0;

        // ===== MILITARY STATE =====
        private Vector2 militaryScroll;
        private MilitaryFC militaryFC;

        // ===== PRISONERS STATE =====
        private Vector2 prisonersScroll;
        private Vector2 situationsScroll;
        private HashSet<int> collapsedPrisonerSections = new HashSet<int>();

        // ===== SORTED LIST CACHES =====
        private List<BillFC> cachedSortedBills;
        private int cachedBillsCount = -1;
        private List<FCEvent> cachedSortedEvents;
        private int cachedEventsVersion = -1;
        private int cachedHiddenCategoriesCount = -1;
        private static List<FCEventCategoryDef> cachedSortedCategories;

        // ===== LIFECYCLE =====

        public override void PreOpen()
        {
            base.PreOpen();
            faction = FindFC.FactionComp;
            if (faction == null)
            {
                LogUtil.Error("WorldComp FactionFC is null - Something is wrong!");
                return;
            }

            // Averages and profit are lazy-cached — no eager update needed
            militaryFC = faction.military;

            // Build tab list
            // Main overview tab
            tabs.Clear();
            overviewFuncs.Clear();
            tabs.Add(new TabRecord(overviewTabs[0], delegate
            {
                curTab = overviewTabs[0];
            }, () => curTab == overviewTabs[0]));
            overviewFuncs.Add(overviewTabs[0], DrawOverviewTab);
            // Bills tab
            tabs.Add(new TabRecord(overviewTabs[1], delegate
            {
                curTab = overviewTabs[1];
                billsScroll = Vector2.zero;
            }, () => curTab == overviewTabs[1]));
            overviewFuncs.Add(overviewTabs[1], DrawBillsTab);
            // Events tab
            tabs.Add(new TabRecord(overviewTabs[2], delegate
            {
                curTab = overviewTabs[2];
                eventsScroll = Vector2.zero;
            }, () => curTab == overviewTabs[2]));
            overviewFuncs.Add(overviewTabs[2], DrawEventsTab);
            // Military tab
            tabs.Add(new TabRecord(overviewTabs[3], delegate
            {
                curTab = overviewTabs[3];
                militaryScroll = Vector2.zero;
            }, () => curTab == overviewTabs[3]));
            overviewFuncs.Add(overviewTabs[3], DrawMilitaryTab);
            // Edicts tab
            tabs.Add(new TabRecord(overviewTabs[4], delegate
            {
                curTab = overviewTabs[4];
                EdictTabDrawer.OnTabSwitch();
            }, () => curTab == overviewTabs[4]));
            overviewFuncs.Add(overviewTabs[4], DrawEdictsTab);
            // Prisoners tab
            tabs.Add(new TabRecord(overviewTabs[5], delegate
            {
                curTab = overviewTabs[5];
                PrisonerUtil.CullNullPrisoners(faction);
                prisonersScroll = Vector2.zero;
            }, () => curTab == overviewTabs[5]));
            overviewFuncs.Add(overviewTabs[5], DrawPrisonersTab);
            // Situations tab — only registered when any situation defs exist in the database.
            if (DefDatabase<FCSituationDef>.AllDefsListForReading.Count > 0)
            {
                tabs.Add(new TabRecord(overviewTabs[6], delegate
                {
                    curTab = overviewTabs[6];
                    situationsScroll = Vector2.zero;
                }, () => curTab == overviewTabs[6]));
                overviewFuncs.Add(overviewTabs[6], DrawSituationsTab);
            }
        }

        public override void PostClose()
        {
            base.PostClose();
            selectingColonyFC = false;
            militaryFC?.CheckMilitaryUtilForErrors();
        }

        // ===== MAIN DRAW =====

        public override void DoWindowContents(Rect inRect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            Faction gfaction = FindFC.EmpireFaction;
            if (gfaction == null)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.Font = GameFont.Medium;
                Rect btn = new Rect(inRect.x + inRect.width / 2f - 150f, inRect.y + inRect.height / 2f - 20f, 300f, 40f);
                if (Widgets.ButtonText(btn, "FCCreateNewFaction".Translate()))
                {
                    ColonyUtil.CreatePlayerColonyFaction();
                    faction = FindFC.FactionComp;
                    if (faction != null)
                    {
                        faction.factionCreated = true;
                        Find.WindowStack.Add(new FactionCustomizeWindowFc(faction));
                        if (Find.CurrentMap.Parent != null)
                        {
                            WorldSettlementFC wsfc = Find.WorldObjects.WorldObjectAt<WorldSettlementFC>(Find.CurrentMap.Parent.Tile);
                            if (wsfc is object)
                            {
                                Messages.Message(
                                    "FCSetAsFactionCapital".Translate(wsfc.Name),
                                    MessageTypeDefOf.NeutralEvent);
                            }
                        }
                    }
                    else
                    {
                        LogUtil.Error("FactionFC world component is still null after creating new faction!");
                    }
                }
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;
                return;
            }

            // Calculate minimum tab width from the longest label
            Text.Font = GameFont.Small;
            float maxLabelWidth = 0f;
            foreach (TabRecord tab in tabs)
            {
                float w = Text.CalcSize(tab.label).x;
                if (w > maxLabelWidth) maxLabelWidth = w;
            }
            float minTabWidth = maxLabelWidth + 16f;

            // Content area sits below the tab strip (dynamic height for overflow rows)
            float tabHeight = TabDrawer.GetOverflowTabHeight(inRect, tabs, minTabWidth, 200f);
            Rect contentRect = new Rect(inRect.x, inRect.y + tabHeight, inRect.width, inRect.height - tabHeight);
            Widgets.DrawMenuSection(contentRect);
            TabDrawer.DrawTabsOverflow(inRect, tabs, minTabWidth, 200f);

            try
            {
                overviewFuncs[curTab](contentRect);
            }
            catch (Exception e)
            {
                LogUtil.Error($"Error drawing tab '{curTab}': {e}");
                curTab = overviewTabs[0];
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        // ===== OVERVIEW TAB =====

        private void DrawOverviewTab(Rect rect)
        {
            const float leftWidth = 175f;
            const float rightWidth = 200f;
            const float panelGap = 10f;
            const float headerHeight = 63f;

            float bodyY = rect.y + margin + headerHeight + panelGap;
            float bodyH = rect.height - panelGap - headerHeight - margin;

            Rect headerPanel = new Rect(rect.x + margin, rect.y + margin, rect.width - (margin * 2), headerHeight);
            Rect leftPanel = new Rect(rect.x + margin, bodyY, leftWidth, bodyH);
            Rect rightPanel = new Rect(rect.xMax - margin - rightWidth, bodyY, rightWidth, bodyH);
            Rect centerPanel = new Rect(leftPanel.xMax + panelGap, bodyY, rightPanel.x - leftPanel.xMax - panelGap * 2, bodyH);

            DrawOverviewHeaderPanel(headerPanel);
            DrawOverviewLeftPanel(leftPanel);
            DrawOverviewCenterPanel(centerPanel);
            DrawOverviewRightPanel(rightPanel);

            Widgets.DrawLineVertical(leftPanel.xMax + (panelGap / 2), leftPanel.y, leftPanel.height - margin);
            Widgets.DrawLineVertical(centerPanel.xMax + (panelGap / 2), centerPanel.y, centerPanel.height - margin);
        }
        private void DrawOverviewHeaderPanel(Rect panel)
        {
            float iconSz = 55f;
            Rect iconRect = new Rect(panel.x, panel.y + (panel.height - iconSz) / 2f, iconSz, iconSz);
            Widgets.ButtonImage(iconRect, faction.factionIcon);

            float customizeBtnSize = 20f;
            Rect codexBtn = new Rect(panel.xMax - customizeBtnSize * 2 - margin, panel.y + margin, customizeBtnSize, customizeBtnSize);
            Rect customizeBtn = new Rect(panel.xMax - customizeBtnSize, panel.y + margin, customizeBtnSize, customizeBtnSize);
            Rect labelBox = new Rect(iconRect.xMax + margin, panel.y, panel.width - iconSz - margin, 30f);
            Rect labelTextBox = new Rect(labelBox.x + margin, labelBox.y, labelBox.width - (margin * 2), labelBox.height);
            Rect titleBox = new Rect(labelBox.x, labelBox.yMax + margin, labelBox.width / 2f, 22f);
            Rect foundingBox = new Rect(titleBox.xMax, titleBox.y, titleBox.width, titleBox.height);

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.DrawHighlight(labelBox);
            Widgets.Label(labelTextBox, faction.name ?? "");

            Text.Font = GameFont.Small;
            Widgets.Label(titleBox, faction.title ?? "");

            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(foundingBox, "FCFoundedOn".Translate(faction.GetFoundingDate()));

            Widgets.DrawLineHorizontal(labelBox.x, titleBox.yMax + margin, panel.xMax - labelBox.x - margin);

            CodexTooltips.DrawCodexButton(codexBtn);

            if (Widgets.ButtonImage(customizeBtn, TexLoad.iconCustomize))
            {
                if (FindFC.EmpireFaction != null)
                    Find.WindowStack.Add(new FactionCustomizeWindowFc(faction));
            }
        }
        private void DrawOverviewLeftPanel(Rect panel)
        {
            float y = panel.y;

            // --- XP Bar ---
            float xpH = 18f;
            Rect xpBar = new Rect(panel.x, y, panel.width, xpH);
            UIUtil.DrawProgressBarColors(xpBar, faction.factionXPCurrent / faction.factionXPGoal, Color.black, Color.green);
            Widgets.DrawShadowAround(xpBar);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(xpBar, Math.Round(faction.factionXPCurrent) + "/" + faction.factionXPGoal);
            y += xpH + margin;

            // Faction level
            Text.Font = GameFont.Small;
            Rect levelBox = new Rect(panel.x, y, panel.width, 20f);
            Widgets.DrawHighlight(levelBox);
            Widgets.Label(levelBox, "FCLevel".Translate(faction.factionLevel));
            y += levelBox.height + margin;

            // --- Stats ---
            Text.Anchor = TextAnchor.MiddleLeft;
            string[] statKeys = { "happiness", "loyalty", "unrest", "prosperity" };
            float statH = 32f;

            foreach (string statKey in statKeys)
            {
                float iconSize = statH - (smallMargin * 2);
                Rect statBox = new Rect(panel.x, y, panel.width, statH);
                Rect iconBox = new Rect(statBox.x + smallMargin, y + smallMargin, iconSize, iconSize);
                Rect valueBox = new Rect(statBox.x + iconSize + bigMargin, y, statBox.width - iconSize - bigMargin, statH);
                Widgets.DrawMenuSection(statBox);

                Texture2D icon;
                string value;
                string tooltip;
                float statVal;
                bool inverted;

                switch (statKey)
                {
                    case "happiness":
                        icon = TexLoad.iconHappiness;
                        statVal = (float)faction.averageHappiness;
                        value = Convert.ToInt32(statVal) + "%";
                        tooltip = util.CodexTooltips.GetFactionHappinessTooltip(faction);
                        inverted = false;
                        break;
                    case "loyalty":
                        icon = TexLoad.iconLoyalty;
                        statVal = (float)faction.averageLoyalty;
                        value = Convert.ToInt32(statVal) + "%";
                        tooltip = util.CodexTooltips.GetFactionLoyaltyTooltip(faction);
                        inverted = false;
                        break;
                    case "unrest":
                        icon = TexLoad.iconUnrest;
                        statVal = (float)faction.averageUnrest;
                        value = Convert.ToInt32(statVal) + "%";
                        tooltip = util.CodexTooltips.GetFactionUnrestTooltip(faction);
                        inverted = true;
                        break;
                    default: // prosperity
                        icon = TexLoad.iconProsperity;
                        statVal = (float)faction.averageProsperity;
                        value = Convert.ToInt32(statVal) + "%";
                        tooltip = util.CodexTooltips.GetFactionProsperityTooltip(faction);
                        inverted = false;
                        break;
                }

                Widgets.Label(iconBox, new GUIContent(icon));

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.DrawColoredLabel(valueBox, value, AccentUtil.GetStatColor(statVal, inverted));

                TooltipHandler.TipRegion(statBox, tooltip);

                y += statH + smallMargin;
            }

            y += margin - smallMargin;

            // --- Policies ---
            float policySize = 40f;
            if (FindFC.PolicyManager.policies.Count == FCSettings.maxPolicyCount)
            {
                float leftX = panel.x + (panel.width - (policySize * FindFC.PolicyManager.policies.Count) - (margin * (FindFC.PolicyManager.policies.Count - 1))) / 2f;
                for (int i = 0; i < FindFC.PolicyManager.policies.Count; i++)
                {
                    Rect policyBox = new Rect(leftX + (i * (policySize + margin)), y, policySize, policySize);
                    if (Widgets.ButtonImage(policyBox, FindFC.PolicyManager.policies[i].def.IconLight))
                    {
                        Find.WindowStack.Add(new FactionCustomizePoliciesWindowFC(faction));
                    }
                    TooltipHandler.TipRegion(policyBox, FindFC.PolicyManager.policies[i].def.PolicyText());
                }
            }
            else
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                Rect buttonRect = new Rect(panel.x, y, panel.width, policySize);
                if (Widgets.ButtonText(buttonRect, "FCSelectPolicies".Translate()))
                {
                    Find.WindowStack.Add(new FactionCustomizePoliciesWindowFC(faction));
                }
            }
            y += policySize + margin;

            // --- Traits ---
            Widgets.DrawLineHorizontal(panel.x + margin, y, panel.width - (margin * 2));
            y += margin;
            float traitH = 30f;

            bool hasOpenSlots = false;
            for (int slot = 0; slot < 5; slot++)
            {
                FCPolicy current = FindFC.PolicyManager.factionTraits[slot];
                bool isLocked = faction.factionLevel < (slot + 1);
                bool isOpen = !isLocked && current.def == FCPolicyDefOf.empty;
                if (isOpen) hasOpenSlots = true;

                Rect traitRect = new Rect(panel.x, y, panel.width, traitH);
                Rect labelRect = new Rect(traitRect.x + (bigMargin * 2), y, traitRect.width - (bigMargin * 2), traitRect.height);

                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.DrawHighlight(traitRect);

                if (isLocked)
                {
                    Widgets.Label(labelRect, "FCTraitLockedUntilLevel".Translate(slot + 1).Colorize(Color.gray));
                }
                else if (current.def != FCPolicyDefOf.empty)
                {
                    Widgets.Label(labelRect, current.def.LabelCap);
                    TooltipHandler.TipRegion(traitRect, current.def.PolicyText());
                }
                else
                {
                    Widgets.Label(labelRect, "FCSelectANewTrait".Translate().Colorize(Color.yellow));
                }

                y += traitH + smallMargin;
            }

            if (hasOpenSlots)
            {
                Rect selectButton = new Rect(panel.x, y, panel.width, traitH);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                if (Widgets.ButtonText(selectButton, "FCSelectTraits".Translate()))
                {
                    Find.WindowStack.Add(new FactionCustomizeTraitsWindowFC(faction));
                }
                y += traitH + smallMargin;
            }

            y += margin - smallMargin;
            Widgets.DrawLineHorizontal(panel.x + margin, y, panel.width - (margin * 2));

            y += margin;

            // --- Road Building ---
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect roadBox = new Rect(panel.x, y, panel.width, 26f);
            Rect roadLabel = new Rect(roadBox.x + margin, y, 120f, roadBox.height);
            Rect checkBox = new Rect(roadBox.xMax - 24f, y + 1, 24f, 24f);
            Widgets.DrawMenuSection(roadBox);
            Widgets.Label(roadLabel, "FCBuildRoads".Translate());
            Widgets.DrawHighlight(checkBox);
            Widgets.Checkbox(checkBox.x, checkBox.y, ref faction.roadBuilder.roadBuildingEnabled);
        }

        private void DrawOverviewCenterPanel(Rect panel)
        {
            float x = panel.x;
            float y = panel.y;
            float width = panel.width;

            // --- Action Buttons ---
            Rect actionPanel = new Rect(x, y, width, 30f);
            DrawActionButtons(actionPanel);

            y += actionPanel.height + margin;

            // Seperator
            Widgets.DrawLineHorizontal(x + margin, y, panel.width - (margin * 2));
            y += margin;

            // --- Settlements Table ---
            float tableH = panel.yMax - y - margin;
            if (tableH > 0f)
                DrawSettlementsTable(new Rect(x, y, width, tableH));
        }
        private void DrawActionButtons(Rect panel)
        {
            // Collect action buttons from all active policy/trait extensions
            List<(TaggedString label, Action onClick)> actionButtons = new List<(TaggedString, Action)>();
            FindFC.PolicyManager.ForEachBehavior(b =>
            {
                var buttons = b.GetMainTabActionButtons(faction);
                if (buttons != null)
                    foreach (var btn in buttons)
                        actionButtons.Add(btn);
            });

            int numButtons = 1 + actionButtons.Count;
            // The "Create New Colony" button is more important than all the rest, so we'll make it as wide as two of the other buttons. Keep that in mind for the following math
            float calcButtonWidth = (panel.width - (margin * (numButtons - 1))) / (numButtons + 1);
            float y = panel.y;
            float x = panel.x;
            float height = panel.height;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;

            foreach (var (label, onClick) in actionButtons)
            {
                Rect btnRect = new Rect(x, y, calcButtonWidth, height);
                if (Widgets.ButtonText(btnRect, label))
                    onClick();
                x += btnRect.width + margin;
            }

            Rect newColonyButton = new Rect(x, y, calcButtonWidth * 2, height);
            if (Widgets.ButtonText(newColonyButton, "FCCreateNewColony".Translate()))
            {
                Find.WindowStack.Add(new CreateColonyWindowFc());
                Find.World.renderer.wantedMode = WorldRenderMode.Planet;
                Messages.Message("FCSelectTile".Translate(), MessageTypeDefOf.NegativeEvent);
                Find.WindowStack.TryRemove(this);
            }
        }
        private Vector2 poolScrollbar = new Vector2();
        private void DrawOverviewRightPanel(Rect panel)
        {
            float x = panel.x;
            float y = panel.y;
            float width = panel.width;

            // --- Economic Stats ---
            // Single-line readout of the period-averaged faction profit. The "Current Rate"
            // subtitle lives only on the per-settlement window — at the faction level we just
            // care about what's actually going to be paid out at the next tax tick.
            bool factionHasAvg = faction.HasTaxAverageData;
            double factionDisplay = factionHasAvg ? faction.averageProfit : faction.profit;

            Rect profitBox = new Rect(x, y, width, 28f);
            Rect profitLabel = new Rect(profitBox.x, profitBox.y, (width - margin) / 2f, profitBox.height);
            Rect profitNum = new Rect(profitLabel.xMax + margin, profitLabel.y, profitLabel.width, profitBox.height);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.DrawHighlight(profitBox);
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(profitLabel, "FCEstimatedProfit".Translate() + ": ");
            Text.Anchor = TextAnchor.MiddleLeft;
            Color profitColor = factionDisplay >= 0 ? AccentUtil.Income : AccentUtil.Expense;
            Widgets.Label(profitNum, new GUIContent(Math.Round(factionDisplay).ToString().Colorize(profitColor), ThingDefOf.Silver.uiIcon));

            TooltipHandler.TipRegion(profitBox, TextUtil.BuildProjectedFactionTooltip());

            y += profitBox.height + margin;

            Rect taxBox = new Rect(x, y, width, 22f);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(taxBox, "FCTimeTillTax".Translate() + ": " + Math.Max(0, faction.taxLedger.nextTaxDueTick - Find.TickManager.TicksGame).ToTimeString());
            TooltipHandler.TipRegion(taxBox, CodexTooltips.GetTaxTimerTooltip());
            y += taxBox.height + margin;

            // Seperator
            Widgets.DrawLineHorizontal(x + margin, y, width - (margin * 2));
            y += margin;

            // --- Resource Pools ---
            int numPools = faction.resourcePools.Count;
            if (numPools > 0)
            {
                float rowSize = 22f;
                float rowWidth = width;
                float sectionHeight = rowSize * Math.Min(numPools, 5);
                float totalHeight = rowSize * numPools;

                Rect poolHeader = new Rect(x, y, width, rowSize);
                Widgets.DrawHighlight(poolHeader);
                Widgets.Label(poolHeader, "FCResourcePools".Translate());
                y += poolHeader.height + margin;

                Rect poolListBox = new Rect(x, y, width, sectionHeight);
                Widgets.DrawMenuSection(poolListBox);
                if (totalHeight > sectionHeight)
                {
                    //scrollbox time, baby
                    // By default, Empire only has two resource pools (research, power). But now that we can add ~more~, I'm including this code to allow the UI to gracefully handle more pools
                    Rect scrollList = ScrollUtil.BeginScrollView(poolListBox, ref poolScrollbar, totalHeight);
                    rowWidth = scrollList.width;
                }

                // draw the pools
                for (int i = 0; i < faction.resourcePools.Count; i++)
                {
                    ResourcePool pool = faction.resourcePools[i];
                    Rect row = new Rect(x, y + (i * rowSize), rowWidth, rowSize);
                    Rect icon = new Rect(row.x + 2f, row.y + 1, rowSize - 2, rowSize - 2);
                    Rect actions = new Rect(row.xMax - 80f, row.y + 1, 79f, rowSize - 2);
                    Rect amount = new Rect(icon.xMax + margin, row.y, actions.x - icon.xMax - (margin * 2), rowSize);

                    if (i % 2 == 0)
                    {
                        Widgets.DrawHighlight(row);
                    }

                    Widgets.DrawBoxSolid(new Rect(row.x, row.y, 3f, rowSize), pool.resource.color);
                    Widgets.ButtonImage(icon, pool.resource.Icon);
                    TooltipHandler.TipRegion(icon, pool.resource.LabelCap);

                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleRight;
                    Widgets.Label(amount, Math.Round(pool.pool).ToString());

                    bool changedGui = false;
                    IEnumerable<FloatMenuOption> options = pool.GetFactionMenuFloatMenuOptions();
                    if (options == null || options.Count() == 0)
                    {
                        GUI.color = Color.gray;
                        changedGui = true;
                    }
                    Text.Font = GameFont.Tiny;
                    if (Widgets.ButtonText(actions, "FCActions".Translate(), active: !changedGui))
                    {
                        List<FloatMenuOption> list = new List<FloatMenuOption>();
                        foreach (FloatMenuOption option in options)
                            list.Add(option);
                        Find.WindowStack.Add(new FloatMenu(list));
                    }
                    if (changedGui)
                    {
                        GUI.color = Color.white;
                    }
                }

                if (totalHeight > sectionHeight)
                {
                    ScrollUtil.EndScrollView();
                }
                y += sectionHeight + margin;

                // Seperator
                Widgets.DrawLineHorizontal(x + margin, y, width - (margin * 2));
                y += margin;
            }

            // --- Resource Production ---
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Rect prodHeaderBox = new Rect(x, y, width, 22f);
            Widgets.DrawHighlight(prodHeaderBox);
            Widgets.Label(prodHeaderBox, "FCTotalProduction".Translate());
            y += prodHeaderBox.height + margin;

            float rowHeight = 22f;
            float rowSpacing = 2f;
            float iconSm = 20f;
            int resourceCount = faction.FactionResources.Count;
            float totalHeight2 = resourceCount * (rowHeight + rowSpacing) - rowSpacing;
            float maxHeight = panel.yMax - y;
            float sectionHeight2 = Math.Min(totalHeight2, maxHeight);
            float rowWidth2 = width;

            Rect sectionBox = new Rect(x, y, width, sectionHeight2);
            bool needsScroll = totalHeight2 > sectionHeight2;
            if (needsScroll)
            {
                Rect scrollContent = ScrollUtil.BeginScrollView(sectionBox, ref productionScroll, totalHeight2);
                rowWidth2 = scrollContent.width;
            }

            int ri = 0;
            Text.Font = GameFont.Tiny;
            foreach (ResourceDisplay resource in faction.FactionResources)
            {
                float ry = y + ri * (rowHeight + rowSpacing);
                Rect rowRect = new Rect(x, ry, rowWidth2, rowHeight);

                if (ri % 2 == 0)
                {
                    Widgets.DrawHighlight(rowRect);
                }

                Widgets.DrawBoxSolid(new Rect(x, ry, 3f, rowHeight), resource.resourceDef.color);

                Rect iconRect = new Rect(x + 5f, ry + (rowHeight - iconSm) / 2f, iconSm, iconSm);
                Widgets.ButtonImage(iconRect, resource.Icon);

                Text.Anchor = TextAnchor.MiddleLeft;
                Rect labelRect = new Rect(iconRect.xMax + 4f, ry, rowWidth2 - iconRect.xMax + x - 4f - 50f, rowHeight);
                Widgets.Label(labelRect, resource.label);

                Text.Anchor = TextAnchor.MiddleRight;
                Rect amountRect = new Rect(x + rowWidth2 - 50f, ry, 48f, rowHeight);
                Widgets.Label(amountRect, resource.amount.ToString());

                TooltipHandler.TipRegion(rowRect, resource.label);
                ri++;
            }

            if (needsScroll)
            {
                ScrollUtil.EndScrollView();
            }
        }

        private static readonly string[] settlementSortLabels =
        {
            "FCSettlementTableName",
            "FCSettlementTableLevel",
            "FCSettlementTableMilLevel",
            "FCSettlementTableProfit",
            "FCSettlementTableWorkers",
            "FCSettlementTableHappiness",
            "FCSettlementTableLoyalty",
            "FCSettlementTableUnrest",
            "FCSettlementTableFounding"
        };

        private static readonly Comparison<WorldSettlementFC>[] settlementSortActions =
        {
            CompareUtil.CompareSettlementName,
            CompareUtil.CompareSettlementLevel,
            CompareUtil.CompareSettlementMilitaryLevel,
            CompareUtil.CompareSettlementProfit,
            CompareUtil.CompareSettlementFreeWorkers,
            CompareUtil.CompareSettlementHappiness,
            CompareUtil.CompareSettlementLoyalty,
            CompareUtil.CompareSettlementUnrest,
            CompareUtil.CompareSettlementFoundingDate
        };

        private void DrawSettlementsTable(Rect tableRect)
        {
            const float rowH = 44f;
            const float accentW = 4f;
            const float rowGap = 2f;
            const float pad = 4f;
            const float summaryH = 24f;
            const float iconSz = 18f;

            float innerX = tableRect.x + pad;
            float innerW = tableRect.width - pad * 2f;

            // Summary header — left: count
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;
            Color origColor = GUI.color;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.DrawColoredLabel(new Rect(innerX, tableRect.y + pad, innerW * 0.5f, summaryH),
                "FCSettlementCount".Translate(faction.settlements.Count), Color.gray);
            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;

            // Summary header — right: sort dropdown
            float sortBtnW = 180f;
            Rect sortBtnRect = new Rect(innerX + innerW - sortBtnW, tableRect.y + pad, sortBtnW, summaryH);
            if (Widgets.ButtonText(sortBtnRect, "FCSortBy".Translate(settlementSortLabels[currentSettlementSortIndex].Translate())))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                for (int i = 0; i < settlementSortLabels.Length; i++)
                {
                    int captured = i;
                    options.Add(new FloatMenuOption(settlementSortLabels[i].Translate(), () =>
                    {
                        currentSettlementSortIndex = captured;
                        faction.settlements.Sort(settlementSortActions[captured]);
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            // Empty state
            if (faction.settlements.Count == 0)
            {
                fontBefore = Text.Font;
                anchorBefore = Text.Anchor;
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.DrawColoredLabel(new Rect(tableRect.x, tableRect.y + tableRect.height * 0.35f, tableRect.width, 40f),
                    "FCNoSettlements".Translate(), Color.gray);
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;
                return;
            }

            // Scrollable settlement list
            float listY = tableRect.y + pad + summaryH + 4f;
            float viewH = tableRect.yMax - listY - pad;
            Rect viewRect = new Rect(innerX, listY, innerW, viewH);
            float contentH = faction.settlements.Count * (rowH + rowGap);
            Rect scrollRect = ScrollUtil.BeginScrollView(viewRect, ref settlementScroll, contentH);

            for (int i = 0; i < faction.settlements.Count; i++)
            {
                WorldSettlementFC s = faction.settlements[i];
                float ry = i * (rowH + rowGap);
                float rowW = scrollRect.width;
                Rect rowRect = new Rect(0f, ry, rowW, rowH);

                // Alternating row background
                if (i % 2 == 0)
                    Widgets.DrawHighlight(rowRect);

                // Accent strip (green = profit, red = loss)
                Color accent = AccentUtil.GetSettlementAccent(s);
                Widgets.DrawBoxSolid(new Rect(0f, ry, accentW, rowH), accent);

                float contentX = accentW + 6f;
                float contentW = rowW - contentX - 4f;
                float topY = ry;
                float botY = ry + rowH / 2f;
                float lineH = rowH / 2f;

                // Top-left: Settlement name (clickable, accent-colored)
                float profitDisplayW = 80f;
                float badgeW = 110f;
                float nameW = contentW - profitDisplayW - badgeW;

                fontBefore = Text.Font;
                anchorBefore = Text.Anchor;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect nameRect = new Rect(contentX, topY, nameW, lineH);
                UIUtil.DrawColoredLabel(nameRect, s.Name, accent);
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;
                if (Widgets.ButtonInvisible(nameRect))
                    Find.WindowStack.Add(new SettlementWindowFc(s));
                if (Mouse.IsOver(nameRect))
                    Widgets.DrawHighlight(nameRect);

                // Top-center: "Lv N • Mil N" badges (gray, Tiny)
                fontBefore = Text.Font;
                anchorBefore = Text.Anchor;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleRight;
                origColor = GUI.color;
                Widgets.Label(new Rect(contentX + nameW, topY, badgeW, lineH),
                    "Lv " + s.settlementLevel + "  •  Mil " + s.settlementMilitaryLevel);
                GUI.color = origColor;
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;

                // Top-right: Profit value. Shows the period-averaged silver — what the player
                // will actually be paid at the next tax tick. Tooltip explains the averaging.
                bool hasAvg = s.TaxAccrualDays > 0;
                int displayProfit = (int)(hasAvg ? s.ProjectedProfit : s.totalProfit);
                string profitStr = "$" + (displayProfit >= 0 ? "+" : "") + displayProfit;
                fontBefore = Text.Font;
                anchorBefore = Text.Anchor;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleRight;
                Rect profitRect = new Rect(contentX + contentW - profitDisplayW, topY, profitDisplayW, lineH);
                UIUtil.DrawColoredLabel(profitRect, profitStr, displayProfit >= 0 ? AccentUtil.Income : AccentUtil.Expense);
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;
                TooltipHandler.TipRegion(profitRect, TextUtil.BuildProjectedTooltip());

                // Bottom-left: Town title + free workers
                string townTitle = TextUtil.GetTownTitle(s);
                int freeWorkers = (int)(s.workersUltraMax - s.GetTotalWorkers());
                string bottomLeftStr = townTitle + "  •  " + freeWorkers + " " + "FCSettlementTableWorkers".Translate();
                fontBefore = Text.Font;
                anchorBefore = Text.Anchor;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                float statsW = 230f;
                Widgets.Label(new Rect(contentX, botY, contentW - statsW, lineH), bottomLeftStr);
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;

                // Bottom-right: Upgrade badge
                float upgradeBadgeW = 105f;
                float upgradeBadgeX = contentX + contentW - statsW;
                if (s.IsUpgrading)
                {
                    // "Upgrading..." label
                    fontBefore = Text.Font;
                    anchorBefore = Text.Anchor;
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    float labelW = 67f;
                    UIUtil.DrawColoredLabel(
                        new Rect(upgradeBadgeX, botY, labelW, lineH),
                        "FCSettlementUpgradeInProgress".Translate(),
                        Color.gray);
                    Text.Font = fontBefore;
                    Text.Anchor = anchorBefore;

                    // Progress bar
                    float progress = UIUtil.NormalizeProgress(s.StartUpgradeTick, s.FinishUpgradeTick);
                    float barW = 30f;
                    float barH = 10f;
                    Rect barRect = new Rect(upgradeBadgeX + labelW + 2f, botY + (lineH - barH) / 2f, barW, barH);
                    UIUtil.DrawProgressBarColors(barRect, progress, new Color(0.15f, 0.15f, 0.15f), new Color(0.3f, 0.75f, 1f));

                    int ticksLeft = Math.Max(0, s.FinishUpgradeTick - Find.TickManager.TicksGame);
                    TooltipHandler.TipRegion(new Rect(upgradeBadgeX, botY, upgradeBadgeW, lineH),
                        "FCUpgradeBadgeInProgress".Translate(ticksLeft.ToStringTicksToPeriod()));
                }
                else if (s.CanUpgrade)
                {
                    fontBefore = Text.Font;
                    Text.Font = GameFont.Tiny;
                    Rect btnRect = new Rect(upgradeBadgeX, botY + 2f, upgradeBadgeW, lineH - 4f);
                    if (UIUtil.ButtonFlat(btnRect, "FCUpgrade".Translate(), AccentUtil.Income, highlighted: i % 2 != 0))
                        Find.WindowStack.Add(new SettlementUpgradeWindowFc(s));
                    Text.Font = fontBefore;

                    int cost = s.GetUpgradeCost(Convert.ToInt32(FCSettings.settlementBaseUpgradeCost));
                    TooltipHandler.TipRegion(new Rect(upgradeBadgeX, botY, upgradeBadgeW, lineH),
                        "FCUpgradeBadgeAvailable".Translate(cost));
                }

                // Bottom-right: Stat icons (happiness, loyalty, unrest)
                float statGroupW = 43f;
                float statsStartX = contentX + contentW - statsW + upgradeBadgeW;
                DrawStatIcon(statsStartX, botY, lineH, iconSz, TexLoad.iconHappiness,
                    ((int)s.Happiness).ToString(), AccentUtil.GetStatColor(s.Happiness, false));
                DrawStatIcon(statsStartX + statGroupW, botY, lineH, iconSz, TexLoad.iconLoyalty,
                    ((int)s.Loyalty).ToString(), AccentUtil.GetStatColor(s.Loyalty, false));
                DrawStatIcon(statsStartX + statGroupW * 2, botY, lineH, iconSz, TexLoad.iconUnrest,
                    ((int)s.Unrest).ToString(), AccentUtil.GetStatColor(s.Unrest, true));

                // Tooltip with full details
                string tooltip = s.Name + "\n\n"
                    + "FCSettlementTableLevel".Translate() + ": " + s.settlementLevel + "\n"
                    + "FCSettlementTableMilLevel".Translate() + ": " + s.settlementMilitaryLevel + "\n"
                    + "FCSettlementTableProfit".Translate() + ": " + displayProfit + "\n"
                    + "FCSettlementTableWorkers".Translate() + ": " + freeWorkers + "/" + (int)s.workersUltraMax + "\n"
                    + "FCSettlementTableHappiness".Translate() + ": " + (int)s.Happiness + "\n"
                    + "FCSettlementTableLoyalty".Translate() + ": " + (int)s.Loyalty + "\n"
                    + "FCSettlementTableUnrest".Translate() + ": " + (int)s.Unrest + "\n"
                    + "FCSettlementTableProsperity".Translate() + ": " + (int)s.Prosperity + "\n"
                    + "FCSettlementTableFounding".Translate() + ": " + s.GetFoundingDate(false);
                if (s.IsUpgrading)
                {
                    int ttTicksLeft = Math.Max(0, s.FinishUpgradeTick - Find.TickManager.TicksGame);
                    tooltip += "\n" + "FCUpgradeBadgeInProgress".Translate(ttTicksLeft.ToStringTicksToPeriod());
                }
                TooltipHandler.TipRegion(rowRect, tooltip);
            }

            ScrollUtil.EndScrollView();
        }


        private static void DrawStatIcon(float x, float y, float lineH, float iconSz, Texture2D icon, string value, Color color)
        {
            float iconY = y + (lineH - iconSz) / 2f;
            GUI.DrawTexture(new Rect(x, iconY, iconSz, iconSz), icon);

            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.DrawColoredLabel(new Rect(x + iconSz + 2f, y, 28f, lineH), value, color);
            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        // ===== BILLS TAB =====

        private void DrawBillsTab(Rect rect)
        {
            IReadOnlyList<BillFC> bills = faction.taxLedger.Bills;
            const float pad = 8f;
            const float rowH = 44f;
            const float accentW = 4f;
            const float rowGap = 2f;
            const float resolveW = 100f;
            const float summaryH = 24f;

            float innerX = rect.x + pad;
            float innerW = rect.width - pad * 2f;

            // Summary line (left): bill count + tax countdown | auto-resolve toggle (right)
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            string taxCountdown = "FCTimeTillTax".Translate() + ": "
                + Math.Max(0, faction.taxLedger.nextTaxDueTick - Find.TickManager.TicksGame).ToTimeString();
            UIUtil.DrawColoredLabel(
                new Rect(innerX, rect.y + pad, innerW * 0.6f, summaryH),
                "FCPendingBillsCount".Translate(bills.Count) + "    |    " + taxCountdown,
                Color.gray);
            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;

            // Auto-resolve checkbox (right side of summary)
            float autoX = innerX + innerW - 180f;
            fontBefore = Text.Font;
            anchorBefore = Text.Anchor;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleRight;
            Rect autoResolveRectLabel = new Rect(autoX, rect.y + pad, 150f, summaryH);
            UIUtil.DrawColoredLabel(autoResolveRectLabel, "FCAutoResolve".Translate(), Color.gray);
            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
            bool prevAutoResolve = faction.taxLedger.autoResolve;
            Widgets.Checkbox(new Vector2(autoX + 153f, rect.y + pad + 1f), ref faction.taxLedger.autoResolve, 22);
            if (faction.taxLedger.autoResolve && !prevAutoResolve)
            {
                Messages.Message("FCBillsAutoResolving".Translate(), MessageTypeDefOf.NeutralEvent);
                faction.taxLedger.AutoresolveBills();
            }
            else if (!faction.taxLedger.autoResolve && prevAutoResolve)
            {
                Messages.Message("FCBillsNotAutoResolving".Translate(), MessageTypeDefOf.NeutralEvent);
            }

            // Allow late payments checkbox (second row, below auto-resolve)
            float lateY = autoResolveRectLabel.y;
            float lateX = autoResolveRectLabel.x - 150f;
            fontBefore = Text.Font;
            anchorBefore = Text.Anchor;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleRight;
            Rect latePaymentRectLabel = new Rect(lateX, lateY, 150f, summaryH);
            UIUtil.DrawColoredLabel(latePaymentRectLabel, "FCAllowLatePayments".Translate(), Color.gray);
            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
            bool prevAllowLate = faction.taxLedger.allowLatePayments;
            Widgets.Checkbox(new Vector2(lateX + 153f, lateY + 1f), ref faction.taxLedger.allowLatePayments, 22);
            if (faction.taxLedger.allowLatePayments && !prevAllowLate)
            {
                Messages.Message("FCBillsLatePaymentsAllowed".Translate(), MessageTypeDefOf.NeutralEvent);
            }
            else if (!faction.taxLedger.allowLatePayments && prevAllowLate)
            {
                Messages.Message("FCBillsLatePaymentsDisallowed".Translate(), MessageTypeDefOf.NeutralEvent);
            }

            // Empty state
            if (bills.Count == 0)
            {
                fontBefore = Text.Font;
                anchorBefore = Text.Anchor;
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.DrawColoredLabel(
                    new Rect(rect.x, rect.y + rect.height * 0.35f, rect.width, 40f),
                    "FCNoPendingBills".Translate(),
                    Color.gray);
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;
                return;
            }

            // Scrollable bill list
            float listY = rect.y + pad + summaryH + 4f;
            float viewH = rect.yMax - listY - pad;
            Rect viewRect = new Rect(innerX, listY, innerW, viewH);
            float contentH = bills.Count * (rowH + rowGap);
            Rect scrollRect = ScrollUtil.BeginScrollView(viewRect, ref billsScroll, contentH);

            if (cachedSortedBills == null || cachedBillsCount != bills.Count)
            {
                cachedSortedBills = bills.OrderBy(b => b.dueTick).ToList();
                cachedBillsCount = bills.Count;
            }
            List<BillFC> sorted = cachedSortedBills;
            for (int i = 0; i < sorted.Count; i++)
            {
                BillFC bill = sorted[i];
                float ry = i * (rowH + rowGap);
                float rowW = scrollRect.width;
                Rect rowRect = new Rect(0f, ry, rowW, rowH);

                // Alternating row background
                if (i % 2 == 0)
                    Widgets.DrawHighlight(rowRect);

                // Accent strip (green = income, red = expense)
                Color accent = GetBillAccentColor(bill);
                Widgets.DrawBoxSolid(new Rect(0f, ry, accentW, rowH), accent);

                float contentX = accentW + 6f;
                float contentW = rowW - contentX - 4f;
                float topY = ry;
                float botY = ry + rowH / 2f;
                float lineH = rowH / 2f;

                // Top-left: Settlement name + bill kind (clickable, colored by bill type)
                string settleName = bill.settlement != null ? bill.settlement.Name : "Null";
                string kindLabel = string.IsNullOrEmpty(bill.label) ? "" : bill.label;
                string headerLabel = settleName + " - " + kindLabel;
                fontBefore = Text.Font;
                anchorBefore = Text.Anchor;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                float nameW = contentW - resolveW - 160f;
                Rect nameRect = new Rect(contentX, topY, nameW, lineH);
                UIUtil.DrawColoredLabel(nameRect, headerLabel, accent);
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;
                if (Widgets.ButtonInvisible(nameRect) && bill.settlement != null)
                    Find.WindowStack.Add(new SettlementWindowFc(bill.settlement));
                if (Mouse.IsOver(nameRect))
                    Widgets.DrawHighlight(nameRect);

                // Top-right: Silver amount (colored) + Resolve button
                float silverW = 140f;
                float silverX = contentX + contentW - resolveW - silverW - 6f;
                string silverStr = bill.taxes.silverAmount.ToString("F0") + " " + "FCSilver".Translate();
                fontBefore = Text.Font;
                anchorBefore = Text.Anchor;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleRight;
                UIUtil.DrawColoredLabel(
                    new Rect(silverX, topY, silverW, lineH),
                    silverStr,
                    bill.taxes.silverAmount >= 0 ? AccentUtil.Income : AccentUtil.Expense);
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;

                // Resolve button (right side, full row height)
                Rect resolveRect = new Rect(contentX + contentW - resolveW, ry + 4f, resolveW, rowH - 8f);
                if (Widgets.ButtonText(resolveRect, "FCResolveBill".Translate()))
                {
                    if (bill.AttemptResolve())
                    {
                        Messages.Message("FCBillResolved".Translate(), MessageTypeDefOf.NeutralEvent);
                        cachedSortedBills = null;
                    }
                    else
                    {
                        int needed = (int)(-1 * bill.taxes.silverAmount);
                        int available = PaymentUtil.GetSilver();
                        Messages.Message(
                            $"{"FCNotEnoughSilverOnMapToPayBill".Translate()} ({available} / {needed} {"FCSilver".Translate()})",
                            MessageTypeDefOf.RejectInput);
                    }
                    break;
                }

                // Bottom-left: Tithe summary
                string titheSummary = GetBillTitheSummary(bill);
                fontBefore = Text.Font;
                anchorBefore = Text.Anchor;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(contentX, botY, contentW - 160f, lineH), titheSummary);
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;

                // Bottom-right: Due time with urgency coloring
                int ticksLeft = bill.dueTick - Find.TickManager.TicksGame;
                string dueStr = ticksLeft <= 0 ? "FCOverdue".Translate().ToString() : Math.Max(ticksLeft, 0).ToTimeString();
                Color dueColor = ticksLeft <= 0 ? new Color(1f, 0.3f, 0.3f)
                    : ticksLeft < GenDate.TicksPerDay ? new Color(1f, 0.7f, 0.2f)
                    : Color.white;
                fontBefore = Text.Font;
                anchorBefore = Text.Anchor;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleRight;
                UIUtil.DrawColoredLabel(
                    new Rect(contentX + contentW - resolveW - 166f, botY, 160f, lineH),
                    dueStr,
                    dueColor);
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;

                // Tooltip
                string tooltip = settleName + "\n\n"
                    + "FCSilver".Translate() + ": " + bill.taxes.silverAmount.ToString("F0") + "\n"
                    + titheSummary + "\n"
                    + "FCDueFC".Translate() + ": " + dueStr;
                TooltipHandler.TipRegion(rowRect, tooltip);
            }

            ScrollUtil.EndScrollView();
        }

        private static Color GetBillAccentColor(BillFC bill)
        {
            return bill.taxes.silverAmount >= 0 ? AccentUtil.Income : AccentUtil.Expense;
        }

        private static string GetBillTitheSummary(BillFC bill)
        {
            int count = bill.taxes.itemTithes.Count;
            return count > 0
                ? "FCTitheItemCount".Translate(count)
                : "FCNoTithes".Translate();
        }

        // ===== EVENTS TAB =====


        private void DrawEventFilterBar(Rect barRect)
        {
            if (cachedSortedCategories == null)
            {
                cachedSortedCategories = FactionCache.FCEventCategoryDefs.OrderBy(c => c.displayOrder).ToList();
            }
            List<FCEventCategoryDef> categories = cachedSortedCategories;
            int count = categories.Count + 1; // +1 for "All" button
            float gap = 3f;
            float btnW = (barRect.width - gap * (count - 1)) / count;

            GameFont fontBefore = Text.Font;
            Text.Font = GameFont.Tiny;

            // "All" button
            Rect allRect = new Rect(barRect.x, barRect.y, btnW, barRect.height);
            bool allActive = hiddenEventCategories.Count == 0;
            Color allColor = allActive ? Color.white : Color.gray;
            if (UIUtil.ButtonFlat(allRect, "FCEventCatAll".Translate(), labelColor: allColor, highlighted: allActive))
            {
                hiddenEventCategories.Clear();
            }

            // Category toggle buttons
            for (int i = 0; i < categories.Count; i++)
            {
                FCEventCategoryDef cat = categories[i];
                float x = barRect.x + (i + 1) * (btnW + gap);
                Rect btnRect = new Rect(x, barRect.y, btnW, barRect.height);

                bool visible = !hiddenEventCategories.Contains(cat);
                Color catColor = cat.color;
                Color labelColor = visible
                    ? catColor
                    : new Color(catColor.r * 0.4f, catColor.g * 0.4f, catColor.b * 0.4f);

                if (UIUtil.ButtonFlat(btnRect, cat.label.CapitalizeFirst(), labelColor: labelColor, highlighted: visible))
                {
                    if (visible)
                        hiddenEventCategories.Add(cat);
                    else
                        hiddenEventCategories.Remove(cat);
                }
            }

            Text.Font = fontBefore;
        }

        private void DrawSituationsTab(Rect rect)
        {
            List<FCSituation> sits = new List<FCSituation>(faction.situationManager.Situations);
            const float pad = 8f;
            float innerX = rect.x + pad;
            float innerW = rect.width - pad * 2f;

            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.DrawColoredLabel(new Rect(innerX, rect.y + pad, innerW, 24f),
                "FCActiveSituationsCount".Translate(sits.Count), Color.gray);
            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;

            if (sits.Count == 0)
            {
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.DrawColoredLabel(new Rect(rect.x, rect.y + rect.height * 0.35f, rect.width, 40f),
                    "FCNoActiveSituations".Translate(), Color.gray);
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;
                return;
            }

            float listY = rect.y + pad + 24f + 6f;
            float viewH = rect.yMax - listY - pad;
            Rect viewRect = new Rect(innerX, listY, innerW, viewH);
            const float gap = 4f;
            float contentH = 0f;
            foreach (FCSituation sit in sits) contentH += SituationsUI.RowHeight(sit, includeActions: false) + gap;

            Rect scrollRect = ScrollUtil.BeginScrollView(viewRect, ref situationsScroll, contentH);
            float y = 0f;
            for (int i = 0; i < sits.Count; i++)
            {
                float h = SituationsUI.RowHeight(sits[i], includeActions: false);
                Rect rowRect = new Rect(0f, y, scrollRect.width, h);
                if (i % 2 == 0) Widgets.DrawHighlight(rowRect);
                SituationsUI.DrawRow(rowRect, sits[i], faction, showTarget: true, includeActions: false);
                y += h + gap;
            }
            ScrollUtil.EndScrollView();
        }

        private void DrawEventsTab(Rect rect)
        {
            IReadOnlyList<FCEvent> events = faction.Events;
            const float pad = 8f;
            const float rowH = 44f;
            const float accentW = 4f;
            const float rowGap = 2f;
            const float progressW = 160f;
            const float progressH = 16f;
            const float summaryH = 24f;
            const float filterH = 24f;

            float innerX = rect.x + pad;
            float innerW = rect.width - pad * 2f;

            // Build sorted + filtered cache
            bool filtering = hiddenEventCategories.Count > 0;
            bool needsRebuild = cachedSortedEvents is null
                || cachedEventsVersion != faction.EventsVersion
                || cachedHiddenCategoriesCount != hiddenEventCategories.Count;
            if (needsRebuild)
            {
                cachedSortedEvents = events.OrderBy(e => e.timeTillTrigger).ToList();
                if (filtering)
                {
                    cachedSortedEvents = cachedSortedEvents
                        .Where(e => !hiddenEventCategories.Contains(AccentUtil.GetEventCategory(e)))
                        .ToList();
                }
                cachedEventsVersion = faction.EventsVersion;
                cachedHiddenCategoriesCount = hiddenEventCategories.Count;
            }
            List<FCEvent> sorted = cachedSortedEvents;

            // Summary line
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            int filteredCount = filtering ? sorted.Count : events.Count;
            string summaryText = filtering
                ? "FCActiveEventsFiltered".Translate(filteredCount, events.Count)
                : "FCActiveEventsCount".Translate(events.Count);
            UIUtil.DrawColoredLabel(
                new Rect(innerX, rect.y + pad, innerW, summaryH),
                summaryText,
                Color.gray);
            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;

            // Filter bar
            Rect filterBar = new Rect(innerX, rect.y + pad + summaryH + 2f, innerW, filterH);
            DrawEventFilterBar(filterBar);

            // Empty state
            if (events.Count == 0)
            {
                fontBefore = Text.Font;
                anchorBefore = Text.Anchor;
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.DrawColoredLabel(
                    new Rect(rect.x, rect.y + rect.height * 0.35f, rect.width, 40f),
                    "FCNoActiveEvents".Translate(),
                    Color.gray);
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;
                return;
            }

            // Scrollable event list
            float listY = rect.y + pad + summaryH + filterH + 6f;
            float viewH = rect.yMax - listY - pad;
            Rect viewRect = new Rect(innerX, listY, innerW, viewH);
            float contentH = sorted.Count * (rowH + rowGap);

            // Filtered empty state
            if (sorted.Count == 0)
            {
                fontBefore = Text.Font;
                anchorBefore = Text.Anchor;
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.DrawColoredLabel(
                    new Rect(rect.x, listY + viewH * 0.25f, rect.width, 40f),
                    "FCNoEventsMatchFilter".Translate(),
                    Color.gray);
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;
                return;
            }

            Rect scrollRect = ScrollUtil.BeginScrollView(viewRect, ref eventsScroll, contentH);

            for (int i = 0; i < sorted.Count; i++)
            {
                FCEvent evt = sorted[i];
                float ry = i * (rowH + rowGap);
                float rowW = scrollRect.width;
                Rect rowRect = new Rect(0f, ry, rowW, rowH);

                // Alternating row background
                if (i % 2 == 0)
                    Widgets.DrawHighlight(rowRect);

                // Category accent strip
                Color catColor = AccentUtil.GetEventCategoryColor(evt);
                Widgets.DrawBoxSolid(new Rect(0f, ry, accentW, rowH), catColor);

                float contentX = accentW + 6f;
                float contentW = rowW - contentX - 4f;
                float topY = ry;
                float botY = ry + rowH / 2f;
                float lineH = rowH / 2f;

                // Top-left: Event name (colored by category for emphasis)
                fontBefore = Text.Font;
                anchorBefore = Text.Anchor;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.DrawColoredLabel(
                    new Rect(contentX, topY, contentW - progressW - 10f, lineH),
                    evt.Label,
                    catColor);
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;

                // Top-right: Clickable location label
                string locLabel = GetEventLocationLabel(evt);
                bool hasLocRect = false;
                Rect locRect = default(Rect);
                if (locLabel != null)
                {
                    fontBefore = Text.Font;
                    anchorBefore = Text.Anchor;
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleRight;
                    float locW = Mathf.Min(Text.CalcSize(locLabel).x + 8f, contentW * 0.4f);
                    locRect = new Rect(contentX + contentW - locW, topY, locW, lineH);
                    hasLocRect = true;
                    UIUtil.DrawColoredLabel(locRect, locLabel, new Color(0.7f, 0.8f, 0.9f));
                    if (Widgets.ButtonInvisible(locRect))
                        HandleLocationClick(evt);
                    if (Mouse.IsOver(locRect))
                        Widgets.DrawHighlight(locRect);
                    Text.Font = fontBefore;
                    Text.Anchor = anchorBefore;
                }

                // Bottom-left: Description text (white, truncated with ellipsis)
                fontBefore = Text.Font;
                anchorBefore = Text.Anchor;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                float descW = contentW - progressW - 10f;
                Rect descRect = new Rect(contentX, botY, descW, lineH);
                UIUtil.ClampedLabel(descRect, TextUtil.CleaveAtNewline(GetEventDescription(evt)));
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;

                // Bottom-right: Progress bar + time label
                float progress = evt.Progress;
                string timeStr;
                if (evt.HasVariableDuration)
                {
                    int now = Find.TickManager.TicksGame;
                    if (now < evt.timeMinTrigger)
                    {
                        int minLeft = evt.timeMinTrigger - now;
                        int maxLeft = evt.timeMaxTrigger - now;
                        timeStr = "FCDurationRange".Translate(
                            Math.Max(minLeft, 0).ToTimeString(),
                            Math.Max(maxLeft, 0).ToTimeString());
                    }
                    else
                    {
                        int ticksLeft = evt.timeMaxTrigger - now;
                        timeStr = "FCEndsWithin".Translate(Math.Max(ticksLeft, 0).ToTimeString());
                    }
                }
                else
                {
                    int ticksLeft = evt.timeTillTrigger - Find.TickManager.TicksGame;
                    timeStr = Math.Max(ticksLeft, 0).ToTimeString();
                }

                float barX = contentX + contentW - progressW;
                float barY = botY + (lineH - progressH) / 2f;
                Color barBg = new Color(catColor.r * 0.25f, catColor.g * 0.25f, catColor.b * 0.25f);
                Color barFill = new Color(catColor.r * 0.7f, catColor.g * 0.7f, catColor.b * 0.7f);
                UIUtil.DrawProgressBarColors(new Rect(barX, barY, progressW, progressH), progress, barBg, barFill);

                fontBefore = Text.Font;
                anchorBefore = Text.Anchor;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(barX, barY, progressW, progressH), timeStr);
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;

                // Row-wide click for in-progress battle events: anywhere outside the location
                // label opens the live battle window. The location label keeps its own
                // "open settlement window" behavior.
                if (evt.def == FCEventDefOf.autoResolveBattleRound
                    && evt.linkedOperation is object
                    && evt.linkedOperation.battleResult is object)
                {
                    bool overLoc = hasLocRect && Mouse.IsOver(locRect);
                    if (!overLoc && Mouse.IsOver(rowRect))
                        Widgets.DrawHighlight(rowRect);
                    if (!overLoc && Widgets.ButtonInvisible(rowRect))
                        Find.WindowStack.Add(new BattleProgressWindow(evt.linkedOperation));
                }

                // Row tooltip
                string tooltip = GetEventFullTooltip(evt);
                TooltipHandler.TipRegion(rowRect, tooltip);
            }

            ScrollUtil.EndScrollView();
        }


        private string GetEventLocationLabel(FCEvent evt)
        {
            if (evt.hasDestination)
            {
                WorldSettlementFC settlement = faction.ReturnSettlementByLocation(evt.location);
                return settlement?.Name;
            }
            if (evt.settlementTraitLocations.Count == 1)
                return evt.settlementTraitLocations[0]?.Name;
            if (evt.settlementTraitLocations.Count > 1)
                return "FCMultipleSettlements".Translate(evt.settlementTraitLocations.Count);
            if (evt.def == FCEventDefOf.taxColony && evt.source != -1)
            {
                WorldSettlementFC settlement = faction.ReturnSettlementByLocation(evt.source);
                return settlement?.Name;
            }
            // Generic fallback: try location, then source
            if (evt.location != -1)
            {
                WorldSettlementFC settlement = faction.ReturnSettlementByLocation(evt.location);
                if (settlement != null) return settlement.Name;
            }
            if (evt.source != -1)
            {
                WorldSettlementFC settlement = faction.ReturnSettlementByLocation(evt.source);
                if (settlement != null) return settlement.Name;
            }
            return null;
        }

        private static string GetEventDescription(FCEvent evt)
        {
            if (evt.hasCustomDescription && !evt.customDescription.NullOrEmpty())
                return evt.customDescription.Format();
            return evt.def.FormattedDesc ?? "";
        }

        private string GetEventFullTooltip(FCEvent evt)
        {
            return $"{evt.Label}\n\n{FCEventMaker.BuildEventLetterBody(evt)}";
        }

        private void HandleLocationClick(FCEvent evt)
        {
            if (evt.hasDestination)
            {
                Find.WindowStack.Add(new SettlementWindowFc(faction.ReturnSettlementByLocation(evt.location)));
            }
            else if (evt.settlementTraitLocations.Count > 0)
            {
                List<FloatMenuOption> list = new List<FloatMenuOption>();
                foreach (WorldSettlementFC settlement in evt.settlementTraitLocations)
                {
                    if (settlement != null)
                    {
                        WorldSettlementFC cap = settlement;
                        list.Add(new FloatMenuOption(settlement.Name, delegate
                        {
                            Find.WindowStack.Add(new SettlementWindowFc(cap));
                        }));
                    }
                }
                if (list.Count == 0)
                    list.Add(new FloatMenuOption("FCNone".Translate(), null));

                if (list.Count == 1 && list[0].action != null)
                    list[0].action();
                else
                    Find.WindowStack.Add(new FloatMenu(list));
            }
            else if (evt.def == FCEventDefOf.taxColony && evt.source != -1)
            {
                Find.WindowStack.Add(new SettlementWindowFc(faction.ReturnSettlementByLocation(evt.source)));
            }
            else
            {
                // Generic fallback: try location, then source
                WorldSettlementFC fallback = null;
                if (evt.location != -1)
                    fallback = faction.ReturnSettlementByLocation(evt.location);
                if (fallback == null && evt.source != -1)
                    fallback = faction.ReturnSettlementByLocation(evt.source);
                if (fallback != null)
                    Find.WindowStack.Add(new SettlementWindowFc(fallback));
            }
        }

        // ===== MILITARY TAB =====

        private void DrawEdictsTab(Rect rect)
        {
            EdictTabDrawer.Draw(rect, faction);
        }

        // Military tab subtabs: 0 = By Settlement, 1 = By Squad, 2 = Battle Reports.
        // Persists across this MainTabWindow_Colony instance.
        private int militarySubtab = 0;
        private MainTabWindow_Squads _bySquadRenderer;
        private MainTabWindow_BattleReports _battleReportsRenderer;

        private void DrawMilitaryTab(Rect rect)
        {
            float x = rect.x;
            float y = rect.y;
            float width = rect.width;

            // --- Create buttons (right-aligned) ---
            float buttonWidth = 187f;
            float buttonHeight = 35f;
            float bx = rect.xMax - buttonWidth * 3 - margin;

            Rect iconRect = new Rect(x + margin, y + margin, buttonHeight, buttonHeight);
            Widgets.ButtonImage(iconRect, faction.factionIcon);

            Rect labelBox = new Rect(iconRect.xMax + margin, y + margin, bx - iconRect.xMax - (margin * 2), buttonHeight);
            Rect labelTextBox = new Rect(labelBox.x + margin, labelBox.y, labelBox.width - (margin * 2), labelBox.height);

            GameFont headerFontBefore = Text.Font;
            TextAnchor headerAnchorBefore = Text.Anchor;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.DrawHighlight(labelBox);
            Widgets.Label(labelTextBox, faction.name ?? "");
            Text.Font = headerFontBefore;
            Text.Anchor = headerAnchorBefore;

            if (faction.settlements?.Count > 0)
            {
                if (Widgets.ButtonTextSubtle(new Rect(bx, y + margin, buttonWidth, buttonHeight), "FCMilitaryTableButtonCreateUnit".Translate()))
                    OpenMilitaryWindow(MilitaryWindowRegistry.CreateUnits(militaryFC, faction), "FCMilitaryTableButtonCreateUnit".Translate());
                bx += buttonWidth;

                if (Widgets.ButtonTextSubtle(new Rect(bx, y + margin, buttonWidth, buttonHeight), "FCMilitaryTableButtonCreateSquad".Translate()))
                    OpenMilitaryWindow(MilitaryWindowRegistry.CreateSquads(militaryFC, faction), "FCMilitaryTableButtonCreateSquad".Translate());
                bx += buttonWidth;

                if (Widgets.ButtonTextSubtle(new Rect(bx, y + margin, buttonWidth, buttonHeight), "FCMilitaryTableButtonCreateFireSupport".Translate()))
                    OpenMilitaryWindow(MilitaryWindowRegistry.CreateFireSupport(militaryFC, faction), "FCMilitaryTableButtonCreateFireSupport".Translate());
            }

            y += buttonHeight + margin * 2;

            /* Subtab strip spans full inner width; DrawTabRow returns the content area below.
               Force Small font here — ButtonFlat inherits the ambient Text.Font, and the
               header label above this method runs at Medium on the very first frame
               (before any tab content has had a chance to set its own font). */
            float subtabAreaH = rect.yMax - y - margin;
            if (subtabAreaH <= 0f) return;
            Rect subtabBox = new Rect(x + margin, y, width - (margin * 2), subtabAreaH);

            Text.Font = GameFont.Small;
            List<string> tabLabels = new List<string>
            {
                (string)"FCMilitaryTabBySettlement".Translate(),
                (string)"FCMilitaryTabBySquad".Translate(),
                (string)"FCMilitaryTabBattleReports".Translate(),
            };
            Rect contentRect;
            militarySubtab = UIUtil.DrawTabRow(subtabBox, tabLabels, militarySubtab,
                out contentRect, tabHeight: 24f);

            Rect tableRect = contentRect.ContractedBy(2f);
            if (tableRect.height <= 0f) return;

            if (militarySubtab == 0)
            {
                DrawMilitarySettlementCards(tableRect);
            }
            else if (militarySubtab == 1)
            {
                if (_bySquadRenderer is null) _bySquadRenderer = new MainTabWindow_Squads();
                _bySquadRenderer.Draw(tableRect);
            }
            else
            {
                if (_battleReportsRenderer is null) _battleReportsRenderer = new MainTabWindow_BattleReports();
                _battleReportsRenderer.Draw(tableRect);
            }
        }

        private void DrawMilitarySettlementCards(Rect tableRect)
        {
            const float headerH = 26f;
            const float slotH = 22f;
            const float accentW = 4f;
            const float rowGap = 2f;
            const float pad = 4f;
            const float summaryH = 24f;
            const float externalRowH = 44f;

            float innerX = tableRect.x + pad;
            float innerW = tableRect.width - pad * 2f;

            // Build list of settlements with military comps
            List<WorldSettlementFC> settlements = faction.settlements.Where(s => s.MilitaryComp != null).ToList();

            // Summary header — left: count
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            IReadOnlyList<IMilitaryTabEntry> externalEntries = MilitaryTabRegistry.Entries;
            int totalMilitaryCount = settlements.Count + externalEntries.Count;

            string countLabel = externalEntries.Count > 0
                ? "FCMilitarySettlementCount".Translate(settlements.Count) + " + " + externalEntries.Count
                : "FCMilitarySettlementCount".Translate(settlements.Count).ToString();
            UIUtil.DrawColoredLabel(
                new Rect(innerX, tableRect.y + pad, innerW * 0.5f, summaryH),
                countLabel,
                Color.gray);
            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;

            // Empty state
            if (totalMilitaryCount == 0)
            {
                fontBefore = Text.Font;
                anchorBefore = Text.Anchor;
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.DrawColoredLabel(
                    new Rect(tableRect.x, tableRect.y + tableRect.height * 0.35f, tableRect.width, 40f),
                    "FCNoMilitarySettlements".Translate(),
                    Color.gray);
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;
                return;
            }

            // Pre-compute per-settlement card heights (variable: headerH + cap*slotH).
            int[] settlementCaps = new int[settlements.Count];
            float[] cardHeights = new float[settlements.Count];
            float totalContentH = 0f;
            for (int i = 0; i < settlements.Count; i++)
            {
                int cap = settlements[i].SquadCap;
                settlementCaps[i] = cap;
                float h = headerH + (cap > 0 ? cap * slotH : 0f);
                cardHeights[i] = h;
                totalContentH += h + rowGap;
            }
            totalContentH += externalEntries.Count * (externalRowH + rowGap);

            // Scrollable card list
            float listY = tableRect.y + pad + summaryH + 4f;
            float viewH = tableRect.yMax - listY - pad;
            Rect viewRect = new Rect(innerX, listY, innerW, viewH);
            Rect scrollRect = ScrollUtil.BeginScrollView(viewRect, ref militaryScroll, totalContentH);

            float runningY = 0f;
            for (int i = 0; i < settlements.Count; i++)
            {
                WorldSettlementFC settlement = settlements[i];
                WorldObjectComp_SettlementMilitary milComp = settlement.MilitaryComp;
                int settlementCap = settlementCaps[i];
                float cardH = cardHeights[i];
                float ry = runningY;
                float rowW = scrollRect.width;
                Rect cardRect = new Rect(0f, ry, rowW, cardH);

                // Alternating row background
                bool isHighlighted = i % 2 == 0;
                if (isHighlighted)
                    Widgets.DrawHighlight(cardRect);

                // Accent strip spans full card height
                Color accent = AccentUtil.GetMilitaryAccent(milComp);
                Widgets.DrawBoxSolid(new Rect(0f, ry, accentW, cardH), accent);

                float contentX = accentW + 6f;
                float contentW = rowW - contentX - 4f;
                float topY = ry;
                float lineH = headerH;

                // === HEADER ROW ===
                List<MercenarySquadFC> stationed = settlement.StationedSquads;

                float fsBtnW = 110f;
                float counterW = 96f;
                float badgeW = 180f;
                float nameW = contentW - fsBtnW - counterW - badgeW - 12f;

                // Header-left: Settlement name (clickable, accent-colored)
                fontBefore = Text.Font;
                anchorBefore = Text.Anchor;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect nameRect = new Rect(contentX, topY, nameW, lineH);
                UIUtil.DrawColoredLabel(nameRect, settlement.Name, accent);
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;
                if (Widgets.ButtonInvisible(nameRect))
                    Find.WindowStack.Add(new SettlementWindowFc(settlement));
                if (Mouse.IsOver(nameRect))
                    Widgets.DrawHighlight(nameRect);

                // Header-center-left: Def + max-deploy-cost badge.
                // Power is squad-derived: strongest available stationed squad (white), or
                // strongest stationed if all busy (yellow), or half-power ghost if empty
                // billet (red). Grey when cap=0. Max deploy cost is the settlement's
                // squad-value budget scaled by the deploy-cost percentage, so it compares
                // apples-to-apples with the Deploy Cost shown in deploy windows.
                fontBefore = Text.Font;
                anchorBefore = Text.Anchor;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                double rawBudget = MilitaryFC.CalculateSquadBudget(settlement.settlementMilitaryLevel);
                int maxDeploy = MilitaryDeploymentUtil.CalculateDeploymentCost(rawBudget);
                FactionFC fcBadge = FindFC.FactionComp;
                (double powLevel, double powEff, SettlementPowerStatus powStatus) = settlement.GetDisplayedPower();
                double defPower = Math.Round(
                    (powLevel + fcBadge.GetStatValue(FCStatDefOf.militaryLevelBonusDefending))
                    * powEff * fcBadge.GetStatValue(FCStatDefOf.militaryEfficiencyBonusDefending)
                    * FCSettings.defenderAdvantage);
                string badgeStr = "FCMilBadge".Translate(defPower, maxDeploy);
                UIUtil.DrawColoredLabel(
                    new Rect(contentX + nameW, topY, badgeW, lineH),
                    badgeStr,
                    ColorForPowerStatus(powStatus));
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;
                Rect badgeRect = new Rect(contentX + nameW, topY, badgeW, lineH);
                TooltipHandler.TipRegion(badgeRect, TooltipForPowerStatus(powStatus, settlement, stationed));

                // Header-center-right: "Squads: N / M" counter (or "No military" for cap=0)
                fontBefore = Text.Font;
                anchorBefore = Text.Anchor;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                string counterStr = settlementCap == 0
                    ? (string)"FCMilitaryTableNoMilitary".Translate()
                    : (string)"FCMilitaryTableSquadsCounter".Translate(stationed.Count, settlementCap);
                UIUtil.DrawColoredLabel(
                    new Rect(contentX + nameW + badgeW, topY, counterW, lineH),
                    counterStr,
                    settlementCap == 0 ? Color.gray : GUI.color);
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;

                // Header-right: Fire Support button (visibility decoupled from squad cap).
                bool noFireSupport = militaryFC.fireSupportDefs.Count == 0
                    || settlement.BuildingsComp?.HasBuilding(BuildingFCDefOf.artilleryOutpost) == false;
                if (!noFireSupport)
                {
                    bool fsDisabled = milComp.artilleryTimer > Find.TickManager.TicksGame
                        || !FindFC.FactionComp.IsActionAllowed(FCActionType.UseFireSupport);
                    float fsBtnH = lineH - 6f;
                    float fsBtnY = topY + (lineH - fsBtnH) / 2f;
                    Rect fsSupportRect = new Rect(contentX + contentW - fsBtnW, fsBtnY, fsBtnW, fsBtnH);
                    Text.Font = GameFont.Tiny;
                    if (UIUtil.ButtonFlat(fsSupportRect, "FCMilitaryTableFireSupport".Translate(), disabled: fsDisabled, highlighted: isHighlighted))
                    {
                        HandleFireSupportClick(settlement, milComp);
                    }
                    TooltipHandler.TipRegion(fsSupportRect, "FCMilBtnFireSupportTip".Translate());
                    Text.Font = fontBefore;

                    if (milComp.artilleryTimer > Find.TickManager.TicksGame)
                    {
                        int fsTicksLeft = Math.Max(0, milComp.artilleryTimer - Find.TickManager.TicksGame);
                        string fsTimer = "FCMilFireSupportCooldownShort".Translate() + ": " + fsTicksLeft.ToTimeString();
                        TooltipHandler.TipRegion(fsSupportRect, fsTimer);
                    }
                }

                // === SLOT ROWS (cap > 0) ===
                if (settlementCap > 0)
                {
                    for (int slotIdx = 0; slotIdx < settlementCap; slotIdx++)
                    {
                        float slotY = ry + headerH + (slotIdx * slotH);
                        Rect slotRect = new Rect(contentX, slotY, contentW, slotH);
                        MercenarySquadFC squadInSlot = (slotIdx < stationed.Count) ? stationed[slotIdx] : null;
                        DrawSettlementSlotRow(slotRect, settlement, milComp, slotIdx, squadInSlot, isHighlighted);
                    }
                }

                // Card-level tooltip on the header strip
                string squadName = stationed.Count > 0
                    ? (stationed[0]?.DisplayName ?? "FCNone".Translate())
                    : (string)"FCNone".Translate();
                // Effective level (what defends now, squad-derived powLevel) vs the level cap
                // (settlementMilitaryLevel — strongest squad this settlement can field). Kept concise;
                // the "Def" badge + its tooltip cover the full defensive-power breakdown.
                string tooltip = settlement.Name + "\n\n"
                    + "FCMilitaryTableBaseLevel".Translate() + ": " + powLevel.ToString("0.#") + "\n"
                    + "FCMilitaryTableLevelCap".Translate() + ": " + settlement.settlementMilitaryLevel + "\n"
                    + "FCMilitaryTableMilitaryBudget".Translate() + ": $" + rawBudget + "\n"
                    + "FCMilitaryTableSquad".Translate() + ": " + squadName + "\n"
                    + "FCMilitaryTableAvailable".Translate() + ": " + (milComp.militaryBusy ? "FCNo".Translate() : "FCYes".Translate()) + "\n"
                    + "FCMilitaryTableUnderAttack".Translate() + ": " + (milComp.isUnderAttack ? "FCYes".Translate() : "FCNo".Translate());
                TooltipHandler.TipRegion(new Rect(0f, ry, rowW, headerH), tooltip);

                runningY += cardH + rowGap;
            }

            // === External military tab entries (e.g., defensive outposts) ===
            for (int j = 0; j < externalEntries.Count; j++)
            {
                IMilitaryTabEntry entry = externalEntries[j];
                int rowIndex = settlements.Count + j;
                float ry = runningY;
                float rowW = scrollRect.width;
                float rowH = externalRowH;
                Rect rowRect = new Rect(0f, ry, rowW, rowH);

                bool isHighlighted = rowIndex % 2 == 0;
                if (isHighlighted)
                    Widgets.DrawHighlight(rowRect);

                // Accent strip
                Color accent = entry.AccentColor;
                Widgets.DrawBoxSolid(new Rect(0f, ry, accentW, rowH), accent);

                float contentX = accentW + 6f;
                float contentW = rowW - contentX - 4f;
                float topY = ry;
                float botY = ry + rowH / 2f;
                float lineH = rowH / 2f;

                // === TOP LINE ===
                float statusW = 190f;
                float badgeW = 120f;
                float nameW = contentW - statusW - badgeW;

                // Top-left: Entry name (clickable, accent-colored — zooms to world object)
                fontBefore = Text.Font;
                anchorBefore = Text.Anchor;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect nameRect = new Rect(contentX, topY, nameW, lineH);
                UIUtil.DrawColoredLabel(nameRect, entry.Name, accent);
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;
                if (Widgets.ButtonInvisible(nameRect))
                    CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(entry.WorldObject));
                if (Mouse.IsOver(nameRect))
                    Widgets.DrawHighlight(nameRect);

                // Top-center: Defense power badge
                fontBefore = Text.Font;
                anchorBefore = Text.Anchor;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                double entryDefPower = Math.Round(entry.MilitaryLevel * FCSettings.defenderAdvantage);
                string entryBadge = "Mil " + entry.MilitaryLevel + " \u2022 Def " + entryDefPower;
                Widgets.Label(new Rect(contentX + nameW, topY, badgeW, lineH), entryBadge);
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;

                // Top-right: Status label
                fontBefore = Text.Font;
                anchorBefore = Text.Anchor;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleRight;
                UIUtil.DrawColoredLabel(
                    new Rect(contentX + contentW - statusW, topY, statusW, lineH),
                    entry.StatusLabel,
                    accent);
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;

                // === BOTTOM LINE ===
                float btnW = 80f;
                float btnH = lineH - 4f;
                float btnY = botY + 2f;

                // Bottom-left: Type label
                fontBefore = Text.Font;
                anchorBefore = Text.Anchor;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(contentX, botY, contentW - btnW - 4f, lineH), entry.WorldObject.def.label.CapitalizeFirst());
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;

                // Bottom-right: Auto-defend toggle only
                Text.Font = GameFont.Tiny;
                Rect autoDefRect = new Rect(contentX + contentW - btnW, btnY, btnW, btnH);
                if (UIUtil.ButtonFlat(autoDefRect, "FCMilAutoDefend".Translate(),
                    labelColor: entry.AutoDefend ? AccentUtil.MilReady : (Color?)null,
                    highlighted: isHighlighted))
                {
                    entry.AutoDefend = !entry.AutoDefend;
                }
                TooltipHandler.TipRegion(autoDefRect, "FCMilBtnAutoDefendTip".Translate());
                Text.Font = fontBefore;

                // Tooltip
                string entryTooltip = entry.Name + "\n\n"
                    + "FCSettlementTableMilLevel".Translate() + ": " + entry.MilitaryLevel + "\n"
                    + "FCMilitaryTableUnderAttack".Translate() + ": " + (entry.IsUnderAttack ? "FCYes".Translate() : "FCNo".Translate());
                TooltipHandler.TipRegion(new Rect(0f, ry, contentX + contentW - btnW, rowH), entryTooltip);

                runningY += rowH + rowGap;
            }

            ScrollUtil.EndScrollView();
        }

        /// <summary>Draws a single slot row in a settlement card. Slot row contains:
        /// "Slot N" label, squad name (or empty), Set/Inspect button, Deploy button,
        /// Auto-Defend toggle, status text. The Set button opens a settlement-wide squad menu;
        /// the per-squad buttons operate on the slot's specific squad. Empty slots show only the
        /// "Set" button (no Deploy/Inspect/Auto-Defend).</summary>
        private void DrawSettlementSlotRow(Rect rect, WorldSettlementFC settlement,
            WorldObjectComp_SettlementMilitary milComp, int slotIdx, MercenarySquadFC squad, bool isHighlighted)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;

            float btnW = 80f;
            float btnGap = 2f;
            float btnH = rect.height - 4f;
            float btnY = rect.y + 2f;

            float powW = 90f;
            float depCostW = 150f;

            // Slot index column (indented to suggest it's a child of the settlement header)
            const float slotIndent = 18f;
            float idxW = 40f;
            Rect fullSlot = new Rect(rect.x + slotIndent, rect.y, rect.width - slotIndent + 4f, rect.height);
            Rect slotLabel = new Rect(rect.x + slotIndent, rect.y, idxW, rect.height);

            UIUtil.DrawColoredHighlight(fullSlot, AccentUtil.GetSquadAccent(squad));

            // Per-slot accent sub-mark: sits inside the indent, just left of the "Slot N" label
            // (indent -> accent -> "Slot N"). GetSquadAccent returns grey (MilInactive) for empty slots.
            const float slotAccentW = 3f;
            Widgets.DrawBoxSolid(
                new Rect(slotLabel.x, rect.y, slotAccentW, rect.height),
                AccentUtil.GetSquadAccent(squad));

            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(slotLabel, "FCMilitaryTableSlotPrefix".Translate(slotIdx + 1));

            // Squad name area. Amber + tooltip when underfunded so the player understands
            // why the deploy/op buttons are greyed. Red is reserved for under-attack state.
            string squadName = squad?.DisplayName ?? (string)"FCMilitaryTableSlotEmpty".Translate();
            float buttonAreaW = btnW * 4 + btnGap * 3;
            const float statusW = 150f;
            // Name area also reserves the status column + a margin between name and status.
            float nameAreaW = rect.xMax - slotLabel.xMax - buttonAreaW - 4f - powW - depCostW - statusW - (margin * 3);
            if (nameAreaW < 40f) nameAreaW = 40f;
            Rect squadNameLabel = new Rect(slotLabel.xMax + 5f, rect.y, nameAreaW, rect.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            int underSquadDeploy = 0;
            int underMaxDeploy = 0;
            bool slotUnderfunded = squad is object
                && MilitaryFC.SquadExceedsSettlementBudget(
                    squad, settlement, out underSquadDeploy, out underMaxDeploy);
            UIUtil.DrawColoredLabel(squadNameLabel, squadName,
                slotUnderfunded ? AccentUtil.MilUnderfunded : GUI.color);
            if (slotUnderfunded)
            {
                TooltipHandler.TipRegion(squadNameLabel,
                    "FCMilSlotUnderfundedTip".Translate(settlement.Name, underSquadDeploy, underMaxDeploy));
            }

            // Squad status (between name and Power). Single source of truth: SquadStatusUtil.Resolve.
            // Left-aligned (anchor still MiddleLeft from the name); clamped with ellipsis + tooltip when
            // it doesn't fit. Mirrors the status shown in the Squads subtab.
            Rect statusRect = new Rect(squadNameLabel.xMax + margin, rect.y, statusW, rect.height);
            if (squad is object)
            {
                SquadStatusUtil.Resolve(squad, out string statusLabel, out Color statusColor, out _);
                string statusShown = Text.ClampTextWithEllipsis(statusRect, statusLabel);
                UIUtil.DrawColoredLabel(statusRect, statusShown, statusColor);
                if (statusShown != statusLabel) TooltipHandler.TipRegion(statusRect, statusLabel);
            }

            Text.Anchor = TextAnchor.MiddleCenter;
            Rect powerLabel = new Rect(statusRect.xMax + margin, rect.y, powW, rect.height);
            Rect depCostLabel = new Rect(powerLabel.xMax + margin, rect.y, depCostW, rect.height);
            if (squad is object)
            {
                double powerLevel = SquadPowerRegistry.Resolve(squad).militaryLevel;
                string powerLbl = (string)"FCSquadColPower".Translate() + ": " + powerLevel.ToString("0.0");
                string costLbl = "FCDeployCost".Translate(squad.DeploymentCost());
                Widgets.Label(powerLabel, powerLbl);
                Widgets.Label(depCostLabel, costLbl);
            }

            // Action buttons (right-aligned)
            float bx = rect.xMax - buttonAreaW;

            // Set / Change squad — disabled when no hired squads exist (templates alone aren't
            // enough; the menu lists the live pool, not templates), or when the slot's squad is busy.
            bool noSquads = (militaryFC.mercenarySquads?.Count ?? 0) == 0;
            bool slotBusy = squad != null && squad.IsBusy;
            Rect setRect = new Rect(bx, btnY, btnW, btnH);
            string setLabel = squad is null
                ? (string)"FCMilitaryTableSetSquad".Translate()
                : (string)"FCMilitaryTableChangeSquad".Translate();
            if (UIUtil.ButtonFlat(setRect, setLabel, disabled: noSquads || slotBusy, highlighted: isHighlighted))
            {
                Find.WindowStack.Add(new Dialog_AssignSquadToSettlement(settlement, squad));
            }
            TooltipHandler.TipRegion(setRect, slotBusy
                ? "FCSquadCannotModifyBusyTip".Translate()
                : "FCMilBtnSetSquadTip".Translate());
            bx += btnW + btnGap;

            // Inspect (per-squad) — opens Dialog_SquadInspection on this slot's squad.
            // Always available even when busy so the player can read pawn state.
            bool canInspect = squad != null;
            Rect inspectRect = new Rect(bx, btnY, btnW, btnH);
            if (UIUtil.ButtonFlat(inspectRect, "FCMilitaryTableInspect".Translate(), disabled: !canInspect, highlighted: isHighlighted))
            {
                Find.WindowStack.Add(new Dialog_SquadInspection(squad));
            }
            TooltipHandler.TipRegion(inspectRect, "FCMilBtnInspectTip".Translate());
            bx += btnW + btnGap;

            // Deploy (per-squad) — offers walk-in + drop pod options for this slot's squad.
            // Gated on IsAvailable (not just IsBusy) so underfunded squads and cooling-down
            // squads can't deploy. The squad-name tooltip explains the underfunded state.
            bool deployDisabled = squad is null || !squad.IsAvailable
                || (squad.outfit is null && (squad.mercenaries?.Any(m => m?.pawn != null) != true));
            Rect deployRect = new Rect(bx, btnY, btnW, btnH);
            if (UIUtil.ButtonFlat(deployRect, "FCDeploy".Translate(), disabled: deployDisabled, highlighted: isHighlighted))
            {
                if (squad != null)
                {
                    Find.WindowStack.Add(new FloatMenu(SquadDeploymentOptions(settlement, squad)));
                }
            }
            string deployTip;
            if (slotBusy) deployTip = "FCSquadCannotModifyBusyTip".Translate();
            else if (slotUnderfunded) deployTip = "FCMilSlotUnderfundedTip".Translate(settlement.Name, underSquadDeploy, underMaxDeploy);
            else if (squad is object && squad.DeploymentCost() > 0) deployTip = "FCMilBtnDeployTipWithCost".Translate(squad.DeploymentCost(), FCSettings.deploymentBillLifespan_days);
            else deployTip = "FCMilBtnDeployTip".Translate();
            TooltipHandler.TipRegion(deployRect, deployTip);
            bx += btnW + btnGap;

            // Auto-Defend toggle (per-squad)
            bool canToggle = squad != null && !slotBusy;
            bool autoDefendOn = squad?.autoDefend ?? false;
            Rect autoDefRect = new Rect(bx, btnY, btnW, btnH);
            if (UIUtil.ButtonFlat(autoDefRect, "FCMilAutoDefend".Translate(),
                disabled: !canToggle,
                labelColor: autoDefendOn ? AccentUtil.MilReady : (Color?)null,
                highlighted: isHighlighted))
            {
                if (squad != null) squad.autoDefend = !squad.autoDefend;
            }
            TooltipHandler.TipRegion(autoDefRect, slotBusy
                ? "FCSquadCannotModifyBusyTip".Translate()
                : "FCMilBtnAutoDefendTip".Translate());

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        private void HandleDeployClick(WorldSettlementFC settlement, WorldObjectComp_SettlementMilitary milComp)
        {
            if (!milComp.IsMilitaryBusy(true) && milComp.IsMilitarySquadValid())
            {
                Find.WindowStack.Add(new FloatMenu(DeploymentOptions(settlement)));
            }
            else if (milComp.IsMilitaryBusy(true) && milComp.IsMilitarySquadValid() && FindFC.FactionComp.IsActionAllowed(FCActionType.DeployExtraSquad))
            {
                List<FloatMenuOption> extraOptions = new List<FloatMenuOption>();
                FindFC.PolicyManager.ForEachBehavior(b =>
                {
                    var options = b.GetExtraDeploymentOptions(faction, settlement, milComp);
                    if (options != null) extraOptions.AddRange(options);
                });
                if (extraOptions.Any())
                    Find.WindowStack.Add(new FloatMenu(extraOptions));
            }
            else
            {
                milComp.IsMilitaryBusy();
            }
        }

        private void HandleFireSupportClick(WorldSettlementFC settlement, WorldObjectComp_SettlementMilitary milComp)
        {
            List<FloatMenuOption> list = new List<FloatMenuOption>();

            foreach (MilitaryFireSupport support in militaryFC.fireSupportDefs)
            {
                if (support.projectiles == null || support.projectiles.Count == 0)
                    continue;

                float cost = support.ReturnTotalCost(settlement);
                list.Add(new FloatMenuOption(support.name + " - $" + cost, delegate
                {
                    if (support.ReturnTotalCost(settlement) <=
                        MilitaryFC.CalculateFireSupportBudget(settlement.settlementMilitaryLevel))
                    {
                        if (settlement.BuildingsComp?.HasBuilding(BuildingFCDefOf.artilleryOutpost) == true)
                        {
                            if (milComp.artilleryTimer <= Find.TickManager.TicksGame)
                            {
                                if (PaymentUtil.GetSilver() >= cost)
                                {
                                    MilitaryDeploymentUtil.FireSupport(settlement, support);
                                }
                                else
                                {
                                    Messages.Message("FCNotEnoughSilverFireSupport".Translate(),
                                        MessageTypeDefOf.RejectInput);
                                }
                            }
                            else
                            {
                                Messages.Message("FCFireSupportCooldown".Translate(
                                    (milComp.artilleryTimer - Find.TickManager.TicksGame).ToStringTicksToDays()),
                                    MessageTypeDefOf.RejectInput);
                            }
                        }
                        else
                        {
                            Messages.Message("FCRequiresArtilleryOutpost".Translate(),
                                MessageTypeDefOf.RejectInput);
                        }
                        Find.WindowStack.TryRemove(this);
                    }
                    else
                    {
                        Messages.Message("FCRequiresHigherMilLevel".Translate(),
                            MessageTypeDefOf.RejectInput);
                    }
                }));
            }

            if (!list.Any())
                list.Add(new FloatMenuOption("FCNoFireSupportsMade".Translate(), delegate { }));

            Find.WindowStack.Add(new Searchable_FloatMenu(list));
        }

        private List<FloatMenuOption> DeploymentOptions(WorldSettlementFC settlement)
        {
            /* Morale lockout: present a single disabled option explaining why deploys are blocked. */
            if (settlement is object && settlement.TryGetSquadDeploymentBlock(out string lockReason))
                return new List<FloatMenuOption> { new FloatMenuOption(lockReason, null) };

            MercenarySquadFC primary = settlement?.FirstAvailableStationedSquad
                                    ?? settlement?.PrimaryStationedSquad;
            int cost = primary.DeploymentCost();
            string costSuffix = cost > 0 ? " ($" + cost + ")" : "";
            return new List<FloatMenuOption>
            {
                new FloatMenuOption("FCWalkIntoMapDeploymentOption".Translate() + costSuffix, delegate
                {
                    MilitaryDeploymentUtil.CallinAlliedForces(settlement, false);
                }),
                DropPodDeploymentOption(settlement)
            };
        }

        /// <summary>Same shape as <see cref="DeploymentOptions"/> but routes to a specific squad
        /// via <see cref="MilitaryDeploymentUtil.CallinAlliedForces"/>'s overrideSquad parameter. Used by
        /// the per-slot Deploy button on the 1+N settlement card layout.</summary>
        private List<FloatMenuOption> SquadDeploymentOptions(WorldSettlementFC settlement, MercenarySquadFC squad)
        {
            /* Morale lockout: present a single disabled option explaining why deploys are blocked. */
            if (settlement is object && settlement.TryGetSquadDeploymentBlock(out string lockReason))
                return new List<FloatMenuOption> { new FloatMenuOption(lockReason, null) };

            int cost = squad.DeploymentCost();
            string costSuffix = cost > 0 ? " ($" + cost + ")" : "";

            List<FloatMenuOption> opts = new List<FloatMenuOption>();
            opts.Add(new FloatMenuOption("FCWalkIntoMapDeploymentOption".Translate() + costSuffix,
                delegate { MilitaryDeploymentUtil.CallinAlliedForces(settlement, false, squad); }));

            bool medievalOnly = FCSettings.medievalTechOnly;
            if (!medievalOnly && (FactionCache.TechTransportPods?.IsFinished ?? false))
            {
                opts.Add(new FloatMenuOption("FCDropPodDeploymentOption".Translate() + costSuffix,
                    delegate { MilitaryDeploymentUtil.CallinAlliedForces(settlement, true, squad); }));
            }
            else
            {
                opts.Add(new FloatMenuOption(
                    "FCDropPodDeploymentOption".Translate() + (medievalOnly
                        ? "FCDropPodDeploymentOptionUnavailableReasonMedieval".Translate()
                        : "FCDropPodDeploymentOptionUnavailableReasonTech".Translate(
                            FactionCache.TechTransportPods?.label ?? "FCErrorDropPodResearchCouldNotBeFound".Translate())),
                    null));
            }
            return opts;
        }

        private FloatMenuOption DropPodDeploymentOption(WorldSettlementFC settlement)
        {
            MercenarySquadFC primary = settlement?.FirstAvailableStationedSquad
                                    ?? settlement?.PrimaryStationedSquad;
            int cost = primary.DeploymentCost();
            string costSuffix = cost > 0 ? " ($" + cost + ")" : "";

            bool medievalOnly = FCSettings.medievalTechOnly;
            if (!medievalOnly && (FactionCache.TechTransportPods?.IsFinished ?? false))
            {
                return new FloatMenuOption("FCDropPodDeploymentOption".Translate() + costSuffix,
                    delegate { MilitaryDeploymentUtil.CallinAlliedForces(settlement, true); });
            }

            return new FloatMenuOption(
                "FCDropPodDeploymentOption".Translate() + (medievalOnly
                    ? "FCDropPodDeploymentOptionUnavailableReasonMedieval".Translate()
                    : "FCDropPodDeploymentOptionUnavailableReasonTech".Translate(
                        FactionCache.TechTransportPods?.label ??
                        "FCErrorDropPodResearchCouldNotBeFound".Translate())), null);
        }

        private void OpenMilitaryWindow(MilitaryWindow content, string title)
        {
            Window toRemove = Find.WindowStack.Windows.FirstOrDefault(
                w => w is FCWindow_Military existing &&
                     existing.GetMilitaryWindow().GetType() == content.GetType());

            if (toRemove != null)
            {
                toRemove.Close();
            }

            Find.WindowStack.Add(new FCWindow_Military(content, title));
        }

        private static Color ColorForPowerStatus(SettlementPowerStatus status)
        {
            switch (status)
            {
                case SettlementPowerStatus.UnderAttack: return AccentUtil.MilUnderAttack;
                case SettlementPowerStatus.AllBusy: return new Color(1f, 0.85f, 0.4f);
                case SettlementPowerStatus.Ghost: return new Color(1f, 0.85f, 0.4f);
                case SettlementPowerStatus.NoMilitary: return Color.gray;
                default: return Color.white;
            }
        }

        private static string TooltipForPowerStatus(SettlementPowerStatus status,
            WorldSettlementFC settlement, List<MercenarySquadFC> stationed)
        {
            switch (status)
            {
                case SettlementPowerStatus.UnderAttack:
                    return "FCMilPowerTipUnderAttack".Translate();
                case SettlementPowerStatus.Squad:
                    MercenarySquadFC strongest = null;
                    double bestLevel = -1;
                    for (int i = 0; i < stationed.Count; i++)
                    {
                        MercenarySquadFC s = stationed[i];
                        if (s is null || !s.IsAvailable) continue;
                        double lvl = SquadPowerRegistry.Resolve(s).militaryLevel;
                        if (lvl > bestLevel) { strongest = s; bestLevel = lvl; }
                    }
                    return "FCMilPowerTipSquad".Translate(strongest?.DisplayName ?? "?");
                case SettlementPowerStatus.AllBusy:
                    return "FCMilPowerTipAllBusy".Translate();
                case SettlementPowerStatus.Ghost:
                    return "FCMilPowerTipGhost".Translate();
                case SettlementPowerStatus.NoMilitary:
                    return "FCMilPowerTipNoMilitary".Translate();
            }
            return "";
        }

        // ===== PRISONERS TAB =====

        private void DrawPrisonersTab(Rect rect)
        {
            float x = rect.x;
            float y = rect.y;
            float width = rect.width;

            // --- Header: faction icon + name label + workload buttons (matches Military tab style) ---
            float buttonWidth = 210f;
            float buttonHeight = 35f;
            bool hasSettlements = faction.settlements?.Count > 0;
            float bx = hasSettlements ? rect.xMax - buttonWidth * 2 - margin : rect.xMax - margin;

            Rect iconRect = new Rect(x + margin, y + margin, buttonHeight, buttonHeight);
            Widgets.ButtonImage(iconRect, faction.factionIcon);

            Rect labelBox = new Rect(iconRect.xMax + margin, y + margin, bx - iconRect.xMax - (margin * 2), buttonHeight);
            Rect labelTextBox = new Rect(labelBox.x + margin, labelBox.y, labelBox.width - (margin * 2), labelBox.height);

            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;
            Color origColor = GUI.color;

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.DrawHighlight(labelBox);
            Widgets.Label(labelTextBox, faction.name ?? "");

            if (hasSettlements)
            {
                string defLabel = "FCSetDefaultWorkload".Translate() + ": " +
                                  PrisonerUtil.WorkloadLabel(faction.defaultPrisonerWorkload);
                if (Widgets.ButtonTextSubtle(new Rect(bx, y + margin, buttonWidth, buttonHeight), defLabel))
                    PrisonerUtil.OpenFactionDefaultWorkloadFloatMenu(faction);
                bx += buttonWidth;

                if (Widgets.ButtonTextSubtle(new Rect(bx, y + margin, buttonWidth, buttonHeight),
                                             "FCBulkSetWorkload".Translate()))
                    PrisonerUtil.OpenFactionBulkSetWorkloadFloatMenu(faction);
            }

            y += buttonHeight + margin * 2;

            // --- Tally prisoners across all settlements ---
            int totalPrisoners = 0;
            for (int i = 0; i < faction.settlements.Count; i++)
            {
                totalPrisoners += faction.settlements[i].PrisonerComp?.prisonerList?.Count ?? 0;
            }

            float tableY = y;
            float tableH = rect.yMax - tableY - margin;
            if (tableH <= 0f)
            {
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;
                return;
            }

            // Empty state
            if (totalPrisoners == 0)
            {
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(new Rect(rect.x, tableY + tableH * 0.35f, rect.width, 40f),
                    "FCNoPrisonersFaction".Translate());
                GUI.color = origColor;
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;
                return;
            }

            // --- Scrollable grouped list ---
            const float pad = 4f;
            const float sectionHeaderH = 32f;
            const float rowGap = 1f;
            const float sectionGap = 6f;
            const float prisonerRowIndent = 16f;
            const float cardGap = 6f;

            float innerX = x + margin + pad;
            float innerW = width - (margin + pad) * 2f;

            float contentH = 0f;
            for (int i = 0; i < faction.settlements.Count; i++)
            {
                WorldSettlementFC ss = faction.settlements[i];
                int count = ss.PrisonerComp?.prisonerList?.Count ?? 0;
                if (count == 0) continue;
                contentH += sectionHeaderH + sectionGap;
                if (!collapsedPrisonerSections.Contains(ss.ID))
                {
                    int rows = Mathf.CeilToInt(count / 2f);
                    contentH += rows * (PrisonerUtil.CompactRowHeight + rowGap);
                }
            }

            Rect viewRect = new Rect(innerX, tableY, innerW, tableH);
            Rect scrollRect = ScrollUtil.BeginScrollView(viewRect, ref prisonersScroll, contentH);

            float cy = 0f;
            for (int i = 0; i < faction.settlements.Count; i++)
            {
                WorldSettlementFC s = faction.settlements[i];
                List<FCPrisoner> sList = s.PrisonerComp?.prisonerList;
                if (sList is null || sList.Count == 0) continue;

                bool collapsed = collapsedPrisonerSections.Contains(s.ID);

                /* Section panel — dark overlay spanning the header + all card rows (if expanded).
                 * Drawn first so the lighter header highlight and per-card highlights layer on top. */
                float sectionH = sectionHeaderH;
                if (!collapsed)
                {
                    int rows = Mathf.CeilToInt(sList.Count / 2f);
                    sectionH += rows * (PrisonerUtil.CompactRowHeight + rowGap);
                }
                Widgets.DrawBoxSolid(new Rect(0f, cy, scrollRect.width, sectionH), ColorUtil.Gray1);

                // Section header: light highlight band + accent + name (clickable) + inline workload buttons + count badge (collapse toggle)
                Color settlementAccent = AccentUtil.GetSettlementAccent(s);
                Rect headerBoxRect = new Rect(0f, cy, scrollRect.width, sectionHeaderH);
                Widgets.DrawBoxSolid(headerBoxRect, ColorUtil.Gray3);
                Widgets.DrawBoxSolid(new Rect(0f, cy, PrisonerUtil.AccentWidth, sectionHeaderH), settlementAccent);

                float headerContentX = PrisonerUtil.AccentWidth + 6f;
                const float countColW = 90f;
                const float sectionBtnW = 160f;
                const float sectionBtnGap = 4f;
                const float sectionBtnH = 26f;
                float sectionBtnY = cy + (sectionHeaderH - sectionBtnH) / 2f;

                float countRectX = scrollRect.width - countColW - 4f;
                Rect bulkBtnRect = new Rect(countRectX - sectionBtnGap - sectionBtnW, sectionBtnY, sectionBtnW, sectionBtnH);
                Rect defaultBtnRect = new Rect(bulkBtnRect.x - sectionBtnGap - sectionBtnW, sectionBtnY, sectionBtnW, sectionBtnH);

                Rect nameRect = new Rect(headerContentX, cy, defaultBtnRect.x - headerContentX - 4f, sectionHeaderH);

                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = settlementAccent;
                Widgets.Label(nameRect, s.Name);
                GUI.color = origColor;

                if (Mouse.IsOver(nameRect))
                    Widgets.DrawHighlight(nameRect);
                if (Widgets.ButtonInvisible(nameRect))
                    Find.WindowStack.Add(new SettlementWindowFc(s));

                /* Settlement-level workload buttons inline in the section header - section is
                 * only drawn when sList.Count > 0, so bulk-set is always meaningful. */
                WorldObjectComp_SettlementPrisoners sComp = s.PrisonerComp;
                if (sComp is object)
                {
                    string sDefLabel = sComp.hasDefaultWorkloadOverride
                        ? "FCSetDefaultWorkloadShort".Translate() + ": " + PrisonerUtil.WorkloadLabel(sComp.defaultWorkloadOverride)
                        : "FCSetDefaultWorkloadShort".Translate() + ": " + "FCFromFaction".Translate()
                            + " (" + PrisonerUtil.WorkloadLabel(sComp.GetEffectiveDefaultWorkload()) + ")";
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    if (UIUtil.ButtonFlat(defaultBtnRect, sDefLabel))
                        sComp.OpenDefaultWorkloadFloatMenu();
                    if (UIUtil.ButtonFlat(bulkBtnRect, "FCBulkSetWorkloadShort".Translate()))
                        sComp.OpenBulkSetWorkloadFloatMenu();
                }

                Rect countRect = new Rect(countRectX, cy, countColW, sectionHeaderH);
                if (Mouse.IsOver(countRect))
                    Widgets.DrawHighlight(countRect);
                if (Widgets.ButtonInvisible(countRect))
                {
                    if (collapsed) collapsedPrisonerSections.Remove(s.ID);
                    else collapsedPrisonerSections.Add(s.ID);
                    collapsed = !collapsed;
                }
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = Color.gray;
                Widgets.Label(countRect, "(" + sList.Count + ") " + (collapsed ? "▶" : "▼"));
                GUI.color = origColor;

                cy += sectionHeaderH;

                if (!collapsed)
                {
                    /* 2-card grid with checkerboard highlight: (row + col) parity.
                     * Row cursor advances only after the right card (or after a final odd-left card). */
                    float cardW = (scrollRect.width - prisonerRowIndent - cardGap) / 2f;
                    for (int j = 0; j < sList.Count; j++)
                    {
                        bool isLeft = (j % 2) == 0;
                        float cardX = isLeft ? prisonerRowIndent : prisonerRowIndent + cardW + cardGap;
                        Rect rowBox = new Rect(cardX, cy, cardW, PrisonerUtil.CompactRowHeight);
                        int checker = (j / 2) + (j % 2);  // (0,0)/(1,1)=highlight, (0,1)/(1,0)=plain
                        PrisonerUtil.DrawPrisonerRowCompact(rowBox, sList[j], s, checker, null);
                        if (!isLeft || j == sList.Count - 1)
                        {
                            cy += PrisonerUtil.CompactRowHeight + rowGap;
                        }
                    }
                }

                cy += sectionGap;
            }

            ScrollUtil.EndScrollView();

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

    }
}
