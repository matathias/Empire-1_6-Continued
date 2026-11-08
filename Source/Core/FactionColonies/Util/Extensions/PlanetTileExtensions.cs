namespace FactionColonies;

public static class PlanetTileExtensions
{
    /// <summary>
    /// Approximate distance in tiles from <paramref name="a"/> to <paramref name="b"/>, or
    /// <see cref="float.PositiveInfinity"/> when they are on different planet layers.
    /// WorldGrid.ApproxDistanceInTiles reads both tile ids on the first tile's layer, so a cross-layer
    /// pair would otherwise measure against an unrelated tile. Range checks against the result reject
    /// cross-layer pairs, and sorting by it puts them last.
    /// </summary>
    public static float ApproxDistanceSameLayer(this PlanetTile a, PlanetTile b) =>
        a.Layer == b.Layer ? Find.WorldGrid.ApproxDistanceInTiles(a, b) : float.PositiveInfinity;
}
