using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class ResourceFC : IExposable
    {
        public ResourceTypeDef def;
        public string label => def?.LabelCap;
        public WorldSettlementFC settlement;
        private int savedAssignedWorkers;
        public int assignedWorkers
        {
            get
            {
                return savedAssignedWorkers;
            }
            set
            {
                savedAssignedWorkers = value;
                FindFC.FactionComp?.SetDirtyResourceDisplayCache(def);
            }
        }

        /* All bonuses and maluses, even from biome or hilliness, should be applied through productionAdditives and productionMultipliers */
        private Dictionary<string, ProductionBonus> productionAdditives = new Dictionary<string, ProductionBonus>();
        private Dictionary<string, ProductionBonus> productionMultipliers = new Dictionary<string, ProductionBonus>();

        private bool dirtyProductionBaseCache = true;
        private bool dirtyProductionMultCache = true;
        private bool dirtyProductionBaseDescCache = true;
        private bool dirtyProductionMultDescCache = true;
        private double cachedProductionBase = 1;
        private double cachedProductionMult = 1;
        private TaggedString cachedProdBaseDesc = "";
        private TaggedString cachedProdMultDesc = "";
        private Texture2D iconLoaded;

        private double accruedRawProduction = 0;        // Sigma per-day raw production over the cycle
        private double accruedEffectiveProduction = 0;  // Sigma per-day post-stockpile production over the cycle
        private double accruedTitheBudget = 0;          // Sigma per-day tithe budget (post-multiplier + external)
        private double accruedExternalTitheBudget = 0;  // Sigma per-day external tithe budget (for breakdown)
        private int accrualDays = 0;

        /* Don't expose tithes publicly. We want values to be added or removed *only* through our special functions, so that we can dirty or set
         * cached values appropriately. */
        private List<TitheEntry> tithes = new List<TitheEntry>();
        public IReadOnlyList<TitheEntry> Tithes => tithes;
        private bool dirtyTitheCache = true;
        private double cachedTitheTotalValue = 0;
        /// <summary>
        /// The amount of budget available to this resource for random tithing. If the lowest-value random tithing thing is still higher in value than the available titheStock,
        /// then production is rolled over to the next tax period, until enough has accrued to actually produce the tithe.
        /// </summary>
        // When budget rolls over (no items enabled, or budget below the cheapest item) the player is alerted via the FCNoTitheLetter letters in GenerateTithe.
        public double randomTitheStock = 0;
        public bool disburseTitheStock = false;

        public bool tithesPaused = false;
        public bool hasRandomTithe = false;
        public bool autoMaxRandomTithe = false;
        public ThingFilter randomTitheFilter = new ThingFilter();
        private bool dirtyRandomTitheCache = true;
        private List<ThingDef> thingsForRandomTithes = new List<ThingDef>();
        private bool dirtyFilteredRandomTitheCache = true;
        private List<ThingDef> filteredThingsForRandomTithes = new List<ThingDef>();
        public string storedRandomTitheBudgetBuffer = "";
        public int storedRandomTitheBudget = 0;
        private int oldStoredRandomTitheBudget = 0;

        // Submods can register named allocations to siphon production away from taxes and tithes.
        // Each mod registers under its own key so multiple submods compose correctly.
        // Not persisted — submods are expected to re-register their allocations on load.
        private struct StockpileEntry
        {
            public double amount;                   // units diverted per DAY
            public Action<double, double> realize;  // (requested, actual) invoked each day; null allowed
        }
        private Dictionary<string, StockpileEntry> stockpileAllocations = new Dictionary<string, StockpileEntry>();
        public double totalStockpileAllocation => stockpileAllocations.Values.Sum(e => e.amount);

        /// <summary>
        /// Attempts to register a named production diversion for a stockpile.
        /// Returns false without registering if the amount would push total diversions above <see cref="rawTotalProduction"/>.
        /// If the key already exists, the old entry is replaced (using the new amount in the capacity check).
        /// </summary>
        /// <param name="key">Unique identifier for the calling mod (e.g. "MyMod.MyFeature").</param>
        /// <param name="realize">Optional callback invoked each day with (requested, actual) units diverted.</param>
        public bool SetStockpileAllocation(string key, double amount, Action<double, double> realize = null)
        {
            double currentForKey = stockpileAllocations.TryGetValue(key, out var existing) ? existing.amount : 0;
            if (totalStockpileAllocation - currentForKey + amount > rawTotalProduction)
                return false;
            stockpileAllocations[key] = new StockpileEntry { amount = amount, realize = realize };
            settlement?.DirtyProfitCache();
            return true;
        }

        /// <summary>Removes a previously registered stockpile allocation. The eviction callback is NOT invoked.</summary>
        public void ClearStockpileAllocation(string key)
        {
            stockpileAllocations.Remove(key);
            settlement?.DirtyProfitCache();
        }

        public int randomTitheBudget
        {
            get
            {
                if (!hasRandomTithe) return 0;
                if (autoMaxRandomTithe)
                {
                    RefreshTitheCacheIfDirty();
                    return Math.Max(0, (int)(GetTitheIncome() - cachedTitheTotalValue));
                }
                return storedRandomTitheBudget;
            }
            set
            {
                storedRandomTitheBudget = value;
                settlement.DirtyProfitCache();
            }
        }

        public double production => Math.Max(0, productionBase * productionMult);
        public double productionBase
        {
            get
            {
                if (dirtyProductionBaseCache)
                {
                    cachedProductionBase = CalculateProductionBase();
                    dirtyProductionBaseCache = false;
                }
                return cachedProductionBase;
            }
        }
        public double productionMult
        {
            get
            {
                if (dirtyProductionMultCache)
                {
                    cachedProductionMult = CalculateProductionMult();
                    dirtyProductionMultCache = false;
                }
                return cachedProductionMult;
            }
        }
        private void RefreshTitheCacheIfDirty()
        {
            if (dirtyTitheCache)
            {
                cachedTitheTotalValue = CalcTotalTitheValue();
                dirtyTitheCache = false;
            }
        }
        public double titheTotalValue
        {
            get
            {
                // Pool resources are always counted as though they are tithing, since you can't actually get any silver from them.
                // By design, a pool resource's full value goes into the pool — there is no player-facing pool/silver split.
                if (def.isPoolResource)
                {
                    return taxableProductionMarketValue;
                }
                if (tithesPaused)
                {
                    return 0;
                }
                RefreshTitheCacheIfDirty();
                return cachedTitheTotalValue + randomTitheBudget;
            }
        }
        public double titheTotalValueNoRandom => titheTotalValue - randomTitheBudget;
        /* NOTE: the production property chain flows as follows:
         *  rawTotalProduction          — gross output (units), before any splits. Display this as "Total Production".
         *  effectiveRawTotalProduction — post-stockpile output (units); what remains after submod allocations are diverted.
         *  grossMarketValue            — silver value of rawTotalProduction; displayed as "Raw Income" (before any deductions).
         *  stockpileMarketValue        — silver value of totalStockpileAllocation; used in Net Income tooltip breakdown.
         *  taxableProductionMarketValue — silver value of effectiveRawTotalProduction; the budget available to taxes and tithes.
         *  actualIncome                — taxableProductionMarketValue minus tithe costs; displayed as "Net Income".
         *                                Can be negative if tithe modifiers push the tithe value above taxable production.
         */
        /// <summary>Current snapshot: production * workers, ignoring accumulation.</summary>
        public double InstantaneousProduction => production * assignedWorkers;

        public double AccruedRawProduction => accruedRawProduction;
        public double AccruedEffectiveProduction => accruedEffectiveProduction;
        public double AccruedEffectiveProductionForTest => accruedEffectiveProduction; // test hook
        public double AccruedTitheBudget => accruedTitheBudget;
        public double AccruedExternalTitheBudget => accruedExternalTitheBudget;
        public int AccrualDays => accrualDays;

        /// <summary>Accrued gross silver value (post-stockpile, pre-tithe). Zero for pool resources.</summary>
        public double AccruedTaxableValue => def.isPoolResource ? 0 : accruedEffectiveProduction * FCSettings.silverPerResource;

        /// <summary>Live instantaneous per-day rate (production * workers). For display and projections.</summary>
        public double rawTotalProduction => InstantaneousProduction;
        public double effectiveRawTotalProduction => rawTotalProduction - totalStockpileAllocation;
        public double grossMarketValue => rawTotalProduction * FCSettings.silverPerResource;
        public double stockpileMarketValue => totalStockpileAllocation * FCSettings.silverPerResource;
        public double taxableProductionMarketValue => effectiveRawTotalProduction * FCSettings.silverPerResource;
        /// <summary>
        /// Aggregates additional tithe budget (in silver) from all <see cref="ITitheBudgetModifier"/> comps
        /// on this settlement. Added to the tithe income cap; in actualIncome, only the portion of tithe
        /// covered by this budget is offset (capped to titheTotalValue).
        /// </summary>
        public double DailyExternalTitheBudget
        {
            get
            {
                double total = 0;
                if (settlement is object)
                {
                    foreach (WorldObjectComp comp in settlement.AllComps)
                    {
                        if (comp is ITitheBudgetModifier provider)
                            total += provider.GetDailyExternalTitheBudget(this);
                    }
                }
                return total;
            }
        }
        public double actualIncome => taxableProductionMarketValue - titheTotalValue + Math.Min(titheTotalValue, DailyExternalTitheBudget);

        public bool canTithe => !def.isPoolResource && def.canTithe;

        public Texture2D getIcon
        {
            get
            {
                if (iconLoaded != null) return iconLoaded;

                if (def != null)
                {
                    iconLoaded = def.Icon;
                }
                else
                {
                    LogUtil.Error("Failed to load icon for ResourceFC: no associated resourceDef!");
                    iconLoaded = TexLoad.questionmark;
                }
                return iconLoaded;
            }
        }

        public ResourceFC()
        {
        }

        public ResourceFC(ResourceTypeDef resourceDef, WorldSettlementFC settlement = null)
        {
            this.settlement = settlement;
            def = resourceDef;
            if (resourceDef == null)
            {
                /* This is a super bad case that should never happen. Find a way to make this a bigger error? */
                LogUtil.Error($"Created ResourceFC with NULL resourceDef!");
            }
            randomTitheFilter = new ThingFilter();
            randomTitheBudget = 0;
            productionAdditives.Clear();
            productionMultipliers.Clear();
            if (settlement != null)
            {
                SetBaseResourceBonuses();
            }
            ResetThingFilter();
        }


        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_Collections.Look(ref productionAdditives, "productionAdditives", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref productionMultipliers, "productionMultiplers", LookMode.Value, LookMode.Deep);

            //tithe and income data
            Scribe_Values.Look(ref savedAssignedWorkers, "assignedWorkers");
            // New ordered-list node. Legacy dictionary dual-read below seeds it from old saves.
            Scribe_Collections.Look(ref tithes, "titheList", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (tithes is null) tithes = new List<TitheEntry>();
                if (tithes.Count == 0)
                {
                    // Legacy: read the old Dictionary<ThingQualityTuple,int> node "tithes" and seed the list.
                    Dictionary<ThingQualityTuple, int> legacy = null;
                    Scribe_Collections.Look(ref legacy, "tithes", LookMode.Deep, LookMode.Value);
                    if (legacy is object)
                        foreach (var kv in legacy)
                            tithes.Add(new TitheEntry(kv.Key, kv.Value)); // arbitrary order — player re-prioritizes
                }
            }
            Scribe_Deep.Look(ref randomTitheFilter, "filter");
            Scribe_Values.Look(ref storedRandomTitheBudget, "randomTitheBudget");
            Scribe_Values.Look(ref hasRandomTithe, "hasRandomTithe");
            Scribe_Values.Look(ref autoMaxRandomTithe, "autoMaxRandomTithe");
            Scribe_Values.Look(ref tithesPaused, "tithesPaused", defaultValue: false);

            //Tax Stock
            Scribe_Values.Look(ref randomTitheStock, "taxStock");
            Scribe_Values.Look(ref disburseTitheStock, "disburseTaxStock");

            Scribe_References.Look(ref settlement, "settlement");

            Scribe_Values.Look(ref accruedRawProduction, "accruedRawProduction", 0);
            Scribe_Values.Look(ref accruedEffectiveProduction, "accruedEffectiveProduction", 0);
            Scribe_Values.Look(ref accruedTitheBudget, "accruedTitheBudget", 0);
            Scribe_Values.Look(ref accruedExternalTitheBudget, "accruedExternalTitheBudget", 0);
            Scribe_Values.Look(ref accrualDays, "accrualDays", 0);
        }

        /// <summary>
        /// Calculates the total production base from three sources:
        /// 1. ProductionBonus dict: static environmental bonuses (biome, hilliness, settlement type)
        /// 2. FCStatDef system: dynamic bonuses from buildings, policies, events
        /// 3. IResourceProductionModifier comps: dynamic per-resource bonuses from WorldObjectComps
        /// </summary>
        private double CalculateProductionBase()
        {
            double dictBase = ResourceFormulas.CalculateProductionBase(productionAdditives.Values.Select(p => p.value));
            double statBase = (settlement != null && def.productionAdditiveStat != null)
                ? settlement.GetStatValue(def.productionAdditiveStat) : 0;
            double compBase = 0;
            if (settlement != null)
            {
                foreach (WorldObjectComp comp in settlement.AllComps)
                {
                    if (comp is IResourceProductionModifier provider)
                        compBase += provider.GetResourceAdditiveModifier(this);
                }
            }
            double workerBase = (settlement != null)
                ? settlement.GetStatValue(FCStatDefOf.workerProductionBase) + FCSettings.workerProductionBaseBonus : 0;
            return dictBase + statBase + compBase + workerBase;
        }
        /// <summary>
        /// Calculates the total production multiplier from three sources:
        /// 1. ProductionBonus dict: static environmental multipliers (biome, hilliness, settlement type)
        /// 2. FCStatDef system: dynamic multipliers from buildings, policies, events
        /// 3. IResourceProductionModifier comps: dynamic per-resource multipliers from WorldObjectComps
        /// Also includes the settlement tax bonus.
        /// </summary>
        private double CalculateProductionMult()
        {
            double taxBonus = settlement?.GetSettlementTaxBonus() ?? 1;
            double dictMult = ResourceFormulas.CalculateProductionMult(productionMultipliers.Values.Select(p => p.value), 1.0);
            double statMult = (settlement != null && def.productionMultiplierStat != null)
                ? settlement.GetStatValue(def.productionMultiplierStat) : 1;
            double compMult = 1;
            if (settlement != null)
            {
                foreach (WorldObjectComp comp in settlement.AllComps)
                {
                    if (comp is IResourceProductionModifier provider)
                        compMult *= provider.GetResourceMultiplierModifier(this);
                }
            }
            double prosperityMult = (settlement != null) ? (settlement.prosperity / 100.0) : 1.0;
            double workerMult = (settlement != null)
                ? settlement.GetStatValue(FCStatDefOf.workerProductionMultiplier) * FCSettings.workerProductionMultBonus : 1;
            return dictMult * statMult * compMult * taxBonus * prosperityMult * workerMult;
        }
        public double GetTitheModifierPerWorker()
        {
            return settlement.GetStatValue(FCStatDefOf.taxBaseRandomModifier) + FCSettings.productionTitheMod;
        }
        public double GetTotalTitheModifierForWorkers()
        {
            return GetTitheModifierPerWorker() * assignedWorkers;
        }
        public double GetTitheValueMultiplier()
        {
            return FindFC.FactionComp.GetStatValue(FCStatDefOf.titheValueMultiplier, settlement);
        }
        public double GetTitheIncome()
        {
            double multForTotal = GetTitheValueMultiplier();
            return ((taxableProductionMarketValue + GetTotalTitheModifierForWorkers()) * multForTotal) + DailyExternalTitheBudget;
        }
        public void RefreshOnRandomTitheBudgetChange()
        {
            if (storedRandomTitheBudget != oldStoredRandomTitheBudget)
            {
                oldStoredRandomTitheBudget = storedRandomTitheBudget;
                settlement.DirtyProfitCache();
            }
        }

        /* Tithe-flag mutators: prefer these over assigning the fields directly so the
         * profit cache stays in sync. */
        public void SetTithesPaused(bool paused)
        {
            if (tithesPaused == paused) return;
            tithesPaused = paused;
            settlement?.DirtyProfitCache();
        }

        public void SetHasRandomTithe(bool enabled)
        {
            if (hasRandomTithe == enabled) return;
            hasRandomTithe = enabled;
            settlement?.DirtyProfitCache();
        }

        public void SetDisburseTitheStock(bool enabled)
        {
            if (disburseTitheStock == enabled) return;
            disburseTitheStock = enabled;
            settlement?.DirtyProfitCache();
        }

        public void SetAutoMaxRandomTithe(bool enabled)
        {
            if (autoMaxRandomTithe == enabled) return;
            bool wasEnabled = autoMaxRandomTithe;
            autoMaxRandomTithe = enabled;
            // When toggling auto-max off, snap stored budget to current effective max.
            if (!enabled && wasEnabled)
            {
                storedRandomTitheBudget = Math.Max(0, (int)(GetTitheIncome() - titheTotalValueNoRandom));
                storedRandomTitheBudgetBuffer = storedRandomTitheBudget.ToString();
            }
            settlement?.DirtyProfitCache();
        }

        public void SetStoredRandomTitheBudget(int budget)
        {
            int clamped = Math.Max(0, budget);
            if (storedRandomTitheBudget == clamped) return;
            storedRandomTitheBudget = clamped;
            storedRandomTitheBudgetBuffer = storedRandomTitheBudget.ToString();
            RefreshOnRandomTitheBudgetChange();
        }

        public void SetDirtyCache()
        {
            SetDirtyCacheProdBase();
            SetDirtyCacheProdMult();
            dirtyTitheCache = true;
            SetDirtyRandomTitheCache();
            dirtyFilteredRandomTitheCache = true;
            FindFC.FactionComp?.SetDirtyResourceDisplayCache(def);
        }
        public void SetDirtyRandomTitheCache()
        {
            dirtyRandomTitheCache = true;
            settlement.DirtyGrandThingList();
        }
        public void SetDirtyCacheProdBase()
        {
            dirtyProductionBaseCache = true;
            dirtyProductionBaseDescCache = true;
            dirtyTitheCache = true;
        }
        public void SetDirtyCacheProdMult()
        {
            dirtyProductionMultCache = true;
            dirtyProductionMultDescCache = true;
            dirtyTitheCache = true;
        }

        public void AccumulateDailyProduction()
        {
            double dayProduction = InstantaneousProduction; // per-day raw
            accruedRawProduction += dayProduction;

            // Daily stockpile diversion: deliver what exists, never drive effective negative.
            double remaining = dayProduction;
            foreach (var kv in new List<KeyValuePair<string, StockpileEntry>>(stockpileAllocations)) // snapshot: realize may mutate
            {
                double requested = kv.Value.amount;
                double actual = Math.Min(requested, Math.Max(0, remaining));
                remaining -= actual;
                kv.Value.realize?.Invoke(requested, actual);
            }
            double dayEffective = Math.Max(0, remaining);
            accruedEffectiveProduction += dayEffective;

            // Per-day tithe budget accrual (only for tithe-eligible resources).
            if (canTithe)
            {
                double mult = GetTitheValueMultiplier();
                double dayExternal = DailyExternalTitheBudget;
                double dayTaxable = dayEffective * FCSettings.silverPerResource;
                double dayWorkerMod = GetTotalTitheModifierForWorkers(); // uses per-day productionTitheMod
                accruedExternalTitheBudget += dayExternal;
                accruedTitheBudget += (dayTaxable + dayWorkerMod) * mult + dayExternal;
            }

            accrualDays++;
        }

        public void ResetAccumulator()
        {
            accruedRawProduction = 0;
            accruedEffectiveProduction = 0;
            accruedTitheBudget = 0;
            accruedExternalTitheBudget = 0;
            accrualDays = 0;
        }

        public ResourcePool CreatePool()
        {
            ResourcePool pool = new ResourcePool
            {
                resource = def,
                pool = 0
            };
            if (def.isPoolResource)
            {
                pool.pool += def.GetModExtension<ResourcePoolExtension>().CreatePool(AccruedEffectiveProduction, settlement);
            }
            return pool;
        }

        /*
         * Production Bonus Initialization
         */

        /// <summary>
        /// Populates both <see cref="productionAdditives"/> and <see cref="productionMultipliers"/> from
        /// the biome, settlement type, and any <see cref="ResourceProductionExtension"/>s on the resource def.
        /// Called once at construction.
        /// </summary>
        private void SetBaseResourceBonuses()
        {
            if (settlement == null) return;

            string settlementId = settlement.Name ?? "nullsettlement";

            // --- Biome bonuses ---
            ResourceAvailability biomeRes = settlement.biomeDef.GetBiomeResource(def);
            if (biomeRes == null)
            {
                LogUtil.Error($"Found NULL biomeBonus for resource {def} in settlement {settlement.Name}, despite the ResourceFC already existing");
            }
            else
            {
                string biomeId = $"{def.defName}_biome_{settlement.biomeDef.defName}_{settlementId}";
                if (biomeRes.additive != 0)
                    AddProductionAdditive(biomeId, biomeRes.additive, settlement.biomeDef.LabelCap);
                if (biomeRes.multiplier != 1)
                    AddProductionMultiplier(biomeId, biomeRes.multiplier, settlement.biomeDef.LabelCap);
            }

            // --- Settlement type bonuses ---
            ResourceAvailability settleRes = settlement.settlementDef.GetSettlementResource(def);
            if (settleRes != null)
            {
                string settleId = $"{def.defName}_settle_{settlement.settlementDef.defName}_{settlementId}";
                if (settleRes.additive != 0)
                    AddProductionAdditive(settleId, settleRes.additive, settlement.settlementDef.LabelCap);
                if (settleRes.multiplier != 1)
                    AddProductionMultiplier(settleId, settleRes.multiplier, settlement.settlementDef.LabelCap);
            }

            // --- ResourceProductionExtension bonuses ---
            if (def?.modExtensions != null)
            {
                foreach (ResourceProductionExtension ext in def.modExtensions.OfType<ResourceProductionExtension>())
                {
                    ext.ContributeToBreakdown(
                        settlement.Tile,
                        settlement,
                        (suffix, v, label) => AddProductionAdditive($"{def.defName}_ext_{suffix}_{settlementId}", v, label),
                        (suffix, v, label) => AddProductionMultiplier($"{def.defName}_ext_{suffix}_{settlementId}", v, label));
                }
            }

            // --- Tile landmark bonuses ---
            Landmark landmark = settlement.Tile.Tile?.Landmark;
            TileLandmarkResourceExtension lmExt = landmark?.def?.GetModExtension<TileLandmarkResourceExtension>();
            if (lmExt?.bonuses != null)
            {
                foreach (TileResourceBonus entry in lmExt.bonuses)
                {
                    if (entry.resource != def) continue;
                    string lmId = $"{def.defName}_landmark_{landmark.def.defName}_{settlementId}";
                    string lmLabel = entry.label.NullOrEmpty() ? landmark.def.LabelCap.ToString() : entry.label;
                    if (entry.additive != 0)
                        AddProductionAdditive(lmId, entry.additive, lmLabel);
                    if (entry.multiplier != 1)
                        AddProductionMultiplier(lmId, entry.multiplier, lmLabel);
                }
            }

            // --- Tile mutator bonuses ---
            IList<TileMutatorDef> tileMutators = settlement.Tile.Tile?.Mutators;
            if (tileMutators != null && tileMutators.Count > 0)
            {
                foreach (TileMutatorDef mut in tileMutators)
                {
                    TileMutatorResourceExtension mutExt = mut?.GetModExtension<TileMutatorResourceExtension>();
                    if (mutExt?.bonuses is null) continue;
                    foreach (TileResourceBonus entry in mutExt.bonuses)
                    {
                        if (entry.resource != def) continue;
                        string mutId = $"{def.defName}_mutator_{mut.defName}_{settlementId}";
                        string mutLabel = entry.label.NullOrEmpty() ? mut.LabelCap.ToString() : entry.label;
                        if (entry.additive != 0)
                            AddProductionAdditive(mutId, entry.additive, mutLabel);
                        if (entry.multiplier != 1)
                            AddProductionMultiplier(mutId, entry.multiplier, mutLabel);
                    }
                }
            }
        }
        public void AddProductionAdditive(string id, double value, string desc)
        {
            ProductionBonus additive = new ProductionBonus(value, desc);

            if (desc.NullOrEmpty())
            {
                LogUtil.Warning($"Created a production additive for resource {label} in settlement {settlement.Name} with an empty description! (id: {id})");
            }
            AddProductionAdditive(id, additive);
        }

        private void AddProductionAdditive(string id, ProductionBonus additive)
        {
            try
            {
                productionAdditives.Add(id, additive);
            }
            catch (Exception e)
            {
                LogUtil.Error($"Failed when adding ProductionBonus additive with id {id}: {e.Message}");
            }
            SetDirtyCacheProdBase();
        }
        public void RemoveProductionAdditiveById(string id)
        {
            productionAdditives.Remove(id);
            SetDirtyCacheProdBase();
        }
        public TaggedString GetProductionAdditivesDesc()
        {
            if (dirtyProductionBaseDescCache)
            {
                TaggedString desc = "";
                foreach (ProductionBonus additive in productionAdditives.Values)
                {
                    desc += TextUtil.ColorizeAdditiveBonus(additive.value) + " - " + additive.desc + "\n";
                }
                if (def.productionAdditiveStat != null && settlement != null)
                {
                    desc += settlement.GetStatDesc(def.productionAdditiveStat);
                }
                if (settlement != null)
                {
                    foreach (WorldObjectComp comp in settlement.AllComps)
                    {
                        if (comp is IResourceProductionModifier provider)
                        {
                            string compDesc = provider.GetResourceAdditiveDesc(this);
                            if (!compDesc.NullOrEmpty())
                                desc += compDesc + "\n";
                        }
                    }
                }
                if (settlement != null)
                {
                    desc += settlement.GetStatDesc(FCStatDefOf.workerProductionBase);
                }
                if (FCSettings.workerProductionBaseBonus != 0f)
                {
                    desc += TextUtil.AdditiveBonusLine(FCSettings.workerProductionBaseBonus,
                        "FCDifficultyWorkerProductionBaseBonus".Translate()) + "\n";
                }
                cachedProdBaseDesc = desc.Trim();
                dirtyProductionBaseDescCache = false;
            }
            return cachedProdBaseDesc;
        }

        public void AddProductionMultiplier(string id, double value, string desc)
        {
            ProductionBonus multiplier = new ProductionBonus(value, desc);

            if (desc.NullOrEmpty())
            {
                LogUtil.Warning($"Created a production multiplier for resource {label} in settlement {settlement.Name} with an empty description! (id: {id})");
            }
            AddProductionMultiplier(id, multiplier);
        }

        private void AddProductionMultiplier(string id, ProductionBonus multiplier)
        {
            try
            {
                productionMultipliers.Add(id, multiplier);
            }
            catch (Exception e)
            {
                LogUtil.Error($"Failed when adding ProductionBonus multiplier with id {id}: {e.Message}");
            }
            SetDirtyCacheProdMult();
        }
        public void RemoveProductionMultiplierById(string id)
        {
            productionMultipliers.Remove(id);
            SetDirtyCacheProdMult();
        }
        public TaggedString GetProductionMultipliersDesc()
        {
            if (dirtyProductionMultDescCache)
            {
                TaggedString desc = "";
                foreach (ProductionBonus multiplier in productionMultipliers.Values)
                {
                    desc += TextUtil.ColorizeMultiplierBonus(multiplier.value) + " - " + multiplier.desc + "\n";
                }
                if (def.productionMultiplierStat != null && settlement != null)
                {
                    desc += settlement.GetStatDesc(def.productionMultiplierStat);
                }
                if (settlement != null)
                {
                    foreach (WorldObjectComp comp in settlement.AllComps)
                    {
                        if (comp is IResourceProductionModifier provider)
                        {
                            string compDesc = provider.GetResourceMultiplierDesc(this);
                            if (!compDesc.NullOrEmpty())
                                desc += compDesc + "\n";
                        }
                    }
                }
                if (settlement != null)
                {
                    desc += settlement.GetStatDesc(FCStatDefOf.workerProductionMultiplier);
                }
                if (FCSettings.workerProductionMultBonus != 1f)
                {
                    desc += TextUtil.MultiplierBonusLine(FCSettings.workerProductionMultBonus,
                        "FCDifficultyWorkerProductionMultBonus".Translate()) + "\n";
                }
                desc += TextUtil.ColorizeMultiplierBonus(settlement?.GetSettlementTaxBonus() ?? 1) + " - " + "FCTaxBase".Translate();

                if (settlement != null)
                {
                    double prosperityMult = settlement.prosperity / 100.0;
                    desc += "\n" + TextUtil.MultiplierBonusLine(prosperityMult,
                        "FCProsperity".Translate().CapitalizeFirst() + " (" + (int)settlement.prosperity + "%)");
                }

                cachedProdMultDesc = desc.Trim();
                dirtyProductionMultDescCache = false;
            }
            return cachedProdMultDesc;
        }

        /*
         * Filter functions
         */
        public void ResetThingFilter()
        {
            FactionFC faction = FindFC.FactionComp;

            if (def == null)
                return;

            def.FilterResource(randomTitheFilter, faction.techLevel, this);
        }
        /// <summary>
        /// Generates a list of ThingDefs that can be generated as tithes for this resource.
        /// </summary>
        /// <returns></returns>
        public List<ThingDef> GenerateThingDefList()
        {
            if (def.isPoolResource)
            {
                LogUtil.Error($"Attempted to generate thing list for pool resource {def.defName} in settlement {settlement.Name}");
                return null;
            }
            if (dirtyRandomTitheCache)
            {
                if (randomTitheFilter == null)
                {
                    randomTitheFilter = new ThingFilter();
                    ResetThingFilter();
                }

                FactionFC faction = FindFC.FactionComp;
                ThingSetMaker thingSetMaker = new ThingSetMaker_Count();
                ThingSetMakerParams param = new ThingSetMakerParams();
                param.filter = new ThingFilter();
                param.techLevel = FindFC.EmpireFaction.def.techLevel;
                param.countRange = new IntRange(1, 1);

                TechLevel tmplevel = TechLevel.Undefined;
                ThingSetMaker tmp = def.GetModExtension<ResourceFilterExtension>()?.GetThingSetMaker(out tmplevel, this);
                if (tmp != null)
                {
                    thingSetMaker = tmp;
                    param.techLevel = tmplevel;
                }

                def.FilterResource(param.filter, faction.techLevel, this);

                /* AllGenerateableThingsDebug(param).ToList() was taken from PaymentUtil.debugGenerateTithe(), which was used to generate the selection float menu
                 * in the settlement screen. */
                thingsForRandomTithes = thingSetMaker.AllGeneratableThingsDebug(param).ToList();
                dirtyRandomTitheCache = false;
            }
            return thingsForRandomTithes;
        }
        public List<ThingDef> GetRandomTitheFilterThings()
        {
            if (dirtyFilteredRandomTitheCache)
            {
                if (randomTitheFilter == null)
                {
                    randomTitheFilter = new ThingFilter();
                    ResetThingFilter();
                }

                filteredThingsForRandomTithes = new List<ThingDef>();
                List<ThingDef> possibleThings = GenerateThingDefList();

                foreach (ThingDef thingDef in possibleThings)
                {
                    if (randomTitheFilter.Allows(thingDef))
                    {
                        filteredThingsForRandomTithes.Add(thingDef);
                    }
                }

                dirtyFilteredRandomTitheCache = false;
            }
            return filteredThingsForRandomTithes;
        }
        public void ClearRandomTitheFilter()
        {
            if (randomTitheFilter == null)
            {
                randomTitheFilter = new ThingFilter();
            }
            randomTitheFilter.SetDisallowAll();
            SetDirtyRandomTitheCache();
            dirtyFilteredRandomTitheCache = true;
        }
        public void SetAllRandomTitheFilter()
        {
            if (randomTitheFilter == null)
            {
                randomTitheFilter = new ThingFilter();
            }
            ResetThingFilter();
            SetDirtyRandomTitheCache();
            dirtyFilteredRandomTitheCache = true;
        }
        public void SetRandomTitheFilterAllow(ThingDef thing, bool allow)
        {
            if (randomTitheFilter == null)
            {
                randomTitheFilter = new ThingFilter();
                ResetThingFilter();
                SetDirtyRandomTitheCache();
            }

            randomTitheFilter.SetAllow(thing, allow);
            dirtyFilteredRandomTitheCache = true;
        }
        // could cache this with a dictionary, but probably best to see if there's an actual performance problem first
        public bool GetRandomTitheFilterAllow(ThingDef thing)
        {
            return randomTitheFilter.Allows(thing);
        }
        /* - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - *
         *   Tithe functions                                                                                                                                             *
         * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - */
        /// <summary>
        /// Appends a new entry to the tithes list at lowest priority. The same thing may appear
        /// multiple times — entries are identified by position, not by their ThingQualityTuple.
        /// <para>This function does not check if the given <paramref name="quantity"/> of <paramref name="thing"/> can actually be afforded.</para>
        /// <para>This function dirties the tithe cache, forcing a recalculation of the total tithe value.</para>
        /// </summary>
        /// <param name="thing">A ThingQualityTuple specifying the ThingDef, QualityCategory, and StuffDef of the thing to add.</param>
        /// <param name="quantity">The quantity for the new entry. Should always be a non-negative value.</param>
        /// <returns>TRUE if the entry was successfully appended, FALSE otherwise.</returns>
        public bool AddTitheEntry(ThingQualityTuple thing, int quantity)
        {
            if (quantity < 0)
            {
                LogUtil.Error($"Tried to add a negative quantity to the tithes list for resource {def.LabelCap}.");
                return false;
            }
            tithes.Add(new TitheEntry(thing, quantity)); // appended at lowest priority
            dirtyTitheCache = true;
            settlement.DirtyProfitCache();
            return true;
        }
        /// <summary>
        /// Sets the quantity of the tithe entry at <paramref name="index"/> (clamped to non-negative).
        /// <para>This function dirties the tithe cache, forcing a recalculation of the total tithe value.</para>
        /// </summary>
        public void SetTitheQuantityAt(int index, int quantity)
        {
            if (index < 0 || index >= tithes.Count) return;
            tithes[index].quantity = Math.Max(0, quantity);
            dirtyTitheCache = true;
            settlement.DirtyProfitCache();
        }
        /// <summary>
        /// Replaces the ThingQualityTuple of the tithe entry at <paramref name="index"/> in place,
        /// preserving the entry's quantity and priority. Used by the quality/stuff edit buttons.
        /// <para>This function dirties the tithe cache, forcing a recalculation of the total tithe value.</para>
        /// </summary>
        public void SetTitheThingAt(int index, ThingQualityTuple newThing)
        {
            if (index < 0 || index >= tithes.Count) return;
            tithes[index].thing = newThing;
            dirtyTitheCache = true;
            settlement.DirtyProfitCache();
        }
        /// <summary>
        /// Removes the tithe entry at <paramref name="index"/>.
        /// <para>This function dirties the tithe cache, forcing a recalculation of the total tithe value.</para>
        /// </summary>
        public void RemoveTitheAt(int index)
        {
            if (index < 0 || index >= tithes.Count) return;
            tithes.RemoveAt(index);
            dirtyTitheCache = true;
            settlement.DirtyProfitCache();
        }
        public int GetTitheListCount()
        {
            return tithes.Count;
        }
        /* Reorder a tithe entry (index 0 = highest priority). Clamps indices; no-op if out of range. */
        public void MoveTitheEntry(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= tithes.Count) return;
            toIndex = Mathf.Clamp(toIndex, 0, tithes.Count - 1);
            if (fromIndex == toIndex) return;
            TitheEntry e = tithes[fromIndex];
            tithes.RemoveAt(fromIndex);
            tithes.Insert(toIndex, e);
            dirtyTitheCache = true;
            settlement.DirtyProfitCache();
        }
        public bool CanSetTitheQuality(out QualityCategory maxQuality)
        {
            // Quality selection is intentionally unrestricted. A building or stat could gate the max quality here in future.
            maxQuality = QualityCategory.Legendary;
            return true;
        }
        public List<QualityCategory> GetValidTitheQualities(QualityCategory maxQuality)
        {
            List<QualityCategory> list = QualityUtility.AllQualityCategories;
            for (int i = list.Count - 1; i > 0; i--)
            {
                if (list[i] > maxQuality)
                {
                    list.RemoveAt(i);
                }
            }

            return list;
        }
        public bool CanSetTitheStuff()
        {
            // Stuff selection is intentionally unrestricted. A building or stat could gate this here in future.
            return true;
        }
        public List<ThingDef> GetStuffListForThingDef(ThingDef thing)
        {
            return CraftUtil.GetThingStuffs(thing, settlement.GetGrandThingList());
        }
        public float TitheThingValue(ThingQualityTuple thing)
        {
            return CraftUtil.ThingValue(thing);
        }
        public float TitheThingValue(ThingDef thing, ThingDef stuff, QualityCategory quality)
        {
            return CraftUtil.ThingValue(thing, stuff, quality);
        }
        public float TitheThingTotalValue(ThingQualityTuple thing, int quanity)
        {
            return CraftUtil.ThingValue(thing) * quanity;
        }
        public bool CanAffordThingAmount(ThingQualityTuple thing, int quanity)
        {
            double available;
            if (autoMaxRandomTithe)
            {
                RefreshTitheCacheIfDirty();
                available = GetTitheIncome() - cachedTitheTotalValue;
            }
            else
            {
                available = GetTitheIncome() - titheTotalValue;
            }
            return ResourceFormulas.CanAffordThingAmount(TitheThingTotalValue(thing, quanity), available);
        }
        public int MaxThingCanAfford(ThingQualityTuple thing)
        {
            double available;
            if (autoMaxRandomTithe)
            {
                RefreshTitheCacheIfDirty();
                available = GetTitheIncome() - cachedTitheTotalValue;
            }
            else
            {
                available = GetTitheIncome() - titheTotalValue;
            }
            return MaxThingCanAfford(thing, available);
        }
        public int MaxThingCanAfford(ThingQualityTuple thing, double budget)
        {
            return ResourceFormulas.MaxThingCanAfford(budget, CraftUtil.ThingValue(thing));
        }
        public float CalcTotalTitheValue()
        {
            float total = 0;
            foreach (TitheEntry e in tithes)
                total += TitheThingTotalValue(e.thing, e.quantity);
            return total;
        }
        public ThingQualityTuple FindHighestValueTitheThing()
        {
            ThingQualityTuple maxthing = null;
            float maxval = 0;
            foreach (TitheEntry e in tithes)
            {
                if (e.quantity <= 0) continue;
                float val = TitheThingValue(e.thing);
                if (val > maxval) { maxthing = e.thing; maxval = val; }
            }
            return maxthing;
        }
        public List<Thing> GenerateTithe(out int extraSilver)
        {
            int outSilver = 0;
            List<Thing> titheItems = new List<Thing>();

            //Sanity check to make sure that this function is only being called after the proper prep
            if (!settlement.IsCalculatingTax)
            {
                LogUtil.Error($"Attempted to generate tithe for resource {label} in settlement {settlement.Name} outside of tax phase");
                extraSilver = outSilver;
                return null;
            }

            if (def.isPoolResource)
            {
                LogUtil.Error($"Attempted to generate tithe for pool resource {label} in settlement {settlement.Name}");
                extraSilver = outSilver;
                return null;
            }

            // Pre-tax generation hook
            def.GetModExtension<ResourceTaxExtension>()?.OnPreTaxGeneration(this, settlement);

            // Prepare the tithe filter. I don't think it should ever be null here, but we'll account for that, just in case.
            if (randomTitheFilter == null)
            {
                randomTitheFilter = new ThingFilter();
                ResetThingFilter();
            }

            // Determine random tithing budget (computed here; consumed after the specified-tithe walk below).
            // randomTitheStock is an escrow: any value it holds was already withheld from silver when it
            // accrued (see the rollover branches below), so releasing it must add silver exactly once.
            double priorStock = randomTitheStock;
            if (disburseTitheStock && priorStock > 0)
            {
                outSilver += (int)priorStock;
                priorStock = 0;
                randomTitheStock = 0;
            }
            // randomTitheBudget is a per-day amount; scale it to the days accrued this cycle so the random
            // tithe can consume its full share of the accrued budget (not just ~one day's worth).
            double randomBudget = randomTitheBudget * AccrualDays + priorStock;

            // Walk the ordered priority list against the accrued tithe budget. Fully fulfil while affordable,
            // partial-fill the straddler, then stop. Unfulfilled entries persist untouched (no prune).
            double budgetRemaining = AccruedTitheBudget; // accrued over the whole cycle
            foreach (TitheEntry entry in tithes)
            {
                if (entry.quantity <= 0) continue;
                float unit = CraftUtil.ThingValue(entry.thing);
                if (unit <= 0) continue;

                int affordable = entry.quantity;
                double entryCost = unit * entry.quantity;
                if (entryCost > budgetRemaining)
                    affordable = ResourceFormulas.MaxThingCanAfford(budgetRemaining, unit); // partial straddler
                if (affordable <= 0) break; // budget exhausted; remaining entries persist for next cycle

                budgetRemaining -= unit * affordable;

                List<Thing> things = def.GetModExtension<ResourceFilterExtension>()
                    ?.GenerateSpecificThings(entry.thing.thingDef, affordable, entry.thing.quality, entry.thing.stuffDef, this);
                if (things is null)
                {
                    for (int i = 0; i < affordable; i++)
                    {
                        Thing thing = ThingMaker.MakeThing(entry.thing.thingDef, entry.thing.stuffDef);
                        if (CraftUtil.ThingHasQuality(entry.thing.thingDef))
                            thing.TryGetComp<CompQuality>().SetQuality(entry.thing.quality, ArtGenerationContext.Outsider);
                        titheItems.Add(thing);
                    }
                }
                else
                {
                    titheItems.AddRange(things);
                }
            }

            // Random tithe is the lowest-priority consumer; it draws from the post-specified remainder.
            double effectiveRandomBudget = Math.Min(randomBudget, budgetRemaining);
            if (hasRandomTithe && effectiveRandomBudget > 0)
            {
                if (!randomTitheFilter.AllowedThingDefs.Any())
                {
                    outSilver -= (int)(effectiveRandomBudget - priorStock); // withhold the new production escrowed this cycle
                    randomTitheStock = effectiveRandomBudget;
                    Find.LetterStack.ReceiveLetter("FCNoTitheLetterLabel".Translate(settlement.Name), "FCNoTitheLetterDesc".Translate(settlement.Name, label, randomTitheStock), LetterDefOf.NeutralEvent);
                }
                else
                {
                    // Calculate the random tithe
                    double minimum = randomTitheFilter.AllowedThingDefs.Aggregate<ThingDef, double>(double.MaxValue, (current, thing) => Math.Min(thing?.BaseMarketValue ?? 100, current));
                    LogUtil.Message($"{settlement.Name}, resource {label}, minimum random tithe: {minimum}, budget: {effectiveRandomBudget}");
                    if (minimum <= effectiveRandomBudget)
                    {
                        List<Thing> randomTitheList = new List<Thing>();
                        ThingSetMaker thingSetMaker = new ThingSetMaker_MarketValue();
                        ThingSetMakerParams param = new ThingSetMakerParams();
                        param.totalMarketValueRange = new FloatRange((float)effectiveRandomBudget, (float)(effectiveRandomBudget + GetTotalTitheModifierForWorkers()));
                        param.filter = randomTitheFilter;
                        param.techLevel = FindFC.EmpireFaction.def.techLevel;

                        LogUtil.Message($"  randomTitheFilter has {randomTitheFilter.AllowedDefCount} allowed items");

                        TechLevel tmplevel = TechLevel.Undefined;
                        ThingSetMaker tmp = def.GetModExtension<ResourceFilterExtension>()?.GetThingSetMaker(out tmplevel, this);
                        if (tmp != null)
                        {
                            thingSetMaker = tmp;
                            param.techLevel = tmplevel;
                        }
                        else
                        {
                            param.countRange = new IntRange(def.titheMinCount, def.titheMaxCountBase + (def.titheMaxCountScaler * assignedWorkers));
                        }
                        randomTitheList = thingSetMaker.Generate(param);

                        if (randomTitheList is null || randomTitheList.Count == 0)
                        {
                            LogUtil.Message($"Resource {label} in settlement {settlement.Name} generated an empty random tithe list");
                        }
                        else
                        {
                            for (int i = 0; i < randomTitheList.Count; i++)
                            {
                                LogUtil.Message($"  randomTitheList[{i}]: {randomTitheList[i].LabelCap}");
                            }
                            titheItems.AddRange(randomTitheList);
                            // Goods are billed in full via CreateTax.fulfilledTitheValue, including the
                            // stock-funded portion — which was already withheld when it accrued, so
                            // un-escrow priorStock here to avoid charging it twice.
                            outSilver += (int)priorStock;
                            randomTitheStock = 0;
                        }
                    }
                    else
                    {
                        outSilver -= (int)(effectiveRandomBudget - priorStock); // withhold the new production escrowed this cycle
                        randomTitheStock = effectiveRandomBudget;
                        Find.LetterStack.ReceiveLetter("FCNoTitheLetterLabel".Translate(settlement.Name), "FCNoTitheLetterDesc2".Translate(settlement.Name, label, randomTitheStock), LetterDefOf.NeutralEvent);
                    }
                }
            }

            // Post-tax generation hook
            def.GetModExtension<ResourceTaxExtension>()?.OnPostTaxGeneration(this, settlement, titheItems, ref outSilver);

            extraSilver = outSilver;
            return titheItems;
        }
        /* - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - *
         *   End Tithe functions                                                                                                                                         *
         * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - * - */

        public int CompareForUI(ResourceFC compareDef)
        {
            if (compareDef == null)
            {
                return -2;
            }
            if (compareDef.def == null)
            {
                return -1;
            }
            if (this.def == null)
            {
                return 1;
            }
            return ResourceTypeDef.SortForUI(this.def, compareDef.def);
        }
        public static int SortForUI(ResourceFC a, ResourceFC b)
        {
            return a.CompareForUI(b);
        }
    }
}
