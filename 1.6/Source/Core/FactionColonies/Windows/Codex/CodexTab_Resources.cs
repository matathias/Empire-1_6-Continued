using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace FactionColonies
{
    public class CodexTab_Resources : ICodexTab
    {
        /* Layout constants */
        private const float EntryRowHeight = 28f;
        private const float AccentBarWidth = 3f;
        private const float margin = 8f;
        private const float SmallMargin = 4f;
        private const float SectionHeaderHeight = 22f;
        private const float StatRowHeight = 22f;
        private const float UpgradeRowHeight = 22f;
        private const float IconSmall = 16f;
        private const float SearchBarHeight = 28f;
        private const float BannerHeight = 70f;

        private static readonly Color DefaultAccent = new Color(0.83f, 0.68f, 0.21f);
        private static readonly Color SectionBgColor = new Color(0.15f, 0.15f, 0.15f, 0.4f);
        private static readonly Color HighlightColor = new Color(0.4f, 0.6f, 0.9f);

        /* Data */
        private readonly CodexWindow parentWindow;
        private readonly List<ResourceTypeDef> resourceDefs;
        private ResourceTypeDef selectedResource;

        /* Scroll state */
        private Vector2 leftScroll;
        private Vector2 centerScroll;
        private Vector2 rightScroll;

        /* Truncation cache */
        private readonly Dictionary<string, string> truncateCache = new Dictionary<string, string>();

        /* Tithe item cache (built once per resource, lazy) */
        private readonly Dictionary<ResourceTypeDef, List<TitheItemEntry>> titheItemCache
            = new Dictionary<ResourceTypeDef, List<TitheItemEntry>>();

        /* Collapsible sections */
        private bool keyInfoExpanded = true;
        private bool biomeProductionExpanded = true;
        private bool titheItemsExpanded = true;

        /* Tithe search */
        private string titheSearchTerm = "";

        private class TitheItemEntry
        {
            public ThingDef thingDef;
            public TechLevel minTechLevel;       // from restriction, not thing.techLevel
            public List<string> researchLabels;  // all required research names
        }

        public string TabLabel => "FCCodexTabResources".Translate();
        public bool HasRightPane => true;

        public CodexTab_Resources(CodexWindow window)
        {
            parentWindow = window;
            resourceDefs = FactionCache.SortedResourceTypeDefsForUI;

            if (resourceDefs.Count > 0)
                selectedResource = resourceDefs[0];
        }

        public void SelectDef(ResourceTypeDef def)
        {
            if (def is object && resourceDefs.Contains(def))
            {
                selectedResource = def;
                centerScroll = Vector2.zero;
                rightScroll = Vector2.zero;
                titheSearchTerm = "";
            }
        }

        public void OnTabSelected() { }
        public void OnTabDeselected() { }

        private Color GetAccent(ResourceTypeDef def)
        {
            return def.color;
        }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  LEFT PANE
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/
        public void DrawLeftPane(Rect rect)
        {
            float totalHeight = resourceDefs.Count * EntryRowHeight;
            Rect viewRect = ScrollUtil.BeginScrollView(rect, ref leftScroll, totalHeight);
            float curY = 0f;

            foreach (ResourceTypeDef def in resourceDefs)
            {
                Rect entryRect = new Rect(0f, curY, viewRect.width, EntryRowHeight);
                bool isSelected = selectedResource == def;
                Color accent = GetAccent(def);

                if (isSelected)
                    Widgets.DrawBoxSolid(entryRect, ColorUtil.TransformA(accent, 0.35f));
                else if (Mouse.IsOver(entryRect))
                    Widgets.DrawBoxSolid(entryRect, ColorUtil.TransformA(accent, 0.15f));

                Color barColor = isSelected ? accent : ColorUtil.TransformA(accent, 0.4f);
                Widgets.DrawBoxSolid(new Rect(entryRect.x, entryRect.y, AccentBarWidth, entryRect.height), barColor);

                float textX = entryRect.x + AccentBarWidth + margin;

                if (def.Icon is object)
                {
                    Rect iconRect = new Rect(textX, entryRect.y + (EntryRowHeight - IconSmall) * 0.5f, IconSmall, IconSmall);
                    GUI.DrawTexture(iconRect, def.Icon);
                    textX = iconRect.xMax + SmallMargin;
                }

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
                    selectedResource = def;
                    centerScroll = Vector2.zero;
                    rightScroll = Vector2.zero;
                    titheSearchTerm = "";
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }

                curY += EntryRowHeight;
            }

            ScrollUtil.EndScrollView();
            ResetText();
        }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  CENTER PANE
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/
        public void DrawCenterPane(Rect rect)
        {
            if (selectedResource is null)
            {
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.DrawColoredLabel(rect, "FCCodexSelectResource".Translate(), Color.gray);
                ResetText();
                return;
            }

            float contentWidth = rect.width - ScrollUtil.ScrollbarWidth - 1f;
            float contentHeight = CalculateCenterHeight(contentWidth);
            Color accent = GetAccent(selectedResource);

            Rect viewRect = ScrollUtil.BeginScrollView(rect, ref centerScroll, contentHeight);
            contentWidth = viewRect.width;
            float curY = 0f;

            /* Title with icon */
            float titleTextX = 0f;
            if (selectedResource.Icon is object)
            {
                float titleIconSize = 28f;
                Rect iconBgRect = new Rect(0f, curY, titleIconSize, titleIconSize);
                Widgets.DrawBoxSolid(iconBgRect, ColorUtil.TransformA(accent, 0.25f));
                UIUtil.DrawColoredBox(iconBgRect, ColorUtil.TransformA(accent, 0.6f));
                GUI.DrawTexture(iconBgRect.ContractedBy(3f), selectedResource.Icon);
                titleTextX = titleIconSize + margin;
            }

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            UIUtil.ClampedLabel(new Rect(titleTextX, curY, contentWidth - titleTextX, 30f), selectedResource.LabelCap);
            ResetText();
            curY += 30f;

            TexLoad.DrawHorizontalGradient(new Rect(0f, curY, contentWidth, 2f), accent);
            curY += 2f + margin;

            /* Description */
            if (!selectedResource.description.NullOrEmpty())
            {
                Text.Font = GameFont.Small;
                Rect descRect = new Rect(0f, curY, contentWidth, 100f);
                Widgets.LabelCacheHeight(ref descRect, selectedResource.FormattedDesc);
                curY += descRect.height + margin;
                ResetText();
            }

            /* Key Info */
            curY = DrawCollapsibleSection(curY, contentWidth, "FCCodexResourceKeyInfo".Translate(), accent,
                ref keyInfoExpanded, DrawKeyInfo);

            /* Biome Production */
            curY = DrawCollapsibleSection(curY, contentWidth, "FCCodexResourceBiomeProduction".Translate(), accent,
                ref biomeProductionExpanded, DrawBiomeProduction);

            /* Tithe Items */
            if (selectedResource.CanTithe)
            {
                List<TitheItemEntry> items = GetTitheItems(selectedResource);
                string header = "FCCodexResourceTitheItems".Translate(items.Count.ToString());
                curY = DrawCollapsibleSection(curY, contentWidth, header, accent,
                    ref titheItemsExpanded, (y, w) => DrawTitheItems(y, w, items));
            }

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

            if (selectedResource is object)
            {
                Color accent = GetAccent(selectedResource);

                /* Banner */
                Texture2D banner = UIUtil.GetModBanner(selectedResource.modContentPack);
                if (banner is object)
                {
                    Rect bannerRect = new Rect(0f, curY, contentWidth, BannerHeight);
                    GUI.DrawTexture(bannerRect, banner, ScaleMode.ScaleToFit);
                    curY += BannerHeight + SmallMargin;
                }

                string modName = selectedResource.modContentPack?.ModMetaData?.Name ?? "";
                if (!modName.NullOrEmpty())
                {
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    UIUtil.DrawColoredLabel(new Rect(0f, curY, contentWidth, 20f), modName, Color.gray);
                    ResetText();
                    curY += 24f;
                }

                UIUtil.DrawColoredHorizontalLine(margin, curY, contentWidth - margin * 2, Color.gray);
                curY += margin;

                /* Compatible Settlements */
                List<WorldSettlementDef> compatible = GetCompatibleSettlements(selectedResource);
                if (compatible.Count > 0)
                    curY = DrawSection(curY, contentWidth, "FCCodexResourceCompatibleSettlements".Translate(), accent,
                        (y, w) => DrawCompatibleSettlements(y, w, compatible));
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
                new Rect(AccentBarWidth + margin, curY, width - AccentBarWidth - margin, SectionHeaderHeight),
                header,
                ColorUtil.TransformRGB(accent, 1.3f));
            ResetText();
            curY += SectionHeaderHeight + SmallMargin;

            curY = drawer(curY, width);
            curY += margin;

            return curY;
        }

        private float DrawCollapsibleSection(float startY, float width, string header, Color accent,
            ref bool expanded, SectionDrawer drawer)
        {
            float curY = startY;

            Rect headerRect = new Rect(0f, curY, width, SectionHeaderHeight);
            Widgets.DrawBoxSolid(headerRect, SectionBgColor);
            TexLoad.DrawHorizontalGradient(headerRect, accent * new Color(1f, 1f, 1f, 0.15f));
            Widgets.DrawBoxSolid(new Rect(0f, curY, AccentBarWidth, SectionHeaderHeight), accent);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.DrawColoredLabel(
                new Rect(AccentBarWidth + margin, curY, width - AccentBarWidth - margin * 2 - 20f, SectionHeaderHeight),
                header,
                ColorUtil.TransformRGB(accent, 1.3f));

            Rect arrowRect = new Rect(headerRect.xMax - 20f - 2f, curY + (SectionHeaderHeight - 16f) * 0.5f, 16f, 16f);
            Widgets.DrawTextureFitted(arrowRect, expanded ? TexButton.Collapse : TexButton.Reveal, 1f);
            ResetText();

            if (Widgets.ButtonInvisible(headerRect))
            {
                expanded = !expanded;
                (expanded ? SoundDefOf.TabOpen : SoundDefOf.TabClose).PlayOneShotOnCamera();
            }

            curY += SectionHeaderHeight + SmallMargin;

            if (expanded)
            {
                curY = drawer(curY, width);
            }

            curY += margin;
            return curY;
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

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  KEY INFO
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/
        private float DrawKeyInfo(float curY, float width)
        {
            float x = AccentBarWidth + margin;
            float textW = width - x - margin;

            string yesStr = "FCCodexYes".Translate();
            string noStr = "FCCodexNo".Translate();

            if (selectedResource.isPoolResource)
                curY = DrawStatLine(curY, x, textW, "FCCodexResourcePoolResource".Translate());

            curY = DrawStatLine(curY, x, textW, "FCCodexResourceTitheable".Translate(
                selectedResource.CanTithe ? yesStr : noStr));

            if (selectedResource.minTechLevel != TechLevel.Undefined)
                curY = DrawStatLine(curY, x, textW, "FCCodexResourceMinTechLevel".Translate(
                    selectedResource.minTechLevel.ToStringHuman()));

            if (selectedResource.maxTechLevel != TechLevel.Undefined)
                curY = DrawStatLine(curY, x, textW, "FCCodexResourceMaxTechLevel".Translate(
                    selectedResource.maxTechLevel.ToStringHuman()));

            if (selectedResource.defenseWeight != 0f)
                curY = DrawStatLine(curY, x, textW, "FCCodexResourceDefenseWeight".Translate(
                    selectedResource.defenseWeight.ToString("F1")));

            return curY;
        }

        private int CountKeyInfoLines()
        {
            int lines = 1; // titheable
            if (selectedResource.isPoolResource) lines++;
            if (selectedResource.minTechLevel != TechLevel.Undefined) lines++;
            if (selectedResource.maxTechLevel != TechLevel.Undefined) lines++;
            if (selectedResource.defenseWeight != 0f) lines++;
            return lines;
        }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  BIOME PRODUCTION
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/
        private float DrawBiomeProduction(float curY, float width)
        {
            float x = AccentBarWidth + margin;
            float tableW = width - x - margin;

            List<BiomeResourceDef> allBiomes = DefDatabase<BiomeResourceDef>.AllDefsListForReading;
            List<BiomeResourceDef> rowBiomes = new List<BiomeResourceDef>();
            List<string> biomeNames = new List<string>();
            List<string> additives = new List<string>();
            List<string> multipliers = new List<string>();
            List<string> totals = new List<string>();

            foreach (BiomeResourceDef biomeDef in allBiomes.OrderBy(b => b.LabelCap.RawText))
            {
                ResourceAvailability ra = biomeDef.GetBiomeResource(selectedResource);
                if (ra is null) continue;

                double additive = double.IsNaN(ra.additive) ? 0 : ra.additive;
                double multiplier = ra.multiplier;
                double total = additive * multiplier;

                string additiveS = TextUtil.ColorizeBonus(Math.Round(additive, 2), 1.0);
                string multiplierS = TextUtil.ColorizeBonus(Math.Round(multiplier, 2), 1.0);
                string finalS = TextUtil.ColorizeBonus(Math.Round(total, 2), 1.0);
                string biomeS = biomeDef.LabelCap;
                if (total > 1)
                    biomeS = biomeS.Colorize(Color.green);
                else if (total < 1)
                    biomeS = biomeS.Colorize(Color.red);

                rowBiomes.Add(biomeDef);
                biomeNames.Add(biomeS);
                additives.Add(additiveS);
                multipliers.Add(multiplierS);
                totals.Add(finalS);
            }

            if (biomeNames.Count > 0)
            {
                Rect tableRect = new Rect(x, curY, tableW, 0f);
                int clickedRow;
                curY += UIUtil.DrawTable(tableRect, out clickedRow,
                    "FCCodexResourceBiomeName".Translate(), biomeNames,
                    "FCCodexResourceBiomeAdditive".Translate(), additives,
                    "FCCodexResourceBiomeMultiplier".Translate(), multipliers,
                    "FCCodexResourceBiomeTotal".Translate(), totals);
                curY += SmallMargin;

                if (clickedRow >= 0 && clickedRow < rowBiomes.Count)
                {
                    parentWindow.SelectBiome(rowBiomes[clickedRow]);
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
            }

            return curY;
        }

        private int CountBiomeRows()
        {
            int count = 0;
            foreach (BiomeResourceDef biomeDef in DefDatabase<BiomeResourceDef>.AllDefsListForReading)
            {
                if (biomeDef.GetBiomeResource(selectedResource) is object)
                    count++;
            }
            return count;
        }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  TITHE ITEMS
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/
        private List<TitheItemEntry> GetTitheItems(ResourceTypeDef def)
        {
            List<TitheItemEntry> cached;
            if (titheItemCache.TryGetValue(def, out cached))
                return cached;

            cached = new List<TitheItemEntry>();

            if (!def.CanTithe) { titheItemCache[def] = cached; return cached; }

            // Build an unrestricted filter that ignores research/tech checks
            ThingFilter filter = new ThingFilter();
            Dictionary<ThingDef, TitheRestrictionInfo> restrictions;
            def.FilterResourceForCodex(filter, out restrictions);

            ThingSetMakerParams param = new ThingSetMakerParams();
            param.filter = filter;
            param.techLevel = TechLevel.Archotech;
            param.countRange = new IntRange(1, 1);

            ThingSetMaker maker = new ThingSetMaker_Count();
            List<ThingDef> things = maker.AllGeneratableThingsDebug(param).ToList();

            foreach (ThingDef thing in things)
            {
                TitheItemEntry entry = new TitheItemEntry();
                entry.thingDef = thing;

                // Use restriction info from the ResourceTypeDef's allow lists
                TitheRestrictionInfo info;
                if (restrictions.TryGetValue(thing, out info))
                {
                    entry.minTechLevel = info.minTechLevel;
                    if (info.researchProjects is object && info.researchProjects.Count > 0)
                    {
                        entry.researchLabels = new List<string>();
                        foreach (ResearchProjectDef rp in info.researchProjects)
                            entry.researchLabels.Add(rp.LabelCap);
                    }
                }

                // Also check recipe-level research prerequisites (not from our restriction, but from the ThingDef itself)
                if ((entry.researchLabels is null || entry.researchLabels.Count == 0) && thing.recipeMaker is object)
                {
                    List<string> recipeResearch = new List<string>();
                    if (thing.recipeMaker.researchPrerequisite is object)
                        recipeResearch.Add(thing.recipeMaker.researchPrerequisite.LabelCap);
                    if (thing.recipeMaker.researchPrerequisites is object)
                    {
                        foreach (ResearchProjectDef rp in thing.recipeMaker.researchPrerequisites)
                            recipeResearch.Add(rp.LabelCap);
                    }
                    if (recipeResearch.Count > 0)
                        entry.researchLabels = recipeResearch;
                }

                // Fall back to thing's own tech level if no restriction-level tech
                if (entry.minTechLevel == TechLevel.Undefined && thing.techLevel != TechLevel.Undefined)
                    entry.minTechLevel = thing.techLevel;

                cached.Add(entry);
            }

            // Sort by tech level, then alphabetically
            cached.Sort((a, b) =>
            {
                int cmp = ((int)a.minTechLevel).CompareTo((int)b.minTechLevel);
                if (cmp != 0) return cmp;
                return string.Compare(a.thingDef.LabelCap.RawText, b.thingDef.LabelCap.RawText, StringComparison.OrdinalIgnoreCase);
            });

            titheItemCache[def] = cached;
            return cached;
        }

        private float DrawTitheItems(float curY, float width, List<TitheItemEntry> allItems)
        {
            float x = AccentBarWidth + margin;
            float textW = width - x - margin;

            // Search bar
            Rect searchRect = new Rect(x, curY, textW, SearchBarHeight);
            string prevSearch = titheSearchTerm;
            Text.Font = GameFont.Small;
            titheSearchTerm = Widgets.TextField(searchRect, titheSearchTerm);
            if (titheSearchTerm.NullOrEmpty())
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.DrawColoredLabel(new Rect(searchRect.x + 5f, searchRect.y, searchRect.width - 10f, searchRect.height),
                    "FCCodexResourceSearchTithe".Translate(), Color.gray);
            }
            ResetText();
            curY += SearchBarHeight + SmallMargin;

            // Filter items
            List<TitheItemEntry> filtered;
            if (titheSearchTerm.NullOrEmpty())
                filtered = allItems;
            else
            {
                filtered = new List<TitheItemEntry>();
                foreach (TitheItemEntry entry in allItems)
                {
                    if ((entry.thingDef.label ?? entry.thingDef.defName)
                        .IndexOf(titheSearchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                        filtered.Add(entry);
                }
            }

            // Draw items
            float infoButtonSize = 18f;
            foreach (TitheItemEntry entry in filtered)
            {
                float rowX = x;

                // Info card button
                Rect infoBtnRect = new Rect(rowX, curY + (StatRowHeight - infoButtonSize) * 0.5f, infoButtonSize, infoButtonSize);
                Widgets.InfoCardButton(infoBtnRect, entry.thingDef);
                rowX = infoBtnRect.xMax + SmallMargin;

                // Icon
                Texture2D icon = entry.thingDef.uiIcon;
                if (icon is object)
                {
                    Rect iconRect = new Rect(rowX, curY + (StatRowHeight - IconSmall) * 0.5f, IconSmall, IconSmall);
                    GUI.color = entry.thingDef.uiIconColor;
                    GUI.DrawTexture(iconRect, icon);
                    GUI.color = Color.white;
                    rowX = iconRect.xMax + SmallMargin;
                }

                // Label with inline tech level and research requirements
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = Color.white;
                string label = entry.thingDef.LabelCap;

                if (entry.minTechLevel != TechLevel.Undefined)
                    label += $" ({entry.minTechLevel.ToStringHuman()})".Colorize(Color.gray);

                if (entry.researchLabels is object && entry.researchLabels.Count > 0)
                {
                    string allResearch = string.Join(", ", entry.researchLabels.ToArray());
                    label += $" {"FCCodexResourceRequiresResearch".Translate(allResearch)}".Colorize(Color.gray);
                }

                UIUtil.ClampedLabel(new Rect(rowX, curY, width - rowX - margin, StatRowHeight), label);
                ResetText();

                curY += StatRowHeight;
            }

            return curY;
        }

        private int CountFilteredTitheItems(List<TitheItemEntry> allItems)
        {
            if (titheSearchTerm.NullOrEmpty()) return allItems.Count;
            int count = 0;
            foreach (TitheItemEntry entry in allItems)
            {
                if ((entry.thingDef.label ?? entry.thingDef.defName)
                    .IndexOf(titheSearchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                    count++;
            }
            return count;
        }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  COMPATIBLE SETTLEMENTS
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/
        private List<WorldSettlementDef> GetCompatibleSettlements(ResourceTypeDef resource)
        {
            List<WorldSettlementDef> result = new List<WorldSettlementDef>();
            foreach (WorldSettlementDef def in DefDatabase<WorldSettlementDef>.AllDefsListForReading)
            {
                if (!def.available) continue;

                // Check if settlement explicitly lists this resource
                bool hasResource = false;
                foreach (ResourceAvailability ra in def.resources)
                {
                    if (ra.resourceDef == resource)
                    {
                        hasResource = true;
                        break;
                    }
                }

                // Or if defaultResources is true and this is a default resource
                if (!hasResource && def.defaultResources && resource.isDefaultResource)
                    hasResource = true;

                if (hasResource)
                    result.Add(def);
            }
            result.Sort((a, b) => string.Compare(a.LabelCap.RawText, b.LabelCap.RawText, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        private float DrawCompatibleSettlements(float curY, float width, List<WorldSettlementDef> settlements)
        {
            float x = AccentBarWidth + margin;

            foreach (WorldSettlementDef def in settlements)
            {
                Rect rowRect = new Rect(x, curY, width - x - margin, UpgradeRowHeight);
                bool isHover = Mouse.IsOver(rowRect);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.DrawColoredLabel(
                    new Rect(x, curY, width - x - margin, UpgradeRowHeight),
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

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  HEIGHT CALCULATIONS
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/
        private float CalculateCenterHeight(float width)
        {
            if (selectedResource is null) return 0f;

            float total = 30f + 2f + margin; // title + accent line

            if (!selectedResource.description.NullOrEmpty())
            {
                Text.Font = GameFont.Small;
                total += Text.CalcHeight(selectedResource.FormattedDesc, width) + margin;
            }

            // Key Info section (always present)
            total += SectionHeaderHeight + SmallMargin;
            if (keyInfoExpanded)
                total += CountKeyInfoLines() * StatRowHeight;
            total += margin;

            // Biome Production section
            total += SectionHeaderHeight + SmallMargin;
            if (biomeProductionExpanded)
            {
                int biomeRows = CountBiomeRows();
                if (biomeRows > 0)
                    total += UIUtil.TableHeight(biomeRows) + SmallMargin;
            }
            total += margin;

            // Tithe Items section
            if (selectedResource.CanTithe)
            {
                total += SectionHeaderHeight + SmallMargin;
                if (titheItemsExpanded)
                {
                    List<TitheItemEntry> items = GetTitheItems(selectedResource);
                    total += SearchBarHeight + SmallMargin;
                    total += CountFilteredTitheItems(items) * StatRowHeight;
                }
                total += margin;
            }

            return total + 50f;
        }

        private float CalculateRightPaneHeight(float width)
        {
            float total = 0f;

            if (selectedResource is object)
            {
                if (UIUtil.GetModBanner(selectedResource.modContentPack) is object)
                    total += BannerHeight + SmallMargin;
                string modName = selectedResource.modContentPack?.ModMetaData?.Name ?? "";
                if (!modName.NullOrEmpty())
                    total += 24f;
                total += margin;

                List<WorldSettlementDef> compatible = GetCompatibleSettlements(selectedResource);
                if (compatible.Count > 0)
                    total += SectionHeaderHeight + SmallMargin + compatible.Count * UpgradeRowHeight + margin;
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
