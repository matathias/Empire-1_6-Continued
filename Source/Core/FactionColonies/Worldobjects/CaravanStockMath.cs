using UnityEngine;

namespace FactionColonies
{
    /// <summary>
    /// Pure calculation methods backing <see cref="EmpireCaravanStockGenerator"/>'s ware-stack
    /// budgeting. Bounding the number of non-pawn ware stacks a caravan emits is what keeps
    /// vanilla <see cref="RimWorld.PawnGroupKindWorker_Trader"/>'s <c>ceil(stacks / 8)</c>
    /// pack-animal formula in check (carriers scale with stack count, not mass), so a developed
    /// empire's caravan arrives with a few well-loaded animals instead of a near-empty herd.
    ///
    /// These methods have zero RimWorld dependencies (UnityEngine.Mathf only), making them
    /// unit-testable without a live world. <see cref="EmpireCaravanStockGenerator.GenerateThings"/>
    /// calls them as the single source of truth.
    /// </summary>
    public static class CaravanStockMath
    {
        /// <summary>Lower bound on the production-scale multiplier (a small/early empire).</summary>
        public const float MinStackScale = 0.5f;

        /// <summary>Upper bound on the production-scale multiplier.</summary>
        public const float MaxStackScale = 4f;

        /// <summary>
        /// Deterministic target count of total non-pawn ware stacks (silver + items) for a caravan.
        /// Scales the per-caravan baseline by the empire's production scaling
        /// (<paramref name="extraScale"/> relative to <paramref name="baseExtraScale"/>), clamped to
        /// [<see cref="MinStackScale"/>, <see cref="MaxStackScale"/>]. Never random; never below 1.
        /// </summary>
        public static int TargetStacks(int baseThingCount, float extraScale, float baseExtraScale)
        {
            float reference = baseExtraScale > 0f ? baseExtraScale : 1f;
            float scale = Mathf.Clamp(extraScale / reference, MinStackScale, MaxStackScale);
            return Mathf.Max(1, Mathf.RoundToInt(baseThingCount * scale));
        }

        /// <summary>
        /// Number of silver stacks the trader carries as buying power. Silver fragments at its
        /// stack limit, so cap it to at most ~half the stack target; always at least one stack.
        /// </summary>
        public static int SilverStacks(int desiredSilver, int targetStacks, int silverStackLimit)
        {
            int limit = Mathf.Max(1, silverStackLimit);
            return Mathf.Clamp(Mathf.CeilToInt((float)desiredSilver / limit), 1, Mathf.Max(1, targetStacks / 2));
        }

        /// <summary>
        /// Per-type stack ceiling: the item-stack budget spread across the item candidates, so a
        /// single cheap non-stacking good can't consume the whole budget with dozens of copies.
        /// Always at least one stack per type.
        /// </summary>
        public static int PerTypeStackCap(int targetItemStacks, int itemCandidateCount)
        {
            return Mathf.Max(1, Mathf.CeilToInt((float)targetItemStacks / Mathf.Max(1, itemCandidateCount))) + 1;
        }

        /// <summary>
        /// Units of one item type to emit, clamped to its stack share. Multiplying the per-type
        /// stack cap by <paramref name="stackLimit"/> lets stackables (food, ore) carry near-full
        /// stacks while holding non-stacking goods (stackLimit 1) to ~<paramref name="perTypeStackCap"/>
        /// units. Always at least one unit.
        /// </summary>
        public static int ClampItemUnits(int desiredUnits, int perTypeStackCap, int stackLimit)
        {
            int maxUnits = Mathf.Max(1, perTypeStackCap) * Mathf.Max(1, stackLimit);
            return Mathf.Clamp(Mathf.Max(1, desiredUnits), 1, maxUnits);
        }
    }
}
