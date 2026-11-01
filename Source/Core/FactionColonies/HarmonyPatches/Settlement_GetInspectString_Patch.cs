using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Appends the cached EnemyPower (military level + combat efficiency) for non-Empire
    /// settlements to the world map inspect pane, mirroring the line shown in the
    /// attack-settlement dialog so the player can scout strength without starting an attack.
    /// </summary>
    [HarmonyPatch(typeof(Settlement), nameof(Settlement.GetInspectString))]
    class Settlement_GetInspectString_Patch
    {
        public static void Postfix(Settlement __instance, ref string __result)
        {
            /* Empire's own settlements draw their own UI in SettlementWindowFC. */
            if (__instance is WorldSettlementFC) return;

            EnemyPower power = FindFC.EnemyPower?.GetOrCompute(__instance);
            if (power is null) return;

            string forceText = TextUtil.FormatRange(power.MinForceRemaining, power.MaxForceRemaining, "0");
            string effText = TextUtil.FormatRange(power.MinEfficiency, power.MaxEfficiency, "0.##");
            string line = "FCSquadPickerEstimatedDefender".Translate(forceText, effText);

            __result = string.IsNullOrEmpty(__result) ? line : __result + "\n" + line;
        }
    }
}
