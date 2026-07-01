using FactionColonies.util;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Faction-wide squad pool overview. Lists every <see cref="MercenarySquadFC"/> as a
    /// header+detail card with accent strip, billet, status, hire/upgrade costs, and per-squad
    /// actions: Inspect, Reassign, Upgrade, Dismiss. Reused as the "By Squad" subtab body of
    /// the main military tab.
    /// </summary>
    public class MainTabWindow_Squads : Window
    {
        public override Vector2 InitialSize => new Vector2(900f, 640f);

        private Vector2 scroll;

        public MainTabWindow_Squads()
        {
            doCloseX = true;
            forcePause = false;
            absorbInputAroundWindow = false;
            draggable = true;
            preventCameraMotion = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Draw(inRect);
        }

        /* Draw layout constants. cardH = cardHeaderH + cardDetailH; cards stack with rowGap.
           SummaryH matches DrawMilitarySettlementCards' "# settlements" readout so the two
           subtabs share the same top-of-content rhythm. */
        private const float SummaryH = 24f;
        private const float Pad = 4f;
        private const float RowGap = 2f;
        private const float CardHeaderH = 24f;
        private const float CardDetailH = 22f;
        private const float CardH = CardHeaderH + CardDetailH;
        private const float AccentW = 4f;

        /// <summary>Draws the squad pool list directly into <paramref name="rect"/>. Used both
        /// standalone (this Window's DoWindowContents) and embedded inside the main military
        /// tab's "By Squad" subtab.</summary>
        public void Draw(Rect rect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            FactionFC fc = FindFC.FactionComp;
            MilitaryFC mfc = fc?.military;
            List<MercenarySquadFC> pool = mfc?.mercenarySquads ?? new List<MercenarySquadFC>();

            float innerX = rect.x + Pad;
            float innerW = rect.width - Pad * 2f;

            // Count readout — small/grey, matches DrawMilitarySettlementCards' "# settlements".
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.DrawColoredLabel(new Rect(innerX, rect.y + Pad, innerW * 0.5f, SummaryH),
                "FCHireSquadsCount".Translate(pool.Count), Color.gray);

            // "Hire Squads" button — right-aligned in the free right half of the header row.
            // Opens the template browse-and-hire menu, dropping hires into the unassigned pool.
            const float hireBtnW = 140f;
            const float hireBtnH = 22f;
            Rect hireBtnRect = new Rect(innerX + innerW - hireBtnW, rect.y + Pad, hireBtnW, hireBtnH);
            Text.Anchor = TextAnchor.UpperLeft;
            if (UIUtil.ButtonFlat(hireBtnRect, "FCHireSquadsPoolButton".Translate()))
            {
                Find.WindowStack.Add(new Dialog_HireSquadsPool());
            }

            // Auto-replace-fallen toggle — label + checkbox sitting left of the Hire Squads button.
            if (mfc is object)
            {
                const float toggleW = 150f;
                Rect toggleRect = new Rect(hireBtnRect.x - toggleW - 8f, rect.y + Pad, toggleW, hireBtnH);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(toggleRect.x, toggleRect.y, toggleW - 26f, toggleRect.height),
                    "FCSquadAutoReplace".Translate());
                Widgets.Checkbox(toggleRect.xMax - 22f, toggleRect.y, ref mfc.autoReplaceDeadPawns, 22f);
                TooltipHandler.TipRegion(toggleRect, "FCSquadAutoReplaceTip".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
            }

            Rect tableRect = new Rect(rect.x, rect.y + SummaryH + 4f,
                rect.width, rect.height - SummaryH - 4f);

            if (pool.Count == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.DrawColoredLabel(new Rect(tableRect.x, tableRect.y + tableRect.height * 0.35f,
                    tableRect.width, 40f), "FCHireSquadsEmpty".Translate(), Color.gray);
                Text.Font = fontBefore;
                Text.Anchor = anchorBefore;
                return;
            }

            float listY = tableRect.y + Pad;
            float viewH = tableRect.yMax - listY - Pad;
            float totalH = pool.Count * (CardH + RowGap);

            Rect viewRect = new Rect(innerX, listY, innerW, viewH);
            Rect scrollRect = ScrollUtil.BeginScrollView(viewRect, ref scroll, totalH);

            int now = Find.TickManager.TicksGame;
            float runningY = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                MercenarySquadFC squad = pool[i];
                if (squad is null) continue;
                Rect cardRect = new Rect(0f, runningY, scrollRect.width, CardH);
                DrawSquadCard(cardRect, squad, i, now, mfc);
                runningY += CardH + RowGap;
            }
            ScrollUtil.EndScrollView();

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        /* Per-squad card. Header row: accent strip, squad name (clickable), right-aligned
           status badge. Detail row: Template / Billet / Cost / Upgrade columns followed by
           four right-aligned action buttons (Inspect, Reassign, Upgrade, Dismiss). */
        private void DrawSquadCard(Rect cardRect, MercenarySquadFC squad, int index, int now, MilitaryFC mfc)
        {
            // Alternating row background to match settlement-card list style
            bool isHighlighted = false;
            if (index % 2 == 0)
            {
                isHighlighted = true;
                Widgets.DrawHighlight(cardRect);
            }

            // Accent strip — driven by the squad's own state, not the settlement's. A squad
            // billeted at a settlement under attack but NOT part of the active defending force
            // shouldn't share the under-attack red.
            Color accent = AccentUtil.GetSquadAccent(squad);
            Widgets.DrawBoxSolid(new Rect(cardRect.x, cardRect.y, AccentW, cardRect.height), accent);

            float contentX = cardRect.x + AccentW + 6f;
            float contentW = cardRect.xMax - contentX - 4f;

            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;
            Color colorBefore = GUI.color;

            /* === HEADER ROW === */
            float headerY = cardRect.y;
            string statusText = ComputeStatus(squad, now);
            Color statusColor = ColorForStatus(squad, now);

            // Status badge — right-aligned
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleRight;
            float statusW = 240f;
            UIUtil.DrawColoredLabel(new Rect(cardRect.xMax - statusW - 4f, headerY, statusW, CardHeaderH), statusText, statusColor);

            // Squad name (clickable to open inspection — kept as a fallback alongside the
            // explicit Inspect button below).
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            float nameW = contentW - statusW - 6f;
            Rect nameRect = new Rect(contentX, headerY, nameW, CardHeaderH);
            UIUtil.DrawColoredLabel(nameRect, squad.DisplayName, accent);
            if (Mouse.IsOver(nameRect)) Widgets.DrawHighlight(nameRect);
            if (Widgets.ButtonInvisible(nameRect))
                Find.WindowStack.Add(new Dialog_SquadInspection(squad));

            /* === DETAIL ROW === */
            float detailY = cardRect.y + CardHeaderH;
            const float btnW = 78f;
            const float btnGap = 2f;
            float btnH = CardDetailH - 2f;
            float btnY = detailY + 1f;
            const int btnCount = 4;
            float buttonAreaW = btnW * btnCount + btnGap * (btnCount - 1);

            // Detail labels — fixed-width columns left of the button block
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            float dx = contentX;
            float labelsW = contentW - buttonAreaW - 6f;
            if (labelsW < 0f) labelsW = 0f;

            float colTemplate = Math.Min(190f, labelsW * 0.28f);
            float colBillet = Math.Min(220f, labelsW * 0.30f);
            float colPower = Math.Min(80f, labelsW * 0.14f);
            float colCost = Math.Min(130f, labelsW * 0.16f);
            float colUpgrade = Math.Max(0f, labelsW - colTemplate - colBillet - colPower - colCost);

            double powerLevel = SquadPowerRegistry.Resolve(squad).militaryLevel;
            string templateLbl = (string)"FCSquadColTemplate".Translate() + ": " + (squad.outfit?.name ?? "-");
            string billetLbl = (string)"FCSquadColBillet".Translate() + ": " + (squad.settlement?.Name ?? (string)"FCMilitaryTableSlotEmpty".Translate());
            string powerLbl = (string)"FCSquadColPower".Translate() + ": " + powerLevel.ToString("0.0");
            string costLbl = "FCDeployCost".Translate(squad.DeploymentCost());
            int upgrade = SquadUpgradeUtil.UpgradeCost(squad);
            bool hasUpgradeWork = SquadUpgradeUtil.HasUpgradeWork(squad);
            /* Show "$0" for a zero-net but real upgrade (a reassignment / same-price re-equip);
               "-" only when there is genuinely nothing to do. */
            string upgradeLbl = (string)"FCSquadColUpgrade".Translate() + ": "
                + (upgrade > 0 ? "$" + upgrade : (hasUpgradeWork ? "$0" : "-"));

            UIUtil.ClampedLabel(new Rect(dx, detailY, colTemplate, CardDetailH), templateLbl); dx += colTemplate;
            UIUtil.ClampedLabel(new Rect(dx, detailY, colBillet, CardDetailH), billetLbl); dx += colBillet;
            UIUtil.ClampedLabel(new Rect(dx, detailY, colPower, CardDetailH), powerLbl); dx += colPower;
            UIUtil.ClampedLabel(new Rect(dx, detailY, colCost, CardDetailH), costLbl); dx += colCost;
            UIUtil.ClampedLabel(new Rect(dx, detailY, colUpgrade, CardDetailH), upgradeLbl);

            // Action buttons (right-aligned)
            MercenarySquadFC capturedSquad = squad;
            float bx = cardRect.xMax - buttonAreaW - 4f;

            // Inspect
            Rect inspectRect = new Rect(bx, btnY, btnW, btnH);
            if (UIUtil.ButtonFlat(inspectRect, "FCMilitaryTableInspect".Translate(), highlighted: isHighlighted))
            {
                Find.WindowStack.Add(new Dialog_SquadInspection(capturedSquad));
            }
            TooltipHandler.TipRegion(inspectRect, "FCMilBtnInspectTip".Translate());
            bx += btnW + btnGap;

            // Reassign
            Rect reassignRect = new Rect(bx, btnY, btnW, btnH);
            if (UIUtil.ButtonFlat(reassignRect, "FCSquadActReassign".Translate(), highlighted: isHighlighted, disabled: squad.IsBusy))
            {
                Find.WindowStack.Add(new Dialog_SquadAssignment(capturedSquad));
            }
            if (squad.IsBusy)
                TooltipHandler.TipRegion(reassignRect, "FCSquadCannotModifyBusyTip".Translate());
            bx += btnW + btnGap;

            // Upgrade
            bool canUpgrade = hasUpgradeWork && !squad.IsBusy;
            Rect upgradeRect = new Rect(bx, btnY, btnW, btnH);
            if (UIUtil.ButtonFlat(upgradeRect, "FCSquadActUpgrade".Translate(), highlighted: isHighlighted, disabled: !canUpgrade))
            {
                MilitaryDeploymentUtil.ConfirmAndUpgradeAll(capturedSquad);
            }
            if (squad.IsBusy)
                TooltipHandler.TipRegion(upgradeRect, "FCSquadCannotModifyBusyTip".Translate());
            bx += btnW + btnGap;

            // Dismiss
            bool canDismiss = !squad.IsBusy;
            Rect dismissRect = new Rect(bx, btnY, btnW, btnH);
            if (UIUtil.ButtonFlat(dismissRect, "FCSquadActDismiss".Translate(), highlighted: isHighlighted, disabled: !canDismiss))
            {
                MilitaryFC utilCaptured = mfc;
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "FCSquadActDismissConfirm".Translate(capturedSquad.DisplayName),
                    delegate { utilCaptured.DismissSquad(capturedSquad); }));
            }
            if (squad.IsBusy)
                TooltipHandler.TipRegion(dismissRect, "FCSquadCannotModifyBusyTip".Translate());

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
            GUI.color = colorBefore;
        }

        /* Status + color for one squad. Both delegate to SquadStatusUtil so this tab and
           the squad pickers stay in lockstep on label priority and time formatting. */
        private static string ComputeStatus(MercenarySquadFC squad, int now)
        {
            SquadStatusUtil.Resolve(squad, out string label, out _, out _);
            return label;
        }

        private static Color ColorForStatus(MercenarySquadFC squad, int now)
        {
            SquadStatusUtil.Resolve(squad, out _, out Color c, out _);
            return c;
        }
    }
}
