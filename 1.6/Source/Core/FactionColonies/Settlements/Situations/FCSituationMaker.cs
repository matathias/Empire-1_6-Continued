using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FactionColonies.util;

namespace FactionColonies
{
    /// <summary>
    /// The per-day situation engine: the spawn pass (condition-driven creation) and the advance pass
    /// (rate accrual + endpoint + stage-transition processing). Also hosts <see cref="ApplyDelta"/>,
    /// the shared mover used by both the advance pass and <see cref="FCSituationManager.AddProgress"/>.
    /// All effects are delegated to <see cref="FCEventDef"/>s queued through the existing event pipeline.
    /// </summary>
    public static class FCSituationMaker
    {
        /// <summary>Daily entry point, hooked into <see cref="FactionFC.StatTick"/> after settlement
        /// stats are refreshed so conditions read fresh values.</summary>
        public static void ProcessSituations(FactionFC faction)
        {
            FCSituationManager manager = faction?.situationManager;
            if (manager == null) return;

            SpawnPass(faction, manager);

            // Snapshot: a Terminal endpoint can remove a situation mid-pass.
            List<FCSituation> snapshot = new List<FCSituation>(manager.Situations);
            foreach (FCSituation sit in snapshot)
            {
                if (!manager.Situations.Contains(sit)) continue;
                AdvanceOne(sit, faction);
            }
        }

        private static void SpawnPass(FactionFC faction, FCSituationManager manager)
        {
            foreach (FCSituationDef def in FactionCache.FactionConditionedSituationDefs)
            {
                FCSituationFactionCondition cond = def.FactionCondition;
                if (cond == null) continue;
                if (!manager.IsEligibleToSpawn(def, null)) continue;
                if (cond.ShouldSpawn(faction)) manager.StartSituation(def, null);
            }

            List<FCSituationDef> settlementDefs = FactionCache.SettlementConditionedSituationDefs;
            if (settlementDefs.Count == 0) return;

            foreach (WorldSettlementFC settlement in faction.settlements)
            {
                if (settlement == null) continue;
                foreach (FCSituationDef def in settlementDefs)
                {
                    FCSituationSettlementCondition cond = def.SettlementCondition;
                    if (cond == null) continue;
                    if (!manager.IsEligibleToSpawn(def, settlement)) continue;
                    if (cond.ShouldSpawn(faction, settlement)) manager.StartSituation(def, settlement);
                }
            }
        }

        private static void AdvanceOne(FCSituation sit, FactionFC faction)
        {
            FCSituationDef def = sit.def;
            bool advancing = IsAdvancing(sit, faction);
            float delta = (advancing ? def.baseRatePerDay : def.decayRatePerDay)
                          + (sit.activeApproach != null ? sit.activeApproach.ratePerDay : 0f);

            if (def.Handler != null) delta = def.Handler.OnProgressTick(sit, delta);
            if (delta == 0f) return;

            ApplyDelta(sit, faction, delta);
        }

        /// <summary>Whether the bar should advance (vs. recede) right now. Condition-less defs (pure
        /// timers) always advance.</summary>
        public static bool IsAdvancing(FCSituation sit, FactionFC faction)
        {
            FCSituationDef def = sit.def;
            if (!def.HasCondition) return true;
            if (def.scope == FCSituationScope.Settlement)
                return def.SettlementCondition?.ShouldAdvance(faction, sit.targetSettlement) ?? true;
            return def.FactionCondition?.ShouldAdvance(faction) ?? true;
        }

        /* -*-*-*-*  Shared mover  *-*-*-*- */

        /// <summary>Applies a signed delta to the bar, running endpoint behavior and stage-transition
        /// processing. Triggered effects are queued through <c>eventManager.AddEvent</c>, so a delta that
        /// crosses an endpoint mid-event-pipeline (e.g. an option outcome) defers its resolution/loop
        /// event to the next processing pass rather than re-entering the event maker.</summary>
        public static void ApplyDelta(FCSituation sit, FactionFC faction, float delta)
        {
            if (sit?.def == null || delta == 0f) return;
            FCSituationDef def = sit.def;
            float old = sit.progress;
            float raw = old + delta;

            if (raw >= def.maxProgress)
                HandleEndpoint(sit, faction, atTop: true, raw: raw, old: old);
            else if (raw <= 0f)
                HandleEndpoint(sit, faction, atTop: false, raw: raw, old: old);
            else
            {
                sit.progress = raw;
                ResolveStageTransition(sit, faction, old, raw);
            }
        }

        private static void HandleEndpoint(FCSituation sit, FactionFC faction, bool atTop, float raw, float old)
        {
            FCSituationDef def = sit.def;
            FCSituationEndpointBehavior behavior = atTop ? def.topBehavior : def.bottomBehavior;
            float boundary = atTop ? def.maxProgress : 0f;

            switch (behavior)
            {
                case FCSituationEndpointBehavior.Clamp:
                    sit.progress = boundary;
                    ResolveStageTransition(sit, faction, old, boundary);
                    break;

                case FCSituationEndpointBehavior.Terminal:
                    sit.progress = boundary;
                    // Fire (defer) the resolution event and remove the situation first; the event's own
                    // handler (e.g. a settlement removal) runs on the next pass, tolerating the gone situation.
                    FireEvent(atTop ? def.topResolutionEvent : def.bottomResolutionEvent, sit, faction);
                    def.Handler?.OnResolved(sit, FCSituationEndpointBehavior.Terminal);
                    faction?.situationManager.Remove(sit);
                    break;

                case FCSituationEndpointBehavior.Loop:
                    // A wrap is a reset, NOT a descent/ascent through every stage: fire only loopEvent,
                    // recompute the stage at the new endpoint, and re-apply that stage's mods directly.
                    float wrapped = Mathf.Clamp(atTop ? raw - def.maxProgress : raw + def.maxProgress, 0f, def.maxProgress);
                    FCSituationStageDef oldStage = sit.currentStage;
                    sit.progress = wrapped;
                    sit.loopCount++;
                    FCSituationStageDef newStage = def.StageAt(wrapped);
                    if (newStage != oldStage)
                    {
                        SituationStatModifierApplier.RemoveStage(sit, faction, invalidate: false);
                        sit.currentStage = newStage;
                        SituationStatModifierApplier.ApplyStage(sit, faction, invalidate: false);
                        faction?.InvalidateFactionStatCache();
                    }
                    FireEvent(def.loopEvent, sit, faction);
                    def.Handler?.OnLooped(sit);
                    break;
            }
        }

        private static void ResolveStageTransition(FCSituation sit, FactionFC faction, float oldProgress, float newProgress)
        {
            FCSituationDef def = sit.def;
            FCSituationStageDef oldStage = sit.currentStage;
            FCSituationStageDef newStage = def.StageAt(newProgress);
            if (newStage == oldStage) return;

            List<FCSituationStageDef> stages = def.stages;
            int oldIdx = oldStage != null ? stages.IndexOf(oldStage) : -1;
            int newIdx = newStage != null ? stages.IndexOf(newStage) : -1;
            bool ascending = newProgress > oldProgress;

            if (ascending)
            {
                for (int k = oldIdx; k < newIdx; k++)
                {
                    if (k >= 0) FireStageSlot(sit, faction, stages[k], "onExitUpward", stages[k].onExitUpward);
                    FireStageSlot(sit, faction, stages[k + 1], "onEnterFromBelow", stages[k + 1].onEnterFromBelow);
                }
            }
            else
            {
                for (int k = oldIdx; k > newIdx; k--)
                {
                    if (k >= 0) FireStageSlot(sit, faction, stages[k], "onExitDownward", stages[k].onExitDownward);
                    if (k - 1 >= 0) FireStageSlot(sit, faction, stages[k - 1], "onEnterFromAbove", stages[k - 1].onEnterFromAbove);
                }
            }

            SituationStatModifierApplier.RemoveStage(sit, faction, invalidate: false);
            sit.currentStage = newStage;
            SituationStatModifierApplier.ApplyStage(sit, faction, invalidate: false);
            faction?.InvalidateFactionStatCache();

            def.Handler?.OnStageChanged(sit, oldStage, newStage,
                ascending ? FCSituationStageDirection.Ascending : FCSituationStageDirection.Descending);
        }

        private static void FireStageSlot(FCSituation sit, FactionFC faction, FCSituationStageDef stage, string slot, FCEventDef ev)
        {
            if (ev == null) return;
            if (stage.fireEventsOnce)
            {
                string key = FCSituation.LatchKey(stage, slot);
                if (sit.firedStageLatches.Contains(key)) return;
                sit.firedStageLatches.Add(key);
            }
            FireEvent(ev, sit, faction);
        }

        /// <summary>Queues an <see cref="FCEventDef"/> through the existing event pipeline, targeting the
        /// situation's settlement (faction-scoped situations fire untargeted events).</summary>
        private static void FireEvent(FCEventDef ev, FCSituation sit, FactionFC faction)
        {
            if (ev == null || faction == null) return;
            FCEvent evt = FCEventMaker.MakeEvent(ev);
            if (evt == null) return;
            if (sit.targetSettlement != null)
            {
                evt.settlementTraitLocations = new List<WorldSettlementFC> { sit.targetSettlement };
                evt.location = sit.targetSettlement.Tile;
                evt.source = sit.targetSettlement.Tile;
            }
            faction.eventManager.AddEvent(evt);
        }
    }
}
