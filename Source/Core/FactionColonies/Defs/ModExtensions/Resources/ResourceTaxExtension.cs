using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// DefModExtension for ResourceTypeDef that provides hooks into the tax/tithe generation
    /// process for non-pool resources. Only one ResourceTaxExtension may be applied per ResourceTypeDef,
    /// and it cannot be used on pool resources.
    /// </summary>
    public abstract class ResourceTaxExtension : DefModExtension
    {
        /// <summary>
        /// Called before tithe generation begins for this resource in a settlement.
        /// Can be used to modify resource state before generation.
        /// </summary>
        /// <param name="resource">The ResourceFC being taxed.</param>
        /// <param name="settlement">The settlement generating the tithe.</param>
        public virtual void OnPreTaxGeneration(ResourceFC resource, WorldSettlementFC settlement)
        {
        }

        /// <summary>
        /// Called after tithe generation completes for this resource in a settlement.
        /// Can modify the generated things list (add, remove, or transform items).
        /// </summary>
        /// <param name="resource">The ResourceFC that was taxed.</param>
        /// <param name="settlement">The settlement that generated the tithe.</param>
        /// <param name="generatedThings">The list of things generated. Can be modified.</param>
        /// <param name="extraSilver">Additional silver amount. Can be modified via ref.</param>
        public virtual void OnPostTaxGeneration(ResourceFC resource, WorldSettlementFC settlement, List<Thing> generatedThings, ref int extraSilver)
        {
        }
    }
}
