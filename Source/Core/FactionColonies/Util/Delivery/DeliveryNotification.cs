using System;
using Verse;
using RimWorld;

namespace FactionColonies.util
{
    /* Letter/message generation for tax & event deliveries. Honors
       FCSettings.taxNotificationMode (All / LetterOnly / MessageOnly / None) and falls
       back to a generic "goods received" letter when the FCEvent didn't supply its own. */
    public static class DeliveryNotification
    {
        public static string ShuttleEventInjuredString
        {
            get
            {
                if (FactionCache.TechTransportPods.IsFinished)
                {
                    if (ModsConfig.RoyaltyActive)
                    {
                        return "FCTransportingInjuredShuttle".Translate();
                    }
                    return "FCTransportingInjuredDropPod".Translate();
                }
                return "FCTransportingInjuredCaravan".Translate();
            }
        }

        public static string ShuttleEventInjuredLostString
        {
            get
            {
                if (FactionCache.TechTransportPods.IsFinished)
                {
                    if (ModsConfig.RoyaltyActive)
                    {
                        return "FCTransportingInjuredShuttleLost".Translate();
                    }
                    return "FCTransportingInjuredDropPodLost".Translate();
                }
                return "FCTransportingInjuredCaravanLost".Translate();
            }
        }

        public static void MakeDeliveryLetterAndMessage(FCEvent evt)
        {
            try
            {
                var notificationMode = FCSettings.taxNotificationMode;
                bool showLetter = notificationMode == TaxNotificationMode.All || notificationMode == TaxNotificationMode.LetterOnly;
                bool showMessage = notificationMode == TaxNotificationMode.All || notificationMode == TaxNotificationMode.MessageOnly;

                if (showLetter)
                {
                    if (evt.let != null)
                    {
                        evt.let.lookTargets = evt.goods;
                        Find.LetterStack.ReceiveLetter(evt.let);
                    }
                    else
                    {
                        string eventLabel = evt.def?.label?.ToLower() ?? "delivery";
                        Find.LetterStack.ReceiveLetter("FCGoodsReceivedFollowing".Translate(eventLabel), evt.goods.ToLetterString(), RimWorld.LetterDefOf.PositiveEvent, evt.goods);
                    }
                }

                if (showMessage && evt.msg != null)
                {
                    evt.msg.lookTargets = evt.goods;
                    Messages.Message(evt.msg);
                }

                if (evt.isDelayed) Messages.Message("FCDeliveryHeldUpArriving".Translate(), evt.goods, MessageTypeDefOf.PositiveEvent);
            }
            catch (Exception ex)
            {
                LogUtil.ErrorOnce("MakeDeliveryLetterAndMessage failed to attach targets to the message: " + ex, 908347458);
            }
        }
    }
}
