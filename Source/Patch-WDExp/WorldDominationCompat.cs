using HarmonyLib;
using TSA_WorldDomination;

namespace FactionColonies.WDExp;

// Patches that only apply to "World Domination - Experimental". The patches shared
// with the stable World Domination build live in Shared/WorldDominationCommon.cs.
//
// Fixes:
// 1. Intercepts WD raids on Empire settlements (routes through Empire's defense system)
// 2. Prevents WD's CheckDefeated intercept from destroying Empire settlements

// ================================================================
// Experimental patch 1: Intercept WD raids targeting Empire settlements
// WD's ExecuteTravelerRaid checks target.Faction.IsPlayer
// to route player attacks, but PColony is not IsPlayer.
// Without this patch, WD runs simulated combat and ResolveVictory
// calls target.Destroy(), permanently deleting Empire settlement data.
// ================================================================
[HarmonyPatch(typeof(Raid_Simulated), "ExecuteTravelerRaid")]
public static class Patch_ExecuteTravelerRaid
{
    private static bool Prefix(WorldObject_Traveler traveler, WorldComponent_SpreadManager manager)
    {
        WorldSettlementFC empireSettlement = traveler.targetObject as WorldSettlementFC;
        if (empireSettlement is null) return true;
        if (traveler.Faction is null) return true;

        // WD's traveler strength dictates the military level (floored at Empire's minimum of 1)
        double wdMilitaryLevel = Math.Max(1.0, traveler.travelerStrength / WDStrengthSettlementModifier.SCALE_FACTOR);
        MilitaryForce attackingForce = MilitaryOperationsUtil.BuildAttackForce(traveler.Faction, wdMilitaryLevel);

        // Route through Empire's defense system (1-day warning + auto-battle/manual)
        bool queued = MilitaryOperationsUtil.AttackPlayerSettlement(attackingForce, empireSettlement, traveler.Faction);

        if (queued)
        {
            LogUtil.Message($"WD raid on Empire settlement {empireSettlement.Name} intercepted (WD strength {traveler.travelerStrength:F0} -> Empire force {attackingForce.forceRemaining})");
        }
        else
        {
            LogUtil.Message($"WD raid on Empire settlement {empireSettlement.Name} dropped (helper rejected: likely missing MilitaryComp or MilitaryManager).");
        }

        return false;
    }
}

// ================================================================
// Experimental patch 2: Prevent WD's CheckDefeated intercept from destroying
// Empire settlements. WD's Patch_InterceptDefeat runs at
// Priority.High and calls factionBase.Destroy() on defeated
// non-player settlements. PColony is not IsPlayer, so Empire
// settlements would be destroyed and replaced with ruins/outpost
// opportunities. This prefix-on-the-prefix skips WD's logic
// for WorldSettlementFC. WD's method is a bool prefix on
// CheckDefeated, so skipping it means choosing its return value:
// false, which also skips vanilla CheckDefeated (vanilla would turn
// a WorldSettlementFC into a DestroyedSettlement and crash).
// ================================================================
[HarmonyPatch(typeof(Patch_InterceptDefeat), "Prefix")]
public static class Fix_WD_InterceptDefeat
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Settlement factionBase, ref bool __result)
    {
        if (factionBase is not WorldSettlementFC) return true;
        __result = false;
        return false;
    }
}
