namespace FactionColonies;

/// <summary>
/// A simple interface that a WorldObjectComp -- attached to a WorldSettlementFC -- can implement to affect non-resource stats.
/// <para>Results are cached alongside stat modifiers. Caches are automatically invalidated after all lifecycle
/// events (building, settlement, military, research, tax hooks). Only call
/// <c>((WorldSettlementFC)parent).InvalidateStatCache()</c> manually if changing values outside a lifecycle callback.</para>
/// </summary>
public interface IStatModifierProvider
{
    /// <summary>
    /// Returns this provider's contribution to <paramref name="stat"/>, combined using the stat's
    /// aggregation. Return <see cref="FCStatDef.IdentityValue"/> (0 additive, 1 multiplicative) for no effect.
    /// </summary>
    double GetStatModifier(FCStatDef stat) => stat.IdentityValue;
    /// <summary>Tooltip line for this provider's contribution. Return null or empty for no line.</summary>
    string GetStatModifierDesc(FCStatDef stat) => null;
}
