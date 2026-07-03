using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// A conquered NPC-settlement tile the player chose to "capture" rather than raze, awaiting
    /// conversion into an Empire settlement. Conversion is deferred until the player leaves the
    /// looted base map (see the map-removal hook in SettlementCapturePatch), so this record has to
    /// survive save/load while the player is still on the map. Holds the snapshot taken at defeat —
    /// the source Settlement no longer exists by conversion time.
    /// </summary>
    public class PendingSettlementCapture : IExposable
    {
        public PlanetTile tile;
        public string name;
        public TechLevel tech;

        public PendingSettlementCapture()
        {
        }

        public PendingSettlementCapture(PlanetTile tile, string name, TechLevel tech)
        {
            this.tile = tile;
            this.name = name;
            this.tech = tech;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref tile, "tile", PlanetTile.Invalid);
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref tech, "tech");
        }
    }
}
