using FactionColonies.util;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /* Shared apparel list panel used by DesignUnitsWindow (per-template) and
     * Dialog_PawnLoadout (per-pawn). Reads from displayUnit; routes every
     * mutation through opts.getEditTarget so callers can defer ownership
     * transfers (e.g. cloning a squad template into ownedLoadout). */
    public static class ApparelListWidget
    {
        public struct Options
        {
            public bool canEdit;
            public bool showHeaderButtons;
            public Func<MilUnitFC> getEditTarget;
        }

        private const float headerHeight = 25f;
        private const float apparelRowHeight = 28f;
        private const float removeButtonSize = 20f;
        private const float IconSize = 24f;

        public static void Draw(Rect rect, MilUnitFC displayUnit, ref Vector2 scrollPos, Options opts)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            // Button row sits at the top — the selected tab already labels this panel.
            float btnY = rect.y;
            float btnW = (rect.width - 4f) / 3f;

            // Header buttons row
            if (opts.canEdit && opts.showHeaderButtons)
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;

                float btnX = rect.xMax;

                btnX -= btnW;
                Rect addBtnRect = new Rect(btnX, btnY, btnW, headerHeight);
                if (UIUtil.ClampedButtonText(addBtnRect, "fcAddApparel".Translate()))
                {
                    OpenApparelPicker(displayUnit, opts);
                }

                if (displayUnit != null && displayUnit.apparel.Any(a => a.thing != null))
                {
                    btnX -= btnW + 2f;
                    Rect setAllBtn = new Rect(btnX, btnY, btnW, headerHeight);
                    if (UIUtil.ClampedButtonText(setAllBtn, "fcSetAllColors".Translate()))
                    {
                        Color current = FindFC.FactionComp?.hasFactionColor == true
                            ? FindFC.FactionComp.factionColorPrimary : Color.white;
                        OpenColorPicker(current, delegate (Color c)
                        {
                            MilUnitFC target = opts.getEditTarget?.Invoke();
                            if (target != null) target.SetAllApparelColors(c);
                        });
                    }

                    if (displayUnit.apparel.Any(a => a.hasColor))
                    {
                        btnX -= btnW + 2f;
                        Rect clearBtn = new Rect(btnX, btnY, btnW, headerHeight);
                        if (UIUtil.ClampedButtonText(clearBtn, "fcClearColors".Translate()))
                        {
                            MilUnitFC target = opts.getEditTarget?.Invoke();
                            if (target != null) target.ClearAllApparelColors();
                        }
                    }
                }
            }

            // List
            Rect listOutRect = new Rect(rect.x, btnY + headerHeight + 2f, rect.width, rect.height - headerHeight - 4f);

            List<SavedThing> sortedApparel = displayUnit?.apparel
                ?.Where(a => a.thing != null)
                .OrderByDescending(a => a.thing.apparel.layers.Max(l => l.drawOrder))
                .ThenBy(a => a.thing.label)
                .ToList() ?? new List<SavedThing>();

            float viewHeight = sortedApparel.Count * apparelRowHeight;
            Rect scrollViewRect = ScrollUtil.BeginScrollView(listOutRect, ref scrollPos, viewHeight);

            for (int i = 0; i < sortedApparel.Count; i++)
            {
                SavedThing item = sortedApparel[i];
                Rect row = new Rect(scrollViewRect.x, scrollViewRect.y + i * apparelRowHeight, scrollViewRect.width, apparelRowHeight);

                if (i % 2 == 0) Widgets.DrawHighlight(row);

                // Icon
                Rect iconRect = new Rect(row.x + 2f, row.y + 2f, IconSize, IconSize);
                Widgets.ThingIcon(iconRect, item.thing, item.stuff);

                // Info card button
                const float infoBtnSize = 24f;
                Rect infoRect = new Rect(iconRect.xMax + 2f, row.y + (apparelRowHeight - infoBtnSize) / 2f, infoBtnSize, infoBtnSize);
                Widgets.InfoCardButton(infoRect.x, infoRect.y, item.thing, item.stuff);

                // Remove button (right side)
                float costWidth = 55f;
                const float swatchSize = 16f;
                Rect removeRect = Rect.zero;
                if (opts.canEdit)
                {
                    removeRect = new Rect(row.xMax - removeButtonSize - 2f, row.y + (apparelRowHeight - removeButtonSize) / 2f, removeButtonSize, removeButtonSize);
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    if (UIUtil.ClampedButtonText(removeRect, "X"))
                    {
                        ThingDef capturedThing = item.thing;
                        MilUnitFC target = opts.getEditTarget?.Invoke();
                        if (target != null) target.RemoveApparel(capturedThing);
                    }
                }

                // Color swatch
                float swatchRightEdge = opts.canEdit ? removeRect.x - 4f : row.xMax - 4f;
                Rect swatchRect = new Rect(swatchRightEdge - swatchSize, row.y + (apparelRowHeight - swatchSize) / 2f, swatchSize, swatchSize);
                FactionFC factionComp = FindFC.FactionComp;
                Color resolvedColor = factionComp != null ? factionComp.ResolveApparelColor(item) : Color.white;
                Color outlineColor = item.hasColor ? Color.white : new Color(0.5f, 0.5f, 0.5f);
                Widgets.DrawBoxSolidWithOutline(swatchRect, resolvedColor, outlineColor);
                if (opts.canEdit && Widgets.ButtonInvisible(swatchRect))
                {
                    ThingDef capturedDef = item.thing;
                    OpenColorPicker(resolvedColor, delegate (Color c)
                    {
                        MilUnitFC target = opts.getEditTarget?.Invoke();
                        if (target != null) target.SetApparelColor(capturedDef, c);
                    });
                }

                // Cost
                Rect costRect = new Rect(swatchRect.x - costWidth - 2f, row.y, costWidth, apparelRowHeight);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleRight;
                UIUtil.ClampedLabel(costRect, "$" + item.MarketValue.ToString("F0"));

                // Label
                Rect labelRect = new Rect(infoRect.xMax + 4f, row.y, costRect.x - infoRect.xMax - 8f, apparelRowHeight);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                string label = item.stuff != null
                    ? (string)(item.thing.LabelCap + " (" + item.stuff.LabelCap + ")")
                    : item.thing.LabelCap.ToString();
                if (item.quality.HasValue)
                    label = item.quality.Value.GetLabel().CapitalizeFirst() + " " + label;
                UIUtil.ClampedLabel(labelRect, label);

                // Click row to open replace picker
                if (opts.canEdit)
                {
                    Rect clickRect = new Rect(row.x, row.y, costRect.x - row.x, apparelRowHeight);
                    if (Widgets.ButtonInvisible(clickRect))
                    {
                        OpenApparelReplacePicker(displayUnit, item, opts);
                    }
                }
            }

            ScrollUtil.EndScrollView();

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        // --- Pickers ---

        private static void OpenApparelPicker(MilUnitFC displayUnit, Options opts)
        {
            ThingDef raceDef = displayUnit?.pawnKind?.race;
            BodyDef body = raceDef?.race?.body ?? BodyDefOf.Human;
            List<SavedThing> currentApparel = displayUnit?.apparel ?? new List<SavedThing>();

            // Static predicates (IsApparel/PawnCanWear) are cached in the pool; only the live research
            // + race gates and the worn-set exclusion run per open.
            List<ThingDef> apparelDefs = MilitaryEquipmentPoolUtil.ApparelPool()
                .Where(t => CraftUtil.CanCraftItem(t)
                    && HARUtil.CanRaceWearApparel(raceDef, t)
                    && !currentApparel.Any(a => a.thing == t))
                .OrderBy(t => t.label)
                .ToList();

            Find.WindowStack.Add(new FCWindow_ItemStuffPicker(
                apparelDefs,
                onConfirm: (item, stuff, quality) =>
                {
                    MilUnitFC target = opts.getEditTarget?.Invoke();
                    if (target != null) target.SetApparel(item, stuff, quality);
                },
                titleKey: "fcPickApparel",
                conflictTooltipFunc: t => GetConflictTooltip(currentApparel, t, body)
            ));
        }

        private static void OpenApparelReplacePicker(MilUnitFC displayUnit, SavedThing current, Options opts)
        {
            ThingDef raceDef = displayUnit?.pawnKind?.race;
            BodyDef body = raceDef?.race?.body ?? BodyDefOf.Human;
            List<SavedThing> currentApparel = displayUnit?.apparel ?? new List<SavedThing>();

            // Static predicates (IsApparel/PawnCanWear) are cached in the pool; only the live research
            // + race gates run per open (the replace picker keeps the currently-worn item selectable).
            List<ThingDef> apparelDefs = MilitaryEquipmentPoolUtil.ApparelPool()
                .Where(t => CraftUtil.CanCraftItem(t)
                    && HARUtil.CanRaceWearApparel(raceDef, t))
                .OrderBy(t => t.label)
                .ToList();

            List<SavedThing> otherApparel = currentApparel.Where(a => a.thing != current.thing).ToList();
            Find.WindowStack.Add(new FCWindow_ItemStuffPicker(
                apparelDefs,
                onConfirm: (item, stuff, quality) =>
                {
                    MilUnitFC target = opts.getEditTarget?.Invoke();
                    if (target != null) target.SetApparel(item, stuff, quality);
                },
                onUnequip: () =>
                {
                    MilUnitFC target = opts.getEditTarget?.Invoke();
                    if (target != null) target.RemoveApparel(current.thing);
                },
                titleKey: "fcPickApparel",
                initialItem: current.thing,
                initialStuff: current.stuff,
                conflictTooltipFunc: t => GetConflictTooltip(otherApparel, t, body),
                initialQuality: current.quality
            ));
        }

        private static void OpenColorPicker(Color current, Action<Color> onApply)
        {
            Find.WindowStack.Add(new FCWindow_ColorPicker(
                "fcChooseApparelColor".Translate(),
                current,
                onApply
            ));
        }

        /// <summary>
        /// Returns a tooltip listing which worn apparel would be replaced by the candidate, or null if compatible.
        /// </summary>
        private static string GetConflictTooltip(List<SavedThing> worn, ThingDef candidate, BodyDef body)
        {
            List<string> conflicts = worn
                .Where(a => a.thing != null && !ApparelUtility.CanWearTogether(a.thing, candidate, body))
                .Select(a => a.thing.LabelCap.ToString())
                .ToList();
            if (conflicts.Count == 0) return null;
            return "fcReplacesApparel".Translate() + ":\n" + string.Join("\n", conflicts.ToArray());
        }
    }
}
