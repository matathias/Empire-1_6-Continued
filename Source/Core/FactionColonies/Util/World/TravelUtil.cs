using System.Collections.Generic;
using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FactionColonies
{
    public static class TravelUtil
    {
        public const int timespanFallback = GenDate.TicksPerDay * 10;

        public static int ReturnTicksToArrive(PlanetTile currentTile, PlanetTile destinationTile)
        {
            return ReturnTicksToArrive(currentTile, destinationTile, out _);
        }

        /// <summary>
        /// As <see cref="ReturnTicksToArrive(PlanetTile,PlanetTile)"/>, but also outputs the ordered
        /// overland tile path (source -> destination) when travel is a caravan-overland journey.
        /// <paramref name="pathTiles"/> is null for straight-line travel (transport pods, shuttle range,
        /// cross-layer) and for fallbacks, meaning "no road path to follow". The path is copied out of the
        /// pooled <see cref="WorldPath"/> before it is disposed, so the caller owns the returned list.
        /// </summary>
        public static int ReturnTicksToArrive(PlanetTile currentTile, PlanetTile destinationTile, out List<PlanetTile> pathTiles)
        {
            pathTiles = null;
            LogUtil.Message($"ReturnTicksToArrive Debug: currentTile={currentTile}, destinationTile={destinationTile}");

            // Cross-layer (e.g. surface <-> orbital): caravan pathing is impossible.
            if (currentTile.Layer != destinationTile.Layer)
            {
                bool podsAvailable = FactionCache.TechTransportPods?.IsFinished ?? false;
                if (podsAvailable)
                {
                    int multiplier = (currentTile, destinationTile).AreTilesInAnyShuttleRange() ? 5 : 10;
                    return Find.WorldGrid.TraversalDistanceBetween(currentTile, destinationTile, canTraverseLayers: true) * multiplier;
                }
                return timespanFallback; //10-day fallback
            }

            bool tilesInShuttleRange = (currentTile, destinationTile).AreTilesInAnyShuttleRange();
            bool medievalOnly = FCSettings.medievalTechOnly;
            bool podsResearched = FactionCache.TechTransportPods?.IsFinished ?? false;

            if (!medievalOnly)
            {
                bool tilesValid = (currentTile, destinationTile).AreValidTiles();
                LogUtil.Message($"ReturnTicksToArrive Debug: tilesValid={tilesValid}, medievalOnly={medievalOnly}, podsResearched={podsResearched}");

                if (!tilesValid)
                {
                    int fallbackTime = podsResearched ? 30000 : timespanFallback;
                    LogUtil.Message($"ReturnTicksToArrive Debug: Invalid tiles, returning fallback time: {fallbackTime} ticks ({fallbackTime / 60000f:F1} days)");
                    return fallbackTime;
                }
                if (podsResearched)
                {
                    int multiplier = tilesInShuttleRange ? 5 : 10;
                    return Find.WorldGrid.TraversalDistanceBetween(currentTile, destinationTile) * multiplier;
                }
            }

            // Both tiles are on the same layer here (cross-layer returns above).
            // Use that layer's pathing rather than hardcoding surface.
            var layer = currentTile.Layer;
            using (var pathing = new WorldPathing(layer))
            {
                using (WorldPath tempPath = pathing.FindPath(currentTile, destinationTile, null))
                {
                    if (tempPath == WorldPath.NotFound) return timespanFallback;

                    // Copy the pooled path out before it is disposed. NodesReversed runs destination -> source
                    // (last node is the start), so reverse it into forward (source -> destination) order.
                    List<PlanetTile> reversed = tempPath.NodesReversed;
                    var forward = new List<PlanetTile>(reversed.Count);
                    for (int i = reversed.Count - 1; i >= 0; i--)
                    {
                        forward.Add(reversed[i]);
                    }
                    pathTiles = forward;

                    return CaravanArrivalTimeEstimator.EstimatedTicksToArrive(currentTile, destinationTile, tempPath, 0f, CaravanTicksPerMoveUtility.GetTicksPerMove(null), Find.TickManager.TicksAbs);
                }
            }
        }

    }
}
