using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    [HarmonyPatch(typeof(TransportersArrivalAction_LandInSpecificCell))]
    [HarmonyPatch("Arrived")]
    class WorldSettlementTransportersArrivePatch
    {
        [ThreadStatic]
        private static List<Pawn> pendingPawns;

        private static void Prefix(TransportersArrivalAction_LandInSpecificCell __instance,
            List<ActiveTransporterInfo> transporters)
        {
            pendingPawns = null;
            if (!(Traverse.Create(__instance).Field("mapParent").GetValue() is WorldSettlementFC))
                return;

            List<Pawn> pawns = new List<Pawn>();
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

        private static void Postfix(TransportersArrivalAction_LandInSpecificCell __instance,
            PlanetTile tile)
        {
            List<Pawn> pawns = pendingPawns;
            pendingPawns = null;
            if (pawns is null || pawns.Count == 0) return;

            if (Traverse.Create(__instance).Field("mapParent").GetValue() is WorldSettlementFC settlement)
            {
                settlement.MilitaryComp?.AddToDefenceFromList(pawns, tile);
            }
        }
    }
}
