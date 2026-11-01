namespace FactionColonies
{
    /// <summary>
    /// A WorldObjectComp/world-component interface for running logic once per day, immediately after
    /// every settlement has accrued the day's production/upkeep and stockpile deposits have landed.
    /// Fires for the whole faction (not per allocation), so it is the place for daily consumption
    /// that must run for every settlement (e.g. SupplyChain's produce-then-consume needs resolution).
    /// </summary>
    public interface IDailyAccrualParticipant
    {
        /// <summary>Called once per day, after the faction's daily accrual loop completes.</summary>
        void PostDailyAccrual(FactionFC faction);
    }
}
