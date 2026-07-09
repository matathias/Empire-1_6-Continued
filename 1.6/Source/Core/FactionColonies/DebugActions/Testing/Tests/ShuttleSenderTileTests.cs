using FactionColonies.util;
using RimWorld.Planet;
using Verse;

namespace FactionColonies
{
    /* Tests that ShuttleSender stores its origin tile as a PlanetTile, keeping the planet layer
       intact. It previously stored the tile as a raw int, which collapsed any orbit-layer origin
       (shuttle port / caravan on an orbit tile) to the surface layer — misplacing the world range
       ring, the cross-layer range check, and the in-flight shuttle's origin.

       The orbit-layer assertions need a live world with an orbit layer (Odyssey); they Skip on
       surface-only worlds. Category "ShuttleSender". */
    public static class ShuttleSenderTileTests
    {
        // Exposes the protected origin tile for assertions.
        private class ProbeShuttleSender : ShuttleSender
        {
            public ProbeShuttleSender(PlanetTile tile) : base(tile, null) { }
            public PlanetTile TileForTest => Tile;
        }

        [EmpireTest("ShuttleSender")]
        public static void ShuttleSender_PreservesOrbitLayer()
        {
            if (!ModsConfig.OdysseyActive) TestAssert.Skip("Odyssey inactive: no orbit layer");
            WorldGrid grid = Find.WorldGrid;
            if (grid is null) TestAssert.Skip("No active world");
            PlanetLayer orbit = grid.Orbit;
            if (orbit is null || orbit.IsRootSurface) TestAssert.Skip("World has no non-surface orbit layer");

            PlanetTile orbitTile = new PlanetTile(0, orbit);
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
            PlanetTile surfaceTile = new PlanetTile(42);
            var sender = new ProbeShuttleSender(surfaceTile);

            TestAssert.IsTrue(sender.TileForTest == surfaceTile, "surface origin must round-trip");
            TestAssert.AreEqual(42, sender.TileForTest.tileId, "tile id must be preserved");
        }
    }
}
