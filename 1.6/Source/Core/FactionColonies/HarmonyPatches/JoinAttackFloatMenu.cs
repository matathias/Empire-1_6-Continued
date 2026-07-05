using System.Collections.Generic;
using HarmonyLib;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Suppresses the vanilla "Attack settlement" caravan float option while an Empire military
    /// operation is actively targeting that settlement (squad en route or battle in progress;
    /// cooldown excluded) -- the same window the Empire attack gizmo is hidden for. You can't start
    /// a competing vanilla attack on a settlement already under an Empire op; during a live manual
    /// battle the caravan instead sees "Join Attack" (added by WorldObjectComp_OffenseControls). A
    /// patch is unavoidable since this option is contributed by vanilla, not by our comp.
    /// </summary>
    [HarmonyPatch(typeof(CaravanArrivalAction_AttackSettlement), nameof(CaravanArrivalAction_AttackSettlement.GetFloatMenuOptions))]
    public static class CaravanAttackSettlement_SuppressDuringOffense
    {
        public static bool Prefix(Settlement settlement, ref IEnumerable<FloatMenuOption> __result)
        {
            if (FindFC.MilitaryManager?.HasActiveOpAt(settlement.Tile) ?? false)
            {
                __result = new List<FloatMenuOption>();
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Gizmo counterpart of the above: while an Empire op is actively targeting the tile, filters the
    /// vanilla "Attack settlement" caravan gizmo (yielded by Settlement.GetCaravanGizmos). Identified
    /// by its shared icon (Settlement.AttackCommand); the Join Attack gizmo uses a different icon, so
    /// it is unaffected.
    /// </summary>
    [HarmonyPatch(typeof(Settlement), nameof(Settlement.GetCaravanGizmos))]
    public static class Settlement_GetCaravanGizmos_SuppressAttackDuringOffense
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Settlement __instance)
        {
            bool underActiveOp = FindFC.MilitaryManager?.HasActiveOpAt(__instance.Tile) ?? false;
            foreach (Gizmo g in __result)
            {
                if (underActiveOp && g is Command_Action ca && ca.icon == Settlement.AttackCommand) continue;
                yield return g;
            }
        }
    }
}
