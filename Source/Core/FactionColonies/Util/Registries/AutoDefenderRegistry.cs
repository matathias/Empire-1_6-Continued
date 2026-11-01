using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    public static class AutoDefenderRegistry
    {
        private static readonly RegistryList<IAutoDefender> _list = new RegistryList<IAutoDefender>();

        internal static void Register(IAutoDefender defender) => _list.Register(defender);
        internal static void Unregister(IAutoDefender defender) => _list.Unregister(defender);
        internal static void ClearAll() => _list.ClearAll();
        public static IReadOnlyList<IAutoDefender> Defenders => _list.Items;

        /// <summary>
        /// Finds the strongest available <see cref="IAutoDefender"/> that can defend the given tile
        /// and is stronger than <paramref name="minMilitaryLevel"/>.
        /// </summary>
        public static IAutoDefender FindBestDefender(PlanetTile targetTile, int minMilitaryLevel)
            => RegistryDispatch.Aggregate<IAutoDefender, IAutoDefender>(_list.Items, null, (best, defender) =>
            {
                if (!defender.CanAutoDefend) return best;
                if (defender.MilitaryLevel <= minMilitaryLevel) return best;
                // canTraverseLayers so a defender on a different planet layer (e.g. orbit) isn't treated
                // as infinitely far — TraversalDistanceBetween returns int.MaxValue across layers otherwise.
                int distance = Find.WorldGrid.TraversalDistanceBetween(defender.WorldObject.Tile, targetTile, canTraverseLayers: true);
                if (distance > defender.Range) return best;
                return (best == null || defender.MilitaryLevel > best.MilitaryLevel) ? defender : best;
            }, "FindBestDefender");

        /// <summary>
        /// Finds the <see cref="IAutoDefender"/> wrapping the given <see cref="WorldObject"/>, or null.
        /// Used to notify an external auto-defender of battle completion.
        /// </summary>
        public static IAutoDefender FindByWorldObject(WorldObject obj)
        {
            if (obj == null) return null;
            return RegistryDispatch.First(_list.Items, d => d.WorldObject == obj, nameof(IAutoDefender.WorldObject));
        }
    }
}
