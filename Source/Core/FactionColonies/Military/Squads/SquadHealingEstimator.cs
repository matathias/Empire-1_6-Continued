using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
    /* SquadHealingEstimator                                                       */
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/

    /// <summary>
    /// Estimates how long it will take an injured squad to heal back to full effectiveness.
    /// Drives the "Healing X" UI label that replaced the multi-day cooldown countdown.
    /// 
    /// <para>Empty / dead slots are ignored. Only the player can refill those, and the
    /// healing system can't bring them back. The squad-level estimate is the max across
    /// all currently-alive injured mercenary pawns: the squad is "fully effective" when
    /// the slowest member finishes healing. Sub-pawns (animals + mechs) are excluded from
    /// this estimate; they heal/repair in the background through the same tick
    /// (<see cref="MilitaryFC.TickMercenaryHealing"/>) but don't gate the squad-effectiveness
    /// label, which tracks the combat mercenaries.</para>
    /// 
    /// <para>Per-tick heal rate matches the vanilla <c>Pawn_HealthTracker.HealthTickInterval</c>
    /// natural-heal formula: <c>8 * HealthScale * 0.01 * InjuryHealingFactor</c> HP per call.
    /// <see cref="MilitaryFC.TickMercenaryHealing"/> invokes that method once
    /// per in-game hour, so we divide the per-call heal by <see cref="GenDate.TicksPerHour"/>
    /// to get a per-tick rate. The <see cref="StatPart_EmpireMercHealRate"/> stat part is
    /// baked into the pawn's <see cref="StatDefOf.InjuryHealingFactor"/> for off-map registered
    /// mercs, so this estimate automatically reflects the
    /// user's heal-rate-multiplier slider and any per-settlement <c>mercHealRateMultiplier</c>
    /// contribution. The estimate undercounts somewhat because it doesn't account for the
    /// tended-healing branch firing on top, but that's a conservative bias the player can
    /// live with (actual recovery will be at least this fast, often faster).</para>
    /// </summary>
    public static class SquadHealingEstimator
    {
        /// <summary>Per-tick HP delivered by one HealthTickInterval call's base heal (before
        /// the per-hediff <c>naturalHealingFactor</c>). 8 base, 0.01 unit-conversion. See
        /// <c>Pawn_HealthTracker.HealthTickInterval</c>.</summary>
        private const float VanillaBaseHealPerCall = 8f * 0.01f;

        /// <summary>Ticks until every currently-alive injured mercenary pawn in
        /// <paramref name="squad"/> reaches full health. Animals are excluded; they're
        /// auto-replaced rather than healed. Returns 0 if the squad is null, empty, or
        /// has no mercenary injuries.</summary>
        public static int TicksToFullEffectiveness(MercenarySquadFC squad)
        {
            if (squad?.mercenaries is null) return 0;
            int worst = 0;
            foreach (Mercenary m in squad.mercenaries)
            {
                int t = TicksToFullHealth(m?.pawn);
                if (t > worst) worst = t;
            }
            return worst;
        }

        /// <summary>Ticks until <paramref name="pawn"/> heals every non-permanent injury at the
        /// pawn's current heal rate. Returns 0 if no injuries or pawn is null/dead.</summary>
        public static int TicksToFullHealth(Pawn pawn)
        {
            if (pawn is null || pawn.Dead || pawn.health?.hediffSet is null) return 0;

            float severitySum = SumActiveInjurySeverity(pawn);
            if (severitySum <= 0f) return 0;

            float healPerHourlyCall = VanillaBaseHealPerCall * pawn.HealthScale
                * pawn.GetStatValue(StatDefOf.InjuryHealingFactor);
            if (healPerHourlyCall <= 0.0001f)
            {
                // Healing has been disabled or stat is zero — return a sentinel large value
                // so UI can render "—" or "indefinite" instead of a misleadingly short ETA.
                return int.MaxValue;
            }

            float hoursToHeal = severitySum / healPerHourlyCall;
            float ticks = hoursToHeal * GenDate.TicksPerHour;
            if (ticks > int.MaxValue) return int.MaxValue;
            return (int)Math.Ceiling(ticks);
        }

        /// <summary>Sum of severities of non-permanent <see cref="Hediff_Injury"/> on
        /// <paramref name="pawn"/>. Mirrors what natural healing will reduce over time.</summary>
        private static float SumActiveInjurySeverity(Pawn pawn)
        {
            float sum = 0f;
            List<Hediff> list = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is Hediff_Injury inj && !inj.IsPermanent())
                    sum += inj.Severity;
            }
            return sum;
        }
    }
}
