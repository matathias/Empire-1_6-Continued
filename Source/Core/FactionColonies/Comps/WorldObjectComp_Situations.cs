using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class WorldObjectCompProperties_Situations : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_Situations()
        {
            compClass = typeof(WorldObjectComp_Situations);
        }
    }

    /// <summary>
    /// Per-settlement Situations panel. Lists this settlement's situations with approach selection and
    /// handler custom actions. The tab is suppressed when the settlement has no situations
    /// (<see cref="ShouldShowOverviewTab"/>). Faction-scoped situations are shown only on the main
    /// colony tab, not here.
    /// </summary>
    public class WorldObjectComp_Situations : WorldObjectComp, ISettlementWindowOverview
    {
        private Vector2 scroll;

        private WorldSettlementFC Settlement => parent as WorldSettlementFC;

        public void PreOpenWindow(WorldSettlementFC settlement) => scroll = Vector2.zero;
        public void OnTabSwitch() => scroll = Vector2.zero;
        public void PostCloseWindow() { }
        public string OverviewTabName() => "FCSituationsTab".Translate();

        public bool ShouldShowOverviewTab(WorldSettlementFC settlement)
        {
            // Guard against load-cache poisoning: never touch FindFC while the Scribe is mid-load.
            if (Scribe.mode != LoadSaveMode.Inactive) return false;
            return FindFC.FactionComp?.situationManager?.AnyForSettlement(settlement) == true;
        }

        public void DrawOverviewTab(Rect boundingBox)
        {
            FactionFC faction = FindFC.FactionComp;
            WorldSettlementFC settlement = Settlement;
            if (faction == null || settlement == null) return;

            List<FCSituation> sits = new List<FCSituation>(faction.situationManager.GetForSettlement(settlement));
            if (sits.Count == 0)
            {
                GameFont f = Text.Font;
                TextAnchor a = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.ClampedLabel(boundingBox, "FCNoSituationsHere".Translate());
                Text.Font = f;
                Text.Anchor = a;
                return;
            }

            const float gap = 4f;
            float contentH = 0f;
            foreach (FCSituation sit in sits) contentH += SituationsUI.RowHeight(sit, includeActions: true) + gap;

            Rect view = ScrollUtil.BeginScrollView(boundingBox, ref scroll, contentH);
            float y = 0f;
            for (int i = 0; i < sits.Count; i++)
            {
                float h = SituationsUI.RowHeight(sits[i], includeActions: true);
                Rect rowRect = new Rect(0f, y, view.width, h);
                if (i % 2 == 0) Widgets.DrawHighlight(rowRect);
                SituationsUI.DrawRow(rowRect, sits[i], faction, showTarget: false, includeActions: true);
                y += h + gap;
            }
            ScrollUtil.EndScrollView();
        }
    }
}
