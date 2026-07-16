using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public enum SettlementStatType
    {
        MilitaryLevel,
        Happiness,
        Loyalty,
        Unrest,
        Prosperity
    }

    public enum SettlementButtonType
    {
        Upgrade,
        SpecialActions,
        Prisoners,
        Military,
        Delete
    }

    public sealed class SettlementWindowFc : Window
    {
        public override Vector2 InitialSize
        {
            get { return new Vector2(1300f, 645f); }
        }


        //UI STUFF
        public const int ScrollSpacing = 45;
        public const int ScrollHeight = 315;

        private const int margin = 5;
        private const int smallMargin = 3;

        private int maxScroll;
        private FactionFC factionfc;

        // Building UI values
        private const int buildingSpacing = margin;//15;
        private const int buildingBoxSide = 72;

        private const int constructionListItemLabelHeight = 15;
        private const int constructionListProgressBarHeight = 10;
        private const int constructionListItemHeight = (smallMargin * 4) + (constructionListItemLabelHeight * 2) + constructionListProgressBarHeight; // 4 * smallMargin + 2 * label height + progress bar height
        private const int constructionListIconHeight = constructionListItemLabelHeight * 2 + smallMargin;

        private const int buildingSpacingFromSide = margin; //15; // (494 - (spacing + boxSide) * elementsPerRow) / 2;

        private const int scrollSpacing = (int)ScrollUtil.ScrollbarWidth + 1;

        // Production resource table row heights (shared by measurement and drawing)
        private const float resourceRowHeight = 25f;
        private const float resourceHeaderRowHeight = 25f;

        // UI State
        private int overviewTab = 0;
        private int titheTab = 0;

        // Tithe buffers
        private List<string> titheBuffers = new List<string>();
        private int currentDictSize = 0;

        // Comps with overview tabs
        private List<ISettlementWindowOverview> overviews = new List<ISettlementWindowOverview>();
        private Color accentColor;
        private Color highlightColor;

        public override void PreOpen()
        {
            base.PreOpen();
            maxScroll = (settlement.Resources.Count * ScrollSpacing) - ScrollHeight;
            factionfc = FindFC.FactionComp;
            Color baseColor = settlement.settlementDef.accentColor ?? Color.white;
            accentColor = baseColor * Color.gray;
            highlightColor = baseColor * Color.white;

            foreach (WorldObjectComp comp in settlement.AllComps)
            {
                ISettlementWindowOverview overview = comp as ISettlementWindowOverview;
                if (!(overview is null))
                {
                    if (!overview.ShouldShowOverviewTab(settlement)) continue;
                    overview.PreOpenWindow(settlement);
                    overviews.Add(overview);
                    overviewTabs.Add(overview.OverviewTabName());
                }
            }
        }
        public override void PostClose()
        {
            base.PostClose();

            foreach (ISettlementWindowOverview overview in overviews)
            {
                overview.PostCloseWindow();
            }
        }

        private List<string> overviewTabs = new List<string>
        {
            "FCOverview".Translate(),
            "FCTithing".Translate()
        };

        private static readonly SettlementStatType[] stats =
        {
            SettlementStatType.MilitaryLevel,
            SettlementStatType.Happiness,
            SettlementStatType.Loyalty,
            SettlementStatType.Unrest,
            SettlementStatType.Prosperity
        };

        private static readonly SettlementButtonType[] mainButtons =
        {
            SettlementButtonType.Upgrade,
            SettlementButtonType.SpecialActions,
            SettlementButtonType.Prisoners,
            SettlementButtonType.Military
        };

        private WorldSettlementFC settlement; //Don't expose

        public SettlementWindowFc(WorldSettlementFC settlement)
        {
            if (settlement == null)
            {
                Close();
                return;
            }

            this.settlement = settlement;
            forcePause = false;
            draggable = true;
            doCloseX = true;
            preventCameraMotion = false;
        }


        public override void DoWindowContents(Rect inRect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            float validWidth = InitialSize.x - (Margin * 2);
            float validHeight = InitialSize.y - (Margin * 2);

            float leftWidth = 150f;
            float rightWidth = 465f;
            float centerWidth = validWidth - leftWidth - rightWidth - (margin * 2);

            Rect headerBox = new Rect(inRect.x, inRect.y, leftWidth + centerWidth + margin, 30 + (margin * 2) + 60);
            Rect leftBox = new Rect(inRect.x, headerBox.yMax + (margin * 2), leftWidth, validHeight - headerBox.height - (margin * 2));
            Rect centerBox = new Rect(leftBox.xMax + margin, headerBox.yMax + (margin * 2), centerWidth, validHeight - headerBox.height - (margin * 2));
            Rect rightBox = new Rect(centerBox.xMax + margin, inRect.y, rightWidth, validHeight);

            DrawCenterHeader(headerBox);
            DrawLeftInfo(leftBox);
            DrawCenterInfo(centerBox);
            DrawRightInfo(rightBox);

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        /* Left side overview */
        private void DrawLeftInfo(Rect boundingBox)
        {
            Rect topBox = new Rect(boundingBox.x, boundingBox.y, boundingBox.width, boundingBox.height / 2f);
            Rect topBoxInner = new Rect(topBox.x + margin, topBox.y + margin, topBox.width - (margin * 2), topBox.height - (margin * 2));
            Rect botBox = new Rect(topBox.x, topBox.yMax, topBox.width, topBox.height);
            Rect botBoxInner = new Rect(botBox.x + margin, botBox.y + margin, botBox.width - (margin * 2), botBox.height - (margin * 2));

            UIUtil.DrawColoredBox(topBox, accentColor);
            UIUtil.DrawColoredBox(botBox, accentColor);
            DrawSettlementStats(topBoxInner);
            DrawMainButtons(botBoxInner);
        }

        private void DrawCenterInfo(Rect boundingBox)
        {
            Color? tabBaseColor = accentColor != Color.gray ? (Color?)accentColor : null;
            int newTab = UIUtil.DrawTabRow(boundingBox, overviewTabs, overviewTab, out Rect contentRect,
                baseColor: tabBaseColor, borderColor: accentColor);
            if (newTab != overviewTab)
            {
                overviewTab = newTab;
                if (overviewTab >= 2 && (overviewTab - 2) < overviews.Count)
                {
                    overviews[overviewTab - 2].OnTabSwitch();
                }
            }
            Rect infobox = contentRect.ContractedBy(margin);
            DrawOverview(infobox);
        }
        private void DrawOverview(Rect boundingBox)
        {
            if (overviewTab == 0)
            {
                DrawBasicOverview(boundingBox);
            }
            else if (overviewTab == 1)
            {
                DrawTitheOverview(boundingBox);
            }
            else if (overviews.Count > 0 && overviewTab >= 2 && (overviewTab - 2) < overviews.Count)
            {
                ISettlementWindowOverview overview = overviews[overviewTab - 2];
                overview.DrawOverviewTab(boundingBox);
            }
        }
        private void DrawCenterHeader(Rect boundingBox)
        {
            /* Settlement name on top */
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect nameRect = new Rect(boundingBox.x + margin, boundingBox.y + margin, boundingBox.width - (margin * 2 + 44), 30);
            UIUtil.SettlementLabel(nameRect, settlement);
            //Draw codex button
            Rect codexBtnRect = new Rect(nameRect.xMax + margin, boundingBox.y + margin, 20, 20);
            CodexTooltips.DrawCodexButton(codexBtnRect);
            //Draw name settings button
            Rect configRect = new Rect(codexBtnRect.xMax + margin, boundingBox.y + margin, 20, 20);
            if (Widgets.ButtonImage(configRect, TexLoad.iconCustomize))
            {
                //if click faction customize button
                Find.WindowStack.Add(new SettlementCustomizeWindowFc(settlement));
            }
            // Just used for alignment. Can maybe use this box to draw some background art based on the settlement's biome. Kinda like stellaris, maybe
            Rect infoBox = new Rect(boundingBox.x, nameRect.yMax, boundingBox.width, boundingBox.height - (nameRect.height + margin * 2));

            /* Town level */
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Rect levelBoundingBox = new Rect(infoBox.x, infoBox.y + margin, 60, 60);
            //gotta love aligning text
            Rect levelBox = new Rect(levelBoundingBox.x + 14, levelBoundingBox.y + 14, 30, 30);
            Widgets.DrawShadowAround(levelBox);
            UIUtil.DrawColoredHighlight(levelBoundingBox, highlightColor);
            UIUtil.DrawColoredBox(levelBoundingBox, accentColor);
            UIUtil.ClampedLabel(levelBox, settlement.settlementLevel.ToString());

            // Draw settlement type, basic description (from def), and location text
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            float rightSideWidth = boundingBox.width - (margin + levelBoundingBox.width);
            Rect typeBox = new Rect(levelBoundingBox.xMax + margin, levelBoundingBox.y, rightSideWidth, levelBoundingBox.height / 2);
            Rect typeTextBox = new Rect(typeBox.x + margin, typeBox.y, (typeBox.width - (margin * 2)) / 2f, typeBox.height);
            Rect foundTextBox = new Rect(typeTextBox.xMax, typeTextBox.y, typeTextBox.width, typeTextBox.height);
            Rect basicDescBox = new Rect(typeBox.x, typeBox.yMax, rightSideWidth * 0.4f, levelBoundingBox.height / 2);
            Rect basicDescTextBox = new Rect(basicDescBox.x + margin, basicDescBox.y, basicDescBox.width - (margin * 2), basicDescBox.height);
            Rect locBox = new Rect(basicDescBox.xMax + margin, typeBox.yMax, (rightSideWidth * 0.6f) - margin, levelBoundingBox.height / 2);
            Rect locTextBox = new Rect(locBox.x + margin, locBox.y, locBox.width - (margin * 2), locBox.height);
            UIUtil.DrawColoredHighlight(typeBox, highlightColor);
            UIUtil.ClampedLabel(typeTextBox, settlement.settlementDef.LabelCap);
            Text.Anchor = TextAnchor.MiddleRight;
            UIUtil.ClampedLabel(foundTextBox, "FCFoundedOn".Translate(settlement.GetFoundingDate()));
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Tiny;
            UIUtil.ClampedLabel(basicDescTextBox, TextUtil.GetTownTitle(settlement));
            UIUtil.DrawColoredVerticalLine(basicDescBox.xMax, basicDescBox.y + margin, basicDescBox.height - (margin * 2), accentColor);
            // locationText is derived in SettlementTypeExtension.GetLocationText, which now resolves through the FCSettlementLocation format key.
            UIUtil.ClampedLabel(locTextBox, settlement.locationText);
        }
        private void DrawBasicOverview(Rect boundingBox)
        {
            float bottomHeight = (buildingBoxSide * 3) + (buildingSpacing * 4) + 30f;
            Rect topRect = new Rect(boundingBox.x, boundingBox.y, boundingBox.width, (boundingBox.height - margin - bottomHeight));
            Rect botRect = new Rect(boundingBox.x, topRect.yMax + margin, boundingBox.width, bottomHeight);

            DrawBasicOverviewTop(topRect);
            DrawBasicOverviewBottom(botRect);
        }

        private void DrawBasicOverviewTop(Rect boundingBox)
        {
            // Maybe we'll put more than just the description here. Who knows?
            // A biome-fitting image might be cool, kind of like Stellaris
            DrawDescription(boundingBox);
        }

        private void DrawBasicOverviewBottom(Rect boundingBox)
        {
            float scrollMargin = ((settlement.BuildingsComp?.Buildings.Count ?? 0) > 12) ? scrollSpacing : 0;
            float buildingBoxWidth = Math.Max(boundingBox.x * 0.7f, (buildingSpacingFromSide * 2) + (buildingBoxSide * 4) + (buildingSpacing * 3) + scrollMargin);
            float constructionBoxWidth = boundingBox.width - buildingBoxWidth;

            Rect leftBox = new Rect(boundingBox.x, boundingBox.y, constructionBoxWidth, boundingBox.height);
            Rect rightBox = new Rect(leftBox.xMax, leftBox.y, buildingBoxWidth, boundingBox.height);

            if (settlement.BuildingsComp == null)
            {
                DrawFacilities(rightBox);
                return;
            }

            int numUnderConstruction = settlement.BuildingsComp.GetUnderConstructionBuildings().Count + (settlement.IsUpgrading ? 1 : 0);
            DrawConstructionBox(leftBox, numUnderConstruction, settlement.BuildingsComp.GetUnderConstructionBuildings());
            DrawFacilities(rightBox);
        }
        private void DrawTitheOverview(Rect boundingBox)
        {
            Color origColor = GUI.color;
            List<ResourceFC> resources = settlement.GetTitheableResources();
            int numResources = resources.Count;
            if (numResources == 0) return;
            if (titheTab >= numResources) titheTab = 0;
            /* Draw resource tabs on the left */
            float tabWidth = 25f;
            float tabHeight = boundingBox.height / numResources;
            Rect chosenRect = new Rect();
            for (int i = 0; i < numResources; i++)
            {
                Rect tabBox = new Rect(boundingBox.x, boundingBox.y + (tabHeight * i), tabWidth, tabHeight);
                float imgSize = Math.Min(tabWidth, tabHeight);
                Rect iconBox = new Rect(tabBox.x + 2f + (tabWidth - imgSize) / 2f, tabBox.y + (tabHeight - imgSize) / 2f, imgSize, imgSize);
                if (UIUtil.ButtonFlat(tabBox, "", highlighted: titheTab == i))
                {
                    titheTab = i;
                    UpdateTitheDictBuffers(resources[i]);
                }
                Text.Font = GameFont.Small;
                Widgets.Label(iconBox, new GUIContent(resources[i].def.Icon));
                // Resource color accent
                Widgets.DrawBoxSolid(new Rect(tabBox.x, tabBox.y, 3f, tabBox.height), resources[i].def.color);
                TooltipHandler.TipRegion(tabBox, resources[i].def.LabelCap);
                // Tithe budget status indicator on right edge
                double titheIncome = resources[i].GetTitheIncome();
                if (titheIncome > 0)
                {
                    Color alertColor;
                    if (resources[i].tithesPaused)
                    {
                        alertColor = Color.grey;
                    }
                    else
                    {
                        float ratio = (float)(resources[i].titheTotalValue / titheIncome);
                        if (ratio >= 1f)
                            alertColor = Color.red;
                        else if (ratio >= 0.5f)
                            alertColor = Color.yellow;
                        else
                            alertColor = Color.green;
                    }

                    float alertSize = 3f;
                    Widgets.DrawBoxSolid(new Rect(tabBox.xMax - alertSize - 2f, tabBox.y + 5f, alertSize, alertSize), alertColor);
                }
                if (titheTab == i)
                {
                    chosenRect = tabBox;
                }
            }
            UIUtil.DrawTabDecoratorVerticalLeft(chosenRect, boundingBox, resources[titheTab].def.color);

            ResourceFC titheRes = resources[titheTab];

            /* Calculate heights */
            float bodyX = boundingBox.x + tabWidth + margin;
            float bodyWidth = boundingBox.width - tabWidth - margin;
            float headerHeight = 30 + margin + (23f * 3);//60f;
            float footerHeight = 23f;
            float bodyHeight = boundingBox.height - headerHeight - footerHeight - (margin * 2);
            float randomboxHeight = 0f;
            if (titheRes.hasRandomTithe)
            {
                randomboxHeight = bodyHeight / 2f;
            }
            else
            {
                randomboxHeight = 23f * 2;
            }
            float scrollboxHeight = bodyHeight - randomboxHeight;

            /* Header box (contains pause button — always interactive) */
            Rect headerBox = new Rect(bodyX, boundingBox.y, bodyWidth, headerHeight);
            DrawTitheHeaderBox(headerBox, titheRes);

            /* Dim content below header when paused */
            if (titheRes.tithesPaused)
            {
                GUI.enabled = false;
            }

            /* Scrollbox */
            Rect scrollBox = new Rect(bodyX, headerBox.yMax + margin, bodyWidth, scrollboxHeight);
            DrawTitheScrollBox(scrollBox, titheRes);

            /* Random tithe box */
            Rect titheBox = new Rect(bodyX, scrollBox.yMax, bodyWidth, randomboxHeight);
            DrawTitheRandomBox(titheBox, titheRes);

            /* Footer box */
            Rect footerBox = new Rect(bodyX, titheBox.yMax + margin, bodyWidth, footerHeight);
            DrawTitheFooterBox(footerBox, titheRes);
            GUI.enabled = true;
            GUI.color = origColor;

            /* Blocking overlay when paused — covers content below header */
            if (titheRes.tithesPaused)
            {
                float overlayY = boundingBox.y + 30f + margin;
                Rect contentArea = new Rect(bodyX, overlayY, bodyWidth, boundingBox.yMax - overlayY);
                Widgets.DrawBoxSolid(contentArea, new Color(0f, 0f, 0f, 0.75f));
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.ClampedLabel(contentArea, "FCTithesPaused".Translate());
            }
        }
        private void DrawTitheHeaderBox(Rect boundingBox, ResourceFC res)
        {
            float pauseBtnWidth = 90f;
            Rect iconBox = new Rect(boundingBox.x, boundingBox.y, 30f, 30f);
            Rect labelHighlight = new Rect(iconBox.xMax + margin, boundingBox.y, boundingBox.width - margin - iconBox.width - pauseBtnWidth - margin, 30f);
            Rect labelText = new Rect(labelHighlight.x + smallMargin, labelHighlight.y + smallMargin, labelHighlight.width - (smallMargin * 2), labelHighlight.height - (smallMargin * 2));
            Rect pauseBtn = new Rect(labelHighlight.xMax + margin, boundingBox.y + 3f, pauseBtnWidth, 27f);

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.DrawHighlight(iconBox);
            Widgets.Label(iconBox, new GUIContent(res.def.Icon));
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.DrawHighlight(labelHighlight);
            UIUtil.ClampedLabel(labelText, res.def.LabelCap);

            /* Pause button */
            Text.Font = GameFont.Tiny;
            string buttonText = res.tithesPaused ? "FCUnPauseTithes".Translate() : "FCPauseTithes".Translate();
            if (UIUtil.ButtonFlat(pauseBtn, buttonText, highlighted: res.tithesPaused))
            {
                res.SetTithesPaused(!res.tithesPaused);
            }
            TooltipHandler.TipRegion(pauseBtn, "FCPauseTithesDesc".Translate());

            Rect iconAccent = new Rect(iconBox.x, iconBox.y, iconBox.width, 3f);
            Rect labelAccent = new Rect(labelHighlight.x, labelHighlight.y, labelHighlight.width, 3f);
            Rect btnAccent = new Rect(pauseBtn.x, pauseBtn.y - 3f, pauseBtn.width, 3f);
            Widgets.DrawBoxSolid(iconAccent, res.def.color);
            Widgets.DrawBoxSolid(labelAccent, res.def.color);
            Widgets.DrawBoxSolid(btnAccent, res.def.color);

            /* Info boxes */
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            float rowHeight = 23f;
            float labelHeight = rowHeight - (smallMargin * 2);

            double extBudget = res.DailyExternalTitheBudget;
            bool hasInjection = extBudget > 0.01;
            double titheMult = res.GetTitheValueMultiplier();
            bool showMult = Math.Abs(titheMult - 1.0) > 0.001;

            Rect titheModBox = new Rect(boundingBox.x, iconBox.yMax + margin, (boundingBox.width - margin) / 2f, rowHeight * 3f);
            Rect prodBox = new Rect(titheModBox.xMax + margin, iconBox.yMax + margin, (boundingBox.width - margin) / 2f, rowHeight);
            float budgetY = hasInjection ? prodBox.yMax + rowHeight : prodBox.yMax;
            Rect budgetBox = new Rect(titheModBox.xMax + margin, budgetY, (boundingBox.width - margin) / 2f, rowHeight);

            /* Tithe modifier info */
            Rect titheRow1 = new Rect(titheModBox.x, titheModBox.y, titheModBox.width, rowHeight);
            Rect trow1label = new Rect(titheRow1.x + smallMargin, titheRow1.y + smallMargin, (titheRow1.width - (smallMargin * 2)), labelHeight);
            Rect titheRow2 = new Rect(titheModBox.x + (margin * 2), titheRow1.yMax, titheModBox.width - (margin * 2), rowHeight);
            Rect trow2label = new Rect(titheRow2.x + smallMargin, titheRow2.y + smallMargin, (titheRow2.width - (smallMargin * 2)) * 0.75f, labelHeight);
            Rect trow2num = new Rect(trow2label.xMax, trow2label.y, (titheRow2.width - (smallMargin * 2)) * 0.25f, labelHeight);
            Rect titheRow3 = new Rect(titheRow2.x, titheRow2.yMax, titheRow2.width, rowHeight);
            Rect trow3label = new Rect(titheRow3.x + smallMargin, titheRow3.y + smallMargin, (titheRow3.width - (smallMargin * 2)) * 0.75f, labelHeight);
            Rect trow3num = new Rect(trow3label.xMax, trow3label.y, (titheRow3.width - (smallMargin * 2)) * 0.25f, labelHeight);
            Widgets.DrawHighlight(titheModBox);
            Widgets.DrawHighlight(titheRow1);
            UIUtil.ClampedLabel(trow1label, "FCTitheModifier".Translate());
            UIUtil.ClampedLabel(trow2label, "FCPerWorker".Translate());
            Widgets.DrawHighlight(titheRow3);
            UIUtil.ClampedLabel(trow3label, "FCTotal".Translate());
            Text.Anchor = TextAnchor.MiddleRight;
            double perWorkerRaw = res.GetTitheModifierPerWorker();
            double totalWorkerRaw = res.GetTotalTitheModifierForWorkers();
            UIUtil.ClampedLabel(trow2num, Math.Round(perWorkerRaw * titheMult).ToString());
            UIUtil.ClampedLabel(trow3num, Math.Round(totalWorkerRaw * titheMult).ToString());
            if (showMult)
            {
                // Keep as TaggedString: assigning to a string here would StripTags() the colorized multiplier.
                // TipRegion's TipSignal(TaggedString) ctor calls .Resolve(), preserving the color.
                TaggedString titheTip = "FCTitheValueMultiplierTooltip".Translate(
                    Math.Round(perWorkerRaw).ToString(),
                    TextUtil.ColorizeMultiplierBonus(titheMult),
                    Math.Round(perWorkerRaw * titheMult).ToString());
                TooltipHandler.TipRegion(titheRow2, titheTip);
            }

            /* Production */
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect prodLabel = new Rect(prodBox.x + smallMargin, prodBox.y + smallMargin, (prodBox.width - (margin * 2)) * 0.75f, labelHeight);
            Rect prodnum = new Rect(prodLabel.xMax, prodLabel.y, (prodBox.width - (margin * 2)) * 0.25f, labelHeight);
            Widgets.DrawHighlight(prodBox);
            UIUtil.ClampedLabel(prodLabel, "FCTotalProd".Translate());
            Text.Anchor = TextAnchor.MiddleRight;
            double prodRaw = res.taxableProductionMarketValue;
            UIUtil.ClampedLabel(prodnum, Math.Round(prodRaw * titheMult).ToString());
            if (showMult)
            {
                TooltipHandler.TipRegion(prodBox, "FCTitheValueMultiplierTooltip".Translate(
                    Math.Round(prodRaw).ToString(), TextUtil.ColorizeMultiplierBonus(titheMult), Math.Round(prodRaw * titheMult).ToString()));
            }

            /* External Tithe Injection */
            if (hasInjection)
            {
                Rect injBox = new Rect(prodBox.x, prodBox.yMax, prodBox.width, rowHeight);
                Rect injLabel = new Rect(injBox.x + smallMargin, injBox.y + smallMargin, (injBox.width - (margin * 2)) * 0.75f, labelHeight);
                Rect injNum = new Rect(injLabel.xMax, injLabel.y, (injBox.width - (margin * 2)) * 0.25f, labelHeight);
                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.ClampedLabel(injLabel, "FCExternalTitheBudget".Translate());
                Text.Anchor = TextAnchor.MiddleRight;
                UIUtil.ClampedLabel(injNum, Math.Round(extBudget).ToString());

                StringBuilder injTip = new StringBuilder();
                foreach (WorldObjectComp comp in res.settlement.AllComps)
                {
                    if (comp is ITitheBudgetModifier modifier)
                    {
                        string desc = modifier.GetExternalTitheBudgetDesc(res);
                        if (!string.IsNullOrEmpty(desc))
                            injTip.AppendLine(desc);
                    }
                }
                if (injTip.Length > 0)
                    TooltipHandler.TipRegion(injBox, injTip.ToString().TrimEnd());
            }

            /* Tithe Budget */
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect budgetLabel = new Rect(budgetBox.x + smallMargin, budgetBox.y + smallMargin, (budgetBox.width - (margin * 2)) * 0.75f, labelHeight);
            Rect budgetnum = new Rect(budgetLabel.xMax, budgetLabel.y, (budgetBox.width - (margin * 2)) * 0.25f, labelHeight);
            Widgets.DrawMenuSection(budgetBox);
            UIUtil.ClampedLabel(budgetLabel, "FCTotalTitheBudget".Translate());
            Text.Anchor = TextAnchor.MiddleRight;
            UIUtil.ClampedLabel(budgetnum, Math.Round(res.GetTitheIncome()).ToString());
        }
        private Vector2 titheScrollBar = new Vector2();
        private void UpdateTitheDictBuffers(ResourceFC res)
        {
            titheBuffers.Clear();
            if (res != null)
            {
                foreach (TitheEntry e in res.Tithes)
                {
                    titheBuffers.Add(e.quantity.ToString());
                }
                res.storedRandomTitheBudgetBuffer = res.storedRandomTitheBudget.ToString();
            }
            currentDictSize = titheBuffers.Count;
        }
        private void KeepTitheDictBuffersUpdated(ResourceFC res)
        {
            if (res != null && currentDictSize != res.GetTitheListCount())
            {
                UpdateTitheDictBuffers(res);
            }
        }
        private void DrawTitheScrollBox(Rect boundingBox, ResourceFC res)
        {
            UIUtil.DrawColoredBox(boundingBox, accentColor);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            float rowHeight = 23f;
            Rect header = new Rect(boundingBox.x, boundingBox.y, boundingBox.width * 0.8f, rowHeight);
            Rect headerText = new Rect(header.x + margin, header.y, header.width - (margin * 2), header.height);
            Widgets.DrawHighlight(header);
            UIUtil.ClampedLabel(headerText, "FCTitheSelection".Translate());

            Rect addItemButton = new Rect(header.xMax, header.y, (boundingBox.width * 0.2f) - smallMargin, header.height);
            if (UIUtil.ClampedButtonText(addItemButton, "FCAddItem".Translate()))
            {
                Find.WindowStack.Add(new SettlementWindowFC_AddTithe(settlement, res));
            }

            KeepTitheDictBuffersUpdated(res);

            IReadOnlyList<TitheEntry> orderedTithes = res.Tithes;

            /* Compute waterline: projected full-cycle tithe budget */
            double projectedBudget = res.AccruedTitheBudget + res.GetTitheIncome() * settlement.DaysRemaining;
            double runningCost = 0.0;

            /* Doing weird box-in-a-box to try and fix some UI drawing issues */
            Rect drawBox = new Rect(boundingBox.x + margin, header.yMax, boundingBox.width - (margin * 2), boundingBox.yMax - header.yMax - margin);
            Rect selectedListBox = new Rect(drawBox.x + 2, drawBox.y + 2, drawBox.width - 4, drawBox.height - 4);
            float listHeight = orderedTithes.Count * rowHeight;
            Widgets.DrawMenuSection(selectedListBox);
            Rect innerScrollBox = ScrollUtil.BeginScrollView(selectedListBox, ref titheScrollBar, listHeight);
            for (int i = 0; i < orderedTithes.Count; i++)
            {
                TitheEntry entry = orderedTithes[i];
                ThingQualityTuple thingTuple = entry.thing;
                ThingDef iThing = thingTuple.thingDef;
                QualityCategory iQuality = thingTuple.quality;
                ThingDef iStuff = thingTuple.stuffDef;
                bool labelExtended = false;

                /* Waterline: check if this entry will be fulfilled this cycle */
                double entryCost = CraftUtil.ThingValue(thingTuple) * entry.quantity;
                bool belowWaterline = runningCost + entryCost > projectedBudget;
                runningCost += entryCost;

                Rect row = new Rect(innerScrollBox.x, innerScrollBox.y + (i * rowHeight), innerScrollBox.width, rowHeight);
                /* Up/down reorder arrows (left-most, square icons stacked vertically) */
                float arrowH = (rowHeight - 4) / 2f;          // ~9.5px each, half the row
                float arrowColW = arrowH;                      // square icons -> column == arrow height
                Rect upArrow = new Rect(row.x + margin, row.y + 2, arrowColW, arrowH);
                Rect downArrow = new Rect(row.x + margin, upArrow.yMax, arrowColW, arrowH);
                Rect icon = new Rect(downArrow.xMax + margin, row.y, rowHeight, rowHeight);
                Rect info = new Rect(icon.xMax, row.y + 2, rowHeight - 4, rowHeight - 4);
                Rect xBox = new Rect(row.xMax - margin - 20f, row.y + 2, rowHeight - 4, rowHeight - 4);
                Rect fieldBox = new Rect(xBox.x - margin - 180f, row.y + 2, 180f, rowHeight - 4);
                Rect valueLabel = new Rect(fieldBox.x - margin - 60f, row.y, 60f, rowHeight);
                Rect stuffBox = new Rect(valueLabel.x - margin - 80f, row.y + 2, 80f, rowHeight - 4);
                Rect qualityBox = new Rect(stuffBox.x - 80f, row.y + 2, 80f, rowHeight - 4);
                Rect label = new Rect(info.xMax + margin, row.y, qualityBox.x - info.xMax - margin, rowHeight);
                if (i % 2 == 0)
                {
                    Widgets.DrawHighlight(row);
                }

                /* Up/Down reorder arrows */
                if (i > 0 && Widgets.ButtonImage(upArrow, TexButton.ReorderUp))
                {
                    res.MoveTitheEntry(i, i - 1);
                    UpdateTitheDictBuffers(res);
                    break;
                }
                if (i < orderedTithes.Count - 1 && Widgets.ButtonImage(downArrow, TexButton.ReorderDown))
                {
                    res.MoveTitheEntry(i, i + 1);
                    UpdateTitheDictBuffers(res);
                    break;
                }

                Widgets.Label(icon, new GUIContent(iThing.uiIcon));
                Widgets.InfoCardButton(info, iThing);
                if (UIUtil.ClampedButtonText(xBox, "X"))
                {
                    res.RemoveTitheAt(i);
                    break;
                }
                TooltipHandler.TipRegion(xBox, "FCTitheXDesc".Translate());
                Text.Anchor = TextAnchor.MiddleLeft;

                /* Below-waterline: grey out label value */
                Color origRowColor = GUI.color;
                if (belowWaterline) GUI.color = new Color(0.5f, 0.5f, 0.5f);
                UIUtil.ClampedLabel(valueLabel, $"${Math.Round(res.TitheThingValue(thingTuple), 2)}");
                if (belowWaterline) GUI.color = origRowColor;

                QualityCategory maxQuality = QualityCategory.Legendary;
                if (CraftUtil.ThingHasQuality(iThing) && res.CanSetTitheQuality(out maxQuality))
                {
                    List<QualityCategory> categoryList = res.GetValidTitheQualities(maxQuality);
                    if (UIUtil.ClampedButtonText(qualityBox, TextUtil.GetQualityLabelCap(iQuality)))
                    {
                        int index = i; // capture for the deferred FloatMenu delegate (loop var would be stale)
                        List<FloatMenuOption> options = new List<FloatMenuOption>();
                        foreach (QualityCategory cat in categoryList)
                        {
                            options.Add(new FloatMenuOption(TextUtil.GetQualityLabelCap(cat), delegate
                            {
                                ThingQualityTuple newTuple = new ThingQualityTuple
                                {
                                    thingDef = iThing,
                                    quality = cat,
                                    stuffDef = iStuff
                                };
                                res.SetTitheThingAt(index, newTuple);
                                UpdateTitheDictBuffers(res);
                            }));
                        }
                        Find.WindowStack.Add(new FloatMenu(options));
                    }
                }
                else
                {
                    label.width += qualityBox.width;
                    labelExtended = true;
                }

                if (CraftUtil.ThingIsStuffable(iThing))
                {
                    List<ThingDef> stuffList = res.GetStuffListForThingDef(iThing);
                    if (UIUtil.ClampedButtonText(stuffBox, iStuff?.LabelCap ?? "None"))
                    {
                        int index = i; // capture for the deferred FloatMenu delegate (loop var would be stale)
                        List<FloatMenuOption> options = new List<FloatMenuOption>();
                        foreach (ThingDef stuff in stuffList)
                        {
                            options.Add(new FloatMenuOption(stuff.LabelCap, delegate
                            {
                                ThingQualityTuple newTuple = new ThingQualityTuple
                                {
                                    thingDef = iThing,
                                    quality = iQuality,
                                    stuffDef = stuff
                                };
                                res.SetTitheThingAt(index, newTuple);
                                UpdateTitheDictBuffers(res);
                            }));
                        }
                        Find.WindowStack.Add(new FloatMenu(options));
                    }
                }
                else if (labelExtended)
                {
                    label.width += stuffBox.width;
                }

                /* Item label — append won't-deliver note if below waterline */
                string itemLabelText = iThing.LabelCap;
                if (belowWaterline)
                    itemLabelText = itemLabelText + " " + "FCTitheWontThisCycle".Translate();
                string nulabel = Text.ClampTextWithEllipsis(label, itemLabelText);
                Color origLabelColor = GUI.color;
                if (belowWaterline) GUI.color = new Color(0.5f, 0.5f, 0.5f);
                UIUtil.ClampedLabel(label, nulabel);
                GUI.color = origLabelColor;
                if (nulabel != itemLabelText)
                {
                    TooltipHandler.TipRegion(label, itemLabelText);
                }

                // This seems like a *really* hacky way to handle these buffers. Seems like it'd be prone to UI jitteryness, or just general bad feel
                //   keep this in mind when testing...
                // No affordability cap: this is a priority list, so over-budget entries are allowed.
                // The waterline grey-out above shows what won't deliver this cycle; unaffordable entries persist.
                int quantity = entry.quantity;
                int oldQuantity = quantity;
                string buf = titheBuffers[i];
                Widgets.IntEntry(fieldBox, ref quantity, ref buf);
                quantity = Math.Max(0, quantity);
                buf = quantity.ToString();
                if (oldQuantity != quantity)
                {
                    res.SetTitheQuantityAt(i, quantity);
                }
                titheBuffers[i] = buf;
            }

            ScrollUtil.EndScrollView();
        }
        private Vector2 randomTitheScrollBar = new Vector2();
        private void DrawTitheRandomBox(Rect boundingBox, ResourceFC res)
        {
            Color origColor = GUI.color;
            UIUtil.DrawColoredBox(boundingBox, accentColor);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            float rowHeight = 23f;
            Rect header = new Rect(boundingBox.x, boundingBox.y, boundingBox.width, rowHeight);
            Rect headerText = new Rect(header.x + margin, header.y, header.width - (margin * 2), header.height);
            Widgets.DrawHighlight(header);
            bool hasRandom = res.hasRandomTithe;
            Widgets.CheckboxLabeled(headerText, "FCRandomTithesEnabled".Translate(), ref hasRandom, disabled: res.tithesPaused);
            if (hasRandom != res.hasRandomTithe) res.SetHasRandomTithe(hasRandom);
            TooltipHandler.TipRegion(header, "FCRandomTithesDesc".Translate());

            Rect accruedBox = new Rect(boundingBox.x, header.yMax, boundingBox.width * 0.6f, rowHeight);
            Rect accruedTextBox = new Rect(accruedBox.x + smallMargin, accruedBox.y, accruedBox.width - (smallMargin * 2), accruedBox.height);
            Rect disburseBox = new Rect(accruedBox.xMax, header.yMax, boundingBox.width - accruedBox.width, rowHeight);
            Rect disbursedTextBox = new Rect(disburseBox.x + smallMargin, disburseBox.y, disburseBox.width - (smallMargin * 2), disburseBox.height);
            UIUtil.ClampedLabel(accruedTextBox, "FCRandomTitheAccrued".Translate(res.randomTitheStock));
            bool disburse = res.disburseTitheStock;
            Widgets.CheckboxLabeled(disbursedTextBox, "FCDisburseAccruedRandomTithe".Translate(), ref disburse, disabled: res.tithesPaused);
            if (disburse != res.disburseTitheStock) res.SetDisburseTitheStock(disburse);
            TooltipHandler.TipRegion(accruedBox, "FCRandomTitheAccruedDesc".Translate());
            TooltipHandler.TipRegion(disburseBox, "FCDisburseAccruedRandomTitheDesc".Translate());

            if (res.hasRandomTithe)
            {
                float budgetRowWidth = boundingBox.width * 0.6f;
                float checkboxWidth = 55f;
                Rect budgetBox = new Rect(boundingBox.x, disburseBox.yMax, budgetRowWidth - checkboxWidth, 23f);
                Rect budgetTextBox = new Rect(budgetBox.x + margin, budgetBox.y, budgetBox.width - (margin * 2), budgetBox.height);
                if (res.autoMaxRandomTithe)
                {
                    UIUtil.ClampedLabel(budgetTextBox, "FCRandomTitheBudget".Translate() + ": " + res.randomTitheBudget);
                }
                else
                {
                    int budget = res.storedRandomTitheBudget;
                    string buffer = res.storedRandomTitheBudgetBuffer;
                    Widgets.TextFieldNumericLabeled(budgetTextBox, "FCRandomTitheBudget".Translate() + ": ", ref budget, ref buffer, 0, (float)(res.GetTitheIncome() - res.titheTotalValueNoRandom));
                    res.storedRandomTitheBudgetBuffer = buffer;
                    if (budget != res.storedRandomTitheBudget) res.SetStoredRandomTitheBudget(budget);
                }
                Rect maxCheckBox = new Rect(budgetBox.xMax, budgetBox.y, checkboxWidth, budgetBox.height);
                bool autoMax = res.autoMaxRandomTithe;
                Widgets.CheckboxLabeled(maxCheckBox, "FCAutoMaxRandomTithe".Translate(), ref autoMax, disabled: res.tithesPaused);
                TooltipHandler.TipRegion(maxCheckBox, "FCAutoMaxRandomTitheDesc".Translate());
                if (autoMax != res.autoMaxRandomTithe) res.SetAutoMaxRandomTithe(autoMax);
                Rect selectBox = new Rect(boundingBox.x + budgetRowWidth, budgetBox.y, boundingBox.width * 0.4f - margin, budgetBox.height);
                if (UIUtil.ClampedButtonText(selectBox, "FCItemSelection".Translate()))
                {
                    Find.WindowStack.Add(new SettlementWindowFC_RandomTithe(settlement, res));
                }

                List<ThingDef> selectedThings = res.GetRandomTitheFilterThings();

                /* Doing weird box-in-a-box to try and fix some UI drawing issues */
                Rect drawBox = new Rect(boundingBox.x + margin, budgetBox.yMax, boundingBox.width - (margin * 2), boundingBox.yMax - budgetBox.yMax - margin);
                Rect selectedListBox = new Rect(drawBox.x + 2, drawBox.y + 2, drawBox.width - 4, drawBox.height - 4);
                float listHeight = selectedThings.Count * rowHeight;

                Widgets.DrawMenuSection(selectedListBox);
                Rect innerScrollBox = ScrollUtil.BeginScrollView(selectedListBox, ref randomTitheScrollBar, listHeight);

                for (int i = 0; i < selectedThings.Count; i++)
                {
                    ThingDef iThing = selectedThings[i];
                    Rect row = new Rect(innerScrollBox.x, innerScrollBox.y + (i * rowHeight), innerScrollBox.width, rowHeight);
                    Rect icon = new Rect(row.x + margin, row.y, rowHeight, rowHeight);
                    Rect info = new Rect(icon.xMax, row.y + 2, rowHeight - 4, rowHeight - 4);
                    Rect xBox = new Rect(row.xMax - margin - 20f, row.y + 2, 19f, 19f);
                    Rect valueLabel = new Rect(xBox.x - margin - 60f, xBox.y, 60f, rowHeight);
                    Rect label = new Rect(info.xMax + margin, row.y, valueLabel.x - info.xMax - margin, rowHeight);

                    if (i % 2 == 0)
                    {
                        Widgets.DrawHighlight(row);
                    }
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(icon, new GUIContent(iThing.uiIcon));
                    if (UIUtil.ClampedButtonText(xBox, "X"))
                    {
                        res.SetRandomTitheFilterAllow(iThing, false);
                    }
                    Text.Anchor = TextAnchor.MiddleLeft;
                    UIUtil.ClampedLabel(label, iThing.LabelCap);
                    UIUtil.ClampedLabel(valueLabel, $"${Math.Round(iThing.BaseMarketValue, 2)}");
                    Widgets.InfoCardButton(info, iThing);
                }

                ScrollUtil.EndScrollView();
            }
        }
        private void DrawTitheFooterBox(Rect boundingBox, ResourceFC res)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;

            /* Waterline readout: selected total vs. projected full-cycle budget */
            double projectedBudget = res.AccruedTitheBudget + res.GetTitheIncome() * settlement.DaysRemaining;
            double selectedTotal = res.titheTotalValue;

            Widgets.DrawHighlight(boundingBox);
            Rect readoutLabel = new Rect(boundingBox.x + smallMargin, boundingBox.y + smallMargin,
                boundingBox.width - (smallMargin * 2), boundingBox.height - (smallMargin * 2));
            Color origColor = GUI.color;
            if (selectedTotal > projectedBudget)
                GUI.color = Color.red;
            else if (selectedTotal > projectedBudget * 0.8)
                GUI.color = Color.yellow;
            else
                GUI.color = Color.green;
            UIUtil.ClampedLabel(readoutLabel, "FCTitheSelectedVsProjected".Translate(
                Math.Round(selectedTotal), Math.Round(projectedBudget)));
            GUI.color = origColor;
        }

        //Original: 125 wide, 215ish tall
        private void DrawSettlementStats(Rect boundingBox)
        {
            float statBoxHeight = (boundingBox.height - (4 * margin)) / 5;
            float statGainBoxHeight = 30;
            float statGainBoxWidth = 35;
            float statSize = Math.Min(30f, statBoxHeight);
            for (int i = 0; i < stats.Length; i++)
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.Font = GameFont.Medium;
                Rect statBox = new Rect(boundingBox.x, boundingBox.y + (statBoxHeight + margin) * i, boundingBox.width, statBoxHeight);
                Widgets.DrawMenuSection(statBox);
                Rect buttonBox = new Rect(statBox.x + margin, statBox.y + margin, statSize + 4, statSize + 4);
                Rect labelBox = new Rect(buttonBox.xMax, buttonBox.y, statBox.width - (buttonBox.width + margin * 2), buttonBox.height);
                Rect statGainBox = new Rect(statBox.xMax - statGainBoxWidth - margin, statBox.y + (statBox.height - statGainBoxHeight) / 2, statGainBoxWidth, statGainBoxHeight);
                Rect mainToolTipBox = new Rect(statBox.x, statBox.y, statGainBox.x - statBox.x, statBox.height);
                string tooltip;

                switch (stats[i])
                {
                    case SettlementStatType.MilitaryLevel:
                        tooltip = DrawStatMilitaryLevel(buttonBox, labelBox);
                        break;
                    case SettlementStatType.Happiness:
                        tooltip = DrawStatWithGainBox(buttonBox, labelBox, statGainBox,
                            TexLoad.iconHappiness, settlement.happiness + "%",
                            "FCSettlementHappiness", "FCSettlementHappinessDesc",
                            settlement.GetTotalHappinessGain(), settlement.GetHappinessDesc());
                        break;
                    case SettlementStatType.Loyalty:
                        tooltip = DrawStatWithGainBox(buttonBox, labelBox, statGainBox,
                            TexLoad.iconLoyalty, settlement.loyalty + "%",
                            "FCSettlementLoyalty", "FCSettlementLoyaltyDesc",
                            settlement.GetTotalLoyaltyGain(), settlement.GetLoyaltyDesc());
                        break;
                    case SettlementStatType.Unrest:
                        tooltip = DrawStatWithGainBox(buttonBox, labelBox, statGainBox,
                            TexLoad.iconUnrest, settlement.unrest + "%",
                            "FCSettlementUnrest", "FCSettlementUnrestDesc",
                            settlement.GetTotalUnrestGain(), settlement.GetUnrestDesc(), invertColor: true);
                        break;
                    case SettlementStatType.Prosperity:
                        tooltip = DrawStatWithGainBox(buttonBox, labelBox, statGainBox,
                            TexLoad.iconProsperity, settlement.prosperity + "%",
                            "FCSettlementProsperity", "FCSettlementProsperityDesc",
                            settlement.GetProsperityGain(), settlement.GetProsperityDesc());
                        break;
                    default:
                        tooltip = "";
                        break;
                }

                TooltipHandler.TipRegion(mainToolTipBox, tooltip);
            }
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Medium;
        }

        private string DrawStatMilitaryLevel(Rect buttonBox, Rect labelBox)
        {
            Widgets.Label(buttonBox, new GUIContent(TexLoad.iconMilitary));
            // Squad-derived display: shows the strongest available stationed squad's level
            // (white), strongest stationed if all busy (yellow), half-power ghost (yellow),
            // or "—" greyed when SquadCap == 0. Red overrides everything when under attack.
            (double powLevel, double powEff, SettlementPowerStatus powStatus) = settlement.GetDisplayedPower();

            // Headline number is the full defensive total — base x efficiency x defender advantage —
            // so the value the player sees matches what the settlement actually brings to a defense.
            double defAdv = FCSettings.defenderAdvantage;
            double totalLevel = powLevel * powEff * defAdv;
            string label = powStatus == SettlementPowerStatus.NoMilitary
                ? "—"
                : ((int)Math.Round(totalLevel)).ToString();
            UIUtil.DrawColoredLabel(labelBox, label, ColorForPowerStatus(powStatus));

            // Level chain — efficiency multiplies offense AND defense, so it's applied at the Offensive step:
            //   Base (squad-derived powLevel, or half-cap "ghost") -> Offensive (× efficiency) -> Defensive
            //   (× defender advantage; = the headline number). Separately, the level cap (settlementMilitaryLevel)
            // is the strongest squad this settlement can field — split out so cap bonuses don't read as free defense.
            // Power-source line mirrors the squad case ("Power source: {squad}") for the no-squad case, since
            // the explanation already covers the half-power rule. Name the actual power-source squad when present.
            MercenarySquadFC powerSource = settlement.GetPowerSourceSquad();
            string powerSourceLine;
            if (powerSource is object)
                powerSourceLine = "FCMilPowerSource".Translate(powerSource.DisplayName);
            else if (powStatus == SettlementPowerStatus.NoMilitary)
                powerSourceLine = "FCMilPowerTipNoMilitary".Translate();
            else if (powStatus == SettlementPowerStatus.AllBusy)
                powerSourceLine = "FCMilPowerSourceNoneAvailable".Translate();
            else
                powerSourceLine = "FCMilPowerSourceNoSquad".Translate();

            double offensiveLevel = powLevel * powEff;
            string tooltip = "FCSettlementMilitaryLevel".Translate() + "\n-----\n"
                + "FCSettlementMilLevelExplain".Translate() + "\n\n"
                + "FCSettlementMilBaseLevel".Translate() + ": " + powLevel.ToString("0.#") + "\n"
                + "  " + powerSourceLine + "\n"
                + "FCSettlementMilOffensiveLine".Translate(offensiveLevel.ToString("0.#"), powEff.ToString("0.0#")) + "\n"
                + "FCSettlementMilDefensiveLine".Translate(totalLevel.ToString("0.#"), defAdv.ToString("0.0#")) + "\n\n"
                + "FCSettlementMilLevelCap".Translate() + ": " + settlement.settlementMilitaryLevel;

            // Cap breakdown: the settlement-level base (settlementLevel - 1) plus the militaryBaseLevel stat
            // modifiers (buildings, events, policies, the defensive-outpost aura). Built as a string so the
            // Colorize tags survive (a TaggedString cast would StripTags()); indent every line two spaces.
            string capLines = TextUtil.AdditiveBonusLine(settlement.settlementLevel - 1,
                "FCSettlementMilCapSettlementLevel".Translate()) + "\n";
            string milMods = settlement.GetStatDesc(FCStatDefOf.militaryBaseLevel);
            if (!milMods.NullOrEmpty()) capLines += milMods;
            tooltip += "\n  " + capLines.TrimEnd().Replace("\n", "\n  ");
            return tooltip;
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

        private string DrawStatWithGainBox(Rect buttonBox, Rect labelBox, Rect statGainBox,
            Texture2D icon, string valueText,
            string tooltipTitleKey, string tooltipDescKey,
            double gainValue, string gainTooltip, bool invertColor = false)
        {
            Widgets.Label(buttonBox, new GUIContent(icon));
            UIUtil.ClampedLabel(labelBox, valueText);
            string tooltip = tooltipTitleKey.Translate() + "\n-----\n" + tooltipDescKey.Translate();

            Widgets.DrawHighlight(statGainBox);
            double rounded = Math.Round(gainValue, 1);
            // Keep this a plain string: ColorizeAdditiveBonus returns a <color>-tagged string, and routing it
            // through a TaggedString here would strip the color (implicit TaggedString->string calls StripTags)
            // when passed to ClampedLabel below, rendering the badge white.
            string statGain = TextUtil.ColorizeAdditiveBonus(rounded, invertColor);

            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Small;
            UIUtil.ClampedLabel(statGainBox, statGain);
            TooltipHandler.TipRegion(statGainBox, gainTooltip);

            return tooltip;
        }

        private void DrawDescription(Rect boundingBox)
        {
            Widgets.DrawMenuSection(boundingBox);

            Rect textBox = new Rect(boundingBox.x + margin, boundingBox.y + margin, boundingBox.width - (margin * 2), boundingBox.height - (margin * 2));

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.Label(textBox, settlement.description);
        }
        private void RemoveSettlement()
        {
            LogUtil.Message($"Removing settlement {settlement.Name}...");
            Find.WindowStack.TryRemove(this);
            ColonyUtil.RemovePlayerSettlement(settlement);
        }
        private void DrawMainButtons(Rect boundingBox)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;

            IReadOnlyList<ISettlementWindowButton> registered = SettlementButtonRegistry.Entries;
            int visibleRegistered = 0;
            foreach (var t in registered)
            {
                if (t.IsVisible(settlement)) visibleRegistered++;
            }
            // 4 main built-ins + registered + 1 delete
            int totalCount = mainButtons.Length + visibleRegistered + 1;
            float size = (boundingBox.height - ((totalCount - 1) * margin)) / totalCount;
            int drawn = 0;

            // Main built-in buttons (Upgrade, SpecialActions, Prisoners, Military)
            for (int i = 0; i < mainButtons.Length; i++)
            {
                DrawBuiltInButton(boundingBox, mainButtons[i], size, ref drawn);
            }

            // Registered submod buttons
            for (int i = 0; i < registered.Count; i++)
            {
                ISettlementWindowButton button = registered[i];
                if (!button.IsVisible(settlement)) continue;

                Rect buttonRect = new Rect(boundingBox.x, boundingBox.y + ((size + margin) * drawn), boundingBox.width, size);
                drawn++;

                bool enabled = button.IsEnabled(settlement);
                if (!enabled) GUI.color = Color.gray;

                if (UIUtil.ClampedButtonText(buttonRect, button.Label(settlement), active: enabled))
                {
                    button.OnClick(settlement);
                }

                if (!enabled) GUI.color = Color.white;
            }

            // Delete button always last
            DrawBuiltInButton(boundingBox, SettlementButtonType.Delete, size, ref drawn);
        }

        private void DrawBuiltInButton(Rect boundingBox, SettlementButtonType type, float size, ref int drawn)
        {
            Rect buttonRect = new Rect(boundingBox.x, boundingBox.y + ((size + margin) * drawn), boundingBox.width, size);
            drawn++;

            string label = GetButtonLabel(type);
            bool enabled = true;

            if (type == SettlementButtonType.Upgrade && settlement.IsUpgrading)
            {
                GUI.color = Color.gray;
                enabled = false;
            }

            if (UIUtil.ClampedButtonText(buttonRect, label, active: enabled))
            {
                HandleBuiltInButtonClick(type);
            }

            if (type == SettlementButtonType.Upgrade && settlement.IsUpgrading)
            {
                GUI.color = Color.white;
            }
        }

        private string GetButtonLabel(SettlementButtonType type)
        {
            switch (type)
            {
                case SettlementButtonType.Upgrade:
                    return settlement.IsUpgrading
                        ? (string)"FCSettlementUpgradeInProgress".Translate()
                        : (string)"FCUpgradeSettlement".Translate();
                case SettlementButtonType.SpecialActions:
                    return "FCSpecialActions".Translate();
                case SettlementButtonType.Prisoners:
                    string label = "FCPrisonersMenu".Translate();
                    int count = settlement.PrisonerComp?.prisonerList?.Count ?? 0;
                    return count > 0 ? label + " (" + count + ")" : label;
                case SettlementButtonType.Military:
                    return "FCMilitary".Translate();
                case SettlementButtonType.Delete:
                    return "FCDeleteSettlement".Translate();
                default:
                    return "";
            }
        }

        private void HandleBuiltInButtonClick(SettlementButtonType type)
        {
            switch (type)
            {
                case SettlementButtonType.Upgrade:
                    if (!settlement.IsUpgrading)
                    {
                        Find.WindowStack.Add(new SettlementUpgradeWindowFc(settlement));
                    }
                    break;
                case SettlementButtonType.Delete:
                    // An active situation may block player deletion (e.g. a settlement that has
                    // negotiated autonomy). Programmatic removal (secession/conquest) is unaffected.
                    if (FindFC.FactionComp?.situationManager?.IsSettlementRemovalBlocked(settlement, out string blockReason) == true)
                    {
                        Messages.Message(blockReason, MessageTypeDefOf.RejectInput);
                        break;
                    }
                    Find.WindowStack.Add(new Dialog_Confirm("FCDeleteSettlementConfirm".Translate(settlement.Name), RemoveSettlement));
                    break;
                case SettlementButtonType.SpecialActions:
                    HandleSpecialActionsClick();
                    break;
                case SettlementButtonType.Prisoners:
                    Find.WindowStack.Add(new FCPrisonerMenu(settlement));
                    break;
                case SettlementButtonType.Military:
                    HandleMilitaryClick();
                    break;
            }
        }

        private void HandleSpecialActionsClick()
        {
            List<FloatMenuOption> list = new List<FloatMenuOption>
            {
                new FloatMenuOption("FCGoToLocation".Translate(), delegate
                {
                    Find.WindowStack.TryRemove(this);
                    settlement.GoTo();
                })
            };

            FindFC.PolicyManager.ForEachBehavior(b =>
            {
                var actions = b.GetSettlementActions(factionfc, settlement);
                if (actions != null)
                    list.AddRange(actions);
            });

            if (list.Count == 0)
                list.Add(new FloatMenuOption("FCNoSpecialActions".Translate(), delegate { }));
            Find.WindowStack.Add(new FloatMenu(list));
        }

        private void HandleMilitaryClick()
        {
            if (settlement.MilitaryComp is null) return;

            List<MercenarySquadFC> stationed = settlement.StationedSquads;
            int cap = settlement.SquadCap;

            List<FloatMenuOption> list = new List<FloatMenuOption>();

            // Header: Squads N / M  •  Max squad size K (informational, no action)
            list.Add(new FloatMenuOption(
                "FCSettlementMilHeader".Translate(stationed.Count, cap, settlement.MaxSquadSize),
                null, MenuOptionPriority.High));

            // Per-stationed-squad submenu — Auto-defend toggle, recall (unassign), reassign, reset pawns.
            foreach (MercenarySquadFC mercSquad in stationed)
            {
                MercenarySquadFC capturedSquad = mercSquad;
                list.Add(new FloatMenuOption(
                    "FCSettlementMilSquadEntry".Translate(capturedSquad.DisplayName,
                        capturedSquad.autoDefend ? (string)"FCYes".Translate() : (string)"FCNo".Translate()),
                    delegate
                    {
                        BuildPerSquadMenu(capturedSquad);
                    }));
            }

            // Hire & assign here.
            bool roomForHire = stationed.Count < cap;
            list.Add(new FloatMenuOption("FCSettlementMilHireAndAssign".Translate(),
                roomForHire ? (Action)delegate
                {
                    Find.WindowStack.Add(new Dialog_HireSquadsPool(settlement));
                }
            : (Action)null));

            if (settlement.MilitaryComp.isUnderAttack)
            {
                FCEvent evt = MilitaryOperationsUtil.ReturnMilitaryEventByLocation(settlement.Tile);
                MilitaryOperation op = evt?.linkedOperation;
                MilitaryForce attackerForce = op?.aggressor?.force;
                MilitaryForce defenderForce = op?.defender?.force;
                if (attackerForce is object && defenderForce is object)
                {
                    double winChance = SimulateBattleFc.CalculateDefenderWinChance(attackerForce, defenderForce);
                    list.Add(new FloatMenuOption(
                        "FCSettlementDefendingInformation".Translate(
                            defenderForce.homeSettlement?.Name ?? "",
                            defenderForce.DefensivePower,
                            (winChance * 100).ToString("F0")), null, MenuOptionPriority.High));
                }

                FCEvent capturedEvt = evt;
                list.Add(new FloatMenuOption("FCChangeDefendingForce".Translate(),
                    capturedEvt is object
                        ? (Action)delegate { Find.WindowStack.Add(new Dialog_DefendSettlement(capturedEvt)); }
                : null));
            }

            Find.WindowStack.Add(new FloatMenu(list));
        }

        /// <summary>Per-squad submenu opened from the settlement's Military button. Toggles
        /// per-squad auto-defend, opens reassignment, dismisses, or resets pawns. Operates on
        /// the squad regardless of which settlement opened the menu — squad-first refactor.</summary>
        private void BuildPerSquadMenu(MercenarySquadFC squad)
        {
            if (squad is null) return;
            MilitaryFC mfc = FindFC.Military;
            List<FloatMenuOption> list = new List<FloatMenuOption>();

            list.Add(new FloatMenuOption("FCSquadMenuInspect".Translate(),
                delegate { Find.WindowStack.Add(new Dialog_SquadInspection(squad)); }));

            list.Add(new FloatMenuOption(
                "FCSquadMenuToggleAutoDefend".Translate(squad.autoDefend ? (string)"FCOn".Translate() : (string)"FCOff".Translate()),
                delegate { squad.autoDefend = !squad.autoDefend; }));

            list.Add(new FloatMenuOption("FCSquadMenuReassign".Translate(),
                squad.IsBusy ? (Action)null : (Action)delegate
                {
                    Find.WindowStack.Add(new Dialog_SquadAssignment(squad));
                }));

            int upgrade = SquadUpgradeUtil.UpgradeCost(squad);
            if (SquadUpgradeUtil.HasUpgradeWork(squad))
            {
                list.Add(new FloatMenuOption("FCSquadMenuUpgrade".Translate(upgrade),
                    squad.IsBusy ? (Action)null : (Action)delegate { MilitaryDeploymentUtil.ConfirmAndUpgradeAll(squad); }));
            }

            list.Add(new FloatMenuOption("FCSquadMenuDismiss".Translate(),
                squad.IsBusy ? (Action)null : (Action)delegate
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "FCSquadActDismissConfirm".Translate(squad.DisplayName),
                        delegate { mfc?.DismissSquad(squad); }));
                }));

            if (!squad.Deployment.IsPhysicallyDeployed())
            {
                list.Add(new FloatMenuOption("fcResetSquadPawns".Translate(), delegate
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "fcResetSquadPawnsConfirm".Translate((NamedArgument)squad.DisplayName),
                        delegate
                        {
                            // Bypass InitiateSquad's empty-slot guard — explicit player reset.
                            squad.mercenaries = null;
                            squad.InitiateSquad();
                            Messages.Message("FCResetSquadPawns".Translate(), MessageTypeDefOf.NeutralEvent);
                        }));
                }));
            }

            Find.WindowStack.Add(new FloatMenu(list));
        }

        private Vector2 scrollVectorBuildings = new Vector2();
        public void DrawFacilities(Rect boundingBox)
        {
            Widgets.DrawMenuSection(boundingBox);

            if (settlement?.BuildingsComp is null)
            {
                // can't draw what doesn't exist
                return;
            }

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;

            Rect labelHighlight = new Rect(boundingBox.x, boundingBox.y, boundingBox.width, 30);
            Rect labelTextBox = new Rect(labelHighlight.x + smallMargin, labelHighlight.y + smallMargin, labelHighlight.width - (smallMargin * 2), labelHighlight.height - (smallMargin * 2));
            Widgets.DrawHighlight(labelHighlight);
            UIUtil.ClampedLabel(labelTextBox, "FCFacilities".Translate());

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.LowerCenter;

            float buildingBoxHeight = boundingBox.height - (labelHighlight.height + margin);
            Rect buildingBox = new Rect(boundingBox.x, labelHighlight.yMax + margin, boundingBox.width, buildingBoxHeight);

            // For a row of n buildings, there will only be n-1 spaces between them. So to offset the denominator, we add one buildingSpacing to the numerator.
            int elementsPerRow = (int)((boundingBox.width - (buildingSpacingFromSide * 2) + buildingSpacing) / (buildingBoxSide + buildingSpacing));
            float totalHeight = Mathf.Ceil(((float)settlement.BuildingsComp.Buildings.Count / (float)elementsPerRow)) * (buildingBoxSide + buildingSpacing);

            int row;
            int column;

            Rect box = new Rect(0 + buildingSpacingFromSide, 0, buildingBoxSide, buildingBoxSide);
            Rect buildingIcon = new Rect(4 + box.x, 4 + box.y, buildingBoxSide - 8, buildingBoxSide - 8);

            Rect nBox;
            Rect nBuilding;

            Rect viewRect = ScrollUtil.BeginScrollView(buildingBox, ref scrollVectorBuildings, totalHeight);


            int i = 0;

            foreach (BuildingFC buildingfc in settlement.BuildingsComp.Buildings)
            {
                BuildingFCDef building = buildingfc.def;
                //Update Variables for List
                row = (int)Math.Floor(i / (double)elementsPerRow);
                column = i % elementsPerRow;

                nBox = new Rect(
                    new Vector2(box.x + ((box.width + buildingSpacing) * column),
                                box.y + viewRect.y + ((box.height + buildingSpacing) * row)),
                    box.size);
                nBuilding = new Rect(
                    new Vector2(buildingIcon.x + ((box.width + buildingSpacing) * column),
                                buildingIcon.y + viewRect.y + ((box.height + buildingSpacing) * row)),
                    buildingIcon.size);

                //Actual UI Code
                Widgets.DrawMenuSection(nBox);
                if (i < settlement.BuildingsComp.NumBuildingSlots)
                {
                    string buildingTooltip = settlement.BuildingsComp.GetBuildingDescFull(building);
                    if (!buildingfc.active)
                        buildingTooltip = buildingTooltip + "\n" + "FCBuildingDormant".Translate();
                    TooltipHandler.TipRegion(nBuilding, buildingTooltip);
                    Color prevColor = GUI.color;
                    if (!buildingfc.active)
                        GUI.color = new Color(0.5f, 0.5f, 0.5f);
                    if (Widgets.ButtonImage(nBuilding, building.Icon))
                    {
                        Find.WindowStack.Add(new FCBuildingWindow(settlement, i));
                    }
                    GUI.color = prevColor;
                }
                else
                {
                    WorldSettlementDef sDef = settlement.settlementDef;
                    int requiredLevel = sDef.GetSettlementTypeExtension().GetRequiredLevelForSlot(i, sDef.maxBuildingCount);
                    bool isCapLocked = requiredLevel < 0 ||
                                       settlement.BuildingsComp.NumBuildingSlots >= sDef.maxBuildingCount;
                    string lockTooltip = isCapLocked
                        ? "FCBuildingLockedMax".Translate()
                        : "FCBuildingLockedLevel".Translate(requiredLevel);
                    TooltipHandler.TipRegion(nBox, lockTooltip);
                    if (Widgets.ButtonImage(nBuilding, TexLoad.buildingLocked))
                    {
                        Messages.Message("FCBuildingLocked".Translate(), MessageTypeDefOf.RejectInput);
                    }
                }

                i++;
            }
            ScrollUtil.EndScrollView();
        }
        private Vector2 scrollVectorConstruction = new Vector2();
        private void DrawConstructionBox(Rect boundingBox, int numConstruction, List<BuildingFC> construction)
        {
            Widgets.DrawMenuSection(boundingBox);

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            /* Draw the construction header */
            Rect conHeader = new Rect(boundingBox.x, boundingBox.y, boundingBox.width, 30);
            Rect conHeaderText = new Rect(conHeader.x, conHeader.y + smallMargin, conHeader.width, conHeader.height - (smallMargin * 2));
            Widgets.DrawHighlight(conHeader);
            UIUtil.ClampedLabel(conHeaderText, "FCActiveConstruction".Translate());

            Text.Font = GameFont.Small;
            if (numConstruction > 0)
            {
                /* Scroll view time, baby */
                float listHeight = boundingBox.height - (conHeader.height + margin);
                float totalHeight = (constructionListItemHeight * numConstruction) + (margin * (numConstruction - 1));
                Rect listBox = new Rect(boundingBox.x, conHeader.yMax + margin, boundingBox.width, listHeight);
                Rect viewRect = ScrollUtil.BeginScrollView(listBox, ref scrollVectorConstruction, totalHeight);

                float initialY = viewRect.y;
                Text.Anchor = TextAnchor.MiddleLeft;

                if (settlement.IsUpgrading)
                {
                    float progress = UIUtil.NormalizeProgress(settlement.StartUpgradeTick, settlement.FinishUpgradeTick);
                    Rect upgradeRect = new Rect(viewRect.x + margin,
                                                viewRect.y,
                                                viewRect.width - (margin * 2),
                                                constructionListItemHeight);
                    DrawConstructionInfoBox(upgradeRect, null, "FCSettlementupgrading".Translate(),
                                            "FCCompletiontimer".Translate((settlement.FinishUpgradeTick - Find.TickManager.TicksGame).ToTimeString()),
                                            progress);

                    initialY = upgradeRect.yMax + margin;
                }

                for (int i = 0; i < construction.Count; i++)
                {
                    float progress = UIUtil.NormalizeProgress(construction[i].startedTick, construction[i].completionTick);
                    Rect upgradeRect = new Rect(viewRect.x + margin,
                                                initialY + (i * (constructionListItemHeight + margin)),
                                                viewRect.width - (margin * 2),
                                                constructionListItemHeight);
                    DrawConstructionInfoBox(upgradeRect, construction[i].underConstructionDef.Icon, construction[i].underConstructionDef.LabelCap,
                                            "FCCompletiontimer".Translate(Math.Max(construction[i].completionTick - Find.TickManager.TicksGame, 0).ToTimeString()),
                                            progress);

                    TooltipHandler.TipRegion(upgradeRect, settlement.BuildingsComp?.GetBuildingDescFull(construction[i].underConstructionDef) ?? TaggedString.Empty);
                }

                ScrollUtil.EndScrollView();
            }
        }
        private void DrawConstructionInfoBox(Rect boundingBox, Texture2D icon, string label, string time, float progress)
        {
            Text.Font = GameFont.Tiny;
            float elementHeight = (boundingBox.height - (smallMargin * 4f)) / 3f;
            float iconHeight = (boundingBox.height - (smallMargin * 3f)) * (2f / 3f);
            float labelX = icon == null ? boundingBox.x : boundingBox.x + constructionListIconHeight + smallMargin;
            Rect iconBox = new Rect(boundingBox.x + smallMargin,
                                    boundingBox.y + smallMargin,
                                    constructionListIconHeight,
                                    constructionListIconHeight);
            Rect labelBox = new Rect(labelX + smallMargin * 2,
                                     boundingBox.y + smallMargin,
                                     boundingBox.xMax - (labelX + smallMargin * 3),
                                     constructionListItemLabelHeight);
            Rect labelHighlight = new Rect(labelX + smallMargin,
                                           boundingBox.y + smallMargin,
                                           boundingBox.xMax - (labelX + smallMargin * 2),
                                           constructionListItemLabelHeight);
            Rect timeBox = new Rect(labelBox.x,
                                    labelBox.yMax + smallMargin,
                                    labelBox.width,
                                    constructionListItemLabelHeight);
            Rect progressRect = new Rect(boundingBox.x + smallMargin,
                                         timeBox.yMax + smallMargin,
                                         boundingBox.width - (smallMargin * 2),
                                         constructionListProgressBarHeight);

            string nulabel = Text.ClampTextWithEllipsis(labelBox, label);

            Widgets.DrawMenuSection(boundingBox);
            if (icon != null)
            {
                Widgets.ButtonImage(iconBox, icon);
            }
            Widgets.DrawHighlight(labelHighlight);
            UIUtil.ClampedLabel(labelBox, nulabel);
            UIUtil.ClampedLabel(timeBox, time);
            UIUtil.DrawProgressBar(progressRect, progress);
        }

        private void DrawRightInfo(Rect boundingBox)
        {
            Color origColor = GUI.color;
            UIUtil.DrawColoredBox(boundingBox, accentColor);
            Rect prodBox = new Rect(boundingBox.x + margin, boundingBox.y + margin, boundingBox.width - (margin * 2), boundingBox.height - (margin * 2));
            DrawProduction(prodBox);
        }
        public void DrawProduction(Rect boundingBox)
        {
            Rect header = new Rect(boundingBox.x, boundingBox.y, boundingBox.width, 30f);
            // Costs block reserves the maximum height (with full subtitle slots) unconditionally
            // so the workers / production sections below stay anchored regardless of which
            // subtitles end up being rendered. See DrawCostBreakdown for per-box centering logic.
            Rect costs = new Rect(boundingBox.x, header.yMax, boundingBox.width, costsH);
            Rect workers = new Rect(boundingBox.x, costs.yMax + margin, boundingBox.width, 69f);

            DrawProductionHeader(header);
            DrawCostBreakdown(costs);
            DrawWorkerBreakdown(workers);

            Rect prodOverview = new Rect(boundingBox.x, workers.yMax + margin, boundingBox.width, boundingBox.yMax - (workers.yMax + margin));
            DrawProductionOverview(prodOverview);
        }

        /* "Current Rate" subtitles only appear when the live value differs from the period
         * average after rounding to whole silver. The cost block, however, ALWAYS reserves
         * the full subtitle slot (per box) so the workers / production sections below stay
         * anchored — they don't shift up/down each time a subtitle appears or disappears.
         * When a box doesn't actually render a subtitle, its other text is vertically
         * centered into the freed space. */
        private const float subtitleH = 18f;
        private const float profitHeadlineH = 28f;
        private const float cardLabelH = 20f;
        private const float cardValueH = 20f;
        private const float profitBoxH = profitHeadlineH + subtitleH;
        private const float cardH = cardLabelH + cardValueH + subtitleH;
        private const float costsH = profitBoxH + margin + cardH;
        private (bool showIncomeSub, bool showUpkeepSub) ComputeSubtitleVisibility()
        {
            bool hasAvg = settlement.TaxAccrualDays > 0;
            bool si = hasAvg && Math.Round(settlement.ProjectedIncome) != Math.Round(settlement.totalIncome);
            bool su = hasAvg && Math.Round(settlement.ProjectedUpkeep) != Math.Round(settlement.totalUpkeep);
            return (si, su);
        }
        private void DrawProductionHeader(Rect boundingBox)
        {
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            UIUtil.ClampedLabel(new Rect(boundingBox.x, boundingBox.y, boundingBox.width, 30), "FCProduction".Translate());
        }

        private void DrawCostBreakdown(Rect boundingBox)
        {
            Text.Font = GameFont.Small;
            float labelWidth = (boundingBox.width - margin) / 2f;

            var sub = ComputeSubtitleVisibility();
            bool showIncomeSub = sub.showIncomeSub;
            bool showUpkeepSub = sub.showUpkeepSub;
            bool hasAvg = settlement.TaxAccrualDays > 0;
            double avgProfit = settlement.ProjectedProfit;
            double liveProfit = settlement.totalProfit;
            double avgIncome = settlement.ProjectedIncome;
            double liveIncome = settlement.totalIncome;
            double avgUpkeep = settlement.ProjectedUpkeep;
            double liveUpkeep = settlement.totalUpkeep;
            Color subColor = new Color(0.7f, 0.7f, 0.7f);

            // Profit box: single projected-profit headline (net of upkeep AND tithes), no subtitle.
            // The headline spans the full box so its MiddleRight/MiddleLeft anchors vertically center it.
            Rect profitBox = new Rect(boundingBox.x, boundingBox.y, boundingBox.width, profitBoxH);
            Rect profitLabel = new Rect(profitBox.x, profitBox.y, labelWidth, profitBoxH);
            Rect profitNum = new Rect(profitLabel.xMax + margin, profitLabel.y, labelWidth, profitBoxH);
            UIUtil.DrawColoredHighlight(profitBox, highlightColor);
            Text.Anchor = TextAnchor.MiddleRight;
            UIUtil.ClampedLabel(profitLabel, "FCSettlementProjectedProfit".Translate() + ":");
            Text.Anchor = TextAnchor.MiddleLeft;
            double displayProfit = hasAvg ? avgProfit : liveProfit;
            Widgets.Label(profitNum, new GUIContent(Math.Round(displayProfit).ToString(), ThingDefOf.Silver.uiIcon));
            TooltipHandler.TipRegion(profitBox, TextUtil.BuildProjectedTooltip());

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.LowerCenter;
            labelWidth = (boundingBox.width - (margin * 12f)) / 3f;

            // Cards row also always reserves the full subtitle slot. Cards that don't render a
            // subtitle center their value vertically in the freed post-label region instead.
            Rect incomeBox = new Rect(boundingBox.x + (margin * 3), profitBox.yMax + margin, labelWidth, cardH);
            Rect costsBox = new Rect(incomeBox.xMax + (margin * 3), incomeBox.y, labelWidth, cardH);
            Rect taxBonusBox = new Rect(costsBox.xMax + (margin * 3), incomeBox.y, labelWidth, cardH);

            Rect incomeLabel = new Rect(incomeBox.x, incomeBox.y, incomeBox.width, cardLabelH);
            Rect costLabel = new Rect(costsBox.x, costsBox.y, costsBox.width, cardLabelH);
            Rect taxBonusLabel = new Rect(taxBonusBox.x, taxBonusBox.y, taxBonusBox.width, cardLabelH);

            UIUtil.DrawColoredHighlight(incomeBox, highlightColor);
            UIUtil.DrawColoredHighlight(costsBox, highlightColor);
            UIUtil.DrawColoredHighlight(taxBonusBox, highlightColor);

            UIUtil.ClampedLabel(incomeLabel, "FCSettlementProjectedIncome".Translate());
            UIUtil.ClampedLabel(costLabel, "FCUpkeep".Translate());
            UIUtil.ClampedLabel(taxBonusLabel, "FCTaxBase".Translate());

            DrawCardValueAndSubtitle(incomeBox, showIncomeSub,
                Math.Round(hasAvg ? avgIncome : liveIncome, 2).ToString(), Math.Round(liveIncome).ToString(), subColor, "FCDailyIncome".Translate());
            DrawCardValueAndSubtitle(costsBox, showUpkeepSub,
                Math.Round(hasAvg ? avgUpkeep : liveUpkeep, 2).ToString(), Math.Round(liveUpkeep).ToString(), subColor, "FCDailyUpkeep".Translate());
            DrawCardValueAndSubtitle(taxBonusBox, false,
                (settlement.GetSettlementTaxBonus() * 100d).ToString() + "%", null, subColor, null);

            string projTooltip = TextUtil.BuildProjectedTooltip();
            TooltipHandler.TipRegion(incomeBox, settlement.incomeExp + "\n\n" + projTooltip);
            TooltipHandler.TipRegion(costsBox, settlement.upkeepExp + "\n\n" + projTooltip);
            TooltipHandler.TipRegion(taxBonusBox, settlement.GetTaxBaseDesc());
        }

        /* Cards always have the same height (subtitle slot is reserved unconditionally). When
         * THIS card doesn't render a subtitle, the value rect fills the entire post-label
         * region and uses MiddleCenter anchoring so the value sits centered in the freed
         * space instead of hugging the top. */
        private void DrawCardValueAndSubtitle(Rect cardBox, bool thisCardHasSubtitle, string valueText, string subtitleLiveValue, Color subColor, string subtitleLabel)
        {
            float postLabelStart = cardBox.y + cardLabelH + smallMargin;
            float postLabelHeight = cardBox.yMax - postLabelStart;
            if (thisCardHasSubtitle)
            {
                Rect numRect = new Rect(cardBox.x, postLabelStart, cardBox.width, postLabelHeight - subtitleH);
                Rect subRect = new Rect(cardBox.x, numRect.yMax, cardBox.width, subtitleH);
                Text.Anchor = TextAnchor.UpperCenter;
                UIUtil.ClampedLabel(numRect, valueText);
                Color origColor = GUI.color;
                GUI.color = subColor;
                UIUtil.ClampedLabel(subRect, subtitleLabel + ": " + subtitleLiveValue);
                GUI.color = origColor;
            }
            else
            {
                Rect numRect = new Rect(cardBox.x, postLabelStart, cardBox.width, postLabelHeight);
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.ClampedLabel(numRect, valueText);
            }
        }

        private void DrawWorkerBreakdown(Rect boundingBox)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            float rowHeight = 23f;
            float labelHeight = rowHeight - (smallMargin * 2);
            float labelWidth = (boundingBox.width - (margin * 2));

            Rect workerBox = new Rect(boundingBox.x, boundingBox.y, boundingBox.width, rowHeight);
            Rect overMaxBox = new Rect(boundingBox.x, workerBox.yMax, boundingBox.width, rowHeight);
            Rect upkeepBox = new Rect(boundingBox.x, overMaxBox.yMax, boundingBox.width, rowHeight);

            Rect workerLabel = new Rect(workerBox.x + margin, workerBox.y + smallMargin, labelWidth * 0.75f, labelHeight);
            Rect overMaxLabel = new Rect(overMaxBox.x + margin, overMaxBox.y + smallMargin, labelWidth * 0.75f, labelHeight);
            Rect upkeepLabel = new Rect(upkeepBox.x + margin, upkeepBox.y + smallMargin, labelWidth * 0.75f, labelHeight);

            Rect workerNum = new Rect(workerLabel.xMax, workerLabel.y, labelWidth * 0.25f, labelHeight);
            Rect overMaxNum = new Rect(overMaxLabel.xMax, overMaxLabel.y, labelWidth * 0.25f, labelHeight);
            Rect upkeepNum = new Rect(upkeepLabel.xMax, upkeepLabel.y, labelWidth * 0.25f, labelHeight);

            UIUtil.DrawColoredHighlight(workerBox, highlightColor);
            TooltipHandler.TipRegion(workerBox, BuildWorkerCapacityTooltip());
            TooltipHandler.TipRegion(overMaxBox, BuildOvermaxCapacityTooltip());
            UIUtil.DrawColoredHighlight(upkeepBox, highlightColor);

            UIUtil.ClampedLabel(workerLabel, "FCAssignedWorkers".Translate());
            UIUtil.ClampedLabel(overMaxLabel, "FCAssignedOvermaxWorkers".Translate());
            UIUtil.ClampedLabel(upkeepLabel, "FCCostPerWorker".Translate());

            Text.Anchor = TextAnchor.MiddleRight;
            int numWorkers = (int)Math.Min(settlement.workers, settlement.workersMax);
            int numOvermaxWorkers = (int)Math.Max(0, settlement.workers - settlement.workersMax);
            UIUtil.ClampedLabel(workerNum, "FCAssignedWorkersValue".Translate(numWorkers, settlement.workersMax));
            UIUtil.ClampedLabel(overMaxNum, "FCAssignedOvermaxWorkersValue".Translate(numOvermaxWorkers, settlement.workersUltraMax - settlement.workersMax));
            UIUtil.ClampedLabel(upkeepNum, settlement.workerCost.ToString());
        }

        /// <summary>
        /// Builds the hover tooltip for the Assigned Workers box: a per-source breakdown of
        /// every modifier currently affecting worker capacity (workersMax). Sources are the
        /// workerBaseMax stat, the faction-wide per-level extraWorkersSoftcap, and prisoner slots.
        /// </summary>
        private string BuildWorkerCapacityTooltip()
        {
            string body = "";

            // Direct worker-cap modifiers: buildings, settlement type, events, policies,
            // traits, edicts, permanent/decaying modifiers, behaviors (each line ends in \n).
            body += settlement.GetStatDesc(FCStatDefOf.workerBaseMax);

            // Softcap bonus — applied per settlement level, so flagged separately.
            string softcap = settlement.GetStatDesc(FCStatDefOf.extraWorkersSoftcap);
            if (!softcap.NullOrEmpty())
                body += "FCExtraWorkersSoftcapTooltipSub".Translate().Resolve() + "\n" + softcap;

            // Prisoner-provided worker slots.
            int prisonerWorkers = settlement.PrisonerComp?.ReturnMaxWorkersFromPrisoners() ?? 0;
            if (prisonerWorkers != 0)
                body += TextUtil.AdditiveBonusLine(prisonerWorkers, "FCWorkersFromPrisoners".Translate()) + "\n";

            if (body.NullOrEmpty())
                body = "FCNoActiveModifiers".Translate();

            // Resolve() the header to a plain string first: a leading TaggedString would make the
            // whole concatenation a TaggedString, whose implicit string cast StripTags()s body's
            // <color> rich text. Keeping it string-typed preserves the colored modifier values.
            return "FCAssignedWorkersTooltip".Translate().Resolve() + "\n\n" + body;
        }

        /// <summary>
        /// Builds the hover tooltip for the Assigned Overmax Workers box: a per-source breakdown of
        /// every modifier affecting overmax capacity (workersUltraMax - workersMax), followed by the
        /// existing explanation of how overmax workers raise the per-worker cost. Sources are the
        /// workerBaseOverMax stat, the faction-wide overMaxWorkersAdjustment, and prisoner slots.
        /// </summary>
        private string BuildOvermaxCapacityTooltip()
        {
            string body = "";

            // Direct overmax-cap modifiers: buildings, settlement type, events, policies,
            // traits, edicts, permanent/decaying modifiers, behaviors (each line ends in \n).
            body += settlement.GetStatDesc(FCStatDefOf.workerBaseOverMax);

            // Overmax adjustment — a flat additive (not scaled by settlement level), so it's listed
            // inline rather than under a per-level sub-header. "" when no modifiers.
            body += settlement.GetStatDesc(FCStatDefOf.overMaxWorkersAdjustment);

            // Prisoner-provided overmax slots.
            int prisonerOvermax = settlement.PrisonerComp?.ReturnOverMaxWorkersFromPrisoners() ?? 0;
            if (prisonerOvermax != 0)
                body += TextUtil.AdditiveBonusLine(prisonerOvermax, "FCWorkersFromPrisoners".Translate());

            if (body.NullOrEmpty())
                body = "FCNoActiveModifiers".Translate();

            // Per-day cost of one more overmax worker: the overwork penalty adds (workers * baseWorkerCost)
            // * (overWork/20) * overworkMult, so each extra overmax worker raises daily worker upkeep by
            // 5% (1/20) of the base worker cost, scaled by the overwork multiplier.
            double overworkMult = settlement.GetStatValue(FCStatDefOf.workerOverworkPenaltyMultiplier);
            double perOvermaxPerDay = (settlement.GetBaseWorkerCost() / 20.0) * overworkMult;

            // Header + breakdown, then the cost explanation below. Resolve()d as in
            // BuildWorkerCapacityTooltip to keep the concatenation string-typed (preserves color).
            return "FCAssignedOvermaxWorkersBreakdownTooltip".Translate().Resolve() + "\n\n" + body
                + "\n\n" + "FCAssignedOvermaxWorkersTooltip".Translate(perOvermaxPerDay).Resolve();
        }

        private void DrawProductionOverview(Rect boundingBox)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            float headerHeight = 44;
            Rect resourceArea = new Rect(boundingBox.x, boundingBox.y + headerHeight + margin,
                boundingBox.width, boundingBox.yMax - (boundingBox.y + headerHeight + margin));

            /* Measure the resource list up-front so the headers AND the columns can be laid out
             * at the reduced width (and stay aligned with the scrolled content) when a scrollbar
             * will appear. needsScroll mirrors ScrollUtil.BeginScrollView's contentHeight > height
             * test on the same resourceArea, so the header and content never disagree. */
            List<ResourceFC> incomeResources, poolResources;
            float contentHeight = MeasureResources(out incomeResources, out poolResources);
            bool needsScroll = contentHeight > resourceArea.height;
            // scrollSpacing reserves the scrollbar itself; the extra 2px keeps the rightmost (Net)
            // column's outline clear of the scroll-view clip edge so DrawMenuSection's right border
            // actually renders (RimWorld eats the boundary pixels otherwise).
            float availableWidth = needsScroll ? boundingBox.width - scrollSpacing - 2f : boundingBox.width;

            /* Header — 9 columns: icon | workers | base | mult | final | total | per-day | accrued | net-income */
            float colWidth = (availableWidth - (margin * 8)) / 9f;

            Rect workersBox = new Rect(boundingBox.x + colWidth + margin, boundingBox.y, colWidth, headerHeight);
            Rect prodHeaderBox = new Rect(workersBox.xMax + margin, boundingBox.y, colWidth * 3 + margin * 2, headerHeight / 2f);
            Rect prodBaseBox = new Rect(workersBox.xMax + margin, prodHeaderBox.yMax, colWidth, headerHeight / 2f);
            Rect prodMultBox = new Rect(prodBaseBox.xMax + margin, prodBaseBox.y, colWidth, prodBaseBox.height);
            Rect prodFinalBox = new Rect(prodMultBox.xMax + margin, prodMultBox.y, colWidth, prodMultBox.height);
            Rect prodTotalBox = new Rect(prodHeaderBox.xMax + margin, boundingBox.y, colWidth, headerHeight);

            Rect incomeBox = new Rect(prodTotalBox.xMax + margin, boundingBox.y, colWidth * 3 + margin * 2, headerHeight / 2f);
            Rect incomePerDayBox = new Rect(incomeBox.x, incomeBox.yMax, colWidth, headerHeight / 2f);
            Rect incomeAccruedBox = new Rect(incomePerDayBox.xMax + margin, incomePerDayBox.y, colWidth, headerHeight / 2f);
            Rect incomeNetBox = new Rect(incomeAccruedBox.xMax + margin, incomeAccruedBox.y, colWidth, headerHeight / 2f);

            UIUtil.DrawColoredHighlight(workersBox, highlightColor);
            UIUtil.ClampedLabel(workersBox, "FCWorkers".Translate());

            UIUtil.DrawColoredHighlight(prodHeaderBox, highlightColor);
            UIUtil.ClampedLabel(prodHeaderBox, "FCPerWorkerProduction".Translate());
            UIUtil.DrawColoredHorizontalLine(prodHeaderBox.x, prodHeaderBox.yMax, prodHeaderBox.width, accentColor);
            UIUtil.DrawColoredHighlight(prodBaseBox, highlightColor);
            UIUtil.ClampedLabel(prodBaseBox, "FCBase".Translate());
            UIUtil.DrawColoredHighlight(prodMultBox, highlightColor);
            UIUtil.ClampedLabel(prodMultBox, "FCMult".Translate());
            UIUtil.DrawColoredHighlight(prodFinalBox, highlightColor);
            UIUtil.ClampedLabel(prodFinalBox, "FCFinal".Translate());

            UIUtil.DrawColoredHighlight(prodTotalBox, highlightColor);
            UIUtil.ClampedLabel(prodTotalBox, "FCTotal".Translate());

            UIUtil.DrawColoredHighlight(incomeBox, highlightColor);
            UIUtil.ClampedLabel(incomeBox, "FCIncome".Translate());
            UIUtil.DrawColoredHorizontalLine(incomeBox.x, incomeBox.yMax, incomeBox.width, accentColor);
            UIUtil.DrawColoredHighlight(incomePerDayBox, highlightColor);
            UIUtil.ClampedLabel(incomePerDayBox, "FCProductionPerDay".Translate());
            TooltipHandler.TipRegion(incomePerDayBox, "FCRawIncomeDesc".Translate());
            UIUtil.DrawColoredHighlight(incomeAccruedBox, highlightColor);
            UIUtil.ClampedLabel(incomeAccruedBox, "FCTotalAccrued".Translate());
            TooltipHandler.TipRegion(incomeAccruedBox, "FCTotalAccruedDesc".Translate());
            UIUtil.DrawColoredHighlight(incomeNetBox, highlightColor);
            UIUtil.ClampedLabel(incomeNetBox, "FCProjectedIncome".Translate());
            TooltipHandler.TipRegion(incomeNetBox, "FCProjectedIncomeDesc".Translate());

            DrawResources(resourceArea, colWidth, incomeResources, poolResources, contentHeight);
        }

        /* Partitions settlement.Resources into income-generating and pool (non-income) groups
         * and returns the total laid-out content height. settlement.Resources is already sorted
         * by uiPriority (see WorldSettlementFC's resources.Sort(ResourceFC.SortForUI)), and
         * isPoolResource does not participate in that sort, so a stable partition keeps each
         * group in its existing order. */
        private float MeasureResources(out List<ResourceFC> incomeResources, out List<ResourceFC> poolResources)
        {
            incomeResources = new List<ResourceFC>();
            poolResources = new List<ResourceFC>();
            List<ResourceFC> availableResources = settlement.Resources;
            for (int i = 0; i < availableResources.Count; i++)
            {
                ResourceFC resource = availableResources[i];
                if (resource is null || resource.def is null) continue;
                if (resource.def.isPoolResource) poolResources.Add(resource);
                else incomeResources.Add(resource);
            }
            int totalRows = incomeResources.Count + poolResources.Count;
            return (totalRows * (resourceRowHeight + margin))
                   + (poolResources.Count > 0 ? (resourceHeaderRowHeight + margin) : 0f);
        }

        private Vector2 scrollVectorResources = new Vector2();
        private void DrawResources(Rect boundingBox, float colWidth, List<ResourceFC> incomeResources, List<ResourceFC> poolResources, float totalHeight)
        {
            bool hasPoolHeader = poolResources.Count > 0;
            Rect viewRect = ScrollUtil.BeginScrollView(boundingBox, ref scrollVectorResources, totalHeight);

            /* Column background bands for total/raw/net. When a Non-Income section exists, the
             * bands are split into a top (income) band and a bottom (pool) band, each stopping
             * a small margin clear of the section header so the header reads as a divider rather
             * than having the bands run through it. Bands are sized to cover all laid-out content
             * (incl. the pool rows), not just the visible viewport. */
            float contentBottom = Math.Max(viewRect.height, totalHeight) - (margin / 2f);
            if (hasPoolHeader)
            {
                float headerTop = incomeResources.Count * (resourceRowHeight + margin);
                float poolFirstRowTop = headerTop + resourceHeaderRowHeight + margin;
                if (incomeResources.Count > 0)
                    DrawResourceColumnBands(colWidth, 0f, headerTop - (margin / 2f));
                DrawResourceColumnBands(colWidth, poolFirstRowTop - (margin / 2f), contentBottom);
            }
            else
            {
                DrawResourceColumnBands(colWidth, 0f, contentBottom);
            }

            float curY = 0f;
            int rowIndex = 0; // counts only data rows so zebra striping stays continuous across both groups

            for (int i = 0; i < incomeResources.Count; i++)
            {
                DrawResourceRow(incomeResources[i], curY, colWidth, resourceRowHeight, viewRect.width, rowIndex % 2 == 0);
                curY += resourceRowHeight + margin;
                rowIndex++;
            }

            if (hasPoolHeader)
            {
                DrawNonIncomeResourceHeader(curY, viewRect.width, resourceHeaderRowHeight);
                curY += resourceHeaderRowHeight + margin;
                for (int i = 0; i < poolResources.Count; i++)
                {
                    DrawResourceRow(poolResources[i], curY, colWidth, resourceRowHeight, viewRect.width, rowIndex % 2 == 0);
                    curY += resourceRowHeight + margin;
                    rowIndex++;
                }
            }

            ScrollUtil.EndScrollView();
        }

        /* Draws the total/per-day/accrued/net column background bands spanning the given vertical range.
         * Called once per resource section so the bands stop clear of the Non-Income header. */
        private void DrawResourceColumnBands(float colWidth, float yTop, float yBottom)
        {
            float h = yBottom - yTop;
            if (h <= 0f) return;
            Rect totalProdCol = new Rect(5f * (colWidth + margin), yTop, colWidth, h);
            Rect incomePerDayCol = new Rect(6f * (colWidth + margin), yTop, colWidth, h);
            Rect incomeAccruedCol = new Rect(7f * (colWidth + margin), yTop, colWidth, h);
            Rect incomeNetCol = new Rect(8f * (colWidth + margin), yTop, colWidth, h);
            UIUtil.DrawColoredHighlight(totalProdCol, highlightColor);
            UIUtil.DrawColoredHighlight(incomePerDayCol, highlightColor);
            UIUtil.DrawColoredHighlight(incomeAccruedCol, highlightColor);
            Widgets.DrawMenuSection(incomeNetCol);
            TooltipHandler.TipRegion(incomePerDayCol, "FCRawIncomeDesc".Translate());
        }

        /* Section header dividing income resources from the pool (non-income) resources below.
         * Mirrors the column-header styling used in DrawProductionOverview. Text.Anchor/Font are
         * already set to MiddleCenter/Tiny by the caller, so they don't need to be re-set here. */
        private void DrawNonIncomeResourceHeader(float rectY, float viewWidth, float headerRowHeight)
        {
            Rect headerBox = new Rect(0f, rectY, viewWidth, headerRowHeight);
            UIUtil.DrawColoredHighlight(headerBox, highlightColor);
            UIUtil.ClampedLabel(headerBox, "FCNonIncomeResourcesHeader".Translate());
            TooltipHandler.TipRegion(headerBox, "FCNonIncomeResourcesDesc".Translate());
        }

        private void DrawResourceRow(ResourceFC resource, float rectY, float colWidth, float rowHeight, float viewWidth, bool altHighlight)
        {
            /* Alternating highlights, to make rows easier to read/track */
            if (altHighlight)
            {
                Rect rowHighlight = new Rect(0f, rectY - (margin / 2f), viewWidth, rowHeight + margin);
                UIUtil.DrawColoredHighlight(rowHighlight, highlightColor);
            }

            // Resource color accent
            Widgets.DrawBoxSolid(new Rect(0f, rectY, 3f, rowHeight), resource.def.color);

            float resourceImgSize = Math.Min(colWidth, rowHeight);
            float resourceImxgX = (colWidth - resourceImgSize) / 2f;
            Rect resourceImgRect = new Rect(resourceImxgX, rectY, resourceImgSize, resourceImgSize);
            Widgets.ButtonImage(resourceImgRect, resource.def.Icon);
            TooltipHandler.TipRegion(resourceImgRect, resource.def.LabelCap);

            //Production Efficiency
            float arrowButtonHeight = Math.Min(rowHeight, 20f);
            float arrowButtonY = rectY + ((rowHeight - arrowButtonHeight) / 2f);
            Rect workersDecArrow = new Rect(colWidth + margin, arrowButtonY, colWidth / 3f, arrowButtonHeight);
            Rect workersNum = new Rect(workersDecArrow.xMax, rectY, workersDecArrow.width, rowHeight);
            Rect workersIncArrow = new Rect(workersNum.xMax, arrowButtonY, workersDecArrow.width, arrowButtonHeight);
            UIUtil.ClampedLabel(workersNum, resource.assignedWorkers.ToString());
            if (UIUtil.ClampedButtonText(workersDecArrow, "<")) IncreaseWorkers(resource, true);
            if (UIUtil.ClampedButtonText(workersIncArrow, ">")) IncreaseWorkers(resource);

            //Base Production
            Rect baseProd = new Rect(workersIncArrow.xMax + margin, rectY, colWidth, rowHeight);
            UIUtil.ClampedLabel(baseProd, TextUtil.FloorStat(resource.productionBase));
            TooltipHandler.TipRegion(baseProd, resource.GetProductionAdditivesDesc());

            //Modifier
            Rect multProd = new Rect(baseProd.xMax + margin, rectY, colWidth, rowHeight);
            UIUtil.ClampedLabel(multProd, TextUtil.FloorStat(resource.productionMult));
            TooltipHandler.TipRegion(multProd, resource.GetProductionMultipliersDesc());

            //Final Base
            Rect finalProd = new Rect(multProd.xMax + margin, rectY, colWidth, rowHeight);
            UIUtil.ClampedLabel(finalProd, (TextUtil.FloorStat(resource.production)));

            //Total Production
            Rect totalProd = new Rect(finalProd.xMax + margin, rectY, colWidth, rowHeight);
            UIUtil.ClampedLabel(totalProd, (TextUtil.FloorStat(resource.rawTotalProduction)));

            //Per Day: effective silver per day (post-stockpile diversion)
            Rect incomePerDayBox = new Rect(totalProd.xMax + margin, rectY, colWidth, rowHeight);
            UIUtil.ClampedLabel(incomePerDayBox, TextUtil.FloorStat(resource.effectiveRawTotalProduction * FCSettings.silverPerResource));

            //Total Accrued: accrued taxable silver this cycle
            Rect incomeAccruedBox = new Rect(incomePerDayBox.xMax + margin, rectY, colWidth, rowHeight);
            UIUtil.ClampedLabel(incomeAccruedBox, TextUtil.FloorStat(resource.AccruedTaxableValue));

            //Projected Income: accrued so far + per-day rate * days remaining (gross, post-diversion)
            double perDay = resource.effectiveRawTotalProduction * FCSettings.silverPerResource;
            double projectedIncome = resource.AccruedTaxableValue + perDay * settlement.DaysRemaining;
            Rect incomeNetBox = new Rect(incomeAccruedBox.xMax + margin, rectY, colWidth, rowHeight);
            UIUtil.ClampedLabel(incomeNetBox, (TextUtil.FloorStat(projectedIncome)));

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("FCProjectedIncomeBreakdownAccrued".Translate(TextUtil.FloorStat(resource.AccruedTaxableValue)));
            sb.AppendLine("FCProjectedIncomeBreakdownPerDay".Translate(TextUtil.FloorStat(perDay), settlement.DaysRemaining));
            sb.Append("FCProjectedIncomeBreakdownProjected".Translate(TextUtil.FloorStat(projectedIncome)));
            TooltipHandler.TipRegion(incomeNetBox, sb.ToString());
        }

        /// <summary>
        /// Increases the amount of workers in a settlement. Decreases if <paramref name="negative"/> is true. Modifies the amount based on if shift/ctrl are held
        /// </summary>
        /// <param name="resource"></param>
        /// <param name="negative"></param>
        private void IncreaseWorkers(ResourceFC resource, bool negative = false)
        {
            if (settlement.MilitaryComp?.isUnderAttack == true)
            {
                Messages.Message("FCSettlementUnderAttack".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }
            //if clicked to lower amount of workers
            settlement.IncreaseWorkers(resource, (negative ? -1 : 1) * UIUtil.GetModifier);
        }
    }
}