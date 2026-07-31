using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FactionColonies.util
{
    /* Thin dispatcher for FCEvent-driven deliveries. Owns the public Action /
       CreateDeliveryEvent entry points; the actual logistics live in
       DeliveryLogistics and the letter/message generation lives in
       DeliveryNotification. */
    public static class DeliveryEvent
    {
        public static void CreateDeliveryEvent(List<Thing> things, PlanetTile source, Letter let = null, Message msg = null)
        {
            CreateDeliveryEvent(new FCEvent()
            {
                source = source,
                goods = things,
                customDescription = "",
                timeTillTrigger = Find.TickManager.TicksGame + 10,
                let = let,
                msg = msg
            });
        }

        public static void CreateDeliveryEvent(FCEvent evtParams)
        {
            FCEvent evt = FCEventMaker.MakeEvent(FCEventDefOf.deliveryArrival);
            evt.source = evtParams.source;
            evt.goods = evtParams.goods;
            evt.customDescription = evtParams.customDescription;
            evt.hasCustomDescription = true;
            evt.timeTillTrigger = evtParams.timeTillTrigger;
            evt.let = evtParams.let;
            evt.msg = evtParams.msg;
            evt.isDelayed = evtParams.isDelayed;
            /* Carry the reschedule count across the fresh event so the SendShuttle cap actually
               accumulates instead of resetting to 0 each 0.4h retry. */
            evt.deliveryAttempts = evtParams.deliveryAttempts;
            evt.deliveryMode = evtParams.deliveryMode;
            FindFC.EventManager.AddEvent(evt);
        }

        public static void Action(FCEvent evt)
        {
            Action(evt, FindFC.Settlements?.FirstOrFallback(settlement => settlement.Tile == evt.source)?.BuildingsComp?.HasBuilding(BuildingFCDefOf.shuttlePort) ?? false);
        }

        public static void Action(FCEvent evt, Letter let, Message msg = null, bool CanUseShuttle = false)
        {
            evt.let = let;
            evt.msg = msg;
            Action(evt, CanUseShuttle || (FindFC.Settlements?.FirstOrFallback(settlement => settlement.Tile == evt.source)?.BuildingsComp?.HasBuilding(BuildingFCDefOf.shuttlePort) ?? false));
        }

        public static void Action(FCEvent evt, bool canUseShuttle)
        {
            try
            {
                /* All Send* paths dereference FindFC.TaxMap on entry; if it's null we
                   must not invoke them. Tax-spot placement is null-safe (PaymentUtil
                   warns + destroys the Thing rather than crash). */
                if (FindFC.TaxMap is null)
                {
                    LogUtil.Warning("DeliveryEvent.Action: no tax map available; routing to tax-spot placement. "
                        + "Set a capital tile and a tax map on the faction main tab.");
                    DeliveryLogistics.SpawnOnTaxSpot(evt);
                    return;
                }

                TaxDeliveryMode taxDeliveryMode = evt.deliveryMode != TaxDeliveryMode.None
                    ? evt.deliveryMode
                    : DeliveryLogistics.TaxDeliveryModeForSettlement(canUseShuttle, evt.source);

                switch (taxDeliveryMode)
                {
                    case TaxDeliveryMode.Caravan:
                        DeliveryLogistics.SendCaravan(evt);
                        break;
                    case TaxDeliveryMode.DropPod:
                        DeliveryLogistics.SendDropPod(evt);
                        break;
                    case TaxDeliveryMode.Shuttle:
                        DeliveryLogistics.SendShuttle(evt);
                        break;
                    default:
                        DeliveryLogistics.SpawnOnTaxSpot(evt);
                        break;
                }
            }
            catch (Exception e)
            {
                LogUtil.ErrorOnce("Critical delivery failure, spawning things on tax spot instead! Message: " + e.Message + " StackTrace: " + e.StackTrace + " Source: " + e.Source, 77239232);
                evt.goods.ForEach(thing => PaymentUtil.PlaceThing(thing));
            }
        }
    }
}
