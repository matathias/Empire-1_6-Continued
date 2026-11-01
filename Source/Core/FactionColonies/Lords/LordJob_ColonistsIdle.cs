using Verse;
using Verse.AI.Group;

namespace FactionColonies
{
    class LordJob_ColonistsIdle : LordJob
    {
        public override bool AddFleeToil => false;
        public override bool AllowStartNewGatherings => false;
        public override bool AlwaysShowWeapon => true;
        private WorldSettlementFC settlement;

        public LordJob_ColonistsIdle() { }
        public LordJob_ColonistsIdle(WorldSettlementFC settlement)
        {
            this.settlement = settlement;
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref settlement, "settlement");
        }

        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();
            stateGraph.AddToil(new LordToil_IdleNearby());
            return stateGraph;
        }

        public override void Notify_PawnLost(Pawn pawn, PawnLostCondition condition)
        {
            settlement?.MilitaryComp?.RemoveDefender(pawn);
        }
    }
}
