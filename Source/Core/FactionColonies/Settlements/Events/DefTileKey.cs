using RimWorld.Planet;
using System;

namespace FactionColonies
{
    /// <summary>
    /// Compound dictionary key pairing an <see cref="FCEventDef"/> with a
    /// <see cref="PlanetTile"/>. Used by <see cref="FCEventManager"/> to
    /// index events by (def, location) for O(1) lookups.
    /// Delegates tile equality to <see cref="PlanetTile.Equals(PlanetTile)"/>,
    /// preserving layer-aware comparison.
    /// </summary>
    public struct DefTileKey : IEquatable<DefTileKey>
    {
        public readonly FCEventDef def;
        public readonly PlanetTile tile;

        public DefTileKey(FCEventDef def, PlanetTile tile)
        {
            this.def = def;
            this.tile = tile;
        }

        public bool Equals(DefTileKey other)
        {
            return def == other.def && tile.Equals(other.tile);
        }

        public override bool Equals(object obj)
        {
            return obj is DefTileKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            int h1 = def is object ? def.GetHashCode() : 0;
            int h2 = tile.GetHashCode();
            return (h1 * 397) ^ h2;
        }
    }
}
