using RimWorld.Planet;
using System;

namespace FactionColonies
{
    /// <summary>
    /// Compound dictionary key for an ordered pair of <see cref="PlanetTile"/>s
    /// (source -> destination). Equality delegates to <see cref="PlanetTile.Equals(PlanetTile)"/>,
    /// preserving layer-aware comparison so that two settlements with the same
    /// tileId on different planet layers (e.g. surface vs orbital) are treated
    /// as distinct endpoints.
    /// </summary>
    public struct DirectedPlanetTilePair : IEquatable<DirectedPlanetTilePair>
    {
        public readonly PlanetTile from;
        public readonly PlanetTile to;

        public DirectedPlanetTilePair(PlanetTile from, PlanetTile to)
        {
            this.from = from;
            this.to = to;
        }

        public bool Equals(DirectedPlanetTilePair other)
            => from.Equals(other.from) && to.Equals(other.to);

        public override bool Equals(object obj)
            => obj is DirectedPlanetTilePair p && Equals(p);

        public override int GetHashCode()
            => (from.GetHashCode() * 397) ^ to.GetHashCode();
    }
}
