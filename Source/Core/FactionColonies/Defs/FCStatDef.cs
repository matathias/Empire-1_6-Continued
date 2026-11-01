using RimWorld;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    public enum FCStatAggregation : byte
    {
        Additive,
        Multiplicative
    }

    /// <summary>
    /// Defines a named stat that policies, buildings, and events can modify.
    /// Referenced by defName in XML, resolved at load time — typos become XML errors at startup.
    /// </summary>
    public class FCStatDef : Def
    {
        /// <summary>
        /// The identity value for this stat's aggregation: 0 for Additive, 1 for Multiplicative.
        /// </summary>
        public double IdentityValue => aggregation == FCStatAggregation.Multiplicative ? 1.0 : 0.0;

        /// <summary>
        /// How multiple modifiers combine: Additive sums values, Multiplicative multiplies them.
        /// </summary>
        public FCStatAggregation aggregation = FCStatAggregation.Additive;

        /// <summary>
        /// Whether this stat applies at the settlement level (propagated to settlements).
        /// If false, it's faction-level only.
        /// </summary>
        public bool appliesToSettlements = true;

        /// <summary>
        /// Whether this stat can be modified per-squad. When true and a squad context is supplied to
        /// GetStatValue, the squad's statModifiers fold into the result.
        /// </summary>
        public bool appliesToSquads = false;

        /// <summary>
        /// Whether this stat can be modified per-unit (individual mercenary). When true and a unit context
        /// is supplied to GetStatValue, that soldier's statModifiers fold into the result.
        /// </summary>
        public bool appliesToUnits = false;

        /// <summary>
        /// Translation key for description display (e.g., "FCTraitDesc_MilitaryLevel").
        /// </summary>
        public string descriptionKey;

        /// <summary>
        /// If true, lower values are "better" for UI coloring purposes (e.g., costs, losses).
        /// </summary>
        public bool invertedForDisplay;

        /// <summary>
        /// If non-zero, the raw stat value is divided by this before display.
        /// Used for tick-based stats (e.g., 2500 to convert ticks to in-game hours).
        /// </summary>
        public double displayDivisor;

        /// <summary>
        /// If non-null, this stat is a resource production stat linked to this ResourceTypeDef.
        /// Used for description formatting (resource name + icon instead of generic descriptionKey).
        /// </summary>
        public ResourceTypeDef linkedResource;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string err in base.ConfigErrors())
                yield return err;

            if (linkedResource != null)
            {
                if (linkedResource.productionAdditiveStat == this && aggregation != FCStatAggregation.Additive)
                    yield return defName + ": linked as productionAdditiveStat on " + linkedResource.defName + " but aggregation is not Additive";
                if (linkedResource.productionMultiplierStat == this && aggregation != FCStatAggregation.Multiplicative)
                    yield return defName + ": linked as productionMultiplierStat on " + linkedResource.defName + " but aggregation is not Multiplicative";
            }
        }
    }
}
