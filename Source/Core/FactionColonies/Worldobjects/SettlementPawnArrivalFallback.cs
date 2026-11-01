using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /* Handles whatever is left in the pods after an "add to settlement" arrival has absorbed the
       pawns it wanted: pawns the chosen arrival comp did not accept (e.g. a colonist in a prisoner
       pod) plus any non-pawn items. Nothing is ever destroyed.

       - Leftover pawns -> a player caravan at the nearest passable tile (items ride along). This
         mirrors the engine's own native fallback (TransportersArrivalAction_FormCaravan) so the
         player keeps control.
       - Items only (the common case once all pawns are absorbed) -> Empire's delivery pipeline,
         which routes the goods home to the tax map (with PaymentUtil.PlaceThing as a failsafe). */
    public static class SettlementPawnArrivalFallback
    {
        public static void RouteLeftovers(List<ActiveTransporterInfo> transporters, PlanetTile tile, WorldSettlementFC settlement)
        {
            List<Pawn> leftoverPawns = new List<Pawn>();
            List<Thing> leftoverItems = new List<Thing>();

            for (int i = 0; i < transporters.Count; i++)
            {
                ThingOwner inner = transporters[i].innerContainer;
                for (int n = inner.Count - 1; n >= 0; n--)
                {
                    Thing thing = inner[n];
                    if (thing is Pawn p) leftoverPawns.Add(p);
                    else leftoverItems.Add(thing);
                    inner.Remove(thing);
                }
            }

            if (leftoverPawns.Count == 0 && leftoverItems.Count == 0) return;

            if (leftoverPawns.Count > 0)
            {
                PlanetTile foundTile;
                if (!GenWorldClosest.TryFindClosestPassableTile(tile, out foundTile))
                    foundTile = tile;

                Caravan caravan = CaravanMaker.MakeCaravan(leftoverPawns, Faction.OfPlayer, foundTile, true);
                for (int j = 0; j < leftoverItems.Count; j++)
                    CaravanInventoryUtility.GiveThing(caravan, leftoverItems[j]);

                Messages.Message("MessageTransportPodsArrived".Translate(), caravan, MessageTypeDefOf.TaskCompletion);
            }
            else
            {
                // No pawns to carry the goods: route them home through Empire's delivery pipeline.
                DeliveryEvent.CreateDeliveryEvent(leftoverItems, settlement?.Tile ?? tile);
            }
        }
    }
}
