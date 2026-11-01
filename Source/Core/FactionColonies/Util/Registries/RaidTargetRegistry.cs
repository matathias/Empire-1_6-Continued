using RimWorld.Planet;
using System.Collections.Generic;

namespace FactionColonies
{
    public static class RaidTargetRegistry
    {
        private static readonly RegistryList<IRaidTarget> _list = new RegistryList<IRaidTarget>();

        internal static void Register(IRaidTarget target) => _list.Register(target);
        internal static void Unregister(IRaidTarget target) => _list.Unregister(target);
        internal static void ClearAll() => _list.ClearAll();
        public static IReadOnlyList<IRaidTarget> Targets => _list.Items;

        /// <summary>
        /// Finds the <see cref="IRaidTarget"/> wrapping the given <see cref="WorldObject"/>, or null.
        /// </summary>
        public static IRaidTarget FindByWorldObject(WorldObject obj)
            => RegistryDispatch.First(_list.Items, t => t.WorldObject == obj, nameof(IRaidTarget.WorldObject));
    }
}
