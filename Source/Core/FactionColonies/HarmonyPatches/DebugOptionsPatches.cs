using HarmonyLib;

namespace FactionColonies;

[HarmonyPatch(typeof(WorldPawns), "PassToWorld")]
class MercenaryPassToWorld
{
    static bool Prefix(Pawn pawn, PawnDiscardDecideMode discardMode = PawnDiscardDecideMode.Decide)
    {
        FactionFC faction = FindFC.FactionComp;
        return faction?.military is null || !faction.military.IsMercenaryPawn(pawn);
    }
}
