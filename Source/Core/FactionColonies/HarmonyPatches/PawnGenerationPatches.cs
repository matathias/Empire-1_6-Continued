using FactionColonies.util;
using HarmonyLib;
using RimWorld;
using Verse;

namespace FactionColonies
{
    [HarmonyPatch(typeof(PawnGenerator), "GeneratePawn", typeof(PawnGenerationRequest))]
    class PawnGenerationPatches
    {
        /// <summary>
        /// When true, the prefix respects any pre-set ForcedXenotype on the request
        /// (e.g. from designed military units). When false, vanilla's default Baseliner
        /// on Empire template PawnKindDefs is cleared so the xenotype filter can run.
        /// Set by <see cref="FCPawnGenerator.GenerateWithForcedXenotype"/>.
        /// </summary>
        // I don't much like this static flag, but I'm not sure of a better way around vanilla's
        //   forced Baseliner behavior
        internal static bool respectForcedXenotype = false;

        public static void Prefix(ref PawnGenerationRequest request)
        {
            if (!(request.Faction is null) && request.Faction == FindFC.EmpireFaction && request.KindDef?.IsHumanLikeRace() == true)
            {
                if (!(request.ForcedXenotype is null) || !(request.ForcedCustomXenotype is null))
                {
                    // If our own code explicitly set the forced xenotype (e.g. designed military units),
                    // respect it and skip the filter.
                    if (respectForcedXenotype)
                    {
                        return;
                    }

                    // Vanilla's PawnGroupKindWorker defaults to Baseliner for Empire template clones
                    // (useFactionXenotypes=false, xenotypeSet=null). Clear it so our filter runs.
                    if (request.KindDef?.defName?.StartsWith("PColony_") == true)
                    {
                        request.ForcedXenotype = null;
                        request.ForcedCustomXenotype = null;
                    }
                    else
                    {
                        return; // Non-Empire PawnKindDef — respect whatever was forced
                    }
                }

                XenotypeFilter filter = FindFC.FactionComp?.xenotypeFilter;
                if (filter is null) return;

                XenotypeDef chosenXenotype = null;
                CustomXenotype chosenCustomXenotype = null;

                filter.GetRandomXenotypeForRequest(request, out chosenXenotype, out chosenCustomXenotype);

                /* Modify the request to force our chosen xenotype */
                if (!(chosenXenotype is null))
                {
                    request.ForcedXenotype = chosenXenotype;
                    LogUtil.Message($"GeneratePawn patch forced xenotype: {chosenXenotype.defName} for pawnKind: {request.KindDef.defName}");
                }
                else if (!(chosenCustomXenotype is null))
                {
                    request.ForcedCustomXenotype = chosenCustomXenotype;
                    LogUtil.Message($"GeneratePawn patch forced custom xenotype: {chosenCustomXenotype.name} for pawnKind: {request.KindDef.defName}");
                }
                else
                {
                    LogUtil.Warning($"GeneratePawn patch failed to force a xenotype or custom xenotype for pawnKind: {request.KindDef.defName}");
                }
            }
        }
    }
}
