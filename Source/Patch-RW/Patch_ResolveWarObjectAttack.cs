using System;
using HarmonyLib;
using RimWar.Planet;
using RimWorld;
using Verse;

namespace FactionColonies.RW
{
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
            if (!(defender.parent is WorldSettlementFC empireSettlement))
            {
                return true; // run original for non-Empire settlements
            }

            if (attacker?.Faction is null)
            {
                return false; // invalid attacker, skip
            }

            // Sample the baseline through the worldcomp so faction-level efficiency modifiers apply.
            // RimWarPoints dictates militaryLevel, so we override the sampled level with it.
            EnemyPower entry = FindFC.EnemyPower?.GetOrCompute(attacker.Faction);
            MilitaryForce attackingForce = entry?.SampleBattleForce(attacker.Faction);
            if (attackingForce is null)
            {
                double efficiency = 1.0;
                if (attacker.Faction.def is object)
                {
                    MilitaryDeploymentUtil.GetTechLevelBaseline(attacker.Faction.def.techLevel, out double _, out efficiency);
                }
                attackingForce = new MilitaryForce(1, efficiency, null, attacker.Faction);
            }

            double level = Math.Max(1.0, Math.Sqrt(attacker.RimWarPoints) / 20.0);
            attackingForce.militaryLevel = level;
            attackingForce.forceRemaining = Math.Max(1, Math.Round(attackingForce.militaryLevel * attackingForce.militaryEfficiency));

            MilitaryOperationsUtil.AttackPlayerSettlement(attackingForce, empireSettlement, attacker.Faction);

            LogUtil.Message("RW attack on " + empireSettlement.Name + " by " + attacker.Faction.Name
                + " (pts=" + attacker.RimWarPoints + " -> level=" + level.ToString("F1") + ")");

            return false; // skip original; don't add to RimWar's AttackingUnits
        }
    }
}
