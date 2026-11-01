using System;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace FactionColonies.util
{
    /* Pure functions that compute the dynamic silver cost of an FCOptionDef in the context of an
     * FCEvent. Read-only — never mutates the event, the option, or any settlement state. The
     * formula has three layers:
     *   1. base * Max(1, affectedSettlementCount)   — automatic per-settlement multiplier
     *   2. + FCDynamicCostExtension contributions   — opt-in income / production coefficients
     *   3. * FCSettings.eventSilverCostMultiplier    — global player slider, applied last
     */
    public static class FCOptionCostUtil
    {
        public static int ComputeScaledCost(FCOptionDef opt, FCEvent evt)
        {
            if (opt is null) return 0;
            if (evt is null) return opt.EffectiveSilverCost;

            double raw = ComputeRaw(opt, evt, null);
            double scaled = raw * FCSettings.eventSilverCostMultiplier;
            int final = (int)Math.Round(scaled, MidpointRounding.AwayFromZero);
            return Math.Max(0, final);
        }

        /// <summary>
        /// Returns a multi-line breakdown string showing each cost contribution. Used by the
        /// option window's hover tooltip when the cost has any scaling applied.
        /// </summary>
        public static string BuildCostBreakdown(FCOptionDef opt, FCEvent evt)
        {
            if (opt is null || evt is null) return null;

            List<string> lines = new List<string>();
            double raw = ComputeRaw(opt, evt, lines);

            // If nothing actually scaled (no breakdown lines beyond the base), return null —
            // tooltip is only useful when there's something to explain.
            if (lines.Count <= 1) return null;

            double scaled = raw * FCSettings.eventSilverCostMultiplier;
            int total = Math.Max(0, (int)Math.Round(scaled, MidpointRounding.AwayFromZero));

            if (Math.Abs(FCSettings.eventSilverCostMultiplier - 1f) > 0.001f)
            {
                lines.Add("FCOptionCostMultiplier".Translate(FCSettings.eventSilverCostMultiplier.ToString("0.0")));
            }
            lines.Add("FCOptionCostTotal".Translate(total.ToString("N0")));

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(lines[i]);
            }
            return sb.ToString();
        }

        /* Shared inner: computes the pre-global-multiplier silver value. If lines is non-null,
         * appends one human-readable line per contribution (used by BuildCostBreakdown). */
        private static double ComputeRaw(FCOptionDef opt, FCEvent evt, List<string> lines)
        {
            int baseCost = Math.Max(0, opt.silverCost);
            int affected = FCEventScalingUtil.CountAffectedSettlements(evt);

            double total = (double)baseCost * affected;
            if (lines != null)
            {
                lines.Add("FCOptionCostBase".Translate(baseCost.ToString("N0")));
                if (affected > 1)
                {
                    lines.Add("FCOptionCostPerSettlement".Translate(affected, ((double)baseCost * affected).ToString("N0")));
                }
            }

            FCDynamicCostExtension ext = opt.GetModExtension<FCDynamicCostExtension>();
            if (ext is object)
            {
                if (ext.costPerEmpireIncomeUnit != 0f)
                {
                    double income = ComputeIncome(evt, ext.incomeScope);
                    double contribution = income * ext.costPerEmpireIncomeUnit;
                    total += contribution;
                    if (lines != null && contribution > 0)
                    {
                        lines.Add("FCOptionCostIncome".Translate(
                            (ext.costPerEmpireIncomeUnit * 100f).ToString("0.#"),
                            ((int)Math.Round(contribution)).ToString("N0"),
                            FindFC.EmpireTitle));
                    }
                }

                if (ext.costPerResourceDelta is object)
                {
                    foreach (ResourceProductionDeltaCost pair in ext.costPerResourceDelta)
                    {
                        if (pair?.resource is null || pair.coefficient == 0f || pair.additiveDelta == 0f) continue;
                        double delta = ComputeProductionDelta(evt, pair.resource, pair.additiveDelta, ext.productionScope);
                        // coefficient is a fraction of the resource's silver-per-unit value.
                        double contribution = delta * FCSettings.silverPerResource * pair.coefficient;
                        total += contribution;
                        if (lines != null && contribution > 0)
                        {
                            string key = pair.framing == FCResourceCostFraming.Added
                                ? "FCOptionCostResourceAdded"
                                : "FCOptionCostResourceMitigated";
                            lines.Add(key.Translate(
                                pair.resource.LabelCap,
                                delta.ToString("0.##"),
                                ((int)Math.Round(contribution)).ToString("N0")));
                        }
                    }
                }
            }

            return total;
        }

        private static double ComputeIncome(FCEvent evt, FCIncomeCostScope scope)
        {
            if (scope == FCIncomeCostScope.AffectedSettlements && HasLiveTargets(evt))
            {
                double sum = 0;
                foreach (WorldSettlementFC s in evt.settlementTraitLocations)
                {
                    if (s is object) sum += s.totalIncome;
                }
                return sum;
            }
            // FactionWide (or AffectedSettlements with no live targets) → faction-wide income.
            FactionFC faction = FindFC.FactionComp;
            return faction is object ? faction.income : 0;
        }

        /* Marginal production the option changes for one resource. A change of additiveDelta in the
         * resource's production additive shifts InstantaneousProduction by
         * additiveDelta * productionMult * assignedWorkers per settlement (since
         * InstantaneousProduction = productionBase * productionMult * assignedWorkers). Summed over
         * the scope. Charges for the production the option actually rescues or adds, not the
         * settlement's total output. Sign-agnostic: framing is presentational, the math is the same. */
        private static double ComputeProductionDelta(FCEvent evt, ResourceTypeDef resourceDef, float additiveDelta, FCProductionCostScope scope)
        {
            if (resourceDef is null) return 0;
            double sum = 0;

            if (scope == FCProductionCostScope.AffectedSettlements && HasLiveTargets(evt))
            {
                foreach (WorldSettlementFC s in evt.settlementTraitLocations)
                {
                    if (s is null) continue;
                    sum += DeltaAt(s, resourceDef, additiveDelta);
                }
                return sum;
            }

            FactionFC faction = FindFC.FactionComp;
            if (faction is null || faction.settlements is null) return 0;
            foreach (WorldSettlementFC s in faction.settlements)
            {
                if (s is null) continue;
                sum += DeltaAt(s, resourceDef, additiveDelta);
            }
            return sum;
        }

        private static double DeltaAt(WorldSettlementFC s, ResourceTypeDef resourceDef, float additiveDelta)
        {
            ResourceFC res = s.GetResource(resourceDef);
            if (res is null) return 0;
            double delta = additiveDelta * res.productionMult * res.assignedWorkers;
            return delta > 0 ? delta : 0;
        }

        private static bool HasLiveTargets(FCEvent evt)
        {
            if (evt.settlementTraitLocations is null) return false;
            foreach (WorldSettlementFC s in evt.settlementTraitLocations)
            {
                if (s is object) return true;
            }
            return false;
        }
    }
}
