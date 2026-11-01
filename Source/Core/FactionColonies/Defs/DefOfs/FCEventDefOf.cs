using RimWorld;
using Verse;

namespace FactionColonies
{
    [DefOf]
    public class FCEventDefOf
    {
        //List Events here - loads events at start
        public static FCEventDef settleNewColony;
        public static FCEventDef taxColony;
        public static FCEventDef constructBuilding;
        public static FCEventDef enactSettlementPolicy;
        public static FCEventDef enactFactionPolicy;
        public static FCEventDef upgradeSettlement;
        public static FCEventDef raidEnemySettlement;
        public static FCEventDef enslaveEnemySettlement;
        public static FCEventDef captureEnemySettlement;
        public static FCEventDef razeEnemySettlement;
        public static FCEventDef cooldownMilitary;
        public static FCEventDef settlementBeingAttacked;
        public static FCEventDef autoResolveBattleRound;
        public static FCEventDef deliveryArrival;

        static FCEventDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FCEventDefOf));
        }
    }
}
