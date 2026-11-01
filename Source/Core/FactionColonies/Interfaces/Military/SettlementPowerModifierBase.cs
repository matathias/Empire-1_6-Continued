using RimWorld.Planet;

namespace FactionColonies
{
    /// <summary>
    /// Convenience base for <see cref="ISettlementPowerModifier"/> implementations that derive a
    /// settlement's <see cref="EnemyPower.level"/> from a mod-specific settlement component. Handles
    /// the shared null-guards, the <c>power.level</c> assignment, and the debug log line; subclasses
    /// supply only <see cref="TryGetLevel"/> and <see cref="LogLabel"/>. Used by the RimWar / World
    /// Domination compat assemblies.
    /// </summary>
    public abstract class SettlementPowerModifierBase : ISettlementPowerModifier
    {
        /// <summary>Reads <paramref name="settlement"/>'s mod component and produces an
        /// <see cref="EnemyPower.level"/>. Return false to leave the power unchanged (component
        /// missing, or strength not yet meaningful).</summary>
        protected abstract bool TryGetLevel(Settlement settlement, out double level);

        /// <summary>Short tag for the debug log line (typically the mod name).</summary>
        protected abstract string LogLabel { get; }

        public void ModifySettlementPower(Settlement settlement, EnemyPower power)
        {
            if (settlement is null || power is null) return;
            if (!TryGetLevel(settlement, out double level)) return;
            power.level = level;
            LogUtil.Message($"{LogLabel}: {settlement.Name} -> EnemyPower level {level:0.0}");
        }
    }
}
