using RimWorld.Planet;

namespace FactionColonies
{
    /// <summary>
    /// Snapshot of the data a force modifier needs about a pending or simulated battle.
    /// Built either from a real <see cref="MilitaryOperation"/> at engagement time, or
    /// constructed directly by the squad-attack window for the displayed-power estimate.
    /// Both code paths run modifiers through the same registry; modifiers therefore see the
    /// same shape of input regardless of whether the battle is real.
    /// </summary>
    public class BattleForceContext
    {
        /// <summary>The military job (raid, capture, enslave, ...). May be null in synthetic contexts.</summary>
        public MilitaryJobDef kind;
        /// <summary>The world tile the battle resolves on.</summary>
        public PlanetTile targetTile;
        /// <summary>The world object being attacked (typically a Settlement).</summary>
        public WorldObject targetObject;
        /// <summary>Attacker side: faction, force, squad, homeSettlement.</summary>
        public MilitaryOperationParticipant aggressor;
        /// <summary>Defender side: faction, force, squad, homeSettlement.</summary>
        public MilitaryOperationParticipant defender;
    }
}
