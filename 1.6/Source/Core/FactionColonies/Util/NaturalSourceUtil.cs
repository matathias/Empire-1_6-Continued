using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Maps mineable rock products (stone chunks and blocks) back to the natural rock they come
    /// from, and answers whether a given tile can naturally supply that product.
    ///
    /// Used by <see cref="ResourceFilterExtension_NaturalSource"/> to gate a settlement's mining
    /// tithe to the rock types its tile actually has. Only *non-resource natural rocks* (granite,
    /// sandstone, limestone, marble, slate, modded stones) flow through here - these are what
    /// <see cref="World.NaturalRockTypesIn"/> reports per tile. Resource-rock veins such as obsidian
    /// are NOT natural rocks (isResourceRock=true), are never returned by NaturalRockTypesIn, and are
    /// gated separately by biome in the extension.
    /// </summary>
    public static class NaturalSourceUtil
    {
        private static readonly List<ThingDef> EmptyRocks = new List<ThingDef>();

        /* product ThingDef (chunk or stone block) -> the natural rock(s) it derives from.
           Built lazily once from DefDatabase and never invalidated (rock/chunk defs are static
           after load). Chain: rock.building.mineableThing = chunk, chunk.butcherProducts = blocks. */
        private static Dictionary<ThingDef, List<ThingDef>> _cachedProductToRocks;

        private static Dictionary<ThingDef, List<ThingDef>> ProductToRocks
        {
            get
            {
                if (_cachedProductToRocks is null)
                {
                    _cachedProductToRocks = BuildProductToRocks();
                }
                return _cachedProductToRocks;
            }
        }

        private static Dictionary<ThingDef, List<ThingDef>> BuildProductToRocks()
        {
            Dictionary<ThingDef, List<ThingDef>> map = new Dictionary<ThingDef, List<ThingDef>>();
            foreach (ThingDef rock in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (!rock.IsNonResourceNaturalRock) continue;

                ThingDef chunk = rock.building?.mineableThing;
                if (chunk is null) continue;

                // The chunk itself is rock-sourced...
                AddSource(map, chunk, rock);

                // ...and every block cut from it (chunk.butcherProducts, e.g. ChunkGranite -> BlocksGranite).
                if (chunk.butcherProducts is object)
                {
                    foreach (ThingDefCountClass product in chunk.butcherProducts)
                    {
                        if (product?.thingDef is object)
                        {
                            AddSource(map, product.thingDef, rock);
                        }
                    }
                }
            }
            return map;
        }

        private static void AddSource(Dictionary<ThingDef, List<ThingDef>> map, ThingDef product, ThingDef rock)
        {
            List<ThingDef> rocks;
            if (!map.TryGetValue(product, out rocks))
            {
                rocks = new List<ThingDef>();
                map[product] = rocks;
            }
            if (!rocks.Contains(rock)) rocks.Add(rock);
        }

        /// <summary>True if this thing is produced by mining a natural rock (a stone chunk or block).</summary>
        public static bool IsRockSourced(ThingDef thing)
        {
            return thing is object && ProductToRocks.ContainsKey(thing);
        }

        /// <summary>The natural rock(s) this thing derives from, or an empty list if it is not rock-sourced.</summary>
        public static IReadOnlyList<ThingDef> GetSourceRocks(ThingDef thing)
        {
            List<ThingDef> rocks;
            if (thing is object && ProductToRocks.TryGetValue(thing, out rocks)) return rocks;
            return EmptyRocks;
        }

        /// <summary>
        /// True if the thing's source rock occurs naturally on the given tile (per
        /// <see cref="World.NaturalRockTypesIn"/>). Non-rock-sourced things return true (nothing to gate).
        /// Fail-open when the world or tile is unavailable.
        /// </summary>
        public static bool IsNaturallyAvailable(ThingDef thing, PlanetTile tile)
        {
            IReadOnlyList<ThingDef> sourceRocks = GetSourceRocks(thing);
            if (sourceRocks.Count == 0) return true;
            if (Find.World is null || !tile.Valid) return true;

            HashSet<ThingDef> tileRocks = new HashSet<ThingDef>(Find.World.NaturalRockTypesIn(tile));
            for (int i = 0; i < sourceRocks.Count; i++)
            {
                if (tileRocks.Contains(sourceRocks[i])) return true;
            }
            return false;
        }
    }
}
