using HarmonyLib;
using RimWar.Planet;

namespace FactionColonies.RW;

/// <summary>
/// When a RimWar warband/scout arrives at an NPC settlement, RimWar calls
/// ResolveWarObjectAttackOnSettlement for abstract point-based combat. This
/// bypasses Empire's battle system entirely.
///
/// This prefix intercepts attacks on Empire settlements and routes them
/// through AttackPlayerSettlement, which creates a proper settlementBeingAttacked
/// event with 1-day timer, auto-defend dispatch, battle forecast, and respects
/// the player's manual/auto battle mode setting.
///
/// The warband self-destructs via base.ArrivalAction() -> ImmediateDestroy()
/// in the caller's flow after this method returns.
/// </summary>
[HarmonyPatch(typeof(IncidentUtility))]
[HarmonyPatch("ResolveWarObjectAttackOnSettlement")]
public static class Patch_ResolveWarObjectAttack
{
    private static bool Prefix(WarObject attacker, RimWarSettlementComp defender)
    {
        if (defender.parent is not WorldSettlementFC empireSettlement)
        {
            return true; // run original for non-Empire settlements
        }

        if (attacker?.Faction is null)
        {
            return false; // invalid attacker, skip
        }

        // RimWarPoints dictates the military level
        double level = Math.Max(1.0, Math.Sqrt(attacker.RimWarPoints) / 20.0);
        MilitaryForce attackingForce = MilitaryOperationsUtil.BuildAttackForce(attacker.Faction, level);

        MilitaryOperationsUtil.AttackPlayerSettlement(attackingForce, empireSettlement, attacker.Faction);

        LogUtil.Message($"RW attack on {empireSettlement.Name} by {attacker.Faction.Name} (pts={attacker.RimWarPoints} -> level={level:F1})");

        return false; // skip original; don't add to RimWar's AttackingUnits
    }
}
