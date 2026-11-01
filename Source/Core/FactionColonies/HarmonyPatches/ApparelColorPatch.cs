using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Applies faction colors to apparel on Empire pawns generated through vanilla's
    /// pawn generation pipeline (e.g. trader caravans, visitors).
    /// Military pawns are unaffected — their generation-time apparel is stripped and
    /// replaced by SquadEquipmentTracker.EquipPawn() which applies colors independently.
    /// </summary>
    [HarmonyPatch(typeof(PawnApparelGenerator))]
    [HarmonyPatch("PostProcessApparel")]
    class Patch_PawnApparelGenerator_PostProcessApparel
    {
        static void Postfix(Apparel apparel, Pawn pawn)
        {
            if (pawn is null) return;
            if (pawn.Faction != FindFC.EmpireFaction) return;
            FactionFC factionComp = FindFC.FactionComp;
            if (factionComp == null) return;
            if (!factionComp.hasFactionColor && !factionComp.hasFactionColorSecondary) return;
            Color resolved = factionComp.ResolveApparelColor(apparel.def);
            apparel.SetColor(resolved, reportFailure: false);
        }
    }
}
