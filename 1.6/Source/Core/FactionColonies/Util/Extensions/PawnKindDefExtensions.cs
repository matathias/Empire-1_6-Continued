using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    static class PawnKindDefExtensions
    {
        public static bool IsHumanLikeRace(this PawnKindDef pawnKindDef)
        {
            return pawnKindDef.ValidPawnKindDef() && pawnKindDef.race.race.intelligence == Intelligence.Humanlike && pawnKindDef.race.BaseMarketValue != 0;
        }

        public static bool IsHumanlikeWithLabelRace(this PawnKindDef pawnKindDef)
        {
            return pawnKindDef?.race?.label != null && pawnKindDef.IsHumanLikeRace();
        }

        public static bool IsXenotypeWithLabel(this XenotypeDef xenotypeDef)
        {
            return xenotypeDef?.label != null;
        }

        private static ResourceFilterExtension_Animals _animalFilterConfig;
        private static ResourceFilterExtension_Animals AnimalFilterConfig =>
            _animalFilterConfig ?? (_animalFilterConfig = ResourceTypeDefOf.RTD_Animals?.GetModExtension<ResourceFilterExtension_Animals>());

        private static HashSet<string> _blacklistedDefNamesSet;
        private static HashSet<string> BlacklistedDefNames =>
            _blacklistedDefNamesSet ?? (_blacklistedDefNamesSet = new HashSet<string>(AnimalFilterConfig?.blacklistedDefNames ?? new List<string>()));

        public static bool ValidPawnKindDef(this PawnKindDef pawnKindDef)
        {
            if (pawnKindDef is null)
            {
                LogUtil.Error($"ValidPawnKindDef: found null pawnKindDef. This shouldn't be possible!");
                return false;
            }
            if (pawnKindDef.defName is null)
            {
                LogUtil.Warning($"ValidPawnKindDef: detected null defName for pawnKindDef {TextUtil.GetDefModInfo(pawnKindDef)}");
                return false;
            }
            if (pawnKindDef.race?.race is null)
            {
                LogUtil.Warning($"ValidPawnKindDef: detected null race or race.race for pawnKindDef {TextUtil.GetDefModInfo(pawnKindDef)}");
                return false;
            }

            return true;
        }
        /// <summary>
        ///		Checks if a given <c>PawnKindDef</c> <paramref name="pawnKindDef"/> is an Animal and if it is not blacklisted.
        ///		Blacklists are configured via XML on <see cref="ResourceFilterExtension_Animals"/> (attached to RTD_Animals).
        /// </summary>
        public static bool IsAnimalAndAllowed(this PawnKindDef pawnKindDef)
        {
            if (!pawnKindDef.ValidPawnKindDef())
                return false;
            
            var config = AnimalFilterConfig;
            if (config is null)
            {
                LogUtil.ErrorOnce($"IsAnimalAndAllowed: found null AnimalFilterConfig", 12532580);
                return false;
            }
            
            return pawnKindDef.race.race.Animal
                && pawnKindDef.RaceProps.IsFlesh
                && pawnKindDef.race.race.animalType != AnimalType.Dryad
                && (!config.requireTradeTags || pawnKindDef.race.tradeTags != null)
                && !BlacklistedDefNames.Contains(pawnKindDef.race.defName)
                && (pawnKindDef.race.tradeTags.NullOrEmpty() || config.blacklistedTradeTags.NullOrEmpty()
                    || !pawnKindDef.race.tradeTags.Any(tag => config.blacklistedTradeTags.Contains(tag)));
        }
        /// <summary>
        /// Checks if a given <c>PawnKindDef</c> <paramref name="pawnKindDef"/> is a valid combat animal.
        /// </summary>
        public static bool IsCombatAnimal(this PawnKindDef pawnKindDef)
        {
            return pawnKindDef.IsAnimalAndAllowed()
                   && pawnKindDef.RaceProps.trainability is object
                   && pawnKindDef.RaceProps.trainability.intelligenceOrder >= TrainabilityDefOf.Intermediate.intelligenceOrder
                   && pawnKindDef.combatPower >= 50f;
        }

        /// <summary>
        /// Checks if a given <c>PawnKindDef</c> <paramref name="pawnKindDef"/> is a valid caravan pack
        /// animal. Eligibility is the biome whitelist (the animal's race appears in at least one biome's
        /// allowedPackAnimals), NOT RaceProps.packAnimal: that flag is about player-caravan cargo
        /// capacity, whereas trade/delivery-caravan carrier generation is gated purely by the biome
        /// whitelist. An animal usable in zero biomes (e.g. Horse, Donkey, Bison, Yak, Mastodon - none
        /// of which any vanilla biome or base-game trader uses as a carrier) is not a usable pack animal.
        /// </summary>
        public static bool IsPackAnimal(this PawnKindDef pawnKindDef)
        {
            return pawnKindDef.IsAnimalAndAllowed()
                && AnimalBiomeUtil.BiomesForPackAnimal(pawnKindDef).Count > 0;
        }


        public static int GetReasonableMercenaryAge(this PawnKindDef pawnKindDef) =>
            Rand.Range((int)Math.Ceiling((pawnKindDef.race?.race?.lifeExpectancy ?? 18) * 0.2625d),
                       (int)Math.Floor((pawnKindDef.race?.race?.lifeExpectancy ?? 100) * 0.625d));

        /// <summary>
        /// Creates a shallow clone of the given PawnKindDef using <see cref="Gen.MemberwiseClone{T}"/>.
        /// The new PawnKindDef will be its own object, but will share references to the original's collection/object fields.
        /// Since Defs are read-only at runtime, this is safe, in theory. Rimworld uses the same approach in DebugAutotests.
        /// <para>Only exists for HAR compatibility when we need runtime PawnKindDefs for alien races.</para>
        /// </summary>
        /// <param name="pawnKindDef">PawnKindDef to copy.</param>
        /// <returns>A new object with the same fields as the provided PawnKindDef.</returns>
		public static PawnKindDef ShallowClone(this PawnKindDef pawnKindDef)
        {
            if (pawnKindDef is null)
                return null;

            return Gen.MemberwiseClone(pawnKindDef);
        }
    }

}
