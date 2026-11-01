using FactionColonies.util;
using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI.Group;

namespace FactionColonies
{
    public class LordJob_DefendColony : LordJob
    {
        private Dictionary<Pawn, Pawn> mounts;
        private List<Pawn> mountsKeys = new List<Pawn>();
        private List<Pawn> mountsValues = new List<Pawn>();
        private readonly HashSet<Pawn> readded = new HashSet<Pawn>();
        private WorldSettlementFC settlement;

        public LordJob_DefendColony()
        {
        }

        public LordJob_DefendColony(WorldSettlementFC settlement, Dictionary<Pawn, Pawn> mounts)
        {
            this.settlement = settlement;
            this.mounts = mounts;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref settlement, "settlement");
            Scribe_Collections.Look(ref mounts, "mounts", LookMode.Reference, LookMode.Reference, ref mountsKeys, ref mountsValues);
        }

        public override bool AddFleeToil => false;
        public override bool AllowStartNewGatherings => false;
        public override bool AlwaysShowWeapon => true;

        public override void LordJobTick()
        {
            base.LordJobTick();
            if (readded.Count > 0)
            {
                lord.CurLordToil.UpdateAllDuties();
                readded.Clear();
            }
        }

        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();
            LordToil_DefendSelfAndMount defendSelf = new LordToil_DefendSelfAndMount(mounts);
            stateGraph.AddToil(defendSelf);
            LordToil defendColony = new LordToil_DefendColony();
            stateGraph.AddToil(defendColony);
            Transition startDefending = new Transition(defendSelf, defendColony);
            startDefending.AddTrigger(new Trigger_Signal("startAssault"));
            stateGraph.AddTransition(startDefending);
            return stateGraph;
        }

        public override void Notify_PawnLost(Pawn pawn, PawnLostCondition condition)
        {
            if (condition == PawnLostCondition.ChangedFaction || condition == PawnLostCondition.ExitedMap)
            {
                // Player is drafting this pawn — let it go without re-adding
                if (condition == PawnLostCondition.ChangedFaction && pawn.Faction == Faction.OfPlayer)
                {
                    return;
                }
                if (pawn.Spawned)
                {
                    lord.AddPawn(pawn);
                    readded.Add(pawn);
                    return;
                }
                // Already despawned (e.g. joined an existing caravan): fall through to RemoveDefender.
            }
            if (pawn.IsMercenary() && pawn.Faction != FindFC.EmpireFaction) pawn.SetFaction(FindFC.EmpireFaction);

            settlement?.MilitaryComp?.RemoveDefender(pawn);
        }
    }
}