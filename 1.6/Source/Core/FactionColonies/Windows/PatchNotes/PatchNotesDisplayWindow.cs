using FactionColonies.util;
using LudeonTK;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace FactionColonies
{
    public static class DebugActionsMisc
    {
        [DebugAction("Mods", "Display Empire patch notes", allowedGameStates = AllowedGameStates.Entry)]
        public static void PatchNotesDisplayWindow() => Find.WindowStack.Add(new PatchNotesDisplayWindow());
    }

    public class PatchNotesDisplayWindow : Window
    {
        private class PatchNoteGroup
        {
            public string versionLabel;
            public string dateRange;
            public PatchNoteType highestSeverity;
            public bool hasNewEntries;
            public List<PatchNoteDef> entries;
        }

        public override Vector2 InitialSize => new Vector2(750f + (StandardMargin * 2), 750f + (StandardMargin * 2));

        private const float HeaderHeight = 45f;
        private const float GroupHeaderHeight = 35f;
        private const float TitleBarHeight = 30f;
        private const float margin = 5f;
        private const float DividerPad = 15f;
        private const float BadgeWidth = 55f;
        private const float BadgeHeight = 22f;
        private const float DateWidth = 90f;
        private const float IconSize = 45f;
        private const float LinkButtonSize = 24f;
        private const float BannerHeight = 120f;
        private const float EntryIndent = 10f;
        private const float NewIndicatorWidth = 4f;

        private static readonly Color GroupBgColor = new Color(0.15f, 0.15f, 0.15f, 0.6f);

        private static List<PatchNoteDef> cachedPatchNoteDefs;

        private static List<PatchNoteDef> GetPatchNoteDefs()
        {
            if (cachedPatchNoteDefs == null)
            {
                cachedPatchNoteDefs = DefDatabase<PatchNoteDef>.AllDefsListForReading.ListFullCopy();
                cachedPatchNoteDefs.SortByDescending(def => def.VersionSortKey);
            }
            return cachedPatchNoteDefs;
        }

        private readonly string modId;
        private readonly List<PatchNoteGroup> groups;

        private Texture2D bannerImage;

        private string title = "FCPatchNotesWindowTitle".Translate();

        // Scroll state
        private HashSet<int> expandedGroups = new HashSet<int>();
        private HashSet<int> expandedEntries = new HashSet<int>();
        private Dictionary<int, float> entryBodyHeights = new Dictionary<int, float>();
        private bool shouldRefreshHeight = true;
        private float scrollViewHeight = 0f;
        private Vector2 patchNoteScrollPos = new Vector2();

        // Scrolling bug fix
        private bool firstRun = true;
        private bool fixDone = false;

        // Badge colors
        private static readonly Color BadgeColorMajor = new Color(0.85f, 0.65f, 0.13f);
        private static readonly Color BadgeColorMinor = new Color(0.3f, 0.5f, 0.9f);
        private static readonly Color BadgeColorHotfix = new Color(0.9f, 0.2f, 0.2f);
        private static readonly Color BadgeColorPatch = new Color(0.5f, 0.5f, 0.5f);

        public PatchNotesDisplayWindow(string modId = "matathias.empire")
        {
            this.modId = modId;
            List<PatchNoteDef> allDefs = GetPatchNoteDefs();
            List<PatchNoteDef> filtered = allDefs.Where(d => d.modId == modId).ToList();

            FCSettings.GetLastSeenVersion(modId, out int lsMajor, out int lsMinor, out int lsPatch);
            groups = BuildGroups(filtered, lsMajor, lsMinor, lsPatch);

            // Auto-expand groups with new entries, and individual new entries within them
            for (int gi = 0; gi < groups.Count; gi++)
            {
                if (groups[gi].hasNewEntries)
                {
                    expandedGroups.Add(gi);
                    foreach (PatchNoteDef def in groups[gi].entries)
                    {
                        if (def.IsNewerThan(lsMajor, lsMinor, lsPatch))
                        {
                            expandedEntries.Add(def.VersionSortKey);
                        }
                    }
                }
            }
        }

        public PatchNotesDisplayWindow(string modId, string title) : this(modId) => this.title = title;

        private List<PatchNoteGroup> BuildGroups(List<PatchNoteDef> sortedDefs, int lsMajor, int lsMinor, int lsPatch)
        {
            var result = new List<PatchNoteGroup>();
            if (sortedDefs.Count == 0) return result;

            int currentMajor = -1;
            int currentMinor = -1;
            PatchNoteGroup current = null;

            for (int i = 0; i < sortedDefs.Count; i++)
            {
                PatchNoteDef def = sortedDefs[i];
                if (def.Major != currentMajor || def.Minor != currentMinor)
                {
                    if (current != null) result.Add(current);
                    currentMajor = def.Major;
                    currentMinor = def.Minor;
                    current = new PatchNoteGroup
                    {
                        versionLabel = "v" + def.Major + "." + def.Minor,
                        highestSeverity = PatchNoteType.Undefined,
                        hasNewEntries = false,
                        entries = new List<PatchNoteDef>()
                    };
                }

                current.entries.Add(def);

                if (def.GetPatchNoteType > current.highestSeverity)
                    current.highestSeverity = def.GetPatchNoteType;

                if (def.IsNewerThan(lsMajor, lsMinor, lsPatch))
                    current.hasNewEntries = true;
            }
            if (current != null) result.Add(current);

            // Compute date ranges
            foreach (PatchNoteGroup g in result)
            {
                System.DateTime oldest = g.entries[g.entries.Count - 1].ReleaseDate;
                System.DateTime newest = g.entries[0].ReleaseDate;
                if (oldest.Date == newest.Date)
                    g.dateRange = newest.ToString("dd MMM yyyy");
                else
                    g.dateRange = oldest.ToString("dd MMM") + " - " + newest.ToString("dd MMM yyyy");
            }

            return result;
        }

        public override void PostClose()
        {
            base.PostClose();
            if (groups.Count > 0 && groups[0].entries.Count > 0)
            {
                PatchNoteDef latest = groups[0].entries[0];
                FCSettings.SetLastSeenVersion(modId, latest.Major, latest.Minor, latest.Patch);
                LoadedModManager.GetMod<FactionColoniesMod>().WriteSettings();
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect titleRect = new Rect(inRect.x + margin, inRect.y, inRect.width - margin * 2, TitleBarHeight);
            float bannerTop = inRect.y + TitleBarHeight + DividerPad;
            Rect bannerRect = new Rect(inRect.x + margin, bannerTop, inRect.width - margin * 2, BannerHeight);
            float contentTop = bannerTop + BannerHeight + margin;
            float contentHeight = inRect.height - (contentTop - inRect.y);
            Rect contentPanel = new Rect(inRect.x + margin, contentTop, inRect.width - margin * 2, contentHeight);

            FixScrollingBug();
            CalculateScrollViewSize();
            DrawTitle(titleRect);
            DrawHorizontalDivider(inRect);
            DrawBanner(bannerRect);
            DrawPatchNotes(contentPanel);
        }

        private void FixScrollingBug()
        {
            if (fixDone) return;

            if (!firstRun)
            {
                shouldRefreshHeight = true;
                fixDone = true;
            }
            else
            {
                firstRun = false;
            }
        }

        private void DrawTitle(Rect titleRect)
        {
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            UIUtil.ClampedLabel(titleRect, title);

            // Link buttons in title bar (right-aligned, before close button)
            if (groups.Count > 0 && groups[0].entries.Count > 0)
            {
                PatchNoteDef anyDef = groups[0].entries[0];
                float startX = titleRect.xMax - TitleBarHeight;
                for (int i = anyDef.Links.Count - 1; i >= 0; i--)
                {
                    startX -= LinkButtonSize + margin;
                    Rect btnRect = new Rect(startX, titleRect.y + 3f, LinkButtonSize, LinkButtonSize);
                    TooltipHandler.TipRegion(btnRect, anyDef.LinkButtonToolTips[i]);
                    if (Widgets.ButtonImage(btnRect, anyDef.LinkButtonImages[i]))
                    {
                        SteamUtility.OpenUrl(anyDef.Links[i]);
                        SoundDefOf.Click.PlayOneShotOnCamera();
                    }
                }
            }

            // Close button
            if (Widgets.ButtonImage(titleRect.RightPartPixels(TitleBarHeight).ContractedBy(6f), TexButton.CloseXSmall))
            {
                Close();
            }

            ResetTextAndColor();
        }

        private void DrawHorizontalDivider(Rect inRect)
        {
            float lineY = inRect.y + TitleBarHeight + (DividerPad * 0.5f) - 1f;
            UIUtil.DrawColoredHorizontalLine(inRect.x + margin, lineY, inRect.width - margin * 2, Color.gray);
            ResetTextAndColor();
        }

        private void DrawBanner(Rect bannerRect)
        {
            if (bannerImage is null && groups.Count > 0 && groups[0].entries.Count > 0)
                bannerImage = groups[0].entries[0].BannerImage;
            if (bannerImage != null)
                GUI.DrawTexture(bannerRect, bannerImage, ScaleMode.ScaleToFit);
        }

        private void DrawPatchNotes(Rect panelRect)
        {
            Rect scrollViewRect = ScrollUtil.BeginScrollView(panelRect, ref patchNoteScrollPos, scrollViewHeight);

            float scrollContentWidth = scrollViewRect.width;
            float curY = 0f;

            for (int gi = 0; gi < groups.Count; gi++)
            {
                PatchNoteGroup group = groups[gi];
                bool groupExpanded = expandedGroups.Contains(gi);

                // --- Group Header ---
                Rect groupRect = new Rect(0f, curY, scrollContentWidth, GroupHeaderHeight);
                DrawGroupHeader(groupRect, group, groupExpanded);

                if (Widgets.ButtonInvisible(groupRect))
                {
                    if (groupExpanded)
                    {
                        expandedGroups.Remove(gi);
                        SoundDefOf.TabClose.PlayOneShotOnCamera();
                    }
                    else
                    {
                        expandedGroups.Add(gi);
                        SoundDefOf.TabOpen.PlayOneShotOnCamera();
                    }
                    shouldRefreshHeight = true;
                }

                curY += GroupHeaderHeight + margin;

                // --- Entries within expanded group ---
                if (groupExpanded)
                {
                    for (int ei = 0; ei < group.entries.Count; ei++)
                    {
                        PatchNoteDef def = group.entries[ei];
                        int entryKey = def.VersionSortKey;
                        bool entryExpanded = expandedEntries.Contains(entryKey);
                        FCSettings.GetLastSeenVersion(modId, out int lsMaj, out int lsMin, out int lsPat);
                        bool isNew = def.IsNewerThan(lsMaj, lsMin, lsPat);

                        // Entry header (indented)
                        Rect headerRect = new Rect(EntryIndent, curY, scrollContentWidth - EntryIndent, HeaderHeight);

                        if (ei % 2 == 0)
                            Widgets.DrawHighlight(headerRect);
                        else
                            Widgets.DrawLightHighlight(headerRect);

                        if (isNew)
                            GUI.color = Color.red;
                        Widgets.DrawBox(headerRect);
                        ResetTextAndColor();

                        // Badge
                        Rect badgeRect = new Rect(headerRect.x + margin, headerRect.y + (HeaderHeight - BadgeHeight) * 0.5f, BadgeWidth, BadgeHeight);
                        DrawTypeBadge(badgeRect, def.GetPatchNoteType);

                        // Expand/collapse icon
                        Rect iconRect = new Rect(headerRect.xMax - IconSize, headerRect.y, IconSize, HeaderHeight);
                        Widgets.DrawTextureFitted(iconRect.ContractedBy(11f), entryExpanded ? TexButton.Collapse : TexButton.Reveal, 1f);

                        // Date
                        Rect dateRect = new Rect(iconRect.x - DateWidth - margin, headerRect.y, DateWidth, HeaderHeight);
                        Text.Font = GameFont.Tiny;
                        Text.Anchor = TextAnchor.MiddleRight;
                        UIUtil.DrawColoredLabel(dateRect, def.ReleaseDate.ToString("dd MMM yyyy"), Color.gray);
                        ResetTextAndColor();

                        // Title
                        float titleX = badgeRect.xMax + margin;
                        Rect titleLabelRect = new Rect(titleX, headerRect.y, dateRect.x - titleX - margin, HeaderHeight);
                        Text.Font = GameFont.Medium;
                        Text.Anchor = TextAnchor.MiddleLeft;
                        UIUtil.ClampedLabel(titleLabelRect, def.ShortTitle);
                        ResetTextAndColor();

                        // Click handling
                        if (Widgets.ButtonInvisible(headerRect))
                        {
                            if (entryExpanded)
                            {
                                expandedEntries.Remove(entryKey);
                                entryBodyHeights.Remove(entryKey);
                                SoundDefOf.TabClose.PlayOneShotOnCamera();
                            }
                            else
                            {
                                expandedEntries.Add(entryKey);
                                SoundDefOf.TabOpen.PlayOneShotOnCamera();
                            }
                            shouldRefreshHeight = true;
                        }

                        curY += HeaderHeight + margin;

                        // Expanded body
                        if (entryExpanded)
                        {
                            Text.Font = GameFont.Small;
                            string bodyText = def.CompactBodyString;
                            float bodyWidth = scrollContentWidth - EntryIndent - margin * 4f;
                            Rect bodyRect = new Rect(EntryIndent + margin * 2f, curY, bodyWidth, 100f);
                            Widgets.LabelCacheHeight(ref bodyRect, bodyText);
                            entryBodyHeights[entryKey] = bodyRect.height;
                            curY += bodyRect.height + margin;
                            ResetTextAndColor();
                        }
                    }
                }
            }

            ScrollUtil.EndScrollView();
        }

        private void DrawGroupHeader(Rect rect, PatchNoteGroup group, bool expanded)
        {
            // Background
            Widgets.DrawBoxSolid(rect, GroupBgColor);

            // New indicator — red left-border accent
            if (group.hasNewEntries)
            {
                Rect newBar = new Rect(rect.x, rect.y, NewIndicatorWidth, rect.height);
                Widgets.DrawBoxSolid(newBar, Color.red);
            }

            // Version label
            float labelX = rect.x + margin + (group.hasNewEntries ? NewIndicatorWidth + margin : 0f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect versionRect = new Rect(labelX, rect.y, 80f, rect.height);
            UIUtil.ClampedLabel(versionRect, group.versionLabel);
            ResetTextAndColor();

            // Badge
            Rect badgeRect = new Rect(versionRect.xMax + margin, rect.y + (GroupHeaderHeight - BadgeHeight) * 0.5f, BadgeWidth, BadgeHeight);
            DrawTypeBadge(badgeRect, group.highestSeverity);

            // Expand/collapse icon
            Rect iconRect = new Rect(rect.xMax - IconSize, rect.y, IconSize, GroupHeaderHeight);
            Widgets.DrawTextureFitted(iconRect.ContractedBy(11f), expanded ? TexButton.Collapse : TexButton.Reveal, 1f);

            // Entry count + date range (right-aligned, before icon)
            string rightText = group.entries.Count + " entries  \u2022  " + group.dateRange;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            float rightWidth = rect.xMax - iconRect.width - badgeRect.xMax - margin * 3f;
            Rect rightRect = new Rect(badgeRect.xMax + margin, rect.y, rightWidth, rect.height);
            UIUtil.DrawColoredLabel(rightRect, rightText, Color.gray);
            ResetTextAndColor();
        }

        private void DrawTypeBadge(Rect rect, PatchNoteType type)
        {
            Color badgeColor;
            string badgeLabel;
            switch (type)
            {
                case PatchNoteType.Major:
                    badgeColor = BadgeColorMajor;
                    badgeLabel = "MAJOR";
                    break;
                case PatchNoteType.Minor:
                    badgeColor = BadgeColorMinor;
                    badgeLabel = "MINOR";
                    break;
                case PatchNoteType.Hotfix:
                    badgeColor = BadgeColorHotfix;
                    badgeLabel = "HOTFIX";
                    break;
                case PatchNoteType.Patch:
                    badgeColor = BadgeColorPatch;
                    badgeLabel = "PATCH";
                    break;
                default:
                    badgeColor = BadgeColorPatch;
                    badgeLabel = "???";
                    break;
            }

            Widgets.DrawBoxSolid(rect, badgeColor);
            GUI.color = Color.white;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            UIUtil.ClampedLabel(rect, badgeLabel);
            ResetTextAndColor();
        }

        private void CalculateScrollViewSize()
        {
            if (!shouldRefreshHeight) return;
            shouldRefreshHeight = false;

            float total = 0f;
            for (int gi = 0; gi < groups.Count; gi++)
            {
                total += GroupHeaderHeight + margin;
                if (expandedGroups.Contains(gi))
                {
                    foreach (PatchNoteDef def in groups[gi].entries)
                    {
                        total += HeaderHeight + margin;
                        if (expandedEntries.Contains(def.VersionSortKey))
                        {
                            float bodyH;
                            if (entryBodyHeights.TryGetValue(def.VersionSortKey, out bodyH))
                                total += bodyH + margin;
                            else
                                total += 200f + margin;
                        }
                    }
                }
            }

            scrollViewHeight = total;
        }

        private void ResetTextAndColor()
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }
    }
}
