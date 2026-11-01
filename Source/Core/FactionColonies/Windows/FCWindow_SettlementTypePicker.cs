using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class FCWindow_SettlementTypePicker : Window
    {
        private readonly Action<WorldSettlementDef> onSelect;
        private readonly List<WorldSettlementDef> allTypes;
        private readonly string titleKey;
        private Vector2 scrollPos;

        /// <summary>
        /// The settlement type the cursor is currently over (null if none). Exposed so a companion
        /// window docked beside the picker can preview cost/info for the hovered type. Updated each
        /// frame in <see cref="DrawSettlementTypeRow"/>; retains its last value when nothing is hovered.
        /// </summary>
        public WorldSettlementDef HoveredType { get; private set; }

        private const float TitleHeight = 35f;
        private const float SeparatorHeight = 1f;

        public override Vector2 InitialSize => new Vector2(480f, 550f);

        /// <summary>Picker over all available settlement types.</summary>
        public FCWindow_SettlementTypePicker(Action<WorldSettlementDef> onSelect)
            : this(FactionCache.AvailableWorldSettlementDefs, onSelect)
        {
        }

        /// <summary>
        /// Picker over an explicit set of settlement types (e.g. the subset an outpost can convert into),
        /// with an optional custom title key.
        /// </summary>
        public FCWindow_SettlementTypePicker(IEnumerable<WorldSettlementDef> types, Action<WorldSettlementDef> onSelect, string titleKey = "FCPickSettlementType")
        {
            this.onSelect = onSelect;
            this.titleKey = titleKey;
            draggable = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            forcePause = false;

            allTypes = types
                .OrderBy(d => d.IsUnlocked() ? 0 : 1)
                .ThenBy(d => d.LabelCap.ToString())
                .ToList();
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            // Title
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            UIUtil.ClampedLabel(new Rect(0, 0, inRect.width, TitleHeight), titleKey.Translate());

            // Scroll view
            float listTop = TitleHeight + SettlementCardDrawer.margin;
            float listHeight = inRect.height - listTop;
            Rect scrollOutRect = new Rect(0, listTop, inRect.width, listHeight);

            float contentWidth = scrollOutRect.width - 16f;
            float totalHeight = 0f;
            foreach (WorldSettlementDef def in allTypes)
            {
                totalHeight += GetRowHeight(def, contentWidth) + SeparatorHeight;
            }

            Rect scrollViewRect = ScrollUtil.BeginScrollView(scrollOutRect, ref scrollPos, Mathf.Max(totalHeight, listHeight));

            float curY = 0f;
            for (int i = 0; i < allTypes.Count; i++)
            {
                WorldSettlementDef def = allTypes[i];
                float rowHeight = GetRowHeight(def, contentWidth);
                Rect rowRect = new Rect(0, curY, contentWidth, rowHeight);

                if (rowRect.yMax >= scrollPos.y && rowRect.y <= scrollPos.y + listHeight)
                {
                    DrawSettlementTypeRow(rowRect, def, i);
                }

                curY += rowHeight;

                // Separator line
                if (i < allTypes.Count - 1)
                {
                    UIUtil.DrawColoredHorizontalLine(
                        SettlementCardDrawer.AccentBarWidth + SettlementCardDrawer.margin,
                        curY,
                        contentWidth - SettlementCardDrawer.AccentBarWidth - SettlementCardDrawer.margin * 2,
                        new Color(0.3f, 0.3f, 0.3f, 0.5f));
                    curY += SeparatorHeight;
                }
            }

            ScrollUtil.EndScrollView();

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        private float GetRowHeight(WorldSettlementDef def, float width)
        {
            float height = SettlementCardDrawer.GetCardHeight(def, width);

            // Lock reason adds extra height
            string lockedReason;
            if (!def.IsUnlocked(out lockedReason))
            {
                float contentWidth = width - SettlementCardDrawer.AccentBarWidth - SettlementCardDrawer.margin * 3;
                Text.Font = GameFont.Tiny;
                height += Text.CalcHeight(lockedReason, contentWidth) + SettlementCardDrawer.margin;
            }

            return height;
        }

        private void DrawSettlementTypeRow(Rect rect, WorldSettlementDef def, int index)
        {
            string lockedReason;
            bool unlocked = def.IsUnlocked(out lockedReason);

            if (Mouse.IsOver(rect))
            {
                HoveredType = def;
            }

            // Background
            if (unlocked && Mouse.IsOver(rect))
            {
                Widgets.DrawHighlightSelected(rect);
            }
            else if (index % 2 == 0)
            {
                Widgets.DrawHighlight(rect);
            }

            if (!unlocked)
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
            }

            // Card content (accent bar, name, description, resources)
            SettlementCardDrawer.DrawSettlementCard(rect, def);

            GUI.color = Color.white;

            // Lock reason (drawn in red, after resetting GUI.color)
            if (!unlocked && lockedReason != null)
            {
                float cardHeight = SettlementCardDrawer.GetCardHeight(def, rect.width);
                float xOffset = rect.x + SettlementCardDrawer.AccentBarWidth + SettlementCardDrawer.margin;
                float contentWidth = rect.width - SettlementCardDrawer.AccentBarWidth - SettlementCardDrawer.margin * 3;
                float reasonY = rect.y + cardHeight;

                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperLeft;
                float reasonHeight = Text.CalcHeight(lockedReason, contentWidth);
                UIUtil.DrawColoredLabel(
                    new Rect(xOffset, reasonY, contentWidth, reasonHeight),
                    lockedReason,
                    new Color(0.8f, 0.2f, 0.2f), clamp: false);
            }

            // Click handling
            if (unlocked && Widgets.ButtonInvisible(rect))
            {
                onSelect(def);
                Close();
            }
            else if (!unlocked)
            {
                TooltipHandler.TipRegion(rect, lockedReason);
            }
        }
    }
}
