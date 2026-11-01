using RimWorld;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Health and need queries / mutations for a mercenary squad.
    /// </summary>
    public static class SquadHealthUtil
    {
        /// <summary>Number of active (non-permanent) injuries on a single pawn. Returns 0 for null.</summary>
        public static int CountActiveInjuries(Pawn pawn)
        {
            int n = 0;
            List<Hediff> hediffs = pawn?.health?.hediffSet?.hediffs;
            if (hediffs is null) return 0;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_Injury inj && !inj.IsPermanent()) n++;
            }
            return n;
        }

        /// <summary>Total number of active (non-permanent) injuries summed across all mercs in the squad.</summary>
        public static int CountActiveInjuries(MercenarySquadFC squad)
        {
            int total = 0;
            if (squad?.mercenaries is null) return 0;
            foreach (Mercenary m in squad.mercenaries)
            {
                if (m is null || m.IsEmptySlot) continue;
                total += CountActiveInjuries(m.pawn);
            }
            return total;
        }

        /// <summary>Number of mercs with at least one active (non-permanent) injury — head count, not wound count.</summary>
        public static int CountInjuredMercs(MercenarySquadFC squad)
        {
            int count = 0;
            if (squad?.mercenaries is null) return 0;
            foreach (Mercenary m in squad.mercenaries)
            {
                if (m is null || m.IsEmptySlot) continue;
                if (CountActiveInjuries(m.pawn) > 0) count++;
            }
            return count;
        }

        /// <summary>Resets a single merc's pawn health (clears injuries, illnesses, etc).</summary>
        public static void HealPawn(Mercenary merc)
        {
            if (merc?.pawn?.health is object)
            {
                merc.pawn.health.Reset();
            }
        }

        /// <summary>Initializes (or restores) food/rest/joy needs to max for every equipped merc
        /// in the squad. Called pre-deployment so dropped-in pawns aren't immediately starving
        /// or exhausted from the gap between hires.</summary>
        public static void ResetNeeds(MercenarySquadFC squad)
        {
            if (squad is null) return;
            foreach (Pawn merc in squad.AllEquippedMercenaryPawns)
            {
                if (merc.health == null)
                    merc.health = new Pawn_HealthTracker(merc);
                if (merc.needs == null)
                    merc.needs = new Pawn_NeedsTracker(merc);
                if (merc.needs.food == null)
                    merc.needs.food = new Need_Food(merc);
                if (merc.needs.rest == null)
                    merc.needs.rest = new Need_Rest(merc);
                if (merc.RaceProps is null)
                {
                    LogUtil.Warning($"ResetNeeds encountered merc ({merc.Name}) with null RaceProps. Skipping");
                    continue;
                }
                if (merc.RaceProps.Humanlike && merc.needs.joy == null)
                    merc.needs.joy = new Need_Joy(merc);
                merc.needs.food.CurLevel = merc.needs.food.MaxLevel;
                merc.needs.rest.CurLevel = merc.needs.rest.MaxLevel;
                if (merc.RaceProps.Humanlike)
                {
                    if (merc.needs.joy != null)
                        merc.needs.joy.CurLevel = merc.needs.joy.MaxLevel;
                    if (merc.needs.mood?.thoughts?.memories != null)
                        merc.needs.mood.thoughts.memories.TryGainMemory(DefDatabase<ThoughtDef>.GetNamed("FC_Mercenary"));
                }
            }
        }
    }
}
