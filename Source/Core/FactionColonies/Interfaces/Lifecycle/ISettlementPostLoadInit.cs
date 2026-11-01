namespace FactionColonies
{
    /// <summary>
    /// A WorldObjectComp interface for performing initialization that depends on fully-rebuilt
    /// settlement state (stat modifiers, buildings, settlement type). Called from
    /// WorldSettlementFC.ExposeData's PostLoadInit block AFTER stat modifiers are rebuilt.
    /// <para>Comps that need to validate against production values or stat caches at load time
    /// should do that work here, not in PostExposeData (which runs before the settlement
    /// finishes its own PostLoadInit).</para>
    /// </summary>
    public interface ISettlementPostLoadInit
    {
        void PostSettlementLoadInit(WorldSettlementFC settlement);
    }
}
