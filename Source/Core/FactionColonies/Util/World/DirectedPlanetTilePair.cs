namespace FactionColonies;

/// <summary>
/// Compound dictionary key for an ordered pair of <see cref="PlanetTile"/>s
/// (source -> destination). Equality delegates to <see cref="PlanetTile.Equals(PlanetTile)"/>,
/// preserving layer-aware comparison so that two settlements with the same
/// tileId on different planet layers (e.g. surface vs orbital) are treated
/// as distinct endpoints.
/// </summary>
public readonly record struct DirectedPlanetTilePair
{
    public readonly PlanetTile from;
    public readonly PlanetTile to;

    public DirectedPlanetTilePair(PlanetTile from, PlanetTile to)
    {
        this.from = from;
        this.to = to;
    }
}
