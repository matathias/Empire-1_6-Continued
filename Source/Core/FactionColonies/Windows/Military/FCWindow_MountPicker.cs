using FactionColonies.util;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Single-select picker for a merc's rideable mount (Giddy Up 2). Lists only the animal kinds GU2
    /// considers mountable (via <see cref="GiddyUpUtil.IsMountable"/>); Confirm sets the mount, Unequip
    /// clears it. Only opened from the gear-panel Mount slot, which itself is shown only when Giddy Up 2
    /// is active. Mirrors the single-select layout of the old animal picker.
    /// </summary>
    public class FCWindow_MountPicker : Window
    {
        private readonly Action<PawnKindDef> onConfirm;
        private readonly Action onUnequip;
        private PawnKindDef selectedDef;
        private string searchTerm = "";
        private Vector2 scrollPos;

        private const float RowHeight = 30f;
        private const float SearchBarHeight = 28f;
        private const float ButtonHeight = 35f;
        private const float margin = 5f;

        public override Vector2 InitialSize => new Vector2(450f, 550f);

        public FCWindow_MountPicker(PawnKindDef initialMount, Action<PawnKindDef> onConfirm, Action onUnequip)
        {
            selectedDef = initialMount;
            this.onConfirm = onConfirm;
            this.onUnequip = onUnequip;
            draggable = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            forcePause = false;
        }

        /* Convenience constructor: sets unit.mount directly. Used by DesignUnitsWindow. */
        public FCWindow_MountPicker(MilUnitFC unit)
            : this(unit?.mount,
                  picked => { unit?.SetMount(picked); },
                  () => { unit?.SetMount(null); })
        {
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            // Title
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            UIUtil.ClampedLabel(new Rect(0, 0, inRect.width, 35f), "fcPickMount".Translate());

            // Search bar
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect searchRect = new Rect(0, 40f, inRect.width, SearchBarHeight);
            searchTerm = Widgets.TextField(searchRect, searchTerm);

            // Build the list of mountable animal kinds (GU2 rules), gated by any submod animal filter
            // (e.g. Herds & Fisheries stocking). No filter registered => only the GU2 mountable check applies.
            List<PawnKindDef> animals = FactionCache.AllAnimalKindDefs
                .Where(a => GiddyUpUtil.IsMountable(a) && AnimalPickerFilterRegistry.IsAllowed(a))
                .OrderBy(a => a.label ?? a.defName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrEmpty(searchTerm))
                animals = animals.Where(a => (a.label ?? a.defName).IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            // Scroll view
            float listTop = searchRect.yMax + margin;
            float listHeight = inRect.height - listTop - ButtonHeight - 15f;
            Rect scrollOutRect = new Rect(0, listTop, inRect.width, listHeight);
            Widgets.DrawMenuSection(scrollOutRect);

            float viewHeight = animals.Count * RowHeight;
            Rect scrollViewRect = ScrollUtil.BeginScrollView(scrollOutRect, ref scrollPos, viewHeight);

            for (int i = 0; i < animals.Count; i++)
            {
                PawnKindDef animal = animals[i];
                Rect row = new Rect(0, i * RowHeight, scrollViewRect.width, RowHeight);

                if (animal == selectedDef)
                    Widgets.DrawHighlightSelected(row);
                else if (i % 2 == 0)
                    Widgets.DrawHighlight(row);

                Rect iconRect = new Rect(row.x + margin, row.y, RowHeight, RowHeight);
                Rect infoRect = new Rect(iconRect.xMax, row.y + 2, RowHeight - 4, RowHeight - 4);
                Rect costRect = new Rect(row.xMax - margin - 70f, row.y, 60f, RowHeight);
                Rect labelRect = new Rect(infoRect.xMax + margin, row.y,
                    costRect.x - infoRect.xMax - (margin * 2), RowHeight);

                Widgets.ThingIcon(iconRect, animal.race);
                Widgets.InfoCardButton(infoRect, animal.race);

                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.ClampedLabel(labelRect, animal.LabelCap);

                Text.Anchor = TextAnchor.MiddleRight;
                double cost = Math.Floor(animal.race.BaseMarketValue * FCSettings.militaryAnimalCostMultiplier);
                UIUtil.ClampedLabel(costRect, "$" + cost.ToString("F0"));

                if (Widgets.ButtonInvisible(row))
                {
                    selectedDef = animal;
                }
            }

            if (animals.Count == 0)
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.ClampedLabel(scrollOutRect, "fcNoMountsAvailable".Translate());
            }

            ScrollUtil.EndScrollView();

            // Bottom buttons
            float buttonWidth = 120f;
            Rect buttonBar = new Rect(0, inRect.height - ButtonHeight - 5f, inRect.width, ButtonHeight);

            // Unequip (left)
            Rect unequipRect = new Rect(buttonBar.x, buttonBar.y, buttonWidth, buttonBar.height);
            if (UIUtil.ClampedButtonText(unequipRect, "FCUnitActionUnequipThing".Translate()))
            {
                if (onUnequip != null) onUnequip();
                Close();
            }

            // Cancel (right)
            Rect cancelRect = new Rect(buttonBar.xMax - buttonWidth, buttonBar.y, buttonWidth, buttonBar.height);
            if (UIUtil.ClampedButtonText(cancelRect, "CancelButton".Translate()))
            {
                Close();
            }

            // Confirm (left of cancel)
            bool canConfirm = selectedDef != null;
            Rect confirmRect = new Rect(cancelRect.x - buttonWidth - 10f, buttonBar.y, buttonWidth, buttonBar.height);
            if (UIUtil.ClampedButtonText(confirmRect, "FCConfirm".Translate(), active: canConfirm))
            {
                if (canConfirm)
                {
                    if (onConfirm != null) onConfirm(selectedDef);
                    Close();
                }
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }
    }
}
