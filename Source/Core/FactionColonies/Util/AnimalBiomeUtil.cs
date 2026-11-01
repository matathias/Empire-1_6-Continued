using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Biome-coverage helpers for caravan pack animals.
    ///
    /// The player faction's trade caravans are generated with carriers drawn from the player's
    /// <see cref="AnimalFilter"/> (see XenotypeFilter.RefreshPawnGroupMakers). Vanilla
    /// PawnGroupKindWorker_Trader.CanGenerateFrom then requires at least one carrier whose race is in
    /// the DESTINATION biome's allowedPackAnimals - so a selection that doesn't cover the delivery
    /// biome silently breaks trade-caravan generation ("no usable PawnGroupMakers for Trader").
    ///
    /// These helpers expose that coverage to the Animal Selection UI and let the mod warn the player
    /// (in-window and via letter) when their pack-animal selection can't reach the biome where
    /// caravans are delivered.
    /// </summary>
    public static class AnimalBiomeUtil
    {
        private static readonly BiomeDef[] EmptyBiomes = new BiomeDef[0];

        /* kind -> biomes (sorted by label) whose allowedPackAnimals include the animal's race.
           Built lazily per kind and never invalidated (biome/animal defs are static after load).
           Lazy-per-kind (rather than prebuilding from FactionCache.AllPackAnimalKinds) is deliberate:
           IsPackAnimal() calls this to decide pack eligibility, and AllPackAnimalKinds is built from
           IsPackAnimal() - prebuilding off that list would recurse. */
        private static readonly Dictionary<PawnKindDef, List<BiomeDef>> _packAnimalBiomes =
            new Dictionary<PawnKindDef, List<BiomeDef>>();

        /// <summary>The biomes in which the given animal can serve as a caravan carrier (its race is
        /// in that biome's allowedPackAnimals). This is the criterion vanilla trade-caravan generation
        /// uses, and the real test of whether an animal is a usable caravan pack animal.</summary>
        public static IReadOnlyList<BiomeDef> BiomesForPackAnimal(PawnKindDef kind)
        {
            if (kind?.race is null) return EmptyBiomes;
            List<BiomeDef> biomes;
            if (!_packAnimalBiomes.TryGetValue(kind, out biomes))
            {
                biomes = DefDatabase<BiomeDef>.AllDefsListForReading
                    .Where(b => b.IsPackAnimalAllowed(kind.race))
                    .OrderBy(b => b.LabelCap.ToString(), StringComparer.OrdinalIgnoreCase)
                    .ToList();
                _packAnimalBiomes[kind] = biomes;
            }
            return biomes;
        }

        /// <summary>
        /// The biome where the player's trade caravans are delivered: the active Tax Spot, else the
        /// Capital location, else the main colony. Returns null when none can be resolved (e.g. before
        /// any colony map exists) - callers should treat null as "can't determine, don't warn".
        /// </summary>
        public static BiomeDef GetCaravanDeliveryBiome()
        {
            // 1. Active tax spot (where tithes/caravans are actually delivered)
            Map taxSpotMap = ActiveTaxSpotMap();
            if (taxSpotMap is object) return taxSpotMap.Biome;

            // 2. Capital location - tile-based, so it works even when the capital's map isn't loaded
            PlanetTile capital = FindFC.CapitalLocation;
            if (capital.Valid) return Find.WorldGrid[capital].PrimaryBiome;

            // 3. Main colony
            return Find.AnyPlayerHomeMap?.Biome;
        }

        private static Map ActiveTaxSpotMap()
        {
            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                Map map = maps[i];
                if (!map.IsPlayerHome) continue;
                List<Building> buildings = map.listerBuildings.allBuildingsColonist;
                for (int j = 0; j < buildings.Count; j++)
                {
                    if (buildings[j] is Building_TaxSpot spot && spot.IsActiveTaxDeliverySpot)
                        return map;
                }
            }
            return null;
        }

        /// <summary>True if at least one allowed pack animal can serve as a carrier in the given biome.</summary>
        public static bool SelectionCoversBiome(AnimalFilter filter, BiomeDef biome)
        {
            if (biome is null || filter is null) return true; // can't determine -> don't warn
            return filter.AllowedPackAnimals.Any(k => k?.race is object && biome.IsPackAnimalAllowed(k.race));
        }

        /// <summary>
        /// Sends an orange minor-threat letter when a newly-set delivery location's biome is not
        /// covered by any allowed pack animal. <paramref name="descKey"/> is the translation key for
        /// the letter body (taking the biome label as {0}). Safe to call before a game is fully loaded.
        /// </summary>
        public static void WarnIfDeliveryBiomeUncovered(BiomeDef biome, string descKey)
        {
            if (biome is null) return;
            AnimalFilter filter = FindFC.FactionComp?.animalFilter;
            if (filter is null) return;
            if (SelectionCoversBiome(filter, biome)) return;

            Find.LetterStack.ReceiveLetter(
                "FCAnimalBiomeLetterLabel".Translate(),
                descKey.Translate(biome.LabelCap),
                LetterDefOf.ThreatSmall);
        }
    }
}
