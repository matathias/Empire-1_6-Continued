using RimWorld;
using System;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Per-pawn combat-effectiveness factor used to weight a mercenary's contribution to
    /// squad combat power. A downed pawn contributes nothing (counts as an empty slot for
    /// power-projection purposes); a conscious pawn contributes a value scaled by the
    /// worst of their consciousness / manipulation / moving capacities so a destroyed leg
    /// or a mangled hand drops the contribution sharply even when the pawn is still awake.
    /// </summary>
    public static class SquadEffectivenessUtil
    {
        /// <summary>
        /// Returns a value in [0, 1]. 0 for null / dead / downed pawns. Otherwise the
        /// minimum of Consciousness, Manipulation, and Moving capacity levels.
        /// </summary>
        public static double PawnEffectiveness(Pawn pawn)
        {
            if (pawn is null || pawn.Dead || pawn.Downed) return 0;
            var caps = pawn.health?.capacities;
            if (caps is null) return 0;
            float c = caps.GetLevel(PawnCapacityDefOf.Consciousness);
            float m = caps.GetLevel(PawnCapacityDefOf.Manipulation);
            float v = caps.GetLevel(PawnCapacityDefOf.Moving);
            double worst = Math.Min(c, Math.Min(m, v));
            if (worst < 0) return 0;
            if (worst > 1) return 1;
            return worst;
        }
    }
}
