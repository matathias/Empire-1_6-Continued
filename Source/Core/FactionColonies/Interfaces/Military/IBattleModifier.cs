namespace FactionColonies
{
    /// <summary>
    /// Attack-time modifier. Mutates a force snapshot at engagement / display. Use for
    /// battle-context effects that depend on the attacker as well as the defender — terrain,
    /// fortification at the battle tile, traveling fatigue, weather, defensive artillery.
    /// Effects that are properties of the settlement alone belong in
    /// <see cref="ISettlementPowerModifier"/> instead so they cache.
    /// </summary>
    public interface IBattleModifier
    {
        /// <summary>
        /// Pure transformation: read <paramref name="ctx"/> (participants, target, kind) and
        /// mutate <paramref name="force"/>'s <see cref="MilitaryForce.militaryLevel"/>,
        /// <see cref="MilitaryForce.militaryEfficiency"/>, or
        /// <see cref="MilitaryForce.forceRemaining"/>. Must NOT touch any state outside
        /// <paramref name="force"/> — the same modifier may be invoked from the squad-picker UI
        /// for an estimate display, where side effects (logging, history, persistence) would
        /// be incorrect. Op lifecycle hooks are the place for those.
        /// </summary>
        void ModifyForce(BattleForceContext ctx, MilitaryForce force, bool isAttacker);
    }
}
