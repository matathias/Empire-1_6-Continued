using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;


namespace FactionColonies
{
    public class FCPrisoner : ILoadReferenceable, IExposable
    {
        public Pawn prisoner;
        public WorldSettlementFC settlement;
        public float unrest;
        public float health;
        public bool isReturning;
        public int loadID;
        public FCWorkLoad workload;

        // Save-time flag: true when the pawn has another deep owner (Map.mapPawns,
        // WorldPawns.pawnsAlive). Used to fall back to Scribe_References and avoid
        // the "Id already used" duplicate-registration cascade on load.
        private bool isExternallyOwned;


        public FCPrisoner()
        {
        }

        public FCPrisoner(Pawn pawn, WorldSettlementFC settlement)
        {
            prisoner = pawn;
            this.settlement = settlement;
            unrest = 0;
            health = (float)Math.Round(prisoner.health.summaryHealth.SummaryHealthPercent * 100);
            isReturning = false;
            FactionFC comp = FindFC.FactionComp;
            if (comp is object)
            {
                loadID = comp.GetNextPrisonerID();
            }
            else
            {
                loadID = Rand.Int;
                LogUtil.Error($"FCPrisoner: FactionComp is null during construction. Using fallback loadID {loadID}.");
            }
            pawn.guest.SetGuestStatus(FindFC.EmpireFaction, GuestStatus.Prisoner);
        }


        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                isExternallyOwned = prisoner is object &&
                    (prisoner.Map is object
                     || prisoner.SpawnedOrAnyParentSpawned
                     || (Find.WorldPawns is object && Find.WorldPawns.Contains(prisoner)));
            }

            Scribe_Values.Look(ref isExternallyOwned, "isExternallyOwned", false);

            if (isExternallyOwned)
            {
                Scribe_References.Look(ref prisoner, "prisoner");
            }
            else
            {
                Scribe_Deep.Look(ref prisoner, "prisoner");
            }

            Scribe_References.Look(ref settlement, "settlement");
            Scribe_Values.Look(ref unrest, "unrest");
            Scribe_Values.Look(ref health, "healthy");
            Scribe_Values.Look(ref isReturning, "isReturning");
            Scribe_Values.Look(ref loadID, "loadID");
            Scribe_Values.Look(ref workload, "workload");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // Migration: legacy saves always Scribe_Deep'd the prisoner even when
                // WorldPawns also held it. The duplicate-id registration failure left
                // FCPrisoner.prisoner as an orphan dupe (different reference, same
                // thingIDNumber as the WorldPawns instance). Re-bind to the canonical
                // instance and pull it out so we own it cleanly going forward.
                if (!isExternallyOwned && prisoner is object && Find.WorldPawns is object)
                {
                    if (Find.WorldPawns.Contains(prisoner))
                    {
                        Find.WorldPawns.RemovePawn(prisoner);
                    }
                    else
                    {
                        Pawn registered = Find.WorldPawns.AllPawnsAliveOrDead
                            .FirstOrDefault(p => p != null && p.thingIDNumber == prisoner.thingIDNumber);
                        if (registered is object && !ReferenceEquals(registered, prisoner))
                        {
                            prisoner = registered;
                            Find.WorldPawns.RemovePawn(prisoner);
                        }
                    }
                }
            }
        }


        public string GetUniqueLoadID()
        {
            return "FCPrisoner_" + loadID;
        }



        public void AdjustHealth(FCWorkLoad workload)
        {
            health += FCWorkLoadInfo.HealthDelta(workload);
            if (health >= 100)
            {
                health = 100;
                HealNonPermanentInjuries();
            }
        }

        /* Rimworld's HealthUtility.HealNonPermanentInjuriesAndRestoreLegs is the closest
         * vanilla helper, but its leg-restoration side effect is undesirable here — a
         * prisoner reaching full "health" shouldn't magically regrow missing limbs. */
        private void HealNonPermanentInjuries()
        {
            if (prisoner?.health?.hediffSet is null) return;
            List<Hediff> hediffs = prisoner.health.hediffSet.hediffs;
            for (int i = hediffs.Count - 1; i >= 0; i--)
            {
                if (hediffs[i] is Hediff_Injury injury && !injury.IsPermanent())
                    prisoner.health.RemoveHediff(injury);
            }
        }

        public bool IsDead => health <= 0;
    }
}
