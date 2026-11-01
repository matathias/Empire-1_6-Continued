using RimWorld.Planet;
using System.Collections.Generic;

namespace FactionColonies
{
    public class ResourceHilliness
    {
        public Hilliness hilliness = Hilliness.Undefined;
        public double additive = 0;
        public double multiplier = 1;
    }
    /// <summary>
    /// This extension defines the production bonuses a resource gets from tile hilliness.
    /// </summary>
    public class ResourceProductionExtension_Hilliness : ResourceProductionExtension
    {
        public List<ResourceHilliness> resourceHilliness = new List<ResourceHilliness>();

        public ResourceProductionExtension_Hilliness()
        {
            extName = "Hilliness";
            extDesc = "Tile hilliness";
        }
        public override double GetAdditiveBonus(PlanetTile tile, WorldSettlementFC settlement = null)
        {
            if (tile == PlanetTile.Invalid)
            {
                return 0;
            }
            Hilliness hilly = tile.Tile?.hilliness ?? Hilliness.Undefined;
            if (hilly == Hilliness.Undefined)
            {
                return 0;
            }
            return resourceHilliness.Find((ResourceHilliness rh) => rh.hilliness == hilly)?.additive ?? 0;
        }
        public override double GetMultiplierBonus(PlanetTile tile, WorldSettlementFC settlement = null)
        {
            if (tile == PlanetTile.Invalid)
            {
                return 1;
            }
            Hilliness hilly = tile.Tile?.hilliness ?? Hilliness.Undefined;
            if (hilly == Hilliness.Undefined)
            {
                return 1;
            }
            return resourceHilliness.Find((ResourceHilliness rh) => rh.hilliness == hilly)?.multiplier ?? 1;
        }
    }
}
