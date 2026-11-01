namespace FactionColonies
{
    /// <summary>
    /// Allows submods to veto squad assignments. Called before a squad is assigned to a
    /// settlement. Receives the actual <see cref="MercenarySquadFC"/> instance so validators
    /// can read per-squad state (current loadout cost, mercenary count, cooldown, etc.) rather
    /// than just the source template. Register implementations via <see cref="SquadAssignmentRegistry"/>.
    /// </summary>
    public interface ISquadAssignmentValidator
    {
        /// <summary>
        /// Returns true if <paramref name="squad"/> can be assigned to <paramref name="settlement"/>.
        /// If false, <paramref name="reason"/> is shown to the player as a rejection message.
        /// </summary>
        bool CanAssign(WorldSettlementFC settlement, MercenarySquadFC squad, out string reason);
    }
}
