using HarmonyLib;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Keeps the Empire's stored world pawns out of vanilla's pawn "redress" pool.
    ///
    /// Visitor groups, trader caravans, and raids generate their pawns via
    /// PawnGenerator.GeneratePawn with forceGenerateNewPawn == false, which lets
    /// GenerateOrRedressPawnInternal reuse an existing WorldPawnSituation.Free world pawn
    /// (RedressPawn re-rolls its apparel/weapon/kind, then WorldPawns.RemovePawn drops the
    /// KeepForever hold). Some submods park pawns in  the world pool with KeepForever precisely
    /// to preserve them, and they share the Empire's faction and race, so an Empire visitor/trader
    /// group would happily redress one — mangling its gear and leaving it garbage-collectable.
    /// IsValidCandidateToRedress is the single gate all redress branches pass through, so reject
    /// our kept pawns here.
    /// </summary>
    [HarmonyPatch(typeof(PawnGenerator), "IsValidCandidateToRedress")]
    static class Patch_IsValidCandidateToRedress_ProtectEmpireWorldPawns
    {
        static void Postfix(Pawn pawn, ref bool __result)
        {
            if (!__result || pawn is null) return;
            if (FindFC.IsEmpireFaction(pawn.Faction)
                && Find.WorldPawns is object
                && Find.WorldPawns.ForcefullyKeptPawns.Contains(pawn))
            {
                __result = false;
            }
        }
    }
}
