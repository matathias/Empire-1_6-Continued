using System;

namespace FactionColonies
{
    /// <summary>
    /// Compound dictionary key for an undirected pair of tile IDs within a single
    /// PlanetLayer. Used by <see cref="FCRoadQueue"/> for MST edge caches where
    /// edge (A, B) is identical to (B, A). Layer context is held externally on
    /// the queue (see <c>incrementalLayer</c>); this struct only carries tile IDs.
    /// </summary>
    public struct TileEdgeKey : IEquatable<TileEdgeKey>
    {
        public readonly int lo;
        public readonly int hi;

        public TileEdgeKey(int a, int b)
        {
            if (a < b) { lo = a; hi = b; }
            else { lo = b; hi = a; }
        }

        public bool Equals(TileEdgeKey other) => lo == other.lo && hi == other.hi;

        public override bool Equals(object obj) => obj is TileEdgeKey k && Equals(k);

        public override int GetHashCode() => (lo * 397) ^ hi;

        /* Save-format bridge for FCRoadQueue.edgeCostCache. The wire format
         * remains Dictionary<long, float> so existing saves continue to load. */
        public long Pack() => ((long)lo << 32) | (uint)hi;

        public static TileEdgeKey Unpack(long key)
            => new TileEdgeKey((int)(key >> 32), (int)(key & 0xFFFFFFFFL));
    }
}
