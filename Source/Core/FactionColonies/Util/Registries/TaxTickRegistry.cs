using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    public static class TaxTickRegistry
    {
        private static readonly RegistryList<ITaxTickParticipant> _list = new RegistryList<ITaxTickParticipant>();

        internal static void Register(ITaxTickParticipant taxer) => _list.Register(taxer);
        internal static void Unregister(ITaxTickParticipant taxer) => _list.Unregister(taxer);
        internal static void ClearAll() => _list.ClearAll();
        public static IReadOnlyList<ITaxTickParticipant> Taxers => _list.Items;

        public static void InvokePreTaxResolution(FactionFC faction)
            => RegistryDispatch.Each(_list.Items, t => t.PreTaxResolution(faction), nameof(ITaxTickParticipant.PreTaxResolution));

        public static void InvokePostTaxResolution(FactionFC faction)
            => RegistryDispatch.Each(_list.Items, t => t.PostTaxResolution(faction), nameof(ITaxTickParticipant.PostTaxResolution));

        public static void InvokePreSettlementCreateTax(WorldSettlementFC settlement)
        {
            RegistryDispatch.EachInvalidating(_list.Items,
                t => t.PreSettlementCreateTax(settlement),
                () => settlement.InvalidateStatCache(),
                nameof(ITaxTickParticipant.PreSettlementCreateTax));
            settlement.InvalidateStatCache();
        }

        public static void InvokePostSettlementCreateTax(WorldSettlementFC settlement, ref int silverAmount, List<Thing> titheThings)
        {
            // Local copies for the closure (out/ref params can't be captured directly across the iteration).
            int silver = silverAmount;
            List<Thing> tithe = titheThings;
            RegistryDispatch.EachInvalidating(_list.Items,
                t => t.PostSettlementCreateTax(settlement, ref silver, tithe),
                () => settlement.InvalidateStatCache(),
                nameof(ITaxTickParticipant.PostSettlementCreateTax));
            silverAmount = silver;
            settlement.InvalidateStatCache();
        }
    }
}
