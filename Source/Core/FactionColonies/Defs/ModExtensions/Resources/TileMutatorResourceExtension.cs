using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Shared row class used by both TileMutatorResourceExtension and
    /// TileLandmarkResourceExtension. Represents a single per-resource bonus entry.
    /// </summary>
    public class TileResourceBonus
    {
        public ResourceTypeDef resource;
        public double additive = 0;
        public double multiplier = 1;
        public string label;
    }

    /// <summary>
    /// DefModExtension attached to vanilla TileMutatorDefs via XML patch. Declares per-resource
    /// bonuses and / or general FCStatDef modifiers that apply to settlements founded on a tile
    /// carrying the mutator.
    /// <para>
    /// - <c>bonuses</c>: direct resource additive/multiplier rows. Consumed by
    ///   <c>ResourceTypeDef.GetMutatorAdditives</c> / <c>GetMutatorMultipliers</c> (preview +
    ///   runtime share the same scan). Prefer this path for resource production bonuses because
    ///   the breakdown row adds the mutator name directly to the resource tooltip.
    /// </para>
    /// <para>
    /// - <c>statModifiers</c>: general <c>FCStatDef</c> entries. Scanned by
    ///   <c>WorldSettlementFC.GetSettlementStatValue</c> / <c>GetStatDesc</c>. Use this path for
    ///   non-resource stats (military, happiness, tax, etc.) and for stat-linked effects the
    ///   direct resource path can't reach.
    /// </para>
    /// </summary>
    public class TileMutatorResourceExtension : DefModExtension
    {
        public List<TileResourceBonus> bonuses = new List<TileResourceBonus>();
        public List<FCStatModifier> statModifiers = new List<FCStatModifier>();
    }
}
