using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// DefModExtension attached to vanilla LandmarkDefs via XML patch. Declares per-resource
    /// bonuses and / or general FCStatDef modifiers that apply to settlements founded on a tile
    /// carrying the landmark. Odyssey-only; the <c>Tile.Landmark</c> accessor returns null when
    /// Odyssey is inactive, so non-Odyssey installs silently no-op.
    /// <para>
    /// - <c>bonuses</c>: direct resource additive/multiplier rows. Consumed by
    ///   <c>ResourceTypeDef.GetLandmarkAdditives</c> / <c>GetLandmarkMultipliers</c> (preview +
    ///   runtime share the same scan). Prefer this path for resource production bonuses.
    /// </para>
    /// <para>
    /// - <c>statModifiers</c>: general <c>FCStatDef</c> entries. Scanned by
    ///   <c>WorldSettlementFC.GetSettlementStatValue</c> / <c>GetStatDesc</c>. Use this path for
    ///   non-resource stats (military, happiness, tax, etc.).
    /// </para>
    /// </summary>
    public class TileLandmarkResourceExtension : DefModExtension
    {
        public List<TileResourceBonus> bonuses = new List<TileResourceBonus>();
        public List<FCStatModifier> statModifiers = new List<FCStatModifier>();
    }
}
