using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace FactionColonies
{
    /// <summary>
    /// The "Settlements" tab in the Codex — a reference for all settlement types.
    /// Left pane: flat list of available settlement types.
    /// Center pane: selected settlement type detail (stats, modifiers, restrictions).
    /// Right pane: source mod banner + available resources.
    /// </summary>
    public class CodexTab_Settlements : ICodexTab
    {
        /* Layout constants */
        private const float EntryRowHeight = 28f;
        private const float GroupHeaderHeight = 28f;
        private const float AccentBarWidth = 3f;
        private const float Margin = 8f;
        private const float SectionHeaderHeight = 22f;
        private const float StatRowHeight = 22f;
        private const float ResourceRowHeight = 24f;
        private const float SmallMargin = 4f;
        private const float BannerHeight = 70f;

        private static readonly Color DefaultAccent = new Color(0.83f, 0.68f, 0.21f);
        private static readonly Color SectionBgColor = new Color(0.15f, 0.15f, 0.15f, 0.4f);
        private static readonly Color GroupBgColor = new Color(0.2f, 0.2f, 0.2f, 0.6f);
        private static readonly Color GroupAccentColor = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color HighlightColor = new Color(0.4f, 0.6f, 0.9f);

        /* Data */
        private readonly CodexWindow parentWindow;
        private readonly List<LayerGroup> layerGroups;
        private WorldSettlementDef selectedDef;

        /* Expand/collapse state */
        private readonly HashSet<string> expandedGroups = new HashSet<string>();

        private class LayerGroup
        {
            public string key;
            public string label;
            public List<WorldSettlementDef> settlements;
        }

        /* Scroll state */
        private Vector2 leftScroll;
        private Vector2 centerScroll;
        private Vector2 rightScroll;

        /* Truncation cache */
        private readonly Dictionary<string, string> truncateCache = new Dictionary<string, string>();

        public string TabLabel => "FCCodexTabSettlements".Translate();
        public bool HasRightPane => true;

        public CodexTab_Settlements(CodexWindow window)
        {
            parentWindow = window;

            List<WorldSettlementDef> allDefs = FactionCache.AvailableWorldSettlementDefs;

            // Group by planet layer. Settlements with empty planetLayers default to Surface.
            Dictionary<string, LayerGroup> groupMap = new Dictionary<string, LayerGroup>();
            PlanetLayerDef surfaceDef = PlanetLayerDefOf.Surface;
            string surfaceKey = surfaceDef?.defName ?? "Surface";

            foreach (WorldSettlementDef def in allDefs)
            {
                List<PlanetLayerDef> layers = def.planetLayers;
                if (layers is null || layers.Count == 0)
                    layers = new List<PlanetLayerDef> { surfaceDef };

                foreach (PlanetLayerDef layer in layers)
                {
                    if (layer is null) continue;
                    string key = layer.defName;
                    LayerGroup group;
                    if (!groupMap.TryGetValue(key, out group))
                    {
                        group = new LayerGroup
                        {
                            key = key,
                            label = layer.LabelCap,
                            settlements = new List<WorldSettlementDef>()
                        };
                        groupMap[key] = group;
                    }
                    group.settlements.Add(def);
                }
            }

            // Sort: Surface first, then alphabetically by label
            layerGroups = new List<LayerGroup>();
            LayerGroup surfaceGroup;
            if (groupMap.TryGetValue(surfaceKey, out surfaceGroup))
            {
                surfaceGroup.settlements = surfaceGroup.settlements.OrderBy(d => d.LabelCap.RawText).ToList();
                layerGroups.Add(surfaceGroup);
                groupMap.Remove(surfaceKey);
            }
            foreach (LayerGroup g in groupMap.Values.OrderBy(g => g.label))
            {
                g.settlements = g.settlements.OrderBy(d => d.LabelCap.RawText).ToList();
                layerGroups.Add(g);
            }

            // Expand all groups by default
            foreach (LayerGroup g in layerGroups)
                expandedGroups.Add(g.key);

            // Select first available
            if (layerGroups.Count > 0 && layerGroups[0].settlements.Count > 0)
                selectedDef = layerGroups[0].settlements[0];
        }

        public void SelectDef(WorldSettlementDef def)
        {
            if (def is null) return;
            foreach (LayerGroup g in layerGroups)
            {
                if (g.settlements.Contains(def))
                {
                    selectedDef = def;
                    centerScroll = Vector2.zero;
                    rightScroll = Vector2.zero;
                    return;
                }
            }
        }

        public void OnTabSelected() { }
        public void OnTabDeselected() { }

        private Color GetAccent(WorldSettlementDef def)
        {
            return def.accentColor ?? DefaultAccent;
        }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  LEFT PANE
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/
        public void DrawLeftPane(Rect rect)
        {
            float totalHeight = CalculateLeftPaneHeight();
            Rect viewRect = ScrollUtil.BeginScrollView(rect, ref leftScroll, totalHeight);
            float curY = 0f;

            foreach (LayerGroup group in layerGroups)
            {
                if (group.settlements.Count == 0) continue;

                bool isExpanded = expandedGroups.Contains(group.key);

                // Group header
                Rect groupRect = new Rect(0f, curY, viewRect.width, GroupHeaderHeight);
                Widgets.DrawBoxSolid(groupRect, GroupBgColor);
                TexLoad.DrawHorizontalGradient(groupRect, ColorUtil.TransformA(GroupAccentColor, 0.2f));
                Widgets.DrawBoxSolid(new Rect(0f, curY, AccentBarWidth, GroupHeaderHeight), GroupAccentColor);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.DrawColoredLabel(
                    new Rect(AccentBarWidth + Margin, curY, viewRect.width - AccentBarWidth - Margin * 2 - 20f, GroupHeaderHeight),
                    group.label,
                    ColorUtil.TransformRGB(GroupAccentColor, 1.3f));

                Rect arrowRect = new Rect(groupRect.xMax - 20f - 2f, curY + (GroupHeaderHeight - 20f) * 0.5f, 20f, 20f);
                Widgets.DrawTextureFitted(arrowRect, isExpanded ? TexButton.Collapse : TexButton.Reveal, 1f);
                ResetText();

                if (Widgets.ButtonInvisible(groupRect))
                {
                    if (isExpanded) expandedGroups.Remove(group.key);
                    else expandedGroups.Add(group.key);
                    (isExpanded ? SoundDefOf.TabClose : SoundDefOf.TabOpen).PlayOneShotOnCamera();
                }

                curY += GroupHeaderHeight + 2f;
                if (!isExpanded) continue;

                // Settlement entries
                foreach (WorldSettlementDef def in group.settlements)
                {
                    Rect entryRect = new Rect(10f, curY, viewRect.width - 10f, EntryRowHeight);
                    bool isSelected = selectedDef == def;
                    Color accent = GetAccent(def);

                    if (isSelected)
                        Widgets.DrawBoxSolid(entryRect, ColorUtil.TransformA(accent, 0.35f));
                    else if (Mouse.IsOver(entryRect))
                        Widgets.DrawBoxSolid(entryRect, ColorUtil.TransformA(accent, 0.15f));

                    Color barColor = isSelected ? accent : ColorUtil.TransformA(accent, 0.4f);
                    Widgets.DrawBoxSolid(new Rect(entryRect.x, entryRect.y, 2f, entryRect.height), barColor);

                    float textX = entryRect.x + Margin;
                    float labelWidth = entryRect.xMax - textX - SmallMargin;
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    string fullLabel = def.LabelCap;
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
                        selectedDef = def;
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

        private float CalculateLeftPaneHeight()
        {
            float total = 0f;
            foreach (LayerGroup group in layerGroups)
            {
                if (group.settlements.Count == 0) continue;
                total += GroupHeaderHeight + 2f;
                if (expandedGroups.Contains(group.key))
                    total += group.settlements.Count * EntryRowHeight;
            }
            return total;
        }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  CENTER PANE
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/
        public void DrawCenterPane(Rect rect)
        {
            if (selectedDef is null)
            {
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.DrawColoredLabel(rect, "FCCodexSelectSettlement".Translate(), Color.gray);
                ResetText();
                return;
            }

            float contentHeight = CalculateCenterHeight(rect.width - ScrollUtil.ScrollbarWidth - 1f);
            Color accent = GetAccent(selectedDef);

            Rect viewRect = ScrollUtil.BeginScrollView(rect, ref centerScroll, contentHeight);
            float contentWidth = viewRect.width;
            float curY = 0f;

            /* Title */
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            UIUtil.ClampedLabel(new Rect(0f, curY, contentWidth, 30f), selectedDef.LabelCap);
            ResetText();
            curY += 30f;

            // Accent gradient line
            TexLoad.DrawHorizontalGradient(new Rect(0f, curY, contentWidth, 2f), accent);
            curY += 2f + Margin;

            /* Description */
            if (!selectedDef.description.NullOrEmpty())
            {
                Text.Font = GameFont.Small;
                Rect descRect = new Rect(0f, curY, contentWidth, 100f);
                Widgets.LabelCacheHeight(ref descRect, selectedDef.FormattedDesc);
                curY += descRect.height + Margin;
                ResetText();
            }

            /* Key Stats */
            curY = DrawSection(curY, contentWidth, "FCCodexSettlementStats".Translate(), accent, DrawKeyStats);

            /* Stat Modifiers */
            TaggedString statDesc = FCStatModifier.GetDescription(selectedDef.statModifiers);
            if (!statDesc.RawText.NullOrEmpty())
                curY = DrawSection(curY, contentWidth, "FCCodexSettlementStatModifiers".Translate(), accent, (y, w) => DrawStatModifiers(y, w, statDesc));

            /* Tech Requirements */
            if (selectedDef.techLevel != TechLevel.Undefined || selectedDef.researchProjects.Count > 0)
                curY = DrawSection(curY, contentWidth, "FCCodexSettlementTechReqs".Translate(), accent, DrawTechRequirements);

            /* Biome Restrictions */
            curY = DrawSection(curY, contentWidth, "FCCodexSettlementBiomes".Translate(), accent, DrawBiomeRestrictions);

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

            /* Banner */
            if (selectedDef is object)
            {
                Texture2D banner = UIUtil.GetModBanner(selectedDef.modContentPack);
                if (banner is object)
                {
                    Rect bannerRect = new Rect(0f, curY, contentWidth, BannerHeight);
                    GUI.DrawTexture(bannerRect, banner, ScaleMode.ScaleToFit);
                    curY += BannerHeight + SmallMargin;
                }

                // Mod name
                string modName = selectedDef.modContentPack?.ModMetaData?.Name ?? "";
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
            }

            /* Available Resources */
            if (selectedDef is object && selectedDef.resources.Count > 0)
            {
                Color accent = GetAccent(selectedDef);
                curY = DrawSection(curY, contentWidth, "FCCodexSettlementResources".Translate(), accent, DrawResources);
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
            TexLoad.DrawHorizontalGradient(headerRect, accent * new Color(1f, 1f, 1f, 0.15f));
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

        private float DrawKeyStats(float curY, float width)
        {
            float x = AccentBarWidth + Margin;
            float textW = width - x - Margin;

            curY = DrawStatLine(curY, x, textW, "FCCodexSettlementWorkers".Translate(
                selectedDef.workersMaxBase.ToString(), selectedDef.workersMaxMult.ToString()));

            if (selectedDef.maxSettlementLevel < 99)
                curY = DrawStatLine(curY, x, textW, "FCCodexSettlementMaxLevel".Translate(selectedDef.maxSettlementLevel.ToString()));

            // Building slots + workers progression table (3 columns, 75% width, centered)
            List<int> slotsLevels = new List<int>();
            List<int> slotsCounts = new List<int>();
            BuildSlotsProgression(selectedDef, slotsLevels, slotsCounts);

            List<string> workerCounts = slotsLevels
                .Select(lvl => (selectedDef.workersMaxBase + selectedDef.workersMaxMult * lvl).ToString())
                .ToList();

            float tableW = textW * 0.75f;
            float tableX = x + (textW - tableW) * 0.5f;
            Rect tableRect = new Rect(tableX, curY, tableW, 0f);
            curY += UIUtil.DrawTable(tableRect,
                "FCCodexSettlementBuildingSlotsLevel".Translate(),
                slotsLevels.Select(l => l.ToString()).ToList(),
                "FCCodexSettlementBuildingSlotsCount".Translate(),
                slotsCounts.Select(s => s.ToString()).ToList(),
                "FCCodexSettlementBuildingSlotsWorkers".Translate(),
                workerCounts);
            curY += SmallMargin;

            if (selectedDef.planetLayers.Count > 0)
                curY = DrawStatLine(curY, x, textW, "FCCodexSettlementPlanetLayer".Translate(selectedDef.PlanetLayersLabel.CapitalizeFirst()));

            string yesStr = "FCCodexYes".Translate();
            string noStr = "FCCodexNo".Translate();

            curY = DrawStatLine(curY, x, textW, "FCCodexSettlementManualBattle".Translate(
                selectedDef.supportsManualBattle ? yesStr : noStr));

            curY = DrawStatLine(curY, x, textW, "FCCodexSettlementCanBeRaided".Translate(
                selectedDef.canBeRaided ? yesStr : noStr));

            if (selectedDef.raidTargetingWeight != 1.0f)
                curY = DrawStatLine(curY, x, textW, "FCCodexSettlementRaidWeight".Translate(
                    selectedDef.raidTargetingWeight.ToString("F1")));

            return curY;
        }

        /// <summary>
        /// Populates parallel lists of levels and slot counts by querying the settlement type extension
        /// at representative levels and only recording levels where the slot count changes.
        /// </summary>
        private static void BuildSlotsProgression(WorldSettlementDef def, List<int> outLevels, List<int> outSlots)
        {
            SettlementTypeExtension ext = def.GetSettlementTypeExtension();
            if (ext is null)
            {
                outLevels.Add(0);
                outSlots.Add(def.baseUnlockedBuildings);
                return;
            }

            int maxLevel = def.maxSettlementLevel < 99 ? def.maxSettlementLevel : 20;
            int maxCount = def.maxBuildingCount;
            int lastSlots = -1;

            for (int lvl = 0; lvl <= maxLevel; lvl++)
            {
                int s = ext.GetBuildingSlots(lvl, maxCount);
                if (s != lastSlots)
                {
                    outLevels.Add(lvl);
                    outSlots.Add(s);
                    lastSlots = s;
                    if (s >= maxCount) break;
                }
            }
        }


        private float DrawStatLine(float curY, float x, float width, string text)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Color.white;
            UIUtil.ClampedLabel(new Rect(x, curY, width, StatRowHeight), text);
            ResetText();
            return curY + StatRowHeight;
        }

        private float DrawResources(float curY, float width)
        {
            float x = AccentBarWidth + Margin;

            foreach (ResourceAvailability ra in selectedDef.resources)
            {
                if (ra.resourceDef is null) continue;

                Rect rowRect = new Rect(x, curY, width - x - Margin, ResourceRowHeight);
                bool isHover = Mouse.IsOver(rowRect);

                Rect iconRect = new Rect(x, curY + (ResourceRowHeight - 20f) * 0.5f, 20f, 20f);
                GUI.DrawTexture(iconRect, ra.resourceDef.Icon);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;

                string label = ra.resourceDef.LabelCap;
                if (ra.additive != 0 && !double.IsNaN(ra.additive))
                    label += " (+" + ra.additive.ToString("F1") + " base)";
                if (ra.multiplier != 1)
                    label += " (\u00d7" + ra.multiplier.ToString("F1") + ")";

                UIUtil.DrawColoredLabel(
                    new Rect(iconRect.xMax + SmallMargin, curY, rowRect.xMax - iconRect.xMax - SmallMargin, ResourceRowHeight),
                    label,
                    isHover ? HighlightColor : Color.white);
                ResetText();

                if (isHover)
                    Widgets.DrawHighlight(rowRect);

                if (Widgets.ButtonInvisible(rowRect))
                {
                    parentWindow.SelectResource(ra.resourceDef);
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }

                curY += ResourceRowHeight;
            }

            return curY;
        }

        private float DrawStatModifiers(float curY, float width, TaggedString desc)
        {
            float x = AccentBarWidth + Margin;
            float textW = width - x - Margin;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            float h = Text.CalcHeight(desc, textW);
            Widgets.Label(new Rect(x, curY, textW, h), desc);
            ResetText();

            return curY + h;
        }

        private float DrawTechRequirements(float curY, float width)
        {
            float x = AccentBarWidth + Margin;
            float textW = width - x - Margin;

            if (selectedDef.techLevel != TechLevel.Undefined)
                curY = DrawStatLine(curY, x, textW, "FCCodexSettlementTechLevel".Translate(selectedDef.techLevel.ToStringHuman()));

            foreach (ResearchProjectDef rp in selectedDef.researchProjects)
                curY = DrawStatLine(curY, x, textW, "FCCodexSettlementResearch".Translate(rp.LabelCap));

            return curY;
        }

        private float DrawBiomeRestrictions(float curY, float width)
        {
            float x = AccentBarWidth + Margin;
            float textW = width - x - Margin;

            if (selectedDef.blockedBiomes.Count > 0)
            {
                curY = DrawStatLine(curY, x, textW, "FCCodexSettlementBlockedBiomes".Translate(""));
                curY = DrawBiomeLinks(curY, x, textW, selectedDef.blockedBiomes);
            }
            else if (selectedDef.allowedBiomes.Count > 0)
            {
                curY = DrawStatLine(curY, x, textW, "FCCodexSettlementAllowedBiomes".Translate(""));
                curY = DrawBiomeLinks(curY, x, textW, selectedDef.allowedBiomes);
            }
            else
            {
                curY = DrawStatLine(curY, x, textW, "FCCodexSettlementAllBiomes".Translate(selectedDef.PlanetLayersLabel));
            }

            return curY;
        }

        /// <summary>
        /// Draws an indented row per biome. Biomes with a matching <see cref="BiomeResourceDef"/>
        /// are clickable and navigate to the Biomes tab; others render as plain text.
        /// </summary>
        private float DrawBiomeLinks(float curY, float x, float textW, List<BiomeDef> biomes)
        {
            float indentX = x + 12f;
            foreach (BiomeDef b in biomes)
            {
                BiomeResourceDef brd = DefDatabase<BiomeResourceDef>.GetNamedSilentFail(b.defName);
                Rect rowRect = new Rect(indentX, curY, textW - 12f, StatRowHeight);

                bool isLink = brd is object;
                bool isHover = isLink && Mouse.IsOver(rowRect);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.DrawColoredLabel(rowRect, b.LabelCap, isHover ? HighlightColor : Color.white);
                ResetText();

                if (isLink)
                {
                    if (isHover)
                        Widgets.DrawHighlight(rowRect);
                    if (Widgets.ButtonInvisible(rowRect))
                    {
                        parentWindow.SelectBiome(brd);
                        SoundDefOf.Click.PlayOneShotOnCamera();
                    }
                }

                curY += StatRowHeight;
            }
            return curY;
        }

        /// <summary>Number of rows <see cref="DrawBiomeRestrictions"/> will render (for height calc).</summary>
        private int BiomeRestrictionRowCount()
        {
            if (selectedDef.blockedBiomes.Count > 0) return 1 + selectedDef.blockedBiomes.Count;
            if (selectedDef.allowedBiomes.Count > 0) return 1 + selectedDef.allowedBiomes.Count;
            return 1;
        }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  HEIGHT CALCULATIONS
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/
        private float CalculateCenterHeight(float width)
        {
            if (selectedDef is null) return 0f;

            float total = 30f + 2f + Margin; // title + accent line

            if (!selectedDef.description.NullOrEmpty())
            {
                Text.Font = GameFont.Small;
                total += Text.CalcHeight(selectedDef.FormattedDesc, width) + Margin;
            }

            // Key Stats section
            total += SectionHeaderHeight + SmallMargin;
            int statLines = 3; // workers, manual battle, can be raided
            if (selectedDef.maxSettlementLevel < 99) statLines++;
            if (selectedDef.planetLayers.Count > 0) statLines++;
            if (selectedDef.raidTargetingWeight != 1.0f) statLines++;
            total += statLines * StatRowHeight;

            // Building slots table
            List<int> slotsLevels = new List<int>();
            List<int> slotsCounts = new List<int>();
            BuildSlotsProgression(selectedDef, slotsLevels, slotsCounts);
            total += UIUtil.TableHeight(slotsLevels.Count) + SmallMargin;

            total += Margin;

            // Stat Modifiers section
            TaggedString statDesc = FCStatModifier.GetDescription(selectedDef.statModifiers);
            if (!statDesc.RawText.NullOrEmpty())
            {
                Text.Font = GameFont.Small;
                total += SectionHeaderHeight + SmallMargin + Text.CalcHeight(statDesc, width - AccentBarWidth - Margin * 2) + Margin;
            }

            // Tech Requirements
            if (selectedDef.techLevel != TechLevel.Undefined || selectedDef.researchProjects.Count > 0)
            {
                int techLines = 0;
                if (selectedDef.techLevel != TechLevel.Undefined) techLines++;
                techLines += selectedDef.researchProjects.Count;
                total += SectionHeaderHeight + SmallMargin + techLines * StatRowHeight + Margin;
            }

            // Biome Restrictions
            total += SectionHeaderHeight + SmallMargin + BiomeRestrictionRowCount() * StatRowHeight + Margin;

            return total + 50f;
        }

        private float CalculateRightPaneHeight(float width)
        {
            float total = 0f;

            if (selectedDef is object)
            {
                if (UIUtil.GetModBanner(selectedDef.modContentPack) is object)
                    total += BannerHeight + SmallMargin;
                string modName = selectedDef.modContentPack?.ModMetaData?.Name ?? "";
                if (!modName.NullOrEmpty())
                    total += 24f;
                total += Margin; // divider

                // Resources
                if (selectedDef.resources.Count > 0)
                    total += SectionHeaderHeight + SmallMargin + selectedDef.resources.Count * ResourceRowHeight + Margin;
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
