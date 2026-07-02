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
    /// The "Info" tab in the Codex — a browsable reference of game mechanics.
    /// Left pane: mod group → category → entry tree.
    /// Center pane: entry detail (title, description, see-also).
    /// Right pane: mod banner + dynamic content.
    /// </summary>
    public class CodexTab_Info : ICodexTab
    {
        /* Layout constants */
        private const float GroupHeaderHeight = 30f;
        private const float GroupHeaderVPad = 8f;      // 22f line + 8f = 30f, matches current single-line group header
        private const float CategoryHeaderHeight = 26f;
        private const float CategoryHeaderVPad = 4f;   // 22f line + 4f = 26f, matches current single-line category header
        private const float EntryRowHeight = 24f;
        private const float ImageMaxHeight = 300f;
        private const float ImageNavButtonSize = 28f;
        private const float SeeAlsoButtonHeight = 24f;
        private const float IconSize = 20f;
        private const float BannerHeight = 70f;
        private const float TitleIconSize = 28f;
        private const float DynamicHeaderHeight = 26f;
        private const float AccentBarWidth = 3f;
        private const float Margin = 8f;

        private static readonly Color GroupBgColor = new Color(0.2f, 0.2f, 0.2f, 0.6f);
        private static readonly Color CategoryBgColor = new Color(0.15f, 0.15f, 0.15f, 0.4f);
        private static readonly Color DynamicContentBg = new Color(0.12f, 0.18f, 0.12f, 0.3f);
        private static readonly Color SeeAlsoColor = new Color(0.4f, 0.6f, 0.9f);

        /* Data model */
        private readonly List<ModGroup> modGroups;
        private CodexEntryDef selectedEntry;

        /* Scroll state */
        private Vector2 leftScroll;
        private Vector2 centerScroll;
        private Vector2 rightScroll;

        /* Image carousel */
        private int currentImageIndex;

        /* Expand/collapse state */
        private readonly HashSet<string> expandedMods = new HashSet<string>();
        private readonly HashSet<string> expandedCategories = new HashSet<string>();
        private bool dynamicSectionExpanded = true;

        /* Truncation cache */
        private readonly Dictionary<string, string> truncateCache = new Dictionary<string, string>();

        private class ModGroup
        {
            public string modId;
            public string modName;
            public List<CategoryGroup> categories = new List<CategoryGroup>();
        }

        private class CategoryGroup
        {
            public CodexCategoryDef categoryDef;
            public string modId;
            public List<CodexEntryDef> entries = new List<CodexEntryDef>();
        }

        public string TabLabel => "FCCodexTabInfo".Translate();
        public bool HasRightPane => true;

        public CodexTab_Info()
        {
            modGroups = BuildModGroups();

            if (modGroups.Count > 0)
            {
                expandedMods.Add(modGroups[0].modId);
                foreach (CategoryGroup cat in modGroups[0].categories)
                    expandedCategories.Add(CatKey(cat));

                if (modGroups[0].categories.Count > 0 && modGroups[0].categories[0].entries.Count > 0)
                    SelectEntry(modGroups[0].categories[0].entries[0]);
            }
        }

        public void SelectEntry(CodexEntryDef entry)
        {
            selectedEntry = entry;
            currentImageIndex = 0;
            centerScroll = Vector2.zero;
            rightScroll = Vector2.zero;

            if (entry.category is object)
            {
                expandedMods.Add(entry.category.modId);
                expandedCategories.Add(CatKey(entry.category.modId, entry.category.defName));
            }
        }

        public void OnTabSelected() { }
        public void OnTabDeselected() { }

        private static string CatKey(CategoryGroup cg) => cg.modId + "|" + cg.categoryDef.defName;
        private static string CatKey(string modId, string catDefName) => modId + "|" + catDefName;

        private static List<ModGroup> BuildModGroups()
        {
            List<CodexEntryDef> allDefs = DefDatabase<CodexEntryDef>.AllDefsListForReading.ToList();

            var byMod = new Dictionary<string, ModGroup>();
            foreach (CodexEntryDef def in allDefs)
            {
                if (def.category is null) continue;
                string mid = def.category.modId.NullOrEmpty() ? "unknown" : def.category.modId;
                ModGroup mg;
                if (!byMod.TryGetValue(mid, out mg))
                {
                    mg = new ModGroup { modId = mid, modName = def.category.ModName };
                    byMod[mid] = mg;
                }

                CategoryGroup cg = mg.categories.FirstOrDefault(c => c.categoryDef == def.category);
                if (cg is null)
                {
                    cg = new CategoryGroup { categoryDef = def.category, modId = mid };
                    mg.categories.Add(cg);
                }
                cg.entries.Add(def);
            }

            foreach (ModGroup mg in byMod.Values)
            {
                foreach (CategoryGroup cg in mg.categories)
                    cg.entries.SortBy(e => e.displayOrder);
                mg.categories.SortBy(c => c.categoryDef.displayOrder);
            }

            List<ModGroup> result = byMod.Values.ToList();
            result.SortBy(mg => mg.modId == "matathias.empire" ? "!" : mg.modName);
            return result;
        }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  LEFT PANE: mod groups → categories → entries
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/
        public void DrawLeftPane(Rect rect)
        {
            float measureWidth = rect.width - ScrollUtil.ScrollbarWidth - 1f;
            float totalHeight = CalculateLeftPaneHeight(measureWidth);
            Rect viewRect = ScrollUtil.BeginScrollView(rect, ref leftScroll, totalHeight);
            float curY = 0f;

            foreach (ModGroup mg in modGroups)
            {
                bool modExpanded = expandedMods.Contains(mg.modId);

                float groupHeight = HeaderHeightFor(mg.modName, viewRect.width - Margin * 2 - IconSize, GroupHeaderHeight, GroupHeaderVPad);
                Rect groupRect = new Rect(0f, curY, viewRect.width, groupHeight);
                Widgets.DrawBoxSolid(groupRect, GroupBgColor);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = Color.white;
                Widgets.Label(new Rect(groupRect.x + Margin, groupRect.y, groupRect.width - Margin * 2 - IconSize, groupRect.height), mg.modName);

                Rect arrowRect = new Rect(groupRect.xMax - IconSize - 2f, groupRect.y + (groupHeight - IconSize) * 0.5f, IconSize, IconSize);
                Widgets.DrawTextureFitted(arrowRect, modExpanded ? TexButton.Collapse : TexButton.Reveal, 1f);

                if (Widgets.ButtonInvisible(groupRect))
                {
                    if (modExpanded) expandedMods.Remove(mg.modId);
                    else expandedMods.Add(mg.modId);
                    (modExpanded ? SoundDefOf.TabClose : SoundDefOf.TabOpen).PlayOneShotOnCamera();
                }

                curY += groupHeight + 2f;
                if (!modExpanded) continue;

                foreach (CategoryGroup cg in mg.categories)
                {
                    string catKey = CatKey(cg);
                    bool catExpanded = expandedCategories.Contains(catKey);
                    Color catColor = cg.categoryDef.color;

                    float catHeight = HeaderHeightFor(cg.categoryDef.LabelCap, (viewRect.width - 10f) - Margin * 2 - IconSize - 3f, CategoryHeaderHeight, CategoryHeaderVPad);
                    Rect catRect = new Rect(10f, curY, viewRect.width - 10f, catHeight);
                    Widgets.DrawBoxSolid(catRect, CategoryBgColor);
                    TexLoad.DrawHorizontalGradient(catRect, ColorUtil.TransformA(catColor, 0.2f));
                    Widgets.DrawBoxSolid(new Rect(catRect.x, catRect.y, 3f, catRect.height), catColor);

                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    UIUtil.DrawColoredLabel(
                        new Rect(catRect.x + Margin + 3f, catRect.y, catRect.width - Margin * 2 - IconSize - 3f, catRect.height),
                        cg.categoryDef.LabelCap,
                        ColorUtil.TransformRGB(catColor, 1.3f));

                    Rect catArrow = new Rect(catRect.xMax - IconSize - 2f, catRect.y + (catHeight - IconSize) * 0.5f, IconSize, IconSize);
                    Widgets.DrawTextureFitted(catArrow, catExpanded ? TexButton.Collapse : TexButton.Reveal, 1f);

                    if (Widgets.ButtonInvisible(catRect))
                    {
                        if (catExpanded) expandedCategories.Remove(catKey);
                        else expandedCategories.Add(catKey);
                        (catExpanded ? SoundDefOf.TabClose : SoundDefOf.TabOpen).PlayOneShotOnCamera();
                    }

                    curY += catHeight + 1f;
                    if (!catExpanded) continue;

                    foreach (CodexEntryDef entry in cg.entries)
                    {
                        Rect entryRect = new Rect(20f, curY, viewRect.width - 20f, EntryRowHeight);
                        bool isSelected = selectedEntry == entry;

                        if (isSelected)
                            Widgets.DrawBoxSolid(entryRect, ColorUtil.TransformA(catColor, 0.35f));
                        else if (Mouse.IsOver(entryRect))
                            Widgets.DrawBoxSolid(entryRect, ColorUtil.TransformA(catColor, 0.15f));

                        Color barColor = isSelected ? catColor : ColorUtil.TransformA(catColor, 0.4f);
                        Widgets.DrawBoxSolid(new Rect(entryRect.x, entryRect.y, 2f, entryRect.height), barColor);

                        float textX = entryRect.x + Margin;
                        if (entry.Icon is object)
                        {
                            Rect iconRect = new Rect(entryRect.x + 4f, entryRect.y + (EntryRowHeight - 16f) * 0.5f, 16f, 16f);
                            GUI.DrawTexture(iconRect, entry.Icon);
                            textX = iconRect.xMax + 4f;
                        }

                        Text.Font = GameFont.Small;
                        Text.Anchor = TextAnchor.MiddleLeft;
                        float labelWidth = entryRect.xMax - textX - 4f;
                        string fullLabel = entry.LabelCap;
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
                            SelectEntry(entry);
                            SoundDefOf.Click.PlayOneShotOnCamera();
                        }

                        curY += EntryRowHeight;
                    }
                }
            }

            ScrollUtil.EndScrollView();
            ResetText();
        }

        private float CalculateLeftPaneHeight(float contentWidth)
        {
            float total = 0f;
            foreach (ModGroup mg in modGroups)
            {
                total += HeaderHeightFor(mg.modName, contentWidth - Margin * 2 - IconSize, GroupHeaderHeight, GroupHeaderVPad) + 2f;
                if (!expandedMods.Contains(mg.modId)) continue;
                foreach (CategoryGroup cg in mg.categories)
                {
                    total += HeaderHeightFor(cg.categoryDef.LabelCap, (contentWidth - 10f) - Margin * 2 - IconSize - 3f, CategoryHeaderHeight, CategoryHeaderVPad) + 1f;
                    if (expandedCategories.Contains(CatKey(cg)))
                        total += cg.entries.Count * EntryRowHeight;
                }
            }
            return total;
        }

        /// <summary>
        /// Height of a tree header row, grown to fit its full (wrapped) label at the given
        /// available label width. Never shrinks below <paramref name="minHeight"/>, so
        /// single-line labels keep the original look. <see cref="Text.CalcHeight"/> reads
        /// the active font, so this sets <see cref="GameFont.Small"/> before measuring.
        /// </summary>
        private static float HeaderHeightFor(string label, float labelWidth, float minHeight, float vPad)
        {
            if (label.NullOrEmpty()) return minHeight;
            Text.Font = GameFont.Small;
            if (labelWidth < 1f) labelWidth = 1f;
            float textHeight = Text.CalcHeight(label, labelWidth);
            return Mathf.Max(minHeight, textHeight + vPad);
        }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  CENTER PANE: entry detail (title, description, see-also)
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/
        public void DrawCenterPane(Rect rect)
        {
            if (selectedEntry is null)
            {
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.DrawColoredLabel(rect, "FCCodexSelectEntry".Translate(), Color.gray);
                ResetText();
                return;
            }

            float contentHeight = CalculateCenterPaneHeight(rect.width - ScrollUtil.ScrollbarWidth - 1f);
            Color catColor = selectedEntry.category.color;

            Rect viewRect = ScrollUtil.BeginScrollView(rect, ref centerScroll, contentHeight);
            float contentWidth = viewRect.width;
            float curY = 0f;

            /* Title with icon */
            float titleTextX = 0f;
            if (selectedEntry.Icon is object)
            {
                Rect iconBgRect = new Rect(0f, curY, TitleIconSize, TitleIconSize);
                Widgets.DrawBoxSolid(iconBgRect, ColorUtil.TransformA(catColor, 0.25f));
                UIUtil.DrawColoredBox(iconBgRect, ColorUtil.TransformA(catColor, 0.6f));
                GUI.DrawTexture(iconBgRect.ContractedBy(3f), selectedEntry.Icon);
                titleTextX = TitleIconSize + Margin;
            }

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            UIUtil.ClampedLabel(new Rect(titleTextX, curY, contentWidth - titleTextX, 30f), selectedEntry.LabelCap);
            ResetText();

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            string meta = selectedEntry.category.LabelCap + "  \u2022  " + selectedEntry.category.ModName;
            float metaY = curY + (selectedEntry.Icon is object ? 26f : 30f);
            UIUtil.DrawColoredLabel(new Rect(titleTextX, metaY, contentWidth - titleTextX, 20f), meta, Color.gray);
            ResetText();
            curY = metaY + 22f;

            /* Gradient accent line */
            TexLoad.DrawHorizontalGradient(new Rect(0f, curY, contentWidth, 2f), catColor);
            curY += 2f + Margin;

            /* Image carousel */
            List<Texture2D> images = selectedEntry.Images;
            if (images.Count > 0)
            {
                curY = DrawImageCarousel(curY, contentWidth, images);
                curY += Margin;
            }

            /* Description */
            if (!selectedEntry.description.NullOrEmpty())
            {
                Text.Font = GameFont.Small;
                Rect descRect = new Rect(0f, curY, contentWidth, 100f);
                Widgets.LabelCacheHeight(ref descRect, selectedEntry.FormattedDesc);
                curY += descRect.height + Margin;
                ResetText();
            }

            /* See Also links */
            if (!selectedEntry.seeAlso.NullOrEmpty())
            {
                curY += Margin;
                Text.Font = GameFont.Small;
                UIUtil.DrawColoredLabel(new Rect(0f, curY, contentWidth, 20f), "FCCodexSeeAlso".Translate(), Color.gray);
                ResetText();
                curY += 22f;

                foreach (CodexEntryDef linked in selectedEntry.seeAlso)
                {
                    if (linked is null) continue;

                    Rect linkRect = new Rect(0f, curY, contentWidth, SeeAlsoButtonHeight);
                    Widgets.DrawBoxSolid(linkRect, new Color(0.39f, 0.67f, 1f, 0.06f));
                    Widgets.DrawBoxSolid(new Rect(0f, curY, AccentBarWidth, SeeAlsoButtonHeight), SeeAlsoColor);

                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    UIUtil.DrawColoredLabel(
                        new Rect(AccentBarWidth + Margin, curY, contentWidth - AccentBarWidth - Margin, SeeAlsoButtonHeight),
                        "\u2192 " + linked.LabelCap,
                        SeeAlsoColor);

                    if (Mouse.IsOver(linkRect))
                        Widgets.DrawHighlight(linkRect);

                    if (Widgets.ButtonInvisible(linkRect))
                    {
                        SelectEntry(linked);
                        SoundDefOf.Click.PlayOneShotOnCamera();
                    }

                    ResetText();
                    curY += SeeAlsoButtonHeight + 2f;
                }
            }

            ScrollUtil.EndScrollView();
            ResetText();
        }

        private float DrawImageCarousel(float startY, float width, List<Texture2D> images)
        {
            float curY = startY;
            currentImageIndex = Mathf.Clamp(currentImageIndex, 0, images.Count - 1);
            Texture2D img = images[currentImageIndex];

            float aspect = (float)img.width / img.height;
            float drawWidth = width;
            float drawHeight = drawWidth / aspect;
            if (drawHeight > ImageMaxHeight)
            {
                drawHeight = ImageMaxHeight;
                drawWidth = drawHeight * aspect;
            }

            float imgX = (width - drawWidth) * 0.5f;
            GUI.DrawTexture(new Rect(imgX, curY, drawWidth, drawHeight), img, ScaleMode.ScaleToFit);
            curY += drawHeight + 4f;

            if (images.Count > 1)
            {
                float navWidth = ImageNavButtonSize * 2 + 60f;
                float navX = (width - navWidth) * 0.5f;

                Rect leftBtn = new Rect(navX, curY, ImageNavButtonSize, ImageNavButtonSize);
                if (currentImageIndex > 0 && UIUtil.ClampedButtonText(leftBtn, "<"))
                {
                    currentImageIndex--;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.ClampedLabel(new Rect(leftBtn.xMax, curY, 60f, ImageNavButtonSize), (currentImageIndex + 1) + " / " + images.Count);
                ResetText();

                Rect rightBtn = new Rect(leftBtn.xMax + 60f, curY, ImageNavButtonSize, ImageNavButtonSize);
                if (currentImageIndex < images.Count - 1 && UIUtil.ClampedButtonText(rightBtn, ">"))
                {
                    currentImageIndex++;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }

                curY += ImageNavButtonSize + 2f;
            }

            return curY;
        }

        private float CalculateCenterPaneHeight(float width)
        {
            if (selectedEntry is null) return 0f;

            float total = 30f + 22f + 2f + Margin; // title + meta + accent line

            List<Texture2D> images = selectedEntry.Images;
            if (images.Count > 0)
            {
                Texture2D img = images[Mathf.Clamp(currentImageIndex, 0, images.Count - 1)];
                float aspect = (float)img.width / img.height;
                total += Mathf.Min(width / aspect, ImageMaxHeight) + 4f + Margin;
                if (images.Count > 1)
                    total += ImageNavButtonSize + 2f;
            }

            if (!selectedEntry.description.NullOrEmpty())
                total += Text.CalcHeight(selectedEntry.FormattedDesc, width) + Margin;

            if (!selectedEntry.seeAlso.NullOrEmpty())
                total += Margin + 22f + selectedEntry.seeAlso.Count * (SeeAlsoButtonHeight + 2f);

            return total + 50f;
        }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  RIGHT PANE: mod banner + dynamic content
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/
        public void DrawRightPane(Rect rect)
        {
            float contentHeight = CalculateRightPaneHeight(rect.width - ScrollUtil.ScrollbarWidth - 1f);

            Rect viewRect = ScrollUtil.BeginScrollView(rect, ref rightScroll, contentHeight);
            float contentWidth = viewRect.width;
            float curY = 0f;

            /* Banner */
            if (selectedEntry is object && selectedEntry.category is object)
            {
                Texture2D banner = selectedEntry.category.BannerImage;
                if (banner is object)
                {
                    Rect bannerRect = new Rect(0f, curY, contentWidth, BannerHeight);
                    GUI.DrawTexture(bannerRect, banner, ScaleMode.ScaleToFit);
                    curY += BannerHeight + 4f;
                }

                // Mod name (wraps to fit long names, mirroring the left pane's HeaderHeightFor sizing)
                float nameHeight = HeaderHeightFor(selectedEntry.category.ModName, contentWidth, 20f, 0f);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.DrawColoredLabel(new Rect(0f, curY, contentWidth, nameHeight), selectedEntry.category.ModName, Color.gray, false);
                ResetText();
                curY += nameHeight + 4f;

                UIUtil.DrawColoredHorizontalLine(Margin, curY, contentWidth - Margin * 2, Color.gray);
                curY += Margin;
            }

            /* Dynamic content */
            if (selectedEntry is object)
            {
                ICodexDynamicProvider provider = selectedEntry.DynamicProvider;
                if (provider is object)
                {
                    FactionFC faction = FindFC.FactionComp;
                    if (faction is object)
                    {
                        string dynamic = null;
                        try
                        {
                            dynamic = provider.GetDynamicContent(faction);
                        }
                        catch (Exception ex)
                        {
                            LogUtil.Error($"CodexTab_Info: dynamic provider for '{selectedEntry.defName}' threw: {ex}");
                        }

                        if (!dynamic.NullOrEmpty())
                        {
                            Color dynColor = new Color(0.5f, 0.93f, 0.5f);

                            Rect headerRect = new Rect(0f, curY, contentWidth, DynamicHeaderHeight);
                            Widgets.DrawBoxSolid(headerRect, new Color(0.17f, 0.17f, 0.17f, 1f));
                            TexLoad.DrawHorizontalGradient(headerRect, ColorUtil.TransformA(dynColor, 0.15f));
                            Widgets.DrawBoxSolid(new Rect(0f, curY, AccentBarWidth, DynamicHeaderHeight), dynColor);

                            Text.Font = GameFont.Small;
                            Text.Anchor = TextAnchor.MiddleLeft;
                            string arrow = dynamicSectionExpanded ? "\u25BC " : "\u25B6 ";
                            UIUtil.DrawColoredLabel(
                                new Rect(AccentBarWidth + Margin, curY, contentWidth - AccentBarWidth - Margin, DynamicHeaderHeight),
                                arrow + "FCCodexLiveData".Translate(),
                                dynColor);
                            ResetText();

                            if (Widgets.ButtonInvisible(headerRect))
                            {
                                dynamicSectionExpanded = !dynamicSectionExpanded;
                                (dynamicSectionExpanded ? SoundDefOf.TabOpen : SoundDefOf.TabClose).PlayOneShotOnCamera();
                            }
                            curY += DynamicHeaderHeight;

                            if (dynamicSectionExpanded)
                            {
                                Text.Font = GameFont.Small;
                                float dynHeight = Text.CalcHeight(dynamic, contentWidth - AccentBarWidth - Margin * 2);
                                float bodyHeight = dynHeight + Margin;
                                Widgets.DrawBoxSolid(new Rect(0f, curY, contentWidth, bodyHeight), DynamicContentBg);
                                Widgets.DrawBoxSolid(new Rect(0f, curY, AccentBarWidth, bodyHeight), ColorUtil.TransformA(dynColor, 0.3f));
                                Widgets.Label(new Rect(AccentBarWidth + Margin, curY + Margin * 0.5f, contentWidth - AccentBarWidth - Margin * 2, dynHeight), dynamic);
                                ResetText();
                                curY += bodyHeight;
                            }

                            UIUtil.DrawColoredHorizontalLine(0f, curY, contentWidth, ColorUtil.Gray3);
                            curY += Margin;
                        }
                    }
                }
            }

            ScrollUtil.EndScrollView();
            ResetText();
        }

        private float CalculateRightPaneHeight(float width)
        {
            float total = 0f;

            if (selectedEntry is object && selectedEntry.category is object)
            {
                if (selectedEntry.category.BannerImage is object)
                    total += BannerHeight + 4f;
                float nameHeight = HeaderHeightFor(selectedEntry.category.ModName, width, 20f, 0f);
                total += nameHeight + 4f + Margin; // mod name (may wrap) + divider
            }

            if (selectedEntry is object && selectedEntry.DynamicProvider is object)
            {
                total += DynamicHeaderHeight;
                if (dynamicSectionExpanded)
                    total += 200f;
                total += Margin;
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
