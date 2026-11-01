using HarmonyLib;
using RimWar.Planet;
using RimWorld.Planet;

namespace FactionColonies.RW
{
    /// <summary>
    /// RimWar's IsValidSettlement only accepts settlements with defNames "Settlement",
    /// "City_Faction", "City_Citadel", or "FactionBaseGenerator". Empire uses custom
    /// WorldSettlementDef defNames, so its settlements are invisible to RimWar.
    ///
    /// This postfix accepts WorldSettlementFC instances, making them visible to
    /// RimWar's settlement tracking, point accumulation, combat, and faction power totals.
    /// </summary>
    [HarmonyPatch(typeof(WorldUtility))]
    [HarmonyPatch("IsValidSettlement")]
    public static class Patch_IsValidSettlement
    {
        private static void Postfix(WorldObject wo, ref bool __result)
        {
            if (__result) return;
            if (wo is WorldSettlementFC settlement && !settlement.Destroyed && settlement.Faction is object)
            {
                __result = true;
            }
        }
    }
}
