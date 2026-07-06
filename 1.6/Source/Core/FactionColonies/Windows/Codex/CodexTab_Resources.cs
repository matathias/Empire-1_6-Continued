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
        /* Layout constants (shared values live in CodexUIUtil) */
        private const float EntryRowHeight = 28f;
        private const float UpgradeRowHeight = 22f;
        private const float IconSmall = 16f;
        private const float SearchBarHeight = 28f;
        private const float AccentBarWidth = CodexUIUtil.AccentBarWidth;
        private const float margin = CodexUIUtil.Margin;
        private const float SmallMargin = CodexUIUtil.SmallMargin;
        private const float SectionHeaderHeight = CodexUIUtil.SectionHeaderHeight;
        private const float StatRowHeight = CodexUIUtil.StatRowHeight;
        private const float BannerHeight = CodexUIUtil.BannerHeight;

        private static readonly Color HighlightColor = CodexUIUtil.HighlightColor;

        /* Data */
        private readonly CodexWindow parentWindow;
        private List<ResourceTypeDef> resourceDefs;
        private bool built;
        private ResourceTypeDef selectedResource;

        /* Scroll state */
        private Vector2 leftScroll;
        private Vector2 centerScroll;
        private Vector2 rightScroll;

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
        }

        /* Deferred so opening the Codex only pays for the tab that is actually shown. Invoked from
         * OnTabSelected, SelectDef, and every Draw*Pane; the guard makes repeat calls free. */
        private void EnsureBuilt()
        {
            if (built) return;
            built = true;

            resourceDefs = FactionCache.SortedResourceTypeDefsForUI;

            if (resourceDefs.Count > 0)
                selectedResource = resourceDefs[0];
        }

        public void SelectDef(ResourceTypeDef def)
        {
            EnsureBuilt();
            if (def is object && resourceDefs.Contains(def))
            {
                selectedResource = def;
                centerScroll = Vector2.zero;
                rightScroll = Vector2.zero;
                titheSearchTerm = "";
            }
        }

        public void OnTabSelected() { EnsureBuilt(); }
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
            EnsureBuilt();
            float totalHeight = resourceDefs.Count * EntryRowHeight;
            Rect viewRect = ScrollUtil.BeginScrollView(rect, ref leftScroll, totalHeight);
            float curY = 0f;

            foreach (ResourceTypeDef def in resourceDefs)
            {
                Rect entryRect = new Rect(0f, curY, viewRect.width, EntryRowHeight);
                bool isSelected = selectedResource == def;
                Color accent = GetAccent(def);

                if (CodexUIUtil.DrawEntryRow(entryRect, def.LabelCap, accent, isSelected,
                    AccentBarWidth, AccentBarWidth + margin, AccentBarWidth + margin, def.Icon,
                    isSelected ? Color.white : ColorUtil.Gray9))
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
            CodexUIUtil.ResetText();
        }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  CENTER PANE
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/
        public void DrawCenterPane(Rect rect)
        {
            EnsureBuilt();
            if (selectedResource is null)
            {
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.DrawColoredLabel(rect, "FCCodexSelectResource".Translate(), Color.gray);
                CodexUIUtil.ResetText();
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
            CodexUIUtil.ResetText();
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
                CodexUIUtil.ResetText();
            }

            /* Key Info */
            curY = CodexUIUtil.DrawCollapsibleSection(curY, contentWidth, "FCCodexResourceKeyInfo".Translate(), accent,
                ref keyInfoExpanded, DrawKeyInfo);

            /* Biome Production */
            curY = CodexUIUtil.DrawCollapsibleSection(curY, contentWidth, "FCCodexResourceBiomeProduction".Translate(), accent,
                ref biomeProductionExpanded, DrawBiomeProduction);

            /* Tithe Items */
            if (selectedResource.CanTithe)
            {
                List<TitheItemEntry> items = GetTitheItems(selectedResource);
                string header = "FCCodexResourceTitheItems".Translate(items.Count.ToString());
                curY = CodexUIUtil.DrawCollapsibleSection(curY, contentWidth, header, accent,
                    ref titheItemsExpanded, (y, w) => DrawTitheItems(y, w, items));
            }

            ScrollUtil.EndScrollView();
            CodexUIUtil.ResetText();
        }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  RIGHT PANE
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/
        public void DrawRightPane(Rect rect)
        {
            EnsureBuilt();
            float contentHeight = CalculateRightPaneHeight(rect.width - ScrollUtil.ScrollbarWidth - 1f);

            Rect viewRect = ScrollUtil.BeginScrollView(rect, ref rightScroll, contentHeight);
            float contentWidth = viewRect.width;
            float curY = 0f;

            if (selectedResource is object)
            {
                Color accent = GetAccent(selectedResource);

                /* Banner */
                curY = CodexUIUtil.DrawRightPaneBannerHeader(curY, contentWidth,
                    UIUtil.GetModBanner(selectedResource.modContentPack),
                    selectedResource.modContentPack?.ModMetaData?.Name ?? "");

                /* Compatible Settlements */
                List<WorldSettlementDef> compatible = GetCompatibleSettlements(selectedResource);
                if (compatible.Count > 0)
                    curY = CodexUIUtil.DrawSection(curY, contentWidth, "FCCodexResourceCompatibleSettlements".Translate(), accent,
                        (y, w) => DrawCompatibleSettlements(y, w, compatible));
            }

            ScrollUtil.EndScrollView();
            CodexUIUtil.ResetText();
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
                curY = CodexUIUtil.DrawStatLine(curY, x, textW, "FCCodexResourcePoolResource".Translate());

            curY = CodexUIUtil.DrawStatLine(curY, x, textW, "FCCodexResourceTitheable".Translate(
                selectedResource.CanTithe ? yesStr : noStr));

            if (selectedResource.minTechLevel != TechLevel.Undefined)
                curY = CodexUIUtil.DrawStatLine(curY, x, textW, "FCCodexResourceMinTechLevel".Translate(
                    selectedResource.minTechLevel.ToStringHuman()));

            if (selectedResource.maxTechLevel != TechLevel.Undefined)
                curY = CodexUIUtil.DrawStatLine(curY, x, textW, "FCCodexResourceMaxTechLevel".Translate(
                    selectedResource.maxTechLevel.ToStringHuman()));

            if (selectedResource.defenseWeight != 0f)
                curY = CodexUIUtil.DrawStatLine(curY, x, textW, "FCCodexResourceDefenseWeight".Translate(
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
            CodexUIUtil.ResetText();
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
                CodexUIUtil.ResetText();

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
                CodexUIUtil.ResetText();

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
                    total += CodexUIUtil.HeaderHeightFor(modName, width, 20f, 0f) + SmallMargin;
                total += margin;

                List<WorldSettlementDef> compatible = GetCompatibleSettlements(selectedResource);
                if (compatible.Count > 0)
                    total += SectionHeaderHeight + SmallMargin + compatible.Count * UpgradeRowHeight + margin;
            }

            return total + 50f;
        }
    }
}
