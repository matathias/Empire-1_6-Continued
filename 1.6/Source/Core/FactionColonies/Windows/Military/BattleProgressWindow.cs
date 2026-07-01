using FactionColonies.util;
using RimWorld;
using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
    /* BattleProgressWindow                                                        */
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/

    /// <summary>
    /// Shared window for "watch this battle", both during live auto-resolve and
    /// when reviewing an archived <see cref="BattleResult"/>. Two mirrored side panels
    /// (attacker left, defender right) with faction icon + name/label, efficiency, and
    /// a health bar with the force ratio overlaid. Live battles get an inline countdown
    /// progress bar showing time until the next round tick. Below: a scrolling, mirrored
    /// round-roll log — Round | Atk Force/Raw/Final | Def Final/Raw/Force — with the
    /// winning side's block tinted per round.
    /// <para>For manual battles the rounds list is empty; the round-list area shows a
    /// "no per-round detail" placeholder instead of an empty scroll viewport.</para>
    /// </summary>
    public class BattleProgressWindow : Window
    {
        // Live op reference is null when opened from an archived report; the countdown
        // bar is the only feature that requires it.
        private readonly MilitaryOperation op;
        private readonly BattleResult result;
        private readonly BattleViewerSide playerSide;
        // Icon resolution priority: live participant -> archived faction reference -> name string.
        private readonly MilitaryOperationParticipant aggressorParticipant;
        private readonly MilitaryOperationParticipant defenderParticipant;

        private Vector2 scrollPos;

        public BattleProgressWindow(MilitaryOperation op)
        {
            this.op = op;
            this.result = op?.battleResult;
            this.playerSide = MilitaryDeploymentUtil.ResolvePlayerSide(op);
            this.aggressorParticipant = op?.aggressor;
            this.defenderParticipant = op?.defender;
            InitWindowProps();
        }

        /// <summary>
        /// Constructor for archived-report viewing: the originating op is gone.
        /// Side panels still render the faction icon when the stored faction reference
        /// resolves (defeated factions still resolve); only when the faction has been
        /// removed from the world entirely do they fall back to the bare name string.
        /// </summary>
        public BattleProgressWindow(BattleResult result, BattleViewerSide playerSide)
        {
            this.result = result;
            this.playerSide = playerSide;
            InitWindowProps();
        }

        private void InitWindowProps()
        {
            doCloseButton = true;
            doCloseX = true;
            forcePause = false;
            draggable = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
        }

        public override Vector2 InitialSize => new Vector2(720f, 600f);

        private bool PlayerSideKnown => playerSide != BattleViewerSide.Neither;
        private bool PlayerIsAttacker => playerSide == BattleViewerSide.Attacker;

        public override void DoWindowContents(Rect inRect)
        {
            if (result is null)
            {
                UIUtil.ClampedLabel(inRect, "FCBattleProgressNoBattle".Translate());
                return;
            }

            BattleResult br = result;

            /* -*- Header (title + sub-phase + optional tick countdown) -*- */
            bool showTickBar = TryGetTickProgress(br, out float tickProgress, out int ticksRemaining);
            float headerH = showTickBar ? 80f : 56f;
            Rect headerRect = new Rect(inRect.x, inRect.y, inRect.width, headerH);

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            string targetName = !string.IsNullOrEmpty(br.defenderLabel) ? br.defenderLabel : "?";
            UIUtil.ClampedLabel(new Rect(headerRect.x, headerRect.y, headerRect.width, 28f),
                "FCBattleProgressWindowTitle".Translate(targetName));
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperCenter;
            UIUtil.ClampedLabel(new Rect(headerRect.x, headerRect.y + 30f, headerRect.width, 22f),
                SubPhaseLabel(br));
            Text.Anchor = TextAnchor.UpperLeft;

            if (showTickBar)
            {
                const float tickBarW = 240f;
                const float tickBarH = 12f;
                const float tickLabelH = 16f;
                Rect tickBarRect = new Rect(
                    headerRect.x + (headerRect.width - tickBarW) / 2f,
                    headerRect.y + 54f,
                    tickBarW, tickBarH);
                Rect tickLabelRect = new Rect(
                    headerRect.x,
                    tickBarRect.yMax + 2f,
                    headerRect.width,
                    tickLabelH);
                DrawTickProgressBar(tickBarRect, tickLabelRect, tickProgress, ticksRemaining);
            }

            /* -*- Two columns: attacker vs defender -*- */
            float columnsY = inRect.y + headerH + 8f;
            // Tight-fit: titleH(22) + gap(4) + blockH(44) + gap(4) + effH(20) + gap(4) + barH(18) + 2*innerPad(16) = 132
            float columnsH = 132f;
            float colGap = 12f;
            float colW = (inRect.width - colGap) * 0.5f;
            Rect attackerCol = new Rect(inRect.x, columnsY, colW, columnsH);
            Rect defenderCol = new Rect(inRect.x + colW + colGap, columnsY, colW, columnsH);

            DrawSideColumn(attackerCol, aggressorParticipant, br.attackerFaction,
                br.attackerLabel, br.attackerFactionName,
                br.attackerInitialForce, br.attackerForceRemaining, br.attackerEfficiency,
                isAttacker: true);
            DrawSideColumn(defenderCol, defenderParticipant, br.defenderFaction,
                br.defenderLabel, br.defenderFactionName,
                br.defenderInitialForce, br.defenderForceRemaining, br.defenderEfficiency,
                isAttacker: false);

            /* -*- Round list (scrollable, latest first) -*- */
            float listY = columnsY + columnsH + 8f;
            float closeBtnReserve = 40f;
            float listH = inRect.height - (listY - inRect.y) - closeBtnReserve;
            Rect listRect = new Rect(inRect.x, listY, inRect.width, listH);

            DrawRoundList(listRect, br);
        }

        private string SubPhaseLabel(BattleResult br)
        {
            switch (br.subPhase)
            {
                case BattleSubPhase.Preparing: return "FCBattlePhasePreparing".Translate();
                case BattleSubPhase.Engaged: return "FCBattlePhaseEngaged".Translate();
                case BattleSubPhase.RollsInProgress: return "FCBattlePhaseRolling".Translate(br.rounds.Count);
                case BattleSubPhase.Resolved: return "FCBattlePhaseResolved".Translate();
                default: return string.Empty;
            }
        }

        /* -*- Tick countdown -*- */

        private bool TryGetTickProgress(BattleResult br, out float progress, out int ticksRemaining)
        {
            progress = 0f;
            ticksRemaining = 0;
            if (op is null) return false;
            if (br.subPhase == BattleSubPhase.Resolved) return false;
            if (br.IsComplete) return false;

            FCEvent evt = op.sourceEvents?.FirstOrDefault(
                e => e is object && e.def == FCEventDefOf.autoResolveBattleRound);
            if (evt is null) return false;

            ticksRemaining = Mathf.Max(0, evt.timeTillTrigger - Find.TickManager.TicksGame);
            int interval = Mathf.Max(1, FCSettings.autoResolveTicksPerRound);
            if (DebugSettings.godMode) interval = 1;
            progress = 1f - Mathf.Clamp01((float)ticksRemaining / interval);
            return true;
        }

        private static void DrawTickProgressBar(Rect barRect, Rect labelRect, float progress, int ticksRemaining)
        {
            Color bg = new Color(0.15f, 0.15f, 0.15f);
            Color fill = new Color(0.35f, 0.65f, 0.75f);
            UIUtil.DrawProgressBarColors(barRect, progress, bg, fill);

            int seconds = Mathf.CeilToInt(ticksRemaining / 60f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            UIUtil.ClampedLabel(labelRect, "FCBattleNextRoundIn".Translate(seconds));
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        /* -*- Side panel (mirrored: attacker = left-anchored, defender = right-anchored) -*- */
        private void DrawSideColumn(Rect rect, MilitaryOperationParticipant participant,
            Faction storedFaction, string label, string fallbackFactionName,
            double initial, double remaining, double efficiency, bool isAttacker)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(8f);

            // Live participant takes priority (faction state is freshest there); the
            // archive-stored Faction reference is the second-tier source so reports
            // remain rendered with full faction info even after the op is gone.
            Faction faction = participant?.faction ?? storedFaction;
            bool isPlayerSide = isAttacker ? PlayerIsAttacker : (playerSide == BattleViewerSide.Defender);
            TextAnchor textAnchor = isAttacker ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;

            /* Title row with side-aware gradient. Full color on the outer edge, fading to
               transparent toward the panel centerline (between the two columns), so the
               two sides read as "facing inward". */
            float titleH = 22f;
            Rect titleRect = new Rect(inner.x, inner.y, inner.width, titleH);
            Color titleTint = ResolveTitleTint(PlayerSideKnown, isPlayerSide);
            TexLoad.DrawHorizontalGradient(titleRect, titleTint, reversed: !isAttacker);

            Text.Font = GameFont.Small;
            Text.Anchor = textAnchor;
            string title = isAttacker
                ? "FCBattleSideAttacker".Translate()
                : "FCBattleSideDefender".Translate();
            UIUtil.DrawColoredLabel(titleRect.ContractedBy(4f, 0f), title, new Color(0.95f, 0.95f, 0.95f));

            /* Icon block: 40x40 icon flush left/right, two text rows on the other side.
               When the faction can't be resolved at all (live participant gone AND the
               stored reference no longer resolves — i.e., faction removed from world),
               the icon is omitted and the text block expands to use the full inner width. */
            float blockY = inner.y + titleH + 4f;
            const float iconSize = 40f;
            const float blockH = 44f;
            bool drawIcon = faction is object;
            Rect textBlockRect;
            if (drawIcon)
            {
                Rect iconRect;
                if (isAttacker)
                {
                    iconRect = new Rect(inner.x, blockY + (blockH - iconSize) / 2f, iconSize, iconSize);
                    textBlockRect = new Rect(inner.x + iconSize + 8f, blockY,
                        inner.width - iconSize - 8f, blockH);
                }
                else
                {
                    iconRect = new Rect(inner.xMax - iconSize, blockY + (blockH - iconSize) / 2f,
                        iconSize, iconSize);
                    textBlockRect = new Rect(inner.x, blockY,
                        inner.width - iconSize - 8f, blockH);
                }

                Texture2D iconTex = faction.def?.FactionIcon ?? BaseContent.BadTex;
                GUI.color = faction.Color;
                GUI.DrawTexture(iconRect, iconTex);
                GUI.color = Color.white;
            }
            else
            {
                textBlockRect = new Rect(inner.x, blockY, inner.width, blockH);
            }

            // Top text row: faction name (white). Bottom: label (dim grey).
            float textRowH = blockH * 0.5f;
            Rect nameRect = new Rect(textBlockRect.x, textBlockRect.y,
                textBlockRect.width, textRowH);
            Rect labelRect = new Rect(textBlockRect.x, textBlockRect.y + textRowH,
                textBlockRect.width, textRowH);

            Text.Anchor = textAnchor;
            string factionName = faction?.Name ?? fallbackFactionName;
            if (!string.IsNullOrEmpty(factionName))
                UIUtil.ClampedLabel(nameRect, factionName);
            UIUtil.DrawColoredLabel(labelRect, label ?? "?", new Color(0.7f, 0.7f, 0.7f));

            /* Efficiency line */
            float effY = blockY + blockH + 4f;
            Rect effRect = new Rect(inner.x, effY, inner.width, 20f);
            Text.Anchor = textAnchor;
            UIUtil.ClampedLabel(effRect, "FCBattleEfficiencyLine".Translate(efficiency.ToString("0.00")));

            /* Force bar with overlay */
            float barY = effY + 24f;
            const float barH = 18f;
            Rect bar = new Rect(inner.x, barY, inner.width, barH);
            float fill = initial > 0 ? Mathf.Clamp01((float)(remaining / initial)) : 0f;
            Color barBg = new Color(0.15f, 0.15f, 0.15f);
            Color barFill = isAttacker
                ? new Color(0.75f, 0.35f, 0.30f)
                : new Color(0.30f, 0.55f, 0.75f);
            UIUtil.DrawProgressBarColors(bar, fill, barBg, barFill);

            string forces = "FCBattleForceLine".Translate(remaining.ToString("0.#"),
                initial.ToString("0.#"));
            Text.Anchor = TextAnchor.MiddleCenter;
            UIUtil.ClampedLabel(bar, forces);

            Text.Anchor = TextAnchor.UpperLeft;
        }

        /* Title row tint: muted player color for player side, soft red for enemy side,
           neutral white-alpha for pure NPC-vs-NPC ops where neither side is the player.
           Alpha is on the higher side here because the gradient fades it to zero by the
           inner edge, so the average density across the band is much lower than a solid fill. */
        private static Color ResolveTitleTint(bool playerSideKnown, bool isPlayerSide)
        {
            if (!playerSideKnown)
                return new Color(1f, 1f, 1f, 0.18f);
            if (isPlayerSide)
            {
                Faction player = FindFC.EmpireFaction;
                Color baseColor = player is object
                    ? player.Color
                    : new Color(0.30f, 0.55f, 0.75f);
                return ColorUtil.Transform(baseColor, 0.5f, 0.55f);
            }
            return new Color(0.75f, 0.30f, 0.25f, 0.55f);
        }

        /* Faction-color tint for the table's "Attacker" / "Defender" group header cells.
           Distinct from the side-panel title tint (which is player-perspective): here we want
           each group label to read as its own faction's color, so attacker reads enemy-red
           against the player's defender-blue (or vice versa) regardless of POV. */
        private static Color ResolveFactionHeaderTint(Faction faction, bool isAttacker)
        {
            if (faction is object)
            {
                return ColorUtil.TransformRGB(faction.Color, 0.5f);
            }
            return isAttacker
                ? new Color(0.75f, 0.30f, 0.25f, 0.35f)
                : new Color(0.30f, 0.55f, 0.75f, 0.35f);
        }

        /* -*- Round list -*- */

        /* Layout vocabulary: the round table borrows the production-section style — cells are
           individually highlighted with a small <see cref="CellMargin"/> gap between them. The
           gap is what visually separates columns; no vertical divider lines are needed. The
           Round (outermost) and Final (innermost) columns are emphasized by a persistent
           column-wide highlight that spans the entire scroll content, while Force and Raw cells
           sit flush against the dark backdrop. */
        private const float CellMargin = 5f;
        private const float RoundHeaderH = 44f; // two 22px tiers
        private const float RoundRowH = 22f;

        private void DrawRoundList(Rect rect, BattleResult br)
        {
            // Lighter framing than DrawMenuSection so the per-cell highlights inside
            // (especially the Final DrawMenuSection emphasis) read as the heavier layer.
            UIUtil.DrawColoredBox(rect, Color.gray);
            Rect inner = rect.ContractedBy(4f);

            // Manual battles have no per-round data — show a placeholder instead of an
            // empty header + empty scroll viewport.
            if (br.rounds is null || br.rounds.Count == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.Font = GameFont.Small;
                UIUtil.DrawColoredLabel(inner, "FCBattleReportNoRoundDetail".Translate(), ColorUtil.Gray7);
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            int count = br.rounds.Count;
            float contentH = Math.Max(RoundRowH, count * RoundRowH);
            float viewportH = inner.height - RoundHeaderH - 2f;
            bool needsScroll = contentH > viewportH;
            float scrollReserve = needsScroll ? ScrollUtil.ScrollbarWidth + 1f : 0f;

            // Faction sources for the group-header tints: prefer live faction state, fall back
            // to the archived reference. Either is fine null — ResolveFactionHeaderTint handles it.
            Faction atkFaction = aggressorParticipant?.faction ?? br.attackerFaction;
            Faction defFaction = defenderParticipant?.faction ?? br.defenderFaction;

            float tableW = inner.width - scrollReserve;
            Rect headerRect = new Rect(inner.x, inner.y, tableW, RoundHeaderH);
            DrawRoundListHeader(headerRect, atkFaction, defFaction);

            Rect viewportOuter = new Rect(inner.x, inner.y + RoundHeaderH + 5f, inner.width, viewportH);
            Rect viewRect = ScrollUtil.BeginScrollView(viewportOuter, ref scrollPos, contentH);

            float[] leafW = ComputeRoundLeafWidths(viewRect.width);
            float[] leafX = ComputeLeafXs(0f, leafW);

            /* Persistent column-wide emphasis. Round columns get a simple highlight; Final
               columns get DrawMenuSection so they read as a heavier framed block, marking the
               battle-result column as the visual anchor. Drawn ONCE behind all rows. */
            Widgets.DrawHighlight(new Rect(leafX[0], 0f, leafW[0], contentH));
            Widgets.DrawHighlight(new Rect(leafX[7], 0f, leafW[7], contentH));
            Widgets.DrawMenuSection(new Rect(leafX[3], 0f, leafW[3], contentH));
            Widgets.DrawMenuSection(new Rect(leafX[4], 0f, leafW[4], contentH));

            // Latest at top.
            for (int i = count - 1; i >= 0; i--)
            {
                int displayIndex = (count - 1) - i;
                Rect rowRect = new Rect(0f, displayIndex * RoundRowH, viewRect.width, RoundRowH);
                // Alternating row highlight (every other row) — same technique as the production
                // table for visual row tracking. Drawn before the row's own cells.
                if (displayIndex % 2 == 0)
                    Widgets.DrawHighlight(rowRect);
                DrawRoundRow(rowRect, br.rounds[i], leafW, leafX,
                    br.attackerInitialForce, br.defenderInitialForce);
            }

            ScrollUtil.EndScrollView();
        }

        /* Header tiers:
            Top:    | Round | Attacker (3 cols, one highlight)     | Defender (3 cols, one highlight)     | Round |
            Bottom:         | Force | Raw | Final (gap-separated)  | Final | Raw | Force (gap-separated)  |       */
        private void DrawRoundListHeader(Rect rect, Faction atkFaction, Faction defFaction)
        {
            Text.Font = GameFont.Small;
            float[] leafW = ComputeRoundLeafWidths(rect.width);
            float[] leafX = ComputeLeafXs(rect.x, leafW);
            float topRowH = RoundHeaderH * 0.5f;
            float midY = rect.y + topRowH;

            string roundLabel = "FCBattleColRound".Translate().ToString();
            string atkLabel = "FCBattleColAttacker".Translate().ToString();
            string defLabel = "FCBattleColDefender".Translate().ToString();
            string rawLabel = "FCBattleColRollRaw".Translate().ToString();
            string finalLabel = "FCBattleColRollFinal".Translate().ToString();
            string forceLabel = "FCBattleColForce".Translate().ToString();

            // Cell rects.
            Rect leftRoundRect = new Rect(leafX[0], rect.y, leafW[0], RoundHeaderH);
            Rect rightRoundRect = new Rect(leafX[7], rect.y, leafW[7], RoundHeaderH);

            // Group top-tier spans cells 1..3 (atk) and 4..6 (def) PLUS the two CellMargin gaps
            // between those leaves — so the group label reads as one continuous band with the
            // bottom-tier leaf cells gap-separated below it.
            Rect atkGroupTopRect = new Rect(leafX[1], rect.y,
                (leafX[3] + leafW[3]) - leafX[1], topRowH);
            Rect defGroupTopRect = new Rect(leafX[4], rect.y,
                (leafX[6] + leafW[6]) - leafX[4], topRowH);

            Rect atkForceRect = new Rect(leafX[1], midY, leafW[1], topRowH);
            Rect atkRawRect = new Rect(leafX[2], midY, leafW[2], topRowH);
            Rect atkFinalRect = new Rect(leafX[3], midY, leafW[3], topRowH);
            Rect defFinalRect = new Rect(leafX[4], midY, leafW[4], topRowH);
            Rect defRawRect = new Rect(leafX[5], midY, leafW[5], topRowH);
            Rect defForceRect = new Rect(leafX[6], midY, leafW[6], topRowH);

            // Per-cell backings. Most cells get a flat highlight; Final cells get DrawMenuSection
            // to match the persistent emphasis applied to the Final columns in the body.
            Widgets.DrawHighlight(leftRoundRect);
            Widgets.DrawHighlight(rightRoundRect);
            Widgets.DrawHighlight(atkForceRect);
            Widgets.DrawHighlight(atkRawRect);
            Widgets.DrawHighlight(atkFinalRect);
            Widgets.DrawHighlight(defFinalRect);
            Widgets.DrawHighlight(defRawRect);
            Widgets.DrawHighlight(defForceRect);

            // Faction-color tints under the "Attacker" / "Defender" top-tier group labels.
            Widgets.DrawBoxSolid(atkGroupTopRect, ResolveFactionHeaderTint(atkFaction, isAttacker: true));
            Widgets.DrawBoxSolid(defGroupTopRect, ResolveFactionHeaderTint(defFaction, isAttacker: false));

            // Labels.
            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            Text.Anchor = TextAnchor.MiddleCenter;

            UIUtil.ClampedLabel(leftRoundRect, roundLabel);
            UIUtil.ClampedLabel(rightRoundRect, roundLabel);

            UIUtil.ClampedLabel(atkGroupTopRect, atkLabel);
            UIUtil.ClampedLabel(atkForceRect, forceLabel);
            UIUtil.ClampedLabel(atkRawRect, rawLabel);
            UIUtil.ClampedLabel(atkFinalRect, finalLabel);

            UIUtil.ClampedLabel(defGroupTopRect, defLabel);
            UIUtil.ClampedLabel(defFinalRect, finalLabel);
            UIUtil.ClampedLabel(defRawRect, rawLabel);
            UIUtil.ClampedLabel(defForceRect, forceLabel);

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            // Decorative peak-gradient lines under each group label (separating the group from
            // its leaves) and across the full header (separating the header from the rows).
            TexLoad.DrawHorizontalPeakGradientLine(atkGroupTopRect.x, atkGroupTopRect.yMax,
                atkGroupTopRect.width, Color.white);
            TexLoad.DrawHorizontalPeakGradientLine(defGroupTopRect.x, defGroupTopRect.yMax,
                defGroupTopRect.width, Color.white);
            TexLoad.DrawHorizontalPeakGradientLine(atkGroupTopRect.x, rect.yMax, defGroupTopRect.xMax - atkGroupTopRect.x, Color.white);
        }

        private void DrawRoundRow(Rect rect, RoundEntry r, float[] leafW, float[] leafX,
            double attackerInitial, double defenderInitial)
        {
            // Font/anchor set once up front so the Force-cell label (drawn before the
            // step-4 text block) doesn't depend on lingering state from prior frames.
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;

            Rect leftRoundCell = new Rect(leafX[0], rect.y, leafW[0], rect.height);
            Rect atkForceCell = new Rect(leafX[1], rect.y, leafW[1], rect.height);
            Rect atkRawCell = new Rect(leafX[2], rect.y, leafW[2], rect.height);
            Rect atkFinalCell = new Rect(leafX[3], rect.y, leafW[3], rect.height);
            Rect defFinalCell = new Rect(leafX[4], rect.y, leafW[4], rect.height);
            Rect defRawCell = new Rect(leafX[5], rect.y, leafW[5], rect.height);
            Rect defForceCell = new Rect(leafX[6], rect.y, leafW[6], rect.height);
            Rect rightRoundCell = new Rect(leafX[7], rect.y, leafW[7], rect.height);

            // (Persistent column highlights for Round / Final cells are drawn once in
            // DrawRoundList behind the entire scroll content, so they're not redrawn here.)

            /* Per-side Force progress bar. Drawn before the winner tint so the tint can layer
               over the losing-side bar; the tint itself is restricted to Raw + Final cells, so
               the winning-side bar still reads cleanly. */
            DrawForceCell(atkForceCell, r.attackerForceAfter, attackerInitial, isAttacker: true);
            DrawForceCell(defForceCell, r.defenderForceAfter, defenderInitial, isAttacker: false);

            /* Winner-side tint: Raw + Final cells (with the inter-cell margin between them so
               the gap stays visible). */
            Color winnerTint = ResolveWinnerBlockTint(r.attackerWonRound);
            if (winnerTint.a > 0f)
            {
                Rect rawCell = r.attackerWonRound ? atkRawCell : defRawCell;
                Rect finalCell = r.attackerWonRound ? atkFinalCell : defFinalCell;
                Widgets.DrawBoxSolid(rawCell, winnerTint);
                Widgets.DrawBoxSolid(finalCell, winnerTint);
            }

            /* Cell text. */
            Text.Anchor = TextAnchor.MiddleCenter;

            // Round (both sides — same number, mirrored for visual symmetry).
            UIUtil.ClampedLabel(leftRoundCell, r.roundNumber.ToString());
            UIUtil.ClampedLabel(rightRoundCell, r.roundNumber.ToString());

            // Raw rolls (de-emphasized in dim grey).
            DrawRawRollCell(atkRawCell, r.attackerRawRoll);
            DrawRawRollCell(defRawCell, r.defenderRawRoll);

            // Final values + center-pointing chevron in the winning Final cell.
            UIUtil.ClampedLabel(atkFinalCell, r.attackerScore.ToString("0.00"));
            UIUtil.ClampedLabel(defFinalCell, r.defenderScore.ToString("0.00"));
            DrawWinnerChevron(atkFinalCell, defFinalCell, r.attackerWonRound,
                ResolveWinnerBlockColor(r.attackerWonRound));

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static void DrawForceCell(Rect rect, double remaining, double initial, bool isAttacker)
        {
            float fill = initial > 0 ? Mathf.Clamp01((float)(remaining / initial)) : 0f;
            Color bg = new Color(0.15f, 0.15f, 0.15f, 0.7f);
            Color barFill = isAttacker
                ? new Color(0.55f, 0.25f, 0.20f)   // muted attacker red-orange
                : new Color(0.22f, 0.40f, 0.55f);  // muted defender blue
            // Inset the bar slightly so it doesn't crowd the column dividers.
            Rect bar = rect.ContractedBy(2f);
            UIUtil.DrawProgressBarColors(bar, fill, bg, barFill);

            Text.Anchor = TextAnchor.MiddleCenter;
            UIUtil.ClampedLabel(rect, remaining.ToString("0.#"));
        }

        private static void DrawRawRollCell(Rect rect, int rawRoll)
        {
            UIUtil.DrawColoredLabel(rect, rawRoll.ToString(), new Color(0.6f, 0.6f, 0.6f));
        }

        /* Center-pointing chevron in the winner's Final cell: "<" on attacker side (points toward
           the attacker's value, which sits left of the mirror axis), ">" on defender side. The
           chevron is justified to the cell's center-facing edge so it appears immediately next
           to the vertical mirror axis. */
        private static void DrawWinnerChevron(Rect atkFinalCell, Rect defFinalCell, bool attackerWon, Color winnerColor)
        {
            if (attackerWon)
            {
                Text.Anchor = TextAnchor.MiddleRight;
                UIUtil.DrawColoredLabel(atkFinalCell.ContractedBy(4f, 0f), "<", winnerColor);
            }
            else
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                UIUtil.DrawColoredLabel(defFinalCell.ContractedBy(4f, 0f), ">", winnerColor);
            }
        }

        private Color ResolveWinnerBlockTint(bool attackerWon)
        {
            if (PlayerSideKnown)
            {
                bool playerWonThisRound = (PlayerIsAttacker == attackerWon);
                return playerWonThisRound
                    ? new Color(0.20f, 0.50f, 0.20f, 0.25f)
                    : new Color(0.50f, 0.20f, 0.20f, 0.25f);
            }
            return new Color(1f, 1f, 1f, 0.10f);
        }

        private Color ResolveWinnerBlockColor(bool attackerWon)
        {
            if (PlayerSideKnown)
            {
                bool playerWonThisRound = (PlayerIsAttacker == attackerWon);
                return playerWonThisRound
                    ? new Color(0.20f, 0.80f, 0.20f, 1f)
                    : new Color(0.80f, 0.20f, 0.20f, 1f);
            }
            return new Color(1f, 1f, 1f, 0.75f);
        }

        /* Leaf order (8 cols, fully mirrored across the centerline):
              0:LeftRound  1:AtkForce  2:AtkRaw  3:AtkFinal | 4:DefFinal  5:DefRaw  6:DefForce  7:RightRound
           Percentages (of the width remaining after the 7 inter-cell margins are subtracted):
              10 / 14 / 11 / 15 / 15 / 11 / 14 / 10 = 100. */
        private static float[] ComputeRoundLeafWidths(float totalWidth)
        {
            float w = totalWidth - 7f * CellMargin;
            return new[]
            {
                w * 0.10f,
                w * 0.14f,
                w * 0.11f,
                w * 0.15f,
                w * 0.15f,
                w * 0.11f,
                w * 0.14f,
                w * 0.10f
            };
        }

        /* X-positions of each leaf cell, accumulating widths and inter-cell margins. */
        private static float[] ComputeLeafXs(float startX, float[] leafW)
        {
            float[] xs = new float[leafW.Length];
            float x = startX;
            for (int i = 0; i < leafW.Length; i++)
            {
                xs[i] = x;
                x += leafW[i] + CellMargin;
            }
            return xs;
        }
    }
}
