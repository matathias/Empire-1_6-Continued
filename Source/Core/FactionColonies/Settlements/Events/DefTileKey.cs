namespace FactionColonies;

/// <summary>
/// Compound dictionary key pairing an <see cref="FCEventDef"/> with a
/// <see cref="PlanetTile"/>. Used by <see cref="FCEventManager"/> to
/// index events by (def, location) for O(1) lookups.
/// Delegates tile equality to <see cref="PlanetTile.Equals(PlanetTile)"/>,
/// preserving layer-aware comparison.
/// </summary>
/* Deliberately not a record struct: generated record equality would compare the def through
 * Def.Equals (defName hash + type) instead of by reference, so two distinct defs with colliding
 * defName hashes would become the same key. */
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
        int h1 = def is not null ? def.GetHashCode() : 0;
        int h2 = tile.GetHashCode();
        return (h1 * 397) ^ h2;
    }
}
