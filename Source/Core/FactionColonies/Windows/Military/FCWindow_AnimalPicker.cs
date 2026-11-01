using FactionColonies.util;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Modal picker that lists every available animal kind (<see cref="FactionCache.AllAnimalKindDefs"/>)
    /// a unit can take as a companion. Each row shows the animal's silver cost; clicking adds it via
    /// <see cref="MilUnitFC.AddAnimal"/>, which hard-blocks the add when it would exceed
    /// FCSettings.maxAnimalSubpawns. Stays open so several animals can be queued; a live "N / cap" header
    /// reflects each add. Mirrors <see cref="FCWindow_MechPicker"/>. The single rideable mount is chosen
    /// in a separate window (<see cref="FCWindow_MountPicker"/>), not here.
    /// </summary>
    public class FCWindow_AnimalPicker : Window
    {
        private readonly Func<MilUnitFC> getDisplayUnit;
        private readonly Func<MilUnitFC> getEditTarget;
        private string searchTerm = "";
        private Vector2 scrollPos;

        private const float RowHeight = 30f;
        private const float SearchBarHeight = 28f;
        private const float HeaderHeight = 24f;
        private const float margin = 5f;

        public override Vector2 InitialSize => new Vector2(450f, 600f);

        public FCWindow_AnimalPicker(Func<MilUnitFC> getDisplayUnit, Func<MilUnitFC> getEditTarget)
        {
            this.getDisplayUnit = getDisplayUnit;
            this.getEditTarget = getEditTarget;
            forcePause = false;
            draggable = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        /* Convenience constructor: edits the given unit directly (no buffering). Used by callers
         * that just want to add companions to a unit. */
        public FCWindow_AnimalPicker(MilUnitFC unit)
            : this(() => unit, () => unit)
        {
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            MilUnitFC displayUnit = getDisplayUnit?.Invoke();

            // Title
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            UIUtil.ClampedLabel(new Rect(0, 0, inRect.width, 35f), "fcPickAnimal".Translate());

            // Count header: "N / cap"
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            int total = displayUnit?.TotalAnimalCount ?? 0;
            int cap = FCSettings.maxAnimalSubpawns;
            Rect countRect = new Rect(0, 38f, inRect.width, HeaderHeight);
            if (total >= cap) GUI.color = ColorLibrary.RedReadable;
            UIUtil.ClampedLabel(countRect, "fcAnimalCount".Translate(total, cap));
            GUI.color = Color.white;

            // Search bar
            Rect searchRect = new Rect(0, countRect.yMax + 2f, inRect.width, SearchBarHeight);
            searchTerm = Widgets.TextField(searchRect, searchTerm);

            // Build animal list. Submods (e.g. Herds & Fisheries) can gate the offered kinds via
            // AnimalPickerFilterRegistry; with no filter registered every kind passes (base behavior).
            List<PawnKindDef> animals = FactionCache.AllAnimalKindDefs
                .Where(a => AnimalPickerFilterRegistry.IsAllowed(a))
                .OrderBy(a => a.label ?? a.defName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrEmpty(searchTerm))
                animals = animals.Where(a => (a.label ?? a.defName).IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            // Scroll view
            float listTop = searchRect.yMax + margin;
            float listHeight = inRect.height - listTop - 15f;
            Rect scrollOutRect = new Rect(0, listTop, inRect.width, listHeight);
            Widgets.DrawMenuSection(scrollOutRect);

            float viewHeight = animals.Count * RowHeight;
            Rect scrollViewRect = ScrollUtil.BeginScrollView(scrollOutRect, ref scrollPos, viewHeight);

            for (int i = 0; i < animals.Count; i++)
            {
                PawnKindDef animal = animals[i];
                Rect row = new Rect(0, i * RowHeight, scrollViewRect.width, RowHeight);
                if (i % 2 == 0) Widgets.DrawHighlight(row);

                // Row layout: Icon | Info | Label | Cost
                Rect iconRect = new Rect(row.x + margin, row.y, RowHeight, RowHeight);
                Rect infoRect = new Rect(iconRect.xMax, row.y + 2, RowHeight - 4, RowHeight - 4);
                Rect costRect = new Rect(row.xMax - margin - 70f, row.y, 60f, RowHeight);
                Rect labelRect = new Rect(infoRect.xMax + margin, row.y,
                    costRect.x - infoRect.xMax - (margin * 2), RowHeight);

                Widgets.ThingIcon(iconRect, animal.race);
                Widgets.InfoCardButton(infoRect, animal.race);

                Text.Anchor = TextAnchor.MiddleLeft;
                Text.Font = GameFont.Small;
                UIUtil.ClampedLabel(labelRect, animal.LabelCap);

                Text.Anchor = TextAnchor.MiddleRight;
                Text.Font = GameFont.Tiny;
                double cost = Math.Floor(animal.race.BaseMarketValue * FCSettings.militaryAnimalCostMultiplier);
                UIUtil.ClampedLabel(costRect, "$" + cost.ToString("F0"));

                if (Widgets.ButtonInvisible(row))
                {
                    MilUnitFC target = getEditTarget?.Invoke();
                    target?.AddAnimal(animal);   // hard-blocks on cap
                }
            }

            ScrollUtil.EndScrollView();

            if (animals.Count == 0)
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.ClampedLabel(scrollOutRect, "fcNoAnimalsAvailable".Translate());
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }
    }
}
