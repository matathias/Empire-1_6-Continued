using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Verse;

namespace FactionColonies
{
    /* Surface-only invariant: the road system operates exclusively on the
       root surface layer — see UpdateSettlementsToProcess, which filters
       orbital settlements out before enqueueing. That guarantee is the
       reason this class and its helpers (FCRoadPath, FCRoadBuilder) can
       safely store tiles as bare ints without risking layer aliasing.
       Revisit (migrate the int collections to PlanetTile) if orbital
       settlements ever need to participate in road building. */
    public class FCRoadQueue : IExposable
    {
        public int nextRoadTick;
        public int daysBetweenTicks;
        protected RoadDef roadDef;

        public bool shouldUpdateSettlementsToProcess = true;

        public int lastFromTileCount;
        public int lastToTileCount;
        IEnumerator<FCRoadPath> roadPathIterator;

        // Background MST computation state
        volatile int mstGeneration;
        volatile int completedGeneration = -1;
        List<Edge> computedMSTEdges;

        // Incremental MST computation state (not saved)
        private List<int> incrementalTiles;
        private PlanetLayer incrementalLayer;
        private WorldPathing incrementalPathing;
        private int incrementalEdgeIndex;
        private List<TileEdgeKey> incrementalEdgeKeys;
        private int incrementalGeneration;

        // KNN filtering constant
        private const int KNearestNeighbors = 8;

        // Edge cost cache (saved). Wire format on disk is Dictionary<long, float>
        // for save-compat with older versions; conversion happens in ExposeData.
        private Dictionary<TileEdgeKey, float> edgeCostCache = new Dictionary<TileEdgeKey, float>();
        private HashSet<int> previousTileSet = new HashSet<int>();
        private RoadDef cachedRoadDef;

        // Background thread result handoff (not saved)
        private Dictionary<TileEdgeKey, float> pendingNewEdges;
        private string pendingLogMessage;
        private HashSet<TileEdgeKey> currentCandidateEdges;

        public bool IsMSTReady => completedGeneration == mstGeneration;

        public RoadDef RoadDef
        {
            get
            {
                return roadDef;
            }
            set
            {
                roadDef = value;
                ResetPaths();
            }
        }

        public List<FCRoadPath> roadPaths = new List<FCRoadPath>();

        private class UnionFind
        {
            private int[] parent;
            private int[] rank;

            public UnionFind(int size)
            {
                parent = new int[size];
                rank = new int[size];
                for (int i = 0; i < size; i++)
                    parent[i] = i;
            }

            public int FindRoot(int x)
            {
                if (parent[x] != x)
                    parent[x] = FindRoot(parent[x]);
                return parent[x];
            }

            public bool TryMerge(int x, int y)
            {
                int rootX = FindRoot(x);
                int rootY = FindRoot(y);
                if (rootX == rootY)
                    return false;
                if (rank[rootX] < rank[rootY])
                    parent[rootX] = rootY;
                else if (rank[rootX] > rank[rootY])
                    parent[rootY] = rootX;
                else
                {
                    parent[rootY] = rootX;
                    rank[rootX]++;
                }
                return true;
            }
        }

        private struct Edge
        {
            public int fromTile;
            public int toTile;
            public float cost;
        }

        /// <summary>
        /// For each tile, selects up to k nearest neighbors by approximate tile distance.
        /// Returns a HashSet of canonical edge keys (deduplicates symmetric pairs).
        /// O(n²) in distance computations but ApproxDistanceInTiles is trivial vector math.
        /// </summary>
        private static HashSet<TileEdgeKey> SelectKNNCandidateEdges(List<int> allTiles, PlanetLayer layer)
        {
            int n = allTiles.Count;
            int k = Math.Min(KNearestNeighbors, n - 1);
            HashSet<TileEdgeKey> candidateKeys = new HashSet<TileEdgeKey>();
            List<KeyValuePair<int, float>> distances = new List<KeyValuePair<int, float>>(n);

            for (int i = 0; i < n; i++)
            {
                distances.Clear();
                for (int j = 0; j < n; j++)
                {
                    if (i == j) continue;
                    float dist = layer.ApproxDistanceInTiles(allTiles[i], allTiles[j]);
                    distances.Add(new KeyValuePair<int, float>(j, dist));
                }
                distances.Sort((a, b) => a.Value.CompareTo(b.Value));

                int count = Math.Min(k, distances.Count);
                for (int d = 0; d < count; d++)
                {
                    candidateKeys.Add(new TileEdgeKey(allTiles[i], allTiles[distances[d].Key]));
                }
            }

            return candidateKeys;
        }

        private void InvalidateCacheForTiles(HashSet<int> removedTiles)
        {
            List<TileEdgeKey> toRemove = new List<TileEdgeKey>();
            foreach (TileEdgeKey key in edgeCostCache.Keys)
            {
                if (removedTiles.Contains(key.lo) || removedTiles.Contains(key.hi))
                    toRemove.Add(key);
            }
            foreach (TileEdgeKey key in toRemove)
                edgeCostCache.Remove(key);
        }

        private List<Edge> BuildEdgeListFromCache(HashSet<TileEdgeKey> candidateEdges)
        {
            List<Edge> edges = new List<Edge>(candidateEdges.Count);
            foreach (TileEdgeKey key in candidateEdges)
            {
                float cost;
                if (!edgeCostCache.TryGetValue(key, out cost))
                    continue;
                edges.Add(new Edge { fromTile = key.lo, toTile = key.hi, cost = cost });
            }
            return edges;
        }

        public void FlushCache()
        {
            edgeCostCache.Clear();
            previousTileSet.Clear();
            cachedRoadDef = null;
            shouldUpdateSettlementsToProcess = true;
            LogUtil.Message("Road path cache flushed");
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref nextRoadTick, "nextRoadTick");
            Scribe_Values.Look(ref daysBetweenTicks, "daysBetweenTicks");
            Scribe_Collections.Look(ref roadPaths, "roadPaths", LookMode.Deep);
            if (roadPaths == null)
                roadPaths = new List<FCRoadPath>();

            // Edge cost cache persistence. Wire format remains Dictionary<long, float>
            // so saves from the pre-TileEdgeKey version still load. The struct provides
            // Pack/Unpack helpers for the round-trip.
            Dictionary<long, float> packedEdgeCache = null;
            if (Scribe.mode == LoadSaveMode.Saving)
                packedEdgeCache = edgeCostCache.ToDictionary(kvp => kvp.Key.Pack(), kvp => kvp.Value);
            Scribe_Collections.Look(ref packedEdgeCache, "edgeCostCache", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                edgeCostCache = packedEdgeCache is null
                    ? new Dictionary<TileEdgeKey, float>()
                    : packedEdgeCache.ToDictionary(kvp => TileEdgeKey.Unpack(kvp.Key), kvp => kvp.Value);
            }

            Scribe_Collections.Look(ref previousTileSet, "previousTilesList", LookMode.Value);
            if (previousTileSet is null)
                previousTileSet = new HashSet<int>();

            Scribe_Defs.Look(ref cachedRoadDef, "cachedRoadDef");
        }

        public FCRoadQueue(RoadDef roadDef, int daysBetweenTicks)
        {
            this.roadDef = roadDef;
            this.daysBetweenTicks = daysBetweenTicks;
        }

        /// <summary>
        /// Updates processed settlements if needed and that it is time to build the segements,
        /// then builds the segments
        /// </summary>
        public bool BuildRoadSegments()
        {
            if (this.nextRoadTick > Find.TickManager.TicksGame)
                return false;

            this.nextRoadTick += GenDate.TicksPerDay * this.daysBetweenTicks;
            return this.ForceBuildRoadSegments();
        }

        bool ForceBuildRoadSegments()
        {
            bool built = false;
            foreach (FCRoadPath path in roadPaths)
            {
                built |= path.BuildSegment(this.roadDef);
            }
            if (built)
            {
                var mainPlanetLayer = Find.WorldGrid.PlanetLayers[0];
                Find.World.renderer.SetDirty<WorldDrawLayer_Roads>(mainPlanetLayer);
                Find.World.renderer.SetDirty<WorldDrawLayer_Paths>(mainPlanetLayer);

                string roadTypeName = this.roadDef?.LabelCap ?? "Road";
                Messages.Message(
                    "FCRoadSegmentsBuilt".Translate(FindFC.EmpireName, roadTypeName),
                    MessageTypeDefOf.PositiveEvent);
            }
            return built;
        }

        public void DrawPaths()
        {
            foreach (FCRoadPath path in roadPaths)
            {
                path.DrawPath();
            }
        }

        /// <summary>
        /// Runs on a background thread. Computes only the uncached edges, merges
        /// with the cache snapshot, and builds the MST using Kruskal's algorithm.
        /// Results are stored for the main thread to pick up via ProcessOnePath.
        ///
        /// Thread-safety notes:
        /// - WorldPathing.FindPath reads Find.World/WorldGrid/WorldReachability and
        ///   PlanetLayer NativeArrays. These are read-only during normal gameplay so
        ///   concurrent access is safe in practice. During game teardown (exit to menu)
        ///   these can be invalidated; we guard against that with a Current.Game null
        ///   check each iteration and catch any residual exceptions.
        /// - WorldPathPool access is synchronized via Harmony patches in
        ///   WorldPathPoolPatches.cs (Monitor lock around Get/Release). The pool's
        ///   internal leak detection may fire an ErrorOnce log due to the background
        ///   thread's borrowed paths inflating the count — this is harmless.
        /// - edgeCostCache is NOT accessed from this thread. The thread receives a
        ///   snapshot copy and stores new edges in pendingNewEdges for the main
        ///   thread to merge.
        /// </summary>
        void ComputeMSTBackground(List<int> allTiles, PlanetLayer layer, int generation,
            HashSet<TileEdgeKey> edgesToCompute, Dictionary<TileEdgeKey, float> cacheSnapshot)
        {
            try
            {
                // Compute only the missing edges
                Dictionary<TileEdgeKey, float> newEdges = new Dictionary<TileEdgeKey, float>(edgesToCompute.Count);
                using (var pathing = new WorldPathing(layer))
                {
                    foreach (TileEdgeKey key in edgesToCompute)
                    {
                        // Bail early if superseded or the game is being torn down
                        if (generation != mstGeneration || Current.Game is null)
                            return;

                        var fromTile = new PlanetTile(key.lo, layer);
                        var toTile = new PlanetTile(key.hi, layer);
                        WorldPath path = pathing.FindPath(fromTile, toTile, null);
                        float cost = path.Found ? path.TotalCost : float.MaxValue;
                        path.Dispose();
                        newEdges[key] = cost;
                    }
                }

                // Bail if superseded or game torn down
                if (generation != mstGeneration || Current.Game is null)
                    return;

                // Merge cached + newly computed edges for Kruskal
                List<Edge> edges = new List<Edge>(cacheSnapshot.Count + newEdges.Count);
                foreach (var kvp in cacheSnapshot)
                {
                    edges.Add(new Edge { fromTile = kvp.Key.lo, toTile = kvp.Key.hi, cost = kvp.Value });
                }
                foreach (var kvp in newEdges)
                {
                    edges.Add(new Edge { fromTile = kvp.Key.lo, toTile = kvp.Key.hi, cost = kvp.Value });
                }

                List<Edge> mstEdges = RunKruskal(edges, allTiles);

                // Publish results only if still the current generation
                if (generation == mstGeneration)
                {
                    pendingNewEdges = newEdges;
                    pendingLogMessage = $"Road MST computed on background thread: {edges.Count} edges ({newEdges.Count} new, {cacheSnapshot.Count} cached), {mstEdges.Count} MST edges";
                    computedMSTEdges = mstEdges;
                    completedGeneration = generation;
                }
            }
            catch (Exception e)
            {
                pendingLogMessage = $"Road MST background computation failed: {e}";
                computedMSTEdges = new List<Edge>();
                completedGeneration = generation;
            }
        }

        /// <summary>
        /// Runs on the main thread. Computes only the uncached edges and builds
        /// the MST. Used as a fallback when threaded computation is disabled
        /// in settings and edgesPerRoadTick is 0 (unlimited).
        /// </summary>
        void ComputeMSTSynchronous(List<int> allTiles, PlanetLayer layer, HashSet<TileEdgeKey> edgesToCompute)
        {
            using (var pathing = new WorldPathing(layer))
            {
                foreach (TileEdgeKey key in edgesToCompute)
                {
                    var fromTile = new PlanetTile(key.lo, layer);
                    var toTile = new PlanetTile(key.hi, layer);
                    WorldPath path = pathing.FindPath(fromTile, toTile, null);
                    float cost = path.Found ? path.TotalCost : float.MaxValue;
                    path.Dispose();
                    edgeCostCache[key] = cost;
                }
            }

            List<Edge> edges = BuildEdgeListFromCache(currentCandidateEdges);
            FinishMSTFromEdges(edges, allTiles, mstGeneration, "synchronously");
        }

        /// <summary>
        /// Core Kruskal's algorithm: sorts edges by cost and builds the MST
        /// using union-find. Returns the MST edge list.
        /// </summary>
        static List<Edge> RunKruskal(List<Edge> edges, List<int> allTiles)
        {
            int n = allTiles.Count;
            Dictionary<int, int> tileToIndex = new Dictionary<int, int>(n);
            for (int i = 0; i < n; i++)
                tileToIndex[allTiles[i]] = i;

            edges.Sort((a, b) => a.cost.CompareTo(b.cost));
            UnionFind uf = new UnionFind(n);
            List<Edge> mstEdges = new List<Edge>(n - 1);

            foreach (Edge edge in edges)
            {
                if (edge.cost >= float.MaxValue)
                    break;

                int idxA = tileToIndex[edge.fromTile];
                int idxB = tileToIndex[edge.toTile];

                if (uf.TryMerge(idxA, idxB))
                {
                    mstEdges.Add(edge);
                    if (mstEdges.Count == n - 1)
                        break;
                }
            }

            return mstEdges;
        }

        /// <summary>
        /// Shared finish phase: runs Kruskal, publishes results, logs.
        /// Used by both synchronous and incremental paths.
        /// </summary>
        void FinishMSTFromEdges(List<Edge> edges, List<int> allTiles, int generation, string label)
        {
            List<Edge> mstEdges = RunKruskal(edges, allTiles);
            computedMSTEdges = mstEdges;
            completedGeneration = generation;
            LogUtil.Message($"Road MST computed {label}: {edges.Count} edges, {mstEdges.Count} MST edges");
        }

        /// <summary>
        /// Initializes incremental MST computation state. The actual edge computation
        /// is spread across ticks via AdvanceMSTIncremental.
        /// </summary>
        void StartMSTIncremental(List<int> allTiles, PlanetLayer layer, int generation, HashSet<TileEdgeKey> edgesToCompute)
        {
            CleanupIncremental();

            incrementalTiles = allTiles;
            incrementalLayer = layer;
            incrementalPathing = new WorldPathing(layer);
            incrementalEdgeKeys = new List<TileEdgeKey>(edgesToCompute);
            incrementalEdgeIndex = 0;
            incrementalGeneration = generation;
        }

        /// <summary>
        /// Advances incremental MST edge computation by up to edgesPerTick A* calls.
        /// Returns true if still computing, false if done or nothing to do.
        /// </summary>
        public bool AdvanceMSTIncremental(int edgesPerTick)
        {
            if (incrementalEdgeKeys is null)
                return false;

            // Stale — a new generation was requested
            if (incrementalGeneration != mstGeneration)
            {
                CleanupIncremental();
                return false;
            }

            int computed = 0;
            while (incrementalEdgeIndex < incrementalEdgeKeys.Count && computed < edgesPerTick)
            {
                TileEdgeKey key = incrementalEdgeKeys[incrementalEdgeIndex];
                var fromTile = new PlanetTile(key.lo, incrementalLayer);
                var toTile = new PlanetTile(key.hi, incrementalLayer);
                WorldPath path = incrementalPathing.FindPath(fromTile, toTile, null);
                float cost = path.Found ? path.TotalCost : float.MaxValue;
                path.Dispose();
                edgeCostCache[key] = cost;
                computed++;
                incrementalEdgeIndex++;
            }

            // All edges computed — build from cache and run Kruskal
            if (incrementalEdgeIndex >= incrementalEdgeKeys.Count)
            {
                List<Edge> edges = BuildEdgeListFromCache(currentCandidateEdges);
                FinishMSTFromEdges(edges, incrementalTiles, incrementalGeneration, "incrementally");
                CleanupIncremental();
                return false;
            }

            return true;
        }

        void CleanupIncremental()
        {
            incrementalPathing?.Dispose();
            incrementalPathing = null;
            incrementalTiles = null;
            incrementalEdgeKeys = null;
        }

        /// <summary>
        /// Yields FCRoadPath objects from pre-computed MST edges (Phase 4 only).
        /// Called on the main thread after ComputeMSTBackground completes.
        /// </summary>
        IEnumerator<FCRoadPath> ProcessPath()
        {
            if (computedMSTEdges is null)
                yield break;

            foreach (Edge edge in computedMSTEdges)
            {
                int from = edge.fromTile;
                int to = edge.toTile;

                bool alreadyExists = this.roadPaths.Any(path =>
                    path.IsCompleted &&
                    !FCRoadPath.IsNewRoadBetter(path.builtRoadDef, this.roadDef) &&
                    ((path.From == from && path.To == to) ||
                     (path.From == to && path.To == from)));

                if (!alreadyExists)
                {
                    FCRoadPath newPath = new FCRoadPath(from, to);
                    newPath.builtRoadDef = this.roadDef;
                    yield return newPath;
                }
            }
        }

        public void UpdateSettlementsToProcess()
        {
            FactionFC fC = FindFC.FactionComp;

            // Collect empire settlement tiles
            HashSet<int> allTileSet = new HashSet<int>();
            int fromCount = 0;
            foreach (WorldSettlementFC settlement in fC.settlements)
            {
                if (!settlement.Tile.Layer.IsRootSurface)
                    continue;
                allTileSet.Add(settlement.Tile.tileId);
                fromCount++;
            }

            // Collect valid road target tiles (pass empire tiles for O(1) lookup)
            int toCount = 0;
            foreach (Settlement settlement in Find.World.worldObjects.Settlements)
            {
                if (FCRoadBuilder.IsValidRoadTarget(settlement, allTileSet))
                {
                    allTileSet.Add(settlement.Tile.tileId);
                    toCount++;
                }
            }

            // Collect submod-contributed road nodes (e.g. VOE outposts). Surface-only
            // filtering and tileId extraction happen inside CollectInto, preserving the
            // surface invariant documented above. No-op (zero cost) when no provider is
            // registered, so the road network is unchanged without contributing submods.
            RoadNodeProviderRegistry.CollectInto(allTileSet);

            lastFromTileCount = fromCount;
            lastToTileCount = toCount;

            // Phase 0: Purge incomplete paths and completed paths with inferior
            // road types so the MST can re-optimize the network when settlements
            // change or road tech upgrades.
            roadPaths.RemoveAll(p => !p.IsCompleted ||
                FCRoadPath.IsNewRoadBetter(p.builtRoadDef, this.roadDef));

            List<int> allTiles = new List<int>(allTileSet);
            if (allTiles.Count < 2)
                return;

            var layer = Find.WorldGrid.PlanetLayers[0];
            int generation = ++mstGeneration;
            roadPathIterator?.Dispose();
            roadPathIterator = null;

            // --- Cache invalidation ---
            if (roadDef != cachedRoadDef)
            {
                LogUtil.Message("Road cache fully invalidated (road tech changed)");
                edgeCostCache.Clear();
                cachedRoadDef = roadDef;
            }
            else
            {
                // Partial invalidation: remove edges touching removed tiles
                HashSet<int> removedTiles = new HashSet<int>(previousTileSet);
                removedTiles.ExceptWith(allTileSet);
                if (removedTiles.Count > 0)
                {
                    InvalidateCacheForTiles(removedTiles);
                    LogUtil.Message($"Road cache: invalidated edges for {removedTiles.Count} removed tile(s)");
                }
            }
            previousTileSet = new HashSet<int>(allTileSet);

            // --- KNN candidate selection ---
            currentCandidateEdges = SelectKNNCandidateEdges(allTiles, layer);

            // Filter to uncached edges
            HashSet<TileEdgeKey> edgesToCompute = new HashSet<TileEdgeKey>(currentCandidateEdges);
            edgesToCompute.ExceptWith(edgeCostCache.Keys);

            // Short-circuit: all candidates are cached, just run Kruskal
            if (edgesToCompute.Count == 0)
            {
                List<Edge> edges = BuildEdgeListFromCache(currentCandidateEdges);
                FinishMSTFromEdges(edges, allTiles, generation, $"from cache ({edgeCostCache.Count} cached entries)");
                return;
            }

            LogUtil.Message($"Road MST: {currentCandidateEdges.Count} candidate edges, {edgesToCompute.Count} to compute, {currentCandidateEdges.Count - edgesToCompute.Count} cached");

            if (FCSettings.useThreadedRoadComputation)
            {
                // Snapshot relevant cache entries for the background thread
                Dictionary<TileEdgeKey, float> cacheSnapshot = new Dictionary<TileEdgeKey, float>();
                foreach (TileEdgeKey key in currentCandidateEdges)
                {
                    float cost;
                    if (edgeCostCache.TryGetValue(key, out cost))
                        cacheSnapshot[key] = cost;
                }
                HashSet<TileEdgeKey> edgesToComputeCopy = new HashSet<TileEdgeKey>(edgesToCompute);
                Thread thread = new Thread(() => ComputeMSTBackground(allTiles, layer, generation, edgesToComputeCopy, cacheSnapshot));
                thread.IsBackground = true;
                thread.Start();
            }
            else if (FCSettings.edgesPerRoadTick > 0)
            {
                StartMSTIncremental(allTiles, layer, generation, edgesToCompute);
            }
            else
            {
                ComputeMSTSynchronous(allTiles, layer, edgesToCompute);
            }
        }

        /// <summary>
        /// Advances the path iterator by one step. Returns true if still working, false if exhausted.
        /// </summary>
        public bool ProcessOnePath()
        {
            // MST still computing on background thread
            if (completedGeneration != mstGeneration)
                return true;

            // Background thread just completed — merge new edges into the live cache
            if (pendingNewEdges is object)
            {
                foreach (var kvp in pendingNewEdges)
                    edgeCostCache[kvp.Key] = kvp.Value;
                pendingNewEdges = null;

                if (pendingLogMessage is object)
                {
                    LogUtil.Message(pendingLogMessage);
                    pendingLogMessage = null;
                }
            }

            // MST just completed — create iterator
            if (this.roadPathIterator is null)
                this.roadPathIterator = ProcessPath();

            if (this.roadPathIterator.MoveNext())
            {
                this.roadPaths.Add(this.roadPathIterator.Current);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Resets the paths progress. Does not recalculate the paths.
        /// </summary>
        public void ResetPaths()
        {
            foreach (FCRoadPath path in this.roadPaths)
            {
                path.ResetProgress();
            }
        }
    }
}
