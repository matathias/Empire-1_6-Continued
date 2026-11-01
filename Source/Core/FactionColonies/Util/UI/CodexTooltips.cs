using RimWorld;
using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies.util
{
    /// <summary>
    /// Builds rich tooltip strings for UI elements, showing formulas
    /// and current values to help players understand game mechanics.
    /// </summary>
    public static class CodexTooltips
    {
        /// <summary>
        /// Draws a Codex "i" button at the given rect. On click, opens the CodexWindow.
        /// Optionally pre-selects a specific <see cref="CodexEntryDef"/>.
        /// </summary>
        public static void DrawCodexButton(Rect rect, CodexEntryDef preselect = null)
        {
            if (Widgets.ButtonImage(rect, TexButton.Info))
            {
                Find.WindowStack.Add(preselect is object ? new CodexWindow(preselect) : new CodexWindow());
            }
            TooltipHandler.TipRegion(rect, "FCCodexTitle".Translate());
        }

        /// <summary>
        /// Enhanced tooltip for the faction-level prosperity stat in the overview panel.
        /// Explains what prosperity does and shows the average with per-settlement breakdown hint.
        /// </summary>
        public static string GetFactionProsperityTooltip(FactionFC faction)
        {
            string tip = "FCFactionProsperity".Translate() + "\n-----\n" + "FCFactionProsperityDesc".Translate();
            tip += "\n\n" + "FCCodexTipProsFormula".Translate();
            if (faction.settlements.Any())
            {
                double lowest = faction.settlements.Min(s => s.prosperity);
                double highest = faction.settlements.Max(s => s.prosperity);
                tip += "\n" + "FCCodexTipProsRange".Translate(
                    Math.Round(lowest, 0),
                    Math.Round(highest, 0));
            }
            return tip;
        }

        /// <summary>
        /// Enhanced tooltip for the faction-level happiness stat.
        /// </summary>
        public static string GetFactionHappinessTooltip(FactionFC faction)
        {
            string tip = "FCFactionHappiness".Translate() + "\n-----\n" + "FCFactionHappinessDesc".Translate();
            tip += "\n\n" + "FCCodexTipSocialDrift".Translate("FCHappiness".Translate().CapitalizeFirst());
            return tip;
        }

        /// <summary>
        /// Enhanced tooltip for the faction-level loyalty stat.
        /// </summary>
        public static string GetFactionLoyaltyTooltip(FactionFC faction)
        {
            string tip = "FCFactionLoyalty".Translate() + "\n-----\n" + "FCFactionLoyaltyDesc".Translate();
            tip += "\n\n" + "FCCodexTipSocialDrift".Translate("FCLoyality".Translate().CapitalizeFirst());
            return tip;
        }

        /// <summary>
        /// Enhanced tooltip for the faction-level unrest stat.
        /// </summary>
        public static string GetFactionUnrestTooltip(FactionFC faction)
        {
            string tip = "FCFactionUnrest".Translate() + "\n-----\n" + "FCFactionUnrestDesc".Translate();
            tip += "\n\n" + "FCCodexTipUnrestDrift".Translate();
            return tip;
        }

        /// <summary>
        /// Tooltip for the estimated profit display.
        /// Shows income vs upkeep breakdown.
        /// </summary>
        public static string GetProfitTooltip(FactionFC faction)
        {
            double income = Math.Round(faction.income, 0);
            double upkeep = Math.Round(faction.upkeep, 0);
            double profit = Math.Round(faction.profit, 0);

            string tip = "FCCodexTipProfitBreakdown".Translate(income, upkeep, profit);
            return tip;
        }

        /// <summary>
        /// Tooltip for the "Time till tax" display.
        /// Explains tax averaging and the current interval.
        /// </summary>
        public static string GetTaxTimerTooltip()
        {
            int days = FCSettings.timeBetweenTaxes / GenDate.TicksPerDay;
            string tip = "FCCodexTipTaxTimer".Translate(days);
            tip += "\n\n" + "FCCodexTipTaxAveraging".Translate();
            return tip;
        }
    }
}
