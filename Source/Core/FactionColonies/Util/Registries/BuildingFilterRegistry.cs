using System.Collections.Generic;

namespace FactionColonies
{
    public static class BuildingFilterRegistry
    {
        private static readonly RegistryList<BuildingFilter> _list = new RegistryList<BuildingFilter>();

        internal static void Register(BuildingFilter filter) => _list.Register(filter);
        internal static void Unregister(BuildingFilter filter) => _list.Unregister(filter);
        internal static void ClearAll() => _list.ClearAll();
        public static IReadOnlyList<BuildingFilter> Filters => _list.Items;
    }
}
