using System.Collections.Generic;

namespace FactionColonies
{
    public static class DefenseValidatorRegistry
    {
        private static readonly RegistryList<IDefenseValidator> _list = new RegistryList<IDefenseValidator>();

        internal static void Register(IDefenseValidator v) => _list.Register(v);
        internal static void Unregister(IDefenseValidator v) => _list.Unregister(v);
        internal static void ClearAll() => _list.ClearAll();
        public static IReadOnlyList<IDefenseValidator> Validators => _list.Items;

        /// <summary>
        /// Returns true if all registered validators allow the defense assignment.
        /// <para>Fail-open: a validator that throws is logged and skipped, never blocking the
        /// assignment — a buggy submod validator cannot brick base defense.</para>
        /// </summary>
        public static bool CanDefend(WorldSettlementFC defender, WorldSettlementFC target)
            => RegistryDispatch.All(_list.Items, v => v.CanDefend(defender, target), nameof(IDefenseValidator.CanDefend));
    }
}
