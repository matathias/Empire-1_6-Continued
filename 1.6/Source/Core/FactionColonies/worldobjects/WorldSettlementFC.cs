using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>Display state for a settlement's squad-derived military power.
    /// Drives text color and tooltip in the settlement window and main military tab.
    /// UnderAttack takes precedence over the squad-derived states — red is reserved for it.</summary>
    public enum SettlementPowerStatus
    {
        /// <summary>At least one stationed squad is available (white text).</summary>
        Squad,
        /// <summary>Squads are stationed but all are busy in ops/cooldown (yellow text).</summary>
        AllBusy,
        /// <summary>SquadCap > 0 but no squad stationed — defending at half power (yellow text).</summary>
        Ghost,
        /// <summary>SquadCap == 0 — settlement type has no military capacity (greyed out).</summary>
        NoMilitary,
        /// <summary>Settlement is the target of an active defensive op (red text). Overrides Squad/AllBusy/Ghost.</summary>
        UnderAttack
    }

    /// <summary>
    ///     WorldObject that in many ways re-implements Settlement.cs from Rimworld.Planet. May cause compatibility issues with
    ///     other mods that rely on finding Settlement objects on the world map. Recommend testing this extensively with mods
    ///     like SoS2, RimWar, or any mods that modify, collect, or deep save world objects before publishing changes
    /// </summary>
    public class WorldSettlementFC : Settlement
    {
        private string name;
        private string nameShort;
        private string nameOriginal;
        public string title = "FCHamlet".Translate();
        private string _description = "FCGenericError".Translate();
        private bool dirtyDescriptionCache = true;
        public string description
        {
            get
            {
                if (dirtyDescriptionCache) RecomputeDescription();
                return _description;
            }
        }
        private int foundingTick;
        public int FoundingTick => foundingTick;

        /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         * ~        Settlement Base Info         ~ *
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
        public int settlementLevel = 1;

        public bool CanUpgrade => !IsUpgrading
            && settlementLevel < FCSettings.settlementMaxLevel
            && settlementLevel < settlementDef.maxSettlementLevel;

        /// <summary>
        /// Edge length (cells) of this settlement's manual-defense battle map.
        /// Single source of truth: BattlefieldContext.GenerateMap and the friendly
        /// spawn-zone math both derive from this so they can't drift apart.
        /// </summary>
        public int DefenseMapSize => DefenseMapSizeFor(settlementLevel);

        public static int DefenseMapSizeFor(int level)
        {
            int size = FCSettings.defenseMapBaseSize + level * FCSettings.defenseMapPerLevelStep;
            if (size > FCSettings.defenseMapMaxSize) size = FCSettings.defenseMapMaxSize;
            if (size < FCSettings.defenseMapMinSize) size = FCSettings.defenseMapMinSize; // floor wins ties (KCSG safety)
            return size;
        }

        public int GetBuildingSlots()
        {
            int slots = settlementDef.GetSettlementTypeExtension().GetBuildingSlots(settlementLevel, settlementDef.maxBuildingCount);
            return Math.Min(slots, settlementDef.maxBuildingCount);
        }

        public int GetUpgradeCost(int baseCost)
        {
            int raw = settlementDef.GetSettlementTypeExtension().GetUpgradeCost(settlementLevel, baseCost);
            double cost = (raw + GetStatValue(FCStatDefOf.settlementUpgradeCostBase))
                          * GetStatValue(FCStatDefOf.settlementUpgradeCostMultiplier);
            return (int)Math.Max(0, cost);
        }

        public int GetUpgradeTime(double buildTimeMult)
        {
            int baseTime = settlementDef.GetSettlementTypeExtension().GetUpgradeTime(settlementLevel, buildTimeMult);
            return (int)(baseTime * FCSettings.settlementUpgradeTimeMultiplier);
        }

        /* Workers — lazy-cached, use DirtyStatsCache()/DirtyProfitCache() to invalidate */
        private double _workers;
        private double _workersMax;
        private double _workersUltraMax;
        private double _workerCost;
        private double _workerTotalUpkeep;
        private bool dirtyStatsCache = true;
        private bool dirtyProfitCache = true;

        public double workers { get { if (dirtyProfitCache) RecomputeProfit(); return _workers; } }
        public double workersMax { get { if (dirtyStatsCache) RecomputeStats(); return _workersMax; } }
        public double workersUltraMax { get { if (dirtyStatsCache) RecomputeStats(); return _workersUltraMax; } }
        public double workerCost { get { if (dirtyProfitCache) RecomputeProfit(); return _workerCost; } }
        public double workerTotalUpkeep { get { if (dirtyProfitCache) RecomputeProfit(); return _workerTotalUpkeep; } }
        /* Social Stats */
        private double _unrest;
        private double _loyalty = 100;
        private double _happiness = 100;
        private double _prosperity = 100;

        public double unrest
        {
            get => _unrest;
            set
            {
                double clamped = Math.Round(Math.Clamp(value, 0, 100), 1);
                if (_unrest == clamped) return;
                _unrest = clamped;
                FindFC.FactionComp?.DirtyAveragesCache();
            }
        }
        public double loyalty
        {
            get => _loyalty;
            set
            {
                double clamped = Math.Round(Math.Clamp(value, 0, 100), 1);
                if (_loyalty == clamped) return;
                _loyalty = clamped;
                FindFC.FactionComp?.DirtyAveragesCache();
            }
        }
        public double happiness
        {
            get => _happiness;
            set
            {
                double clamped = Math.Round(Math.Clamp(value, 0, 100), 1);
                if (_happiness == clamped) return;
                _happiness = clamped;
                FindFC.FactionComp?.DirtyAveragesCache();
            }
        }
        public double prosperity
        {
            get => _prosperity;
            set
            {
                if (_prosperity == value) return;
                _prosperity = value;
                InvalidateResourceCaches();
                DirtyProfitCache();
            }
        }

        /*-*-*- Squad deployment morale lockout -*-*-*/
        /* A settlement whose morale has collapsed cannot launch offensive or deploy operations from
         * itself. This is the anti-farming cutoff: squad deploys/raids charge a deferred bill, so
         * without a morale gate a player could farm raid loot indefinitely while only ever eating
         * the unpaid-bill happiness/unrest penalty. Defensive ops are intentionally NOT gated here
         * (a low-morale settlement must still be able to defend itself). */
        public const double SquadDeployHappinessFloor = 25;
        public const double SquadDeployLoyaltyFloor = 25;
        public const double SquadDeployUnrestCeiling = 75;

        /// <summary>True when this settlement's morale is too low to launch offensive/deploy ops.</summary>
        public bool SquadDeploymentLocked =>
            happiness < SquadDeployHappinessFloor
            || loyalty < SquadDeployLoyaltyFloor
            || unrest > SquadDeployUnrestCeiling;

        /// <summary>When <see cref="SquadDeploymentLocked"/>, outs the first failing condition as a
        /// translated, player-facing reason and returns true. Otherwise outs null and returns false.</summary>
        public bool TryGetSquadDeploymentBlock(out string reason)
        {
            if (happiness < SquadDeployHappinessFloor)
            {
                reason = "FCSquadDeployLockedHappiness".Translate(Name, (int)SquadDeployHappinessFloor);
                return true;
            }
            if (loyalty < SquadDeployLoyaltyFloor)
            {
                reason = "FCSquadDeployLockedLoyalty".Translate(Name, (int)SquadDeployLoyaltyFloor);
                return true;
            }
            if (unrest > SquadDeployUnrestCeiling)
            {
                reason = "FCSquadDeployLockedUnrest".Translate(Name, (int)SquadDeployUnrestCeiling);
                return true;
            }
            reason = null;
            return false;
        }

        /// <summary>
        /// Stat modifiers from buildings, settlement type, and events that apply to this settlement.
        /// Use AddStatModifiers/RemoveStatModifiers to modify.
        /// Each entry tracks the sourceId that added it for removal by source.
        /// </summary>
        private struct TaggedStatModifier
        {
            public string sourceId;
            public string sourceLabel;
            public FCStatModifier mod;
        }
        private List<TaggedStatModifier> statModifiers = new List<TaggedStatModifier>();

        /// <summary>
        /// Permanent stat modifiers that survive event expiry and are serialized with the settlement.
        /// Use AddPermanentModifiers/RemovePermanentModifiersBySource to modify.
        /// </summary>
        private List<PermanentStatModifier> permanentModifiers = new List<PermanentStatModifier>();

        /// <summary>
        /// Temporary, self-decaying penalties that drip a morale loss over several days.
        /// Each contributes its <see cref="DecayingStatPenalty.CurrentValue"/> to a loss stat
        /// (happinessLostBase/loyaltyLostBase/unrestGainedBase) and is ticked down once per day in
        /// FactionFC.UpdateSettlementStats. Use AddDecayingPenalty/TickDecayingPenalties to modify.
        /// </summary>
        private List<DecayingStatPenalty> decayingPenalties = new List<DecayingStatPenalty>();

        private Dictionary<FCStatDef, double> cachedStatValues = new Dictionary<FCStatDef, double>();
        private Dictionary<FCStatDef, string> cachedStatDescs = new Dictionary<FCStatDef, string>();

        /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         * ~          Ticking comp filter        ~ *
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
        /* RimWorld's WorldObject.Tick() and WorldObject.TickInterval(delta) both dispatch to EVERY comp
         * (CompTick / CompTickInterval) with no per-comp opt-out. Comps that exist only to provide
         * settlement data (e.g. IResourceProductionModifier) never override those, so the virtual call
         * is pure overhead. We override both to dispatch only to comps that actually override the
         * matching method. For TickInterval we additionally re-inline the rest of the base chain
         * (MapParent + Settlement work) so no base behavior is lost — see TickInterval below. */

        // Comps that actually override CompTick / CompTickInterval. Lazy; rebuilt when the comp set
        // changes. The "does this type override X?" test is cached centrally in TickOverrideUtil.
        [Unsaved] private List<WorldObjectComp> tickingComps;
        [Unsaved] private List<WorldObjectComp> tickIntervalComps;

        private void RebuildTickingComps()
        {
            tickingComps = new List<WorldObjectComp>();
            List<WorldObjectComp> all = AllComps;
            foreach (WorldObjectComp comp in all)
            {
                if (TickOverrideUtil.Overrides(comp.GetType(), "CompTick", typeof(WorldObjectComp)))
                    tickingComps.Add(comp);
            }
        }

        private void RebuildTickIntervalComps()
        {
            tickIntervalComps = new List<WorldObjectComp>();
            List<WorldObjectComp> all = AllComps;
            foreach (WorldObjectComp comp in all)
            {
                if (TickOverrideUtil.Overrides(comp.GetType(), "CompTickInterval", typeof(WorldObjectComp), typeof(int)))
                    tickIntervalComps.Add(comp);
            }
        }

        protected override void Tick()
        {
            if (tickingComps is null) RebuildTickingComps();
            for (int i = 0; i < tickingComps.Count; i++)
            {
                tickingComps[i].CompTick();
            }
        }

        /* Overriding TickInterval means we no longer call base, so we must replicate the parent chain
         * (Settlement -> MapParent -> WorldObject) here, swapping only WorldObject's unconditional comp
         * loop for the filtered one. Mirror of base bodies as of RimWorld 1.6 — revisit if they change.
         * 
         * The purpose of overriding TickInterval is so we can choose to only call CompTickInterval on
         * WorldObjectComps that have actually defined it, and thus need to tick. Basic testing shows that
         * skipping non-ticking WorldObjectComps can have an actual performance benefit with as few as 20-30
         * Empire Settlements (though the benefit is admittedly small).
         */
        protected override void TickInterval(int delta)
        {
            // WorldObject.TickInterval: dispatch only to comps that override CompTickInterval.
            if (tickIntervalComps is null) RebuildTickIntervalComps();
            foreach (WorldObjectComp comp in tickIntervalComps)
            {
                comp.CompTickInterval(delta);
            }

            // MapParent.TickInterval: remove the map when our ShouldRemoveMapNow override says so.
            CheckRemoveMapNow();

            // Settlement.TickInterval: trader restock/tick.
            if (trader != null)
            {
                trader.TraderTrackerTick();
            }
            // CheckDefeated(this) is INTENTIONALLY omitted here. Vanilla Settlement.TickInterval calls it,
            // but for a WorldSettlementFC it spawns DestroyedSettlement objects and crashes in
            // TimedDetectionRaids.CopyFrom. We used to suppress that with a Harmony prefix on
            // SettlementDefeatUtility.CheckDefeated; now that this override is the sole tick path for our
            // settlements and never calls it, the call and that patch have both been removed. Map teardown
            // is handled by CheckRemoveMapNow() above.
        }

        internal void DebugLogTickingComps()
        {
            if (tickingComps is null) RebuildTickingComps();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Ticking comps for settlement '{Name}' (Lv{settlementLevel}): "
                + $"{tickingComps.Count} ticking / {AllComps.Count} total");
            List<WorldObjectComp> all = AllComps;
            foreach (WorldObjectComp comp in all)
            {
                bool ticks = TickOverrideUtil.Overrides(comp.GetType(), "CompTick", typeof(WorldObjectComp));
                sb.AppendLine($"  {(ticks ? "[tick]" : "[skip]")} {comp.GetType().Name}");
            }
            LogUtil.MessageForce(sb.ToString());
        }

        /* Legacy save migration: prisoner data lived on the settlement under the
         * "prisonerList" XML key until WorldObjectComp_SettlementPrisoners took ownership.
         * Filled only during LoadingVars and drained into PrisonerComp.prisonerList during
         * PostLoadInit, then reset to null so subsequent saves omit the legacy key. */
        private List<FCPrisoner> _legacyPrisonerList = null;

        public float oneTimeSilverIncome;
        public List<Thing> tithe = new List<Thing>();
        public int titheEstimatedIncome;

        public string biome;
        public BiomeResourceDef biomeDef;

        private bool _isUpgrading;
        private int _startUpgradeTick = -1;
        private int _finishUpgradeTick = -1;

        public bool IsUpgrading => _isUpgrading;
        public int StartUpgradeTick => _startUpgradeTick;
        public int FinishUpgradeTick => _finishUpgradeTick;

        public void StartUpgrade(int finishTick)
        {
            _isUpgrading = true;
            _startUpgradeTick = Find.TickManager.TicksGame;
            _finishUpgradeTick = finishTick;
        }

        public void ClearUpgrade()
        {
            _isUpgrading = false;
            _startUpgradeTick = -1;
            _finishUpgradeTick = -1;
        }

        //ui only — lazy-cached via dirtyProfitCache
        private double _buildingsUpkeep;
        private double _totalUpkeep;
        private string _upkeepExp = "";
        private double _totalIncome;
        private string _incomeExp = "";
        private double _totalProfit;

        public double totalUpkeep { get { if (dirtyProfitCache) RecomputeProfit(); return _totalUpkeep; } }
        public string upkeepExp { get { if (dirtyProfitCache) RecomputeProfit(); return _upkeepExp; } }
        public double totalIncome { get { if (dirtyProfitCache) RecomputeProfit(); return _totalIncome; } }
        public string incomeExp { get { if (dirtyProfitCache) RecomputeProfit(); return _incomeExp; } }
        public double totalProfit { get { if (dirtyProfitCache) RecomputeProfit(); return _totalProfit; } }

        /* Accrued-this-cycle aggregates. Resource income is DERIVED by summing each resource's accrual
         * (never reconstructed at the settlement level). */
        public int TaxAccrualDays => taxAccrualDays;
        public double AccruedGrossIncome => resources.Sum(r => r.AccruedTaxableValue) + accruedNonResourceIncome;
        public double AccruedUpkeep => accruedTotalUpkeep;
        public int DaysRemaining => Math.Max(0, FCSettings.timeBetweenTaxes / GenDate.TicksPerDay - taxAccrualDays);

        /* Forward-looking full-cycle projection: accrued so far + live per-day rate * days remaining. */
        public double ProjectedIncome => AccruedGrossIncome + totalIncome * DaysRemaining;
        public double ProjectedUpkeep => AccruedUpkeep + totalUpkeep * DaysRemaining;

        /* Projected silver value of tithe goods that will be delivered this cycle: per tithing resource,
         * the selected tithe demand capped by the projected tithe budget (accrued + daily rate * days left).
         * Mirrors the fulfilledTitheValue deduction in CreateTax. */
        public double ProjectedTitheValue
        {
            get
            {
                double total = 0;
                foreach (ResourceFC r in resources)
                {
                    if (!r.canTithe || r.tithesPaused) continue;
                    double projBudget = r.AccruedTitheBudget + r.GetTitheIncome() * DaysRemaining;
                    total += Math.Min(r.titheTotalValue, projBudget);
                }
                return total;
            }
        }

        /* Net of upkeep AND projected tithe goods (diversions are already netted via post-diversion production). */
        public double ProjectedProfit => ProjectedIncome - ProjectedUpkeep - ProjectedTitheValue;

        // Jealously guard our resources. Only we can modify them!
        private List<ResourceFC> resources = new List<ResourceFC>();
        public List<ResourceFC> Resources => resources;
        private List<ThingDef> grandThingList = new List<ThingDef>();
        private bool dirtyGrandThingListFlag = true;

        // Comp caching for the most-frequently accessed comps
        private WorldObjectComp_SettlementMilitary cachedMilitaryComp = null;
        private bool checkedMilitaryComp = false;
        private WorldObjectComp_SettlementBuildings cachedBuildingsComp = null;
        private bool checkedBuildingsComp = false;
        private WorldObjectComp_SettlementPrisoners cachedPrisonerComp = null;
        private bool checkedPrisonerComp = false;

        // A private state variable
        private bool calculatingTax = false;
        public bool IsCalculatingTax => calculatingTax;

        /* Accrued over the current tax cycle. Resources accrue their own taxable/tithe budget;
         * non-resource income and all upkeep terms are tracked here.
         * taxAccrualDays closes the unassign-before-tax exploit for the upkeep path. */
        private double accruedNonResourceIncome = 0; // negative-building income + IProfitContributor income
        private double accruedTotalUpkeep = 0;       // worker upkeep + positive building upkeep + contributor upkeep
        private int taxAccrualDays = 0;
        public WorldObjectComp_SettlementMilitary MilitaryComp
        {
            get
            {
                if (!checkedMilitaryComp)
                {
                    cachedMilitaryComp = GetComponent<WorldObjectComp_SettlementMilitary>();
                    checkedMilitaryComp = true;
                    if (cachedMilitaryComp == null)
                    {
                        LogUtil.Warning($"Attempted to access settlement {Name}'s MilitaryComp, but it doesn't have one");
                    }
                }
                return cachedMilitaryComp;
            }
        }

        public WorldObjectComp_SettlementPrisoners PrisonerComp
        {
            get
            {
                if (!checkedPrisonerComp)
                {
                    cachedPrisonerComp = GetComponent<WorldObjectComp_SettlementPrisoners>();
                    checkedPrisonerComp = true;
                    if (cachedPrisonerComp == null)
                    {
                        LogUtil.Warning($"Attempted to access settlement {Name}'s PrisonerComp, but it doesn't have one");
                    }
                }
                return cachedPrisonerComp;
            }
        }

        /// <summary>Squads currently assigned to this settlement (their billet).
        /// Lazily filtered from the faction-wide mercenary pool — pool sizes are small enough
        /// that a per-call scan is cheap. Add a cache only if profiling shows hot paths.</summary>
        public List<MercenarySquadFC> StationedSquads
        {
            get
            {
                List<MercenarySquadFC> result = new List<MercenarySquadFC>();
                List<MercenarySquadFC> pool = FindFC.Military?.mercenarySquads;
                if (pool is null) return result;
                for (int i = 0; i < pool.Count; i++)
                {
                    MercenarySquadFC s = pool[i];
                    if (s is object && s.settlement == this) result.Add(s);
                }
                return result;
            }
        }

        /// <summary>The first stationed squad, or null. Use for read-only queries that just
        /// need any squad reference (loadout outfit, status icon, cost basis). For deploy /
        /// reinforcement / actions that consume a squad, prefer
        /// <see cref="FirstAvailableStationedSquad"/>.</summary>
        public MercenarySquadFC PrimaryStationedSquad
        {
            get
            {
                List<MercenarySquadFC> pool = FindFC.Military?.mercenarySquads;
                if (pool is null) return null;
                for (int i = 0; i < pool.Count; i++)
                {
                    MercenarySquadFC s = pool[i];
                    if (s is object && s.settlement == this) return s;
                }
                return null;
            }
        }

        /// <summary>The first stationed squad with <see cref="MercenarySquadFC.IsAvailable"/> true
        /// (assigned, not in any active op, past cooldown), or null. Use for deploy / foreign-
        /// defender reinforcement / extra-deployment paths that need a squad ready to act.</summary>
        public MercenarySquadFC FirstAvailableStationedSquad
        {
            get
            {
                List<MercenarySquadFC> pool = FindFC.Military?.mercenarySquads;
                if (pool is null) return null;
                for (int i = 0; i < pool.Count; i++)
                {
                    MercenarySquadFC s = pool[i];
                    if (s is object && s.settlement == this && s.IsAvailable) return s;
                }
                return null;
            }
        }

        /// <summary>Number of squads this settlement can simultaneously host. Base 1, modified by
        /// the <c>squadCapPerSettlement</c> stat (buildings, policies, settlement-type extensions).
        /// Floored at 0 — settlements can have no squad capacity at all (e.g. structurally
        /// non-military settlement types). Fire support remains independent of cap (gated only
        /// by the artillery building).</summary>
        public int SquadCap
        {
            get
            {
                FactionFC fc = FindFC.FactionComp;
                if (fc is null) return 1;
                int bonus = (int)Math.Floor(fc.GetStatValue(FCStatDefOf.squadCapPerSettlement, this));
                return Math.Max(0, 1 + bonus);
            }
        }

        /// <summary>Settlement-wide military power for UI display. Reflects the strongest
        /// available stationed squad's projected force (white). When stationed squads
        /// exist but none are available (all busy in raid/cooldown/defense), returns the
        /// half-power "ghost" values that battle resolution actually uses via
        /// <see cref="MilitaryForce.CreateMilitaryForceFromUnstaffedBillet"/> — same
        /// numbers as the empty-billet Ghost state, differentiated only by the status
        /// (AllBusy vs Ghost) for color/tooltip purposes. Greyed out when cap == 0
        /// (structurally non-military). Red UnderAttack overrides the AllBusy/Ghost
        /// status (but not the numbers) when the settlement is the target of an active
        /// defensive op.</summary>
        public (double level, double efficiency, SettlementPowerStatus status) GetDisplayedPower()
        {
            int cap = SquadCap;
            if (cap <= 0) return (0, 0, SettlementPowerStatus.NoMilitary);

            bool underAttack = MilitaryComp?.isUnderAttack ?? false;

            // The strongest available stationed squad provides the displayed power. When none is available
            // (none stationed, or all busy), the settlement defends through the unstaffed-billet ghost path.
            MercenarySquadFC bestAvailable = GetPowerSourceSquad();
            if (bestAvailable is null)
            {
                // Mirrors CreateMilitaryForceFromUnstaffedBillet's formula so display
                // matches battle. AllBusy (some squads stationed but busy) and Ghost
                // (no squads stationed) share the same numbers; only the status differs.
                double ghostLevel = Math.Max(1, settlementMilitaryLevel) * 0.5;
                double ghostEff = 1.0;
                FactionFC fc = FindFC.FactionComp;
                if (fc is object) ghostEff = fc.GetStatValue(FCStatDefOf.militaryCombatEfficiency, this);
                SettlementPowerStatus emptyStatus;
                if (underAttack) emptyStatus = SettlementPowerStatus.UnderAttack;
                else if (StationedSquads.Count == 0) emptyStatus = SettlementPowerStatus.Ghost;
                else emptyStatus = SettlementPowerStatus.AllBusy;
                return (ghostLevel, ghostEff, emptyStatus);
            }

            SquadPower power = SquadPowerRegistry.Resolve(bestAvailable);
            return (power.militaryLevel, power.militaryEfficiency,
                underAttack ? SettlementPowerStatus.UnderAttack : SettlementPowerStatus.Squad);
        }

        /// <summary>
        /// The strongest currently-available stationed squad — the one whose power <see cref="GetDisplayedPower"/>
        /// reports. Null when the settlement is non-military (SquadCap 0) or no stationed squad is available
        /// (none stationed, or all busy), in which case the settlement defends at the half-cap "ghost" level.
        /// </summary>
        public MercenarySquadFC GetPowerSourceSquad()
        {
            if (SquadCap <= 0) return null;
            List<MercenarySquadFC> stationed = StationedSquads;
            MercenarySquadFC bestAvailable = null;
            double bestAvailableLevel = -1;
            foreach (MercenarySquadFC s in stationed)
            {
                if (s is null || !s.IsAvailable) continue;
                double level = SquadPowerRegistry.Resolve(s).militaryLevel;
                if (level > bestAvailableLevel)
                {
                    bestAvailable = s;
                    bestAvailableLevel = level;
                }
            }
            return bestAvailable;
        }

        /// <summary>Maximum number of mercenaries a squad assigned here may have. Base 30 (matches
        /// <c>MilSquadFC.MaxSquadSize</c>), modified by the <c>maxSquadSize</c> stat. Floored at 1.</summary>
        public int MaxSquadSize
        {
            get
            {
                FactionFC fc = FindFC.FactionComp;
                if (fc is null) return MilSquadFC.MaxSquadSize;
                int bonus = (int)Math.Floor(fc.GetStatValue(FCStatDefOf.maxSquadSize, this));
                return Math.Max(1, MilSquadFC.MaxSquadSize + bonus);
            }
        }
        public WorldObjectComp_SettlementBuildings BuildingsComp
        {
            get
            {
                if (!checkedBuildingsComp)
                {
                    cachedBuildingsComp = GetComponent<WorldObjectComp_SettlementBuildings>();
                    checkedBuildingsComp = true;
                    if (cachedBuildingsComp == null)
                    {
                        LogUtil.Warning($"Attempted to access settlement {Name}'s BuildingsComp, but it doesn't have one");
                    }
                }
                return cachedBuildingsComp;
            }
        }
        public int settlementMilitaryLevel
        {
            get
            {
                if (dirtyStatsCache) RecomputeStats();

                return MilitaryComp?.settlementMilitaryLevel ?? 0;
            }
            set
            {
                if (!(MilitaryComp is null))
                {
                    MilitaryComp.settlementMilitaryLevel = value;
                }
                else
                {
                    LogUtil.Warning($"Settlement {Name} does not have a MilitaryComp, but tried to set its settlementMilitaryLevel to {value}");
                }
            }
        }

        public string ShortName
        {
            get
            {
                if (!nameShort.NullOrEmpty()) return nameShort;

                nameShort = TextUtil.ToShortName(name);

                return nameShort;
            }
            set => nameShort = value.NullOrEmpty() ? name : value;
        }

        public string OriginalName
        {
            get => nameOriginal;
            private set => nameOriginal = value;
        }

        private string cachedlocationText = string.Empty;
        public string locationText
        {
            get
            {
                if (cachedlocationText.NullOrEmpty())
                {
                    cachedlocationText = settlementDef.GetSettlementTypeExtension().GetLocationText(this);
                }
                return cachedlocationText;
            }
            set => cachedlocationText = value;
        }

        public static readonly FieldInfo traitCachedIcon = typeof(WorldObjectDef).GetField("expandingIconTextureInt",
            BindingFlags.NonPublic | BindingFlags.Instance);

        public static readonly FieldInfo traitCachedMaterial = typeof(WorldObjectDef).GetField("material",
            BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>
        ///     A flag meant to indicate whether or not this settlement is meant for actual destruction; used to override
        ///     WorldObject.Destroy() for compatibility purposes
        /// </summary>
        private bool destroyFlag;

        public new string Name
        {
            get => name ?? (name = "");
            set => name = value;
        }
        public override string Label => Name;

        public WorldSettlementDef settlementDef => def as WorldSettlementDef;

        /// <summary>
        ///     Indicate that this should be destroyed when WorldObject.Destroy() is called
        /// </summary>
        public void PrepareDestroy()
        {
            destroyFlag = true;
        }

        /// <summary>
        ///     Compatibility focused: this object should only be destroyed very deliberately, else another object is likely trying
        ///     to handle negative combat resolution against this settlement.
        /// </summary>
        public override void Destroy()
        {
            MilitaryComp?.EndBattle(false, 0);

            if (destroyFlag)
            {
                base.Destroy();
            }
        }

        public void InvalidateCache()
        {
            InvalidateStatCache();
            DirtyDescriptionCache();
            cachedlocationText = null;
            cachedBuildingsComp = null;
            checkedBuildingsComp = false;
            cachedMilitaryComp = null;
            checkedMilitaryComp = false;
        }
        public void InvalidateStatCache()
        {
            cachedStatDescs.Clear();
            cachedStatValues.Clear();
            InvalidateResourceCaches();
            DirtyStatsCache();
        }

        /// <summary>
        /// Clears cached stat descriptions without clearing stat value caches.
        /// Called when faction-level modifiers change (desc includes faction contributions).
        /// </summary>
        public void InvalidateDescCache()
        {
            cachedStatDescs.Clear();
        }

        /// <summary>
        /// Dirties resource production caches without clearing stat caches.
        /// Called by FactionFC.InvalidateFactionStatCache when faction-level modifiers change
        /// (settlement stat caches are unaffected, but final combined values change).
        /// </summary>
        public void InvalidateResourceCaches()
        {
            foreach (ResourceFC resource in resources)
            {
                resource.SetDirtyCacheProdBase();
                resource.SetDirtyCacheProdMult();
            }
        }

        /// <summary>
        /// Handles the setting up of a settlement's resources. Allows for adding or removing resources after settlement creation (such as if the resource itself has
        /// a techlevel or research restriction)
        /// </summary>
        /// <param name="techlevel"></param>
        public void PrepareResources(TechLevel techlevel)
        {
            foreach (ResourceAvailability rtd in settlementDef.resources)
            {
                bool resourceAllowed = biomeDef.GetBiomeResource(rtd.resourceDef) != null && rtd.resourceDef.ResourceTypeAllowedByTech(techlevel);
                ResourceFC res = resources.Find((ResourceFC rfc) => rfc.def == rtd.resourceDef);
                if (res is null && resourceAllowed)
                {
                    LogUtil.Message($"Adding resource {rtd.resourceDef.label} to settlement {Name}");
                    /* ResourceFC initialization takes care of biome bonuses, so no need to handle that up here */
                    resources.Add(new ResourceFC(rtd.resourceDef, this));
                }
                else if (!(res is null) && !resourceAllowed)
                {
                    LogUtil.Message($"Removing resource {rtd.resourceDef.label} from settlement {Name}");
                    resources.Remove(res);
                }
                else
                {
                    res?.SetDirtyCache();
                }
            }
            resources.Sort(ResourceFC.SortForUI);
            BuildingsComp?.InvalidateFilters();
        }

        public override void PostMake()
        {
            trader = new SettlementTraderTracker_Empire(this);

            if (!(def is WorldSettlementDef))
            {
                LogUtil.Error($"Created settlement {name} with an invalid def: {def}! Panic! Defaulting to base def!");
                def = WorldSettlementDefOf.WorldSettlementDef_Surface;
            }
            FactionFC faction = FindFC.FactionComp;
            Name = settlementDef.GetSettlementTypeExtension().GetSettlementName();

            UpdateTechIcon();
            def.expandingIconTexture = "FactionIcons/" + faction.factionIconPath;
            traitCachedIcon.SetValue(def, ContentFinder<Texture2D>.Get(def.expandingIconTexture));
            base.PostMake();

            LogUtil.Message($"Created world settlement {Name} with def {def}");
        }
        /// <summary>
        /// Handles necessary post-PostMake processing that requires the Tile field to be set.
        /// </summary>
        /// <param name="tile"></param>
        public void PostPostMake(PlanetTile tile)
        {
            FactionFC faction = FindFC.FactionComp;
            this.Tile = tile;

            settlementLevel = 1;

            _workers = 0;

            biome = Tile.Tile.PrimaryBiome.defName;
            bool useTileBiome = true;

            if (settlementDef.biomeResourceOverride != null)
            {
                LogUtil.Message($"Using biome {settlementDef.biomeResourceOverride.defName} as override for settlement {Name} of type {settlementDef}");
                useTileBiome = false;
                biomeDef = settlementDef.biomeResourceOverride;
                if (!FactionCache.BiomeResourceDefSet.Contains(biomeDef))
                {
                    LogUtil.Error($"Settlement {Name} of type {settlementDef.LabelCap} has invalid override biome. Falling back onto tile biome");
                    biomeDef = BiomeResourceDefOf.defaultBiome;
                    useTileBiome = true;
                }
            }
            if (useTileBiome)
            {
                //modded biomes handling
                biomeDef = DefDatabase<BiomeResourceDef>.GetNamed(biome, false) ?? BiomeResourceDefOf.defaultBiome;
                LogUtil.Message($"Founding settlement {Name} on biome {biomeDef.LabelCap}");
            }

            // Bake biome stat modifiers as permanent modifiers
            if (biomeDef.statModifiers != null && biomeDef.statModifiers.Count > 0)
            {
                AddPermanentModifiers(biomeDef.statModifiers, "biome_" + biomeDef.defName, biomeDef.LabelCap);
            }

            BuildingsComp?.InitBuildings();

            PrepareResources(faction.techLevel);

            /* If the settlement type has inherent stat modifiers, add them here. */
            // AddStatModifiers calls InvalidateStatCache -> DirtyStatsCache, so values recompute on first access
            AddStatModifiers(settlementDef.statModifiers, "settlementType", settlementDef.label);

            /* Bake tile mutator and landmark stat modifiers as permanent modifiers. */
            Tile worldTile = tile.Tile;
            if (worldTile != null)
            {
                IList<TileMutatorDef> mutators = worldTile.Mutators;
                if (mutators != null)
                {
                    foreach (TileMutatorDef mut in mutators)
                    {
                        TileMutatorResourceExtension mutExt = mut?.GetModExtension<TileMutatorResourceExtension>();
                        if (mutExt?.statModifiers == null || mutExt.statModifiers.Count == 0) continue;
                        AddPermanentModifiers(mutExt.statModifiers, "tile_mutator_" + mut.defName, mut.LabelCap);
                    }
                }

                Landmark landmark = worldTile.Landmark;
                if (landmark?.def != null)
                {
                    TileLandmarkResourceExtension lmExt = landmark.def.GetModExtension<TileLandmarkResourceExtension>();
                    if (lmExt?.statModifiers != null && lmExt.statModifiers.Count > 0)
                    {
                        AddPermanentModifiers(lmExt.statModifiers, "tile_landmark_" + landmark.def.defName, landmark.def.LabelCap);
                    }
                }
            }

            foundingTick = Find.TickManager.TicksGame;
        }
        public string GetFoundingDate(bool full = true)
        {
            if (full)
            {
                return GenDate.DateFullStringAt(foundingTick, FindFC.FactionComp?.StartingLongLat ?? default(Vector2));
            }
            return GenDate.DateShortStringAt(foundingTick, FindFC.FactionComp?.StartingLongLat ?? default(Vector2));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref foundingTick, "foundingTick", defaultValue: 0);
            Scribe_Values.Look(ref nameShort, "nameShort", ShortName);
            Scribe_Values.Look(ref nameOriginal, "nameOriginal", OriginalName);
            Scribe_Values.Look(ref title, "title");
            Scribe_Values.Look(ref _description, "description");
            Scribe_Values.Look(ref _workers, "workers");
            Scribe_Values.Look(ref _workersMax, "workersMax");
            Scribe_Values.Look(ref _workersUltraMax, "workersUltraMax");
            Scribe_Values.Look(ref settlementLevel, "settlementLevel");
            Scribe_Values.Look(ref _unrest, "unrest");
            Scribe_Values.Look(ref _loyalty, "loyalty");
            Scribe_Values.Look(ref _happiness, "happiness");
            Scribe_Values.Look(ref _prosperity, "prosperity");
            Scribe_Values.Look(ref _workerCost, "workerCost");
            Scribe_Values.Look(ref _workerTotalUpkeep, "workerTotalUpkeep");
            Scribe_Values.Look(ref accruedNonResourceIncome, "accruedNonResourceIncome", 0);
            Scribe_Values.Look(ref accruedTotalUpkeep, "accruedTotalUpkeep", 0);
            Scribe_Values.Look(ref taxAccrualDays, "taxAccrualDays", 0);

            Scribe_Collections.Look(ref resources, "resources", LookMode.Deep);

            //Taxes
            Scribe_Collections.Look(ref tithe, "tithe", LookMode.Deep);
            Scribe_Values.Look(ref titheEstimatedIncome, "titheEstimatedIncome");
            Scribe_Values.Look(ref oneTimeSilverIncome, "silverIncome");


            //Stat modifiers — transient list not serialized; rebuilt from buildings/settlement type on load
            //Permanent modifiers ARE serialized — they survive event expiry
            Scribe_Collections.Look(ref permanentModifiers, "permanentModifiers", LookMode.Deep);
            //Decaying penalties — not permanent, but they track their own expiry, so we need to serialize them here
            Scribe_Collections.Look(ref decayingPenalties, "decayingPenalties", LookMode.Deep);

            //Biome_info
            Scribe_Values.Look(ref biome, "biome");
            Scribe_Defs.Look(ref biomeDef, "biomedef");

            Scribe_Values.Look(ref _isUpgrading, "isupgrading", defaultValue: false);
            Scribe_Values.Look(ref _startUpgradeTick, "startupgradetick", -1);
            Scribe_Values.Look(ref _finishUpgradeTick, "finishupgradetick", -1);

            //Prisoners — legacy buffer; modern data lives on PrisonerComp.
            //Gated to LoadingVars so Saving never re-emits the legacy <prisonerList> key.
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                Scribe_Collections.Look(ref _legacyPrisonerList, "prisonerList", LookMode.Deep);

            // We never want permanentModifiers to be null. So just always check it here.
            if (permanentModifiers is null) permanentModifiers = new List<PermanentStatModifier>();
            if (decayingPenalties is null) decayingPenalties = new List<DecayingStatPenalty>();

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                PostLoadInit();
            }
        }

        /// <summary>
        /// Handles all post-load initialization.
        /// </summary>
        private void PostLoadInit()
        {
            if (Scribe.mode != LoadSaveMode.PostLoadInit)
            {
                LogUtil.Error($"Settlement {Name} attempted to call PostLoadInit during Scribe mode {Scribe.mode}. Bailing out.");
                return;
            }

            // Safety net: if trader is null or wrong type (e.g., loading old save), recreate it
            if (!(trader is SettlementTraderTracker_Empire))
                trader = new SettlementTraderTracker_Empire(this);

            // Rebuild stat modifiers from buildings and settlement type before calculating stats.
            // statModifiers is intentionally not serialized; it's rebuilt from sources on load.
            // base.ExposeData() already called comp PostExposeData, so buildings are loaded.
            ClearStatModifiers();
            BuildingsComp?.ReapplyBuildingStatModifiers();
            // AddStatModifiers calls InvalidateStatCache -> DirtyStatsCache, so values recompute on first access
            AddStatModifiers(settlementDef.statModifiers, "settlementType", settlementDef.label);

            // Re-apply active event stat modifiers that target this settlement.
            // Cross-references are resolved before DoAllPostLoadInits, so
            // FactionFC.Events and each event's settlementTraitLocations are populated.
            // (FactionFC migrates any legacy save events into eventManager during
            // ResolvingCrossRefs, before this PostLoadInit runs. See FactionFC.ExposeData.)
            FactionFC factionComp = FindFC.FactionComp;
            if (factionComp != null)
            {
                foreach (FCEvent evt in factionComp.Events)
                    EventStatModifierApplier.ApplyForSettlement(evt, this);
                // Same mechanism for situations: faction-scoped situations are pulled in by every
                // settlement, settlement-scoped only by their target.
                foreach (FCSituation sit in factionComp.situationManager.Situations)
                    SituationStatModifierApplier.ApplyForSettlement(sit, this);
            }
            else
            {
                LogUtil.Warning($"factionComp is null in PoastLoadInit phase for settlement {Name}");
            }

            // Migration: drain legacy <prisonerList> key into PrisonerComp.prisonerList.
            // Old saves stored the prisoner list on this settlement; it now lives on the comp.
            if (_legacyPrisonerList != null && _legacyPrisonerList.Count > 0)
            {
                WorldObjectComp_SettlementPrisoners pComp = PrisonerComp;
                if (pComp is object)
                {
                    if (pComp.prisonerList is null) pComp.prisonerList = new List<FCPrisoner>();
                    foreach (FCPrisoner p in _legacyPrisonerList)
                    {
                        if (p is null) continue;
                        // Defensive: Scribe_References should already have rebound this, but the
                        // legacy code path didn't always preserve the back-ref cleanly.
                        if (p.settlement is null) p.settlement = this;
                        pComp.prisonerList.Add(p);
                    }
                    LogUtil.MessageForce($"WorldSettlementFC {Name}: migrated {_legacyPrisonerList.Count} legacy prisoners into PrisonerComp.");
                }
                else
                {
                    LogUtil.Error($"WorldSettlementFC {Name}: cannot migrate {_legacyPrisonerList.Count} legacy prisoners — no PrisonerComp on this settlement.");
                }
            }
            _legacyPrisonerList = null;

            DirtyDescriptionCache();

            // Notify comps that settlement state is fully rebuilt (stat modifiers, buildings, type, events).
            // PostExposeData runs before this point, so comps that depend on production/stat values
            // should defer that work to this callback.
            LogUtil.Message($"Finished PostLoadInit for settlement {Name}. Calling PostSettlementLoadInit on {AllComps.Count} comps...");
            foreach (WorldObjectComp comp in AllComps)
            {
                if (comp is ISettlementPostLoadInit postLoad)
                {
                    try { postLoad.PostSettlementLoadInit(this); }
                    catch (Exception e) { LogUtil.Error($"ISettlementPostLoadInit {comp.GetType().Name} threw: {e}"); }
                }
            }
        }

        public void UpdateTechIcon()
        {
            var techLevel = FindFC.TechLevel;
            LogUtil.Message("Got tech level " + techLevel);
            if (techLevel == TechLevel.Animal || techLevel == TechLevel.Neolithic)
                def.texture = "World/WorldObjects/TribalSettlement";
            else
                def.texture = "World/WorldObjects/DefaultSettlement";

            traitCachedMaterial.SetValue(def, MaterialPool.MatFrom(def.texture,
                ShaderDatabase.WorldOverlayTransparentLit, WorldMaterials.WorldObjectRenderQueue));
        }

        /* Caravan gizmos for this settlement. We deliberately do NOT chain to base:
           Settlement adds a vanilla Trade command (duplicates our own gated Trade below)
           and an Attack command (wrong for the player's own colony). Replicate only the
           Gift command (shows only for hostile factions) and the comp dispatch (e.g. the
           SettlementMilitary "Defend" gizmo), then append our own gated Trade. Keep this
           block in sync with Settlement.GetCaravanGizmos. */
        public override IEnumerable<Gizmo> GetCaravanGizmos(Caravan caravan)
        {
            if ((bool)CaravanArrivalAction_OfferGifts.CanOfferGiftsTo(caravan, this))
            {
                yield return FactionGiftUtility.OfferGiftsCommand(caravan, this);
            }
            foreach (WorldObjectComp comp in AllComps)
            {
                foreach (Gizmo gizmo in comp.GetCaravanGizmos(caravan))
                {
                    yield return gizmo;
                }
            }
            if (MilitaryComp?.isUnderAttack != true && FindFC.FactionComp.IsActionAllowed(FCActionType.TradeWithSettlement))
            {
                var kindDef = TraderKind;
                var action = (Command_Action)CaravanVisitUtility.TradeCommand(caravan, Faction, kindDef);

                var bestNegotiator = BestCaravanPawnUtility.FindBestNegotiator(caravan, Faction, kindDef);
                action.action = () =>
                {
                    if (!CanTradeNow)
                        return;
                    Find.WindowStack.Add(new Dialog_Trade(bestNegotiator, this));
                    PawnRelationUtility.Notify_PawnsSeenByPlayer_Letter_Send(Goods.OfType<Pawn>(),
                        "LetterRelatedPawnsTradingWithSettlement"
                            .Translate((NamedArgument)Faction.OfPlayer.def.pawnsPlural), LetterDefOf.NeutralEvent);
                };

                yield return action;
            }
        }

        /* Caravan float-menu options for this settlement. We deliberately do NOT chain to base:
           Settlement adds a vanilla Trade option (duplicates our own gated Trade below) and an
           Attack option (wrong for the player's own colony). Replicate only the comp dispatch
           (e.g. the SettlementMilitary "Defend" option), the Visit option, and the Gift option
           (shows only for hostile factions), then append our own gated Trade. Keep this block in
           sync with Settlement.GetFloatMenuOptions. */
        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan)
        {
            foreach (WorldObjectComp comp in AllComps)
            {
                foreach (FloatMenuOption option in comp.GetFloatMenuOptions(caravan))
                {
                    yield return option;
                }
            }
            if (CaravanVisitUtility.SettlementVisitedNow(caravan) != this)
            {
                foreach (FloatMenuOption option in CaravanArrivalAction_VisitSettlement.GetFloatMenuOptions(caravan, this))
                {
                    yield return option;
                }
            }
            foreach (FloatMenuOption option in CaravanArrivalAction_OfferGifts.GetFloatMenuOptions(caravan, this))
            {
                yield return option;
            }
            if ((MilitaryComp is null || !MilitaryComp.isUnderAttack) && FindFC.FactionComp.IsActionAllowed(FCActionType.TradeWithSettlement))
                foreach (var option in WorldSettlementTradeAction.GetFloatMenuOptions(caravan, this))
                    yield return option;
        }

        /* Transport pods targeting this settlement. We deliberately do NOT chain to base:
           Settlement adds Visit/Gift/Attack options (all wrong for the player's own colony).
           Instead, we replicate only MapParent's "land in existing map" branch — available
           only during a battle when Map != null — then append our "add pawns to settlement"
           options. Keep this block in sync with MapParent.GetTransportersFloatMenuOptions. */
        public override IEnumerable<FloatMenuOption> GetTransportersFloatMenuOptions(
            IEnumerable<IThingHolder> pods, Action<PlanetTile, TransportersArrivalAction> launchAction)
        {
            if (TransportersArrivalAction_LandInSpecificCell.CanLandInSpecificCell(pods, this))
            {
                yield return new FloatMenuOption("LandInExistingMap".Translate(Label), delegate
                {
                    Map map = Map;
                    Current.Game.CurrentMap = map;
                    CameraJumper.TryHideWorld();
                    Find.Targeter.BeginTargeting(TargetingParameters.ForDropPodsDestination(), delegate (LocalTargetInfo x)
                    {
                        launchAction(Tile, new TransportersArrivalAction_LandInSpecificCell(this, x.Cell, Rot4.North, landInShuttle: false));
                    }, null, null, CompLaunchable.TargeterMouseAttachment);
                });
            }

            foreach (FloatMenuOption option in
                TransportersArrivalAction_AddToSettlementFC.GetFloatMenuOptions(pods, launchAction, this))
            {
                yield return option;
            }
        }

        public override bool ShouldRemoveMapNow(out bool removeWorldObject)
        {
            removeWorldObject = false;
            var map = Map;
            if (map is null) return false;
            if (MilitaryComp?.isUnderAttack == true) return false;
            if (MilitaryComp is object && (MilitaryComp.defenders.Any() || MilitaryComp.attackers.Any())) return false;
            // Vanilla checks: wait for player pawns to leave and incoming transporters to arrive
            if (map.mapPawns.AnyPawnBlockingMapRemoval) return false;
            if (TransporterUtility.IncomingTransporterPreventingMapRemoval(map)) return false;
            return true;
        }

        public override void Notify_MyMapAboutToBeRemoved()
        {
            // Clean up Empire faction pawns to prevent ghost colonists in the world pawn pool.
            // By this point all player pawns have left (ShouldRemoveMapNow confirmed no blockers).
            // Shared with the manual-offense teardown so both preserve the squad identically.
            SquadMapTeardownUtil.PreserveEmpirePawns(Map);

            // Squad mercs are now despawned (off-map), so auto-replace any fallen members from a
            // manual battle right at map tear-down — including maps that lingered past the battle's
            // CompleteBattle because the player still had mobile pawns present. Opt-in; no-op when off.
            FindFC.Military?.TryAutoReplaceAllSquads();

            base.Notify_MyMapAboutToBeRemoved();
        }

        public void UpgradeSettlement(int times = 1, bool setFlags = false)
        {
            int oldLevel = settlementLevel;
            settlementLevel += times;
            if (settlementLevel > FCSettings.settlementMaxLevel ||
                settlementLevel > settlementDef.maxSettlementLevel)
            {
                settlementLevel = FCSettings.settlementMaxLevel;
            }
            if (settlementLevel < 0) settlementLevel = 0;
            DirtyStatsCache();
            DirtyDescriptionCache();
            settlementDef.GetSettlementTypeExtension()?.OnUpgrade(this, oldLevel, settlementLevel);
            LifecycleRegistry.InvokeOnSettlementUpgraded(this, oldLevel, settlementLevel);

            if (setFlags)
            {
                ClearUpgrade();
            }
        }

        public void DelevelSettlement(int times = -1)
        {
            UpgradeSettlement(times);
        }

        /// <summary>
        /// Transitions this settlement to a new WorldSettlementDef, reconciling all dependent state
        /// (comps, stats, resources, buildings, caches). Returns false if the transition is blocked
        /// (e.g., incompatible planet layer or biome).
        /// </summary>
        public bool TransitionType(WorldSettlementDef newDef)
        {
            if (newDef is null || newDef == settlementDef) return false;

            WorldSettlementDef oldDef = settlementDef;

            // --- Validation: tile must be valid for new type ---
            SettlementTypeExtension newExt = newDef.GetSettlementTypeExtension();
            if (newExt is null)
            {
                LogUtil.Error($"Cannot transition {Name}: {newDef.defName} has no SettlementTypeExtension");
                return false;
            }
            StringBuilder reason = new StringBuilder();
            if (!newExt.TileIsValidForTypeTransition(Tile, reason))
            {
                LogUtil.Warning($"Cannot transition {Name} from {oldDef.defName} to {newDef.defName}: {reason}");
                return false;
            }

            // --- Pre-transition hooks ---
            oldDef.GetSettlementTypeExtension()?.PreTypeTransition(this, newDef);

            // --- Stat cleanup ---
            RemoveStatModifiersBySource("settlementType");

            // --- Deconstruct invalid buildings (before def swap, using new def for validation) ---
            if (BuildingsComp != null)
            {
                for (int i = BuildingsComp.Buildings.Count - 1; i >= 0; i--)
                {
                    BuildingFCDef bDef = BuildingsComp.Buildings[i].def;
                    if (bDef != BuildingFCDefOf.Empty && !bDef.CanBeBuiltForSettlementType(newDef))
                    {
                        Messages.Message("FCBuildingRemovedByTypeTransition".Translate(bDef.LabelCap, Name), MessageTypeDefOf.NeutralEvent);
                        BuildingsComp.DeconstructBuilding(i);
                    }
                }
            }

            // --- Core swap ---
            def = newDef;

            // --- Reconcile comps ---
            ReconcileComps(oldDef, newDef);

            // --- Clamp level ---
            if (settlementLevel > settlementDef.maxSettlementLevel)
                settlementLevel = settlementDef.maxSettlementLevel;

            // --- Reconcile resources: remove orphans, then add/dirty via PrepareResources ---
            HashSet<ResourceTypeDef> newResourceDefs = new HashSet<ResourceTypeDef>();
            foreach (ResourceAvailability ra in settlementDef.resources)
                newResourceDefs.Add(ra.resourceDef);
            for (int i = resources.Count - 1; i >= 0; i--)
            {
                if (!newResourceDefs.Contains(resources[i].def))
                    resources.RemoveAt(i);
            }
            PrepareResources(FindFC.TechLevel);

            // --- Apply new stat modifiers ---
            AddStatModifiers(settlementDef.statModifiers, "settlementType", settlementDef.label);

            // --- Reconcile building slots ---
            BuildingsComp?.ReinitBuildings();

            // --- Update icon/texture ---
            UpdateTechIcon();
            def.expandingIconTexture = "FactionIcons/" + FindFC.FactionComp.factionIconPath;
            traitCachedIcon.SetValue(def, ContentFinder<Texture2D>.Get(def.expandingIconTexture));

            // --- Invalidate all caches ---
            InvalidateCache();
            FindFC.FactionComp?.DirtyFactionProfitCache();
            FindFC.FactionComp?.DirtyAveragesCache();

            // --- Post-transition hooks ---
            newDef.GetSettlementTypeExtension()?.PostTypeTransition(this, oldDef);
            LifecycleRegistry.InvokeOnSettlementTypeChanged(this, oldDef, newDef);

            LogUtil.Message($"Settlement {Name} transitioned from {oldDef.defName} to {newDef.defName}");
            return true;
        }

        /// <summary>
        /// Reconciles the WorldObjectComp list after a def swap.
        /// Removes comps whose compClass only existed on the old def (calling PostDestroy).
        /// Adds comps whose compClass only exists on the new def.
        /// Comps present on both defs are left untouched, preserving their state.
        /// </summary>
        private void ReconcileComps(WorldSettlementDef oldDef, WorldSettlementDef newDef)
        {
            HashSet<Type> oldCompClasses = new HashSet<Type>();
            foreach (WorldObjectCompProperties props in oldDef.comps)
                oldCompClasses.Add(props.compClass);

            HashSet<Type> newCompClasses = new HashSet<Type>();
            foreach (WorldObjectCompProperties props in newDef.comps)
                newCompClasses.Add(props.compClass);

            // Remove comps that are on the old def but NOT on the new def
            List<WorldObjectComp> compsList = AllComps;
            for (int i = compsList.Count - 1; i >= 0; i--)
            {
                Type compType = compsList[i].GetType();
                if (oldCompClasses.Contains(compType) && !newCompClasses.Contains(compType))
                {
                    compsList[i].PostDestroy();
                    compsList.RemoveAt(i);
                }
            }

            // Add comps that are on the new def but NOT on the old def
            HashSet<Type> currentCompClasses = new HashSet<Type>();
            foreach (WorldObjectComp comp in compsList)
                currentCompClasses.Add(comp.GetType());

            foreach (WorldObjectCompProperties props in newDef.comps)
            {
                if (!currentCompClasses.Contains(props.compClass))
                {
                    try
                    {
                        WorldObjectComp comp = (WorldObjectComp)Activator.CreateInstance(props.compClass);
                        comp.parent = this;
                        compsList.Add(comp);
                        comp.Initialize(props);
                    }
                    catch (Exception e)
                    {
                        LogUtil.Error($"Failed to create comp {props.compClass} during type transition: {e}");
                    }
                }
            }

            // Comp set changed; rebuild the ticking-comp filter on the next tick.
            tickingComps = null;
        }

        public double GainUnrestWithReason(Message message, double amount)
        {
            Messages.Message(message);
            return GainUnrest(amount);
        }
        public double GainUnrest(double amount)
        {
            double gain = amount * GetStatValue(FCStatDefOf.unrestGainedMultiplier);
            unrest += gain;
            return gain;
        }

        public double GainHappiness(double amount)
        {
            double gain = amount * GetStatValue(FCStatDefOf.happinessGainedMultiplier);
            happiness += gain;
            return gain;
        }

        public double GainLoyalty(double amount)
        {
            // When or if we add a loyaltyGainedMultiplier, we'd refer to it here
            double gain = amount;
            loyalty += gain;
            return gain;
        }

        public double GainProsperity(double amount)
        {
            // When or if we add a prosperityGainedMultiplier, we'd refer to it here
            double gain = amount;
            prosperity += gain;
            return gain;
        }

        /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         * ~     Lazy Cache Invalidation        ~ *
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/

        /* Per-settlement cascade (each Dirty* method calls the next link).
         *
         *   InvalidateStatCache (full) --> clears stat dicts
         *                              --> InvalidateResourceCaches
         *                              --> DirtyStatsCache
         *
         *   InvalidateResourceCaches    --> per-resource production base/mult dirty
         *
         *   DirtyStatsCache             --> DirtyProfitCache
         *                              --> FactionFC.DirtyFactionProfitCache
         *
         *   DirtyProfitCache            --> FactionFC.DirtyFactionProfitCache
         *
         *   InvalidateDescCache         --> stat-desc dict only
         *   DirtyDescriptionCache       --> settlement description text only
         *
         * Callers should pick the highest-level entry point that matches the change:
         *   stat-defining state changed     -> InvalidateStatCache
         *   resource modifier changed       -> InvalidateResourceCaches
         *   workforce composition changed   -> NotifyWorkforceChanged (= DirtyStatsCache)
         *   profit-affecting state changed  -> DirtyProfitCache
         *   description text changed        -> DirtyDescriptionCache
         */

        /// <summary>
        /// Marks the stats cache (workersMax, workersUltraMax, militaryLevel) as dirty.
        /// Also cascades to dirty the profit cache since profit depends on stats.
        /// </summary>
        public void DirtyStatsCache()
        {
            dirtyStatsCache = true;
            DirtyProfitCache();
        }

        /// <summary>
        /// Marks the profit cache (income, upkeep, profit, workerCost) as dirty.
        /// </summary>
        public void DirtyProfitCache()
        {
            dirtyProfitCache = true;
            FindFC.FactionComp?.DirtyFactionProfitCache();
        }

        /// <summary>
        /// Marks the description cache as dirty.
        /// </summary>
        public void DirtyDescriptionCache()
        {
            dirtyDescriptionCache = true;
        }

        /* Name mutators: prefer these over assigning Name / ShortName directly so the
         * description cache stays in sync. */
        public void SetName(string newName)
        {
            if (newName.NullOrEmpty() || newName == Name) return;
            Name = newName;
            DirtyDescriptionCache();
        }

        public void SetShortName(string newShortName)
        {
            if (newShortName == ShortName) return;
            ShortName = newShortName;
            DirtyDescriptionCache();
        }

        /// <summary>
        /// Workforce composition changed (prisoner workload, worker reallocation). Equivalent
        /// to <see cref="DirtyStatsCache"/> — kept as a named entry point so call sites
        /// document intent rather than the cache being invalidated.
        /// </summary>
        public void NotifyWorkforceChanged() => DirtyStatsCache();

        /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*
         * ~     Lazy Cache Recomputation       ~ *
         *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/

        private void RecomputeStats()
        {
            FactionFC factionFc = FindFC.FactionComp;

            int extraWorkersSoftcap = (int)factionFc.GetStatValue(FCStatDefOf.extraWorkersSoftcap, this);
            int overMaxAdjustment = (int)factionFc.GetStatValue(FCStatDefOf.overMaxWorkersAdjustment, this);

            //Military Settlement Level
            settlementMilitaryLevel = settlementLevel - 1 + Convert.ToInt32(GetStatValue(FCStatDefOf.militaryBaseLevel));

            //Worker Stats
            //Floor at 1: harsh event modifiers (negative workerBaseMax) can otherwise drive the max below zero
            //on low-level settlements. There must always be at least 1 worker slot available for assignment.
            _workersMax = Math.Max(1, settlementDef.workersMaxBase + (settlementLevel * (settlementDef.workersMaxMult + extraWorkersSoftcap)) +
                         GetStatValue(FCStatDefOf.workerBaseMax) + (PrisonerComp?.ReturnMaxWorkersFromPrisoners() ?? 0));
            //Floor at _workersMax: negative overMaxAdjustment/workerBaseOverMax must never push the ultra cap below the normal max
            //(keeps the overmax capacity, workersUltraMax - workersMax, non-negative).
            _workersUltraMax = Math.Max(_workersMax, _workersMax + settlementDef.workersUltraMaxBase + overMaxAdjustment + (settlementLevel * settlementDef.workersUltraMaxMult) +
                              GetStatValue(FCStatDefOf.workerBaseOverMax) + (PrisonerComp?.ReturnOverMaxWorkersFromPrisoners() ?? 0));

            dirtyStatsCache = false;
            dirtyProfitCache = true;
        }

        private void RecomputeProfit()
        {
            if (dirtyStatsCache) RecomputeStats();

            _upkeepExp = "";
            _incomeExp = "";
            _workers = GetTotalWorkers_Internal();
            double upkeep = 0;
            double income = 0;

            _workerTotalUpkeep = SettlementFormulas.CalculateWorkerUpkeep(_workers, _workersMax, GetBaseWorkerCost(),
                GetStatValue(FCStatDefOf.workerOverworkPenaltyMultiplier));
            if (_workerTotalUpkeep > 0)
            {
                _upkeepExp += $"+{Math.Round(_workerTotalUpkeep, 2)} - {"FCWorkers".Translate()}\n";
            }

            upkeep += _workerTotalUpkeep;

            double buildingsUpkeep = BuildingsComp?.TotalUpkeep() ?? 0;
            _buildingsUpkeep = buildingsUpkeep;
            if (buildingsUpkeep > 0)
            {
                upkeep += buildingsUpkeep;
                _upkeepExp += $"+{Math.Round(buildingsUpkeep, 2)} - {"FCBuildings".Translate()}\n";
            }
            else if (buildingsUpkeep < 0)
            {
                income += Math.Abs(buildingsUpkeep);
                _incomeExp += $"+{Math.Round(Math.Abs(buildingsUpkeep), 2)} - {"FCBuildings".Translate()}\n";
            }

            foreach (ResourceFC resource in resources)
            {
                if (resource.def.isPoolResource) continue; // pools feed CreatePool, not silver income
                double resIncome = resource.taxableProductionMarketValue; // gross/day, post-stockpile, pre-tithe
                if (resIncome > 0)
                {
                    income += resIncome;
                    _incomeExp += $"+{Math.Round(resIncome, 2)} - {resource.label} {"FCIncome".Translate()}\n";
                }
            }

            foreach (WorldObjectComp comp in AllComps)
            {
                if (comp is IProfitContributor contributor)
                {
                    double upkeepContrib = contributor.GetDailyUpkeepContribution();
                    if (upkeepContrib > 0)
                    {
                        upkeep += upkeepContrib;
                        string upkeepDesc = contributor.GetDailyUpkeepContributionDesc();
                        if (!upkeepDesc.NullOrEmpty())
                        {
                            _upkeepExp += upkeepDesc + "\n";
                        }
                    }

                    double incomeContrib = contributor.GetDailyIncomeContribution();
                    if (incomeContrib > 0)
                    {
                        income += incomeContrib;
                        string incomeDesc = contributor.GetDailyIncomeContributionDesc();
                        if (!incomeDesc.NullOrEmpty())
                        {
                            _incomeExp += incomeDesc + "\n";
                        }
                    }
                }
            }

            _upkeepExp = _upkeepExp.Trim();
            _incomeExp = _incomeExp.Trim();

            _totalUpkeep = upkeep; // live per-day rate; tax pays the accrued sum (see CreateTax)
            _totalIncome = income;
            _workerCost = _workers == 0 ? GetBaseWorkerCost() : (_workerTotalUpkeep / _workers);
            _totalProfit = _totalIncome - _totalUpkeep;

            dirtyProfitCache = false;
        }

        private void RecomputeDescription()
        {
            _description = GetDescriptionBiome() + "\n\n" + GetSettlementLevelDesc();
            dirtyDescriptionCache = false;
        }

        public double GetHappinessGain()
        {
            double happinessGainMultiplier = GetStatValue(FCStatDefOf.happinessGainedMultiplier);
            return happinessGainMultiplier * (FCSettings.happinessBaseGain + GetStatValue(FCStatDefOf.happinessGainedBase));
        }
        public double GetHappinessLoss()
        {
            double happinessLostMultiplier = GetStatValue(FCStatDefOf.happinessLostMultiplier);
            return happinessLostMultiplier * (FCSettings.happinessBaseLost + GetStatValue(FCStatDefOf.happinessLostBase));
        }
        public double GetTotalHappinessGain()
        {
            return GetHappinessGain() - GetHappinessLoss();
        }
        public void UpdateHappiness()
        {
            happiness = SettlementFormulas.ClampStat(happiness, GetTotalHappinessGain());
        }
        public string GetHappinessDesc()
        {
            double happinessGain = Math.Round(GetTotalHappinessGain(), 2);
            string desc = "";

            if (happinessGain >= 0)
                desc = "FCSettlementStatGain".Translate(Math.Abs(happinessGain), "FCHappiness".Translate());
            else
                desc = "FCSettlementStatLoss".Translate(Math.Abs(happinessGain), "FCHappiness".Translate());

            desc += "\n\n";
            string gain = "";
            if (FCSettings.happinessBaseGain != 0)
                gain += TextUtil.AdditiveBonusLine(FCSettings.happinessBaseGain, "FCBaseGain".Translate()) + "\n";

            gain += GetStatDesc(FCStatDefOf.happinessGainedBase);
            gain += GetStatDesc(FCStatDefOf.happinessGainedMultiplier);
            if (!gain.NullOrEmpty())
                desc += gain + "\n";

            if (FCSettings.happinessBaseLost != 0)
                desc += TextUtil.AdditiveBonusLine(FCSettings.happinessBaseLost, "FCBaseLoss".Translate(), hardinvert: true) + "\n";

            desc += GetStatDesc(FCStatDefOf.happinessLostBase, hardinvert: true);
            desc += GetStatDesc(FCStatDefOf.happinessLostMultiplier);

            return desc.Trim();
        }

        public double GetLoyaltyGain()
        {
            double loyaltyGainMultiplier = GetStatValue(FCStatDefOf.loyaltyGainedMultiplier);
            return loyaltyGainMultiplier * (FCSettings.loyaltyBaseGain + GetStatValue(FCStatDefOf.loyaltyGainedBase));
        }
        public double GetLoyaltyLoss()
        {
            double loyaltyLostMultiplier = GetStatValue(FCStatDefOf.loyaltyLostMultiplier);
            return loyaltyLostMultiplier * (FCSettings.loyaltyBaseLost + GetStatValue(FCStatDefOf.loyaltyLostBase));
        }
        public double GetTotalLoyaltyGain()
        {
            return GetLoyaltyGain() - GetLoyaltyLoss();
        }
        public void UpdateLoyalty()
        {
            loyalty = SettlementFormulas.ClampStat(loyalty, GetTotalLoyaltyGain());
        }
        public string GetLoyaltyDesc()
        {
            double loyaltyGain = Math.Round(GetTotalLoyaltyGain(), 2);
            string desc = "";
            if (loyaltyGain >= 0)
                desc = "FCSettlementStatGain".Translate(Math.Abs(loyaltyGain), "FCLoyality".Translate());
            else
                desc = "FCSettlementStatLoss".Translate(Math.Abs(loyaltyGain), "FCLoyality".Translate());

            desc += "\n\n";
            string gain = "";
            if (FCSettings.loyaltyBaseGain != 0)
                gain += TextUtil.AdditiveBonusLine(FCSettings.loyaltyBaseGain, "FCBaseGain".Translate()) + "\n";

            gain += GetStatDesc(FCStatDefOf.loyaltyGainedBase);
            gain += GetStatDesc(FCStatDefOf.loyaltyGainedMultiplier);
            if (!gain.NullOrEmpty())
                desc += gain + "\n";

            if (FCSettings.loyaltyBaseLost != 0)
                desc += "\n" + TextUtil.AdditiveBonusLine(FCSettings.loyaltyBaseLost, "FCBaseLoss".Translate(), hardinvert: true) + "\n";

            desc += GetStatDesc(FCStatDefOf.loyaltyLostBase, hardinvert: true);
            desc += GetStatDesc(FCStatDefOf.loyaltyLostMultiplier);

            return desc.Trim();
        }

        public double GetProsperityTarget()
        {
            return (happiness + loyalty + (100.0 - unrest)) / 3.0;
        }

        public double GetProsperityDrift()
        {
            return SettlementFormulas.CalculateProsperityDrift(
                prosperity, GetProsperityTarget(), FCSettings.prosperityDriftRate, FCSettings.prosperityDriftStep);
        }
        public double GetProsperityGain()
        {
            return GetProsperityDrift()
                + GetStatValue(FCStatDefOf.prosperityGainedBase)
                - GetStatValue(FCStatDefOf.prosperityLostBase);
        }
        public void UpdateProsperity()
        {
            prosperity = SettlementFormulas.ClampStat(prosperity, GetProsperityGain());
        }
        public string GetProsperityDesc()
        {
            double prosperityGain = Math.Round(GetProsperityGain(), 2);
            string desc = "";
            if (prosperityGain >= 0)
                desc = "FCSettlementStatGain".Translate(Math.Abs(Math.Round(prosperityGain, 1)), "FCProsperity".Translate());
            else
                desc = "FCSettlementStatLoss".Translate(Math.Abs(Math.Round(prosperityGain, 1)), "FCProsperity".Translate());

            desc += "\n\n";

            double target = Math.Round(GetProsperityTarget(), 1);
            desc += "FCProsperityTarget".Translate(target) + "\n";
            desc += "FCProsperityTargetBreakdown".Translate(
                Math.Round(happiness, 1),
                Math.Round(loyalty, 1),
                Math.Round(100.0 - unrest, 1)) + "\n\n";

            double drift = GetProsperityDrift();
            desc += TextUtil.AdditiveBonusLine(Math.Round(drift, 1), "FCProsperityDrift".Translate()) + "\n";

            desc += GetStatDesc(FCStatDefOf.prosperityGainedBase);
            desc += GetStatDesc(FCStatDefOf.prosperityLostBase, hardinvert: true);

            return desc.Trim();
        }
        public double GetUnrestGain()
        {
            double unrestGainMultiplier = GetStatValue(FCStatDefOf.unrestGainedMultiplier);
            return unrestGainMultiplier * (FCSettings.unrestBaseGain + GetStatValue(FCStatDefOf.unrestGainedBase));
        }
        public double GetUnrestLoss()
        {
            double unrestLostMultiplier = GetStatValue(FCStatDefOf.unrestLostMultiplier);
            return unrestLostMultiplier * (FCSettings.unrestBaseLost + GetStatValue(FCStatDefOf.unrestLostBase));
        }
        public double GetTotalUnrestGain()
        {
            return GetUnrestGain() - GetUnrestLoss();
        }
        public void UpdateUnrest()
        {
            unrest = SettlementFormulas.ClampStat(unrest, GetTotalUnrestGain());
        }
        public string GetUnrestDesc()
        {
            double unrestGain = Math.Round(GetTotalUnrestGain(), 2);
            string desc = "";
            if (unrestGain >= 0)
                desc = "FCSettlementStatGain".Translate(Math.Abs(unrestGain), "FCUnrest".Translate());
            else
                desc = "FCSettlementStatLoss".Translate(Math.Abs(unrestGain), "FCUnrest".Translate());

            desc += "\n\n";
            string gain = "";
            if (FCSettings.unrestBaseGain != 0)
                gain += TextUtil.AdditiveBonusLine(FCSettings.unrestBaseGain, "FCBaseGain".Translate(), invert: true) + "\n";

            gain += GetStatDesc(FCStatDefOf.unrestGainedBase);
            gain += GetStatDesc(FCStatDefOf.unrestGainedMultiplier);
            if (!gain.NullOrEmpty())
                desc += gain + "\n";

            if (FCSettings.unrestBaseLost != 0)
                desc += TextUtil.AdditiveBonusLine(FCSettings.unrestBaseLost, "FCBaseLoss".Translate(), invert: true, hardinvert: true) + "\n";

            desc += GetStatDesc(FCStatDefOf.unrestLostBase, hardinvert: true);
            desc += GetStatDesc(FCStatDefOf.unrestLostMultiplier);

            return desc.Trim();
        }
        public double GetSettlementTaxBonus()
        {
            FactionFC faction = FindFC.FactionComp;
            double bonus = faction.GetStatValue(FCStatDefOf.taxBonusFlat, this);
            bonus += GetStatValue(FCStatDefOf.taxBasePercentage);
            bonus = ((100d + bonus) / 100d);
            return bonus;
        }

        public string GetTaxBaseDesc()
        {
            double taxBonus = GetSettlementTaxBonus();
            string desc = "FCTaxBase".Translate() + ": " + (taxBonus * 100d).ToString() + "%";
            desc += "\n\n";

            // taxBasePercentage: buildings, events, settlement type, plus faction-level policies
            string settlementMods = GetStatDesc(FCStatDefOf.taxBasePercentage);
            if (!settlementMods.NullOrEmpty())
                desc += settlementMods;

            // taxBonusFlat: faction-only stat (appliesToSettlements=false, so GetStatDesc skips it)
            FactionFC faction = FindFC.FactionComp;
            string factionMods = faction.GetFactionStatDesc(FCStatDefOf.taxBonusFlat);
            if (!factionMods.NullOrEmpty())
                desc += factionMods;

            // Behavior contributions for taxBonusFlat (e.g., Egalitarian tax-break penalty)
            FindFC.PolicyManager.ForEachBehavior(b =>
            {
                string behaviorDesc = b.GetStatDescription(FCStatDefOf.taxBonusFlat, this);
                if (!behaviorDesc.NullOrEmpty())
                    desc += behaviorDesc;
            });

            return desc.Trim();
        }

        public double GetTotalIncome() => totalIncome;

        public int GetTotalWorkers()
        {
            int totalWorkers = 0;
            foreach (ResourceFC resource in resources)
            {
                totalWorkers += resource.assignedWorkers;
            }

            while (totalWorkers > workersUltraMax)
            {
                if (IncreaseWorkers(null, -1))
                {
                    totalWorkers -= 1;
                }
                else
                {
                    LogUtil.Error($"GetTotalWorkers: IncreaseWorkers failed to shed a worker for {Name}. Breaking to prevent freeze.");
                    break;
                }
            }

            return totalWorkers;
        }

        /// <summary>
        /// Internal worker count for use inside RecomputeProfit. Reads backing fields directly
        /// and sheds workers without triggering profit recalculation.
        /// </summary>
        private int GetTotalWorkers_Internal()
        {
            int totalWorkers = 0;
            foreach (ResourceFC resource in resources)
            {
                totalWorkers += resource.assignedWorkers;
            }

            int maxAttempts = resources.Count * ((int)(totalWorkers - _workersUltraMax) + 1) * 3;
            int attempts = 0;
            while (totalWorkers > _workersUltraMax)
            {
                if (++attempts > maxAttempts)
                {
                    LogUtil.Error($"GetTotalWorkers_Internal: exceeded {maxAttempts} attempts shedding workers for {Name}. Bailing out to prevent freeze.");
                    break;
                }
                int idx = Rand.RangeInclusive(0, resources.Count - 1);
                if (resources[idx].assignedWorkers > 0)
                {
                    resources[idx].assignedWorkers -= 1;
                    totalWorkers -= 1;
                }
            }

            return totalWorkers;
        }

        private bool CanStillModify(ResourceFC resource, int singleMod) => _workers + singleMod <= workersUltraMax && _workers + singleMod >= 0 && resource.assignedWorkers + singleMod <= workersUltraMax && resource.assignedWorkers + singleMod >= 0;

        public bool IncreaseWorkers(ResourceFC resource, int numWorkers)
        {
            int singleMod = (numWorkers > 0) ? 1 : -1;
            if (resource is null)
            {
                if (numWorkers >= 0 && _workers <= workersUltraMax)
                {
                    return false;
                }

                int maxAttempts = resources.Count * 3;
                while (_workers > workersUltraMax)
                {
                    if (--maxAttempts < 0)
                    {
                        LogUtil.Error($"IncreaseWorkers: exceeded max attempts finding a worker to shed for {Name}. Bailing out to prevent freeze.");
                        break;
                    }
                    int num = Rand.RangeInclusive(0, resources.Count - 1);
                    if (resources[num].assignedWorkers > 0)
                    {
                        resources[num].assignedWorkers -= 1;
                        return true;
                    }
                }
            }
            else
            {
                while (CanStillModify(resource, singleMod))
                {
                    _workers += singleMod;
                    resource.assignedWorkers += singleMod;
                    numWorkers -= singleMod;
                    if (numWorkers == 0)
                    {
                        DirtyProfitCache();
                        FindFC.FactionComp.DirtyFactionProfitCache();
                        return true;
                    }
                }
                DirtyProfitCache();
                FindFC.FactionComp.DirtyFactionProfitCache();
            }

            return false;
        }

        public double GetBaseWorkerCost()
        {
            return FCSettings.workerCost + GetStatValue(FCStatDefOf.workerBaseCost);
        }
        public double GetTotalUpkeep() => totalUpkeep;
        public double GetTotalProfit() => totalProfit;
        public float Happiness => (float)Math.Round(happiness, 1);
        public float Unrest => (float)Math.Round(unrest, 1);
        public float Loyalty => (float)Math.Round(loyalty, 1);
        public float Prosperity => (float)Math.Round(prosperity, 1);

        public ResourceFC ReturnHighestResource()
        {
            double highest = -1;
            ResourceFC highestResource = null;

            foreach (ResourceFC resource in resources)
            {
                if (resource.actualIncome > highest)
                {
                    highest = resource.actualIncome;
                    highestResource = resource;
                }
            }

            return highestResource;
        }

        public double GetDefenseBonus()
        {
            double defenseBonus = 0;
            foreach (ResourceFC resource in resources)
            {
                if (resource.def.defenseWeight > 0f && resource.effectiveRawTotalProduction > 0)
                {
                    defenseBonus += resource.effectiveRawTotalProduction * resource.def.defenseWeight;
                }
            }
            return defenseBonus;
        }

        private string GetDescriptionBiome()
        {
            if (!biomeDef.descriptionKey.NullOrEmpty())
                return biomeDef.descriptionKey.Translate();
            return "FCDescUnknown".Translate();
        }
        private string GetSettlementLevelDesc()
        {
            return settlementDef.GetSettlementTypeExtension()?.GetSettlementLevelDesc(settlementLevel)
                ?? "FCTownLevel5".Translate();
        }

        /// <summary>
        /// Adds stat modifiers from a source (building, settlement type, etc).
        /// Resource production bonuses are now handled via FCStatDef's linkedResource on ResourceFC.
        /// </summary>
        public void AddStatModifiers(List<FCStatModifier> mods, string sourceId, string sourceLabel = null)
        {
            if (mods != null)
            {
                foreach (FCStatModifier mod in mods)
                    statModifiers.Add(new TaggedStatModifier { sourceId = sourceId, sourceLabel = sourceLabel ?? sourceId, mod = mod });
            }
            InvalidateStatCache();
        }

        /// <summary>
        /// Removes stat modifiers previously added by the given source.
        /// mods must be the exact same FCStatModifier object references that were passed to
        /// AddStatModifiers, since removal uses reference equality (the def's objects stored via AddRange).
        /// </summary>
        public void RemoveStatModifiers(List<FCStatModifier> mods, string sourceId)
        {
            if (mods != null)
            {
                foreach (FCStatModifier mod in mods)
                {
                    for (int i = statModifiers.Count - 1; i >= 0; i--)
                    {
                        if (statModifiers[i].mod == mod)
                        {
                            statModifiers.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
            InvalidateStatCache();
        }

        /// <summary>
        /// Removes all stat modifiers that were added with the given sourceId.
        /// </summary>
        public void RemoveStatModifiersBySource(string sourceId)
        {
            for (int i = statModifiers.Count - 1; i >= 0; i--)
            {
                if (statModifiers[i].sourceId == sourceId)
                    statModifiers.RemoveAt(i);
            }
            InvalidateStatCache();
        }

        /// <summary>
        /// Clears all settlement-level transient stat modifiers (from buildings, settlement type).
        /// Does NOT clear permanent modifiers.
        /// </summary>
        public void ClearStatModifiers()
        {
            statModifiers.Clear();
            InvalidateStatCache();
        }

        /// <summary>
        /// Adds permanent stat modifiers that survive event expiry and are serialized with the settlement.
        /// </summary>
        public void AddPermanentModifiers(List<FCStatModifier> mods, string sourceId, string sourceLabel)
        {
            if (mods is null || mods.Count == 0) return;
            foreach (FCStatModifier mod in mods)
            {
                permanentModifiers.Add(new PermanentStatModifier
                {
                    stat = mod.stat,
                    value = mod.value,
                    sourceId = sourceId,
                    sourceLabel = sourceLabel ?? sourceId
                });
            }
            InvalidateStatCache();
        }

        /// <summary>
        /// Removes all permanent modifiers that were added with the given sourceId.
        /// </summary>
        public void RemovePermanentModifiersBySource(string sourceId)
        {
            for (int i = permanentModifiers.Count - 1; i >= 0; i--)
            {
                if (permanentModifiers[i].sourceId == sourceId)
                    permanentModifiers.RemoveAt(i);
            }
            InvalidateStatCache();
        }

        /// <summary>
        /// Returns true if this settlement has any permanent modifier from the given source.
        /// </summary>
        public bool HasPermanentModifier(string sourceId)
        {
            foreach (PermanentStatModifier psm in permanentModifiers)
            {
                if (psm.sourceId == sourceId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Schedules a self-decaying penalty that delivers <paramref name="total"/> severity to a loss
        /// stat (happinessLostBase/loyaltyLostBase/unrestGainedBase) spread over <paramref name="days"/>
        /// days. Entries with the same stat + sourceLabel are merged (severity summed, window reset) so a
        /// massacre doesn't flood the list. Visible in the settlement's morale tooltip while active.
        /// </summary>
        public void AddDecayingPenalty(FCStatDef lossStat, double total, int days, string sourceId, string label)
        {
            if (lossStat is null || total <= 0 || days <= 0) return;

            foreach (DecayingStatPenalty existing in decayingPenalties)
            {
                if (existing.stat == lossStat && existing.sourceLabel == label)
                {
                    existing.MergePenalty(total, days);
                    InvalidateStatCache();
                    return;
                }
            }

            decayingPenalties.Add(new DecayingStatPenalty
            {
                stat = lossStat,
                remaining = total,
                daysTotal = days,
                daysElapsed = 0,
                sourceId = sourceId,
                sourceLabel = label ?? sourceId
            });
            InvalidateStatCache();
        }

        /// <summary>
        /// Advances every decaying penalty by one day and prunes finished ones. Called once per day from
        /// FactionFC.UpdateSettlementStats AFTER the Update* calls have already applied this day's slice,
        /// so the next day reads the decremented value.
        /// </summary>
        public void TickDecayingPenalties()
        {
            if (decayingPenalties.Count == 0) return;
            for (int i = decayingPenalties.Count - 1; i >= 0; i--)
            {
                DecayingStatPenalty penalty = decayingPenalties[i];
                penalty.DecrementDay();
                if (penalty.Finished)
                    decayingPenalties.RemoveAt(i);
            }
            InvalidateStatCache();
        }

        /// <summary>
        /// The settlement-level stat modifier list (unwrapped from tagged entries).
        /// </summary>
        public List<FCStatModifier> StatModifiers
        {
            get
            {
                var result = new List<FCStatModifier>(statModifiers.Count);
                foreach (TaggedStatModifier tagged in statModifiers)
                    result.Add(tagged.mod);
                return result;
            }
        }

        /// <summary>
        /// Computes and caches the settlement-level stat partial (buildings, settlement type, events, IStatModifierProvider comps).
        /// Does NOT include faction-level modifiers or behavior adjustments.
        /// Called by FactionFC.GetStatValue to get the settlement contribution for aggregation.
        /// </summary>
        public double GetSettlementStatValue(FCStatDef stat)
        {
            if (cachedStatValues.TryGetValue(stat, out double cached))
                return cached;

            double value = stat.IdentityValue;

            foreach (TaggedStatModifier tagged in statModifiers)
            {
                if (tagged.mod.stat == stat)
                {
                    if (stat.aggregation == FCStatAggregation.Additive)
                        value += tagged.mod.value;
                    else
                        value *= tagged.mod.value;
                }
            }

            foreach (PermanentStatModifier perm in permanentModifiers)
            {
                if (perm.stat == stat)
                {
                    if (stat.aggregation == FCStatAggregation.Additive)
                        value += perm.value;
                    else
                        value *= perm.value;
                }
            }

            foreach (WorldObjectComp comp in AllComps)
            {
                if (comp is IStatModifierProvider provider)
                {
                    double compValue = provider.GetStatModifier(stat);
                    if (stat.aggregation == FCStatAggregation.Additive)
                        value += compValue;
                    else
                        value *= compValue;
                }
            }

            // Decaying penalties only ever target Additive loss stats, contributing their per-day slice.
            foreach (DecayingStatPenalty penalty in decayingPenalties)
            {
                if (penalty.stat == stat)
                    value += penalty.CurrentValue;
            }

            cachedStatValues[stat] = value;
            return value;
        }

        /// <summary>
        /// Returns the final combined stat value at this settlement.
        /// Delegates to FactionFC.GetStatValue which combines settlement + faction partials + behaviors.
        /// </summary>
        public double GetStatValue(FCStatDef stat)
        {
            if (!stat.appliesToSettlements)
                return FindFC.FactionComp.GetStatValue(stat);
            return FindFC.FactionComp.GetStatValue(stat, this);
        }

        /// <summary>
        /// Builds a per-source breakdown description for a stat at this settlement.
        /// Combines settlement-level, faction-level, and behavior contributions.
        /// </summary>
        public string GetStatDesc(FCStatDef stat, bool hardinvert = false)
        {
            if (!stat.appliesToSettlements) return "";
            if (!cachedStatDescs.TryGetValue(stat, out string desc))
            {
                desc = "";
                bool isAdditive = stat.aggregation == FCStatAggregation.Additive;
                bool invert = stat.invertedForDisplay;
                // hardinvert flips the displayed sign, so the color test must flip with it to
                // keep "harmful modifier = red". Additive-only; multiplier lines aren't sign-flipped.
                bool colorInvert = invert ^ hardinvert;

                // Settlement-level modifiers (buildings, settlement type, events)
                foreach (TaggedStatModifier tagged in statModifiers)
                {
                    if (tagged.mod.stat != stat) continue;
                    if (isAdditive)
                        desc += TextUtil.ColorizeAdditiveBonus(tagged.mod.value, invert: colorInvert, hardinvert: hardinvert) + " - " + tagged.sourceLabel + "\n";
                    else
                        desc += TextUtil.ColorizeMultiplierBonus(tagged.mod.value, invert: invert) + " - " + tagged.sourceLabel + "\n";
                }

                // Permanent modifiers (persist after event expiry)
                foreach (PermanentStatModifier perm in permanentModifiers)
                {
                    if (perm.stat != stat) continue;
                    if (isAdditive)
                        desc += TextUtil.ColorizeAdditiveBonus(perm.value, invert: colorInvert, hardinvert: hardinvert) + " - " + perm.sourceLabel + " (permanent)\n";
                    else
                        desc += TextUtil.ColorizeMultiplierBonus(perm.value, invert: invert) + " - " + perm.sourceLabel + " (permanent)\n";
                }

                // Decaying penalties (pawn/caravan-loss drips) — always Additive; show the per-day slice + days left
                foreach (DecayingStatPenalty penalty in decayingPenalties)
                {
                    if (penalty.stat != stat) continue;
                    desc += TextUtil.AdditiveBonusLine(penalty.CurrentValue,
                        penalty.sourceLabel + " (" + "FCDecayingPenaltyDaysLeft".Translate(penalty.DaysLeft) + ")",
                        invert: colorInvert, hardinvert: hardinvert) + "\n";
                }

                // IStatModifierProvider comps. Each provider joins its own lines with "\n" but
                // omits a trailing one, so add the separator every other block above already emits.
                foreach (WorldObjectComp comp in AllComps)
                {
                    if (comp is IStatModifierProvider provider)
                    {
                        string provDesc = provider.GetStatModifierDesc(stat);
                        if (!provDesc.NullOrEmpty())
                            desc += provDesc + "\n";
                    }
                }

                // Faction-level policy/trait modifiers (delegated to FactionFC)
                FactionFC faction = FindFC.FactionComp;
                desc += faction.GetFactionStatDesc(stat, hardinvert);

                // Behavior runtime contributions (e.g., Egalitarian tax-break modifiers)
                FindFC.PolicyManager.ForEachBehavior(b =>
                {
                    string behaviorDesc = b.GetStatDescription(stat, this);
                    if (!behaviorDesc.NullOrEmpty())
                        desc += behaviorDesc;
                });

                cachedStatDescs[stat] = desc;
            }
            return desc;
        }

        public void DeconstructBuilding(int buildingSlot)
        {
            BuildingsComp?.DeconstructBuilding(buildingSlot);
        }

        public bool ValidConstructBuilding(BuildingFCDef building, int buildingSlot)
        {
            return BuildingsComp?.ValidConstructBuilding(building, buildingSlot) ?? false;
        }


        public void ConstructBuilding(BuildingFCDef building, int buildingSlot)
        {
            BuildingsComp?.ConstructBuilding(building, buildingSlot);
        }

        public ResourceFC ReturnResource(string defName) //used to return the correct resource based on string name
        {
            ResourceFC res = resources.Find((ResourceFC rfc) => rfc.def.defName == defName);
            if (res is null)
            {
                LogUtil.Message($"Requested resource {defName} is not in settlement {Name}'s resource list");
            }
            return res;
        }

        public ResourceFC GetResource(ResourceTypeDef type) //used to return the correct resource based on string name
        {
            ResourceFC res = resources.Find((ResourceFC rfc) => rfc.def == type);
            if (res is null)
            {
                LogUtil.Message($"Requested resource {type.defName} is not in settlement {Name}'s resource list");
            }
            return res;
        }

        public ResourceFC GetResourceByIndex(int index)
        {
            if (index >= resources.Count || index < 0)
            {
                LogUtil.Warning($"GetResourceByIndex called with out-of-bounds index {index} for settlement {Name} (count: {resources.Count})");
                return null;
            }
            return resources[index];
        }
        public List<ResourceFC> GetTitheableResources()
        {
            List<ResourceFC> list = new List<ResourceFC>();
            foreach (ResourceFC res in Resources)
            {
                if (res.canTithe)
                {
                    list.Add(res);
                }
            }
            return list;
        }
        /// <summary>
        /// Returns a list of *all* things that this settlement can produce.
        /// </summary>
        /// <returns></returns>
        public List<ThingDef> GetGrandThingList()
        {
            if (dirtyGrandThingListFlag)
            {
                grandThingList = new List<ThingDef>();
                foreach (ResourceFC res in resources)
                {
                    if (res.canTithe)
                    {
                        List<ThingDef> resList = res.GenerateThingDefList();
                        if (resList != null && resList.Count > 0)
                        {
                            grandThingList.AddRange(resList);
                        }
                    }
                }
                dirtyGrandThingListFlag = false;
            }
            return grandThingList;
        }
        public void DirtyGrandThingList()
        {
            dirtyGrandThingListFlag = true;
            FindFC.FactionComp.DirtyGrandThingList();
        }

        public float GetOneTimeSilverIncome()
        {
            return oneTimeSilverIncome;
        }

        public void ResetOneTimeSilverIncome()
        {
            oneTimeSilverIncome = 0;
        }

        public void AddOneTimeSilverIncome(float amount)
        {
            oneTimeSilverIncome += amount;
        }

        public float ReturnOneTimeSilverIncome(bool reset)
        {
            float income = oneTimeSilverIncome;

            if (reset)
            {
                ResetOneTimeSilverIncome();
            }

            return income;
        }

        public void GoTo()
        {
            Find.World.renderer.wantedMode = WorldRenderMode.Planet;

            //Select Settlement Tile
            Find.WorldSelector.ClearSelection();
            Find.WorldSelector.Select(Find.WorldObjects.MapParentAt(Tile));
            if (Find.MainButtonsRoot.tabs.OpenTab != null)
            {
                Find.MainButtonsRoot.tabs.OpenTab.TabWindow.Close();
            }
        }
        public List<ResourcePool> CreateResourcePools()
        {
            List<ResourcePool> pools = new List<ResourcePool>();

            foreach (ResourceFC resource in resources)
            {
                if (resource.def.isPoolResource)
                {
                    ResourcePool pool = resource.CreatePool();
                    if (pool.pool != 0)
                    {
                        pools.Add(pool);
                    }
                }
            }

            return pools;
        }

        public void DirtyResourceCache(ResourceTypeDef resDef)
        {
            GetResource(resDef)?.SetDirtyCache();
        }
        public void DirtyResourceCache(ResourceFC res)
        {
            res?.SetDirtyCache();
        }
        public void DirtyResourceCaches()
        {
            foreach (ResourceFC res in resources)
            {
                res.SetDirtyCache();
            }
        }
        /// <summary>
        /// Handles any necessary pre-tax preparations to ensure that the tax calculation is up-to-date and accurate.
        /// </summary>
        private void PreTaxPrep()
        {
            DirtyResourceCaches();
            DirtyStatsCache();
            calculatingTax = true;
        }
        public void AccumulateDailyProduction()
        {
            foreach (ResourceFC res in resources)
                res.AccumulateDailyProduction(); // each resource accrues its own taxable/tithe budget

            // Non-resource terms for the day. Negative building upkeep is income (2b-bis).
            DirtyProfitCache();
            // Force RecomputeProfit so _workerTotalUpkeep is fresh before we read it below.
            double _ignore = totalUpkeep;
            double dayNonResourceIncome = 0;
            double buildingsUpkeep = _buildingsUpkeep;
            if (buildingsUpkeep < 0) dayNonResourceIncome += -buildingsUpkeep;

            double dayUpkeep = _workerTotalUpkeep + (buildingsUpkeep > 0 ? buildingsUpkeep : 0);

            foreach (WorldObjectComp comp in AllComps)
            {
                if (comp is IProfitContributor contributor)
                {
                    double inc = contributor.GetDailyIncomeContribution();
                    if (inc > 0) dayNonResourceIncome += inc;
                    double up = contributor.GetDailyUpkeepContribution();
                    if (up > 0) dayUpkeep += up;
                }
            }

            accruedNonResourceIncome += dayNonResourceIncome;
            accruedTotalUpkeep += dayUpkeep; // deliberately excludes the resource tithe term — tithes deducted once at tax
            taxAccrualDays++;
        }
        private void PostTaxPrep()
        {
            calculatingTax = false;
            foreach (ResourceFC res in resources)
                res.ResetAccumulator();
            accruedNonResourceIncome = 0;
            accruedTotalUpkeep = 0;
            taxAccrualDays = 0;
        }
        /// <summary>
        /// This function handles the calculations for determining this settlement's taxes at tax time. It handles both tithes and silver taxes.
        /// </summary>
        /// <param name="silverAmount">The amount of silver to tax; positive if the player gains silver, negative otherwise.</param>
        /// <returns>A list of things produced by tithing resources. May be empty if there are no tithes.</returns>
        public List<Thing> CreateTax(out int silverAmount)
        {
            PreTaxPrep();
            settlementDef.GetSettlementTypeExtension()?.PreTax(this);
            TaxTickRegistry.InvokePreSettlementCreateTax(this);

            List<Thing> titheThings = new List<Thing>();
            // Accrued gross (post-stockpile, pre-tithe) + accrued non-resource income - accrued upkeep + one-time silver.
            double accruedNet = AccruedGrossIncome - AccruedUpkeep;

            double fulfilledTitheValue = 0;
            foreach (ResourceFC resource in resources)
            {
                if (resource.canTithe && !resource.tithesPaused)
                {
                    List<Thing> resTitheThings = resource.GenerateTithe(out int resExtraSilver);
                    if (resTitheThings is object && resTitheThings.Count > 0)
                    {
                        titheThings.AddRange(resTitheThings);
                        foreach (Thing t in resTitheThings)
                            fulfilledTitheValue += t.MarketValue * t.stackCount;
                    }
                    accruedNet += resExtraSilver; // random-tithe stock disbursement, post-tax hook silver
                }
            }

            int tmpSilverAmount = (int)(accruedNet - fulfilledTitheValue + ReturnOneTimeSilverIncome(true));

            PostTaxPrep();
            silverAmount = tmpSilverAmount;
            settlementDef.GetSettlementTypeExtension()?.PostTax(this, ref silverAmount, titheThings);
            TaxTickRegistry.InvokePostSettlementCreateTax(this, ref silverAmount, titheThings);
            return titheThings;
        }
    }

    // NOTE: PawnGizmos patch moved to GizmosPatches.cs to avoid duplication
    // The optimized version in GizmosPatches.PawnDraftGizmos handles all pawn gizmo modifications
}