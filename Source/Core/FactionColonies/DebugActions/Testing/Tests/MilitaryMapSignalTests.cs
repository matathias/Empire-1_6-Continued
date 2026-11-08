using Verse.AI.Group;

namespace FactionColonies;

/* Map-scoped military behavior that needs a live map but changes nothing on it: the assault signal
   that wakes defense lords, and when an active fire-support strike ends. The lord and the strikes
   are built here and never registered, so nothing outlives the test. */
public static class MilitaryMapSignalTests
{
    private static Map RequireMap() =>
        TestAssert.NotNull(Find.AnyPlayerHomeMap ?? Find.CurrentMap ?? Find.Maps.FirstOrDefault(), "No map in a loaded game");

    private static TriggerSignal SignalOf(string tag) => TriggerSignal.ForSignal(new Signal(tag));

    /* Trigger_MapAssaultSignal */

    [EmpireTest("Military")]
    public static void AssaultSignal_FiresOnlyForTheLordsOwnMap()
    {
        Map map = RequireMap();
        Lord lord = new() { lordManager = map.lordManager };
        Trigger_MapAssaultSignal trigger = new();

        TestAssert.IsTrue(trigger.ActivateOn(lord, SignalOf(LordJob_DefendColony.AssaultSignalTag(map))),
            "An assault on the lord's own map should wake it");
        TestAssert.IsFalse(trigger.ActivateOn(lord, SignalOf($"startAssault_{map.uniqueID + 1}")),
            "An assault on another map must not wake it");
        TestAssert.IsFalse(trigger.ActivateOn(lord, SignalOf("startAssault")),
            "The old unscoped signal must not wake it");
        TestAssert.IsFalse(trigger.ActivateOn(lord, TriggerSignal.ForTick), "A tick is not a signal");
    }

    [EmpireTest("Military")]
    public static void AssaultSignalTag_DiffersPerMap()
    {
        Map map = RequireMap();
        string tag = LordJob_DefendColony.AssaultSignalTag(map);
        TestAssert.IsTrue(tag.EndsWith(map.uniqueID.ToString()), $"The tag '{tag}' should name the map");
        foreach (Map other in Find.Maps)
        {
            if (other == map) continue;
            TestAssert.IsFalse(LordJob_DefendColony.AssaultSignalTag(other) == tag, "Two maps share an assault tag");
        }
    }

    /* MilitaryFireSupport */

    [EmpireTest("Military")]
    public static void FireSupport_EndsWhenItsMapIsGone()
    {
        Map map = RequireMap();
        MilitaryFireSupport live = new() { map = map, ticksTillEnd = Find.TickManager.TicksGame + 100_000 };
        TestAssert.IsFalse(live.ShouldBeOver, "A strike on a live map with time left should keep firing");

        MilitaryFireSupport noMap = new() { map = null, ticksTillEnd = Find.TickManager.TicksGame + 100_000 };
        TestAssert.IsTrue(noMap.ShouldBeOver, "A strike whose map is gone should end");

        MilitaryFireSupport expired = new() { map = map, ticksTillEnd = Find.TickManager.TicksGame - 1 };
        TestAssert.IsTrue(expired.ShouldBeOver, "A strike past its end tick should end");
    }

    [EmpireTest("Military")]
    public static void FireSupport_ComesInFromTheSettlementSide()
    {
        Map map = RequireMap();
        TestAssert.IsTrue(map.Tile.Valid, "The map has no world tile");
        PlanetTile source = new(map.Tile.tileId == 0 ? 1 : 0, map.Tile.Layer);
        Rot4 expected = Find.WorldGrid.GetRotFromTo(map.Tile, source);

        MilitaryFireSupport strike = new("fireSupport", map, map.Center, 100, 600, 15f, [], source);
        IntVec3 cell = strike.sourceLocation;
        bool onExpectedEdge = expected == Rot4.North ? cell.z == map.Size.z - 1
            : expected == Rot4.South ? cell.z == 0
            : expected == Rot4.West ? cell.x == 0
            : cell.x == map.Size.x - 1;
        TestAssert.IsTrue(onExpectedEdge, $"A strike from the {expected} should start on that edge, not at {cell}");
    }

    [EmpireTest("Military")]
    public static void FireSupport_NoOrInvalidSourceTile_UsesAnyEdge()
    {
        Map map = RequireMap();
        foreach (PlanetTile? source in new PlanetTile?[] { null, PlanetTile.Invalid })
        {
            MilitaryFireSupport strike = null;
            TestAssert.DoesNotThrow(() => strike = new MilitaryFireSupport("fireSupport", map, map.Center, 100, 600, 15f, [], source),
                $"Building a strike threw for source {source?.ToString() ?? "null"}");
            IntVec3 cell = strike.sourceLocation;
            TestAssert.IsTrue(cell.InBounds(map) && cell.OnEdge(map), $"The strike should start on a map edge, not at {cell}");
        }
    }
}
