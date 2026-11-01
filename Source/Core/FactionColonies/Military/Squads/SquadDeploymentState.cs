using System.Linq;
using Verse;
using Verse.AI.Group;
using FactionColonies.util;

namespace FactionColonies
{
    /// <summary>
    /// Per-squad on-map deployment state. Owns the fields that
    /// <see cref="LordJob_DeployMilitary"/> reads and writes during a deploy
    /// (lord ref, current map, player-issued orderLocation/militaryOrder).
    /// </summary>
    public class SquadDeploymentState : IExposable
    {
        protected MercenarySquadFC squad;

        public Lord Lord;
        public bool HasLord => Lord is object;
        public Map Map;
        /// <summary>Player-issued behavior order for the squad's deployment lord.
        /// <see cref="MilitaryOrder.Undefined"/> until the player issues a command (Attack /
        /// Move / Leave). Read by <c>LordJob_DeployMilitary</c>'s state-graph triggers.</summary>
        public MilitaryOrder MilitaryOrder = MilitaryOrder.Undefined;
        /// <summary>Current target tile for the squad's deployment lord. Initially set to the
        /// drop position by <c>MilitaryUtil.SpawnSquad</c>; updated when the player issues a
        /// "move here" command via <see cref="DeployedMilitaryCommandMenu"/>.</summary>
        public IntVec3 OrderLocation;

        public SquadDeploymentState() { }

        public SquadDeploymentState(MercenarySquadFC squad)
        {
            this.squad = squad;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref Lord, "lord");
            Scribe_References.Look(ref Map, "map");
            Scribe_Values.Look(ref MilitaryOrder, "militaryOrder", MilitaryOrder.Undefined);
            Scribe_Values.Look(ref OrderLocation, "orderLocation");
        }

        /// <summary>True if any mercenary pawn is currently spawned on a map. Walks the
        /// merc list rather than reading <c>Operation.battlefieldRef</c> so it works for
        /// squads spawned outside an op (e.g. drop pods that haven't yet wired up their op).
        /// </summary>
        public bool IsPhysicallyDeployed()
        {
            return squad?.mercenaries?.Any(m => m?.pawn?.Map != null) ?? false;
        }

        /// <summary>Migration drain — adopts legacy top-level deployment fields from a pre-refactor
        /// save into this sub-object. Post-refactor saves store deployment under the nested "deployment"
        /// node and leave the legacy buffers at their defaults, so this no-ops for them and preserves the
        /// already-resolved live state.</summary>
        internal void AdoptLegacyValues(Lord lord, Map map, MilitaryOrder order, IntVec3 orderLoc)
        {
            if (lord is null && map is null && order == MilitaryOrder.Undefined
                && orderLoc == default(IntVec3))
                return;
            Lord = lord;
            Map = map;
            MilitaryOrder = order;
            OrderLocation = orderLoc;
        }
    }
}
