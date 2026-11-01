using HarmonyLib;

namespace FactionColonies.RW
{
    /// <summary>
    /// RimWar's EmpireFaction_ColonyCheck iterates ALL world objects looking for
    /// defName == "Colony", which never matches any Empire def. Our RimWarPoints
    /// prefix already bypasses this for Empire settlements, so this is a defensive
    /// no-op to eliminate the dead O(n) iteration if anything else calls it.
    /// </summary>
    [HarmonyPatch(typeof(RimWar.ModCheck.Empire))]
    [HarmonyPatch("EmpireFaction_ColonyCheck")]
    public static class Patch_EmpireColonyCheck
    {
        private static bool Prefix(ref bool __result)
        {
            __result = false;
            return false; // skip original
        }
    }
}
