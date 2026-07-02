using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Gates a settlement's tithe list to what its tile can naturally supply.
    ///
    /// Two independent checks, both hard blocks (this extension only ever removes things, and runs
    /// after the thing/category allow-block lists):
    ///   - <see cref="checkRockTypes"/>: stone chunks and blocks are blocked unless their source rock
    ///     occurs naturally on the tile (via <see cref="NaturalSourceUtil"/> / World.NaturalRockTypesIn).
    ///   - <see cref="biomeAllowList"/>: each listed thing is allowed only in the listed biomes. This
    ///     handles resource-rock veins such as obsidian, which are never reported by NaturalRockTypesIn
    ///     and so cannot be gated by rock type - obsidian only forms in the LavaField biome.
    ///
    /// On the buy/trade side the resource is null (no settlement context), so nothing is gated.
    /// </summary>
    public class ResourceFilterExtension_NaturalSource : ResourceFilterExtension
    {
        /// <summary>If true, block stone products whose source rock is not present on the settlement's tile.</summary>
        public bool checkRockTypes = true;

        /// <summary>Per-thing biome gate: each entry's thing is allowed only in the listed biomes.</summary>
        public List<BiomeThingRestriction> biomeAllowList = new List<BiomeThingRestriction>();

        /// <summary>Things always allowed regardless of tile (unconditional exception, applied last).</summary>
        public List<ThingDef> forceAllow = new List<ThingDef>();

        /// <summary>Things always blocked regardless of tile (unconditional exception, applied last).</summary>
        public List<ThingDef> forceBlock = new List<ThingDef>();

        public override void SetFilter(ThingFilter filter, TechLevel techlevel, ResourceFC resource = null)
        {
            // Buy/trade side passes a null resource - no settlement, no geography to gate against.
            if (resource is null) return;
            WorldSettlementFC settlement = resource.settlement;
            if (settlement is null) return;
            PlanetTile tile = settlement.Tile;
            if (!tile.Valid) return;

            if (checkRockTypes)
            {
                GateRockProducts(filter, tile);
            }

            if (biomeAllowList is object && biomeAllowList.Count > 0)
            {
                BiomeDef biome = tile.Tile.PrimaryBiome;
                foreach (BiomeThingRestriction entry in biomeAllowList)
                {
                    if (entry?.thing is null) continue;
                    if (entry.biomes is null || !entry.biomes.Contains(biome))
                    {
                        filter.SetAllow(entry.thing, false);
                    }
                }
            }

            // Unconditional exceptions last, so they win over the checks above.
            if (forceBlock is object)
            {
                foreach (ThingDef t in forceBlock)
                {
                    if (t is object) filter.SetAllow(t, false);
                }
            }
            if (forceAllow is object)
            {
                foreach (ThingDef t in forceAllow)
                {
                    if (t is object) filter.SetAllow(t, true);
                }
            }
        }

        private static void GateRockProducts(ThingFilter filter, PlanetTile tile)
        {
            // Snapshot before mutating - SetAllow modifies the filter's allowed set we'd be iterating.
            List<ThingDef> allowed = filter.AllowedThingDefs.ToList();
            HashSet<ThingDef> tileRocks = null;

            foreach (ThingDef t in allowed)
            {
                if (!NaturalSourceUtil.IsRockSourced(t)) continue;

                if (tileRocks is null)
                {
                    if (Find.World is null) return; // fail-open: can't determine, allow all
                    tileRocks = new HashSet<ThingDef>(Find.World.NaturalRockTypesIn(tile));
                }

                bool available = false;
                foreach (ThingDef rock in NaturalSourceUtil.GetSourceRocks(t))
                {
                    if (tileRocks.Contains(rock))
                    {
                        available = true;
                        break;
                    }
                }
                if (!available) filter.SetAllow(t, false);
            }
        }
    }

    /// <summary>An XML entry pairing a thing with the biomes it is allowed in (see biomeAllowList).</summary>
    public class BiomeThingRestriction
    {
        public ThingDef thing;
        public List<BiomeDef> biomes = new List<BiomeDef>();
    }
}
