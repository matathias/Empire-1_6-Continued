using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Caravan arrival action that joins an ongoing Empire manual offensive battle at an enemy
    /// settlement (the offense counterpart of <see cref="WorldSettlementDefendAction"/>). On arrival
    /// the caravan's pawns are dropped onto the live assault map as player-controlled reinforcements.
    /// </summary>
    public class WorldSettlementJoinAttackAction : CaravanArrivalAction
    {
        private Settlement settlement;

        // For saving.
        public WorldSettlementJoinAttackAction() { }

        public WorldSettlementJoinAttackAction(Settlement settlement)
        {
            this.settlement = settlement;
        }

        public override FloatMenuAcceptanceReport StillValid(Caravan caravan, PlanetTile destinationTile)
        {
            FloatMenuAcceptanceReport report = base.StillValid(caravan, destinationTile);
            if (!report) return report;
            if (settlement is null || !settlement.Spawned || settlement.Tile != destinationTile)
                return false;
            if (!(FindFC.MilitaryManager?.GetBattlefield(settlement.Tile)?.CanJoinAttack() ?? false))
                return FloatMenuAcceptanceReport.WithFailReason("FCJoinAttackNoBattle".Translate());
            return true;
        }

        public override void Arrived(Caravan caravan)
        {
            BattlefieldContext bf = FindFC.MilitaryManager?.GetBattlefield(settlement.Tile);
            bf?.CaravanJoinAttack(caravan);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref settlement, "settlement");
        }

        public override string Label => "FCJoinAttack".Translate();

        public override string ReportString => "FCJoinAttackDesc".Translate();

        public static IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan, Settlement settlement)
        {
            return CaravanArrivalActionUtility.GetFloatMenuOptions(
                () => settlement.Spawned
                    && (FindFC.MilitaryManager?.GetBattlefield(settlement.Tile)?.CanJoinAttack() ?? false),
                () => new WorldSettlementJoinAttackAction(settlement),
                "FCJoinAttack".Translate(), caravan,
                settlement.Tile, settlement);
        }
    }
}
