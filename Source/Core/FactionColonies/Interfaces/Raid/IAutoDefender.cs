using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Allows external mods to register world objects as auto-defenders for Empire settlements
    /// (and other <see cref="IRaidTarget"/>s). Defenders create a <see cref="MilitaryForce"/> and
    /// are placed on cooldown after battle resolution.
    /// Register implementations via <see cref="AutoDefenderRegistry"/>.
    /// </summary>
    public interface IAutoDefender
    {
        WorldObject WorldObject { get; }
        int MilitaryLevel { get; }
        /// <summary>Maximum tile distance for auto-defense eligibility.</summary>
        int Range { get; }
        /// <summary>True if the defender is available (enabled, not busy, not packing, etc.).</summary>
        bool CanAutoDefend { get; }
        MilitaryForce CreateDefendingForce();
        /// <summary>Called when this defender is committed to an op during the pre-battle warning window
        /// (auto-selection or manual assignment), BEFORE the battle starts. Implementations should mark
        /// themselves busy so a second concurrent attack won't double-book them. A pledge is always
        /// followed by either <see cref="OnDefenseReplaced"/> (if swapped out) or
        /// <see cref="OnDefenseStarted"/> (at engagement).</summary>
        void OnDefensePledged(WorldObject target);
        void OnDefenseStarted(WorldObject target);
        void OnDefenseComplete(bool won, BattleResult result);
        /// <summary>Called when this defender is replaced by another force (not defeated).</summary>
        void OnDefenseReplaced();
        /// <summary>
        /// Returns pawns to fight in a manual battle, or null to generate pawns from force points.
        /// Implementations should remove pawns from their source before returning them.
        /// </summary>
        List<Pawn> GetDefendingPawns();
        /// <summary>
        /// Called after a manual battle ends to return surviving pawns.
        /// Pawns will already be despawned from the battle map.
        /// </summary>
        void ReturnDefendingPawns(List<Pawn> pawns);
    }
}
