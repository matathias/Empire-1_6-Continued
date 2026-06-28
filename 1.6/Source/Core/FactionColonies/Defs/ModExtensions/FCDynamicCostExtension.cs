using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    public enum FCProductionCostScope : byte
    {
        AffectedSettlements = 0,
        FactionWide = 1
    }

    public enum FCIncomeCostScope : byte
    {
        FactionWide = 0,
        AffectedSettlements = 1
    }

    /* How a resource production-delta cost is framed in the option's cost-breakdown tooltip.
     * Presentational only — the silver math is identical either way. Mitigated = the option
     * rescues production that would otherwise be lost; Added = the option grants new production. */
    public enum FCResourceCostFraming : byte
    {
        Mitigated = 0,
        Added = 1
    }

    /* {resource, additiveDelta, coefficient, framing} used by FCDynamicCostExtension.costPerResourceDelta.
     * Plain POCO so RimWorld's XML loader populates fields directly. */
    public class ResourceProductionDeltaCost
    {
        public ResourceTypeDef resource;

        /* The magnitude of the resource's ProductionAdditive that this option changes relative to
         * the free fallback option — e.g. an outcome of -0.5 vs the free option's -1 rescues 0.5
         * (Mitigated), or an outcome of +1 vs the free option's 0 adds 1 (Added). Per settlement
         * this is multiplied by productionMult and assignedWorkers to get the real units of
         * production.*/
        public float additiveDelta;

        /* Fraction of the resource's silver-per-unit value (FCSettings.silverPerResource) charged
         * per unit of production delta. 0.5 = 50%. */
        public float coefficient;

        /* Chooses the tooltip wording ("mitigated" vs "added"); does not affect the cost. */
        public FCResourceCostFraming framing = FCResourceCostFraming.Mitigated;
    }

    /// <summary>
    /// Opt-in DefModExtension on <see cref="FCOptionDef"/> that adds income- and/or
    /// production-based silver-cost scaling on top of the automatic per-settlement multiplier.
    /// Contributions are additive — the extension can only raise the cost, never lower it.
    /// </summary>
    public class FCDynamicCostExtension : DefModExtension
    {
        /// <summary>Coefficient applied to current income (silver/cycle). 0.05 = +5% of income.</summary>
        public float costPerEmpireIncomeUnit = 0f;

        /// <summary>
        /// Per-resource scaling on the marginal production the option changes (rescues or adds):
        /// coefficient x (additiveDelta x productionMult x assignedWorkers), summed over the scope.
        /// </summary>
        public List<ResourceProductionDeltaCost> costPerResourceDelta = new List<ResourceProductionDeltaCost>();

        /// <summary>Which settlements contribute to the production-delta sum.</summary>
        public FCProductionCostScope productionScope = FCProductionCostScope.AffectedSettlements;

        /// <summary>Which settlements contribute to the income sum.</summary>
        public FCIncomeCostScope incomeScope = FCIncomeCostScope.FactionWide;
    }
}
