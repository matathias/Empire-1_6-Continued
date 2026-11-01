using HarmonyLib;
using RimWar;
using RimWar.Planet;
using RimWorld.Planet;

namespace FactionColonies.RW
{
    /// <summary>
    /// When RimWar's abstract combat resolves with an attacker winning,
    /// ConvertSettlement calls Destroy() on the defender then creates a new
    /// vanilla settlement at the same tile. For Empire settlements:
    ///   - Destroy() is blocked by Empire's destroyFlag guard, but
    ///   - A duplicate vanilla settlement is still created at the tile
    ///   - If this was the last visible settlement, RemoveRWDFaction purges PColony
    ///
    /// This prefix skips the original for Empire settlements and instead applies
    /// point damage so the battle has consequences in RimWar's point system.
    /// Vassal behavior already prevents most capture attempts, but this catches
    /// any edge case where that check is bypassed.
    /// </summary>
    [HarmonyPatch(typeof(WorldUtility))]
    [HarmonyPatch("ConvertSettlement")]
    public static class Patch_ConvertSettlement
    {
        private static bool Prefix(Settlement worldSettlement, int points)
        {
            if (!(worldSettlement is WorldSettlementFC))
            {
                return true; // run original for non-Empire settlements
            }

            RimWarSettlementComp rwsc = worldSettlement.GetComponent<RimWarSettlementComp>();
            if (rwsc is object)
            {
                rwsc.PointDamage += points / 2;
                rwsc.AttackingUnits.Clear();
            }

            return false; // skip original
        }
    }
}
