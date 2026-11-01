using RimWorld;
using System.Collections.Generic;

namespace FactionColonies
{
    public static class RaidWeightRegistry
    {
        private static readonly RegistryList<IRaidWeightProvider> _list = new RegistryList<IRaidWeightProvider>();

        internal static void Register(IRaidWeightProvider provider) => _list.Register(provider);
        internal static void Unregister(IRaidWeightProvider provider) => _list.Unregister(provider);
        internal static void ClearAll() => _list.ClearAll();
        public static IReadOnlyList<IRaidWeightProvider> Providers => _list.Items;

        /// <summary>
        /// Returns the combined weight multiplier for a settlement from all registered providers.
        /// If no providers are registered, returns 1.0 (no modification).
        /// </summary>
        public static float GetCombinedWeight(WorldSettlementFC settlement, Faction attackingFaction)
            => _list.Count == 0
                ? 1f
                : RegistryDispatch.Aggregate(_list.Items, 1f,
                    (acc, p) => acc * p.GetSettlementRaidWeight(settlement, attackingFaction),
                    nameof(IRaidWeightProvider.GetSettlementRaidWeight));
    }
}
