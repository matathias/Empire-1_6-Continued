using FactionColonies.util;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Battle archive overview. Lists every entry in <see cref="WorldComponent_Archive"/>'s
    /// battle archive, newest-first, as a row table (kind / target / date / outcome).
    /// Clicking a row opens the same <see cref="BattleProgressWindow"/> that the live
    /// auto-resolve uses, so the user can review per-round detail (or, for manual battles,
    /// the side panels with a "no per-round detail" placeholder).
    /// <para>Used as the "Battle Reports" subtab body of the main military tab. Also
    /// usable as a standalone <see cref="Window"/> via <see cref="DoWindowContents"/>.</para>
    /// </summary>
    public class MainTabWindow_BattleReports : Window
    {
        public override Vector2 InitialSize => new Vector2(900f, 640f);

        private Vector2 listScrollPos;

        private const float SummaryH = 24f;
        private const float Pad = 4f;
        private const float HeaderH = 24f;
        private const float RowH = 30f;

        public MainTabWindow_BattleReports()
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

        /// <summary>Draws the archive list directly into <paramref name="rect"/>. Used both
        /// standalone and embedded inside the main military tab's "Battle Reports" subtab.</summary>
        public void Draw(Rect rect)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            WorldComponent_Archive archive = WorldComponent_Archive.Get();
            int count = archive?.BattleReportCount ?? 0;

            float innerX = rect.x + Pad;
            float innerW = rect.width - Pad * 2f;

            // Count readout — small/grey, matches the rhythm of MainTabWindow_Squads /
            // DrawMilitarySettlementCards.
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.DrawColoredLabel(
                new Rect(innerX, rect.y + Pad, innerW * 0.5f, SummaryH),
                "FCBattleArchiveCount".Translate(count),
                Color.gray);

            Rect tableRect = new Rect(rect.x, rect.y + SummaryH + 4f,
                rect.width, rect.height - SummaryH - 4f);

            if (archive is null || count == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                UIUtil.DrawColoredLabel(
                    new Rect(tableRect.x, tableRect.y + tableRect.height * 0.35f, tableRect.width, 40f),
                    "FCBattleArchiveEmpty".Translate(),
                    Color.gray);
                Text.Anchor = anchorBefore;
                Text.Font = fontBefore;
                return;
            }

            // Header row above the scroll viewport.
            Rect headerRect = new Rect(tableRect.x + Pad, tableRect.y, tableRect.width - Pad * 2f, HeaderH);
            DrawHeader(headerRect);

            Rect listOuter = new Rect(tableRect.x, headerRect.yMax + 2f,
                tableRect.width, tableRect.yMax - headerRect.yMax - 2f);

            // Snapshot to a list so the user can open a row mid-frame without a mutated
            // archive interfering.
            List<BattleResult> reports = new List<BattleResult>();
            foreach (BattleResult r in archive.RecentBattleReports) reports.Add(r);

            float viewH = reports.Count * RowH;
            Rect scrollView = ScrollUtil.BeginScrollView(listOuter, ref listScrollPos, viewH);

            for (int i = 0; i < reports.Count; i++)
            {
                Rect row = new Rect(scrollView.x, scrollView.y + i * RowH,
                    scrollView.width, RowH);
                DrawRow(row, reports[i], i);
            }

            ScrollUtil.EndScrollView();

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        private void DrawHeader(Rect rect)
        {
            float[] colW = ComputeColumnWidths(rect.width);
            float x = rect.x + 4f;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Small;

            string[] headers = new[]
            {
                "FCBattleArchiveColKind".Translate().ToString(),
                "FCBattleArchiveColTarget".Translate().ToString(),
                "FCBattleArchiveColOurForce".Translate().ToString(),
                "FCBattleArchiveColDate".Translate().ToString(),
                "FCBattleArchiveColOutcome".Translate().ToString()
            };
            for (int i = 0; i < headers.Length; i++)
            {
                UIUtil.ClampedLabel(new Rect(x, rect.y, colW[i], rect.height), headers[i]);
                x += colW[i];
            }
            GUI.color = Color.white;
            Widgets.DrawLineHorizontal(rect.x, rect.yMax, rect.width);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawRow(Rect rect, BattleResult report, int rowIndex)
        {
            if (rowIndex % 2 == 1)
                Widgets.DrawHighlight(rect);
            if (Mouse.IsOver(rect))
                Widgets.DrawHighlight(rect);

            float[] colW = ComputeColumnWidths(rect.width);
            float x = rect.x + 4f;
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Small;

            BattleViewerSide playerSide = ResolveStoredPlayerSide(report);
            bool isDefense = report.kind == BattleOperationKind.Defense;

            // Kind
            UIUtil.ClampedLabel(new Rect(x, rect.y, colW[0], rect.height), KindLabel(report.kind));
            x += colW[0];

            // Opposing Faction — for offensive ops, the defender is the opponent; for defense,
            // the attacker is. Appended with the opponent's initial force figure.
            string oppName = isDefense
                ? (report.attackerLabel ?? report.attackerFactionName ?? "?")
                : (report.defenderLabel ?? report.defenderFactionName ?? "?");
            double oppForce = isDefense ? report.attackerInitialForce : report.defenderInitialForce;
            string oppText = oppName + (string)"FCBattleArchiveForceSuffix".Translate(oppForce.ToString("F0"));
            Rect oppRect = new Rect(x, rect.y, colW[1], rect.height);
            UIUtil.ClampedLabel(oppRect, oppText);
            x += colW[1];

            // Our Force — squad name for offensive ops (attackerLabel is squad-first); the
            // defending settlement for Defense (defenderLabel is settlement-first, which is
            // what's actually at stake). NPC-vs-NPC archive rows get a dash.
            string ourCell;
            if (playerSide == BattleViewerSide.Neither)
            {
                ourCell = "—";
            }
            else
            {
                string ourName = isDefense
                    ? (report.defenderLabel ?? report.defenderFactionName ?? "?")
                    : (report.attackerLabel ?? report.attackerFactionName ?? "?");
                double ourForce = isDefense ? report.defenderInitialForce : report.attackerInitialForce;
                ourCell = ourName + (string)"FCBattleArchiveForceSuffix".Translate(ourForce.ToString("F0"));
            }
            Rect ourRect = new Rect(x, rect.y, colW[2], rect.height);
            UIUtil.ClampedLabel(ourRect, ourCell);
            x += colW[2];

            // Date — format absolute tick into "Day N, Year Y" using GenDate at world-zero
            // longitude (no per-tile locale here; world-zero is fine for an archive list).
            string dateStr = GenDate.DateFullStringAt(
                GenDate.TickGameToAbs(report.recordedTick), Vector2.zero);
            UIUtil.ClampedLabel(new Rect(x, rect.y, colW[3], rect.height), dateStr);
            x += colW[3];

            // Outcome — Victory if the player won, Defeat if they lost, dash if pure NPC vs NPC.
            UIUtil.ClampedLabel(new Rect(x, rect.y, colW[4], rect.height), OutcomeLabel(report, playerSide));

            if (Widgets.ButtonInvisible(rect))
            {
                Find.WindowStack.Add(new BattleProgressWindow(report, playerSide));
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        /// <summary>
        /// Map archive entry kind → which side the player was on. Defense ops put the
        /// player on the defender side; the offensive kinds put the player on the
        /// attacker side. <c>Other</c> is treated as pure NPC vs NPC.
        /// </summary>
        private static BattleViewerSide ResolveStoredPlayerSide(BattleResult report)
        {
            switch (report.kind)
            {
                case BattleOperationKind.Defense: return BattleViewerSide.Defender;
                case BattleOperationKind.Raid:
                case BattleOperationKind.Capture:
                case BattleOperationKind.Enslave:
                case BattleOperationKind.Raze:
                    return BattleViewerSide.Attacker;
                default: return BattleViewerSide.Neither;
            }
        }

        private static string KindLabel(BattleOperationKind kind)
        {
            switch (kind)
            {
                case BattleOperationKind.Raid: return "FCRaidSettlement".Translate();
                case BattleOperationKind.Capture: return "FCCaptureSettlement".Translate();
                case BattleOperationKind.Raze: return "FCRazeSettlement".Translate();
                case BattleOperationKind.Enslave: return "FCEnslavePopulation".Translate();
                case BattleOperationKind.Defense: return "FCDefendColony".Translate();
                default: return "?";
            }
        }

        private static string OutcomeLabel(BattleResult report, BattleViewerSide playerSide)
        {
            if (playerSide == BattleViewerSide.Neither) return "—";
            bool playerWon = (playerSide == BattleViewerSide.Attacker)
                ? report.AttackerVictory
                : report.DefenderVictory;
            if (!playerWon)
            {
                // Crushing Defeat is the loser-side mirror of Overwhelming Victory — the player
                // failed to inflict a single casualty. Distinct label so the report tab surfaces
                // the state at a glance.
                return report.IsCrushingDefeat
                    ? (string)"FCBattleArchiveOutcomeCrushingDefeat".Translate()
                    : (string)"FCBattleArchiveOutcomeDefeat".Translate();
            }
            return report.IsOverwhelmingVictory
                ? (string)"FCBattleArchiveOutcomeOverwhelmingVictory".Translate()
                : (string)"FCBattleArchiveOutcomeVictory".Translate();
        }

        /// <summary>Column widths: Kind 16%, Opposing Faction 26%, Our Force 24%, Date 18%, Outcome 16%.</summary>
        private static float[] ComputeColumnWidths(float totalWidth)
        {
            float w = totalWidth - 8f;
            return new[]
            {
                w * 0.16f,
                w * 0.26f,
                w * 0.24f,
                w * 0.18f,
                w * 0.16f
            };
        }
    }
}
