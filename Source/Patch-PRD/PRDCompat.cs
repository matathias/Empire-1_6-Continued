using HarmonyLib;
using PawnkindRaceDiversification.Patches;
using System.Reflection;
using Verse;

namespace FactionColonies.PRD
{
    /// <summary>
    /// Compatibility patches for Pawnkind Race Diversification.
    /// This assembly is only loaded when PRD is active (via LoadFolders.xml).
    ///
    /// Fix: PRD's DetermineRace prefix on PawnGenerator.GeneratePawn calls
    /// ResetRequest, which does an unsafe dictionary lookup on
    /// defaultKindBackstorySettings[kindDef.defName]. Empire's runtime-created
    /// PawnKindDef clones (e.g. PColony_Leader_Alien_Cinder) are never registered
    /// in that dictionary, causing a KeyNotFoundException.
    ///
    /// Solution: prefix DetermineRace to skip Empire's clones entirely.
    /// Empire already sets the correct race on its clones, so PRD's race
    /// diversification should not apply to them.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class PRDCompatInit
    {
        static PRDCompatInit()
        {
            new Harmony("com.Matathias.Empire.PRD").PatchAll(Assembly.GetExecutingAssembly());
            LogUtil.MessageForce("Pawnkind Race Diversification compatibility patched");
        }
    }

    /// <summary>
    /// Prefix on PawnkindGenerationHijacker.DetermineRace.
    /// Skips PRD processing for Empire's runtime PawnKindDef clones,
    /// which all have defNames starting with "PColony_".
    /// </summary>
    [HarmonyPatch(typeof(PawnkindGenerationHijacker))]
    [HarmonyPatch("DetermineRace")]
    public static class DetermineRace_SkipEmpireClones
    {
        private static bool Prefix(PawnGenerationRequest request)
        {
            if (request.KindDef?.defName?.StartsWith("PColony_") == true)
            {
                return false;
            }
            return true;
        }
    }
}
