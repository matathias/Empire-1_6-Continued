using System.Collections.Generic;

namespace FactionColonies
{
    public static class MainTableRegistry
    {
        private static readonly RegistryList<IMainTabWindowOverview> _list = new RegistryList<IMainTabWindowOverview>();

        internal static void Register(IMainTabWindowOverview tab) => _list.Register(tab);
        internal static void Unregister(IMainTabWindowOverview tab) => _list.Unregister(tab);
        internal static void ClearAll() => _list.ClearAll();
        public static IReadOnlyList<IMainTabWindowOverview> Tabs => _list.Items;

        public static void InvokePostCloseWindow()
            => RegistryDispatch.Each(_list.Items, t => t.PostCloseWindow(), nameof(IMainTabWindowOverview.PostCloseWindow));
    }
}
