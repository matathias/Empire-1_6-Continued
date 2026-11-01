using FactionTerritories;
using FactionTerritories.Vassalise;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace FactionColonies.FTV
{
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
    /// The static constructor deliberately avoids referencing any FTV types directly.
    /// All FTV type access is in ApplyPatches(), which is marked NoInlining so the
    /// JIT compiler resolves FTV types only when that method is actually called
    /// (inside a try/catch), rather than when the constructor is JIT-compiled.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class FactionTerritoriesCompatInit
    {
        static FactionTerritoriesCompatInit()
        {
            new Harmony("com.Matathias.Empire.FTV").PatchAll(Assembly.GetExecutingAssembly());
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
            if (__result == null || __result.Count == 0) return;

            Faction pColony = FindFC.EmpireFaction;
            if (pColony == null) return;

            Faction player = Faction.OfPlayer;
            if (player == null) return;

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
    /// </summary>
    [HarmonyPatch(typeof(InterceptBaseDestroyedLetterPatch))]
    [HarmonyPatch("Prefix")]
    public static class InterceptPrefix_Guard
    {
        private static bool Prefix(Settlement factionBase)
        {
            if (factionBase is WorldSettlementFC)
            {
                return false;
            }
            return true;
        }
    }
}
