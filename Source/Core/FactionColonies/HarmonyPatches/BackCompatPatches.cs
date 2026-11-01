using HarmonyLib;
using System;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Remaps runtime PawnKindDef clone defNames (e.g. PColony_Elite_Human) back to their
    /// base template (PColony_Elite) during save loading. The clones are created at runtime by
    /// <see cref="FactionColonies.util.PawnKindTemplateUtil"/> and aren't in the DefDatabase
    /// at cross-ref resolution time. After loading, <see cref="FactionColonies.util.PawnKindTemplateUtil.FixupPawnKindDefs"/>
    /// restores the correct race-specific clone.
    /// </summary>
    [HarmonyPatch(typeof(BackCompatibility))]
    [HarmonyPatch("BackCompatibleDefName")]
    static class BackCompatibleDefName_Patch
    {
        // Must stay in sync with PawnKindTemplateUtil.FixupPawnKindDefs templates
        // and PColonyPawnKindDefOf entries.
        private static readonly string[] TemplateNames =
        {
            "PColony_Fighter", "PColony_Elite", "PColony_Leader",
            "PColony_Trader", "PColony_Guard", "PColony_Villager"
        };

        static void Postfix(Type defType, ref string __result)
        {
            if (defType != typeof(PawnKindDef)) return;
            if (DefDatabase<PawnKindDef>.GetNamedSilentFail(__result) != null) return;

            foreach (string template in TemplateNames)
            {
                if (__result.StartsWith(template + "_"))
                {
                    __result = template;
                    return;
                }
            }
        }
    }
}
