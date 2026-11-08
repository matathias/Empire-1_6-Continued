using FactionTerritories;
using FactionTerritories.Vassalise;
using HarmonyLib;
using System.Reflection;

namespace FactionColonies.FTV;

/// <summary>
/// Compatibility patches for "Faction Territories and Vassalage".
/// This assembly is only loaded when FTV is active (via LoadFolders.xml).
///
/// Fixes:
/// 1. Territory double-counting — FTV processes Empire settlements in both its
///    Settlements loop (with PColony→player remapping) and AllWorldObjects loop
///    (without remapping), creating false "contested" territory.
/// 2. Vassalage interception — FTV's CheckDefeated prefix could attempt to
///    vassalize the player's own Empire settlements.
///
/// The static constructor references no FTV types directly; the patch classes do, and each
/// class is applied on its own, so one that no longer resolves against a newer FTV is logged
/// and skipped without stopping the others.
/// </summary>
[StaticConstructorOnStartup]
public static class FactionTerritoriesCompatInit
{
    static FactionTerritoriesCompatInit()
    {
        new Harmony("com.Matathias.Empire.FTV").PatchEachClass(Assembly.GetExecutingAssembly());
        LogUtil.MessageForce("Faction Territories & Vassalage patched");
    }
}

/// <summary>
/// Postfix on TerritoryOwnershipCache.BuildFactionMap.
/// Replaces all PColony faction entries with the player faction,
/// eliminating double-counted territory tiles that cause "contested"
/// display and spurious caravan meetings.
/// </summary>
[HarmonyPatch(typeof(TerritoryOwnershipCache))]
[HarmonyPatch("BuildFactionMap")]
public static class BuildFactionMap_Patch
{
    private static void Postfix(Dictionary<int, HashSet<int>> __result)
    {
        if (__result is null || __result.Count == 0) return;

        Faction pColony = FindFC.EmpireFaction;
        if (pColony is null) return;

        Faction player = Faction.OfPlayer;
        if (player is null) return;

        int pColonyId = pColony.loadID;
        int playerId = player.loadID;
        if (pColonyId == playerId) return;

        foreach (KeyValuePair<int, HashSet<int>> kvp in __result)
        {
            if (kvp.Value.Remove(pColonyId))
            {
                kvp.Value.Add(playerId);
            }
        }
    }
}

/// <summary>
/// Prefix on FTV's InterceptBaseDestroyedLetterPatch.Prefix.
/// Prevents FTV from attempting vassalage processing on Empire settlements.
/// FTV's method is itself a bool prefix on SettlementDefeatUtility.CheckDefeated, so skipping
/// it means choosing its return value: false, which also skips vanilla CheckDefeated (vanilla
/// would turn a WorldSettlementFC into a DestroyedSettlement and crash).
/// </summary>
[HarmonyPatch(typeof(InterceptBaseDestroyedLetterPatch))]
[HarmonyPatch("Prefix")]
public static class InterceptPrefix_Guard
{
    private static bool Prefix(Settlement factionBase, ref bool __result)
    {
        if (factionBase is WorldSettlementFC)
        {
            __result = false;
            return false;
        }
        return true;
    }
}
