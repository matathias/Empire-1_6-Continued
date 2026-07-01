using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace FactionColonies
{
    /// <summary>
    /// The "Biomes" tab in the Codex — the inverse of the Resources tab. Select a biome to
    /// see every resource's additive / multiplier / total base production for that biome,
    /// plus which settlement types can be founded there.
    /// Left pane: biomes grouped by the mod that adds them (Core/DLC first, then third-party).
    /// Center pane: selected biome detail (key info, resource production table, stat modifiers).
    /// Right pane: source mod banner + foundable settlement types.
    /// </summary>
    public class CodexTab_Biomes : ICodexTab
    {
        /* Layout constants */
        private const float EntryRowHeight = 28f;
        private const float GroupHeaderHeight = 28f;
        private const float AccentBarWidth = 3f;
        private const float margin = 8f;
        private const float SmallMargin = 4f;
        private const float SectionHeaderHeight = 22f;
        private const float StatRowHeight = 22f;
        private const float SettlementRowHeight = 22f;
        private const float BannerHeight = 70f;

        private static readonly Color BiomeAccent = new Color(0.45f, 0.72f, 0.42f);
        private static readonly Color SectionBgColor = new Color(0.15f, 0.15f, 0.15f, 0.4f);
        private static readonly Color GroupBgColor = new Color(0.2f, 0.2f, 0.2f, 0.6f);
        private static readonly Color GroupAccentColor = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color HighlightColor = new Color(0.4f, 0.6f, 0.9f);

        /* Data */
        private readonly CodexWindow parentWindow;
        private readonly List<ModGroup> modGroups;
        private BiomeEntry selectedEntry;

        /* Expand/collapse state (left pane mod groups) */
        private readonly HashSet<string> expandedGroups = new HashSet<string>();

        /* Collapsible sections (center pane) */
        private bool keyInfoExpanded = true;
        private bool resourceProductionExpanded = true;
        private bool statModifiersExpanded = true;

        /* Scroll state */
        private Vector2 leftScroll;
        private Vector2 centerScroll;
        private Vector2 rightScroll;

        /// <summary>
        /// One biome entry: Empire's per-biome production def paired with the matching
        /// vanilla/modded <see cref="BiomeDef"/> (the source of mod attribution and description).
        /// </summary>
        private class BiomeEntry
        {
            public BiomeResourceDef resourceDef;
            public BiomeDef biomeDef;
            /// <summary>True if at least one settlement type can be founded here (implies canSettle).</summary>
            public bool foundable;
        }

        private class ModGroup
        {
            public string key;
            public string label;
            public List<BiomeEntry> biomes;
        }

        public string TabLabel => "FCCodexTabBiomes".Translate();
        public bool HasRightPane => true;

        public CodexTab_Biomes(CodexWindow window)
        {
            parentWindow = window;

            /* Build entries: only biomes that have a matching BiomeDef. This excludes synthetic
             * defs (e.g. defaultBiome) and lets each biome be attributed to its source mod. */
            Dictionary<string, ModGroup> groupMap = new Dictionary<string, ModGroup>();
            foreach (BiomeResourceDef brd in DefDatabase<BiomeResourceDef>.AllDefsListForReading)
            {
                BiomeDef bd = DefDatabase<BiomeDef>.GetNamedSilentFail(brd.defName);
                if (bd is null) continue;

                string key = bd.modContentPack?.PackageId ?? "unknown";
                ModGroup group;
                if (!groupMap.TryGetValue(key, out group))
                {
                    group = new ModGroup
                    {
                        key = key,
                        label = bd.modContentPack?.ModMetaData?.Name ?? key,
                        biomes = new List<BiomeEntry>()
                    };
                    groupMap[key] = group;
                }
                group.biomes.Add(new BiomeEntry { resourceDef = brd, biomeDef = bd });
            }

            /* Sort: official mods (Core + DLC) first, then third-party alphabetically.
             * Within a group, biomes alphabetically by label. Expand all groups by default. */
            modGroups = groupMap.Values
                .OrderByDescending(g => IsOfficialGroup(g) ? 1 : 0)
                .ThenBy(g => g.label)
                .ToList();
            foreach (ModGroup g in modGroups)
            {
                g.biomes = g.biomes.OrderBy(e => e.biomeDef.LabelCap.RawText).ToList();
                foreach (BiomeEntry e in g.biomes)
                    e.foundable = GetFoundableSettlements(e).Count > 0;
                expandedGroups.Add(g.key);
            }

            if (modGroups.Count > 0 && modGroups[0].biomes.Count > 0)
                selectedEntry = modGroups[0].biomes[0];
        }

        private static bool IsOfficialGroup(ModGroup g)
        {
            foreach (BiomeEntry e in g.biomes)
            {
                if (e.biomeDef.modContentPack?.IsOfficialMod == true)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Switches selection to the biome backed by the given production def.
        /// Returns false if that biome isn't listed in this tab (e.g. it has no matching BiomeDef).
        /// </summary>
        public bool SelectDef(BiomeResourceDef def)
        {
            if (def is null) return false;
            foreach (ModGroup g in modGroups)
            {
                foreach (BiomeEntry e in g.biomes)
                {
                    if (e.resourceDef == def)
                    {
                        selectedEntry = e;
                        centerScroll = Vector2.zero;
                        rightScroll = Vector2.zero;
                        expandedGroups.Add(g.key);
                        return true;
                    }
                }
            }
            return false;
        }

        public void OnTabSelected() { }
        public void OnTabDeselected() { }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  LEFT PANE
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/
        public void DrawLeftPane(Rect rect)
        {
            float totalHeight = CalculateLeftPaneHeight();
            Rect viewRect = ScrollUtil.BeginScrollView(rect, ref leftScroll, totalHeight);
            float curY = 0f;

            foreach (ModGroup group in modGroups)
            {
                if (group.biomes.Count == 0) continue;

                bool isExpanded = expandedGroups.Contains(group.key);

                /* Group header */
                Rect groupRect = new Rect(0f, curY, viewRect.width, GroupHeaderHeight);
                Widgets.DrawBoxSolid(groupRect, GroupBgColor);
                TexLoad.DrawHorizontalGradient(groupRect, ColorUtil.TransformA(GroupAccentColor, 0.2f));
                Widgets.DrawBoxSolid(new Rect(0f, curY, AccentBarWidth, GroupHeaderHeight), GroupAccentColor);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect groupLabelRect = new Rect(AccentBarWidth + margin, curY, viewRect.width - AccentBarWidth - margin * 2 - 20f, GroupHeaderHeight);
                string groupLabel = Text.ClampTextWithEllipsis(groupLabelRect, group.label);
                UIUtil.DrawColoredLabel(groupLabelRect, groupLabel, ColorUtil.TransformRGB(GroupAccentColor, 1.3f));
                if (groupLabel != group.label)
                    TooltipHandler.TipRegion(groupRect, group.label);

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

                /* Biome entries */
                foreach (BiomeEntry entry in group.biomes)
                {
                    Rect entryRect = new Rect(10f, curY, viewRect.width - 10f, EntryRowHeight);
                    bool isSelected = selectedEntry == entry;

                    if (isSelected)
                        Widgets.DrawBoxSolid(entryRect, ColorUtil.TransformA(BiomeAccent, 0.35f));
                    else if (Mouse.IsOver(entryRect))
                        Widgets.DrawBoxSolid(entryRect, ColorUtil.TransformA(BiomeAccent, 0.15f));

                    // Biomes with no foundable settlement types (including non-settleable ones) are dimmed.
                    Color barColor = isSelected ? BiomeAccent : ColorUtil.TransformA(BiomeAccent, 0.4f);
                    if (!entry.foundable)
                        barColor = ColorUtil.TransformA(barColor, 0.4f);
                    Widgets.DrawBoxSolid(new Rect(entryRect.x, entryRect.y, 2f, entryRect.height), barColor);

                    float textX = entryRect.x + margin;
                    float labelWidth = entryRect.xMax - textX - SmallMargin;
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    string fullLabel = entry.biomeDef.LabelCap;
                    Rect labelRect = new Rect(textX, entryRect.y, labelWidth, entryRect.height);
                    string truncated = Text.ClampTextWithEllipsis(labelRect, fullLabel);
                    Color labelColor = isSelected ? Color.white : ColorUtil.Gray9;
                    if (!entry.foundable)
                        labelColor = ColorUtil.TransformA(labelColor, 0.5f);
                    UIUtil.DrawColoredLabel(labelRect, truncated, labelColor);

                    string tip = truncated != fullLabel ? fullLabel : null;
                    if (!entry.foundable)
                    {
                        string note = "FCCodexBiomeNoSettlements".Translate();
                        tip = tip.NullOrEmpty() ? note : tip + "\n" + note;
                    }
                    if (!tip.NullOrEmpty())
                        TooltipHandler.TipRegion(entryRect, tip);
                    ResetText();

                    if (Widgets.ButtonInvisible(entryRect))
                    {
                        selectedEntry = entry;
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
            foreach (ModGroup group in modGroups)
            {
                if (group.biomes.Count == 0) continue;
                total += GroupHeaderHeight + 2f;
                if (expandedGroups.Contains(group.key))
                    total += group.biomes.Count * EntryRowHeight;
            }
            return total;
        }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  CENTER PANE
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/
        public void DrawCenterPane(Rect rect)
        {
            if (selectedEntry is null)
            {
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.DrawColoredLabel(rect, "FCCodexSelectBiome".Translate(), Color.gray);
                ResetText();
                return;
            }

            float contentWidth = rect.width - ScrollUtil.ScrollbarWidth - 1f;
            float contentHeight = CalculateCenterHeight(contentWidth);

            Rect viewRect = ScrollUtil.BeginScrollView(rect, ref centerScroll, contentHeight);
            contentWidth = viewRect.width;
            float curY = 0f;

            /* Title */
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            UIUtil.ClampedLabel(new Rect(0f, curY, contentWidth, 30f), selectedEntry.biomeDef.LabelCap);
            ResetText();
            curY += 30f;

            TexLoad.DrawHorizontalGradient(new Rect(0f, curY, contentWidth, 2f), BiomeAccent);
            curY += 2f + margin;

            /* Description */
            string desc = GetDescription();
            if (!desc.NullOrEmpty())
            {
                Text.Font = GameFont.Small;
                Rect descRect = new Rect(0f, curY, contentWidth, 100f);
                Widgets.LabelCacheHeight(ref descRect, desc);
                curY += descRect.height + margin;
                ResetText();
            }

            /* Key Info */
            curY = DrawCollapsibleSection(curY, contentWidth, "FCCodexBiomeKeyInfo".Translate(), BiomeAccent,
                ref keyInfoExpanded, DrawKeyInfo);

            /* Resource Production */
            curY = DrawCollapsibleSection(curY, contentWidth, "FCCodexBiomeResourceProduction".Translate(), BiomeAccent,
                ref resourceProductionExpanded, DrawResourceProduction);

            /* Stat Modifiers */
            TaggedString statDesc = FCStatModifier.GetDescription(selectedEntry.resourceDef.statModifiers);
            if (!statDesc.RawText.NullOrEmpty())
                curY = DrawCollapsibleSection(curY, contentWidth, "FCCodexBiomeStatModifiers".Translate(), BiomeAccent,
                    ref statModifiersExpanded, (y, w) => DrawStatModifiers(y, w, statDesc));

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

            if (selectedEntry is object)
            {
                /* Banner */
                Texture2D banner = UIUtil.GetModBanner(selectedEntry.biomeDef.modContentPack);
                if (banner is object)
                {
                    Rect bannerRect = new Rect(0f, curY, contentWidth, BannerHeight);
                    GUI.DrawTexture(bannerRect, banner, ScaleMode.ScaleToFit);
                    curY += BannerHeight + SmallMargin;
                }

                string modName = selectedEntry.biomeDef.modContentPack?.ModMetaData?.Name ?? "";
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

                /* Foundable Settlement Types */
                curY = DrawSection(curY, contentWidth, "FCCodexBiomeSettlementTypes".Translate(), BiomeAccent,
                    DrawFoundableSettlements);
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
                curY = drawer(curY, width);

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

            curY = DrawStatLine(curY, x, textW, "FCCodexBiomeSettleable".Translate(
                selectedEntry.resourceDef.canSettle ? yesStr : noStr));

            return curY;
        }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  RESOURCE PRODUCTION
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/
        private float DrawResourceProduction(float curY, float width)
        {
            float x = AccentBarWidth + margin;
            float tableW = width - x - margin;

            List<ResourceTypeDef> rowResources = new List<ResourceTypeDef>();
            List<string> resourceNames = new List<string>();
            List<string> additives = new List<string>();
            List<string> multipliers = new List<string>();
            List<string> totals = new List<string>();

            foreach (ResourceTypeDef res in FactionCache.SortedResourceTypeDefsForUI)
            {
                ResourceAvailability ra = selectedEntry.resourceDef.GetBiomeResource(res);
                if (ra is null) continue;

                double additive = double.IsNaN(ra.additive) ? 0 : ra.additive;
                double multiplier = ra.multiplier;
                double total = additive * multiplier;

                string resourceS = res.LabelCap;
                if (total > 1)
                    resourceS = resourceS.Colorize(Color.green);
                else if (total < 1)
                    resourceS = resourceS.Colorize(Color.red);

                rowResources.Add(res);
                resourceNames.Add(resourceS);
                additives.Add(TextUtil.ColorizeBonus(Math.Round(additive, 2), 1.0));
                multipliers.Add(TextUtil.ColorizeBonus(Math.Round(multiplier, 2), 1.0));
                totals.Add(TextUtil.ColorizeBonus(Math.Round(total, 2), 1.0));
            }

            if (rowResources.Count > 0)
            {
                Rect tableRect = new Rect(x, curY, tableW, 0f);
                int clickedRow;
                curY += UIUtil.DrawTable(tableRect, out clickedRow,
                    "FCCodexBiomeResourceColumn".Translate(), resourceNames,
                    "FCCodexResourceBiomeAdditive".Translate(), additives,
                    "FCCodexResourceBiomeMultiplier".Translate(), multipliers,
                    "FCCodexResourceBiomeTotal".Translate(), totals);
                curY += SmallMargin;

                if (clickedRow >= 0 && clickedRow < rowResources.Count)
                {
                    parentWindow.SelectResource(rowResources[clickedRow]);
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
            }

            return curY;
        }

        private int CountProductionRows()
        {
            int count = 0;
            foreach (ResourceTypeDef res in FactionCache.SortedResourceTypeDefsForUI)
            {
                if (selectedEntry.resourceDef.GetBiomeResource(res) is object)
                    count++;
            }
            return count;
        }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  STAT MODIFIERS
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/
        private float DrawStatModifiers(float curY, float width, TaggedString desc)
        {
            float x = AccentBarWidth + margin;
            float textW = width - x - margin;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            float h = Text.CalcHeight(desc, textW);
            Widgets.Label(new Rect(x, curY, textW, h), desc);
            ResetText();

            return curY + h;
        }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  FOUNDABLE SETTLEMENTS
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/
        private List<WorldSettlementDef> GetFoundableSettlements(BiomeEntry entry)
        {
            List<WorldSettlementDef> result = new List<WorldSettlementDef>();
            if (entry is null || !entry.resourceDef.canSettle) return result;

            BiomeDef bd = entry.biomeDef;
            foreach (WorldSettlementDef def in FactionCache.AvailableWorldSettlementDefs)
            {
                if (!def.CanFoundOnSurface) continue; // orbital/non-surface types aren't foundable in any (surface) biome
                if (def.allowedBiomes != null && def.allowedBiomes.Count > 0 && !def.allowedBiomes.Contains(bd))
                    continue;
                if (def.blockedBiomes != null && def.blockedBiomes.Contains(bd))
                    continue;
                result.Add(def);
            }
            result.Sort((a, b) => string.Compare(a.LabelCap.RawText, b.LabelCap.RawText, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        private float DrawFoundableSettlements(float curY, float width)
        {
            float x = AccentBarWidth + margin;

            List<WorldSettlementDef> foundable = GetFoundableSettlements(selectedEntry);
            if (foundable.Count == 0)
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Rect noneRect = new Rect(x, curY, width - x - margin, StatRowHeight);
                string msg = "FCCodexBiomeNoSettlements".Translate();
                Widgets.LabelCacheHeight(ref noneRect, msg);
                UIUtil.DrawColoredLabel(noneRect, msg, Color.gray);
                ResetText();
                return curY + noneRect.height;
            }

            foreach (WorldSettlementDef def in foundable)
            {
                Rect rowRect = new Rect(x, curY, width - x - margin, SettlementRowHeight);
                bool isHover = Mouse.IsOver(rowRect);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.DrawColoredLabel(rowRect, def.LabelCap, isHover ? HighlightColor : Color.white);
                ResetText();

                if (isHover)
                    Widgets.DrawHighlight(rowRect);

                if (Widgets.ButtonInvisible(rowRect))
                {
                    parentWindow.SelectSettlement(def);
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }

                curY += SettlementRowHeight;
            }

            return curY;
        }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  DESCRIPTION HELPER
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/
        private string GetDescription()
        {
            if (selectedEntry is null) return "";
            string desc = selectedEntry.biomeDef.description;
            if (desc.NullOrEmpty()
                && !selectedEntry.resourceDef.descriptionKey.NullOrEmpty()
                && selectedEntry.resourceDef.descriptionKey.CanTranslate())
                desc = selectedEntry.resourceDef.descriptionKey.Translate();
            return desc;
        }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  HEIGHT CALCULATIONS
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/
        private float CalculateCenterHeight(float width)
        {
            if (selectedEntry is null) return 0f;

            float total = 30f + 2f + margin; // title + accent line

            string desc = GetDescription();
            if (!desc.NullOrEmpty())
            {
                Text.Font = GameFont.Small;
                total += Text.CalcHeight(desc, width) + margin;
            }

            // Key Info section (always present): 1 line
            total += SectionHeaderHeight + SmallMargin;
            if (keyInfoExpanded)
                total += StatRowHeight;
            total += margin;

            // Resource Production section
            total += SectionHeaderHeight + SmallMargin;
            if (resourceProductionExpanded)
            {
                int rows = CountProductionRows();
                if (rows > 0)
                    total += UIUtil.TableHeight(rows) + SmallMargin;
            }
            total += margin;

            // Stat Modifiers section
            TaggedString statDesc = FCStatModifier.GetDescription(selectedEntry.resourceDef.statModifiers);
            if (!statDesc.RawText.NullOrEmpty())
            {
                total += SectionHeaderHeight + SmallMargin;
                if (statModifiersExpanded)
                    total += Text.CalcHeight(statDesc, width - AccentBarWidth - margin * 2);
                total += margin;
            }

            return total + 50f;
        }

        private float CalculateRightPaneHeight(float width)
        {
            float total = 0f;

            if (selectedEntry is object)
            {
                if (UIUtil.GetModBanner(selectedEntry.biomeDef.modContentPack) is object)
                    total += BannerHeight + SmallMargin;
                string modName = selectedEntry.biomeDef.modContentPack?.ModMetaData?.Name ?? "";
                if (!modName.NullOrEmpty())
                    total += 24f;
                total += margin; // divider

                // Foundable Settlement Types section
                total += SectionHeaderHeight + SmallMargin;
                List<WorldSettlementDef> foundable = GetFoundableSettlements(selectedEntry);
                if (foundable.Count > 0)
                    total += foundable.Count * SettlementRowHeight;
                else
                    total += StatRowHeight * 2f; // wrapped "no settlements" message
                total += margin;
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
