using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWar.Planet;
using Verse;

namespace FactionColonies.RW
{
    /// <summary>
    /// Vassal settlements get manual unit request gizmos (Send Trader, Send Scout,
    /// Send Warband, Launch Warband). Since our RimWarPoints prefix bypasses the
    /// original getter, unit costs from these gizmos would never actually deduct,
    /// making them free. We suppress all RimWar gizmos for Empire settlements;
    /// players should use Empire's own military deployment system instead.
    /// </summary>
    [HarmonyPatch(typeof(RimWarSettlementComp))]
    [HarmonyPatch("GetGizmos")]
    public static class Patch_GetGizmos
    {
        private static bool Prefix(RimWarSettlementComp __instance, ref IEnumerable<Gizmo> __result)
        {
            if (__instance.parent is WorldSettlementFC)
            {
                __result = Enumerable.Empty<Gizmo>();
                return false; // skip original. No RimWar gizmos for Empire settlements
            }
            return true;
        }
    }
}
