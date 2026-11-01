using System;
using Verse;

namespace FactionColonies
{
    public static class CompareUtil
    {
        public static int CompareFloatMenuOption(FloatMenuOption x, FloatMenuOption y)
        {
            return String.Compare(x.Label, y.Label);
        }

        public static int CompareBuildingDef(BuildingFCDef x, BuildingFCDef y)
        {
            return string.Compare(x.label, y.label);
        }

        public static int CompareSettlementName(WorldSettlementFC x, WorldSettlementFC y)
        {
            return string.Compare(x.Name, y.Name);
        }

        public static int CompareSettlementLevel(WorldSettlementFC x, WorldSettlementFC y)
        {
            return y.settlementLevel.CompareTo(x.settlementLevel);
        }

        public static int CompareSettlementMilitaryLevel(WorldSettlementFC x, WorldSettlementFC y)
        {
            return y.settlementMilitaryLevel.CompareTo(x.settlementMilitaryLevel);
        }

        public static int CompareSettlementFreeWorkers(WorldSettlementFC x, WorldSettlementFC y)
        {
            return ((y.workersUltraMax - y.GetTotalWorkers()).CompareTo((x.workersUltraMax - x.GetTotalWorkers())));
        }

        public static int CompareSettlementUnrest(WorldSettlementFC x, WorldSettlementFC y)
        {
            return x.unrest.CompareTo(y.unrest);
        }

        public static int CompareSettlementLoyalty(WorldSettlementFC x, WorldSettlementFC y)
        {
            return y.loyalty.CompareTo(x.loyalty);
        }

        public static int CompareSettlementHappiness(WorldSettlementFC x, WorldSettlementFC y)
        {
            return y.happiness.CompareTo(x.happiness);
        }

        public static int CompareSettlementProsperity(WorldSettlementFC x, WorldSettlementFC y)
        {
            return y.prosperity.CompareTo(x.prosperity);
        }

        public static int CompareSettlementProfit(WorldSettlementFC x, WorldSettlementFC y)
        {
            return y.GetTotalProfit().CompareTo(x.GetTotalProfit());
        }
        public static int CompareSettlementFoundingDate(WorldSettlementFC x, WorldSettlementFC y)
        {
            return y.FoundingTick.CompareTo(x.FoundingTick);
        }
    }
}
