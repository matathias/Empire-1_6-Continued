using FactionColonies.util;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Squad picker opened by the main-tab Settlement Slot "Set / Change" button. Pure
    /// assignment flow — no offensive op, no defense engagement, so travel time and win
    /// chance are hidden. When the slot already has an occupant, an extra "Unassign"
    /// card sits above the squad list. Confirm dispatches through
    /// <see cref="MilitaryFC.AttemptToAssign"/>,
    /// <see cref="MilitaryFC.AttemptToSwap"/>, or
    /// <see cref="MilitaryFC.Unassign"/> depending on the selection.
    /// </summary>
    public class Dialog_AssignSquadToSettlement : Dialog_SquadPicker
    {
        private readonly WorldSettlementFC target;
        private readonly MercenarySquadFC currentSlotSquad;
        private readonly int maxDeployCost;
        private bool unassignSelected;

        /* Travel and win-chance are unused for assignment — hide them. Pow/Eff stay visible so
         * the player can compare squad strength while picking. */
        protected override bool ShowTravel => false;
        protected override bool ShowWinChance => false;

        /* Flag the slot's existing occupant so the player can see what they'd be displacing
         * before committing. */
        protected override MercenarySquadFC CurrentSquadIndicator => currentSlotSquad;

        public Dialog_AssignSquadToSettlement(WorldSettlementFC target, MercenarySquadFC currentSlotSquad)
        {
            this.target = target;
            this.currentSlotSquad = currentSlotSquad;
            // Cache the destination's max deploy cost so the header and the over-budget tint
            // share one value. Settlement military level doesn't change while the dialog is open.
            double budget = MilitaryFC.CalculateSquadBudget(target?.settlementMilitaryLevel ?? 0);
            maxDeployCost = MilitaryDeploymentUtil.CalculateDeploymentCost(budget);
            // WinChance is the base default but it's pruned from the toolbar here, so seed sort
            // with a mode that's still visible. Power matches what most players will care about
            // when picking a squad to billet.
            this.sort = SortMode.Power;

            doCloseX = true;
            forcePause = false;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            draggable = true;
        }

        protected override float DrawHeader(Rect inRect)
        {
            Rect titleRect = new Rect(0, 0, inRect.width, TitleH);
            Widgets.DrawHighlight(titleRect);

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(8f, 0, inRect.width - 16f, TitleH),
                "FCAssignSquadPickerTitle".Translate(target?.Name ?? "?"));

            UIUtil.DrawColoredHorizontalLine(0, TitleH, inRect.width, Color.gray);

            float y = TitleH + 6f;
            float innerX = 8f;
            float innerW = inRect.width - 16f;
            float lineH = 22f;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;

            Widgets.Label(new Rect(innerX, y, innerW, lineH),
                "FCAssignSquadPickerMaxDeploy".Translate(maxDeployCost));
            y += lineH;

            string slotLine = currentSlotSquad is object
                ? (string)"FCAssignSquadPickerCurrentSlot".Translate(currentSlotSquad.DisplayName)
                : (string)"FCAssignSquadPickerSlotEmpty".Translate();
            Widgets.Label(new Rect(innerX, y, innerW, lineH), slotLine);
            y += lineH;

            int stationed = target?.StationedSquads?.Count ?? 0;
            int cap = target?.SquadCap ?? 0;
            Widgets.Label(new Rect(innerX, y, innerW, lineH),
                "FCAssignSquadPickerStationed".Translate(stationed, cap));
            y += lineH;

            return y;
        }

        /* Flag squads whose deploy cost exceeds the destination settlement's max deploy cost
         * so the base renderer paints their labels amber. Matches MainTabWindow_Colony's
         * underfunded-slot treatment. */
        protected override bool RowOverBudget(RowData row) => row.deploymentCost > maxDeployCost;

        /* Win chance is hidden in this picker, so the accent strip would otherwise be a flat
         * gray. Encode billet-readiness instead: amber for over-budget (can't be deployed from
         * this slot), green for ready/available, gray for busy/unavailable. */
        protected override Color AccentColor(RowData row)
        {
            if (RowOverBudget(row)) return AccentUtil.MilUnderfunded;
            return row.available ? AccentUtil.MilReady : AccentUtil.MilInactive;
        }

        protected override bool CanConfirm()
        {
            if (unassignSelected) return currentSlotSquad is object && !currentSlotSquad.IsBusy;
            if (selected is null) return false;
            if (selected == currentSlotSquad) return true; // explicit no-op confirm
            // Can't billet an over-budget squad here (matches the unavailable treatment in RebuildRows).
            if (selected.DeploymentCost() > maxDeployCost) return false;
            return !selected.IsBusy;
        }

        protected override void OnRowSelected(MercenarySquadFC squad)
        {
            base.OnRowSelected(squad);
            unassignSelected = false;
        }

        protected override void Confirm()
        {
            MilitaryFC mfc = FindFC.Military;
            if (mfc is null || target is null) { Close(); return; }

            if (unassignSelected)
            {
                if (currentSlotSquad is object) mfc.Unassign(currentSlotSquad);
                Close();
                return;
            }

            if (selected is null || selected == currentSlotSquad)
            {
                Close();
                return;
            }

            int stationed = target.StationedSquads?.Count ?? 0;
            int cap = target.SquadCap;
            bool atCap = stationed >= cap && selected.settlement != target;

            bool ok;
            if (atCap && currentSlotSquad is object && currentSlotSquad.settlement == target)
            {
                ok = mfc.AttemptToSwap(selected, target, currentSlotSquad);
            }
            else
            {
                ok = mfc.AttemptToAssign(selected, target);
            }
            if (ok) Close();
        }

        protected override void RebuildRows()
        {
            rows.Clear();

            FactionFC fc = FindFC.FactionComp;
            List<MercenarySquadFC> pool = fc?.military?.mercenarySquads;
            if (pool is null)
            {
                rowsDirty = false;
                return;
            }

            int now = Find.TickManager.TicksGame;

            foreach (MercenarySquadFC squad in pool)
            {
                if (squad is null) continue;
                // Over-budget squads (deploy cost > the destination's max deploy budget) are
                // treated as unavailable — they can't be billeted here. The current occupant
                // stays available so it can be kept (no-op confirm) even if it's over budget.
                bool overBudget = squad.DeploymentCost() > maxDeployCost;
                bool available = !squad.IsBusy && (!overBudget || squad == currentSlotSquad);
                if (availableOnly && !available && squad != currentSlotSquad) continue;

                SquadPower sp = SquadPowerRegistry.Resolve(squad);

                string status;
                Color statusColor;
                bool isReady;
                ComputeStatus(squad, now, out status, out statusColor, out isReady);

                int injuredCount = SquadHealthUtil.CountInjuredMercs(squad);
                if (isReady && injuredCount > 0)
                {
                    status = "FCSquadStatusInjured".Translate(injuredCount);
                    statusColor = AccentUtil.MilActiveMission;
                }

                // Assign gates differ from IsAvailable: only busy and destination-budget block, and
                // the budget is measured against the destination (maxDeployCost), not the squad's own
                // settlement. Build the reason from those two conditions directly.
                string unavailableReason = null;
                if (!available)
                {
                    List<string> reasons = new List<string>();
                    if (squad.IsBusy) reasons.Add(SquadStatusUtil.BusyReason(squad));
                    if (overBudget) reasons.Add(SquadStatusUtil.OverBudgetReason(squad.DeploymentCost(), maxDeployCost, target?.Name));
                    unavailableReason = SquadStatusUtil.FormatUnavailTooltip(reasons);
                }

                rows.Add(new RowData
                {
                    squad = squad,
                    travelTicks = 0,
                    winChanceMin = 0,
                    winChanceMax = 0,
                    ourPower = sp.militaryLevel,
                    ourEfficiency = sp.militaryEfficiency,
                    hasOurForce = true,
                    status = status,
                    statusColor = statusColor,
                    available = available,
                    deploymentCost = squad.DeploymentCost(),
                    injuredCount = injuredCount,
                    unavailableReason = unavailableReason
                });
            }

            ApplySort();
            rowsDirty = false;
        }

        /* Extra "Unassign — empty this slot" card at the bottom of the squad list. Renders only
         * when the slot has a current occupant; click selects it (clears any squad selection),
         * confirm calls Unassign on the occupant. */
        protected override float ExtraRowsHeight =>
            currentSlotSquad is object ? (CardH + RowGap) : 0f;

        protected override void DrawExtraRows(Rect viewRect, ref float runningY)
        {
            if (currentSlotSquad is null) return;

            Rect cardRect = new Rect(0f, runningY, viewRect.width, CardH);
            DrawUnassignCard(cardRect);
            runningY += CardH + RowGap;
        }

        private void DrawUnassignCard(Rect cardRect)
        {
            bool isSelected = unassignSelected;
            Color accent = AccentUtil.MilInactive;

            if (isSelected)
            {
                Widgets.DrawHighlightSelected(cardRect);
                Widgets.DrawBoxSolid(cardRect, new Color(accent.r, accent.g, accent.b, 0.10f));
            }
            else if (Mouse.IsOver(cardRect))
            {
                Widgets.DrawHighlight(cardRect);
            }

            Widgets.DrawBoxSolid(new Rect(cardRect.x, cardRect.y, AccentW, cardRect.height), accent);

            float contentX = cardRect.x + AccentW + 6f;

            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.DrawColoredLabel(
                new Rect(contentX, cardRect.y, cardRect.width - contentX - 8f, CardHeaderH),
                "FCSquadPickerUnassignSlot".Translate(),
                accent);

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.DrawColoredLabel(
                new Rect(contentX, cardRect.y + CardHeaderH, cardRect.width - contentX - 8f, CardDetailH),
                "FCAssignSquadPickerCurrentSlot".Translate(currentSlotSquad.DisplayName),
                ColorUtil.Gray7);

            if (Widgets.ButtonInvisible(cardRect))
            {
                selected = null;
                unassignSelected = true;
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }
    }
}
