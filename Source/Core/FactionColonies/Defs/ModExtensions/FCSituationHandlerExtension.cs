using System;
using System.Collections.Generic;
using Verse;
using FactionColonies.util;

namespace FactionColonies
{
    /// <summary>
    /// A one-shot decision button surfaced on the settlement Situations panel alongside the approach
    /// selector, for decisions that don't fit the ongoing-stance approach model (e.g. a permanent
    /// "Reintegrate" / "Release" button). A null <see cref="DisabledReason"/> means enabled.
    /// </summary>
    public class SituationAction
    {
        public string Label;
        public Action Action;
        public string DisabledReason;
        public bool Destructive;

        public SituationAction(string label, Action action, string disabledReason = null, bool destructive = false)
        {
            Label = label;
            Action = action;
            DisabledReason = disabledReason;
            Destructive = destructive;
        }
    }

    /// <summary>
    /// Lifecycle / decision hooks for a <see cref="FCSituationDef"/>, separate from its condition
    /// extension (a def may carry both). Subclass and override only what you need, then attach via
    /// <c>&lt;modExtensions&gt;</c>.
    /// </summary>
    public class FCSituationHandlerExtension : DefModExtension
    {
        /// <summary>Called once after the situation is created and its initial stat-modifiers applied.</summary>
        public virtual void OnStarted(FCSituation sit) { }

        /// <summary>Called after the current stage changes (stat-modifiers already swapped, slot events
        /// already queued).</summary>
        public virtual void OnStageChanged(FCSituation sit, FCSituationStageDef oldStage,
            FCSituationStageDef newStage, FCSituationStageDirection dir) { }

        /// <summary>Adjust the bar's daily delta. Default: returns delta unchanged.</summary>
        public virtual float OnProgressTick(FCSituation sit, float delta) => delta;

        /// <summary>Called when the situation resolves at an endpoint, just before it is removed.</summary>
        public virtual void OnResolved(FCSituation sit, FCSituationEndpointBehavior endpoint) { }

        /// <summary>Called when a Loop endpoint wraps the bar.</summary>
        public virtual void OnLooped(FCSituation sit) { }

        /// <summary>Runtime override of approach availability, layered on top of the static
        /// policy/research gates. Default: available.</summary>
        public virtual bool IsApproachAvailable(FCSituationApproachDef approach, FCSituation sit, out string reason)
        {
            reason = null;
            return true;
        }

        /// <summary>Dynamic label for an approach in the selector. Default: null (use the def label).</summary>
        public virtual string GetDynamicApproachLabel(FCSituationApproachDef approach, FCSituation sit) => null;

        /// <summary>Dynamic override of <see cref="FCSituationDef.blocksSettlementRemoval"/>. Default:
        /// the def's static value (with its removalBlockedReasonKey).</summary>
        public virtual bool BlocksSettlementRemoval(FCSituation sit, out string reason)
        {
            if (sit.def.blocksSettlementRemoval)
            {
                reason = sit.def.removalBlockedReasonKey.NullOrEmpty()
                    ? (string)"FCSituationBlocksRemovalDefault".Translate(sit.def.LabelCap)
                    : (string)sit.def.removalBlockedReasonKey.Translate();
                return true;
            }
            reason = null;
            return false;
        }

        /// <summary>One-shot decision buttons the settlement panel renders. Default: none.</summary>
        public virtual IEnumerable<SituationAction> GetCustomActions(FCSituation sit)
        {
            yield break;
        }
    }
}
