namespace FactionColonies
{
    /// <summary>
    /// A WorldObjectComp interface for contributing dynamic, per-resource production bonuses.
    /// Unlike <see cref="IStatModifierProvider"/> (which operates at the stat level), this operates
    /// directly on <see cref="ResourceFC"/> instances, letting comps target specific resources.
    /// <para>Results are queried during production calculation (lazy-cached by ResourceFC's dirty flags).
    /// Caches are automatically invalidated after all lifecycle events (building, settlement, military,
    /// research, tax hooks). Only call <c>((WorldSettlementFC)parent).InvalidateResourceCaches()</c>
    /// manually if changing values outside a lifecycle callback.</para>
    /// </summary>
    public interface IResourceProductionModifier
    {
        /// <summary>
        /// Returns an additive production bonus for the given resource. Return 0 for no effect.
        /// </summary>
        double GetResourceAdditiveModifier(ResourceFC resource);
        /// <summary>
        /// Returns a multiplicative production modifier for the given resource. Return 1 for no effect.
        /// </summary>
        double GetResourceMultiplierModifier(ResourceFC resource);
        /// <summary>
        /// Returns a description of this comp's additive contribution for the additive tooltip.
        /// Return null or empty if not contributing additively to this resource.
        /// </summary>
        string GetResourceAdditiveDesc(ResourceFC resource);
        /// <summary>
        /// Returns a description of this comp's multiplier contribution for the multiplier tooltip.
        /// Return null or empty if not contributing a multiplier to this resource.
        /// </summary>
        string GetResourceMultiplierDesc(ResourceFC resource);
    }
}
