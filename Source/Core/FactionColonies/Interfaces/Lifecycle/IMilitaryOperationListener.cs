namespace FactionColonies
{
    /// <summary>
    /// Lifecycle hook for <see cref="MilitaryOperation"/> events (offensive, defensive, deploy).
    /// Register implementations via <see cref="LifecycleRegistry"/>.
    /// </summary>
    public interface IMilitaryOperationListener
    {
        /// <summary>Called immediately after a <see cref="MilitaryOperation"/> is created and registered.
        /// For defensive ops the defending squad is reachable via <c>op.defender.squad</c> when an
        /// Empire settlement is the defender.</summary>
        void OnOperationCreated(MilitaryOperation op);
        /// <summary>Called when an op resolves. Any squads referenced by <c>op.aggressor.squad</c>
        /// or <c>op.defender.squad</c> are freed at this point.</summary>
        void OnOperationResolved(MilitaryOperation op);
        /// <summary>Called after the battle simulation / manual battle has produced a result.</summary>
        void OnBattleResolved(MilitaryOperation op, bool victory, BattleResult result);
    }
}
