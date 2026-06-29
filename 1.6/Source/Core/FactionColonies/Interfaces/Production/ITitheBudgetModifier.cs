namespace FactionColonies
{
    /// <summary>
    /// A WorldObjectComp interface for injecting additional tithe budget into a resource.
    /// The injected budget raises the tithe income cap (<see cref="ResourceFC.GetTitheIncome"/>).
    /// In <see cref="ResourceFC.actualIncome"/>, only the portion of tithe actually covered by the
    /// injection is offset, so the settlement is not penalised for externally-sourced goods.
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
