using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class FCWindow_RacePicker : Window
    {
        private readonly MilUnitFC unit;
        private readonly FactionFC faction;
        private PawnKindDef selectedDef;
        private string searchTerm = "";
        private Vector2 scrollPos;

        private const float RowHeight = 30f;
        private const float IconSize = 24f;
        private const float SearchBarHeight = 28f;
        private const float ButtonHeight = 35f;
        private const float margin = 5f;

        public override Vector2 InitialSize => new Vector2(450f, 550f);

        public FCWindow_RacePicker(MilUnitFC unit, FactionFC faction)
        {
            this.unit = unit;
            this.faction = faction;
            selectedDef = unit.pawnKind;
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
            UIUtil.ClampedLabel(new Rect(0, 0, inRect.width, 35f), "FCChangeUnitRaceButton".Translate());

            // Search bar
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect searchRect = new Rect(0, 40f, inRect.width, SearchBarHeight);
            searchTerm = Widgets.TextField(searchRect, searchTerm);

            // Build race list (same logic as DesignUnitsWindow lines 186-209)
            List<string> seenRaces = new List<string>();
            List<(PawnKindDef def, string label, double cost)> raceOptions = new List<(PawnKindDef, string, double)>();

            foreach (PawnKindDef def in FactionCache.AllPawnKindDefs
                .Where(d => d.IsHumanLikeRace()
                    && !seenRaces.Contains(d.race.label ?? d.race.defName)))
            {
                if (def.race == ThingDefOf.Human && def.LabelCap != "Colonist") continue;
                seenRaces.Add(def.race.label ?? def.race.defName);
                double cost = Math.Floor(def.race.BaseMarketValue * FCSettings.militaryRaceCostMultiplier);
                raceOptions.Add((def, (def.race.label ?? def.race.defName).CapitalizeFirst(), cost));
            }

            raceOptions.Sort((a, b) => string.Compare(a.label, b.label, StringComparison.OrdinalIgnoreCase));

            // Filter by search
            List<(PawnKindDef def, string label, double cost)> filtered = string.IsNullOrEmpty(searchTerm)
                ? raceOptions
                : raceOptions.Where(r => r.label.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            // Scroll view
            float listTop = searchRect.yMax + margin;
            float listHeight = inRect.height - listTop - ButtonHeight - 15f;
            Rect scrollOutRect = new Rect(0, listTop, inRect.width, listHeight);
            Widgets.DrawMenuSection(scrollOutRect);

            float viewHeight = filtered.Count * RowHeight;
            Rect scrollViewRect = ScrollUtil.BeginScrollView(scrollOutRect, ref scrollPos, viewHeight);

            for (int i = 0; i < filtered.Count; i++)
            {
                var (def, label, cost) = filtered[i];
                Rect row = new Rect(0, i * RowHeight, scrollViewRect.width, RowHeight);

                if (def == selectedDef)
                    Widgets.DrawHighlightSelected(row);
                else if (i % 2 == 0)
                    Widgets.DrawHighlight(row);

                Rect iconRect = new Rect(row.x + 2f, row.y + 3f, IconSize, IconSize);
                Widgets.ThingIcon(iconRect, def.race);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect labelRect = new Rect(iconRect.xMax + 5f, row.y, row.width - IconSize - 10f, RowHeight);
                UIUtil.ClampedLabel(labelRect, label + " - Cost: " + cost);

                if (Widgets.ButtonInvisible(row))
                {
                    selectedDef = def;
                }
            }

            if (filtered.Count == 0)
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.ClampedLabel(scrollOutRect, "FCChangeUnitRaceNoRaces".Translate());
            }

            ScrollUtil.EndScrollView();

            // Bottom buttons
            float buttonWidth = 120f;
            Rect buttonBar = new Rect(0, inRect.height - ButtonHeight - 5f, inRect.width, ButtonHeight);

            Rect cancelRect = new Rect(buttonBar.xMax - buttonWidth, buttonBar.y, buttonWidth, buttonBar.height);
            if (UIUtil.ClampedButtonText(cancelRect, "CancelButton".Translate()))
            {
                Close();
            }

            bool canConfirm = selectedDef != null;
            Rect confirmRect = new Rect(cancelRect.x - buttonWidth - 10f, buttonBar.y, buttonWidth, buttonBar.height);
            if (UIUtil.ClampedButtonText(confirmRect, "FCConfirm".Translate(), active: canConfirm))
            {
                if (canConfirm)
                {
                    unit.pawnKind = selectedDef;
                    // Custom xenotypes only apply to humans
                    if (selectedDef?.race != ThingDefOf.Human && unit.customXenotypeName != null)
                    {
                        unit.customXenotypeName = null;
                        if (unit.xenotype == null)
                            unit.xenotype = XenotypeDefOf.Baseliner;
                    }
                    unit.RerollPreviewPawn();
                    // Drop implants that the new race's body can no longer accept.
                    unit.OnRaceChanged();
                    Close();
                }
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }
    }
}
