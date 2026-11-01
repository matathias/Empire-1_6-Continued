using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class FCWindow_ProjectilePicker : Window
    {
        private readonly List<ThingDef> projectiles;
        private readonly Action<ThingDef> onSelect;
        private ThingDef selectedProjectile;
        private string searchTerm = "";
        private Vector2 scrollPos;

        private const float RowHeight = 30f;
        private const float SearchBarHeight = 28f;
        private const float ButtonHeight = 35f;
        private const float margin = 5f;

        public override Vector2 InitialSize => new Vector2(450f, 550f);

        public FCWindow_ProjectilePicker(List<ThingDef> projectiles, Action<ThingDef> onSelect)
        {
            this.projectiles = projectiles;
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
            UIUtil.ClampedLabel(new Rect(0, 0, inRect.width, 35f), "FCAddNewProjectile".Translate());

            // Search bar
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect searchRect = new Rect(0, 40f, inRect.width, SearchBarHeight);
            searchTerm = Widgets.TextField(searchRect, searchTerm);

            // Filter
            List<ThingDef> filtered = projectiles;
            if (!string.IsNullOrEmpty(searchTerm))
                filtered = projectiles.Where(d => d.LabelCap.ToString().IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            // Scroll view
            float listTop = searchRect.yMax + margin;
            float listHeight = inRect.height - listTop - ButtonHeight - 15f;
            Rect scrollOutRect = new Rect(0, listTop, inRect.width, listHeight);
            Widgets.DrawMenuSection(scrollOutRect);

            float viewHeight = filtered.Count * RowHeight;
            Rect scrollViewRect = ScrollUtil.BeginScrollView(scrollOutRect, ref scrollPos, viewHeight);

            for (int i = 0; i < filtered.Count; i++)
            {
                ThingDef def = filtered[i];
                Rect row = new Rect(0, i * RowHeight, scrollViewRect.width, RowHeight);

                if (def == selectedProjectile)
                    Widgets.DrawHighlightSelected(row);
                else if (i % 2 == 0)
                    Widgets.DrawHighlight(row);

                // Row layout: Icon+Name | Cost
                Rect costRect = new Rect(row.xMax - margin - 80f, row.y, 70f, RowHeight);
                Rect iconNameRect = new Rect(row.x + margin, row.y, costRect.x - row.x - margin * 2, RowHeight);

                Widgets.DefLabelWithIcon(iconNameRect, def);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleRight;
                UIUtil.ClampedLabel(costRect, "$" + Math.Round(def.BaseMarketValue * 1.5, 2));

                if (Widgets.ButtonInvisible(row))
                {
                    selectedProjectile = def;
                }
            }

            if (filtered.Count == 0)
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.ClampedLabel(scrollOutRect, "FCNoProjectilesFound".Translate());
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
            if (UIUtil.ClampedButtonText(confirmRect, "FCConfirm".Translate(), active: selectedProjectile != null))
            {
                if (selectedProjectile != null)
                {
                    onSelect(selectedProjectile);
                    Close();
                }
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }
    }
}
