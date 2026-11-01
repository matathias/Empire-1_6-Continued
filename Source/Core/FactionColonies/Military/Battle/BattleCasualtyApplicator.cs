using RimWorld;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
    /* BattleCasualtyApplicator                                                    */
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/

    /// <summary>
    /// Translates an auto-resolved battle's abstract force decrement into real hediffs
    /// and deaths on a squad's deployed mercenary pawns. Without this, an auto-resolved
    /// loss never touches the merc healing or death-lifecycle systems and the squad is
    /// functionally invincible.
    /// <para>Algorithm:
    ///   rate = (initial - remaining) / initial, scaled by mercenaryCasualtyRateMultiplier;
    ///   deathChance = max(0, (rate - threshold) / (1 - threshold)) * maxDeathFraction,
    ///                 scaled by mercenaryDeathChanceMultiplier;
    ///   pick floor(deployedCount * rate) victims; per-victim roll for death vs injury.</para>
    /// <para>Deaths route through <c>Pawn.Kill</c>, which the existing MercenaryDied harmony
    /// patch handles (lifecycle hook, slot nulling, equipment cleanup). Injuries apply 1-3
    /// Cut hediffs on random non-vital body parts; severity scales with the casualty rate so
    /// a 30% loss feels mild and a 75%+ loss feels grim. Cumulative <c>Hediff_Injury</c>
    /// severity is clamped to stay below vanilla's <c>LethalDamageThreshold</c> so the injury
    /// path never crosses into vanilla's auto-kill check (opt-out via
    /// <see cref="FCSettings.respectLethalDamageThreshold"/>).</para>
    /// <para>Each pipeline step is a separate <c>public static</c> method so submods can
    /// Harmony-patch individual seams (death chance scaling, victim selection, wound type)
    /// without rewriting the orchestrator. Small helpers carry
    /// <see cref="MethodImplOptions.NoInlining"/> so RyuJIT can't optimize past a patch.</para>
    /// </summary>
    public static class BattleCasualtyApplicator
    {
        /* Injury severity bounds at the low and high ends of the casualty-rate spectrum.
         * Linear interpolation between them based on the squad's casualty rate. */
        private const float MildSeverityMin = 3f;
        private const float MildSeverityMax = 6f;
        private const float SevereSeverityMin = 10f;
        private const float SevereSeverityMax = 18f;

        /* Buffer below vanilla's LethalDamageThreshold so the last wound's severity roll
         * can't accidentally tip the pawn over the line due to clamping rounding. */
        private const float LethalSeveritySafetyMargin = 5f;

        /* Orchestrator. Calls each step in order. Patch only this if you need to restructure
         * the entire flow; most submod tweaks should patch one of the per-step methods below. */
        public static void ApplyCasualtiesToSquad(
            MercenarySquadFC squad,
            double initialForce,
            double remainingForce,
            WorldSettlementFC statsContext)
        {
            if (!FCSettings.applyAutoResolveInjuries) return;
            if (squad is null) return;
            if (initialForce <= 0) return;

            double rate = ComputeCasualtyRate(squad, initialForce, remainingForce, statsContext);
            if (rate <= 0) return;

            double deathChance = ComputeDeathChance(squad, rate, statsContext);

            List<Pawn> candidates = GatherCasualtyCandidates(squad);
            if (candidates.Count == 0) return;

            int victimCount = ComputeVictimCount(candidates.Count, rate);
            if (victimCount <= 0) return;
            if (victimCount > candidates.Count) victimCount = candidates.Count;

            FactionFC faction = FindFC.FactionComp;
            candidates.Shuffle();
            for (int i = 0; i < victimCount; i++)
            {
                Pawn victim = candidates[i];
                if (victim is null || victim.Dead) continue;
                try
                {
                    // Per-unit death-chance scope: scale the squad-wide chance by this soldier's own modifiers.
                    double victimDeathChance = deathChance;
                    if (faction is object)
                    {
                        double unitMult = faction.GetUnitStatValue(FCStatDefOf.mercenaryDeathChanceMultiplier, FindMercForPawn(squad, victim));
                        victimDeathChance *= (unitMult > 0 ? unitMult : 1.0);
                        if (victimDeathChance < 0) victimDeathChance = 0;
                        if (victimDeathChance > 1) victimDeathChance = 1;
                    }
                    ApplyCasualtyOutcome(victim, rate, victimDeathChance);
                }
                catch (Exception e)
                {
                    LogUtil.Error($"BattleCasualtyApplicator: failed to apply casualty to {victim?.LabelShortCap}: {e}");
                }
            }
        }

        /// <summary>Finds the Mercenary owning <paramref name="pawn"/> within the squad (mercs or animals), or null.</summary>
        private static Mercenary FindMercForPawn(MercenarySquadFC squad, Pawn pawn)
        {
            if (squad?.mercenaries != null)
                foreach (Mercenary m in squad.mercenaries)
                    if (m?.pawn == pawn) return m;
            if (squad != null)
                foreach (Mercenary m in squad.AllSubPawns())
                    if (m?.pawn == pawn) return m;
            return null;
        }

        /// <summary>
        /// Returns the clamped fraction [0, 1] of <paramref name="squad"/>'s force that
        /// should become real casualties, given the abstract force snapshot. Applies the
        /// <c>mercenaryCasualtyRateMultiplier</c> FCStat so policies/events/buildings can
        /// soften or amplify attrition through the existing modifier pipeline.
        /// </summary>
        public static double ComputeCasualtyRate(
            MercenarySquadFC squad,
            double initialForce,
            double remainingForce,
            WorldSettlementFC statsContext)
        {
            if (initialForce <= 0) return 0;

            double rawRate = (initialForce - remainingForce) / initialForce;
            if (rawRate < 0) rawRate = 0;
            if (rawRate > 1) rawRate = 1;

            FactionFC faction = FindFC.FactionComp;
            // squad context folds per-squad casualty-rate modifiers on top of faction/settlement
            double rateMult = faction is object
                ? faction.GetStatValue(FCStatDefOf.mercenaryCasualtyRateMultiplier, statsContext, squad)
                : 1.0;
            double finalRate = rawRate * (rateMult > 0 ? rateMult : 1.0);
            if (finalRate < 0) finalRate = 0;
            if (finalRate > 1) finalRate = 1;
            return finalRate;
        }

        /// <summary>
        /// Per-casualty probability of death (vs injury). Ramps from 0 at
        /// <see cref="FCSettings.autoResolveCasualtyDeathThreshold"/> to
        /// <see cref="FCSettings.autoResolveCasualtyMaxDeathFraction"/> at rate=1.0. Multiplied
        /// by the <c>mercenaryDeathChanceMultiplier</c> FCStat so submods can make a squad
        /// more or less lethal-prone.
        /// </summary>
        public static double ComputeDeathChance(
            MercenarySquadFC squad,
            double rate,
            WorldSettlementFC statsContext)
        {
            float threshold = FCSettings.autoResolveCasualtyDeathThreshold;
            float maxDeathFraction = FCSettings.autoResolveCasualtyMaxDeathFraction;

            double deathChance = 0;
            if (rate > threshold && threshold < 1f)
                deathChance = ((rate - threshold) / (1.0 - threshold)) * maxDeathFraction;

            FactionFC faction = FindFC.FactionComp;
            // squad context folds per-squad death-chance modifiers; per-unit modifiers are applied per-victim
            // in ApplyCasualtiesToSquad (so individual soldiers can differ).
            double deathMult = faction is object
                ? faction.GetStatValue(FCStatDefOf.mercenaryDeathChanceMultiplier, statsContext, squad)
                : 1.0;
            deathChance *= (deathMult > 0 ? deathMult : 1.0);
            if (deathChance < 0) deathChance = 0;
            if (deathChance > 1) deathChance = 1;
            return deathChance;
        }

        /// <summary>
        /// Returns all living mercenary and animal pawns in <paramref name="squad"/> that are
        /// eligible to take a casualty. Both lists are flattened — animals were part of the
        /// abstract force and share the casualty roll.
        /// </summary>
        public static List<Pawn> GatherCasualtyCandidates(MercenarySquadFC squad)
        {
            List<Pawn> candidates = new List<Pawn>();
            if (squad?.mercenaries != null)
            {
                foreach (Mercenary m in squad.mercenaries)
                {
                    if (m?.pawn is object && !m.pawn.Dead) candidates.Add(m.pawn);
                }
            }
            if (squad != null)
            {
                foreach (Mercenary m in squad.AllSubPawns())
                {
                    if (m?.pawn is object && !m.pawn.Dead) candidates.Add(m.pawn);
                }
            }
            return candidates;
        }

        /// <summary>
        /// Number of casualty slots to roll outcomes for, given the candidate pool and the
        /// casualty rate. At least 1 when rate &gt; 0 and there's at least one candidate.
        /// </summary>
        public static int ComputeVictimCount(int candidateCount, double rate)
        {
            if (candidateCount <= 0 || rate <= 0) return 0;
            int n = (int)Math.Floor(candidateCount * rate);
            if (n <= 0) n = 1;
            if (n > candidateCount) n = candidateCount;
            return n;
        }

        /// <summary>
        /// Single-pawn outcome: kill or injure. Default routes via the death-chance roll;
        /// submods can patch to bias outcomes per-pawn (e.g. specialist immunity to deaths).
        /// </summary>
        public static void ApplyCasualtyOutcome(Pawn pawn, double rate, double deathChance)
        {
            if (deathChance > 0 && Rand.Value < deathChance) KillCasualty(pawn);
            else InjureCasualty(pawn, rate);
        }

        /// <summary>
        /// Kills <paramref name="pawn"/> via <c>Pawn.Kill</c>, letting the existing
        /// MercenaryDied harmony patch handle squad/lifecycle bookkeeping.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void KillCasualty(Pawn pawn)
        {
            if (pawn is null || pawn.Dead) return;
            pawn.Kill(null);
        }

        /// <summary>
        /// Adds 1-3 Cut hediffs on random non-vital body parts of <paramref name="pawn"/>.
        /// Severity is linearly interpolated between mild and severe bounds based on
        /// <paramref name="rate"/>. Total added severity is clamped to keep cumulative
        /// <c>Hediff_Injury</c> severity below vanilla's <c>LethalDamageThreshold</c>
        /// (opt-out: <see cref="FCSettings.respectLethalDamageThreshold"/>) so the injury
        /// path never accidentally triggers vanilla's auto-kill check.
        /// </summary>
        public static void InjureCasualty(Pawn pawn, double rate)
        {
            if (pawn?.health?.hediffSet is null) return;

            float budget = GetAvailableLethalSeverityBudget(pawn);
            if (budget <= 0)
            {
                // Pawn is already at or beyond the lethal ceiling — any wound risks the
                // auto-kill. Submods that explicitly want to push pawns over can disable
                // the ceiling via FCSettings.respectLethalDamageThreshold.
                return;
            }

            int woundCount = rate >= 0.75 ? Rand.RangeInclusive(2, 3) : Rand.RangeInclusive(1, 2);

            // casualtyWoundSeverityMultiplier (faction-wide) scales wound severity; the lethal budget below still caps applied damage.
            float severityMult = (float)(FindFC.FactionComp?.GetStatValue(FCStatDefOf.casualtyWoundSeverityMultiplier) ?? 1);

            float t = (float)Math.Min(1.0, rate / 0.75);
            float sevMin = Mathf.Lerp(MildSeverityMin, SevereSeverityMin, t) * severityMult;
            float sevMax = Mathf.Lerp(MildSeverityMax, SevereSeverityMax, t) * severityMult;

            for (int i = 0; i < woundCount; i++)
            {
                if (budget <= 0) break;

                BodyPartRecord part = PickWoundablePart(pawn);
                if (part is null) return;

                float rolled = Rand.Range(sevMin, sevMax);
                float applied = Math.Min(rolled, budget);
                if (applied <= 0) break;

                Hediff hediff = HediffMaker.MakeHediff(HediffDefOf.Cut, pawn, part);
                hediff.Severity = applied;
                pawn.health.AddHediff(hediff, part, null, null);
                budget -= applied;
            }
        }

        /// <summary>
        /// Picks a non-missing body part that isn't a consciousness source (brain), so the
        /// applicator can't accidentally kill a pawn through brain damage when the death
        /// roll already decided it should survive as injured. Returns null when no suitable
        /// part exists, in which case the caller skips the wound.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static BodyPartRecord PickWoundablePart(Pawn pawn)
        {
            if (pawn?.health?.hediffSet is null) return null;
            List<BodyPartRecord> all = new List<BodyPartRecord>();
            foreach (BodyPartRecord p in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (p?.def?.tags != null && p.def.tags.Contains(BodyPartTagDefOf.ConsciousnessSource)) continue;
                all.Add(p);
            }
            return all.Count == 0 ? null : all.RandomElement();
        }

        /// <summary>
        /// Returns how much additional <c>Hediff_Injury</c> severity can be added to
        /// <paramref name="pawn"/> before vanilla's <c>ShouldBeDeadFromLethalDamageThreshold</c>
        /// check trips. A small safety margin is subtracted so the last clamped wound can't
        /// tip the pawn over due to rounding. Returns <see cref="float.MaxValue"/> when the
        /// ceiling is disabled by FCSettings (for mod compat).
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static float GetAvailableLethalSeverityBudget(Pawn pawn)
        {
            if (!FCSettings.respectLethalDamageThreshold) return float.MaxValue;
            if (pawn?.health is null) return float.MaxValue;
            float threshold = pawn.health.LethalDamageThreshold;
            float current = CurrentLethalInjurySeverity(pawn);
            return threshold - current - LethalSeveritySafetyMargin;
        }

        /// <summary>
        /// Sum of severities of all currently-present <c>Hediff_Injury</c> on
        /// <paramref name="pawn"/> — the same count vanilla's
        /// <c>ShouldBeDeadFromLethalDamageThreshold</c> uses.
        /// </summary>
        private static float CurrentLethalInjurySeverity(Pawn pawn)
        {
            float sum = 0f;
            if (pawn?.health?.hediffSet is null) return sum;
            List<Hediff> list = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < list.Count; i++)
                if (list[i] is Hediff_Injury) sum += list[i].Severity;
            return sum;
        }
    }
}
