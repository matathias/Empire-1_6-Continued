using System;
using HarmonyLib;
using RimWar.Planet;
using UnityEngine;

namespace FactionColonies.RW
{
    /// <summary>
    /// RimWar clamps vassal settlement points to 100-10,000 and runs a broken
    /// EmpireFaction_ColonyCheck on every getter call. For Empire settlements,
    /// we skip the original entirely and return points calculated from Empire's
    /// actual military and economic strength.
    ///
    /// This is the single interception point for ALL point reads in RimWar:
    /// PointsFromSettlements, TotalFactionPoints, EffectivePoints, SettlementScanRange,
    /// capitol selection, combat resolution, targeting, and UI display all read
    /// through this property.
    /// </summary>
    [HarmonyPatch(typeof(RimWarSettlementComp))]
    [HarmonyPatch("RimWarPoints", MethodType.Getter)]
    public static class Patch_RimWarPoints
    {
        private static bool Prefix(RimWarSettlementComp __instance, ref int __result)
        {
            if (!(__instance.parent is WorldSettlementFC settlement))
            {
                return true; // run original for non-Empire settlements
            }

            int points = 500                                                // base
                + settlement.settlementLevel * 300                          // development tier (300-2400)
                + settlement.settlementMilitaryLevel * 2500                 // garrison strength (0-17500+)
                + (int)(settlement.prosperity * 15)                         // economic health (0-1500)
                + (int)(settlement.workers * 40)                            // population
                + (int)Math.Max(settlement.GetDefenseBonus() * 5, 0);       // resource-weighted defense

            __result = Mathf.Clamp(points, 500, 100000);
            return false; // skip original
        }
    }
}
