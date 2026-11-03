namespace FactionColonies;

/// <summary>
/// Compound dictionary key for an undirected pair of tile IDs within a single
/// PlanetLayer. Used by <see cref="FCRoadQueue"/> for MST edge caches where
/// edge (A, B) is identical to (B, A). Layer context is held externally on
/// the queue (see <c>incrementalLayer</c>); this struct only carries tile IDs.
/// </summary>
public readonly record struct TileEdgeKey
{
    public readonly int lo;
    public readonly int hi;

    /* Normalizes the order, so (A, B) and (B, A) produce equal keys and equal hashes. */
    public TileEdgeKey(int a, int b)
    {
        if (a < b) { lo = a; hi = b; }
        else { lo = b; hi = a; }
    }

    /* Save-format bridge for FCRoadQueue.edgeCostCache. The wire format
     * remains Dictionary<long, float> so existing saves continue to load. */
    public long Pack() => ((long)lo << 32) | (uint)hi;

    public static TileEdgeKey Unpack(long key)
        => new((int)(key >> 32), (int)(key & 0xFFFFFFFFL));
}
