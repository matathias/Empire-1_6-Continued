using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Defines an interface to let classes hook into the tax system.
    /// </summary>
    public interface ITaxTickParticipant
    {
        void PreTaxResolution(FactionFC faction);
        void PostTaxResolution(FactionFC faction);
        /// <summary>
        /// Called at the start of tax collection, after pre-tax preparation (cache invalidation, resource pruning) and SettlementTypeExtension.PreTax.
        /// </summary>
        void PreSettlementCreateTax(WorldSettlementFC settlement);
        /// <summary>
        /// Called at the end of tax collection, after all calculations are complete and SettlementTypeExtension.PostTax.
        /// </summary>
        void PostSettlementCreateTax(WorldSettlementFC settlement, ref int silverAmount, List<Thing> titheThings);
    }
}
