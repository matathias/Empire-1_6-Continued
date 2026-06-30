using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace FactionColonies
{
    /// <summary>
    /// The "Buildings" tab in the Codex — a reference for all empire buildings.
    /// Left pane: search bar + buildings grouped by tech level.
    /// Center pane: selected building detail (stats, modifiers, requirements).
    /// Right pane: source mod banner, placement restrictions, upgrade tree.
    /// </summary>
    public class CodexTab_Buildings : ICodexTab
    {
        /* Layout constants */
        private const float EntryRowHeight = 24f;
        private const float GroupHeaderHeight = 28f;
        private const float AccentBarWidth = 3f;
        private const float Margin = 8f;
        private const float SmallMargin = 4f;
        private const float SectionHeaderHeight = 22f;
        private const float StatRowHeight = 22f;
        private const float SearchBarHeight = 28f;
        private const float TitleIconSize = 28f;
        private const float IconSmall = 16f;
        private const float IndentWidth = 20f;
        private const float UpgradeRowHeight = 22f;
        private const float BannerHeight = 70f;

        private static readonly Color DefaultAccent = new Color(0.83f, 0.68f, 0.21f);
        private static readonly Color GroupBgColor = new Color(0.2f, 0.2f, 0.2f, 0.6f);
        private static readonly Color SectionBgColor = new Color(0.15f, 0.15f, 0.15f, 0.4f);
        private static readonly Color HighlightColor = new Color(0.4f, 0.6f, 0.9f);

        /* Tech level colors */
        private static readonly Dictionary<TechLevel, Color> TechColors = new Dictionary<TechLevel, Color>
        {
            { TechLevel.Undefined, new Color(0.6f, 0.6f, 0.6f) },
            { TechLevel.Neolithic, new Color(0.6f, 0.5f, 0.3f) },
            { TechLevel.Medieval, new Color(0.5f, 0.5f, 0.6f) },
            { TechLevel.Industrial, new Color(0.4f, 0.6f, 0.4f) },
            { TechLevel.Spacer, new Color(0.4f, 0.5f, 0.8f) },
            { TechLevel.Ultra, new Color(0.7f, 0.4f, 0.7f) },
            { TechLevel.Archotech, new Color(0.8f, 0.7f, 0.3f) },
        };

        /* Data model */
        private readonly CodexWindow parentWindow;
        private readonly List<TechGroup> allTechGroups;
        private BuildingFCDef selectedBuilding;
        private string searchTerm = "";

        /* Upgrade tree root reverse lookup */
        private readonly Dictionary<BuildingFCDef, BuildingFCDef> upgradeRootMap = new Dictionary<BuildingFCDef, BuildingFCDef>();

        /* Scroll state */
        private Vector2 leftScroll;
        private Vector2 centerScroll;
        private Vector2 rightScroll;

        /* Expand/collapse state */
        private readonly HashSet<TechLevel> expandedGroups = new HashSet<TechLevel>();

        /* Truncation cache */
        private readonly Dictionary<string, string> truncateCache = new Dictionary<string, string>();
        private readonly Dictionary<string, string> truncateCacheRight = new Dictionary<string, string>();

        /* Filtered building cache */
        private string lastAppliedSearch = "";
        private readonly Dictionary<TechLevel, List<BuildingFCDef>> filteredCache = new Dictionary<TechLevel, List<BuildingFCDef>>();

        private class TechGroup
        {
            public TechLevel techLevel;
            public List<BuildingFCDef> buildings = new List<BuildingFCDef>();
        }

        public string TabLabel => "FCCodexTabBuildings".Translate();
        public bool HasRightPane => true;

        public CodexTab_Buildings(CodexWindow window)
        {
            parentWindow = window;
            allTechGroups = new List<TechGroup>();

            foreach (var kvp in FactionCache.BuildingDefsByTechLevel.OrderBy(g => (int)g.Key))
            {
                TechGroup tg = new TechGroup
                {
                    techLevel = kvp.Key,
                    buildings = kvp.Value
                };
                allTechGroups.Add(tg);
                expandedGroups.Add(kvp.Key);
            }

            // Build direct-parent map (child -> its immediate parent in the upgrade tree)
            Dictionary<BuildingFCDef, BuildingFCDef> directParent = new Dictionary<BuildingFCDef, BuildingFCDef>();
            foreach (BuildingFCDef b in DefDatabase<BuildingFCDef>.AllDefsListForReading)
            {
                if (b.upgrades is null) continue;
                foreach (BuildingFCDef child in b.upgrades)
                    directParent[child] = b;
            }

            // Walk up from each child to the TRUE root so the right-pane upgrade tree
            // always renders from the family root, regardless of which node is selected.
            foreach (BuildingFCDef child in directParent.Keys)
            {
                BuildingFCDef current = child;
                BuildingFCDef parent;
                int safety = 100;
                while (directParent.TryGetValue(current, out parent) && safety-- > 0)
                    current = parent;
                upgradeRootMap[child] = current;
            }

            if (allTechGroups.Count > 0 && allTechGroups[0].buildings.Count > 0)
                selectedBuilding = allTechGroups[0].buildings[0];
        }

        public void OnTabSelected() { }
        public void OnTabDeselected() { }

        private static Color GetTechColor(TechLevel level)
        {
            Color c;
            if (TechColors.TryGetValue(level, out c))
                return c;
            return DefaultAccent;
        }

        private static string GetTechLabel(TechLevel level)
        {
            if (level == TechLevel.Undefined)
                return "FCCodexBuildingTechGeneral".Translate();
            return level.ToStringHuman().CapitalizeFirst();
        }

        private bool MatchesSearch(BuildingFCDef building)
        {
            if (searchTerm.NullOrEmpty()) return true;
            return (building.label ?? building.defName).IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void RebuildFilteredCache()
        {
            if (lastAppliedSearch == (searchTerm ?? "")) return;
            lastAppliedSearch = searchTerm ?? "";
            filteredCache.Clear();
            foreach (TechGroup tg in allTechGroups)
            {
                List<BuildingFCDef> visible = new List<BuildingFCDef>();
                foreach (BuildingFCDef b in tg.buildings)
                {
                    if (MatchesSearch(b))
                        visible.Add(b);
                }
                filteredCache[tg.techLevel] = visible;
            }
        }

        private List<BuildingFCDef> GetFilteredBuildings(TechGroup tg)
        {
            List<BuildingFCDef> result;
            if (filteredCache.TryGetValue(tg.techLevel, out result))
                return result;
            return tg.buildings;
        }

        /// <summary>
        /// Gets the full upgrade tree for any building, whether it's a root, mid-node, or leaf.
        /// Returns the root building and the tree entries.
        /// </summary>
        private bool TryGetFullUpgradeTree(BuildingFCDef building, out BuildingFCDef root, out List<BuildingUpgradeEntry> tree)
        {
            // upgradeRootMap points to the TRUE root for any descendant; buildings absent from it
            // are either roots themselves or standalone (no upgrade family).
            BuildingFCDef actualRoot;
            if (!upgradeRootMap.TryGetValue(building, out actualRoot))
                actualRoot = building;

            if (FactionCache.UpgradeTrees.TryGetValue(actualRoot, out tree))
            {
                root = actualRoot;
                return true;
            }

            root = null;
            tree = null;
            return false;
        }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  LEFT PANE
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/
        public void DrawLeftPane(Rect rect)
        {
            // Search bar at top
            Rect searchRect = new Rect(rect.x, rect.y, rect.width, SearchBarHeight);
            string prevSearch = searchTerm;
            Text.Font = GameFont.Small;
            searchTerm = Widgets.TextField(searchRect, searchTerm);
            if (searchTerm != prevSearch)
            {
                truncateCache.Clear();
                lastAppliedSearch = "";
            }
            RebuildFilteredCache();
            if (searchTerm.NullOrEmpty())
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.DrawColoredLabel(new Rect(searchRect.x + 5f, searchRect.y, searchRect.width - 10f, searchRect.height),
                    "FCCodexSearchBuildings".Translate(), Color.gray);
            }
            ResetText();

            // Building list below search bar
            float listTop = searchRect.yMax + SmallMargin;
            Rect listRect = new Rect(rect.x, listTop, rect.width, rect.yMax - listTop);
            float totalHeight = CalculateLeftPaneHeight(listRect.width);
            Rect viewRect = ScrollUtil.BeginScrollView(listRect, ref leftScroll, totalHeight);
            float curY = 0f;

            foreach (TechGroup tg in allTechGroups)
            {
                List<BuildingFCDef> visible = GetFilteredBuildings(tg);
                if (visible.Count == 0) continue;

                bool isExpanded = expandedGroups.Contains(tg.techLevel);
                Color techColor = GetTechColor(tg.techLevel);

                // Group header
                Rect groupRect = new Rect(0f, curY, viewRect.width, GroupHeaderHeight);
                Widgets.DrawBoxSolid(groupRect, GroupBgColor);
                TexLoad.DrawHorizontalGradient(groupRect, ColorUtil.TransformA(techColor, 0.2f));
                Widgets.DrawBoxSolid(new Rect(0f, curY, AccentBarWidth, GroupHeaderHeight), techColor);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.DrawColoredLabel(
                    new Rect(AccentBarWidth + Margin, curY, viewRect.width - AccentBarWidth - Margin * 2 - 20f, GroupHeaderHeight),
                    GetTechLabel(tg.techLevel),
                    ColorUtil.TransformRGB(techColor, 1.3f));

                Rect arrowRect = new Rect(groupRect.xMax - 20f - 2f, curY + (GroupHeaderHeight - 20f) * 0.5f, 20f, 20f);
                Widgets.DrawTextureFitted(arrowRect, isExpanded ? TexButton.Collapse : TexButton.Reveal, 1f);
                ResetText();

                if (Widgets.ButtonInvisible(groupRect))
                {
                    if (isExpanded) expandedGroups.Remove(tg.techLevel);
                    else expandedGroups.Add(tg.techLevel);
                    (isExpanded ? SoundDefOf.TabClose : SoundDefOf.TabOpen).PlayOneShotOnCamera();
                }

                curY += GroupHeaderHeight + 2f;
                if (!isExpanded) continue;

                // Building entries
                foreach (BuildingFCDef building in visible)
                {
                    Rect entryRect = new Rect(10f, curY, viewRect.width - 10f, EntryRowHeight);
                    bool isSelected = selectedBuilding == building;

                    if (isSelected)
                        Widgets.DrawBoxSolid(entryRect, ColorUtil.TransformA(techColor, 0.35f));
                    else if (Mouse.IsOver(entryRect))
                        Widgets.DrawBoxSolid(entryRect, ColorUtil.TransformA(techColor, 0.15f));

                    Color barColor = isSelected ? techColor : techColor * new Color(1f, 1f, 1f, 0.4f);
                    Widgets.DrawBoxSolid(new Rect(entryRect.x, entryRect.y, 2f, entryRect.height), barColor);

                    float textX = entryRect.x + Margin;

                    if (building.Icon is object)
                    {
                        Rect iconRect = new Rect(entryRect.x + 4f, entryRect.y + (EntryRowHeight - IconSmall) * 0.5f, IconSmall, IconSmall);
                        GUI.DrawTexture(iconRect, building.Icon);
                        textX = iconRect.xMax + 4f;
                    }

                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    float labelWidth = entryRect.xMax - textX - 4f;
                    string fullLabel = building.LabelCap;
                    string truncated = fullLabel.Truncate(labelWidth, truncateCache);
                    UIUtil.DrawColoredLabel(
                        new Rect(textX, entryRect.y, labelWidth, entryRect.height),
                        truncated,
                        isSelected ? Color.white : ColorUtil.Gray9);
                    if (truncated != fullLabel)
                        TooltipHandler.TipRegion(entryRect, fullLabel);
                    ResetText();

                    if (Widgets.ButtonInvisible(entryRect))
                    {
                        selectedBuilding = building;
                        centerScroll = Vector2.zero;
                        rightScroll = Vector2.zero;
                        SoundDefOf.Click.PlayOneShotOnCamera();
                    }

                    curY += EntryRowHeight;
                }
            }

            ScrollUtil.EndScrollView();
            ResetText();
        }

        private float CalculateLeftPaneHeight(float width)
        {
            float total = 0f;
            foreach (TechGroup tg in allTechGroups)
            {
                List<BuildingFCDef> visible = GetFilteredBuildings(tg);
                if (visible.Count == 0) continue;

                total += GroupHeaderHeight + 2f;
                if (expandedGroups.Contains(tg.techLevel))
                    total += visible.Count * EntryRowHeight;
            }
            return total;
        }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  CENTER PANE
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/
        public void DrawCenterPane(Rect rect)
        {
            if (selectedBuilding is null)
            {
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.DrawColoredLabel(rect, "FCCodexSelectBuilding".Translate(), Color.gray);
                ResetText();
                return;
            }

            float contentHeight = CalculateCenterHeight(rect.width - ScrollUtil.ScrollbarWidth - 1f);
            Color accent = GetTechColor(selectedBuilding.techLevel);

            Rect viewRect = ScrollUtil.BeginScrollView(rect, ref centerScroll, contentHeight);
            float contentWidth = viewRect.width;
            float curY = 0f;

            /* Title with icon */
            float titleTextX = 0f;
            if (selectedBuilding.Icon is object)
            {
                Rect iconBgRect = new Rect(0f, curY, TitleIconSize, TitleIconSize);
                Widgets.DrawBoxSolid(iconBgRect, ColorUtil.TransformA(accent, 0.25f));
                UIUtil.DrawColoredBox(iconBgRect, ColorUtil.TransformA(accent, 0.6f));
                GUI.DrawTexture(iconBgRect.ContractedBy(3f), selectedBuilding.Icon);
                titleTextX = TitleIconSize + Margin;
            }

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            Widgets.Label(new Rect(titleTextX, curY, contentWidth - titleTextX, 30f), selectedBuilding.LabelCap);
            ResetText();
            curY += 30f;

            TexLoad.DrawHorizontalGradient(new Rect(0f, curY, contentWidth, 2f), accent);
            curY += 2f + Margin;

            /* Core Stats */
            curY = DrawSection(curY, contentWidth, "FCCodexBuildingStats".Translate(), accent, DrawCoreStats);

            /* Description */
            if (!selectedBuilding.desc.NullOrEmpty())
            {
                Text.Font = GameFont.Small;
                Rect descRect = new Rect(0f, curY, contentWidth, 100f);
                Widgets.LabelCacheHeight(ref descRect, selectedBuilding.FormattedDesc);
                curY += descRect.height + Margin;
                ResetText();
            }

            /* Stat Modifiers */
            TaggedString attrDesc = selectedBuilding.AttributeDesc;
            if (!attrDesc.RawText.NullOrEmpty())
                curY = DrawSection(curY, contentWidth, "FCCodexBuildingModifiers".Translate(), accent,
                    (y, w) => DrawTextBlock(y, w, attrDesc));

            /* Extension Sections */
            curY = DrawExtensionSections(curY, contentWidth, accent);

            /* Required Buildings */
            if (selectedBuilding.requiredBuildings.Count > 0)
                curY = DrawSection(curY, contentWidth, "FCCodexBuildingRequired".Translate(), accent, DrawRequiredBuildings);

            /* Required By */
            List<BuildingFCDef> requiredBy;
            if (FactionCache.RequiredByBuildingMap.TryGetValue(selectedBuilding, out requiredBy) && requiredBy.Count > 0)
                curY = DrawSection(curY, contentWidth, "FCCodexBuildingRequiredBy".Translate(), accent,
                    (y, w) => DrawBuildingList(y, w, requiredBy));

            ScrollUtil.EndScrollView();
            ResetText();
        }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  RIGHT PANE
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/
        public void DrawRightPane(Rect rect)
        {
            float contentHeight = CalculateRightPaneHeight(rect.width - ScrollUtil.ScrollbarWidth - 1f);

            Rect viewRect = ScrollUtil.BeginScrollView(rect, ref rightScroll, contentHeight);
            float contentWidth = viewRect.width;
            float curY = 0f;

            if (selectedBuilding is object)
            {
                Color accent = GetTechColor(selectedBuilding.techLevel);

                /* Banner */
                Texture2D banner = UIUtil.GetModBanner(selectedBuilding.modContentPack);
                if (banner is object)
                {
                    Rect bannerRect = new Rect(0f, curY, contentWidth, BannerHeight);
                    GUI.DrawTexture(bannerRect, banner, ScaleMode.ScaleToFit);
                    curY += BannerHeight + SmallMargin;
                }

                string modName = selectedBuilding.modContentPack?.ModMetaData?.Name ?? "";
                if (!modName.NullOrEmpty())
                {
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    UIUtil.DrawColoredLabel(new Rect(0f, curY, contentWidth, 20f), modName, Color.gray);
                    ResetText();
                    curY += 24f;
                }

                UIUtil.DrawColoredHorizontalLine(Margin, curY, contentWidth - Margin * 2, Color.gray);
                curY += Margin;

                /* Compatible Settlements */
                List<WorldSettlementDef> compatible = selectedBuilding.CompatibleSettlementTypes;
                if (compatible.Count > 0)
                    curY = DrawSection(curY, contentWidth, "FCCodexBuildingCompatibleSettlements".Translate(), accent,
                        (y, w) => DrawCompatibleSettlements(y, w, compatible));

                /* Terrain Restrictions */
                if (HasTerrainRestrictions())
                    curY = DrawSection(curY, contentWidth, "FCCodexBuildingBiomeRestrictions".Translate(), accent, DrawTerrainRestrictions);

                /* Upgrade Tree */
                BuildingFCDef root;
                List<BuildingUpgradeEntry> tree;
                if (TryGetFullUpgradeTree(selectedBuilding, out root, out tree))
                    curY = DrawSection(curY, contentWidth, "FCCodexBuildingUpgrades".Translate(), accent,
                        (y, w) => DrawFullUpgradeTree(y, w, root, tree));
            }

            ScrollUtil.EndScrollView();
            ResetText();
        }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  SECTION DRAWING HELPERS
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/
        private delegate float SectionDrawer(float curY, float width);

        private float DrawSection(float startY, float width, string header, Color accent, SectionDrawer drawer)
        {
            float curY = startY;

            Rect headerRect = new Rect(0f, curY, width, SectionHeaderHeight);
            Widgets.DrawBoxSolid(headerRect, SectionBgColor);
            TexLoad.DrawHorizontalGradient(headerRect, ColorUtil.TransformA(accent, 0.15f));
            Widgets.DrawBoxSolid(new Rect(0f, curY, AccentBarWidth, SectionHeaderHeight), accent);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.DrawColoredLabel(
                new Rect(AccentBarWidth + Margin, curY, width - AccentBarWidth - Margin, SectionHeaderHeight),
                header,
                ColorUtil.TransformRGB(accent, 1.3f));
            ResetText();
            curY += SectionHeaderHeight + SmallMargin;

            curY = drawer(curY, width);
            curY += Margin;

            return curY;
        }

        private float DrawCoreStats(float curY, float width)
        {
            float x = AccentBarWidth + Margin;
            float textW = width - x - Margin;

            curY = DrawStatLine(curY, x, textW, "FCCodexBuildingCost".Translate(selectedBuilding.cost.ToString("F0")));
            curY = DrawStatLine(curY, x, textW, "FCCodexBuildingDuration".Translate(selectedBuilding.constructionDuration.ToTimeString()));

            if (selectedBuilding.Upkeep > 0)
                curY = DrawStatLine(curY, x, textW, "FCCodexBuildingUpkeep".Translate(selectedBuilding.Upkeep.ToString("F0")));
            else if (selectedBuilding.Upkeep < 0)
                curY = DrawStatLine(curY, x, textW, "FCCodexBuildingIncome".Translate(Math.Abs(selectedBuilding.Upkeep).ToString("F0")));

            if (selectedBuilding.techLevel != TechLevel.Undefined)
                curY = DrawStatLine(curY, x, textW, "FCCodexBuildingTechLevel".Translate(selectedBuilding.techLevel.ToStringHuman()));

            return curY;
        }

        private float DrawStatLine(float curY, float x, float width, string text)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Color.white;
            Widgets.Label(new Rect(x, curY, width, StatRowHeight), text);
            ResetText();
            return curY + StatRowHeight;
        }

        private float DrawTextBlock(float curY, float width, TaggedString text)
        {
            float x = AccentBarWidth + Margin;
            float textW = width - x - Margin;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            float h = Text.CalcHeight(text, textW);
            Widgets.Label(new Rect(x, curY, textW, h), text);
            ResetText();

            return curY + h;
        }

        private float DrawExtensionSections(float curY, float width, Color accent)
        {
            if (selectedBuilding.modExtensions is null) return curY;
            foreach (IBuildingDetailSection section in selectedBuilding.modExtensions.OfType<IBuildingDetailSection>())
            {
                float contentWidth = width - AccentBarWidth - Margin * 2;
                float contentHeight = section.GetSectionHeight(selectedBuilding, contentWidth);
                if (contentHeight <= 0) continue;

                curY = DrawSection(curY, width, section.SectionLabel, accent, (y, w) =>
                {
                    float cx = AccentBarWidth + Margin;
                    float cw = w - cx - Margin;
                    Rect contentRect = new Rect(cx, y, cw, contentHeight);
                    section.DrawSection(selectedBuilding, contentRect);
                    return y + contentHeight;
                });
            }
            return curY;
        }

        private float DrawRequiredBuildings(float curY, float width)
        {
            float x = AccentBarWidth + Margin;
            foreach (BuildingFCDef req in selectedBuilding.requiredBuildings)
                curY = DrawClickableBuilding(curY, x, width, req);
            return curY;
        }

        private float DrawBuildingList(float curY, float width, List<BuildingFCDef> buildings)
        {
            float x = AccentBarWidth + Margin;
            foreach (BuildingFCDef building in buildings)
                curY = DrawClickableBuilding(curY, x, width, building);
            return curY;
        }

        private float DrawClickableBuilding(float curY, float x, float width, BuildingFCDef building, string prefix = "")
        {
            Rect rowRect = new Rect(x, curY, width - x - Margin, UpgradeRowHeight);
            float textX = x;

            if (building.Icon is object)
            {
                Rect iconRect = new Rect(x, curY + (UpgradeRowHeight - IconSmall) * 0.5f, IconSmall, IconSmall);
                GUI.DrawTexture(iconRect, building.Icon);
                textX = iconRect.xMax + SmallMargin;
            }

            bool isHover = Mouse.IsOver(rowRect);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.DrawColoredLabel(new Rect(textX, curY, width - textX - Margin, UpgradeRowHeight), prefix + building.LabelCap, isHover ? HighlightColor : Color.white);
            ResetText();

            if (isHover)
                Widgets.DrawHighlight(rowRect);

            if (Widgets.ButtonInvisible(rowRect))
            {
                selectedBuilding = building;
                centerScroll = Vector2.zero;
                rightScroll = Vector2.zero;
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            return curY + UpgradeRowHeight;
        }

        private float DrawCompatibleSettlements(float curY, float width, List<WorldSettlementDef> settlements)
        {
            float x = AccentBarWidth + Margin;

            foreach (WorldSettlementDef def in settlements)
            {
                Rect rowRect = new Rect(x, curY, width - x - Margin, UpgradeRowHeight);
                bool isHover = Mouse.IsOver(rowRect);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.DrawColoredLabel(
                    new Rect(x, curY, width - x - Margin, UpgradeRowHeight),
                    def.LabelCap,
                    isHover ? HighlightColor : Color.white);
                ResetText();

                if (isHover)
                    Widgets.DrawHighlight(rowRect);

                if (Widgets.ButtonInvisible(rowRect))
                {
                    parentWindow.SelectSettlement(def);
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }

                curY += UpgradeRowHeight;
            }

            return curY;
        }

        private float DrawFullUpgradeTree(float curY, float width, BuildingFCDef root, List<BuildingUpgradeEntry> tree)
        {
            float baseX = AccentBarWidth + Margin;

            // Draw root first (not included in tree entries)
            curY = DrawUpgradeTreeEntry(curY, baseX, width, root, 0);

            // Draw all descendants
            foreach (BuildingUpgradeEntry entry in tree)
                curY = DrawUpgradeTreeEntry(curY, baseX, width, entry.def, entry.depth + 1);

            return curY;
        }

        private float DrawUpgradeTreeEntry(float curY, float baseX, float width, BuildingFCDef building, int depth)
        {
            float indent = depth * IndentWidth;
            float x = baseX + indent;
            Rect rowRect = new Rect(x, curY, width - x - Margin, UpgradeRowHeight);
            float textX = x;
            bool isCurrent = building == selectedBuilding;

            if (building.Icon is object)
            {
                Rect iconRect = new Rect(x, curY + (UpgradeRowHeight - IconSmall) * 0.5f, IconSmall, IconSmall);
                GUI.DrawTexture(iconRect, building.Icon);
                textX = iconRect.xMax + SmallMargin;
            }

            string prefix = depth > 0 ? "\u2514 " : "";
            bool isHover = Mouse.IsOver(rowRect);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;

            Color upgradeLabelColor = isCurrent
                ? HighlightColor
                : (isHover ? ColorUtil.TransformA(HighlightColor, 0.7f) : Color.white);

            float labelWidth = width - textX - Margin;
            string fullLabel = prefix + building.LabelCap;
            string truncated = fullLabel.Truncate(labelWidth, truncateCacheRight);
            UIUtil.DrawColoredLabel(new Rect(textX, curY, labelWidth, UpgradeRowHeight), truncated, upgradeLabelColor);
            if (truncated != fullLabel)
                TooltipHandler.TipRegion(rowRect, building.LabelCap);
            ResetText();

            // Highlight background for current building
            if (isCurrent)
                Widgets.DrawBoxSolid(new Rect(x, curY, 2f, UpgradeRowHeight), HighlightColor);

            if (isHover)
                Widgets.DrawHighlight(rowRect);

            if (Widgets.ButtonInvisible(rowRect) && !isCurrent)
            {
                selectedBuilding = building;
                centerScroll = Vector2.zero;
                rightScroll = Vector2.zero;
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            return curY + UpgradeRowHeight;
        }

        private bool HasTerrainRestrictions()
        {
            return selectedBuilding.applicableBiomes.Count > 0
                || selectedBuilding.minhilliness != Hilliness.Undefined
                || selectedBuilding.maxhilliness != Hilliness.Undefined;
        }

        private float DrawTerrainRestrictions(float curY, float width)
        {
            float x = AccentBarWidth + Margin;
            float textW = width - x - Margin;
            float bulletX = x + Margin;
            float bulletW = textW - Margin;

            if (selectedBuilding.applicableBiomes.Count > 0 && CountResolvableBiomes() > 0)
            {
                curY = DrawStatLine(curY, x, textW, "FCCodexBuildingApplicableBiomes".Translate());

                foreach (string biomeName in selectedBuilding.applicableBiomes)
                {
                    BiomeDef biome = DefDatabase<BiomeDef>.GetNamedSilentFail(biomeName);
                    if (biome is null) continue;

                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    GUI.color = Color.white;
                    Widgets.Label(new Rect(bulletX, curY, bulletW, StatRowHeight), "\u2022 " + biome.LabelCap);
                    ResetText();
                    curY += StatRowHeight;
                }
            }

            if (selectedBuilding.minhilliness != Hilliness.Undefined && selectedBuilding.maxhilliness != Hilliness.Undefined)
            {
                curY = DrawStatLine(curY, x, textW, "FCCodexBuildingHilliness".Translate(
                    selectedBuilding.minhilliness.ToString(), selectedBuilding.maxhilliness.ToString()));
            }
            else if (selectedBuilding.maxhilliness != Hilliness.Undefined)
            {
                curY = DrawStatLine(curY, x, textW, "FCCodexBuildingMaxHilliness".Translate(selectedBuilding.maxhilliness.ToString()));
            }
            else if (selectedBuilding.minhilliness != Hilliness.Undefined)
            {
                curY = DrawStatLine(curY, x, textW, "FCCodexBuildingMinHilliness".Translate(selectedBuilding.minhilliness.ToString()));
            }

            return curY;
        }

        /// <summary>
        /// Counts the number of applicable biomes that have valid BiomeDefs (mod loaded).
        /// </summary>
        private int CountResolvableBiomes()
        {
            int count = 0;
            foreach (string biomeName in selectedBuilding.applicableBiomes)
            {
                if (DefDatabase<BiomeDef>.GetNamedSilentFail(biomeName) is object)
                    count++;
            }
            return count;
        }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  HEIGHT CALCULATIONS
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/
        private float CalculateCenterHeight(float width)
        {
            if (selectedBuilding is null) return 0f;

            float total = 30f + 2f + Margin; // title + accent line

            // Core stats section
            int statLines = 2; // cost, duration
            if (selectedBuilding.Upkeep != 0) statLines++;
            if (selectedBuilding.techLevel != TechLevel.Undefined) statLines++;
            total += SectionHeaderHeight + SmallMargin + statLines * StatRowHeight + Margin;

            // Description
            if (!selectedBuilding.desc.NullOrEmpty())
            {
                Text.Font = GameFont.Small;
                total += Text.CalcHeight(selectedBuilding.FormattedDesc, width) + Margin;
            }

            // Stat Modifiers
            TaggedString attrDesc = selectedBuilding.AttributeDesc;
            if (!attrDesc.RawText.NullOrEmpty())
            {
                Text.Font = GameFont.Small;
                total += SectionHeaderHeight + SmallMargin + Text.CalcHeight(attrDesc, width - AccentBarWidth - Margin * 2) + Margin;
            }

            // Extension sections
            if (selectedBuilding.modExtensions is object)
            {
                foreach (IBuildingDetailSection section in selectedBuilding.modExtensions.OfType<IBuildingDetailSection>())
                {
                    float contentWidth = width - AccentBarWidth - Margin * 2;
                    float h = section.GetSectionHeight(selectedBuilding, contentWidth);
                    if (h > 0)
                        total += SectionHeaderHeight + SmallMargin + h + Margin;
                }
            }

            // Required Buildings
            if (selectedBuilding.requiredBuildings.Count > 0)
                total += SectionHeaderHeight + SmallMargin + selectedBuilding.requiredBuildings.Count * UpgradeRowHeight + Margin;

            // Required By
            List<BuildingFCDef> requiredBy;
            if (FactionCache.RequiredByBuildingMap.TryGetValue(selectedBuilding, out requiredBy) && requiredBy.Count > 0)
                total += SectionHeaderHeight + SmallMargin + requiredBy.Count * UpgradeRowHeight + Margin;

            return total + 50f;
        }

        private float CalculateRightPaneHeight(float width)
        {
            float total = 0f;

            if (selectedBuilding is object)
            {
                // Banner + mod name
                if (UIUtil.GetModBanner(selectedBuilding.modContentPack) is object)
                    total += BannerHeight + SmallMargin;
                string modName = selectedBuilding.modContentPack?.ModMetaData?.Name ?? "";
                if (!modName.NullOrEmpty())
                    total += 24f;
                total += Margin;

                // Compatible settlements
                List<WorldSettlementDef> compatible = selectedBuilding.CompatibleSettlementTypes;
                if (compatible.Count > 0)
                    total += SectionHeaderHeight + SmallMargin + compatible.Count * UpgradeRowHeight + Margin;

                // Terrain restrictions
                if (HasTerrainRestrictions())
                {
                    int lines = 0;
                    int resolvableBiomes = CountResolvableBiomes();
                    if (selectedBuilding.applicableBiomes.Count > 0 && resolvableBiomes > 0)
                        lines += 1 + resolvableBiomes; // "Biomes:" label + one line per resolvable biome
                    if (selectedBuilding.minhilliness != Hilliness.Undefined || selectedBuilding.maxhilliness != Hilliness.Undefined)
                        lines++;
                    total += SectionHeaderHeight + SmallMargin + lines * StatRowHeight + Margin;
                }

                // Upgrade tree
                BuildingFCDef root;
                List<BuildingUpgradeEntry> tree;
                if (TryGetFullUpgradeTree(selectedBuilding, out root, out tree))
                    total += SectionHeaderHeight + SmallMargin + (1 + tree.Count) * UpgradeRowHeight + Margin; // +1 for root
            }

            return total + 50f;
        }

        private void ResetText()
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }
    }
}
