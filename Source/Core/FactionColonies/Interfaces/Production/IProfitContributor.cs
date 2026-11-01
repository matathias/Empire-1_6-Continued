namespace FactionColonies
{
    /// <summary>
    /// A WorldObjectComp interface for contributing additional upkeep or income to a settlement's
    /// cost breakdown.
    /// <para>Queried during profit recomputation (<see cref="WorldSettlementFC.RecomputeProfit"/>).
    /// Caches are automatically invalidated after all lifecycle events. Call
    /// <c>((WorldSettlementFC)parent).DirtyProfitCache()</c> manually if changing values
    /// outside a lifecycle callback.</para>
    /// </summary>
    public interface IProfitContributor
    {
        /// <summary>
        /// Returns the total upkeep cost (in silver) to add to the settlement's upkeep per day.
        /// Return 0 for no effect.
        /// </summary>
        double GetDailyUpkeepContribution();

        /// <summary>
        /// Returns a formatted description line for the upkeep tooltip breakdown.
        /// Return null or empty to add no tooltip line.
        /// </summary>
        string GetDailyUpkeepContributionDesc();

        /// <summary>
        /// Returns additional silver income to add to the settlement's income per day.
        /// Return 0 for no effect.
        /// </summary>
        double GetDailyIncomeContribution();

        /// <summary>
        /// Returns a formatted description line for the income tooltip breakdown.
        /// Return null or empty to add no tooltip line.
        /// </summary>
        string GetDailyIncomeContributionDesc();
    }
}
