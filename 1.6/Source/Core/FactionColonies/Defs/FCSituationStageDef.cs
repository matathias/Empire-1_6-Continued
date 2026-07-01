using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// One ordered stage of a <see cref="FCSituationDef"/>'s progress bar. The current stage is the
    /// highest stage whose <see cref="threshold"/> is &lt;= the bar's progress. While the bar sits in
    /// this stage its <see cref="statModifiers"/> are applied (position-based: in-stage == applied),
    /// and crossing into/out of it fires the direction-appropriate event slot.
    ///
    /// Referenced by defName from <see cref="FCSituationDef.stages"/>.
    /// </summary>
    public class FCSituationStageDef : Def
    {
        /* The stage's entry point on the bar. Stages are ordered ascending by threshold. */
        public float threshold = 0f;

        /* Applied whenever the bar is in this stage, regardless of entry direction. */
        public List<FCStatModifier> statModifiers = new List<FCStatModifier>();

        /* Direction-aware event slots (all optional). A boundary crossing belongs to whichever stage
         * conceptually owns it; do not populate both sides of one crossing unless two events are
         * genuinely intended. */
        public FCEventDef onEnterFromBelow;   // bar rose into this stage across its lower threshold
        public FCEventDef onEnterFromAbove;   // bar fell into this stage from the stage above
        public FCEventDef onExitUpward;       // bar left this stage by rising past its upper threshold
        public FCEventDef onExitDownward;     // bar left this stage by falling below its threshold

        /* When true, each slot fires at most once for a given situation instance (latched by
         * defName + slot in FCSituation.firedStageLatches). */
        public bool fireEventsOnce = false;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string err in base.ConfigErrors())
                yield return err;

            if (threshold < 0f)
                yield return $"FCSituationStageDef {defName}: threshold {threshold} is negative.";
        }
    }
}
