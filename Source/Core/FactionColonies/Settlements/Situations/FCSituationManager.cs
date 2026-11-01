using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using FactionColonies.util;

namespace FactionColonies
{
    /// <summary>
    /// Owns every live <see cref="FCSituation"/> for the faction. Lives on <see cref="FactionFC"/>,
    /// scribed <c>Scribe_Deep</c> (additive — old saves deserialize an empty manager, no migration).
    /// Mirrors <see cref="FCEventManager"/>'s ownership / version-cache / id-allocation conventions.
    /// </summary>
    public class FCSituationManager : IExposable
    {
        private List<FCSituation> situations = new List<FCSituation>();
        /* Spawn key -> tick a situation of that (def, target) was last removed; drives cooldownDays. */
        private Dictionary<string, int> cooldowns = new Dictionary<string, int>();
        private int version;
        private int nextSituationId = 1;

        public IReadOnlyList<FCSituation> Situations => situations;
        public int Version => version;
        public int Count => situations.Count;

        public int NextSituationId() => nextSituationId++;

        private void Bump() => version++;

        /// <summary>Stable spawn-eligibility key. Settlement-scoped defs key on (defName, settlement.ID)
        /// so a per-settlement cooldown/cap never blocks a different settlement.</summary>
        public static string SpawnKey(FCSituationDef def, WorldSettlementFC target) =>
            target != null ? def.defName + "@" + target.ID : def.defName;

        /* -*-*-*-*  Creation / removal  *-*-*-*- */

        /// <summary>The public creation API. Sets the bar to <c>startProgress</c>, the default approach,
        /// applies the starting stage + approach stat-modifiers, and fires the handler's OnStarted.</summary>
        public FCSituation StartSituation(FCSituationDef def, WorldSettlementFC target = null)
        {
            if (def == null) return null;
            if (def.scope == FCSituationScope.Settlement && target == null)
            {
                LogUtil.Warning($"FCSituationManager.StartSituation: settlement-scoped def '{def.defName}' started with no target; ignoring.");
                return null;
            }

            FCSituation sit = new FCSituation
            {
                def = def,
                progress = def.startProgress,
                targetSettlement = def.scope == FCSituationScope.Settlement ? target : null,
                activeApproach = def.defaultApproach,
                loadID = NextSituationId(),
                tickStarted = Find.TickManager.TicksGame,
                firedStageLatches = new HashSet<string>()
            };
            sit.currentStage = def.StageAt(sit.progress);
            situations.Add(sit);

            FactionFC faction = FindFC.FactionComp;
            SituationStatModifierApplier.ApplyStage(sit, faction);
            SituationStatModifierApplier.ApplyApproach(sit, faction);

            def.Handler?.OnStarted(sit);
            Bump();
            return sit;
        }

        /// <summary>Removes a situation: strips its stat-modifier sources and records the cooldown
        /// stamp. Idempotent — returns false if the situation is already gone (tolerates a
        /// resolution-event handler that the engine already removed).</summary>
        public bool Remove(FCSituation sit)
        {
            if (sit == null || !situations.Contains(sit)) return false;
            FactionFC faction = FindFC.FactionComp;
            SituationStatModifierApplier.RemoveAll(sit, faction);
            cooldowns[SpawnKey(sit.def, sit.targetSettlement)] = Find.TickManager.TicksGame;
            situations.Remove(sit);
            Bump();
            return true;
        }

        /// <summary>Switches a situation's active approach (free + immediate): swaps the approach
        /// stat-modifiers and bumps the UI version. No-op if already active.</summary>
        public void SwitchApproach(FCSituation sit, FCSituationApproachDef approach)
        {
            if (sit == null || approach == null || sit.activeApproach == approach) return;
            FactionFC faction = FindFC.FactionComp;
            SituationStatModifierApplier.RemoveApproach(sit, faction, invalidate: false);
            sit.activeApproach = approach;
            SituationStatModifierApplier.ApplyApproach(sit, faction, invalidate: false);
            faction?.InvalidateFactionStatCache();
            Bump();
        }

        /// <summary>Nudges the bar directly (events / options / debug). Runs the same endpoint + stage
        /// processing as the daily advance pass.</summary>
        public void AddProgress(FCSituation sit, float delta)
        {
            if (sit == null) return;
            FCSituationMaker.ApplyDelta(sit, FindFC.FactionComp, delta);
            Bump();
        }

        /* -*-*-*-*  Queries  *-*-*-*- */

        public bool AnyWithDef(FCSituationDef def) => situations.Any(s => s.def == def);
        public IEnumerable<FCSituation> GetByDef(FCSituationDef def) => situations.Where(s => s.def == def);
        public int CountWithDef(FCSituationDef def) => situations.Count(s => s.def == def);
        public int CountWithDefAndTarget(FCSituationDef def, WorldSettlementFC target) =>
            situations.Count(s => s.def == def && s.targetSettlement == target);
        public IEnumerable<FCSituation> GetForSettlement(WorldSettlementFC settlement) =>
            situations.Where(s => s.targetSettlement == settlement);
        public bool AnyForSettlement(WorldSettlementFC settlement) =>
            situations.Any(s => s.targetSettlement == settlement);

        /// <summary>Eligible to spawn a new instance of this def for this target: under the per-target
        /// concurrency cap and past the cooldown window.</summary>
        public bool IsEligibleToSpawn(FCSituationDef def, WorldSettlementFC target)
        {
            if (CountWithDefAndTarget(def, target) >= Mathf.Max(1, def.maxConcurrent)) return false;
            if (cooldowns.TryGetValue(SpawnKey(def, target), out int last)
                && Find.TickManager.TicksGame - last < def.cooldownDays * GenDate.TicksPerDay)
                return false;
            return true;
        }

        /* -*-*-*-*  Settlement removal & gating  *-*-*-*- */

        /// <summary>Drops a removed settlement's situations and their stat-modifier sources. Idempotent.</summary>
        public void OnSettlementRemoved(WorldSettlementFC settlement)
        {
            if (settlement == null) return;
            FactionFC faction = FindFC.FactionComp;
            for (int i = situations.Count - 1; i >= 0; i--)
            {
                if (situations[i].targetSettlement == settlement)
                {
                    SituationStatModifierApplier.RemoveAll(situations[i], faction);
                    situations.RemoveAt(i);
                    Bump();
                }
            }
        }

        /// <summary>True if any active situation on the settlement blocks player deletion.</summary>
        public bool IsSettlementRemovalBlocked(WorldSettlementFC settlement, out string reason)
        {
            reason = null;
            if (settlement == null) return false;
            foreach (FCSituation sit in situations)
            {
                if (sit.targetSettlement != settlement) continue;
                FCSituationHandlerExtension handler = sit.def.Handler;
                if (handler != null)
                {
                    if (handler.BlocksSettlementRemoval(sit, out reason)) return true;
                }
                else if (sit.def.blocksSettlementRemoval)
                {
                    reason = sit.def.removalBlockedReasonKey.NullOrEmpty()
                        ? (string)"FCSituationBlocksRemovalDefault".Translate(sit.def.LabelCap)
                        : (string)sit.def.removalBlockedReasonKey.Translate();
                    return true;
                }
            }
            return false;
        }

        /* -*-*-*-*  Load  *-*-*-*- */

        // Stat-modifiers are re-applied on load per-settlement from WorldSettlementFC.PostLoadInit via
        // SituationStatModifierApplier.ApplyForSettlement — the same mechanism events use (settlement
        // statModifiers are not serialized; they rebuild from sources). No manager-level reapply.

        private void PruneOrphaned()
        {
            int removed = situations.RemoveAll(s => s == null || s.def == null
                || (s.def.scope == FCSituationScope.Settlement && s.targetSettlement == null));
            if (removed > 0)
            {
                LogUtil.Warning($"FCSituationManager: pruned {removed} orphaned situation(s) on load (missing def or target).");
                Bump();
            }
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref situations, "situations", LookMode.Deep);
            if (situations == null) situations = new List<FCSituation>();
            Scribe_Collections.Look(ref cooldowns, "cooldowns", LookMode.Value, LookMode.Value);
            if (cooldowns == null) cooldowns = new Dictionary<string, int>();
            Scribe_Values.Look(ref nextSituationId, "nextSituationId", 1);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                PruneOrphaned();
        }
    }
}
