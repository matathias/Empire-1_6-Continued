using System.Collections.Generic;
using RimWorld.Planet;

namespace FactionColonies
{
    /// <summary>
    /// Allows submods to contribute extra world tiles as nodes in Empire's road network
    /// (e.g. VOE outposts). Register implementations of <see cref="IRoadNodeProvider"/>
    /// here (or via the <see cref="EmpireRegistry"/> facade).
    /// </summary>
    public static class RoadNodeProviderRegistry
    {
        private static readonly RegistryList<IRoadNodeProvider> _list = new RegistryList<IRoadNodeProvider>();

        internal static void Register(IRoadNodeProvider provider) => _list.Register(provider);
        internal static void Unregister(IRoadNodeProvider provider) => _list.Unregister(provider);
        internal static void ClearAll() => _list.ClearAll();
        public static IReadOnlyList<IRoadNodeProvider> Providers => _list.Items;

        /// <summary>
        /// Adds every provider's valid, root-surface tiles (as bare tileIds) to
        /// <paramref name="tileSet"/>. Centralizes the road system's surface-only invariant:
        /// the <c>tile.Valid</c> check is ordered first so <c>tile.Layer</c> is never
        /// dereferenced on an invalid tile.
        /// </summary>
        public static void CollectInto(HashSet<int> tileSet)
            => RegistryDispatch.Each(_list.Items, provider =>
            {
                foreach (PlanetTile tile in provider.GetRoadNodeTiles())
                {
                    if (tile.Valid && tile.Layer != null && tile.Layer.IsRootSurface)
                        tileSet.Add(tile.tileId);
                }
            }, nameof(IRoadNodeProvider.GetRoadNodeTiles));
    }
}
