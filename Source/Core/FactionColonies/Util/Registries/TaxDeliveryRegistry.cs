using System.Collections.Generic;

namespace FactionColonies
{
    public class TaxDeliveryContext
    {
        /// <summary>The tax event being processed.</summary>
        public FCEvent Event;
        /// <summary>The source settlement, if resolvable.</summary>
        public WorldSettlementFC Settlement;
        /// <summary>Set to true by an interceptor to indicate it has redirected the event. Stops further interceptors.</summary>
        public bool Redirected = false;
        /// <summary>Set internally by <see cref="TaxDeliveryRegistry.InvokeTryDeliverGoods"/> when an interceptor consumes delivery.</summary>
        public bool Delivered = false;

        public TaxDeliveryContext(FCEvent evt, WorldSettlementFC settlement)
        {
            Event = evt;
            Settlement = settlement;
        }
    }

    public static class TaxDeliveryRegistry
    {
        private static readonly RegistryList<ITaxDeliveryInterceptor> _list = new RegistryList<ITaxDeliveryInterceptor>();

        internal static void Register(ITaxDeliveryInterceptor interceptor) => _list.Register(interceptor);
        internal static void Unregister(ITaxDeliveryInterceptor interceptor) => _list.Unregister(interceptor);
        internal static void ClearAll() => _list.ClearAll();
        public static IReadOnlyList<ITaxDeliveryInterceptor> Interceptors => _list.Items;

        /// <summary>
        /// Invokes <see cref="ITaxDeliveryInterceptor.OnTaxEventCreated"/> on all interceptors,
        /// stopping at the first one that sets <see cref="TaxDeliveryContext.Redirected"/> to true.
        /// </summary>
        public static void InvokeOnTaxEventCreated(TaxDeliveryContext context)
        {
            RegistryDispatch.First(_list.Items, interceptor =>
            {
                interceptor.OnTaxEventCreated(context);
                return context.Redirected;
            }, nameof(ITaxDeliveryInterceptor.OnTaxEventCreated));
        }

        /// <summary>
        /// Invokes <see cref="ITaxDeliveryInterceptor.TryDeliverGoods"/> on all interceptors,
        /// stopping at the first one that returns true.
        /// Returns true if any interceptor consumed the delivery.
        /// </summary>
        public static bool InvokeTryDeliverGoods(TaxDeliveryContext context)
        {
            ITaxDeliveryInterceptor winner = RegistryDispatch.First(_list.Items,
                interceptor => interceptor.TryDeliverGoods(context),
                nameof(ITaxDeliveryInterceptor.TryDeliverGoods));
            if (winner is object)
            {
                context.Delivered = true;
                return true;
            }
            return false;
        }
    }
}
