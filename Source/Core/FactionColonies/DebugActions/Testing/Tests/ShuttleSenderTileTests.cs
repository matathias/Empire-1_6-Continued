using UnityEngine;

namespace FactionColonies;

/* Tests that ShuttleSender stores its origin tile as a PlanetTile, keeping the planet layer
   intact. It previously stored the tile as a raw int, which collapsed any orbit-layer origin
   (shuttle port / caravan on an orbit tile) to the surface layer — misplacing the world range
   ring, the cross-layer range check, and the in-flight shuttle's origin.

   The orbit-layer tests need Odyssey, which gives every world an orbit layer; without it they are
   not part of the suite. Category "ShuttleSender". */
public static class ShuttleSenderTileTests
{
    // Exposes the protected origin tile for assertions, and bypasses the destination-map
    // requirement so ChoseWorldTarget's range branch can be exercised without a real target map.
    private class ProbeShuttleSender : ShuttleSender
    {
        public ProbeShuttleSender(PlanetTile tile) : base(tile, null) { }
        public PlanetTile TileForTest => Tile;
        protected override bool TargetHasValidWorldObject(GlobalTargetInfo target) => true;
    }

    // Odyssey adds an orbit layer to every scenario, so an Odyssey world without one is broken.
    private static PlanetLayer OdysseyOrbit(WorldGrid grid)
    {
        PlanetLayer orbit = TestAssert.NotNull(grid.Orbit, "Odyssey is active but the world has no orbit layer");
        TestAssert.IsFalse(orbit.IsRootSurface, "The orbit layer must not be the root surface");
        return orbit;
    }

    [EmpireTest("ShuttleSender", RequiresPackageId = ModContentPack.OdysseyModPackageId)]
    public static void ShuttleSender_PreservesOrbitLayer()
    {
        WorldGrid grid = Find.WorldGrid;
        PlanetLayer orbit = OdysseyOrbit(grid);

        PlanetTile orbitTile = new(0, orbit);
        var sender = new ProbeShuttleSender(orbitTile);

        TestAssert.IsFalse(sender.TileForTest.Layer.IsRootSurface,
            "the orbit layer must survive construction (not collapse to surface)");
        TestAssert.IsTrue(sender.TileForTest == orbitTile,
            "the stored tile must round-trip the orbit origin");
        // The surface tile with the same id is where the old int field would have collapsed to;
        // it must compare unequal, proving the layer is not silently dropped.
        TestAssert.IsFalse(sender.TileForTest == new PlanetTile(0),
            "an orbit origin must not equal the surface tile it would collapse to");
    }

    [EmpireTest("ShuttleSender")]
    public static void ShuttleSender_PreservesSurfaceTileIdentity()
    {
        // Layer-agnostic sanity check that always runs: a surface origin round-trips exactly.
        PlanetTile surfaceTile = new(42);
        var sender = new ProbeShuttleSender(surfaceTile);

        TestAssert.IsTrue(sender.TileForTest == surfaceTile, "surface origin must round-trip");
        TestAssert.AreEqual(42, sender.TileForTest.tileId, "tile id must be preserved");
    }

    [EmpireTest("ShuttleSender", RequiresPackageId = ModContentPack.OdysseyModPackageId)]
    public static void ShuttleSender_AcceptsCrossLayerTarget()
    {
        WorldGrid grid = Find.WorldGrid;
        PlanetLayer orbit = OdysseyOrbit(grid);

        // An orbital shuttle port targeting the surface tile directly beneath it: the tiles are on
        // different layers, so without canTraverseLayers the distance is int.MaxValue and the target
        // is wrongly rejected. The surface projection sits at distance ~0, well within range.
        PlanetTile orbitTile = new(0, orbit);
        var sender = new ProbeShuttleSender(orbitTile);
        PlanetTile surfaceTarget = grid.Surface.GetClosestTile_NewTemp(orbitTile);

        TestAssert.IsTrue(sender.ChoseWorldTarget(new GlobalTargetInfo(surfaceTarget)),
            "an orbital shuttle port must accept an in-range surface target across the layer boundary");
    }

    [EmpireTest("ShuttleSender")]
    public static void ShuttleSender_EffectiveRangeScalesByLayer()
    {
        WorldGrid grid = Find.WorldGrid;

        // Surface has rangeDistanceFactor 1, so the range is unchanged from the flat constant.
        TestAssert.AreEqual(ShuttleSender.ShuttleRange, ShuttleSender.EffectiveRange(grid.Surface),
            "surface range must be unscaled");

        // Without Odyssey the surface is the only layer, so the surface assertion above is the whole check.
        if (ModsConfig.OdysseyActive)
        {
            // Orbit's larger rangeDistanceFactor must shrink the range (avoiding the whole-layer flood
            // that produced the broken ring mesh).
            PlanetLayer orbit = OdysseyOrbit(grid);
            TestAssert.AreEqual(Mathf.RoundToInt(ShuttleSender.ShuttleRange / orbit.Def.rangeDistanceFactor),
                ShuttleSender.EffectiveRange(orbit),
                "orbit range must be scaled down by the orbit layer's rangeDistanceFactor");
        }
    }
}
