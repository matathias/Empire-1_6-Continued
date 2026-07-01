using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Condition extension for a faction-scoped <see cref="FCSituationDef"/>. Exposes two independent
    /// predicates so the rule that <em>starts</em> a situation can differ from the rule that
    /// <em>keeps it advancing</em> (hysteresis). Both are stateless — the extension is a shared
    /// singleton per def, and the manager owns all per-instance bookkeeping.
    ///
    /// Attach in XML via <c>&lt;modExtensions&gt;&lt;li Class="..."/&gt;&lt;/modExtensions&gt;</c>.
    /// </summary>
    public abstract class FCSituationFactionCondition : DefModExtension
    {
        /// <summary>While no situation for this def exists and the def is eligible (off cooldown,
        /// under maxConcurrent), returning true starts one.</summary>
        public abstract bool ShouldSpawn(FactionFC faction);

        /// <summary>For an active situation: true advances the bar at baseRatePerDay, false recedes
        /// it at decayRatePerDay.</summary>
        public abstract bool ShouldAdvance(FactionFC faction);
    }

    /// <summary>
    /// Condition extension for a settlement-scoped <see cref="FCSituationDef"/>. Evaluated per
    /// settlement during the spawn pass, and per active instance (against its target) during advance.
    /// See <see cref="FCSituationFactionCondition"/> for the spawn/advance split.
    /// </summary>
    public abstract class FCSituationSettlementCondition : DefModExtension
    {
        public abstract bool ShouldSpawn(FactionFC faction, WorldSettlementFC settlement);

        public abstract bool ShouldAdvance(FactionFC faction, WorldSettlementFC settlement);
    }
}
