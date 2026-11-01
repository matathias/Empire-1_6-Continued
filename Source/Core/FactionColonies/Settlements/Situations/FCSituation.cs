using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// A live instance of a <see cref="FCSituationDef"/>: a progress bar with an active approach and a
    /// current stage. Its stat-effects are applied to settlements through a per-instance source-ID
    /// namespace (<c>situation_&lt;loadID&gt;_stage</c> / <c>_approach</c>); its stage / endpoint effects
    /// fire <see cref="FCEventDef"/>s through the existing event pipeline.
    /// </summary>
    public class FCSituation : IExposable, ILoadReferenceable
    {
        public FCSituationDef def;
        public float progress;
        public WorldSettlementFC targetSettlement;     // null == faction-scoped
        public FCSituationApproachDef activeApproach;
        public FCSituationStageDef currentStage;        // null when below the first stage
        public HashSet<string> firedStageLatches = new HashSet<string>();
        public int loopCount;
        public int loadID = -1;
        public int tickStarted = -1;

        public FCSituation() { }

        /// <summary>Bar fill fraction in [0, 1], for the progress widget.</summary>
        public float Progress01 =>
            def != null && def.maxProgress > 0f ? Mathf.Clamp01(progress / def.maxProgress) : 0f;

        public string StageSourceId => "situation_" + loadID + "_stage";
        public string ApproachSourceId => "situation_" + loadID + "_approach";

        /// <summary>The net signed per-day delta given whether the bar is currently advancing — for UI
        /// display. Mirrors the advance-pass formula minus the handler hook.</summary>
        public float NetDailyRateGiven(bool advancing)
        {
            float baseDelta = advancing ? def.baseRatePerDay : def.decayRatePerDay;
            return baseDelta + (activeApproach != null ? activeApproach.ratePerDay : 0f);
        }

        public static string LatchKey(FCSituationStageDef stage, string slot) => stage.defName + "|" + slot;

        public string GetUniqueLoadID() => "FCSituation_" + loadID;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref progress, "progress");
            Scribe_References.Look(ref targetSettlement, "targetSettlement");
            Scribe_Defs.Look(ref activeApproach, "activeApproach");
            Scribe_Defs.Look(ref currentStage, "currentStage");
            Scribe_Collections.Look(ref firedStageLatches, "firedStageLatches", LookMode.Value);
            Scribe_Values.Look(ref loopCount, "loopCount", 0);
            Scribe_Values.Look(ref loadID, "loadID", -1);
            Scribe_Values.Look(ref tickStarted, "tickStarted", -1);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && firedStageLatches == null)
                firedStageLatches = new HashSet<string>();
        }
    }
}
