using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace FactionColonies
{
    /// <summary>
    /// Shared drawing primitives for the <see cref="CodexWindow"/> tabs. Each tab keeps its own
    /// left/center/right iteration logic but delegates the repeated per-section, per-row, and
    /// per-banner drawing here so the look stays consistent and lives in one place.
    /// </summary>
    public static class CodexUIUtil
    {
        /* Shared layout constants (identical across the data-browser tabs) */
        public const float AccentBarWidth = 3f;
        public const float Margin = 8f;
        public const float SmallMargin = 4f;
        public const float SectionHeaderHeight = 22f;
        public const float StatRowHeight = 22f;
        public const float BannerHeight = 70f;
        public const float EntryIconSize = 16f;

        /* Shared colors */
        public static readonly Color DefaultAccent = ColorUtil.Gold; // (0.83, 0.68, 0.21)
        public static readonly Color SectionBgColor = new Color(0.15f, 0.15f, 0.15f, 0.4f);
        public static readonly Color GroupBgColor = new Color(0.2f, 0.2f, 0.2f, 0.6f);
        public static readonly Color GroupAccentColor = new Color(0.7f, 0.7f, 0.7f);
        public static readonly Color HighlightColor = new Color(0.4f, 0.6f, 0.9f);

        /// <summary>Draws the body of a section between its header and the trailing margin. Returns the new curY.</summary>
        public delegate float CodexSectionDrawer(float curY, float width);

        /// <summary>Resets font/anchor/color to the tabs' default (Small, upper-left, white).</summary>
        public static void ResetText()
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  CENTER / RIGHT PANE SECTIONS
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/

        /// <summary>Header bar (bg + gradient + accent bar + label), then the body, then a trailing margin.</summary>
        public static float DrawSection(float startY, float width, string header, Color accent, CodexSectionDrawer drawer)
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

        /// <summary>As <see cref="DrawSection"/> but with a collapse arrow; the body is skipped while collapsed.</summary>
        public static float DrawCollapsibleSection(float startY, float width, string header, Color accent,
            ref bool expanded, CodexSectionDrawer drawer)
        {
            float curY = startY;

            Rect headerRect = new Rect(0f, curY, width, SectionHeaderHeight);
            Widgets.DrawBoxSolid(headerRect, SectionBgColor);
            TexLoad.DrawHorizontalGradient(headerRect, ColorUtil.TransformA(accent, 0.15f));
            Widgets.DrawBoxSolid(new Rect(0f, curY, AccentBarWidth, SectionHeaderHeight), accent);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.DrawColoredLabel(
                new Rect(AccentBarWidth + Margin, curY, width - AccentBarWidth - Margin * 2 - 20f, SectionHeaderHeight),
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

            curY += Margin;
            return curY;
        }

        /// <summary>One left-aligned white stat row of height <see cref="StatRowHeight"/>.</summary>
        public static float DrawStatLine(float curY, float x, float width, string text)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Color.white;
            UIUtil.ClampedLabel(new Rect(x, curY, width, StatRowHeight), text);
            ResetText();
            return curY + StatRowHeight;
        }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  RIGHT PANE BANNER HEADER
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/

        /// <summary>
        /// Optional banner + centered gray mod name (wrapped to fit, never clamped) + a divider.
        /// Pass a null <paramref name="banner"/> to skip the image. Returns the new curY.
        /// </summary>
        public static float DrawRightPaneBannerHeader(float startY, float width, Texture2D banner, string modName)
        {
            float curY = startY;

            if (banner is object)
            {
                Rect bannerRect = new Rect(0f, curY, width, BannerHeight);
                GUI.DrawTexture(bannerRect, banner, ScaleMode.ScaleToFit);
                curY += BannerHeight + SmallMargin;
            }

            if (!modName.NullOrEmpty())
            {
                float nameHeight = HeaderHeightFor(modName, width, 20f, 0f);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.DrawColoredLabel(new Rect(0f, curY, width, nameHeight), modName, Color.gray, false);
                ResetText();
                curY += nameHeight + SmallMargin;
            }

            UIUtil.DrawColoredHorizontalLine(Margin, curY, width - Margin * 2, Color.gray);
            curY += Margin;

            return curY;
        }

        /// <summary>
        /// Height of a header row grown to fit its full (wrapped) label at the given available width.
        /// Never shrinks below <paramref name="minHeight"/>. Sets <see cref="GameFont.Small"/> before measuring.
        /// </summary>
        public static float HeaderHeightFor(string label, float labelWidth, float minHeight, float vPad)
        {
            if (label.NullOrEmpty()) return minHeight;
            Text.Font = GameFont.Small;
            if (labelWidth < 1f) labelWidth = 1f;
            float textHeight = Text.CalcHeight(label, labelWidth);
            return Mathf.Max(minHeight, textHeight + vPad);
        }

        /**-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         *  LEFT PANE ATOMS
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-**/

        /// <summary>
        /// A collapsible left-pane group header (bg + gradient + accent bar + clamped label + arrow).
        /// Returns true when clicked this frame; the caller flips its own expand state and plays the sound.
        /// </summary>
        public static bool DrawGroupHeader(Rect headerRect, string label, Color accent, bool expanded,
            Color bgColor, float arrowSize)
        {
            Widgets.DrawBoxSolid(headerRect, bgColor);
            TexLoad.DrawHorizontalGradient(headerRect, ColorUtil.TransformA(accent, 0.2f));
            Widgets.DrawBoxSolid(new Rect(headerRect.x, headerRect.y, AccentBarWidth, headerRect.height), accent);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.DrawColoredLabel(
                new Rect(headerRect.x + AccentBarWidth + Margin, headerRect.y,
                    headerRect.width - AccentBarWidth - Margin * 2 - arrowSize, headerRect.height),
                label,
                ColorUtil.TransformRGB(accent, 1.3f));

            Rect arrowRect = new Rect(headerRect.xMax - arrowSize - 2f, headerRect.y + (headerRect.height - arrowSize) * 0.5f,
                arrowSize, arrowSize);
            Widgets.DrawTextureFitted(arrowRect, expanded ? TexButton.Collapse : TexButton.Reveal, 1f);
            ResetText();

            return Widgets.ButtonInvisible(headerRect);
        }

        /// <summary>
        /// A selectable left-pane entry row: selection/hover highlight + accent bar + optional icon +
        /// clamped label (ellipsis + tooltip when it doesn't fit). Returns true when clicked this frame.
        /// <paramref name="textInset"/> is the label's left inset from the row when there is no icon;
        /// <paramref name="iconInset"/> is the icon's left inset (and the label follows the icon).
        /// <paramref name="labelColor"/> is passed in so callers that dim rows stay in control.
        /// </summary>
        public static bool DrawEntryRow(Rect entryRect, string fullLabel, Color accent, bool isSelected,
            float barWidth, float textInset, float iconInset, Texture2D icon, Color labelColor)
        {
            if (isSelected)
                Widgets.DrawBoxSolid(entryRect, ColorUtil.TransformA(accent, 0.35f));
            else if (Mouse.IsOver(entryRect))
                Widgets.DrawBoxSolid(entryRect, ColorUtil.TransformA(accent, 0.15f));

            Color barColor = isSelected ? accent : ColorUtil.TransformA(accent, 0.4f);
            Widgets.DrawBoxSolid(new Rect(entryRect.x, entryRect.y, barWidth, entryRect.height), barColor);

            float textX = entryRect.x + textInset;
            if (icon is object)
            {
                Rect iconRect = new Rect(entryRect.x + iconInset, entryRect.y + (entryRect.height - EntryIconSize) * 0.5f,
                    EntryIconSize, EntryIconSize);
                GUI.DrawTexture(iconRect, icon);
                textX = iconRect.xMax + SmallMargin;
            }

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.DrawColoredLabel(
                new Rect(textX, entryRect.y, entryRect.xMax - textX - SmallMargin, entryRect.height),
                fullLabel,
                labelColor);
            ResetText();

            return Widgets.ButtonInvisible(entryRect);
        }
    }
}
