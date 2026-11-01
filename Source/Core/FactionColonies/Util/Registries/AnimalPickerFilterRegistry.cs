using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Registry of <see cref="IAnimalPickerFilter"/> gates consulted by the military unit
    /// designer's animal/mount pickers. All registered filters must allow a kind for it to be
    /// offered (AND semantics); with none registered, every kind is allowed (base behavior).
    /// Cleared on game dispose / load via <see cref="EmpireRegistry.ClearAll"/>.
    /// </summary>
    public static class AnimalPickerFilterRegistry
    {
        private static readonly RegistryList<IAnimalPickerFilter> _list = new RegistryList<IAnimalPickerFilter>();

        internal static void Register(IAnimalPickerFilter filter) => _list.Register(filter);
        internal static void Unregister(IAnimalPickerFilter filter) => _list.Unregister(filter);
        internal static void ClearAll() => _list.ClearAll();
        public static IReadOnlyList<IAnimalPickerFilter> Filters => _list.Items;

        /// <summary>True if every registered filter allows the kind. No filters => allowed.</summary>
        public static bool IsAllowed(PawnKindDef animal)
        {
            IReadOnlyList<IAnimalPickerFilter> filters = _list.Items;
            foreach (IAnimalPickerFilter filter in filters)
            {
                if (!filter.IsAnimalAllowed(animal))
                    return false;
            }
            return true;
        }
    }
}
