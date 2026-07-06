using FactionColonies.util;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// While an Empire manual offensive battle owns an enemy settlement's tile, Empire drives
    /// win-detection, the outcome letter, settlement fate, and teardown (via EndOffense / ApplyResult).
    /// These patches suppress vanilla's own defeat handling and automatic map-removal so they can't
    /// convert the settlement to a DestroyedSettlement, fire a vanilla victory letter, or tear the
    /// map down underneath Empire mid-battle.
    /// Once the op completes and the offense context releases the map, these stop intercepting.
    /// </summary>
    internal static class OffenseGuardUtil
    {
        public static bool OffenseActiveAt(Settlement s)
        {
            if (s is null || s is WorldSettlementFC) return false;
            // CheckDefeated runs this per settlement every settlement tick. Skip the PlanetTile hash +
            // dict probe when no battlefield exists at all (the common case: no active Empire offense).
            MilitaryOperationManager mgr = FindFC.MilitaryManager;
            if (mgr?.battlefields is null || mgr.battlefields.Count == 0) return false;
            BattlefieldContext bf = mgr.GetBattlefield(s.Tile);
            return bf is object && bf.HasOffenseAt();
        }
    }

    [HarmonyPatch(typeof(Settlement), nameof(Settlement.ShouldRemoveMapNow))]
    public static class Settlement_ShouldRemoveMapNow_OffenseGuard
    {
        public static bool Prefix(Settlement __instance, ref bool __result, ref bool alsoRemoveWorldObject)
        {
            if (OffenseGuardUtil.OffenseActiveAt(__instance))
            {
                alsoRemoveWorldObject = false;
                __result = false;   // never auto-remove the map while Empire owns the assault
                return false;
            }
            return true;
        }
    }
}
