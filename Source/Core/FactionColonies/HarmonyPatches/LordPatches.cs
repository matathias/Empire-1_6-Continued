using HarmonyLib;
using Verse.AI.Group;

namespace FactionColonies;

// Tags every Empire trade caravan with a "home" settlement when its Lord is created. MakeNewLord is
// the single chokepoint for both base-game and Mercantile-policy caravans, and exposes the faction,
// lord job, starting pawns, and the resulting Lord. Tagging at spawn (rather than resolving at death)
// keeps the home stable even if the player kills the trader pawn first.
[HarmonyPatch(typeof(LordMaker), nameof(LordMaker.MakeNewLord))]
class TagEmpireCaravanHome
{
    static void Postfix(Faction faction, LordJob lordJob, Lord __result, IEnumerable<Pawn> startingPawns)
    {
        if (__result is null || faction != FindFC.EmpireFaction) return;
        if (lordJob is not LordJob_TradeWithColony) return;

        FactionFC fc = FindFC.FactionComp;
        if (fc is null || startingPawns is null) return;

        TraderKindDef tk = null;
        foreach (Pawn p in startingPawns)
        {
            if (p?.TraderKind is not null)
            {
                tk = p.TraderKind;
                break;
            }
        }

        WorldSettlementFC home = EmpireDeathPenaltyUtil.ResolveCaravanHome(tk);
        if (home is not null)
            fc.RegisterCaravanHome(__result.loadID, home);
    }
}

// Drops the caravan-home tag when its Lord ends (caravan leaves or is disbanded).
[HarmonyPatch(typeof(LordManager), nameof(LordManager.RemoveLord))]
class UntagEmpireCaravanHome
{
    static void Postfix(Lord oldLord)
    {
        if (oldLord is null) return;
        FindFC.FactionComp?.UnregisterCaravanHome(oldLord.loadID);
    }
}
