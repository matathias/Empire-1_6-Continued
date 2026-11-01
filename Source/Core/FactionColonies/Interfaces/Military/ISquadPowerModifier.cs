namespace FactionColonies
{
    /// <summary>
    /// Lets submods compose adjustments to a squad's projected combat power
    /// (veterancy bonuses, specialist multipliers, augmentations, etc.). Modifiers
    /// chain: each receives the running <see cref="SquadPower"/> (initially the
    /// base computed from loadout cost + settlement efficiency) and returns the
    /// modified value. Higher <see cref="Priority"/> runs first.
    /// <para>Submods that don't want to apply in a given case should return
    /// <paramref name="currentPower"/> unchanged.</para>
    /// Register via <see cref="SquadPowerRegistry"/>.
    /// </summary>
    public interface ISquadPowerModifier
    {
        /// <summary>Higher priorities run first. Modifiers see the power after all
        /// higher-priority modifiers have run.</summary>
        int Priority { get; }
        /// <summary>Returns the squad's adjusted power. Implementations may consult
        /// <c>squad.outfit</c>, mercs, custom data, etc. Throw-safe: exceptions are
        /// logged and the modifier is skipped (running power is preserved).</summary>
        SquadPower ModifyPower(MercenarySquadFC squad, SquadPower currentPower);
    }
}
