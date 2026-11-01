using FactionColonies.util;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// A DefModExtension for FCEventDef that provides a type-safe hook into event
    /// processing without Harmony patches.
    ///
    /// Usage in XML:
    ///   <modExtensions>
    ///     <li Class="MyMod.MyEventHandler"/>
    ///   </modExtensions>
    ///
    /// In C#, subclass this and override the virtual methods you need.
    /// </summary>
    public class FCEventHandlerExtension : DefModExtension
    {
        /// <summary>
        /// Called once after the event is enqueued in <see cref="FCEventManager.AddEvent"/>.
        /// Default impl: applies <c>def.statModifiers</c> + <c>def.permanentStatModifiers</c>
        /// to <c>settlementTraitLocations</c> (or all settlements if untargeted) via
        /// <c>EventStatModifierApplier.Apply</c>.
        /// </summary>
        public virtual void OnEventQueued(FCEvent evt, FactionFC faction)
        {
            EventStatModifierApplier.Apply(evt, faction);
        }

        /// <summary>
        /// Called once when the event leaves the queue (transitioning to Completed) inside
        /// <see cref="FCEventManager.Remove"/> / <c>RemoveWhere</c>. Default impl: removes
        /// the stat modifiers added in <see cref="OnEventQueued"/> and subtracts
        /// <c>def.prosperityLost</c>. Permanent modifiers are intentionally NOT removed.
        /// </summary>
        public virtual void OnEventExpired(FCEvent evt, FactionFC faction)
        {
            EventStatModifierApplier.Remove(evt, faction);
        }

        /// <summary>
        /// Called to resolve a custom event. Return true if handled (skips built-in resolution).
        /// Generic post-processing (loot, stat cleanup, cascading events, OnEventTriggered)
        /// still runs afterward regardless of return value.
        /// </summary>
        public virtual bool ResolveEvent(FCEvent evt, FactionFC faction)
        {
            return false;
        }

        /// <summary>
        /// Called after all standard event processing has completed (loot delivery,
        /// trait removal, prosperity changes, following events).
        /// </summary>
        public virtual void OnEventTriggered(FCEvent evt)
        {
        }

        /// <summary>
        /// Called during settlement removal for each active event that wasn't already
        /// handled by the core cleanup logic. Return true to cancel this event.
        /// </summary>
        public virtual bool ShouldCancelOnSettlementRemoval(FCEvent evt, WorldSettlementFC settlement)
        {
            return false;
        }

        /* Option Display Hooks */
        /// <summary>
        /// Called to get dynamic label text for an option in this event's option window.
        /// Return null to use the default label from XML.
        /// </summary>
        public virtual string GetDynamicOptionLabel(FCOptionDef option, FCEvent parentEvent)
        {
            return null;
        }

        /// <summary>
        /// Called to get a dynamic success chance for an option in this event's option window.
        /// Return a negative value to use the static <see cref="FCOptionDef.baseChanceOfSuccess"/>.
        /// </summary>
        public virtual float GetDynamicOptionSuccessChance(FCOptionDef option, FCEvent parentEvent)
        {
            return -1f;
        }

        /// <summary>
        /// Called to check whether an option should be available based on runtime state.
        /// Checked after policy requirements. Return false with a reason to grey out the option.
        /// </summary>
        public virtual bool IsOptionAvailable(FCOptionDef option, FCEvent parentEvent, out string unavailableReason)
        {
            unavailableReason = null;
            return true;
        }
    }

    /// <summary>Internal handler for the deliveryArrival event.</summary>
    internal class FCEventHandlerExtension_DeliveryArrival : FCEventHandlerExtension
    {
        public override void OnEventTriggered(FCEvent evt)
        {
            DeliveryEvent.Action(evt);
        }
    }
}
