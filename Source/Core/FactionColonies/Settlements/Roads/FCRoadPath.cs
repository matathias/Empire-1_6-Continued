namespace FactionColonies;

/* Surface-only invariant: see FCRoadQueue. Tile IDs stored as int here
   are safe because the road system never handles non-surface tiles. */
public class FCRoadPath : IExposable
{
    public WorldPath Path { get; protected set; }
    public int From { get; protected set; }
    public int To { get; protected set; }
    public RoadDef builtRoadDef;

    List<int> nodeIds;
    int forwardIndex;
    int backwardIndex;

    /// <summary>
    /// Parameterless constructor required for Scribe deserialization.
    /// </summary>
    public FCRoadPath() { }

    public FCRoadPath(int from, int to)
    {
        if (from == to)
        {
            LogUtil.Error("Attempted to create road path to the same tile");
        }
        this.SetupPath(from, to);
    }

    void SetupPath(int from, int to)
    {
        this.From = from;
        this.To = to;

        var mainPlanetLayer = Find.WorldGrid.PlanetLayers[0];
        var fromTile = new PlanetTile(from, mainPlanetLayer);
        var toTile = new PlanetTile(to, mainPlanetLayer);
        WorldPath path;
        using (var pathing = new WorldPathing(mainPlanetLayer))
        {
            path = pathing.FindPath(fromTile, toTile, null);
        }

        // path belongs to a WorldPathPool that gets very vocal in the error log
        // when theres more WorldPaths than caravans. The workaround to this error
        // is to copy the path to a new WorldPath object that is not a part of
        // the pool and Dispose of the one that is
        this.Path = new WorldPath();
        foreach (int node in path.NodesReversed)
        {
            this.Path.AddNodeAtStart(node);
        }
        this.Path.SetupFound(path.TotalCost, mainPlanetLayer);
        this.Path.inUse = true;
        path.Dispose();

        this.nodeIds = this.Path.NodesReversed.Select(t => t.tileId).ToList();
        this.forwardIndex = 0;
        this.backwardIndex = this.nodeIds.Count - 1;
    }

    public void ExposeData()
    {
        int from = this.From;
        int to = this.To;
        Scribe_Values.Look(ref from, "from");
        Scribe_Values.Look(ref to, "to");
        Scribe_Defs.Look(ref builtRoadDef, "builtRoadDef");
        Scribe_Values.Look(ref forwardIndex, "forwardIndex");
        Scribe_Values.Look(ref backwardIndex, "backwardIndex");

        List<int> savedNodeIds = nodeIds;
        float totalCost = 0f;

        if (Scribe.mode == LoadSaveMode.Saving)
        {
            totalCost = this.Path.TotalCost;
        }

        Scribe_Collections.Look(ref savedNodeIds, "pathNodes", LookMode.Value);
        Scribe_Values.Look(ref totalCost, "totalCost");

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            this.From = from;
            this.To = to;
            this.nodeIds = savedNodeIds;

            var mainPlanetLayer = Find.WorldGrid.PlanetLayers[0];
            this.Path = new WorldPath();
            foreach (int nodeId in this.nodeIds)
            {
                this.Path.AddNodeAtStart(new PlanetTile(nodeId, mainPlanetLayer));
            }
            this.Path.SetupFound(totalCost, mainPlanetLayer);
            this.Path.inUse = true;
        }
    }

    /// <summary>
    /// Builds 1 segment from each end of the road. Returns true if any segment was built.
    /// </summary>
    public bool BuildSegment(RoadDef roadDef)
    {
        if (!this.Path.Found || this.IsCompleted)
            return false;

        bool builtForward = BuildForward(roadDef);
        bool builtBackward = BuildBackward(roadDef);
        return builtForward || builtBackward;
    }

    bool BuildForward(RoadDef roadDef) => BuildStep(ref forwardIndex, 1, roadDef);

    bool BuildBackward(RoadDef roadDef) => BuildStep(ref backwardIndex, -1, roadDef);

    /// <summary>
    /// Walks one end of the road toward the other (step +1 from the start, -1 from the end),
    /// skipping segments that already carry an equal or better road, and overlays the first
    /// segment that does not. Returns true if a segment was built.
    /// </summary>
    bool BuildStep(ref int index, int step, RoadDef roadDef)
    {
        WorldGrid grid = Find.WorldGrid;
        while (forwardIndex < backwardIndex)
        {
            int from = nodeIds[index];
            int to = nodeIds[index + step];
            index += step;

            RoadDef existingRoad = grid.GetRoadDef(from, to);
            if (IsNewRoadBetter(existingRoad, roadDef))
            {
                grid.OverlayRoad(from, to, roadDef);
                Find.WorldPathGrid.RecalculatePerceivedMovementDifficultyAt(from, out _);
                Find.WorldPathGrid.RecalculatePerceivedMovementDifficultyAt(to, out _);
                return true;
            }
        }
        return false;
    }

    public bool IsCompleted => forwardIndex >= backwardIndex;

    public static bool IsNewRoadBetter(RoadDef oldRoad, RoadDef newRoad)
    {
        if (newRoad is null)
            return false;

        if (oldRoad is null)
            return true;

        return newRoad.priority > oldRoad.priority;
    }

    public void DrawPath()
    {
        this.Path.DrawPath(null);
    }

    public void ResetProgress()
    {
        this.forwardIndex = 0;
        this.backwardIndex = this.nodeIds.Count - 1;
    }
}
