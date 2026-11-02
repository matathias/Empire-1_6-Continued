using System.Text;

namespace FactionColonies.util;

public static class WorldTileChecker
{
    public static bool IsValidTileForNewSettlement(PlanetTile tile, WorldSettlementDef settlementdef, StringBuilder reason = null)
    {
        if (!tile.Valid)
        {
            reason?.Append("FCSelectedInvalidTile".Translate());
            return false;
        }

        if (!settlementdef.AllowsTileLayer(tile))
        {
            reason?.Append("FCInvalidPlanetLayer".Translate());
            return false;
        }

        if (!(settlementdef.GetSettlementTypeExtension().TileIsValidForSettlement(tile, reason)))
        {
            return false;
        }

        return true;
    }
}
