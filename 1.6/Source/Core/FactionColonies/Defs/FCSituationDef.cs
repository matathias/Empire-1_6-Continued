using System.Collections.Generic;
using Verse;
using FactionColonies.util;

namespace FactionColonies
{
    /// <summary>
    /// The authored template for a Situation: a long-lived, bidirectional progress meter that advances
    /// or recedes over time, crossing ordered <see cref="FCSituationStageDef"/> thresholds (each of
    /// which applies stat-modifiers and fires direction-aware events) and resolving / holding / looping
    /// at its endpoints. The player steers the bar by picking one active <see cref="FCSituationApproachDef"/>.
    ///
    /// Every effect a situation produces is delegated to the existing event system (stage / endpoint
    /// slots fire <see cref="FCEventDef"/>s). A situation may carry a condition extension
    /// (<see cref="FCSituationFactionCondition"/> / <see cref="FCSituationSettlementCondition"/>) that
    /// drives spawning and advance, and/or a <see cref="FCSituationHandlerExtension"/> for code hooks.
    /// </summary>
    public class FCSituationDef : Def
    {
        /* Display — reuses FCEventCategoryDef for accent color + ordering. */
        public FCEventCategoryDef category;
        public string iconPath;

        public FCSituationScope scope = FCSituationScope.Faction;

        public float maxProgress = 100f;
        public float startProgress = 0f;

        /* Advance rate (applied while the condition's ShouldAdvance is true, or always if no condition). */
        public float baseRatePerDay = 0f;
        /* Recede rate (applied while ShouldAdvance is false; ignored for condition-less pure timers). */
        public float decayRatePerDay = 0f;

        /* Stages and approaches are referenced by defName in XML. */
        public List<FCSituationStageDef> stages = new List<FCSituationStageDef>();
        public List<FCSituationApproachDef> approaches = new List<FCSituationApproachDef>();
        public FCSituationApproachDef defaultApproach;

        public FCSituationEndpointBehavior topBehavior = FCSituationEndpointBehavior.Clamp;
        public FCSituationEndpointBehavior bottomBehavior = FCSituationEndpointBehavior.Clamp;
        public FCEventDef topResolutionEvent;
        public FCEventDef bottomResolutionEvent;
        public FCEventDef loopEvent;

        public int cooldownDays = 0;
        public int maxConcurrent = 1;

        /* While any active situation of a blocking def targets a settlement, the player cannot delete
         * that settlement. The handler extension may override this dynamically. */
        public bool blocksSettlementRemoval = false;
        public string removalBlockedReasonKey;

        /* -*-*-*-*  Cached extension accessors  *-*-*-*- */

        public FCSituationFactionCondition FactionCondition => GetModExtension<FCSituationFactionCondition>();
        public FCSituationSettlementCondition SettlementCondition => GetModExtension<FCSituationSettlementCondition>();
        public FCSituationHandlerExtension Handler => GetModExtension<FCSituationHandlerExtension>();

        /// <summary>True if this def has a condition extension appropriate to its scope.</summary>
        public bool HasCondition =>
            scope == FCSituationScope.Settlement ? SettlementCondition != null : FactionCondition != null;

        /// <summary>The stage the bar is in at the given progress: the highest stage whose threshold
        /// is &lt;= progress, or null when below the first stage. Assumes <see cref="stages"/> is
        /// ascending (enforced by ConfigErrors).</summary>
        public FCSituationStageDef StageAt(float progress)
        {
            FCSituationStageDef result = null;
            if (stages == null) return null;
            for (int i = 0; i < stages.Count; i++)
            {
                if (stages[i] == null) continue;
                if (stages[i].threshold <= progress) result = stages[i];
                else break;
            }
            return result;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string err in base.ConfigErrors())
                yield return err;

            if (maxProgress <= 0f)
                yield return $"FCSituationDef {defName}: maxProgress must be > 0 (is {maxProgress}).";
            if (startProgress < 0f || startProgress > maxProgress)
                yield return $"FCSituationDef {defName}: startProgress {startProgress} outside [0, {maxProgress}].";

            if (approaches == null || approaches.Count == 0)
                yield return $"FCSituationDef {defName}: must define at least one approach.";

            if (defaultApproach == null)
                yield return $"FCSituationDef {defName}: defaultApproach is not set.";
            else
            {
                if (approaches == null || !approaches.Contains(defaultApproach))
                    yield return $"FCSituationDef {defName}: defaultApproach '{defaultApproach.defName}' is not a member of approaches.";
                if (defaultApproach.upkeepSilver != 0)
                    yield return $"FCSituationDef {defName}: defaultApproach '{defaultApproach.defName}' should be free (upkeepSilver is {defaultApproach.upkeepSilver}).";
                if ((defaultApproach.requiredPolicies != null && defaultApproach.requiredPolicies.Count > 0)
                    || (defaultApproach.requiredResearch != null && defaultApproach.requiredResearch.Count > 0))
                    yield return $"FCSituationDef {defName}: defaultApproach '{defaultApproach.defName}' should be ungated.";
            }

            if (stages != null)
            {
                float prev = float.NegativeInfinity;
                foreach (FCSituationStageDef stage in stages)
                {
                    if (stage == null) continue;
                    if (stage.threshold < prev)
                        yield return $"FCSituationDef {defName}: stage thresholds must be ascending (stage '{stage.defName}' at {stage.threshold} follows {prev}).";
                    if (stage.threshold < 0f || stage.threshold > maxProgress)
                        yield return $"FCSituationDef {defName}: stage '{stage.defName}' threshold {stage.threshold} outside [0, {maxProgress}].";
                    prev = stage.threshold;
                }
            }

            if (topBehavior == FCSituationEndpointBehavior.Terminal && topResolutionEvent == null)
                yield return $"FCSituationDef {defName}: topBehavior is Terminal but topResolutionEvent is unset.";
            if (bottomBehavior == FCSituationEndpointBehavior.Terminal && bottomResolutionEvent == null)
                yield return $"FCSituationDef {defName}: bottomBehavior is Terminal but bottomResolutionEvent is unset.";
            if ((topBehavior == FCSituationEndpointBehavior.Loop || bottomBehavior == FCSituationEndpointBehavior.Loop) && loopEvent == null)
                yield return $"FCSituationDef {defName}: a Loop endpoint is set but loopEvent is unset.";

            if (blocksSettlementRemoval && scope != FCSituationScope.Settlement)
                yield return $"FCSituationDef {defName}: blocksSettlementRemoval is only meaningful on a Settlement-scoped def.";
        }
    }
}
