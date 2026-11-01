using FactionColonies.util;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /* Companion-animals panel for the unit designer's Animals tab. Lists the unit's SavedAnimal stacks
     * with per-row count steppers and a remove button, plus a header showing the running total against
     * FCSettings.maxAnimalSubpawns and an Add Animal button (opens the multi-add picker). Mirrors
     * MechListWidget minus the mech-only group/bandwidth/work-mode machinery: reads displayUnit, routes
     * mutations via getEditTarget. The mount (Giddy Up 2) is NOT shown here — it lives in the gear-panel
     * slot. */
    public static class AnimalListWidget
    {
        public struct Options
        {
            public bool canEdit;
            public bool showHeaderButtons;
            public Func<MilUnitFC> getEditTarget;
            public Func<MilUnitFC> getDisplayUnit;
        }

        private const float headerHeight = 25f;
        private const float rowHeight = 28f;
        private const float removeButtonSize = 20f;
        private const float stepperButtonW = 20f;
        private const float IconSize = 24f;

        public static void Draw(Rect rect, MilUnitFC displayUnit, ref Vector2 scrollPos, Options opts)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            bool editable = opts.canEdit && opts.showHeaderButtons;

            List<SavedAnimal> items = displayUnit?.animals ?? new List<SavedAnimal>();
            int total = displayUnit?.TotalAnimalCount ?? 0;
            int cap = FCSettings.maxAnimalSubpawns;

            // --- Header: "Animals (N / cap)" + Add Animal button ---
            Rect header = new Rect(rect.x, rect.y, rect.width, headerHeight);
            const float addW = 90f;
            Rect addBtnRect = new Rect(header.xMax - addW - 2f, header.y + 1f, addW, headerHeight - 2f);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            if (total > cap) GUI.color = ColorLibrary.RedReadable;
            UIUtil.ClampedLabel(new Rect(header.x + 4f, header.y, addBtnRect.x - header.x - 8f, header.height),
                "fcAnimalCount".Translate(total, cap));
            GUI.color = Color.white;

            if (editable)
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                if (UIUtil.ClampedButtonText(addBtnRect, "fcAddAnimal".Translate()))
                {
                    Func<MilUnitFC> getDisplay = opts.getDisplayUnit ?? (() => displayUnit);
                    Find.WindowStack.Add(new FCWindow_AnimalPicker(getDisplay, opts.getEditTarget));
                }
            }

            // --- List ---
            float listTop = header.yMax + 4f;
            Rect listOutRect = new Rect(rect.x, listTop, rect.width, rect.height - (listTop - rect.y));
            float viewHeight = items.Count * rowHeight;
            Rect scrollViewRect = ScrollUtil.BeginScrollView(listOutRect, ref scrollPos, viewHeight);

            float y = scrollViewRect.y;
            for (int index = 0; index < items.Count; index++)
            {
                SavedAnimal item = items[index];
                if (item.kind?.race is null) { y += rowHeight; continue; }

                Rect row = new Rect(scrollViewRect.x, y, scrollViewRect.width, rowHeight);
                Widgets.DrawHighlightIfMouseover(row);

                Rect iconRect = new Rect(row.x + 6f, row.y + 2f, IconSize, IconSize);
                Widgets.ThingIcon(iconRect, item.kind.race);

                Rect infoRect = new Rect(iconRect.xMax + 2f, row.y + 2f, IconSize - 2f, IconSize - 2f);
                Widgets.InfoCardButton(infoRect, item.kind.race);

                float cursorRight = row.xMax - 4f;

                // Remove button (far right)
                if (editable)
                {
                    Rect removeRect = new Rect(cursorRight - removeButtonSize, row.y + (rowHeight - removeButtonSize) / 2f, removeButtonSize, removeButtonSize);
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    if (UIUtil.ClampedButtonText(removeRect, "X"))
                    {
                        MilUnitFC target = opts.getEditTarget?.Invoke();
                        if (target != null) target.RemoveAnimal(index);
                    }
                    cursorRight = removeRect.x - 6f;
                }

                // Count steppers ( - N + )
                if (editable)
                {
                    Rect plusRect = new Rect(cursorRight - stepperButtonW, row.y + (rowHeight - stepperButtonW) / 2f, stepperButtonW, stepperButtonW);
                    Rect countRect = new Rect(plusRect.x - 26f, row.y, 26f, rowHeight);
                    Rect minusRect = new Rect(countRect.x - stepperButtonW, plusRect.y, stepperButtonW, stepperButtonW);
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    if (UIUtil.ClampedButtonText(minusRect, "-"))
                    {
                        MilUnitFC target = opts.getEditTarget?.Invoke();
                        if (target != null) target.DecrementAnimal(index);
                    }
                    UIUtil.ClampedLabel(countRect, "x" + Mathf.Max(1, item.count));
                    if (UIUtil.ClampedButtonText(plusRect, "+"))
                    {
                        MilUnitFC target = opts.getEditTarget?.Invoke();
                        if (target != null) target.AddAnimal(item.kind);   // hard-blocks on cap
                    }
                    cursorRight = minusRect.x - 6f;
                }
                else
                {
                    Rect countRect = new Rect(cursorRight - 30f, row.y, 30f, rowHeight);
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleRight;
                    UIUtil.ClampedLabel(countRect, "x" + Mathf.Max(1, item.count));
                    cursorRight = countRect.x - 6f;
                }

                // Cost (per animal)
                double cost = Math.Floor(item.kind.race.BaseMarketValue * FCSettings.militaryAnimalCostMultiplier);
                Rect costRect = new Rect(cursorRight - 55f, row.y, 55f, rowHeight);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleRight;
                UIUtil.ClampedLabel(costRect, "$" + cost.ToString("F0"));

                // Label
                string label = item.kind.LabelCap;
                Rect labelRect = new Rect(infoRect.xMax + 6f, row.y, costRect.x - infoRect.xMax - 10f, rowHeight);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                string shownLabel = Text.ClampTextWithEllipsis(labelRect, label);
                UIUtil.ClampedLabel(labelRect, shownLabel);
                if (shownLabel != label) TooltipHandler.TipRegion(labelRect, label);

                y += rowHeight;
            }

            ScrollUtil.EndScrollView();

            // Empty-state hint (drawn after EndScrollView so it centers in the visible box).
            if (items.Count == 0)
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.ClampedLabel(listOutRect, "fcNoAnimalsAssigned".Translate());
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }
    }
}
