using HarmonyLib;
using RimWorld;
using System.Collections.Generic;

namespace FactionColonies
{
    /// <summary>
    /// Filters zero-cost options from PawnGroupMakerUtility.GetOptions to prevent
    /// an infinite loop in ChoosePawnGenOptionsByPoints. When a PawnGenOption has
    /// effective cost 0 (combatPower=0 or xenotype.combatPowerFactor=0), the loop
    /// subtracts 0 from the point budget and never terminates.
    /// </summary>
    [HarmonyPatch(typeof(PawnGroupMakerUtility))]
    [HarmonyPatch("GetOptions")]
    static class Patch_GetOptions_FilterZeroCost
    {
        static void Postfix(List<PawnGenOptionWithXenotype> __result)
        {
            int culledCount = 0;
            for (int i = __result.Count - 1; i >= 0; i--)
            {
                if (__result[i].Cost <= 0f)
                {
                    PawnGenOptionWithXenotype culled = __result[i];
                    LogUtil.Warning("GetOptions culled zero-cost option: kind="
                        + (culled.Option.kind?.defName ?? "[null kind]")
                        + " combatPower=" + (culled.Option.kind?.combatPower.ToString() ?? "?")
                        + " xenotype=" + (culled.Xenotype != null ? culled.Xenotype.defName : "null")
                        + " xenotypeCombatPowerFactor=" + (culled.Xenotype != null ? culled.Xenotype.combatPowerFactor.ToString() : "N/A")
                        + " effectiveCost=" + culled.Cost);
                    __result.RemoveAt(i);
                    culledCount++;
                }
            }
            if (culledCount > 0 && __result.Count == 0)
            {
                LogUtil.Warning("GetOptions: all " + culledCount + " option(s) had zero effective cost and were removed. No pawns will be generated for this group.");
            }
        }
    }
}
