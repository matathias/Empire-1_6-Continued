using HarmonyLib;
using LudeonTK;

namespace FactionColonies;

/* Hides [EmpireDebugAction] entries from the debug menu while the Empire faction does not exist.
 * The menu re-reads this getter whenever it rebuilds its visible list, so the actions appear as
 * soon as the faction is created. */
[HarmonyPatch(typeof(DebugActionAttribute), nameof(DebugActionAttribute.IsAllowedInCurrentGameState), MethodType.Getter)]
static class EmpireDebugActionVisibilityPatch
{
    static void Postfix(DebugActionAttribute __instance, ref bool __result)
    {
        if (__result && __instance is EmpireDebugActionAttribute && FindFC.EmpireFaction is null)
            __result = false;
    }
}
