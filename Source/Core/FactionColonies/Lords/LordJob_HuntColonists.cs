using Verse;
using Verse.AI.Group;

namespace FactionColonies
{
    public class LordJob_HuntColonists : LordJob
    {
        private bool delay;
        private WorldSettlementFC settlement;

        public LordJob_HuntColonists() { }
        public LordJob_HuntColonists(WorldSettlementFC settlement, bool delay)
        {
            this.settlement = settlement;
            this.delay = delay;
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref settlement, "settlement");
            Scribe_Values.Look(ref delay, "delay");
        }

        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();

            LordToil lordToil = new LordToil_HuntColonists();
            stateGraph.AddToil(lordToil);

            if (!delay) return stateGraph;
            LordToil idleToil = new LordToil_IdleNearby();
            stateGraph.AddToil(idleToil);
            stateGraph.StartingToil = idleToil;

            Transition startAssault = new Transition(idleToil, lordToil);
            startAssault.AddTrigger(new Trigger_TicksPassed(500));
            stateGraph.AddTransition(startAssault);

            return stateGraph;
        }

        public override void Notify_PawnLost(Pawn pawn, PawnLostCondition condition)
        {
            settlement?.MilitaryComp?.RemoveAttacker(pawn);
        }
    }
}