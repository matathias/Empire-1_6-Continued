using RimWorld.Planet;

namespace FactionColonies
{
    /// <summary>
    /// Allows submods to add custom validation, display additional costs, and perform
    /// side effects (e.g., resource consumption) when settlements are founded.
    /// Register implementations via <see cref="FoundingValidatorRegistry"/>.
    /// </summary>
    public interface ISettlementFoundingValidator
    {
        /// <summary>
        /// Called during settlement creation validation. Return false with a reason
        /// to prevent the player from founding the settlement.
        /// <para><paramref name="costMultiplier"/> scales any resource/cost requirement
        /// (1 = full cost; e.g. 0.5 for a discounted founding such as an outpost conversion).</para>
        /// </summary>
        bool CanFoundSettlement(PlanetTile tile, WorldSettlementDef type, out string reason, float costMultiplier);

        /// <summary>
        /// Returns additional cost text to display in the settlement creation UI,
        /// below the silver cost (e.g., "100 Food, 50 Lumber from [Settlement]").
        /// Return null if no additional costs to display.
        /// <para><paramref name="costMultiplier"/> scales the displayed amounts to match
        /// what will actually be charged.</para>
        /// </summary>
        string GetAdditionalCostDescription(PlanetTile tile, WorldSettlementDef type, float costMultiplier);

        /// <summary>
        /// Called in DoFoundSettlement() after silver payment succeeds.
        /// Consume resources or perform other founding side effects here.
        /// <para>Note that at this point, the settlement has not actually been created yet.</para>
        /// <para><paramref name="costMultiplier"/> scales the resources consumed (1 = full cost).</para>
        /// </summary>
        void OnSettlementFounded(PlanetTile tile, WorldSettlementDef type, float costMultiplier);
    }
}
