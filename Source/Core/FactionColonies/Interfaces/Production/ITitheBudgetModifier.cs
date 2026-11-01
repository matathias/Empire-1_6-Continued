namespace FactionColonies
{
    /// <summary>
    /// A WorldObjectComp interface for injecting additional tithe budget into a resource.
    /// The injected budget raises the per-day tithe budget (<see cref="ResourceFC.GetTitheIncome"/>) and
    /// accrues into the cycle's tithe budget each day (<see cref="ResourceFC.AccumulateDailyProduction"/>),
    /// so externally-funded tithe goods don't reduce the settlement's own silver income.
    /// <para>Queried during tithe budget calculation via <see cref="ResourceFC.DailyExternalTitheBudget"/>.
    /// Caches are automatically invalidated after all lifecycle events. Call
    /// <c>((WorldSettlementFC)parent).InvalidateStatCache()</c> manually if changing values outside
    /// a lifecycle callback.</para>
    /// </summary>
    public interface ITitheBudgetModifier
    {
        /// <summary>
        /// Returns additional tithe budget (in silver value) for the given resource, per day (sampled daily).
        /// Return 0 for no effect.
        /// </summary>
        double GetDailyExternalTitheBudget(ResourceFC resource);

        /// <summary>
        /// Description text for the tithe budget breakdown tooltip. Return null or empty for no entry.
        /// </summary>
        string GetExternalTitheBudgetDesc(ResourceFC resource);
    }
}
