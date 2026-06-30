using System.Collections.Generic;

namespace FactionColonies
{
    public static class DailyAccrualRegistry
    {
        private static readonly RegistryList<IDailyAccrualParticipant> _list = new RegistryList<IDailyAccrualParticipant>();

        internal static void Register(IDailyAccrualParticipant p) => _list.Register(p);
        internal static void Unregister(IDailyAccrualParticipant p) => _list.Unregister(p);
        internal static void ClearAll() => _list.ClearAll();
        public static IReadOnlyList<IDailyAccrualParticipant> Participants => _list.Items;

        public static void InvokePostDailyAccrual(FactionFC faction)
            => RegistryDispatch.Each(_list.Items, p => p.PostDailyAccrual(faction),
                nameof(IDailyAccrualParticipant.PostDailyAccrual));
    }
}
