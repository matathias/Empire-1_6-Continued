using System.Collections.Generic;

namespace FactionColonies
{
    public static class MilitaryTabRegistry
    {
        private static readonly RegistryList<IMilitaryTabEntry> _list = new RegistryList<IMilitaryTabEntry>();

        internal static void Register(IMilitaryTabEntry entry) => _list.Register(entry);
        internal static void Unregister(IMilitaryTabEntry entry) => _list.Unregister(entry);
        internal static void ClearAll() => _list.ClearAll();
        public static IReadOnlyList<IMilitaryTabEntry> Entries => _list.Items;
    }
}
