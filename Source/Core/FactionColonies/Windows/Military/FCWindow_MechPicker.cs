using FactionColonies.util;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Modal picker that lists the player-controllable mechanoids the player has researched
    /// (<see cref="FactionCache.UnlockedControllableMechKinds"/>) that a mechanitor merc can be
    /// assigned. Each row shows the mech's bandwidth cost and silver cost.
    /// Adding a mech routes through <see cref="MilUnitFC.AddMech"/>, which hard-blocks the add when it
    /// would exceed the unit's available bandwidth. Stays open so several mechs can be queued; a live
    /// "used / total bandwidth" header reflects each add (every add regenerates the preview pawn used
    /// to read the bandwidth stat). Biotech-only — the Mechs tab only opens it when Biotech is active.
    /// </summary>
    public class FCWindow_MechPicker : Window
    {
        private readonly Func<MilUnitFC> getDisplayUnit;
        private readonly Func<MilUnitFC> getEditTarget;
        private readonly int group;

        private string searchTerm = "";
        private Vector2 scrollPos;

        private const float RowHeight = 30f;
        private const float SearchBarHeight = 28f;
        private const float HeaderHeight = 24f;
        private const float margin = 5f;

        public override Vector2 InitialSize => new Vector2(480f, 600f);

        public FCWindow_MechPicker(Func<MilUnitFC> getDisplayUnit, Func<MilUnitFC> getEditTarget, int group)
        {
            this.getDisplayUnit = getDisplayUnit;
            this.getEditTarget = getEditTarget;
            this.group = group;
            forcePause = false;
            draggable = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            MilUnitFC displayUnit = getDisplayUnit?.Invoke();

            // Title — names the group the picked mech will be added to.
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            UIUtil.ClampedLabel(new Rect(0, 0, inRect.width, 35f),
                "fcPickMech".Translate() + " — " + "fcMechGroup".Translate(group + 1));

            // Bandwidth header
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            float used = displayUnit?.UsedMechBandwidth ?? 0f;
            float total = displayUnit?.TotalMechBandwidth ?? 0f;
            Rect bwRect = new Rect(0, 38f, inRect.width, HeaderHeight);
            if (used > total + 0.0001f) GUI.color = ColorLibrary.RedReadable;
            UIUtil.ClampedLabel(bwRect, "fcMechBandwidth".Translate(used.ToString("0.#"), total.ToString("0.#")));
            GUI.color = Color.white;

            // Search bar
            Rect searchRect = new Rect(0, bwRect.yMax + 2f, inRect.width, SearchBarHeight);
            searchTerm = Widgets.TextField(searchRect, searchTerm);

            // Build mech list — only mechs the player has actually unlocked through research.
            List<PawnKindDef> mechKinds = FactionCache.UnlockedControllableMechKinds
                .OrderBy(m => m.label ?? m.defName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrEmpty(searchTerm))
                mechKinds = mechKinds.Where(m => (m.label ?? m.defName).IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            // Scroll view
            float listTop = searchRect.yMax + margin;
            float listHeight = inRect.height - listTop - 15f;
            Rect scrollOutRect = new Rect(0, listTop, inRect.width, listHeight);
            Widgets.DrawMenuSection(scrollOutRect);

            float viewHeight = mechKinds.Count * RowHeight;
            Rect scrollViewRect = ScrollUtil.BeginScrollView(scrollOutRect, ref scrollPos, viewHeight);

            for (int i = 0; i < mechKinds.Count; i++)
            {
                PawnKindDef mech = mechKinds[i];
                Rect row = new Rect(0, i * RowHeight, scrollViewRect.width, RowHeight);
                if (i % 2 == 0) Widgets.DrawHighlight(row);

                // Row layout: Icon | Info | Label | Bandwidth | Cost
                Rect iconRect = new Rect(row.x + margin, row.y, RowHeight, RowHeight);
                Rect infoRect = new Rect(iconRect.xMax, row.y + 2, RowHeight - 4, RowHeight - 4);
                Rect costRect = new Rect(row.xMax - margin - 60f, row.y, 60f, RowHeight);
                Rect bwCostRect = new Rect(costRect.x - 50f, row.y, 50f, RowHeight);
                Rect labelRect = new Rect(infoRect.xMax + margin, row.y,
                    bwCostRect.x - infoRect.xMax - (margin * 2), RowHeight);

                Widgets.ThingIcon(iconRect, mech.race);
                Widgets.InfoCardButton(infoRect, mech.race);

                Text.Anchor = TextAnchor.MiddleLeft;
                Text.Font = GameFont.Small;
                UIUtil.ClampedLabel(labelRect, mech.LabelCap);

                float bandwidth = mech.race.GetStatValueAbstract(StatDefOf.BandwidthCost);
                Text.Anchor = TextAnchor.MiddleRight;
                Text.Font = GameFont.Tiny;
                UIUtil.ClampedLabel(bwCostRect, "BW " + bandwidth.ToString("0.#"));

                double cost = Math.Floor(mech.race.BaseMarketValue * FCSettings.militaryMechCostMultiplier);
                UIUtil.ClampedLabel(costRect, "$" + cost.ToString("F0"));

                if (Widgets.ButtonInvisible(row))
                {
                    MilUnitFC target = getEditTarget?.Invoke();
                    target?.AddMech(mech, group);   // adds to this picker's group; hard-blocks on bandwidth
                }
            }

            ScrollUtil.EndScrollView();

            // Empty-state label: drawn AFTER EndScrollView so it centers in the visible box
            // (drawing it inside the scroll view's translated space pushes it awkwardly low).
            if (mechKinds.Count == 0)
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.ClampedLabel(scrollOutRect, "fcNoMechsAvailable".Translate());
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }
    }
}
