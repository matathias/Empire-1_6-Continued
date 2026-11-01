using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Self-contained StockGenerator that produces trader stock from the settlement's actual
    /// resource production. Budget is derived from each resource's grossMarketValue; variety
    /// (how many distinct items) is scaled by settlement level.
    /// <para>NOTE: This instance is a singleton on the TraderKindDef, shared across all settlements.
    /// The <c>settlement</c> field is only valid during <c>GenerateThings</c> (set/cleared by
    /// <c>SettlementTraderTracker_Empire</c>). Do not read it outside that window.</para>
    /// </summary>
    public class EmpireStockGenerator : StockGenerator
    {
        /// <summary>
        /// Set by SettlementTraderTracker_Empire before stock generation, cleared after.
        /// Only valid during GenerateThings; null at all other times.
        /// </summary>
        [Unsaved(false)]
        public WorldSettlementFC settlement;

        /// <summary>Settlement level at which variety is 100%. Level 1 = 20%, level 10 = 200%.</summary>
        private const float AnchorLevel = 5f;

        /// <summary>
        /// Distinct ThingDef count to pick per active resource. The settlement trader cycles
        /// through every active resource, so total stock distinct-count is roughly this range
        /// times the number of active resources. Scaled by settlement level (a level-1 settlement
        /// at 0.2x scale shows ~1 item per resource; a level-10 settlement at 2x shows the full
        /// range times 2). Default is calibrated against vanilla focused-trader counts.
        /// </summary>
        public IntRange varietyRange = new IntRange(5, 10);

        /// <summary>Randomness range for per-item budget (multiplier). Prevents uniform stack sizes.</summary>
        private const float BudgetRandomMin = 0.5f;
        private const float BudgetRandomMax = 1.5f;

        public override IEnumerable<Thing> GenerateThings(PlanetTile forTile, Faction faction = null)
        {
            if (settlement is null)
                yield break;

            LogUtil.Message($"Generating stock for settlement {settlement.Name}");

            float levelScale = settlement.settlementLevel / AnchorLevel;

            // Generate silver based on total settlement income
            double totalIncome = settlement.totalIncome * ExtraScaling();

            if (totalIncome <= 0)
                yield break;

            int silverCount = Mathf.Max(100, Mathf.RoundToInt((float)totalIncome));
            foreach (Thing silver in StockGeneratorUtility.TryMakeForStock(ThingDefOf.Silver, silverCount, faction))
            {
                yield return silver;
            }

            // Compute total production for resource share calculation
            float totalProduction = 0f;
            foreach (ResourceFC res in settlement.Resources)
            {
                if (IsActiveResource(res))
                    totalProduction += (float)res.InstantaneousProduction;
            }

            if (totalProduction <= 0f)
                yield break;

            // Generate goods per resource
            foreach (ResourceFC res in settlement.Resources)
            {
                if (!IsActiveResource(res))
                    continue;

                List<ThingDef> thingDefs = res.GenerateThingDefList();
                if (thingDefs.NullOrEmpty())
                    continue;

                float resourceShare = (float)res.InstantaneousProduction / totalProduction;
                float resourceBudget = resourceShare * (float)totalIncome;

                // Filter & shuffle the pool first so we know how many tradeable candidates exist
                // before deciding variety.
                List<ThingDef> candidates = thingDefs
                    .Where(td => td.tradeability.TraderCanSell())
                    .InRandomOrder()
                    .ToList();

                if (candidates.Count == 0)
                    continue;

                // Variety is XML-tunable and scales with settlement level. Budget already scales
                // with resourceShare via resourceBudget above, so don't double-count share here.
                int targetVariety = Mathf.RoundToInt(varietyRange.RandomInRange * levelScale);
                int variety = Mathf.Clamp(targetVariety, 1, candidates.Count);

                float perItemBudget = resourceBudget / variety;

                candidates = candidates.Take(variety).ToList();

                foreach (ThingDef td in candidates)
                {
                    // Randomize per-item budget so stack sizes feel organic
                    float randomizedBudget = perItemBudget * Rand.Range(BudgetRandomMin, BudgetRandomMax);
                    float marketValue = td.BaseMarketValue;
                    if (marketValue <= 0f)
                        marketValue = 1f;

                    int stackCount = Mathf.Max(1, Mathf.RoundToInt(randomizedBudget / marketValue));

                    if (td.race is object && td.race.Animal)
                    {
                        for (int i = 0; i < stackCount; i++)
                        {
                            PawnKindDef pawnKind = td.race.AnyPawnKind;
                            if (pawnKind is null) continue;
                            PawnGenerationRequest request = new PawnGenerationRequest(pawnKind, null, PawnGenerationContext.NonPlayer);
                            Pawn pawn = PawnGenerator.GeneratePawn(request);
                            if (pawn is object) yield return pawn;
                        }
                    }
                    else
                    {
                        foreach (Thing thing in StockGeneratorUtility.TryMakeForStock(td, stackCount, faction))
                        {
                            yield return thing;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Accepts items matching any resource type or common essentials (food, medicine,
        /// non-armor apparel). Rejects dangerous/worthless items via the shared blocklist.
        /// Silver is always accepted — the trader generates it as stock and uses it as currency.
        /// </summary>
        public override bool HandlesThingDef(ThingDef thingDef)
        {
            if (thingDef == ThingDefOf.Silver)
                return true;

            if (EmpireTradeFilterUtil.ShouldReject(thingDef))
                return false;

            return EmpireTradeFilterUtil.IsCommonEssential(thingDef)
                || EmpireTradeFilterUtil.MatchesAnyResource(thingDef);
        }

        private static bool IsActiveResource(ResourceFC res)
        {
            return res.assignedWorkers > 0
                && !res.def.isPoolResource
                && res.def.CanTithe;
        }
        /// <summary>
        /// Baseline (pre tech-level) trader wealth multiplier. A default-tech (Industrial)
        /// empire at default <see cref="FCSettings.silverPerResource"/> ends up at exactly this
        /// scale, so it doubles as the reference point for scaling caravan size relative to a
        /// "normal" empire (see EmpireCaravanStockGenerator.baseThingCount).
        /// </summary>
        internal const float BaseExtraScale = 3f;

        /// <summary>
        /// Provides extra scaling for the trader's wealth. Centralized for ease of editing.
        /// </summary>
        /// <returns></returns>
        internal static float ExtraScaling()
        {
            // Triple the income to start with. At default settings, a level 5 settlement will have 15 workers,
            // 20 if overmax is assigned. If we assume that each worker produces 1.5 production, and that 1 production
            // is 100 silver, then that's only about 2250 - 3000 silver. That's a very small amount for settlement
            // trading.
            float extraScale = BaseExtraScale;

            FactionFC faction = FindFC.FactionComp;
            if (faction is object)
            {
                // Also scale by tech level
                switch (faction.techLevel)
                {
                    case TechLevel.Animal: extraScale *= 0.25f; break;
                    case TechLevel.Neolithic: extraScale *= 0.5f; break;
                    case TechLevel.Medieval: extraScale *= 0.75f; break;
                    case TechLevel.Industrial: extraScale *= 1f; break;
                    case TechLevel.Spacer: extraScale *= 1.5f; break;
                    case TechLevel.Ultra: extraScale *= 3f; break;
                    case TechLevel.Archotech: extraScale *= 10f; break;
                }
            }

            // The stock generator currently assumes 1 production = 100 silver. If the player has set something different,
            //   we should normalize.
            if (FCSettings.silverPerResource != 100 && FCSettings.silverPerResource != 0)
            {
                extraScale *= 100 / (float)(FCSettings.silverPerResource);
            }

            return extraScale;
        }
    }
}
