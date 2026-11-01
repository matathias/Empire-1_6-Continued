using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class FCWindow_UnitPicker : Window
    {
        private readonly List<MilUnitFC> units;
        private readonly Action<MilUnitFC> onSelect;
        private MilUnitFC selectedUnit;
        private string searchTerm = "";
        private Vector2 scrollPos;

        private const float RowHeight = 30f;
        private const float SearchBarHeight = 28f;
        private const float ButtonHeight = 35f;
        private const float margin = 5f;

        public override Vector2 InitialSize => new Vector2(450f, 550f);

        public FCWindow_UnitPicker(MilitaryFC mfc, Action<MilUnitFC> onSelect)
        {
            this.units = mfc.units.Where(u => !u.isBlank).ToList();
            this.onSelect = onSelect;
            draggable = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            forcePause = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            // Title
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            UIUtil.ClampedLabel(new Rect(0, 0, inRect.width, 35f), "FCAddUnit".Translate());

            // Search bar
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect searchRect = new Rect(0, 40f, inRect.width, SearchBarHeight);
            searchTerm = Widgets.TextField(searchRect, searchTerm);

            // Filter
            List<MilUnitFC> filtered = units;
            if (!string.IsNullOrEmpty(searchTerm))
                filtered = units.Where(u => (u.name ?? "").IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            // Scroll view
            float listTop = searchRect.yMax + margin;
            float listHeight = inRect.height - listTop - ButtonHeight - 15f;
            Rect scrollOutRect = new Rect(0, listTop, inRect.width, listHeight);
            Widgets.DrawMenuSection(scrollOutRect);

            float viewHeight = filtered.Count * RowHeight;
            Rect scrollViewRect = ScrollUtil.BeginScrollView(scrollOutRect, ref scrollPos, viewHeight);

            for (int i = 0; i < filtered.Count; i++)
            {
                MilUnitFC unit = filtered[i];
                Rect row = new Rect(0, i * RowHeight, scrollViewRect.width, RowHeight);

                if (unit == selectedUnit)
                    Widgets.DrawHighlightSelected(row);
                else if (i % 2 == 0)
                    Widgets.DrawHighlight(row);

                // Row layout: Icon | Label | Cost
                Rect iconRect = new Rect(row.x + margin, row.y, RowHeight, RowHeight);
                Rect costRect = new Rect(row.xMax - margin - 70f, row.y, 60f, RowHeight);
                Rect labelRect = new Rect(iconRect.xMax + margin, row.y,
                    costRect.x - iconRect.xMax - (margin * 2), RowHeight);

                // Icon: weapon icon if available, else race icon
                if (unit.HasWeapon && unit.weapons[0].thing != null)
                    Widgets.ThingIcon(iconRect, unit.weapons[0].thing);
                else if (unit.pawnKind?.race != null)
                    Widgets.ThingIcon(iconRect, unit.pawnKind.race);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.ClampedLabel(labelRect, unit.name);

                Text.Anchor = TextAnchor.MiddleRight;
                UIUtil.ClampedLabel(costRect, "$" + unit.getTotalCost);

                if (Widgets.ButtonInvisible(row))
                {
                    selectedUnit = unit;
                }
            }

            if (filtered.Count == 0)
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.ClampedLabel(scrollOutRect, "FCSelectAUnitButton".Translate());
            }

            ScrollUtil.EndScrollView();

            // Bottom buttons
            float buttonWidth = 120f;
            Rect buttonBar = new Rect(0, inRect.height - ButtonHeight - 5f, inRect.width, ButtonHeight);

            // Cancel (right)
            Rect cancelRect = new Rect(buttonBar.xMax - buttonWidth, buttonBar.y, buttonWidth, buttonBar.height);
            if (UIUtil.ClampedButtonText(cancelRect, "CancelButton".Translate()))
            {
                Close();
            }

            // Confirm (left of cancel)
            Rect confirmRect = new Rect(cancelRect.x - buttonWidth - 10f, buttonBar.y, buttonWidth, buttonBar.height);
            if (UIUtil.ClampedButtonText(confirmRect, "FCConfirm".Translate(), active: selectedUnit != null))
            {
                if (selectedUnit != null)
                {
                    onSelect(selectedUnit);
                    Close();
                }
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }
    }
}
