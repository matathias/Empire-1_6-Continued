using System.Collections.Generic;
using RimWorld.Planet;

namespace FactionColonies
{
    /// <summary>
    /// Allows external mods to contribute extra world tiles that should participate in
    /// Empire's road-network MST (e.g. VOE outposts), alongside Empire settlements and
    /// valid vanilla settlement targets.
    /// <para>The road system is surface-only (see <c>FCRoadQueue</c>'s surface invariant);
    /// non-surface or invalid tiles returned here are filtered out by
    /// <see cref="RoadNodeProviderRegistry.CollectInto"/>, so implementations may yield
    /// freely without enforcing the invariant themselves.</para>
    /// Register implementations via <see cref="EmpireRegistry"/>.
    /// </summary>
    public interface IRoadNodeProvider
    {
        /// <summary>Tiles to add as road-network nodes. Called on the main thread during
        /// road queue recalculation.</summary>
        IEnumerable<PlanetTile> GetRoadNodeTiles();
    }
}
