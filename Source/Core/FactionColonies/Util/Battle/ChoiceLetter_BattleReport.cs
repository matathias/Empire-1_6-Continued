using FactionColonies.util;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
    /* ChoiceLetter_BattleReport                                                   */
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/

    /// <summary>
    /// Letter type for post-battle outcome letters. Carries one or more <see cref="reportIds"/>
    /// pointing into <see cref="WorldComponent_Archive"/>; each gets a "View battle report"
    /// option that opens a <see cref="BattleProgressWindow"/> against the archived result.
    /// <para>A single-report letter dismisses itself when its report is viewed (legacy behavior).
    /// A multi-report letter — sent by the condensed defense letter when several concurrent
    /// attacks resolved together — keeps each attack's button available until the player
    /// closes it.</para>
    /// <para>If a report has been evicted by the archive cap by the time the user opens the
    /// letter, the option toasts a "report no longer available" message instead of crashing.</para>
    /// </summary>
    public class ChoiceLetter_BattleReport : ChoiceLetter
    {
        /// <summary>Canonical list of archived report ids — one per attack the letter covers.</summary>
        public List<int> reportIds = new List<int>();

        /// <summary>Legacy single-id field. Retained only so pre-multi-report saves migrate into
        /// <see cref="reportIds"/> on load; never written by new code.</summary>
        public int reportId;

        public BattleViewerSide playerSide = BattleViewerSide.Neither;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly)
                {
                    yield return Option_Close;
                    yield break;
                }
                if (reportIds.Count <= 1)
                {
                    yield return MakeViewReportOption(
                        reportIds.Count == 1 ? reportIds[0] : reportId, 0);
                }
                else
                {
                    for (int i = 0; i < reportIds.Count; i++)
                        yield return MakeViewReportOption(reportIds[i], i + 1);
                }
                if (lookTargets.IsValid())
                    yield return Option_JumpToLocation;
                yield return Option_Close;
            }
        }

        /// <param name="id">Archived report id to open.</param>
        /// <param name="attackNumber">1-based attack number for a multi-report letter, or 0 for a
        /// single-report letter (unnumbered label, and viewing dismisses the letter).</param>
        private DiaOption MakeViewReportOption(int id, int attackNumber)
        {
            string label = attackNumber > 0
                ? "FCBattleReportButtonNumbered".Translate(attackNumber)
                : "FCBattleReportButton".Translate();
            DiaOption opt = new DiaOption(label);
            opt.action = delegate
            {
                WorldComponent_Archive archive = WorldComponent_Archive.Get();
                if (archive is object && archive.TryGetBattleReport(id, out BattleResult result))
                {
                    Find.WindowStack.Add(new BattleProgressWindow(result, playerSide));
                }
                else
                {
                    Messages.Message("FCBattleReportEvicted".Translate(),
                        MessageTypeDefOf.RejectInput, historical: false);
                }
                // Single-report letters dismiss on view. Multi-report letters stay so the
                // player can open each attack's report; Option_Close dismisses them.
                if (attackNumber == 0)
                    Find.LetterStack.RemoveLetter(this);
            };
            opt.resolveTree = true;
            return opt;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref reportId, "reportId", 0);
            Scribe_Collections.Look(ref reportIds, "reportIds", LookMode.Value);
            Scribe_Values.Look(ref playerSide, "playerSide", BattleViewerSide.Neither);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (reportIds is null) reportIds = new List<int>();
                // Pre-multi-report save: the letter carried a single reportId. Migrate it.
                if (reportIds.Count == 0 && reportId > 0) reportIds.Add(reportId);
            }
        }
    }
}
