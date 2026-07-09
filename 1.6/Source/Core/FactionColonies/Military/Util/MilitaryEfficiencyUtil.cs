using RimWorld;
using System;
using Verse;

namespace FactionColonies
{
    public static class MilitaryEfficiencyUtil
    {
        /// <summary>
        /// Applies a combat efficiency hediff (buff or debuff) to a pawn based on
        /// the force's military efficiency. Removes any existing FC combat efficiency
        /// hediff first.
        /// </summary>
        public static void ApplyCombatEfficiencyHediff(Pawn pawn, double efficiency)
        {
            if (pawn == null || pawn.health == null) return;

            float dampedDelta = (float)((efficiency - 1.0) * FCSettings.efficiencyDamping);
            RemoveCombatEfficiencyHediff(pawn);

            if (Math.Abs(dampedDelta) < 0.001f) return;

            HediffDef hediffDef;
            float severity;

            if (dampedDelta > 0f)
            {
                hediffDef = FCHediffDefOf.FC_CombatEfficiency_Buff;
                severity = dampedDelta;
            }
            else
            {
                hediffDef = FCHediffDefOf.FC_CombatEfficiency_Debuff;
                severity = Math.Abs(dampedDelta);
            }

            Hediff hediff = HediffMaker.MakeHediff(hediffDef, pawn);
            hediff.Severity = severity;
            pawn.health.AddHediff(hediff);
        }

        /// <summary>
        /// Removes any FC combat efficiency hediff (buff or debuff) from the pawn.
        /// </summary>
        public static void RemoveCombatEfficiencyHediff(Pawn pawn)
        {
            if (pawn?.health?.hediffSet is null) return;

            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(FCHediffDefOf.FC_CombatEfficiency_Buff)
                ?? pawn.health.hediffSet.GetFirstHediffOfDef(FCHediffDefOf.FC_CombatEfficiency_Debuff);
            if (existing != null)
            {
                pawn.health.RemoveHediff(existing);
            }
        }
    }
}
