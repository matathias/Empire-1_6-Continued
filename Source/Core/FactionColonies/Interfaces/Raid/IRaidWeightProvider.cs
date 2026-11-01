namespace FactionColonies
{
    /// <summary>
    /// Allows submods to influence which settlement gets attacked by enemy factions.
    /// Each provider returns a weight multiplier per settlement. The final weight for
    /// settlement selection is: base weight * product of all provider weights.
    /// Register implementations via <see cref="RaidWeightRegistry"/>.
    /// </summary>
    public interface IRaidWeightProvider
    {
        /// <summary>
        /// Returns a weight multiplier for <paramref name="settlement"/> when attacked by <paramref name="attackingFaction"/>.
        /// Return 1.0 for no effect. Return &gt; 1.0 to make the settlement more likely to be targeted.
        /// Return &lt; 1.0 (but &gt; 0) to make it less likely. Return 0 to completely exclude it.
        /// </summary>
        float GetSettlementRaidWeight(WorldSettlementFC settlement, RimWorld.Faction attackingFaction);
    }
}
