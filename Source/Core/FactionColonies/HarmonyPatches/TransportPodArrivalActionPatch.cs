using HarmonyLib;

namespace FactionColonies;

[HarmonyPatch(typeof(TransportersArrivalAction_LandInSpecificCell))]
[HarmonyPatch("Arrived")]
class WorldSettlementTransportersArrivePatch
{
    [ThreadStatic]
    private static List<Pawn> pendingPawns;

    // ___mapParent: Harmony injects the private TransportersArrivalAction_LandInSpecificCell.mapParent field.
    private static void Prefix(MapParent ___mapParent, List<ActiveTransporterInfo> transporters)
    {
        pendingPawns = null;
        if (___mapParent is not WorldSettlementFC)
            return;

        List<Pawn> pawns = [];
        foreach (ActiveTransporterInfo info in transporters)
        {
            foreach (Thing thing in info.innerContainer)
            {
                if (thing is Pawn pawn)
                    pawns.Add(pawn);
            }
        }
        if (pawns.Count > 0)
            pendingPawns = pawns;
    }

    private static void Postfix(MapParent ___mapParent, PlanetTile tile)
    {
        List<Pawn> pawns = pendingPawns;
        pendingPawns = null;
        if (pawns is null || pawns.Count == 0) return;

        if (___mapParent is WorldSettlementFC settlement)
        {
            settlement.MilitaryComp?.AddToDefenceFromList(pawns, tile);
        }
    }
}
