using HarmonyLib;
using RimWorld;

namespace FactionColonies
{
    // Stops the storyteller from sending organic Empire trade caravans when the Empire is too
    // unhappy/disloyal/restless to bother. Gated on !parms.forced so the Mercantile policy's own
    // (separately gated) forced spawn is never blocked here.
    [HarmonyPatch(typeof(IncidentWorker_TraderCaravanArrival), "CanFireNowSub")]
    class SuppressEmpireCaravansWhenUnhappy
    {
        static void Postfix(IncidentParms parms, ref bool __result)
        {
            if (!__result || parms is null || parms.forced) return;
            if (parms.faction != FindFC.EmpireFaction) return;
            if (FindFC.FactionComp?.ShouldSuppressCaravans() == true)
                __result = false;
        }
    }
}
