using System.Collections.Generic;

namespace FactionColonies
{
    /// <summary>
    /// Allows submods to contribute additive or multiplicative modifiers to the Empire Threat Level (ETL).
    /// </summary>
    public static class ThreatScalingRegistry
    {
        private static readonly RegistryList<IThreatScalingContributor> _list = new RegistryList<IThreatScalingContributor>();

        internal static void Register(IThreatScalingContributor contributor) => _list.Register(contributor);
        internal static void Unregister(IThreatScalingContributor contributor) => _list.Unregister(contributor);
        internal static void ClearAll() => _list.ClearAll();
        public static IReadOnlyList<IThreatScalingContributor> Contributors => _list.Items;

        public static double InvokeGetAdditiveContributions(FactionFC faction)
            => RegistryDispatch.Aggregate(_list.Items, 0.0,
                (acc, c) => acc + c.GetAdditiveContribution(faction),
                nameof(IThreatScalingContributor.GetAdditiveContribution));

        public static double InvokeGetMultiplierContributions(FactionFC faction)
            => RegistryDispatch.Aggregate(_list.Items, 1.0,
                (acc, c) => acc * c.GetMultiplicativeContribution(faction),
                nameof(IThreatScalingContributor.GetMultiplicativeContribution));
    }
}
