using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Settlement-picker dialog used by the HireSquadsWindow's per-row "Reassign" action.
    /// Lists every Empire settlement with an indicator showing remaining cap room, and the
    /// option to "Unassign" (return the squad to the pool). On confirm calls
    /// <see cref="MilitaryFC.AttemptToAssign"/> or <see cref="MilitaryFC.Unassign"/>.
    /// Settlements at cap open a sub-menu listing their current squads to displace, dispatched
    /// via <see cref="MilitaryFC.AttemptToSwap"/>.
    /// </summary>
    public class Dialog_SquadAssignment : Window
    {
        public override Vector2 InitialSize => new Vector2(480f, 460f);

        private const float TitleHeight = 30f;
        private const float CostHeaderHeight = 22f;
        private const float HeaderHeight = TitleHeight + CostHeaderHeight;
        private const float RowHeight = 32f;

        private readonly MercenarySquadFC squad;
        private Vector2 scroll;

        public Dialog_SquadAssignment(MercenarySquadFC squad)
        {
            this.squad = squad;
            doCloseX = true;
            forcePause = false;
            absorbInputAroundWindow = true;
            draggable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            int squadDeploy = squad.DeploymentCost();

            Rect titleHighlightRect = new Rect(0, 0, inRect.width, TitleHeight);
            Widgets.DrawHighlight(titleHighlightRect);

            Text.Font = GameFont.Medium;
            UIUtil.ClampedLabelWithMargin(titleHighlightRect, "FCDialogSquadAssignmentHeader".Translate(squad?.DisplayName ?? "(?)"));
            Text.Font = GameFont.Small;
            UIUtil.LabelWithMargin(new Rect(4f, TitleHeight, inRect.width, CostHeaderHeight),
                "FCDialogSquadAssignmentCostHeader".Translate(squadDeploy));

            float listTop = HeaderHeight + 6f;
            float btnH = 36f;
            float listH = inRect.height - listTop - btnH - 8f;
            Rect listRect = new Rect(0, listTop, inRect.width, listH);

            DrawList(listRect, squadDeploy);

            float btnY = inRect.height - btnH;
            if (UIUtil.ClampedButtonText(new Rect(inRect.width - 160f, btnY, 150f, 32f), "Close".Translate()))
                Close();
        }

        /* Per-row state precomputed once so the sort key and the render path share one
         * source of truth. atCap/allBusy/tooExpensive mirror the gates inside
         * MilitaryCustomizationUtil.AttemptToAssign + SquadValueValidator, so the dialog
         * and the actual validator never disagree. */
        private class RowData
        {
            public WorldSettlementFC settlement;
            public int stationed;
            public int cap;
            public bool isHere;
            public bool atCap;
            public bool allDisplaceableBusy;
            public bool tooExpensive;
            public int maxDeploy;
            public bool Disabled => tooExpensive || (atCap && allDisplaceableBusy);
        }

        private void DrawList(Rect rect, int squadDeploy)
        {
            FactionFC fc = FindFC.FactionComp;
            MilitaryFC mfc = fc?.military;
            if (fc is null || mfc is null) return;

            List<RowData> rows = new List<RowData>();
            if (fc.settlements is object)
            {
                foreach (WorldSettlementFC s in fc.settlements)
                {
                    if (s is null) continue;
                    RowData r = new RowData
                    {
                        settlement = s,
                        stationed = s.StationedSquads.Count,
                        cap = s.SquadCap,
                        isHere = squad?.settlement == s,
                    };
                    r.atCap = !r.isHere && r.stationed >= r.cap;
                    r.allDisplaceableBusy = r.atCap && s.StationedSquads.All(q => q is null || q.IsBusy);
                    r.tooExpensive = !r.isHere && MilitaryFC.SquadExceedsSettlementBudget(
                        squad, s, out _, out r.maxDeploy);
                    if (r.isHere)
                    {
                        double budget = MilitaryFC.CalculateSquadBudget(s.settlementMilitaryLevel);
                        r.maxDeploy = MilitaryDeploymentUtil.CalculateDeploymentCost(budget);
                    }
                    rows.Add(r);
                }
                // Stable sort: disabled rows last, preserving original order within each bucket.
                rows = rows.OrderBy(r => r.Disabled ? 1 : 0).ToList();
            }

            int rowCount = rows.Count + 1; // +1 for unassign row
            float contentHeight = rowCount * RowHeight;
            Rect viewRect = ScrollUtil.BeginScrollView(rect, ref scroll, contentHeight);

            int row = 0;

            // Unassign row
            Rect unassignRect = new Rect(0, row * RowHeight, viewRect.width, RowHeight);
            if (row % 2 == 0) Widgets.DrawHighlight(unassignRect);
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.ClampedLabel(new Rect(unassignRect.x + 8f, unassignRect.y, unassignRect.width - 100f, RowHeight),
                "FCDialogSquadAssignmentUnassign".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonInvisible(unassignRect))
            {
                mfc.Unassign(squad);
                Close();
            }
            row++;

            foreach (RowData r in rows)
            {
                Rect rowRect = new Rect(0, row * RowHeight, viewRect.width, RowHeight);
                if (row % 2 == 0) Widgets.DrawHighlight(rowRect);

                bool disabled = r.Disabled;
                bool clickable = !disabled;

                Color colorBefore = GUI.color;
                if (disabled) GUI.color = new Color(0.6f, 0.6f, 0.6f);
                else if (r.atCap) GUI.color = new Color(0.9f, 0.85f, 0.6f); // swap-target tint
                else if (r.isHere) GUI.color = new Color(0.6f, 0.9f, 0.6f);

                // Far-right: stationed / cap. Middle: max deployment budget (Tiny). Left: name.
                Rect countRect = new Rect(rowRect.xMax - 60f, rowRect.y, 50f, RowHeight);
                Rect budgetRect = new Rect(countRect.x - 200f, rowRect.y, 200f, RowHeight);
                Rect nameRect = new Rect(rowRect.x + 8f, rowRect.y, budgetRect.x - rowRect.x - 12f, RowHeight);

                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.ClampedLabel(nameRect, r.settlement.Name ?? "?");

                GameFont fontBefore = Text.Font;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleRight;
                UIUtil.ClampedLabel(budgetRect,
                    "FCDialogSquadAssignmentBudgetLabel".Translate(r.maxDeploy));
                Text.Font = fontBefore;

                Text.Anchor = TextAnchor.MiddleRight;
                UIUtil.ClampedLabel(countRect, r.stationed + " / " + r.cap);

                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = colorBefore;

                if (disabled)
                {
                    if (r.tooExpensive)
                    {
                        TooltipHandler.TipRegion(rowRect,
                            "FCDialogSquadAssignmentCostExceededTip".Translate(squadDeploy, r.maxDeploy));
                    }
                    else
                    {
                        TooltipHandler.TipRegion(rowRect, "FCDialogSquadAssignmentAllBusyTip".Translate());
                    }
                }
                else if (clickable && Widgets.ButtonInvisible(rowRect))
                {
                    if (r.atCap)
                    {
                        OpenDisplaceMenu(mfc, r.settlement);
                    }
                    else if (mfc.AttemptToAssign(squad, r.settlement))
                    {
                        Close();
                    }
                }
                row++;
            }

            ScrollUtil.EndScrollView();
        }

        /// <summary>Opens a sub-menu listing the target settlement's current squads and lets the
        /// player pick which one to displace. Busy squads are greyed. Picking dispatches through
        /// <see cref="MilitaryFC.AttemptToSwap"/>.</summary>
        private void OpenDisplaceMenu(MilitaryFC mfc, WorldSettlementFC target)
        {
            List<FloatMenuOption> opts = new List<FloatMenuOption>();
            foreach (MercenarySquadFC occupant in target.StationedSquads)
            {
                if (occupant is null) continue;
                MercenarySquadFC capturedOccupant = occupant;
                System.Action onPick = occupant.IsBusy ? (System.Action)null : delegate
                {
                    if (mfc.AttemptToSwap(squad, target, capturedOccupant))
                    {
                        Close();
                    }
                };
                opts.Add(new FloatMenuOption(
                    "FCDialogSquadAssignmentDisplaceLabel".Translate(occupant.DisplayName),
                    onPick));
            }

            Find.WindowStack.Add(new FloatMenu(opts));
        }
    }
}
