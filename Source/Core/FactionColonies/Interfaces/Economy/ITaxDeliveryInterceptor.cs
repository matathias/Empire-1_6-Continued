namespace FactionColonies
{
    /// <summary>
    /// Allows submods to intercept and redirect tax delivery events at two points:
    /// when the event is queued (to redirect the destination) and when it fires
    /// (to handle delivery of goods to a non-standard location).
    /// Register implementations via <see cref="TaxDeliveryRegistry"/>.
    /// </summary>
    public interface ITaxDeliveryInterceptor
    {
        /// <summary>
        /// Called when a taxColony event is about to be queued via <see cref="FactionFC.AddEvent"/>,
        /// after goods consolidation. Implementations can mutate <c>context.Event.location</c> and
        /// <c>context.Event.timeTillTrigger</c> to redirect the delivery destination.
        /// Set <c>context.Redirected = true</c> to prevent further interceptors from running.
        /// </summary>
        void OnTaxEventCreated(TaxDeliveryContext context);

        /// <summary>
        /// Called when a taxColony event fires and goods are about to be delivered.
        /// Return true to consume the delivery (this interceptor handled the goods).
        /// Return false to let the next interceptor or default delivery logic handle it.
        /// </summary>
        bool TryDeliverGoods(TaxDeliveryContext context);
    }
}
