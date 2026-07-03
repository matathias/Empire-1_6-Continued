using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// The Empire Codex window — a central reference hub with tabbed content.
    /// The window manages tab selection and pane layout; each <see cref="ICodexTab"/>
    /// determines what gets drawn in the left, center, and optional right panes.
    /// </summary>
    public class CodexWindow : Window
    {
        private const float LeftPaneWidth = 240f;
        private const float RightPaneWidth = 260f;
        private const float DividerWidth = 1f;
        private const float margin = 8f;
        private const float TitleHeight = 30f;
        private const float TabHeight = 22f;

        private static readonly Color TitleGold = ColorUtil.Gold;

        public override Vector2 InitialSize => new Vector2(1100f, 650f);

        private readonly List<ICodexTab> tabs;
        private readonly List<string> tabLabels;
        private int activeTabIndex;

        public CodexWindow()
        {
            doCloseButton = false;
            doCloseX = true;
            forcePause = false;
            absorbInputAroundWindow = false;
            draggable = true;
            resizeable = true;

            tabs = new List<ICodexTab>
            {
                new CodexTab_Info(),
                new CodexTab_Settlements(this),
                new CodexTab_Resources(this),
                new CodexTab_Biomes(this),
                new CodexTab_Buildings(this)
            };
            tabLabels = tabs.Select(t => t.TabLabel).ToList();
            activeTabIndex = 0;
            tabs[0].OnTabSelected();
        }

        /// <summary>
        /// Switches to the Settlements tab and selects the given settlement type.
        /// Used for cross-tab navigation from the Buildings tab.
        /// </summary>
        public void SelectSettlement(WorldSettlementDef def)
        {
            CodexTab_Settlements settTab = tabs.OfType<CodexTab_Settlements>().FirstOrDefault();
            if (settTab is null) return;

            int tabIndex = tabs.IndexOf(settTab);
            if (tabIndex >= 0 && tabIndex != activeTabIndex)
            {
                tabs[activeTabIndex].OnTabDeselected();
                activeTabIndex = tabIndex;
                tabs[activeTabIndex].OnTabSelected();
            }
            settTab.SelectDef(def);
        }

        /// <summary>
        /// Switches to the Resources tab and selects the given resource type.
        /// Used for cross-tab navigation.
        /// </summary>
        public void SelectResource(ResourceTypeDef def)
        {
            CodexTab_Resources resTab = tabs.OfType<CodexTab_Resources>().FirstOrDefault();
            if (resTab is null) return;

            int tabIndex = tabs.IndexOf(resTab);
            if (tabIndex >= 0 && tabIndex != activeTabIndex)
            {
                tabs[activeTabIndex].OnTabDeselected();
                activeTabIndex = tabIndex;
                tabs[activeTabIndex].OnTabSelected();
            }
            resTab.SelectDef(def);
        }

        /// <summary>
        /// Switches to the Biomes tab and selects the given biome.
        /// Used for cross-tab navigation from the Resources and Settlements tabs.
        /// </summary>
        public void SelectBiome(BiomeResourceDef def)
        {
            CodexTab_Biomes biomeTab = tabs.OfType<CodexTab_Biomes>().FirstOrDefault();
            if (biomeTab is null) return;

            // Only switch to the tab if the biome is actually listed there (skips synthetic
            // biomes like defaultBiome that the Resources table can show but this tab excludes).
            if (!biomeTab.SelectDef(def)) return;

            int tabIndex = tabs.IndexOf(biomeTab);
            if (tabIndex >= 0 && tabIndex != activeTabIndex)
            {
                tabs[activeTabIndex].OnTabDeselected();
                activeTabIndex = tabIndex;
                tabs[activeTabIndex].OnTabSelected();
            }
        }

        /// <summary>
        /// Opens the Codex and pre-selects a specific entry in the Info tab.
        /// </summary>
        public CodexWindow(CodexEntryDef preselect) : this()
        {
            if (preselect is object)
            {
                CodexTab_Info infoTab = tabs.OfType<CodexTab_Info>().FirstOrDefault();
                if (infoTab is object)
                    infoTab.SelectEntry(preselect);
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            /* Title header with gold gradient + logo */
            Rect titleRect = new Rect(inRect.x, inRect.y, inRect.width, TitleHeight);
            TexLoad.DrawHorizontalGradient(titleRect, ColorUtil.TransformA(TitleGold, 0.5f));

            // Logo
            float logoSize = 24f;
            Rect logoRect = new Rect(titleRect.x + margin, titleRect.y + (TitleHeight - logoSize) * 0.5f, logoSize, logoSize);
            GUI.DrawTexture(logoRect, TexLoad.codexLogo, ScaleMode.ScaleToFit);

            // Title text
            float labelX = logoRect.xMax + 4f;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Color.white;
            UIUtil.ClampedLabel(new Rect(labelX, titleRect.y, titleRect.xMax - labelX - margin, titleRect.height), "FCCodexTitle".Translate());
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // Gold accent line under header
            TexLoad.DrawHorizontalGradient(new Rect(titleRect.x, titleRect.yMax, titleRect.width, 2f), TitleGold);

            /* Tab row (below title) */
            // Subtract 2 from the width so the right-side border line doesn't get cut off
            Rect tabArea = new Rect(inRect.x, inRect.y + TitleHeight + margin + 2f, inRect.width - 2f, inRect.height - TitleHeight - margin - 2f);
            int newTab = UIUtil.DrawTabRow(tabArea, tabLabels, activeTabIndex, out Rect contentRect, tabHeight: TabHeight);
            if (newTab != activeTabIndex)
            {
                tabs[activeTabIndex].OnTabDeselected();
                activeTabIndex = newTab;
                tabs[activeTabIndex].OnTabSelected();
            }

            Rect bodyRect = contentRect.ContractedBy(margin);
            ICodexTab activeTab = tabs[activeTabIndex];

            /* Calculate pane rects */
            float bodyX = bodyRect.x;
            float bodyY = bodyRect.y;
            float bodyW = bodyRect.width;
            float bodyH = bodyRect.height;

            Rect leftRect = new Rect(bodyX, bodyY, LeftPaneWidth, bodyH);
            float divider1X = leftRect.xMax + margin * 0.5f;

            float rightPaneW = activeTab.HasRightPane ? RightPaneWidth : 0f;
            float centerW = bodyW - LeftPaneWidth - rightPaneW - (activeTab.HasRightPane ? margin * 2 + DividerWidth * 2 : margin + DividerWidth);
            float centerX = leftRect.xMax + margin + DividerWidth;
            Rect centerRect2 = new Rect(centerX, bodyY, centerW, bodyH);

            /* Draw dividers */
            UIUtil.DrawColoredVerticalLine(divider1X, bodyY, bodyH, Color.gray);

            Rect rightRect = default(Rect);
            if (activeTab.HasRightPane)
            {
                float divider2X = centerRect2.xMax + margin * 0.5f;
                UIUtil.DrawColoredVerticalLine(divider2X, bodyY, bodyH, Color.gray);
                float rightX = divider2X + margin * 0.5f + DividerWidth;
                float rightW = bodyRect.xMax - rightX;
                rightRect = new Rect(rightX, bodyY, rightW, bodyH);
            }

            /* Draw panes */
            activeTab.DrawLeftPane(leftRect);
            activeTab.DrawCenterPane(centerRect2);
            if (activeTab.HasRightPane)
                activeTab.DrawRightPane(rightRect);
        }
    }
}
