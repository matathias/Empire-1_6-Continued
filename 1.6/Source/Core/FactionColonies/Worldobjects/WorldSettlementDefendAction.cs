using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    public class WorldSettlementDefendAction : CaravanArrivalAction
    {
        private WorldSettlementFC settlement;

        //For saving
        public WorldSettlementDefendAction()
        {
        }

        public WorldSettlementDefendAction(WorldSettlementFC settlement)
        {
            this.settlement = settlement;
        }

        public override void Arrived(Caravan caravan) => settlement.MilitaryComp?.StartDefence(MilitaryOperationsUtil.ReturnMilitaryEventByLocation(settlement.Tile), () => settlement.MilitaryComp?.CaravanDefend(caravan));

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref settlement, "settlement");
        }

        public override string Label => "FCDefendColony".Translate();

        public override string ReportString => "FCDefendColonyDesc".Translate();

        public static IEnumerable<FloatMenuOption> GetFloatMenuOptions(
            Caravan caravan,
            WorldSettlementFC settlement)
        {
            return CaravanArrivalActionUtility.GetFloatMenuOptions(
                () => settlement.Spawned && settlement.MilitaryComp?.isUnderAttack == true,
                () => new WorldSettlementDefendAction(settlement),
                "FCDefendColony".Translate(), caravan,
                settlement.Tile, settlement);
        }
    }
}