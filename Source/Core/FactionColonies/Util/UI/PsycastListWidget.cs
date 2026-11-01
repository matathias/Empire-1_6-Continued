using FactionColonies.util;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /* Shared psycasts/psycasts panel for the unit designer's Psycasts tab. A psylink-level stepper at
     * the top gates how powerful the unit's psycasts are. Below it the panel adapts to the active psycast
     * system:
     *   - VPE (SupportsExplicitSelection): an "Edit Psycasts" button opens VPE's own picker window, and a
     *     read-only list shows the chosen psycasts with cost.
     *   - Base game (Royalty): no picker — a note explains psycasts are granted randomly at this psylink
     *     level when the unit deploys (vanilla behavior). */
    public static class PsycastListWidget
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
        private const float IconSize = 24f;
        private const float stepperButtonW = 24f;

        public static void Draw(Rect rect, MilUnitFC displayUnit, ref Vector2 scrollPos, Options opts)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            IPsycastSystemProvider active = PsycastSystemRegistry.Active;

            // No psycast system available — explain and bail.
            if (active is null)
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.ClampedLabel(rect, "fcPsycastsNoSystem".Translate());
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;
                return;
            }

            // --- Psylink stepper row ---
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, headerHeight);
            int maxLevel = active.MaxPsylinkLevel;
            int curLevel = displayUnit?.psylinkLevel ?? 0;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect psyLabelRect = new Rect(headerRect.x, headerRect.y, 120f, headerHeight);
            UIUtil.ClampedLabel(psyLabelRect, "fcPsylinkLevel".Translate() + ": " + curLevel);

            // getEditTarget is resolved lazily inside each click handler (it may allocate a buffered
            // working copy and mark the host dialog dirty), so merely viewing this tab mutates nothing.
            bool editable = opts.canEdit && opts.showHeaderButtons;
            if (editable)
            {
                Rect minusRect = new Rect(psyLabelRect.xMax, headerRect.y + (headerHeight - stepperButtonW) / 2f, stepperButtonW, stepperButtonW);
                Rect plusRect = new Rect(minusRect.xMax + 2f, minusRect.y, stepperButtonW, stepperButtonW);
                Text.Anchor = TextAnchor.MiddleCenter;
                if (UIUtil.ClampedButtonText(minusRect, "-") && curLevel > 0)
                {
                    MilUnitFC t = opts.getEditTarget?.Invoke();
                    if (t != null) t.SetPsylinkLevel(curLevel - 1);
                }
                if (UIUtil.ClampedButtonText(plusRect, "+") && curLevel < maxLevel)
                {
                    MilUnitFC t = opts.getEditTarget?.Invoke();
                    if (t != null) t.SetPsylinkLevel(curLevel + 1);
                }

                // VPE: "Edit Psycasts" button (right-aligned). Base game: none.
                if (active.SupportsExplicitSelection)
                {
                    float btnW = 130f;
                    Rect editBtnRect = new Rect(headerRect.xMax - btnW, headerRect.y, btnW, headerHeight);
                    bool canEditPsycasts = curLevel > 0;
                    if (canEditPsycasts)
                    {
                        if (UIUtil.ClampedButtonText(editBtnRect, "fcEditPsycasts".Translate()))
                        {
                            MilUnitFC t = opts.getEditTarget?.Invoke();
                            if (t != null) active.OpenEditor(t, delegate { t.ChangeTick(); });
                        }
                    }
                    else
                    {
                        GUI.color = Color.gray;
                        UIUtil.ClampedButtonText(editBtnRect, "fcEditPsycasts".Translate(), active: false);
                        GUI.color = Color.white;
                        TooltipHandler.TipRegion(editBtnRect, "fcPsycastsNeedPsylink".Translate());
                    }
                }
            }

            Rect bodyRect = new Rect(rect.x, headerRect.yMax + 2f, rect.width, rect.height - headerHeight - 4f);

            // --- Base game: explanatory note, no list ---
            if (!active.SupportsExplicitSelection)
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.Label(bodyRect.ContractedBy(4f), "fcPsycastsRandomNote".Translate());
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;
                return;
            }

            // --- VPE: point summary + read-only list of chosen psycasts + Clear ---
            int spent, budget;
            if (active.TryGetPointBudget(displayUnit, out spent, out budget))
            {
                Rect summaryRect = new Rect(bodyRect.x, bodyRect.y, bodyRect.width, 18f);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.ClampedLabel(summaryRect, "fcPsycastPointsSummary".Translate(spent, budget));
                bodyRect.yMin += 20f;
            }

            // Reserve a bottom strip for the Clear button (only when this unit is editable).
            bool canClear = opts.canEdit && opts.showHeaderButtons;
            if (canClear)
            {
                Rect clearRect = new Rect(bodyRect.x, bodyRect.yMax - 26f, 120f, 24f);
                bodyRect.height -= 30f;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                if (UIUtil.ClampedButtonText(clearRect, "fcClearPsycasts".Translate()))
                {
                    MilUnitFC t = opts.getEditTarget?.Invoke();
                    if (t != null) t.SetPsycastsForSystem(active.Key, new List<SavedPsycast>());
                }
            }

            // Display picks in their stored order — i.e. the order they were chosen — so the list reads
            // the same way trimming removes them (most recent last).
            var items = displayUnit?.psycasts;
            int count = items?.Count ?? 0;
            float viewHeight = count * rowHeight;
            Rect scrollViewRect = ScrollUtil.BeginScrollView(bodyRect, ref scrollPos, viewHeight);

            for (int i = 0; i < count; i++)
            {
                SavedPsycast item = items[i];
                IPsycastSystemProvider provider = PsycastSystemRegistry.ByKey(item.systemKey);
                PsycastPickEntry entry = null;
                bool resolved = provider is object && provider.TryGetDisplay(item, out entry);
                Rect row = new Rect(scrollViewRect.x, scrollViewRect.y + i * rowHeight, scrollViewRect.width, rowHeight);
                if (i % 2 == 0) Widgets.DrawHighlight(row);

                Rect iconRect = new Rect(row.x + 2f, row.y + 2f, IconSize, IconSize);
                if (resolved && entry.icon != null)
                    GUI.DrawTexture(iconRect, entry.icon);

                Rect costRect = new Rect(row.xMax - 4f - 60f, row.y, 60f, rowHeight);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleRight;
                double cost = resolved ? entry.cost : 0;
                UIUtil.ClampedLabel(costRect, "$" + cost.ToString("F0"));

                string fallback = item.psycastDef.NullOrEmpty() ? (item.kind ?? "?") : item.psycastDef;
                string label = resolved ? entry.label : (fallback + " (?)");
                Rect labelRect = new Rect(iconRect.xMax + 6f, row.y, costRect.x - iconRect.xMax - 10f, rowHeight);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                string shownLabel = Text.ClampTextWithEllipsis(labelRect, label);
                UIUtil.ClampedLabel(labelRect, shownLabel);
                if (resolved && (shownLabel != label || !string.IsNullOrEmpty(entry.description)))
                    TooltipHandler.TipRegion(labelRect, label + (string.IsNullOrEmpty(entry.description) ? "" : "\n\n" + entry.description));
            }

            ScrollUtil.EndScrollView();

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }
    }
}
