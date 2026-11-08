using UnityEngine;

namespace FactionColonies;

/// <summary>
/// Per-item steps shared by <see cref="EmpireStockGenerator"/> and
/// <see cref="EmpireCaravanStockGenerator"/>: picking sellable candidates, turning a per-item
/// budget into a randomized unit count, and generating stock pawns. Each generator layers its own
/// caps on top (<see cref="CaravanStockMath"/> for caravans, animal caps for settlements).
/// </summary>
internal static class EmpireStockUtil
{
    /// <summary>Randomness range for per-item budget (multiplier). Prevents uniform stack sizes.</summary>
    private const float BudgetRandomMin = 0.5f;
    private const float BudgetRandomMax = 1.5f;

    /// <summary>The trader-sellable defs among <paramref name="defs"/>, in random order.</summary>
    internal static List<ThingDef> SellableCandidates(IEnumerable<ThingDef> defs) =>
        defs.Where(td => td.tradeability.TraderCanSell())
            .InRandomOrder()
            .ToList();

    /// <summary>Market value of one unit for stock budgeting, floored at 1 so worthless defs
    /// don't divide by zero.</summary>
    internal static float UnitValue(ThingDef td)
    {
        float marketValue = td.BaseMarketValue;
        return marketValue <= 0f ? 1f : marketValue;
    }

    /// <summary>How many units of <paramref name="td"/> a randomized slice of
    /// <paramref name="perItemBudget"/> (plus a flat <paramref name="bonusBudget"/>) buys, rounded.
    /// Can be 0; callers apply their own floor and caps.</summary>
    internal static int RandomizedUnits(ThingDef td, float perItemBudget, float bonusBudget = 0f)
    {
        float randomizedBudget = perItemBudget * Rand.Range(BudgetRandomMin, BudgetRandomMax) + bonusBudget;
        return Mathf.RoundToInt(randomizedBudget / UnitValue(td));
    }

    /// <summary>Generates up to <paramref name="count"/> non-player pawns of
    /// <paramref name="kind"/> for trader stock, skipping any that fail to generate.</summary>
    internal static IEnumerable<Pawn> GenerateStockPawns(PawnKindDef kind, int count)
    {
        if (kind is null) yield break;
        for (int i = 0; i < count; i++)
        {
            PawnGenerationRequest request = new(kind, null, PawnGenerationContext.NonPlayer);
            Pawn pawn = PawnGenerator.GeneratePawn(request);
            if (pawn is not null) yield return pawn;
        }
    }
}
