using RimWorld;
using RimWorld.BaseGen;
using System;
using Verse;

namespace FactionColonies
{
    public class GenStep_Colony : GenStep_Scatterer
    {
        //The curve factor is used to curve the turret count depending on the guns production. Rewards
        //basic investment into guns, even though there will be a profit hit.
        private const double CurveFactor = 1.6;

        private WorldSettlementFC Settlement { get; set; }

        public override int SeedPart => 1806208471;

        private WorldSettlementFC LookupSettlement(Map map)
        {
            FactionFC settlementFaction = FindFC.FactionComp;
            return settlementFaction.ReturnSettlementByLocation(map.Tile);
        }

        protected override bool CanScatterAt(IntVec3 c, Map map)
        {
            if (!base.CanScatterAt(c, map) || !c.Standable(map) || c.Roofed(map) || !map.reachability.CanReachMapEdge(c,
                TraverseParms.For(TraverseMode.PassDoors)))
            {
                return false;
            }

            Settlement = LookupSettlement(map);
            if (Settlement == null) return false;

            int min = 36 + Settlement.settlementLevel * 2 - 2;
            return new CellRect(c.x - min / 2, c.z - min / 2, min, min).FullyContainedWithin(map.BoundsRect(12));
        }

        protected override void ScatterAt(IntVec3 c, Map map, GenStepParams parms, int stackCount = 1)
        {
            Settlement = LookupSettlement(map);

            int middle = 36 + Settlement.settlementLevel * 2;
            IntRange range = new IntRange(middle - 2, middle + 2);
            int randomInRange1 = range.RandomInRange;
            int randomInRange2 = range.RandomInRange;
            CellRect cellRect = new CellRect(c.x - randomInRange1 / 2, c.z - randomInRange2 / 2, randomInRange1,
                randomInRange2);
            Faction faction = FindFC.EmpireFaction;
            cellRect.ClipInsideMap(map);
            ResolveParams resolveParams = new ResolveParams();
            if (Settlement.settlementLevel >= 7)
            {
                resolveParams.filthDef = null;
            }
            else
            {
                resolveParams.filthDensity =
                    new FloatRange(Settlement.settlementLevel / 10f, (Settlement.settlementLevel + 3) / 10f);
            }

            resolveParams.chanceToSkipFloor = Math.Max(0, 100 - Settlement.settlementLevel * 20);
            resolveParams.rect = cellRect;
            resolveParams.faction = faction;
            BaseGen.globalSettings.map = map;
            double defenseBuildings = Settlement.GetDefenseBonus();

            int defenseCount = (int)(CurveFactor * Math.Log(defenseBuildings + 1));
            resolveParams.edgeDefenseMortarsCount = (int)Math.Ceiling(defenseCount / 3f);
            resolveParams.edgeDefenseTurretsCount = defenseCount;
            BaseGen.globalSettings.minBuildings = Settlement.settlementLevel;
            BaseGen.globalSettings.minBarracks = Settlement.settlementLevel;
            BaseGen.symbolStack.Push("colony", resolveParams);
            BaseGen.Generate();
        }
    }
}
