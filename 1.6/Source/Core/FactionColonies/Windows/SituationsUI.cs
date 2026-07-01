using System.Collections.Generic;
using UnityEngine;
using Verse;
using FactionColonies.util;

namespace FactionColonies
{
    /// <summary>
    /// Shared row renderer for the two Situations surfaces (the main colony tab and the per-settlement
    /// panel). Draws the category accent, label/target, progress bar with stage tick-marks, the signed
    /// net per-day rate, an approach selector, and (on the settlement panel) handler custom actions.
    /// </summary>
    public static class SituationsUI
    {
        public const float BaseRowHeight = 62f;
        public const float ActionRowHeight = 26f;

        private const float AccentW = 4f;
        private const float Pad = 6f;

        /// <summary>Total height a row needs, including any handler custom actions (settlement panel only).</summary>
        public static float RowHeight(FCSituation sit, bool includeActions)
        {
            float h = BaseRowHeight;
            if (includeActions && sit.def.Handler != null)
            {
                foreach (SituationAction a in sit.def.Handler.GetCustomActions(sit))
                {
                    if (a != null) h += ActionRowHeight;
                }
            }
            return h;
        }

        public static void DrawRow(Rect rowRect, FCSituation sit, FactionFC faction, bool showTarget, bool includeActions)
        {
            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;

            Color accent = sit.def.category != null ? sit.def.category.color : new Color(0.65f, 0.65f, 0.65f);
            Widgets.DrawBoxSolid(new Rect(rowRect.x, rowRect.y, AccentW, rowRect.height), accent);

            float x = rowRect.x + AccentW + Pad;
            float w = rowRect.width - AccentW - Pad * 2f;
            float y = rowRect.y + 3f;

            // Line 1: label (left) + target (right)
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            UIUtil.DrawColoredLabel(new Rect(x, y, w * 0.6f, 20f), sit.def.LabelCap, accent);
            if (showTarget)
            {
                Text.Anchor = TextAnchor.MiddleRight;
                string target = sit.targetSettlement != null
                    ? sit.targetSettlement.Name
                    : (string)"FCSituationFactionWide".Translate();
                UIUtil.DrawColoredLabel(new Rect(x + w * 0.6f, y, w * 0.4f, 20f), target, Color.gray);
            }

            // Line 2: progress bar with stage ticks (left) + stage label & net rate (right)
            float barY = y + 22f;
            float barW = w * 0.55f;
            Rect barRect = new Rect(x, barY, barW, 16f);
            UIUtil.DrawProgressBarColors(barRect, sit.Progress01, new Color(0.16f, 0.16f, 0.16f), accent);
            DrawStageTicks(barRect, sit.def);
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            Widgets.Label(barRect, $"{Mathf.RoundToInt(sit.progress)} / {Mathf.RoundToInt(sit.def.maxProgress)}");

            bool advancing = FCSituationMaker.IsAdvancing(sit, faction);
            float rate = sit.NetDailyRateGiven(advancing);
            string stageLabel = sit.currentStage != null ? sit.currentStage.LabelCap.Resolve() : (string)"FCSituationNoStage".Translate();
            string rateText = (rate >= 0f ? "+" : "") + rate.ToString("0.##") + (string)"FCSituationPerDay".Translate();
            Color rateColor = rate >= 0f ? new Color(0.6f, 0.85f, 0.6f) : new Color(0.85f, 0.6f, 0.6f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect infoRect = new Rect(x + barW + Pad, barY, w - barW - Pad, 16f);
            UIUtil.DrawColoredLabel(infoRect, stageLabel + "   " + rateText, rateColor);

            // Line 3: approach selector
            float ctrlY = barY + 20f;
            Rect approachRect = new Rect(x, ctrlY, Mathf.Min(240f, w), 20f);
            DrawApproachSelector(approachRect, sit, faction);

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;

            // Optional custom-action rows (settlement panel)
            if (includeActions && sit.def.Handler != null)
            {
                float ay = rowRect.y + BaseRowHeight;
                foreach (SituationAction action in sit.def.Handler.GetCustomActions(sit))
                {
                    if (action == null) continue;
                    Rect aRect = new Rect(x, ay, Mathf.Min(280f, w), ActionRowHeight - 2f);
                    bool enabled = action.DisabledReason.NullOrEmpty();
                    if (Widgets.ButtonText(aRect, action.Label, active: enabled))
                    {
                        if (action.Destructive)
                            Find.WindowStack.Add(new Dialog_Confirm("FCSituationConfirmAction".Translate(action.Label), action.Action));
                        else
                            action.Action?.Invoke();
                    }
                    if (!enabled && Mouse.IsOver(aRect))
                        TooltipHandler.TipRegion(aRect, action.DisabledReason);
                    ay += ActionRowHeight;
                }
            }
        }

        private static void DrawStageTicks(Rect barRect, FCSituationDef def)
        {
            if (def.stages == null) return;
            foreach (FCSituationStageDef stage in def.stages)
            {
                if (stage == null || def.maxProgress <= 0f) continue;
                float frac = Mathf.Clamp01(stage.threshold / def.maxProgress);
                float tx = barRect.x + barRect.width * frac;
                Widgets.DrawBoxSolid(new Rect(tx, barRect.y, 1f, barRect.height), new Color(1f, 1f, 1f, 0.5f));
            }
        }

        private static void DrawApproachSelector(Rect rect, FCSituation sit, FactionFC faction)
        {
            FCSituationApproachDef active = sit.activeApproach;
            string label = active != null
                ? (string)"FCSituationApproachLabel".Translate(active.LabelCap)
                : (string)"FCSituationNoApproach".Translate();

            if (Widgets.ButtonText(rect, label))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                if (sit.def.approaches != null)
                {
                    foreach (FCSituationApproachDef approach in sit.def.approaches)
                    {
                        if (approach == null) continue;
                        options.Add(BuildApproachOption(sit, faction, approach));
                    }
                }
                if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private static FloatMenuOption BuildApproachOption(FCSituation sit, FactionFC faction, FCSituationApproachDef approach)
        {
            // Static gate (policies + research), then runtime handler override.
            bool available = approach.MeetsStaticRequirements(out string reason);
            if (available && sit.def.Handler != null)
                available = sit.def.Handler.IsApproachAvailable(approach, sit, out reason);

            string dynLabel = sit.def.Handler?.GetDynamicApproachLabel(approach, sit);
            string baseLabel = dynLabel ?? approach.LabelCap.Resolve();
            string detail = approach.upkeepSilver > 0
                ? "FCSituationApproachOption".Translate(baseLabel, approach.ratePerDay.ToString("0.##"), approach.upkeepSilver)
                : "FCSituationApproachOptionFree".Translate(baseLabel, approach.ratePerDay.ToString("0.##"));

            if (!available)
            {
                return new FloatMenuOption(detail + (reason.NullOrEmpty() ? "" : " (" + reason + ")"), null);
            }

            FCSituationApproachDef captured = approach;
            return new FloatMenuOption(detail, delegate { faction.situationManager.SwitchApproach(sit, captured); });
        }
    }
}
