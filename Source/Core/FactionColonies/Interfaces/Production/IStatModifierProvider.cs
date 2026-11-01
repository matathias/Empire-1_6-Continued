namespace FactionColonies
{
    /// <summary>
    /// A simple interface that a WorldObjectComp -- attached to a WorldSettlementFC -- can implement to affect non-resource stats.
    /// <para>Results are cached alongside stat modifiers. Caches are automatically invalidated after all lifecycle
    /// events (building, settlement, military, research, tax hooks). Only call
    /// <c>((WorldSettlementFC)parent).InvalidateStatCache()</c> manually if changing values outside a lifecycle callback.</para>
    /// </summary>
    public interface IStatModifierProvider
    {
        double GetStatModifier(FCStatDef stat);
        string GetStatModifierDesc(FCStatDef stat);
    }
}
