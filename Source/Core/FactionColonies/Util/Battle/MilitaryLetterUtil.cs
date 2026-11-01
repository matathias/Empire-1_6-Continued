using RimWorld;
using System.Collections.Generic;
using Verse;

namespace FactionColonies.util
{
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
    /* MilitaryLetterUtil                                                          */
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/

    /// <summary>
    /// Helpers shared across military handlers for delivering outcome letters, including the
    /// overwhelming-victory / crushing-defeat flavor that each handler folds into its single
    /// outcome letter (no longer a separate letter from <see cref="MilitaryOperation.CompleteBattle"/>).
    /// </summary>
    internal static class MilitaryLetterUtil
    {
        /// <summary>
        /// Send a post-battle outcome letter carrying a "View battle report" button that
        /// opens the archived <see cref="BattleResult"/> identified by <paramref name="reportId"/>.
        /// Mirrors <see cref="LetterStack.ReceiveLetter(TaggedString, TaggedString, LetterDef, LookTargets, Faction, Quest, System.Collections.Generic.List{ThingDef}, string, int, bool, bool)"/>
        /// but uses the custom <see cref="ChoiceLetter_BattleReport"/> letter type so the
        /// dialog shows the button.
        /// </summary>
        /// <param name="op">Originating op (used to derive which side the player is on for tinting).
        /// May be null when the letter is being sent post-completion with no live op reference.</param>
        public static void SendBattleReportLetter(string label, string text, LetterDef def,
            LookTargets lookTargets, int reportId, MilitaryOperation op = null)
        {
            SendBattleReportLetter(label, text, def, lookTargets,
                new List<int> { reportId }, op);
        }

        /// <summary>
        /// Multi-report variant: the letter exposes one "View battle report" button per id in
        /// <paramref name="reportIds"/>. Used by the condensed defense letter when several
        /// concurrent attacks resolved together on one map — each attack archives its own
        /// <see cref="BattleResult"/> and gets its own button.
        /// </summary>
        public static void SendBattleReportLetter(string label, string text, LetterDef def,
            LookTargets lookTargets, List<int> reportIds, MilitaryOperation op = null)
        {
            ChoiceLetter_BattleReport letter = (ChoiceLetter_BattleReport)
                LetterMaker.MakeLetter(label, text, def, lookTargets);
            letter.reportIds = reportIds ?? new List<int>();
            letter.playerSide = MilitaryDeploymentUtil.ResolvePlayerSide(op);
            Find.LetterStack.ReceiveLetter(letter);
        }

        /* -*-*-*-*- Overwhelming victory / crushing defeat -*-*-*-*- */

        /// <summary>True when the player won and the winning side took zero casualties.</summary>
        public static bool IsOverwhelmingWin(BattleResult result, bool playerWon)
            => playerWon && result is object && result.IsOverwhelmingVictory;

        /// <summary>True when the player lost and inflicted zero casualties on the winner.</summary>
        public static bool IsCrushingLoss(BattleResult result, bool playerWon)
            => !playerWon && result is object && result.IsOverwhelmingVictory;

        /// <summary>
        /// Applies the overwhelming-victory happiness/loyalty reward to <paramref name="home"/>
        /// (scaled by <see cref="FCSettings.overwhelmingVictoryRewardMultiplier"/>; a value of 0
        /// disables it). Returns the amounts actually gained — <c>(0, 0)</c> when the home
        /// settlement is null (e.g. external <see cref="IAutoDefender"/> wins) or the multiplier
        /// is non-positive. The caller is responsible for displaying the gain (see
        /// <see cref="FormatOverwhelmingVictoryRewardLine"/>); the condensed defense letter
        /// aggregates per-op gains into a single line.
        /// </summary>
        public static (double happiness, double loyalty) GainOverwhelmingVictoryReward(WorldSettlementFC home)
        {
            float ovMult = FCSettings.overwhelmingVictoryRewardMultiplier;
            if (home is null || ovMult <= 0f) return (0.0, 0.0);

            var (hap, loy) = SettlementFormulas.CalculateBattleVictoryRewards();
            hap += home.GetStatValue(FCStatDefOf.victoryHappinessBonus);
            loy += home.GetStatValue(FCStatDefOf.victoryLoyaltyBonus);
            double gainedHap = home.GainHappiness(hap * ovMult);
            double gainedLoy = home.GainLoyalty(loy * ovMult);
            return (gainedHap, gainedLoy);
        }

        /// <summary>Formats the overwhelming-victory reward line, or "" when nothing was gained.</summary>
        public static string FormatOverwhelmingVictoryRewardLine(WorldSettlementFC home, double happiness, double loyalty)
        {
            if (home is null) return "";
            int h = (int)System.Math.Round(happiness);
            int l = (int)System.Math.Round(loyalty);
            if (h <= 0 && l <= 0) return "";
            return "FCOverwhelmingVictoryRewardLine".Translate(home.Name, h.ToString(), l.ToString());
        }

        /// <summary>
        /// Gains the overwhelming-victory reward for <paramref name="home"/> and appends the
        /// reward line to <paramref name="body"/>. Convenience wrapper over
        /// <see cref="GainOverwhelmingVictoryReward"/> + <see cref="FormatOverwhelmingVictoryRewardLine"/>
        /// for single-letter callers (offensive handlers, single-op defenses).
        /// </summary>
        public static void ApplyOverwhelmingVictoryReward(WorldSettlementFC home, ref string body)
        {
            var (hap, loy) = GainOverwhelmingVictoryReward(home);
            string line = FormatOverwhelmingVictoryRewardLine(home, hap, loy);
            if (!string.IsNullOrEmpty(line)) body += "\n\n" + line;
        }
    }
}
