using System.Collections.Generic;
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

        /// <summary>
        /// True while an Empire manual offense still owns this tile AND has not yet entered the
        /// post-win loot linger. During the linger the player must be able to reform a caravan to
        /// extract their Join-Attack colonists (by then the drafted squad mercs are already reclaimed
        /// to Empire and drafting is blocked, so reforming is safe) -- hence the extra
        /// <c>!awaitingPlayerExit</c> beyond <see cref="OffenseActiveAt"/>.
        /// </summary>
        public static bool OffenseBlocksReformAt(Settlement s)
        {
            if (s is null || s is WorldSettlementFC) return false;
            MilitaryOperationManager mgr = FindFC.MilitaryManager;
            if (mgr?.battlefields is null || mgr.battlefields.Count == 0) return false;
            BattlefieldContext bf = mgr.GetBattlefield(s.Tile);
            return bf is object && bf.HasOffenseAt() && !bf.awaitingPlayerExit;
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

    /// <summary>
    /// Disables the vanilla Reform Caravan gizmo on an enemy settlement's battle map while Empire's
    /// manual offense is still unresolved. Vanilla re-enables reform the instant the garrison stops
    /// being an active threat (downed/fled), but Empire only completes the battle on its next
    /// OffenseTick poll; reforming in that gap would sweep the still-drafted Empire squad mercs into
    /// a caravan, and they get culled as non-world pawns on map exit. Mirrors the manual-defense
    /// block done by <see cref="FormCaravanCompFC"/> (which can't apply here, since the offense map's
    /// parent is a vanilla Settlement, not an Empire WorldSettlementFC).
    /// </summary>
    [HarmonyPatch(typeof(FormCaravanComp), nameof(FormCaravanComp.GetGizmos))]
    public static class FormCaravanComp_GetGizmos_OffenseGuard
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, WorldObjectComp __instance)
        {
            bool block = OffenseGuardUtil.OffenseBlocksReformAt(__instance.parent as Settlement);
            foreach (Gizmo gizmo in __result)
            {
                if (block && gizmo is Command_Action cmd && cmd.tutorTag == "ReformCaravan")
                    cmd.Disable("FCReformCaravanBattleStillActive".Translate());
                yield return gizmo;
            }
        }
    }
}
