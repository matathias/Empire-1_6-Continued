using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /* The sub-tabs of a unit's loadout editor (shared by DesignUnitsWindow and Dialog_PawnLoadout).
     * Psycasts and Mechs are conditional (psycast-system present / Biotech active); the tab strip
     * builds a present-tabs list rather than indexing this enum directly, so any subset collapses
     * correctly regardless of declaration order. */
    public enum LoadoutTab
    {
        Apparel,
        Inventory,
        Implants,
        Animals,
        Psycasts,
        Mechs
    }

    /* Draws the loadout tab row using the shared ButtonFlat tab drawer (UIUtil.DrawTabRow) and
     * returns the bordered content area below the tabs. */
    public static class LoadoutTabStrip
    {
        public const float TabHeight = 28f;

        public static LoadoutTab Draw(Rect boundingBox, LoadoutTab selected, out Rect contentRect,
            bool includePsycasts = true, bool includeMechs = false, bool includeAnimals = true)
        {
            // Build the list of present tabs in display order, with a parallel label list. Conditional
            // tabs are appended only when enabled, so a hidden tab never shifts another tab's index.
            List<LoadoutTab> present = new List<LoadoutTab>
            {
                LoadoutTab.Apparel,
                LoadoutTab.Inventory,
                LoadoutTab.Implants
            };
            List<string> labels = new List<string>
            {
                "fcTabApparel".Translate(),
                "fcTabInventory".Translate(),
                "fcTabImplants".Translate()
            };
            if (includeAnimals)
            {
                present.Add(LoadoutTab.Animals);
                labels.Add("fcTabAnimals".Translate());
            }
            if (includePsycasts)
            {
                present.Add(LoadoutTab.Psycasts);
                labels.Add("fcTabPsycasts".Translate());
            }
            if (includeMechs)
            {
                present.Add(LoadoutTab.Mechs);
                labels.Add("fcTabMechs".Translate());
            }

            // Map the current selection to its slot in the present list; clamp a now-hidden tab to 0.
            int selectedIdx = present.IndexOf(selected);
            if (selectedIdx < 0) selectedIdx = 0;

            int idx = UIUtil.DrawTabRow(boundingBox, labels, selectedIdx, out contentRect,
                tabHeight: TabHeight, minTabWidth: 70f);
            return present[idx];
        }
    }
}
