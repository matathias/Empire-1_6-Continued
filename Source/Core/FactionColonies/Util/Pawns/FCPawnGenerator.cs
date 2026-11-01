using RimWorld;
using System;
using Verse;

namespace FactionColonies.util
{
    public static class FCPawnGenerator
    {
        /// <summary>
        /// Generates a pawn with a specific forced xenotype that the PawnGenerationPatches prefix
        /// will respect (instead of overriding with the xenotype filter).
        /// Use this for designed military units where the player chose a specific xenotype.
        /// </summary>
        public static Pawn GenerateWithForcedXenotype(PawnGenerationRequest request)
        {
            PawnGenerationPatches.respectForcedXenotype = true;
            try
            {
                return PawnGenerator.GeneratePawn(request);
            }
            finally
            {
                PawnGenerationPatches.respectForcedXenotype = false;
            }
        }

        public static PawnGenerationRequest WorkerOrMilitaryRequest(PawnKindDef pawnKindDef = null, XenotypeDef xenotypeDef = null, Gender? fixedGender = null)
        {
            PawnKindDef kindDef = pawnKindDef ?? GetDefaultFighter();

            return HumanlikeRequest(kindDef, mustBeViolent: true, xenotypeDef: xenotypeDef, fixedGender: fixedGender);
        }

        public static PawnGenerationRequest WorkerOrMilitaryRequest(PawnKindDef pawnKindDef, CustomXenotype customXenotype, Gender? fixedGender = null)
        {
            PawnKindDef kindDef = pawnKindDef ?? GetDefaultFighter();

            return HumanlikeRequest(kindDef, mustBeViolent: true, customXenotype: customXenotype, fixedGender: fixedGender);
        }

        /// <summary>
        /// Creates a WorkerOrMilitaryRequest dispatching to the correct overload
        /// based on whether the unit uses a XenotypeDef or CustomXenotype.
        /// </summary>
        public static PawnGenerationRequest WorkerOrMilitaryRequestForUnit(MilUnitFC unit)
        {
            if (unit.IsCustomXenotype)
            {
                CustomXenotype custom = unit.ResolveCustomXenotype();
                if (custom != null)
                    return WorkerOrMilitaryRequest(unit.pawnKind, custom, unit.forcedGender);
                return WorkerOrMilitaryRequest(unit.pawnKind, (XenotypeDef)null, unit.forcedGender);
            }
            return WorkerOrMilitaryRequest(unit.pawnKind, unit.xenotype, unit.forcedGender);
        }

        public static PawnGenerationRequest CivilianRequest(PawnKindDef pawnKindDef = null, XenotypeDef xenotypeDef = null)
        {
            PawnKindDef kindDef = pawnKindDef ?? GetDefaultVillager();

            return HumanlikeRequest(kindDef, mustBeViolent: false, xenotypeDef: xenotypeDef);
        }
        public static PawnGenerationRequest CivilianRequest(PawnKindDef pawnKindDef, CustomXenotype customXenotype)
        {
            PawnKindDef kindDef = pawnKindDef ?? GetDefaultVillager();

            return HumanlikeRequest(kindDef, mustBeViolent: false, customXenotype: customXenotype);
        }

        private static PawnKindDef GetDefaultFighter()
        {
            XenotypeFilter filter = FindFC.FactionComp?.xenotypeFilter;
            ThingDef race = filter?.GetRandomRace() ?? ThingDefOf.Human;
            return PawnKindTemplateUtil.GetFighterForRace(race);
        }

        private static PawnKindDef GetDefaultVillager()
        {
            XenotypeFilter filter = FindFC.FactionComp?.xenotypeFilter;
            ThingDef race = filter?.GetRandomRace() ?? ThingDefOf.Human;
            return PawnKindTemplateUtil.GetVillagerForRace(race);
        }

        private static PawnGenerationRequest HumanlikeRequest(PawnKindDef pawnKindDef = null,
                                                              bool mustBeViolent = true,
                                                              XenotypeDef xenotypeDef = null,
                                                              CustomXenotype customXenotype = null,
                                                              Gender? fixedGender = null)
        {
            if (pawnKindDef is null)
            {
                LogUtil.Warning($"HumanlikeRequest called with null pawnKindDef. Defaulting to PColony_Fighter");
                pawnKindDef = PColonyPawnKindDefOf.PColony_Fighter;
            }

            if (!(xenotypeDef is null) && !(customXenotype is null))
            {
                LogUtil.Warning($"HumanlikeRequest called with xenotypeDef {xenotypeDef.defName} and customXenotype {customXenotype.name}. Defaulting to xenotypeDef");
                customXenotype = null;
            }

            float? fixedAge = null;
            try
            {
                fixedAge = pawnKindDef.GetReasonableMercenaryAge();
            }
            catch (Exception ex)
            {
                LogUtil.Warning($"Failed to get reasonable age for {TextUtil.GetDefModInfo(pawnKindDef)}: {ex.Message}");
                fixedAge = null;
            }

            // Normalize Gender.None (== "any") to null so PawnGenerator randomizes.
            Gender? resolvedGender = (fixedGender.HasValue && fixedGender.Value != Gender.None)
                ? fixedGender : (Gender?)null;

            return new PawnGenerationRequest(
                kind: pawnKindDef,
                faction: FindFC.EmpireFaction,
                context: PawnGenerationContext.NonPlayer,
                tile: -1,
                forceGenerateNewPawn: false,
                allowDead: false,
                allowDowned: false,
                canGeneratePawnRelations: false,
                mustBeCapableOfViolence: mustBeViolent,
                colonistRelationChanceFactor: 0,
                forceAddFreeWarmLayerIfNeeded: false,
                allowGay: true,
                allowFood: true,
                allowAddictions: false,
                inhabitant: false,
                certainlyBeenInCryptosleep: false,
                forceRedressWorldPawnIfFormerColonist: false,
                worldPawnFactionDoesntMatter: false,
                biocodeWeaponChance: 0,
                extraPawnForExtraRelationChance: null,
                relationWithExtraPawnChanceFactor: 0,
                validatorPreGear: null,
                validatorPostGear: null,
                forcedTraits: null,
                prohibitedTraits: null,
                forcedXenotype: xenotypeDef,
                forcedCustomXenotype: customXenotype,
                fixedBiologicalAge: fixedAge,
                fixedChronologicalAge: fixedAge,
                fixedGender: resolvedGender
            );
        }

        public static PawnGenerationRequest AnimalRequest(PawnKindDef race)
        {
            return new PawnGenerationRequest(
                kind: race,
                faction: FindFC.EmpireFaction,
                context: PawnGenerationContext.NonPlayer,
                tile: -1,
                forceGenerateNewPawn: false,
                allowDead: false,
                allowDowned: false,
                canGeneratePawnRelations: false,
                mustBeCapableOfViolence: false,
                colonistRelationChanceFactor: 0,
                forceAddFreeWarmLayerIfNeeded: false,
                allowGay: true,
                allowFood: true,
                allowAddictions: false,
                inhabitant: false,
                certainlyBeenInCryptosleep: false,
                forceRedressWorldPawnIfFormerColonist: false,
                worldPawnFactionDoesntMatter: false,
                biocodeWeaponChance: 0,
                extraPawnForExtraRelationChance: null,
                relationWithExtraPawnChanceFactor: 0,
                validatorPreGear: null,
                validatorPostGear: null,
                forcedTraits: null,
                prohibitedTraits: null,
                fixedBiologicalAge: race.GetReasonableMercenaryAge()
            );
        }

        /// <summary>
        /// Generate a controllable mechanoid for a mechanitor merc's bonded mech roster. Mirrors
        /// <see cref="AnimalRequest"/> (Empire faction, no relations); mechs need no xenotype and no
        /// mercenary-age handling. Biotech-only — callers gate on ModsConfig.BiotechActive.
        /// </summary>
        public static PawnGenerationRequest MechRequest(PawnKindDef mechKind)
        {
            return new PawnGenerationRequest(
                kind: mechKind,
                faction: FindFC.EmpireFaction,
                context: PawnGenerationContext.NonPlayer,
                tile: -1,
                forceGenerateNewPawn: false,
                allowDead: false,
                allowDowned: false,
                canGeneratePawnRelations: false,
                mustBeCapableOfViolence: false,
                colonistRelationChanceFactor: 0,
                forceAddFreeWarmLayerIfNeeded: false,
                allowGay: true,
                allowFood: true,
                allowAddictions: false,
                inhabitant: false,
                certainlyBeenInCryptosleep: false,
                forceRedressWorldPawnIfFormerColonist: false,
                worldPawnFactionDoesntMatter: false,
                biocodeWeaponChance: 0,
                extraPawnForExtraRelationChance: null,
                relationWithExtraPawnChanceFactor: 0,
                validatorPreGear: null,
                validatorPostGear: null,
                forcedTraits: null,
                prohibitedTraits: null
            );
        }

        /// <summary>
        /// Generate a simple delivery pawn using the Empire's fighter template (tech-scaled).
        /// </summary>
        public static PawnGenerationRequest SimpleDeliveryRequest()
        {
            return new PawnGenerationRequest(
                kind: GetDefaultFighter(),
                faction: FindFC.EmpireFaction,
                context: PawnGenerationContext.NonPlayer,
                tile: -1,
                forceGenerateNewPawn: false,
                allowDead: false,
                allowDowned: false,
                canGeneratePawnRelations: false,
                mustBeCapableOfViolence: true,
                colonistRelationChanceFactor: 0,
                forceAddFreeWarmLayerIfNeeded: false,
                allowGay: true,
                allowFood: true,
                allowAddictions: false,
                inhabitant: false,
                certainlyBeenInCryptosleep: false,
                forceRedressWorldPawnIfFormerColonist: false,
                worldPawnFactionDoesntMatter: false,
                biocodeWeaponChance: 0,
                extraPawnForExtraRelationChance: null,
                relationWithExtraPawnChanceFactor: 0,
                validatorPreGear: null,
                validatorPostGear: null,
                forcedTraits: null,
                prohibitedTraits: null
            );
        }
    }
}
