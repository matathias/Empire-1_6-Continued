namespace FactionColonies
{
    /// <summary>
    /// Allows submods to veto or filter defense assignments. Called when a settlement
    /// is considered as a defender for another settlement (both manual selection and auto-defend).
    /// Register implementations via <see cref="DefenseValidatorRegistry"/>.
    /// </summary>
    public interface IDefenseValidator
    {
        /// <summary>
        /// Returns true if <paramref name="defender"/> is allowed to defend <paramref name="target"/>.
        /// Return false to exclude it from the defender list or auto-defend selection.
        /// </summary>
        bool CanDefend(WorldSettlementFC defender, WorldSettlementFC target);
    }
}
