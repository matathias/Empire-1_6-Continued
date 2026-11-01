using FactionColonies.util;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /* Shared mechanitor panel for the unit designer's Mechs tab (Biotech only). A "make mechanitor"
     * toggle gates the rest: when on, the unit is given a mechlink at spawn and can be assigned mechs.
     * Bandwidth (read from the preview pawn's MechBandwidth stat — so control-sublink implants and
     * bandwidth-pack apparel count) limits how many mechs fit, counted across all groups; the add picker
     * hard-blocks an over-budget add. Mechs are organized into control groups (count from the
     * MechControlGroups stat): each group is a highlighted header strip with its own work-mode chooser
     * and Add Mech button (adds straight into that group), with the group's mechs listed below it and a
     * gradient separator between groups. Each mech row has a group selector to move it. Mirrors
     * ImplantListWidget/PsycastListWidget: reads displayUnit, routes mutations via getEditTarget. */
    public static class MechListWidget
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
        private const float groupGap = 12f;
        private static readonly Color separatorColor = new Color(0.6f, 0.6f, 0.6f, 0.85f);

        public static void Draw(Rect rect, MilUnitFC displayUnit, ref Vector2 scrollPos, Options opts)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            // Biotech-absent guard (the tab is normally hidden, but be defensive).
            if (!ModsConfig.BiotechActive)
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.ClampedLabel(rect, "fcMechsNoBiotech".Translate());
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;
                return;
            }

            bool editable = opts.canEdit && opts.showHeaderButtons;

            // --- "Make mechanitor" toggle ---
            Rect toggleRect = new Rect(rect.x, rect.y, rect.width, headerHeight);
            bool isMech = displayUnit?.isMechanitor ?? false;
            bool newIsMech = isMech;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            if (editable)
            {
                Widgets.CheckboxLabeled(toggleRect, "fcMakeMechanitor".Translate(), ref newIsMech);
                if (newIsMech != isMech)
                {
                    MilUnitFC t = opts.getEditTarget?.Invoke();
                    if (t != null) t.SetMechanitor(newIsMech);
                }
            }
            else
            {
                UIUtil.ClampedLabel(toggleRect, "fcMakeMechanitor".Translate() + ": " + (isMech ? "Yes" : "No"));
            }

            // When not a mechanitor, show a hint and stop.
            if (!(displayUnit?.IsMechanitorDesign ?? false) && !newIsMech)
            {
                Rect hintRect = new Rect(rect.x, toggleRect.yMax + 4f, rect.width, rect.height - headerHeight - 6f);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperLeft;
                UIUtil.ClampedLabel(hintRect.ContractedBy(4f), "fcMechsHint".Translate());
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;
                return;
            }

            // --- Bandwidth summary (full width; Add Mech lives per-group below) ---
            float used = displayUnit?.UsedMechBandwidth ?? 0f;
            float total = displayUnit?.TotalMechBandwidth ?? 0f;
            Rect bwRect = new Rect(rect.x, toggleRect.yMax + 2f, rect.width, headerHeight);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            if (used > total + 0.0001f) GUI.color = ColorLibrary.RedReadable;
            UIUtil.ClampedLabel(bwRect, "fcMechBandwidth".Translate(used.ToString("0.#"), total.ToString("0.#")));
            GUI.color = Color.white;

            // --- Grouped mech list ---
            int groupCount = displayUnit != null ? Mathf.Max(1, displayUnit.MechGroupCount) : 1;
            List<SavedMech> items = displayUnit?.mechs ?? new List<SavedMech>();

            // Bucket mech indices by their (clamped) control group.
            List<List<int>> buckets = new List<List<int>>();
            for (int g = 0; g < groupCount; g++) buckets.Add(new List<int>());
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].kind?.race is null) continue;
                int eg = items[i].group;
                if (eg < 0) eg = 0;
                if (eg > groupCount - 1) eg = groupCount - 1;
                buckets[eg].Add(i);
            }

            float listTop = bwRect.yMax + 4f;
            Rect listOutRect = new Rect(rect.x, listTop, rect.width, rect.height - (listTop - rect.y));
            float viewHeight = 0f;
            for (int g = 0; g < groupCount; g++) viewHeight += headerHeight + buckets[g].Count * rowHeight;
            viewHeight += (groupCount - 1) * groupGap;
            Rect scrollViewRect = ScrollUtil.BeginScrollView(listOutRect, ref scrollPos, viewHeight);

            float y = scrollViewRect.y;
            for (int g = 0; g < groupCount; g++)
            {
                // Gap + gradient separator line between groups.
                if (g > 0)
                {
                    TexLoad.DrawHorizontalPeakGradientLine(scrollViewRect.x + 8f, y + groupGap * 0.5f,
                        scrollViewRect.width - 16f, separatorColor);
                    y += groupGap;
                }

                // Group header (the only highlighted strip): "Group N" + work-mode + Add Mech buttons.
                Rect gHeader = new Rect(scrollViewRect.x, y, scrollViewRect.width, headerHeight);
                Widgets.DrawHighlight(gHeader);
                MechWorkModeDef gMode = displayUnit?.GetGroupWorkMode(g) ?? MechWorkModeDefOf.Escort;

                float gAddW = 90f;
                float gWmW = 150f;
                Rect addBtnRect = new Rect(gHeader.xMax - gAddW - 2f, gHeader.y + 1f, gAddW, headerHeight - 2f);
                Rect wmRect = new Rect(addBtnRect.x - gWmW - 4f, gHeader.y + 1f, gWmW, headerHeight - 2f);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect gLabelRect = new Rect(gHeader.x + 4f, gHeader.y, wmRect.x - gHeader.x - 8f, headerHeight);
                UIUtil.ClampedLabel(gLabelRect, "fcMechGroup".Translate(g + 1));

                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                if (editable)
                {
                    int capturedGroup = g;
                    if (UIUtil.ClampedButtonText(wmRect, "fcMechWorkMode".Translate() + ": " + gMode.LabelCap))
                    {
                        List<FloatMenuOption> modeOpts = new List<FloatMenuOption>();
                        foreach (MechWorkModeDef mode in DefDatabase<MechWorkModeDef>.AllDefsListForReading)
                        {
                            MechWorkModeDef captured = mode;
                            modeOpts.Add(new FloatMenuOption(mode.LabelCap, delegate
                            {
                                MilUnitFC t = opts.getEditTarget?.Invoke();
                                if (t != null) t.SetGroupWorkMode(capturedGroup, captured);
                            }));
                        }
                        if (modeOpts.Count > 0) Find.WindowStack.Add(new FloatMenu(modeOpts));
                    }

                    if (UIUtil.ClampedButtonText(addBtnRect, "fcAddMech".Translate()))
                    {
                        Func<MilUnitFC> getDisplay = opts.getDisplayUnit ?? (() => displayUnit);
                        Find.WindowStack.Add(new FCWindow_MechPicker(getDisplay, opts.getEditTarget, capturedGroup));
                    }
                }
                else
                {
                    UIUtil.ClampedLabel(new Rect(wmRect.x, wmRect.y, addBtnRect.xMax - wmRect.x, wmRect.height),
                        "fcMechWorkMode".Translate() + ": " + gMode.LabelCap);
                }
                y += headerHeight;

                // Mech rows in this group (no row highlight — separation comes from the group header
                // strip and the gradient line; mouseover gives feedback).
                foreach (int index in buckets[g])
                {
                    SavedMech item = items[index];
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
                            if (target != null) target.RemoveMech(index);
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
                            if (target != null) target.DecrementMech(index);
                        }
                        UIUtil.ClampedLabel(countRect, "x" + Mathf.Max(1, item.count));
                        if (UIUtil.ClampedButtonText(plusRect, "+"))
                        {
                            MilUnitFC target = opts.getEditTarget?.Invoke();
                            if (target != null) target.AddMech(item.kind, item.group);   // hard-blocks on bandwidth
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

                    // Group selector ( G2 ) — only useful when there's more than one group.
                    if (editable && groupCount > 1)
                    {
                        float grpW = 34f;
                        Rect grpRect = new Rect(cursorRight - grpW, row.y + 2f, grpW, rowHeight - 4f);
                        Text.Font = GameFont.Tiny;
                        Text.Anchor = TextAnchor.MiddleCenter;
                        if (UIUtil.ClampedButtonText(grpRect, "G" + (g + 1)))
                        {
                            List<FloatMenuOption> grpOpts = new List<FloatMenuOption>();
                            for (int dest = 0; dest < groupCount; dest++)
                            {
                                int capturedDest = dest;
                                grpOpts.Add(new FloatMenuOption("fcMechGroup".Translate(dest + 1), delegate
                                {
                                    MilUnitFC t = opts.getEditTarget?.Invoke();
                                    if (t != null) t.SetMechGroup(index, capturedDest);
                                }));
                            }
                            if (grpOpts.Count > 0) Find.WindowStack.Add(new FloatMenu(grpOpts));
                        }
                        cursorRight = grpRect.x - 6f;
                    }

                    // Bandwidth cost
                    float bandwidth = item.kind.race.GetStatValueAbstract(StatDefOf.BandwidthCost) * Mathf.Max(1, item.count);
                    Rect bwCostRect = new Rect(cursorRight - 45f, row.y, 45f, rowHeight);
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleRight;
                    UIUtil.ClampedLabel(bwCostRect, "BW " + bandwidth.ToString("0.#"));

                    // Label
                    string label = item.kind.LabelCap;
                    Rect labelRect = new Rect(infoRect.xMax + 6f, row.y, bwCostRect.x - infoRect.xMax - 10f, rowHeight);
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    string shownLabel = Text.ClampTextWithEllipsis(labelRect, label);
                    UIUtil.ClampedLabel(labelRect, shownLabel);
                    if (shownLabel != label) TooltipHandler.TipRegion(labelRect, label);

                    y += rowHeight;
                }
            }

            ScrollUtil.EndScrollView();

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }
    }
}
