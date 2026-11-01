using System.Collections.Generic;

namespace FactionColonies
{
    public static class SettlementButtonRegistry
    {
        private static readonly RegistryList<ISettlementWindowButton> _list = new RegistryList<ISettlementWindowButton>();

        internal static void Register(ISettlementWindowButton entry) => _list.Register(entry);
        internal static void Unregister(ISettlementWindowButton entry) => _list.Unregister(entry);
        internal static void ClearAll() => _list.ClearAll();
        public static IReadOnlyList<ISettlementWindowButton> Entries => _list.Items;
    }
}
