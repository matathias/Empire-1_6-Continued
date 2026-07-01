using FactionColonies.util;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /* Shared implant panel used by DesignUnitsWindow (per-template) and Dialog_PawnLoadout
     * (per-pawn). Reads from displayUnit; routes mutations through opts.getEditTarget. The
     * "Add" button opens FCWindow_ImplantPicker, which validates candidate implants against the
     * unit's preview pawn using the base game's own surgery validation. */
    public static class ImplantListWidget
    {
        public struct Options
        {
            public bool canEdit;
            public bool showHeaderButtons;
            public Func<MilUnitFC> getEditTarget;
            // Returns the loadout currently being displayed/validated against (may differ from
            // the edit target until the first buffered edit in Dialog_PawnLoadout).
            public Func<MilUnitFC> getDisplayUnit;
        }

        private const float headerHeight = 25f;
        private const float rowHeight = 28f;
        private const float removeButtonSize = 20f;
        private const float IconSize = 24f;

        public static void Draw(Rect rect, MilUnitFC displayUnit, ref Vector2 scrollPos, Options opts)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            // Add button at the top-right — the tab labels the panel, no title needed.
            float btnY = rect.y;

            if (opts.canEdit && opts.showHeaderButtons && displayUnit != null)
            {
                float addW = 120f;
                Rect addBtnRect = new Rect(rect.xMax - addW, btnY, addW, headerHeight);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                if (UIUtil.ClampedButtonText(addBtnRect, "fcAddImplant".Translate()))
                {
                    Func<MilUnitFC> getDisplay = opts.getDisplayUnit ?? (() => displayUnit);
                    Find.WindowStack.Add(new FCWindow_ImplantPicker(getDisplay, opts.getEditTarget));
                }
            }

            Rect listOutRect = new Rect(rect.x, btnY + headerHeight + 2f, rect.width, rect.height - headerHeight - 4f);

            List<SavedImplant> items = displayUnit?.implants ?? new List<SavedImplant>();
            float viewHeight = items.Count * rowHeight;
            Rect scrollViewRect = ScrollUtil.BeginScrollView(listOutRect, ref scrollPos, viewHeight);

            for (int i = 0; i < items.Count; i++)
            {
                SavedImplant item = items[i];
                if (!item.IsValid) continue;
                int index = i;
                Rect row = new Rect(scrollViewRect.x, scrollViewRect.y + i * rowHeight, scrollViewRect.width, rowHeight);
                if (i % 2 == 0) Widgets.DrawHighlight(row);

                // Icon + info card (the implant item / self-install item)
                ThingDef iconThing = MilUnitFC.ImplantIconThing(item);
                Rect iconRect = new Rect(row.x + 2f, row.y + 2f, IconSize, IconSize);
                if (iconThing != null)
                    Widgets.ThingIcon(iconRect, iconThing);

                Rect infoRect = new Rect(iconRect.xMax + 2f, row.y + 2f, IconSize - 2f, IconSize - 2f);
                if (iconThing != null)
                    Widgets.InfoCardButton(infoRect, iconThing);

                // Remove button
                Rect removeRect = Rect.zero;
                if (opts.canEdit)
                {
                    removeRect = new Rect(row.xMax - removeButtonSize - 2f, row.y + (rowHeight - removeButtonSize) / 2f, removeButtonSize, removeButtonSize);
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    if (UIUtil.ClampedButtonText(removeRect, "X"))
                    {
                        MilUnitFC target = opts.getEditTarget?.Invoke();
                        if (target != null) target.RemoveImplant(index);
                    }
                }

                // Cost
                float costRight = opts.canEdit ? removeRect.x - 4f : row.xMax - 4f;
                Rect costRect = new Rect(costRight - 60f, row.y, 60f, rowHeight);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleRight;
                UIUtil.ClampedLabel(costRect, "$" + MilUnitFC.ImplantCost(item).ToString("F0"));

                // Label: implant + body part
                string hediffLabel;
                if (item.recipe is object)
                    hediffLabel = (item.recipe.addsHediff?.LabelCap ?? item.recipe.LabelCap).ToString();
                else
                    hediffLabel = item.selfInstallThing.LabelCap.ToString();
                string label = item.bodyPart != null
                    ? hediffLabel + " (" + item.bodyPart.label + ")"
                    : hediffLabel;
                Rect labelRect = new Rect(infoRect.xMax + 6f, row.y, costRect.x - infoRect.xMax - 10f, rowHeight);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                string shownLabel = Text.ClampTextWithEllipsis(labelRect, label);
                UIUtil.ClampedLabel(labelRect, shownLabel);
                if (shownLabel != label) TooltipHandler.TipRegion(labelRect, label);
            }

            ScrollUtil.EndScrollView();

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }
    }
}
