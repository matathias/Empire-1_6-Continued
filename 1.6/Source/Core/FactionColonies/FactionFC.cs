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
    public class FactionFC : WorldComponent, ISettlementListener, IMilitaryOperationListener, IMercenarySquadListener, IResearchListener
    {
        #region Fields & Properties

        /* Core Identity */
        public string name = "FCPlayerFaction".Translate();
        public string title = "FCEmpire".Translate();
        public Texture2D factionIcon = TexLoad.factionIcons[0];
        public string factionIconPath = TexLoad.factionIcons[0].name;

        /* Reflection handle to RimWorld's private FactionDef.factionIcon texture cache.
         * The cache is lazy-loaded once by the FactionIcon getter and never re-read, so we
         * must null it whenever we reassign factionIconPath on the shared PColony def
         * (otherwise the previous save's icon leaks into the next load). */
        private static readonly System.Reflection.FieldInfo factionDefIconCache =
            typeof(FactionDef).GetField("factionIcon",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        public Color factionColorPrimary = Color.white;
        public Color factionColorSecondary = Color.white;
        public bool hasFactionColor;
        public bool hasFactionColorSecondary;
        public bool factionCreated;
        private int foundingTick = 0;
        public int FoundingTick => foundingTick;

        /* Save-format version stamp */
        // Semantic mod version (major.minor.patch) that wrote the currently-loaded save.
        // Re-stamped to the active version on every save. Null for pre-stamp (legacy) saves.
        private string savedModVersion;

        // The value read from the save on load, before re-stamping. Migration reads this. Not scribed.
        private string loadedModVersion;
        public string LoadedModVersion => loadedModVersion;
        private Vector2 startingLongLat = new Vector2();
        public Vector2 StartingLongLat => startingLongLat;

        /* Capital & Maps */
        public PlanetTile capitalLocation = PlanetTile.Invalid;
        private Map taxMap;

        public Map TaxMap
        {
            get
            {
                if (taxMap is object) return taxMap;

                FactionFC comp = FindFC.FactionComp;
                Map map = null;
                if (comp is object)
                {
                    map = Find.WorldObjects.SettlementAt(comp.capitalLocation)?.Map;
                }

                if (map is null)
                {
                    Map currentMap = Find.CurrentMap;
                    if (currentMap is object && currentMap.IsPlayerHome)
                    {
                        map = currentMap;
                    }
                    else
                    {
                        map = Find.AnyPlayerHomeMap;
                    }

                    if (map is object)
                    {
                        LogUtil.MessageForce(
                            "Unable to find a player-set tax map or a valid location for the capital. Please open the faction main menu tab and set the capital and tax map. Taxes were sent to the following random PlayerHomeMap " +
                            map.Parent.LabelCap);
                    }
                    else
                    {
                        LogUtil.Warning("TaxMap: No player home map found. Taxes cannot be delivered.");
                    }
                }

                return map;
            }
        }

        /* Settlements */
        /// <summary>
        /// Used by other mods to find our settlements. Move, rename, or otherwise modify at your own peril
        /// </summary>
        public List<WorldSettlementFC> settlements = new List<WorldSettlementFC>();

        /// <summary>
        /// Maps an Empire trade-caravan Lord (by loadID) to the settlement it counts as "home", computed
        /// once when the caravan spawns (see LordPatches). Used to route a slaughtered caravan's penalty
        /// to the right settlement. Cleaned up when the lord ends; pruned daily as a backstop.
        /// </summary>
        public Dictionary<int, WorldSettlementFC> caravanHomeSettlements = new Dictionary<int, WorldSettlementFC>();
        private List<WorldSettlementFC> _caravanHomes = new List<WorldSettlementFC>();
        private List<int> _caravanHomeIDs = new List<int>();

        /* Timing & Scheduling */
        public int timeStart = Find.TickManager.TicksGame;
        public int uiTimeUpdate;
        public int militaryTimeDue;
        public const int MercenaryHealTickInterval = GenDate.TicksPerHour;
        private bool firstTick = true;

        /* Lazy-Cached Averages */
        /* Faction averages — lazy-cached via dirtyAveragesCache */
        private double _averageHappiness = 100;
        private double _averageLoyalty = 100;
        private double _averageUnrest = 0;
        private double _averageProsperity = 100;
        private bool dirtyAveragesCache = true;
        public double averageHappiness { get { if (dirtyAveragesCache) RecomputeAverages(); return _averageHappiness; } }
        public double averageLoyalty { get { if (dirtyAveragesCache) RecomputeAverages(); return _averageLoyalty; } }
        public double averageUnrest { get { if (dirtyAveragesCache) RecomputeAverages(); return _averageUnrest; } }
        public double averageProsperity { get { if (dirtyAveragesCache) RecomputeAverages(); return _averageProsperity; } }

        /* Lazy-Cached Profit */
        /* Faction profit — lazy-cached via dirtyFactionProfitCache */
        private double _income;
        private double _upkeep;
        private double _profit;
        private bool dirtyFactionProfitCache = true;
        public double income { get { if (dirtyFactionProfitCache) RecomputeTotalProfit(); return _income; } }
        public double upkeep { get { if (dirtyFactionProfitCache) RecomputeTotalProfit(); return _upkeep; } }
        public double profit { get { if (dirtyFactionProfitCache) RecomputeTotalProfit(); return _profit; } }

        /* Projected faction-wide silver flows (per-cycle forecast, not period averages).
         * Derived from each settlement's projected values. Edict upkeep is stable,
         * paid in full each tax cycle, so it's added directly to projected upkeep. */
        public bool HasTaxAverageData => settlements.Any(s => s.TaxAccrualDays > 0);
        public double averageIncome => settlements.Sum(s => s.ProjectedIncome);
        public double averageUpkeep => settlements.Sum(s => s.ProjectedUpkeep) + policyManager.GetEdictUpkeep();
        public double averageProfit => averageIncome - averageUpkeep - settlements.Sum(s => s.ProjectedTitheValue);

        /* Lazy-Cached Tech Level */
        /* Tech level — lazy-cached via dirtyTechLevelCache */
        private TechLevel _techLevel = TechLevel.Undefined;
        private bool dirtyTechLevelCache = true;
        public TechLevel techLevel
        {
            get
            {
                if (dirtyTechLevelCache) RecomputeTechLevel();
                return _techLevel;
            }
        }

        /* Lazy-Cached Grand Thing List */
        private bool DirtyGrandThingListFlag = true;
        private List<ThingDef> grandThingList = null;

        /* Faction-level stat cache. Invalidated by InvalidateFactionStatCache (e.g. on policy
         * mutations); read by GetFactionStatValue. Policy/trait/edict/behavior state moved to
         * PolicyManager; access via FindFC.PolicyManager. */
        private Dictionary<FCStatDef, double> cachedFactionStatValues = new Dictionary<FCStatDef, double>();

        public PolicyManager policyManager = new PolicyManager();

        /* Events & Bills */
        public FCEventManager eventManager = new FCEventManager();

        // The canonical read path for the event queue. Delegates to the manager.
        public IReadOnlyList<FCEvent> Events => eventManager.Events;
        public int EventsVersion => eventManager.Version;

        /* Situations — long-lived progress meters; effects delegate to the event system. */
        public FCSituationManager situationManager = new FCSituationManager();

        /// <summary>
        /// Holds and indexes all active <see cref="MilitaryOperation"/>s. Single source of truth
        /// for military operation state. <c>SendMilitary</c> / <c>AttackPlayerSettlement</c>
        /// route through <c>CreateOffensiveOp</c> / <c>CreateDefensiveOp</c> on this manager.
        /// </summary>
        public MilitaryOperationManager militaryOperationManager = new MilitaryOperationManager();

        public float randomEventLastAdded = 0f;

        /* Tax / billing: bills list, due timer, autoresolve toggle, ID counters */
        public TaxLedger taxLedger = new TaxLedger();

        /* Resources */
        public List<ResourcePool> resourcePools = new List<ResourcePool>();
        public ThingWithComps powerOutput;
        public List<ResourceDisplay> factionResources = new List<ResourceDisplay>();
        public List<ResourceDisplay> FactionResources => factionResources;

        /* Faction-level cooldown for the "Hire Laborers" action (Empire sends temporary laborers). */
        public CooldownAbility laborerCooldown = new CooldownAbility();

        /* Military & Roads */
        public MilitaryFC military = new MilitaryFC();
        public EmpireThreatAdaptation threatAdaptation = new EmpireThreatAdaptation();
        public FCRoadBuilder roadBuilder = new FCRoadBuilder();

        /* Enemy settlements the player conquered and chose to capture, awaiting deferred
         * conversion into Empire settlements once they leave the looted base map. */
        public List<PendingSettlementCapture> pendingCaptures = new List<PendingSettlementCapture>();

        /* Caravans */
        public List<PlanetTile> settlementCaravansList = new List<PlanetTile>(); //list of locations caravans already sent to
        /// <summary>
        /// Player-selected caravan types. Strings are logical identifiers:
        /// resource defNames (e.g. "RTD_Food"), "Exotic", or "Slaver".
        /// Resolved to actual TraderKindDefs in UpdateFactionDef().
        /// </summary>
        public List<string> enabledCaravanTypes = new List<string>();

        /* Leveling */
        public const int MaxFactionLevel = 5;
        public int factionLevel = 1;
        public float factionXPCurrent = 0;
        public float factionXPGoal = 100;

        /* ID Counters */
        // Unit/squad/mercenary/mercenarySquad/fireSupport ID counters now live on
        // MilitaryFC (next to the collections they index). See the Legacy Save
        // Migration State block at the end of this region for the load-time shim.
        public int nextPrisonerID = 1;

        /* Workload assigned to new prisoners on capture. Per-settlement override lives on
         * WorldObjectComp_SettlementPrisoners.defaultWorkloadOverride. */
        public FCWorkLoad defaultPrisonerWorkload = FCWorkLoad.Light;

        /* Filters & Misc */
        public XenotypeFilter xenotypeFilter;
        public AnimalFilter animalFilter;
        public List<PlanetLayerDef> layersForTilePicker = null;
        public float tradedAmount = 0;

        /*-*-*-*-* Legacy Save Migration State *-*-*-*-*/
        /* All fields below exist only to round-trip pre-extraction save data into the
         * current managers (TaxLedger, FCEventManager, PolicyManager, MilitaryFC). They
         * are read during ExposeData's LoadingVars phase by ScribeAndMigrateLegacyState(),
         * consumed during ResolvingCrossRefs / PostLoadInit, and then reset to null /
         * sentinel so subsequent saves omit the legacy XML keys.
         *
         * To drop pre-extraction save compatibility: delete this block, delete
         * ScribeAndMigrateLegacyState(), and delete its call site in ExposeData. */

        // Pre-FCEventManager events list. Mirrored under the legacy "events" XML key;
        // drained into eventManager during ResolvingCrossRefs. DO NOT READ — use Events.
        private List<FCEvent> events = new List<FCEvent>();

        // Pre-1.5 MilitaryCustomizationUtil -> MilitaryFC rename. Loaded into this buffer
        // during LoadingVars, swapped onto `military` during PostLoadInit.
        private MilitaryFC _legacyMilitary = null;

        // Pre-TaxLedger flat tax/billing scribe keys.
        List<BillFC> legacyBills = null;
        List<BillFC> legacyOldBills = null;
        bool legacyAutoResolve = false;
        bool legacyAllowLate = true;
        int legacyTaxTimeDue = -1;
        int legacyNextTaxId = -1;
        int legacyNextBillId = -1;
        int legacyNextEventId = -1;

        // Pre-MilitaryFC.SeedNextIds ID counters. Scribe_Values reads during LoadingVars,
        // the SeedNextIds consumer runs during PostLoadInit; must be class fields so
        // values survive the phase transition.
        int legacyNextUnitId = -1;
        int legacyNextSquadId = -1;
        int legacyNextMercId = -1;
        int legacyNextMercSquadId = -1;
        int legacyNextFireSupportId = -1;

        // Pre-PolicyManager policy/trait/edict lists. Drained into policyManager during PostLoadInit.
        List<FCPolicy> legacyPolicies = null;
        List<FCPolicy> legacyFactionTraits = null;
        Dictionary<FCPolicyCategory, FCPolicy> legacyEdicts = null;

        #endregion

        #region Constructor & Lifecycle

        public FactionFC(World world) : base(world)
        {
            // We used to do the harmony patching here, but I moved it to HarmonyPatcher.cs with a
            // [StaticConstructoreOnStartup] tag. Honestly not sure why the harmony patch was run here.
            // Leaving this comment mostly for posterity.
        }

        /// <summary>
        /// Called when the Empire faction is created.
        /// </summary>
        public void OnCreation()
        {
            foundingTick = Find.TickManager.TicksGame;
        }

        public string GetFoundingDate(bool full = true)
        {
            if (full)
            {
                return GenDate.DateFullStringAt(foundingTick, startingLongLat);
            }
            return GenDate.DateShortStringAt(foundingTick, startingLongLat);
        }

        #endregion

        #region Serialization & Initialization

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref title, "title");
            Scribe_Values.Look(ref foundingTick, "foundingTick", defaultValue: 0);
            Scribe_Values.Look(ref startingLongLat, "foundingLongLat");
            Scribe_Values.Look(ref capitalLocation, "capitalLocation");
            Scribe_References.Look(ref taxMap, "taxMap");
            Scribe_Values.Look(ref factionCreated, "factionCreated");

            // Save-format version stamp. Always re-stamp to the active version on save; capture the
            // previously-stored value on load so MigrateSaveFormat() can detect schema differences.
            if (Scribe.mode == LoadSaveMode.Saving) savedModVersion = FCSettings.GetModVersion();
            Scribe_Values.Look(ref savedModVersion, "savedModVersion", null);
            if (Scribe.mode == LoadSaveMode.LoadingVars) loadedModVersion = savedModVersion;

            Scribe_Values.Look(ref _averageHappiness, "averageHappiness");
            Scribe_Values.Look(ref _averageLoyalty, "averageLoyalty");
            Scribe_Values.Look(ref _averageUnrest, "averageUnrest");
            Scribe_Values.Look(ref _averageProsperity, "averageProsperity");

            Scribe_Values.Look(ref _income, "income");
            Scribe_Values.Look(ref _upkeep, "upkeep");
            Scribe_Values.Look(ref _profit, "profit");

            Scribe_Values.Look(ref timeStart, "timeStart", -1);
            Scribe_Values.Look(ref uiTimeUpdate, "uiTimeUpdate");
            Scribe_Values.Look(ref militaryTimeDue, "militaryTimeDue", -1);
            Scribe_Values.Look(ref _techLevel, "techLevel");
            Scribe_Values.Look(ref factionIconPath, "factionIconPath", "Base");
            Scribe_Values.Look(ref hasFactionColor, "hasFactionColor", false);
            Scribe_Values.Look(ref factionColorPrimary, "factionColorPrimary", Color.white);
            Scribe_Values.Look(ref hasFactionColorSecondary, "hasFactionColorSecondary", false);
            Scribe_Values.Look(ref factionColorSecondary, "factionColorSecondary", Color.white);

            Scribe_Collections.Look(ref settlements, "settlements", LookMode.Reference);

            Scribe_Deep.Look(ref policyManager, "policyManager");
            if (policyManager is null) policyManager = new PolicyManager();

            Scribe_Deep.Look(ref eventManager, "eventManager");
            if (eventManager is null) eventManager = new FCEventManager();

            Scribe_Deep.Look(ref situationManager, "situationManager");
            if (situationManager is null) situationManager = new FCSituationManager();

            Scribe_Deep.Look(ref militaryOperationManager, "militaryOperationManager");
            if (militaryOperationManager is null) militaryOperationManager = new MilitaryOperationManager();

            Scribe_Collections.Look(ref settlementCaravansList, "settlementCaravansList", LookMode.Value);
            Scribe_Collections.Look(ref enabledCaravanTypes, "enabledCaravanTypes", LookMode.Value);
            Scribe_Collections.Look(ref caravanHomeSettlements, "caravanHomeSettlements", LookMode.Value, LookMode.Reference, ref _caravanHomeIDs, ref _caravanHomes);
            // Re-init ONLY in PostLoadInit — a reference-valued dictionary re-inited before ResolvingCrossRefs crashes the cross-ref resolve
            if (Scribe.mode == LoadSaveMode.PostLoadInit && caravanHomeSettlements is null)
                caravanHomeSettlements = new Dictionary<int, WorldSettlementFC>();

            //New Production types
            Scribe_Collections.Look(ref resourcePools, "resourcePools", LookMode.Deep);
            Scribe_References.Look(ref powerOutput, "powerOutput");

            Scribe_Deep.Look(ref xenotypeFilter, "xenotypeFilter");
            Scribe_Deep.Look(ref animalFilter, "animalFilter");

            //Military Data
            Scribe_Deep.Look(ref military, "militaryFC");

            //Load ID tracking
            Scribe_Values.Look(ref nextPrisonerID, "nextPrisonerID", 1);

            //Prisoner defaults
            Scribe_Values.Look(ref defaultPrisonerWorkload, "defaultPrisonerWorkload", FCWorkLoad.Light);

            //Tax/billing
            Scribe_Deep.Look(ref taxLedger, "taxLedger");
            if (taxLedger is null) taxLedger = new TaxLedger();

            //Hire-laborers action cooldown
            Scribe_Deep.Look(ref laborerCooldown, "laborerCooldown");
            if (laborerCooldown is null) laborerCooldown = new CooldownAbility();

            //Road builder
            Scribe_Deep.Look(ref roadBuilder, "roadBuilder");

            //Threat adaptation
            Scribe_Deep.Look(ref threatAdaptation, "threatAdaptation");
            if (threatAdaptation == null) threatAdaptation = new EmpireThreatAdaptation();

            //Pending player-colony settlement captures (deferred conversion)
            Scribe_Collections.Look(ref pendingCaptures, "pendingCaptures", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && pendingCaptures is null)
                pendingCaptures = new List<PendingSettlementCapture>();

            //Settlement Leveling
            Scribe_Values.Look(ref factionLevel, "factionLevel");
            Scribe_Values.Look(ref factionXPCurrent, "factionXPCurrent");
            Scribe_Values.Look(ref factionXPGoal, "factionXPGoal");

            // Legacy save migration — see helper for the full scribe + migration pipeline.
            ScribeAndMigrateLegacyState();

            if (Scribe.mode == LoadSaveMode.PostLoadInit) PostLoadInit();

            //Research Trading
            Scribe_Values.Look(ref tradedAmount, "tradedAmount");

            //Random Event
            Scribe_Values.Look(ref randomEventLastAdded, "randomEventLastAddedTick");
            // eventCooldowns / eventFireCounts now live on eventManager (scribed above).
        }

        /*-*-*-*-* Legacy Save Migration *-*-*-*-*/
        /// <summary>
        /// All legacy-save migration logic. Reads pre-extraction XML keys during LoadingVars
        /// and drains them into the modern managers during ResolvingCrossRefs / PostLoadInit.
        /// Called once from <see cref="ExposeData"/>, after the modern managers
        /// (taxLedger, eventManager, policyManager, military) have been Scribe_Deep'd.
        ///
        /// To drop pre-extraction save compatibility: delete this method, delete its call
        /// site in ExposeData, and delete the Legacy Save Migration State block in the
        /// field-declarations region.
        /// </summary>
        private void ScribeAndMigrateLegacyState()
        {
            /* Phase 1: scribe legacy XML keys.
             * Scribe_*.Look runs in all phases; sentinel defaults ensure new saves omit
             * these keys once the buffers have been reset post-migration. */

            // Pre-PolicyManager policy/trait/edict — explicit LoadingVars gate (no write on Saving).
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Collections.Look(ref legacyPolicies, "factionPolicies", LookMode.Deep);
                Scribe_Collections.Look(ref legacyFactionTraits, "factionTraits", LookMode.Deep);
                Scribe_Collections.Look(ref legacyEdicts, "edicts", LookMode.Value, LookMode.Deep);
            }

            // Pre-FCEventManager events list.
            Scribe_Collections.Look(ref events, "events", LookMode.Deep);

            // Pre-MilitaryFC.SeedNextIds ID counters.
            Scribe_Values.Look(ref legacyNextUnitId, "nextUnitID", -1);
            Scribe_Values.Look(ref legacyNextSquadId, "nextSquadID", -1);
            Scribe_Values.Look(ref legacyNextMercId, "nextMercenaryID", -1);
            Scribe_Values.Look(ref legacyNextMercSquadId, "nextMercenarySquadID", -1);
            Scribe_Values.Look(ref legacyNextFireSupportId, "nextMilitaryFireSupportID", -1);

            // Pre-1.5 MilitaryCustomizationUtil -> MilitaryFC rename. LoadingVars-gated to
            // avoid spurious empty writes on save.
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Deep.Look(ref _legacyMilitary, "militaryCustomizationUtil");
            }

            // Pre-TaxLedger flat tax/billing keys.
            Scribe_Values.Look(ref legacyNextTaxId, "nextTaxID", -1);
            Scribe_Values.Look(ref legacyNextBillId, "nextBillID", -1);
            Scribe_Values.Look(ref legacyNextEventId, "nextEventID", -1);
            Scribe_Collections.Look(ref legacyBills, "Bills", LookMode.Deep);
            Scribe_Collections.Look(ref legacyOldBills, "OldBills", LookMode.Deep);
            Scribe_Values.Look(ref legacyAutoResolve, "autoResolveBills");
            Scribe_Values.Look(ref legacyAllowLate, "allowLatePayments", true);
            Scribe_Values.Look(ref legacyTaxTimeDue, "taxTimeDue", -1);

            /* Phase 2: ResolvingCrossRefs migrations. Run before any PostLoadInit consumer
             * (e.g. WorldSettlementFC stat-modifier reapply) reads the managers. */
            if (Scribe.mode == LoadSaveMode.ResolvingCrossRefs)
            {
                // events → eventManager
                if (events != null && events.Count > 0)
                {
                    eventManager.SeedFromLegacy(events);
                    events = null;
                    LogUtil.MessageForce("FactionFC: migrated legacy events list into FCEventManager.");
                }

                // Legacy tax fields → taxLedger
                if (taxLedger.IsEmpty
                    && (legacyBills != null || legacyOldBills != null
                        || legacyTaxTimeDue != -1 || legacyAutoResolve
                        || legacyNextTaxId != -1 || legacyNextBillId != -1))
                {
                    taxLedger.SeedFromLegacy(
                        legacyBills, legacyOldBills,
                        legacyAutoResolve, legacyAllowLate,
                        legacyTaxTimeDue, legacyNextTaxId, legacyNextBillId);
                    LogUtil.MessageForce("FactionFC: migrated legacy tax fields into TaxLedger.");
                }
                /* Reset legacy buffers after consumption. SeedFromLegacy assigns the bill
                 * lists by reference, so without nulling them out the next save would
                 * double-write under both the legacy <Bills> and the nested <taxLedger><bills>
                 * keys. Resetting scalars to their sentinel defaults makes Scribe omit them. */
                legacyBills = null;
                legacyOldBills = null;
                legacyAutoResolve = false;
                legacyAllowLate = true;
                legacyTaxTimeDue = -1;
                legacyNextTaxId = -1;
                legacyNextBillId = -1;

                if (legacyNextEventId != -1)
                {
                    eventManager.SeedNextEventId(legacyNextEventId);
                    legacyNextEventId = -1;
                }

                /* Military migration must run here (not PostLoadInit): mercenaries live inside
                 * _legacyMilitary's deep subtree, and their PostLoadInit (which clones loadouts via
                 * MilUnitFC(bool) -> FindFC.Military.NextUnitId()) fires BEFORE FactionFC's own
                 * PostLoadInit (children register for PostLoad before parents). Swapping military in
                 * and seeding the ID counters here — before any PostLoadInit consumer reads them —
                 * keeps FindFC.Military non-null and hands clones non-colliding loadIDs. */
                // Pre-1.5 military rename swap + new-instance fallback.
                if (military is null && _legacyMilitary is object) military = _legacyMilitary;
                if (military is null) military = new MilitaryFC();
                _legacyMilitary = null;

                // ID counters -> military.SeedNextIds
                if (legacyNextUnitId != -1 || legacyNextSquadId != -1
                    || legacyNextMercId != -1 || legacyNextMercSquadId != -1
                    || legacyNextFireSupportId != -1)
                {
                    military.SeedNextIds(
                        legacyNextUnitId, legacyNextSquadId,
                        legacyNextMercId, legacyNextMercSquadId,
                        legacyNextFireSupportId);
                    LogUtil.MessageForce("FactionFC: migrated legacy ID counters into MilitaryFC.");
                    /* Reset to sentinel so subsequent saves omit these legacy XML keys. */
                    legacyNextUnitId = -1;
                    legacyNextSquadId = -1;
                    legacyNextMercId = -1;
                    legacyNextMercSquadId = -1;
                    legacyNextFireSupportId = -1;
                }
            }

            /* Phase 3: PostLoadInit migrations. Cross-refs resolved by this point. */
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // Pre-manager policy/trait/edict lists → policyManager
                if (legacyPolicies != null || legacyFactionTraits != null || legacyEdicts != null)
                {
                    policyManager.SeedFromLegacy(legacyPolicies, legacyFactionTraits, legacyEdicts);
                    legacyPolicies = null;
                    legacyFactionTraits = null;
                    legacyEdicts = null;
                    LogUtil.MessageForce("FactionFC: migrated legacy policies/traits/edicts into PolicyManager.");
                }
            }
        }

        private void ScrubNullSettlements(string caller = "")
        {
            int removed = settlements.RemoveAll(s => s is null);
            if (removed > 0)
                LogUtil.Warning($"{caller}: Removed {removed} null settlement reference(s) from save data.");
        }

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);
            LogUtil.MessageForce($"Finalizing init of FactionFC. fromload: {fromLoad}");

            /* Shared init. Runs on both new-game and load paths; all idempotent. */
            RebuildFactionResources();
            EnsureCaravanTypesPopulated();
            EnsureResourcePools();
            EmpireRegistry.Register(this);

            // Rebuild op indices from `active` whether we just migrated a save or not — cheap
            // and always-correct even on a fresh-game start (no-op when active is empty).
            militaryOperationManager?.RebuildIndices();

            if (fromLoad)
            {
                /* Load path. Scribe.mode == LoadingVars here; cross-refs NOT resolved,
                 * maps NOT loaded. Filter finalize is deferred to firstTick because
                 * xenotypeFilter.FinalizeInit reaches into CustomXenotypesForReading,
                 * which calls Scribe.ForceStop if Scribe is still active. And if the
                 * Scribe is ForceStopped during load, then everything breaks.
                 * And I do mean everything. The game straight-up crashes. */
                ApplySavedTechLevelToFactionDef();
            }
            else
            {
                /* New-world path. Scribe is Inactive, disk I/O is legal. */
                EnsureFiltersInitialized();

                /* Rebuild caravan trader kinds last, once factionResources, settlements, and tech
                 * level are settled. If production hasn't computed yet, the helper preserves the
                 * FactionDef's existing list rather than clobbering it with an empty result.
                 *
                 * New-world path only. On the load path this is deferred to FirstTick: it reads the
                 * techLevel property, whose getter would run RecomputeTechLevel during LoadingVars,
                 * firing xenotypeFilter.FinalizeInit mid-Scribe and stripping disk-only custom
                 * xenotypes from the loaded filter. FirstTick rebuilds it once Scribe is Inactive. */
                RebuildCaravanTraderKinds();
            }
        }

        /* Rebuilt on each game init from DefDatabase, ensures defs stay in sync across load. */
        private void RebuildFactionResources()
        {
            factionResources.Clear();
            foreach (ResourceTypeDef resourceTypeDef in FactionCache.AllResourceTypeDefs)
            {
                factionResources.Add(new ResourceDisplay(resourceTypeDef));
                LogUtil.Message($"Added ResourceDisplay for resourceTypeDef {resourceTypeDef} to FactionFC.factionResources");
            }
            factionResources.Sort(ResourceDisplay.SortForUI);
        }

        private void EnsureCaravanTypesPopulated()
        {
            if (enabledCaravanTypes.NullOrEmpty())
            {
                LogUtil.Warning("Null or empty enabledCaravanTypes - Creating and filling list");
                InitEnabledCaravanTypes();
            }
        }

        /* Apply saved tech level to FactionDef early — must happen before anything
         * reads faction.def.techLevel directly. Calls UpdateFactionDef directly instead
         * of going through RecomputeTechLevel, which has side effects (xenotypeFilter
         * FinalizeInit) that depend on deferred initialization. */
        private void ApplySavedTechLevelToFactionDef()
        {
            if (_techLevel <= TechLevel.Undefined) return;
            Faction playerColonyfaction = FindFC.EmpireFaction;
            if (playerColonyfaction != null && playerColonyfaction.def.techLevel < _techLevel)
            {
                UpdateFactionDef(_techLevel, ref playerColonyfaction);
            }
        }

        /* Ensures both filters exist and are initialized. Idempotent (safe to call
         * multiple times). Called from FinalizeInit on the new-world path and from
         * FirstTick on the load path.
         *
         * MUST be called with Scribe.mode == Inactive. xenotypeFilter.FinalizeInit
         * reads custom xenotypes from disk via InitLoadingMetaHeaderOnly, which calls
         * Scribe.ForceStop() when Scribe is active, destroying the active save-load
         * pipeline and nulling all cross-references.
         * In other words, the game crashes and burns. */
        private void EnsureFiltersInitialized()
        {
            if (Scribe.mode != LoadSaveMode.Inactive)
            {
                LogUtil.Error($"EnsureFiltersInitialized called with Scribe.mode={Scribe.mode}. Skipping to avoid Scribe.ForceStop trap.");
                return;
            }

            bool animalWasInitialized = false;
            if (animalFilter is null)
            {
                LogUtil.Warning("Null animalFilter detected - Creating new one");
                animalFilter = new AnimalFilter();
            }
            if (!animalFilter.IsInitialized)
            {
                animalFilter.FinalizeInit();
                animalWasInitialized = true;
            }

            if (xenotypeFilter is null)
            {
                LogUtil.Warning("Null xenotypeFilter detected - Creating new one");
                xenotypeFilter = new XenotypeFilter(this);
            }
            /* Force xenotype re-finalize if animal filter was just initialized; xeno
             * filter depends on animal filter state during its own finalization. */
            if (!xenotypeFilter.IsInitialized || animalWasInitialized)
            {
                xenotypeFilter.FinalizeInit(this);
            }
        }

        /* Cross-ref-dependent post-load work. Invoked from ExposeData's PostLoadInit
         * branch — by this point settlements/edicts/events cross-refs are all resolved. */
        private void PostLoadInit()
        {
            if (Scribe.mode != LoadSaveMode.PostLoadInit)
            {
                LogUtil.Error($"FactionFC.PostLoadInit called during Scribe mode {Scribe.mode}");
                return;
            }
            ScrubNullSettlements("FactionFC.PostLoadInit");
            eventManager.PruneNullDefEvents("FactionFC.PostLoadInit");
            RebuildPendingEdictActivations();
            MigrateSaveFormat();

            // Squad-first refactor migration: bind any legacy comp.militarySquad onto the squad
            // itself (sets squad.settlement) and propagate comp.autoDefend to squad.autoDefend.
            // Runs unconditionally because the legacy buffers are [Unsaved] — once drained, they
            // stay null on subsequent loads.
            MilitaryMigrationUtil.MigrateLegacyComp_MilitarySquad(this);

            // Drain pre-refactor military operation state into the new MilitaryOperationManager.
            // Idempotent: only runs when the manager is empty AND legacy state is present in the
            // loaded save (post-refactor saves write the manager directly and skip migration).
            if (militaryOperationManager is object && militaryOperationManager.IsEmpty
                && MilitaryMigrationUtil.AnyLegacyStatePresent(this))
            {
                MilitaryMigrationUtil.Migrate(this);
            }
            militaryOperationManager?.RebuildIndices();
        }

        // Detects whether the loaded save was written by a different mod version than the active one,
        // and is the single entry point for version-gated save migrations. No migrations exist yet.
        private void MigrateSaveFormat()
        {
            string active = FCSettings.GetModVersion();

            if (loadedModVersion.NullOrEmpty())
            {
                LogUtil.MessageForce($"Loaded a pre-stamp Empire save (no version recorded); active version {active}.");
                // Future: migrations for saves written before the stamp existed.
                return;
            }

            if (loadedModVersion == active) return; // same version, nothing to do

            LogUtil.MessageForce($"Loaded save was written with Empire {loadedModVersion}; active version is {active}.");

            if (FCVersion.TryParse(loadedModVersion, out FCVersion loaded))
            {
                // Future: version-gated migrations, ordered oldest-first, e.g.
                //   if (loaded.IsOlderThan(new FCVersion(1, 6, 0))) { /* migrate ... */ }
            }
        }

        private void RebuildPendingEdictActivations()
        {
            policyManager.pendingEdictActivations.Clear();
            foreach (var kvp in policyManager.edicts)
            {
                if (kvp.Value != null && !kvp.Value.IsFullyActive)
                    policyManager.pendingEdictActivations.Add(kvp.Key);
            }
        }

        #endregion

        public override void WorldComponentUpdate()
        {
            if (roadBuilder.shouldDrawPaths)
            {
                roadBuilder.DrawPaths();
            }
        }

        #region Tick Loop

        private void FirstTick(Faction faction)
        {
            // Settlement resource assignments aren't actually available when we first create the resource display list
            //   in FinalizeInit. So set the display caches as dirty here so they get properly calculated the next time
            //   the UI shows up (or anything else tries to access them)
            SetAllDirtyResourceDisplayCaches();
            
            /* Scribe.mode is Inactive by firstTick — filter FinalizeInit is safe.
             * On the load path, this is where deferred filter init actually happens.
             * On the new-world path, FinalizeInit already initialized them; this is a no-op. */
            EnsureFiltersInitialized();

            /* Re-register with EmpireRegistry in case ClearCaches ran after FinalizeInit
             * (happens during Game.InitNewGame; ClearCaches postfix clears the registry
             * after World.FinalizeInit already registered us during world generation). */
            EmpireRegistry.Register(this);

            /* Built-in stateless squad-assignment validators. */
            EmpireRegistry.Register(new SquadCapValidator());
            EmpireRegistry.Register(new SquadSizeValidator());
            EmpireRegistry.Register(new SquadValueValidator());

            /* Situation approach upkeep (faction-wide, charged daily). */
            EmpireRegistry.Register(new SituationUpkeepDailyCharger());

            roadBuilder.FirstTick();

            /* Hire-laborers action: configurable base cooldown, scaled by policyActionCooldownMultiplier.
             * Re-applied every FirstTick so setting/multiplier changes and loaded state stay consistent
             * (SetCooldown never touches the live tickLastUsed). */
            laborerCooldown.readyLetterKey = "FCHireLaborersReady";
            laborerCooldown.cooldownMessageKey = "FCHireLaborersCooldown";
            laborerCooldown.SetCooldown(FCSettings.laborerCooldownDays * GenDate.TicksPerDay);

            if (!(faction is null))
            {
                _ = techLevel;
                if (TexLoad.factionIcons.Any())
                {
                    factionIcon = TexLoad.factionIcons.FirstOrFallback(obj => obj.name == factionIconPath,
                        TexLoad.factionIcons[0]);
                    UpdateFactionIcon(ref faction, "FactionIcons/" + factionIcon.name);
                    factionIconPath = factionIcon.name;
                }
                else
                {
                    LogUtil.Error("No faction icons loaded. Cannot set faction icon.");
                }

                if (!name.NullOrEmpty() && faction.Name != name)
                {
                    faction.Name = name;
                }

                if (hasFactionColor)
                    faction.color = factionColorPrimary;
            }

            military.CheckMilitaryUtilForErrors();

            // Auto-open patch notes for each mod that has new entries exceeding the player's threshold
            if (FCSettings.patchNoteAutoOpenThreshold != PatchNoteType.Undefined)
            {
                HashSet<string> modIds = new HashSet<string>();
                foreach (PatchNoteDef def in DefDatabase<PatchNoteDef>.AllDefsListForReading)
                    modIds.Add(def.modId);

                foreach (string modId in modIds)
                {
                    PatchNoteDef latest = PatchNoteDef.GetLatestForMod(modId);
                    if (latest is null) continue;
                    FCSettings.GetLastSeenVersion(modId, out int maj, out int min, out int pat);
                    if (latest.IsNewerThan(maj, min, pat)
                        && latest.GetPatchNoteType >= FCSettings.patchNoteAutoOpenThreshold)
                    {
                        Find.WindowStack.Add(new PatchNotesDisplayWindow(modId));
                    }
                }
            }

            /* Get the longlat of the player's starting location. This will be used when calculating founding dates. */
            Map playerHome = Find.AnyPlayerHomeMap;
            if (playerHome is null)
            {
                LogUtil.Warning("Found NULL for player map on first tick. This probably shouldn't happen...");
                startingLongLat = default(Vector2);
            }
            else
            {
                startingLongLat = Find.WorldGrid.LongLatOf(playerHome.Tile);
            }

            /* Rebuild caravan trader kinds last, once factionResources, settlements, and tech
             * level are settled. If production hasn't computed yet (new world, or load path
             * where caches still warm up), the helper preserves the FactionDef's existing list
             * rather than clobbering it with an empty result. */
            RebuildCaravanTraderKinds();
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();
            Faction faction = FindFC.EmpireFaction;
            if (firstTick)
            {
                FirstTick(faction);
                firstTick = false;
            }
            int ticksGame = Find.TickManager.TicksGame;

            // The FireSupportTick governs when artillery shells enter the map, which requires tick precision.
            // The function will early-return if there are no active fire supports, so the overhead in the no-active-support case is hopefully minimal
            FireSupportTick();
            if (faction is object)
            {
                // TickActions dispatches the tick to interfaces and registries, so it has to run every tick.
                TickActions();
                // Offense battle maps have no comp to tick them (defense maps are comp-ticked via
                // WorldObjectComp_SettlementMilitary). Sweep them here every tick so win/loss
                // detection matches defense's per-tick responsiveness.
                OffenseBattlefieldTick();
            }

            // Rare tick
            if (ticksGame % 250 == 0)
            {
                FCEventMaker.ProcessEvents();
                taxLedger.ProcessBills();
                if (policyManager.pendingEdictActivations.Count > 0)
                    policyManager.CheckEdictActivations();
                if (faction is object)
                    roadBuilder.RoadTick();
            }

            // Hourly tick
            if (ticksGame % MercenaryHealTickInterval == 0)
            {
                military?.TickMercenaryHealing(MercenaryHealTickInterval);
            }

            // Daily tick
            if (ticksGame % GenDate.TicksPerDay == 0 && !(faction is null))
            {
                ValidateSettlementCaravansList();
                RecoverOrphanedConstructions(ticksGame);

                if (faction.leader is null || faction.leader.Dead)
                    ColonyUtil.CreatePlayerFactionLeader(faction);

                // Just for future's sake; StatTick expects a non-null faction. If it's ever moved from this if-block, remember to keep the null check
                StatTick();
            }

            // These checks have variable tick times, so they're in charge of their own tick guards
            taxLedger.TaxTick(this, faction);
            MilitaryTick(faction);
            threatAdaptation.Tick();
        }

        public void StatTick()
        {
            // Tick guard moved up into the world component tick
            UpdateSettlementStats();
            AccumulateDailyProduction();
            DirtyAveragesCache();
            SyncGoodwillWithAverages();
            RelationsUtilFC.ResetPlayerColonyRelations();
            UpdateDailyResourcePools();
            PruneCaravanHomeSettlements();
            // After settlement stats + averages are fresh, so condition extensions read current values.
            FCSituationMaker.ProcessSituations(this);
            MakeRandomEvent();
        }

        public void MilitaryTick(Faction faction)
        {
            if (Find.TickManager.TicksGame >= militaryTimeDue)
            {
                if (faction != null &&
                    !FCSettings.disableHostileMilitaryActions &&
                    Find.TickManager.TicksGame > (timeStart + GenDate.TicksPerSeason))
                {
                    //if military actions not disabled or game has not passed through the first season

                    if (settlements.Any() || RaidTargetRegistry.Targets.Count > 0)
                    {
                        // Self-heal orphaned raid-target flags: a target whose attacking op resolved
                        // without clearing IsUnderAttack (older saves from before that path existed,
                        // or an op lost without resolution) would be excluded from the pool below
                        // forever. Clear the flag on any registered target with no live op attacking
                        // it, so it re-enters raid selection this same tick.
                        MilitaryOperationManager opManager = FindFC.MilitaryManager;
                        if (opManager is object)
                        {
                            foreach (IRaidTarget rt in RaidTargetRegistry.Targets)
                            {
                                if (rt is object && rt.IsUnderAttack && !opManager.HasActiveOpTargeting(rt.WorldObject))
                                {
                                    LogUtil.Warning($"Clearing orphaned IsUnderAttack flag on raid target '{rt.Name}' " +
                                        "(no live operation targeting it).");
                                    rt.IsUnderAttack = false;
                                }
                            }
                        }

                        List<WorldSettlementFC> validSettlements = settlements
                            .Where(s => s.MilitaryComp?.isUnderAttack != true && s.settlementDef.canBeRaided)
                            .ToList();
                        List<IRaidTarget> validExternalTargets = RaidTargetRegistry.Targets
                            .Where(t => !t.IsUnderAttack)
                            .ToList();

                        if (validSettlements.Any() || validExternalTargets.Any())
                        {
                            Faction enemy = ThreatScalingUtil.PickWeightedEnemyFaction(this);
                            if (enemy != null)
                            {
                                List<WorldSettlementFC> raidableSettlements = validSettlements
                                    .Where(s => s.settlementDef.GetSettlementTypeExtension()?.CanBeRaidedByFaction(enemy) != false)
                                    .ToList();

                                float settlementTotalWeight = raidableSettlements.Sum(
                                    s => (float)GetMilitaryTargetWeight(s.settlementMilitaryLevel) * s.settlementDef.raidTargetingWeight
                                         * RaidWeightRegistry.GetCombinedWeight(s, enemy));
                                float externalTotalWeight = validExternalTargets.Sum(
                                    t => (float)GetMilitaryTargetWeight(t.MilitaryLevel));
                                float totalWeight = settlementTotalWeight + externalTotalWeight;

                                EnemyPower attackerEntry = FindFC.EnemyPower?.GetOrCompute(enemy);
                                MilitaryForce attackingForce = attackerEntry?.SampleBattleForce(enemy, handicap: true);
                                // Extra NPC offensive levels boost the raid force (on top of the early-game grace cap).
                                if (attackingForce is object && FCSettings.extraNPCOffensiveLevels > 0)
                                {
                                    attackingForce = new MilitaryForce(
                                        attackingForce.militaryLevel + FCSettings.extraNPCOffensiveLevels,
                                        attackingForce.militaryEfficiency,
                                        attackingForce.homeSettlement, attackingForce.homeFaction);
                                }
                                if (attackingForce is null)
                                {
                                    LogUtil.Warning($"AI attack from {enemy?.Name} aborted: no power entry resolvable.");
                                }
                                else if (Rand.Value * totalWeight < settlementTotalWeight && raidableSettlements.Any())
                                {
                                    WorldSettlementFC target = raidableSettlements.RandomElementByWeight(
                                        s => (float)GetMilitaryTargetWeight(s.settlementMilitaryLevel) * s.settlementDef.raidTargetingWeight
                                             * RaidWeightRegistry.GetCombinedWeight(s, enemy));
                                    MilitaryOperationsUtil.AttackPlayerSettlement(attackingForce, target, enemy);
                                }
                                else if (validExternalTargets.Any())
                                {
                                    IRaidTarget target = validExternalTargets.RandomElementByWeight(
                                        t => (float)GetMilitaryTargetWeight(t.MilitaryLevel));
                                    MilitaryOperationsUtil.AttackRaidTarget(attackingForce, target, enemy);
                                }
                            }
                        }
                    }
                }

                militaryTimeDue = Find.TickManager.TicksGame + (GenDate.TicksPerDay * ThreatScalingUtil.ComputeScaledAttackInterval(this));
            }
        }

        private static int GetMilitaryTargetWeight(int militaryLevel)
        {
            switch (militaryLevel)
            {
                case 0:
                case 1:
                    return 10;
                case 2:
                case 3:
                    return 7;
                case 4:
                case 5:
                    return 3;
                default:
                    return 1;
            }
        }

        public void FireSupportTick()
        {
            if (military.fireSupport is null)
            {
                military.fireSupport = new List<MilitaryFireSupport>();
            }
            if (military.fireSupport.Count == 0)
            {
                return;
            }

            //Process ongoing fire supports
            military.fireSupport.RemoveAll(support => support.ShouldBeOver);
            military.fireSupport.ForEach(support => support.Process());
        }

        public void TickActions()
        {
            // Dispatch Tick only to behaviors that override it (no per-tick closure/no-op virtual calls).
            policyManager.TickBehaviors(this);
            laborerCooldown.TickCheckReady();
        }

        /// <summary>Per-tick sweep of offensive battle maps. Defense maps stay comp-ticked
        /// (WorldObjectComp_SettlementMilitary.CompTick); offense maps have no comp, so they are
        /// ticked here. Iterates only isOffense contexts and early-exits instantly when none exist.
        /// The manager is otherwise event-driven (FCEventMaker.ProcessEvents), so this is a new loop.</summary>
        private void OffenseBattlefieldTick()
        {
            MilitaryOperationManager mgr = FindFC.MilitaryManager;
            if (mgr?.battlefields is null || mgr.battlefields.Count == 0) return;
            // Snapshot the offense contexts before ticking: OffenseTick can tear down and mutate the
            // battlefields dict on resolution.
            List<BattlefieldContext> contexts = null;
            foreach (BattlefieldContext ctx in mgr.battlefields.Values)
            {
                if (ctx is null || !ctx.isOffense) continue;
                if (contexts is null) contexts = new List<BattlefieldContext>();
                contexts.Add(ctx);
            }
            if (contexts is null) return;
            foreach (BattlefieldContext ctx in contexts) ctx.OffenseTick();
        }

        #endregion

        #region Lazy Caches

        /* Faction-level cache cascade. Settlement-level cascade is documented in
         * WorldSettlementFC's "Lazy Cache Invalidation" region.
         *
         *   RebuildBehaviorCache              --> InvalidateFactionStatCache
         *
         *   InvalidateFactionStatCache        --> clear cachedFactionStatValues
         *                                     --> foreach settlement: InvalidateDescCache
         *                                                          + InvalidateResourceCaches
         *
         *   InvalidateAllSettlementStatCaches --> foreach settlement: InvalidateStatCache
         *                                        (full per-settlement cascade, heavier than
         *                                        InvalidateFactionStatCache)
         *
         *   DirtyFactionProfitCache           --> flag only
         *   DirtyAveragesCache                --> flag only (set by happiness/loyalty/
         *                                        unrest/prosperity setters)
         *   DirtyTechLevelCache               --> flag only
         */

        public void DirtyFactionProfitCache()
        {
            dirtyFactionProfitCache = true;
        }

        private void RecomputeTotalProfit()
        {
            _income = settlements.Sum(s => s.totalIncome);
            _upkeep = settlements.Sum(s => s.totalUpkeep) + policyManager.GetEdictUpkeep();
            _profit = _income - _upkeep;
            dirtyFactionProfitCache = false;
        }

        public void DirtyAveragesCache()
        {
            dirtyAveragesCache = true;
        }

        private void RecomputeAverages()
        {
            double avgHappiness = 0;
            double avgLoyalty = 0;
            double avgUnrest = 0;
            double avgProsperity = 0;

            if (settlements.Count > 0)
            {
                foreach (WorldSettlementFC settlement in settlements)
                {
                    if (settlement is null) continue;
                    avgHappiness += settlement.happiness;
                    avgLoyalty += settlement.loyalty;
                    avgUnrest += settlement.unrest;
                    avgProsperity += settlement.prosperity;
                }

                avgHappiness /= settlements.Count;
                avgLoyalty /= settlements.Count;
                avgUnrest /= settlements.Count;
                avgProsperity /= settlements.Count;
            }

            _averageHappiness = avgHappiness;
            _averageLoyalty = avgLoyalty;
            _averageUnrest = avgUnrest;
            _averageProsperity = avgProsperity;
            dirtyAveragesCache = false;
        }

        public void DirtyTechLevelCache()
        {
            dirtyTechLevelCache = true;
        }

        private static readonly TechLevel[] TechLevelDescending =
        {
            TechLevel.Archotech,
            TechLevel.Ultra,
            TechLevel.Spacer,
            TechLevel.Industrial,
            TechLevel.Medieval,
            TechLevel.Neolithic,
        };

        private void RecomputeTechLevel()
        {
            TechLevel curTechLevel = _techLevel;
            bool medievalOnly = FCSettings.medievalTechOnly;
            TechLevel newLevel;
            TechLevel playerTech = FindFC.PlayerFaction?.def?.techLevel ?? TechLevel.Neolithic;

            if (FCSettings.mirrorPlayerTechLevel)
            {
                // Mirror mode: pin Empire tech to the player faction's tech level.
                if (playerTech < TechLevel.Neolithic) playerTech = TechLevel.Neolithic;
                newLevel = playerTech;
                LogUtil.Message("updateTechLevel: Mirroring player tech " + newLevel);
            }
            else
            {
                // Research-barrier cascade: the highest satisfied barrier wins.
                ResearchManager researchManager = Find.ResearchManager;
                newLevel = TechLevel.Undefined;
                foreach (TechLevel tl in TechLevelDescending)
                {
                    if (medievalOnly && tl > TechLevel.Medieval) continue;
                    TechLevelBarrier barrier = FactionCache.GetTechBarrier(tl);
                    if (barrier is null) continue;
                    if (barrier.IsSatisfied(researchManager))
                    {
                        newLevel = tl;
                        LogUtil.Message("updateTechLevel: " + tl);
                        break;
                    }
                }
                // Safety floor if no barriers matched at all.
                if (newLevel == TechLevel.Undefined) newLevel = TechLevel.Neolithic;

                // Floor: Empire tech level should never be below the player faction's tech level.
                if (medievalOnly && playerTech > TechLevel.Medieval) playerTech = TechLevel.Medieval;
                if (playerTech > TechLevel.Undefined && newLevel < playerTech)
                {
                    newLevel = playerTech;
                    LogUtil.Message("updateTechLevel: Matched player faction tech level " + playerTech);
                }
            }

            // medievalTechOnly cap applies to both mirror and cascade paths.
            if (medievalOnly && newLevel > TechLevel.Medieval) newLevel = TechLevel.Medieval;

            // Never downgrade the faction's tech level.
            if (newLevel > _techLevel) _techLevel = newLevel;

            if (_techLevel != curTechLevel)
            {
                xenotypeFilter.FinalizeInit(this);
                DirtyAllTitheCaches();
            }

            Faction playerColonyfaction = FindFC.EmpireFaction;
            bool techLevelChanged = playerColonyfaction?.def.techLevel < _techLevel;
            if (techLevelChanged)
            {
                LogUtil.Message("Updating Tech Level");
                UpdateFactionDef(_techLevel, ref playerColonyfaction);
            }

            dirtyTechLevelCache = false;

            // Refresh caravan trader kinds after a tech-level bump (must run after
            // dirtyTechLevelCache is cleared to avoid re-entering RecomputeTechLevel
            // through the techLevel property).
            if (techLevelChanged)
                RebuildCaravanTraderKinds();
        }

        public void DirtyAllTitheCaches()
        {
            foreach (WorldSettlementFC settlement in settlements)
            {
                foreach (ResourceFC resource in settlement.Resources)
                {
                    resource.SetDirtyRandomTitheCache();
                }
            }
        }

        /// <summary>
        /// Returns a list of *all* things that this faction can produce.
        /// </summary>
        public List<ThingDef> GetGrandThingList()
        {
            if (DirtyGrandThingListFlag)
            {
                grandThingList = new List<ThingDef>();
                foreach (WorldSettlementFC settlement in settlements)
                {
                    if (settlement is null) continue;
                    grandThingList.AddRange(settlement.GetGrandThingList());
                }
                grandThingList = grandThingList.Distinct().ToList();
                DirtyGrandThingListFlag = false;
            }
            return grandThingList;
        }

        public void DirtyGrandThingList()
        {
            DirtyGrandThingListFlag = true;
        }

        public List<ThingDef> GetStuffListForThingDef(ThingDef thing)
        {
            return CraftUtil.GetThingStuffs(thing, GetGrandThingList());
        }

        #endregion

        #region Stat System

        /// <summary>
        /// Entry point for stat queries. Combines settlement-level and faction-level cached partials,
        /// then applies uncached behavior ModifyStat adjustments.
        /// </summary>
        public double GetStatValue(FCStatDef stat, WorldSettlementFC settlement = null,
                                   MercenarySquadFC squad = null, Mercenary unit = null)
        {
            double value = GetFactionStatValue(stat);

            // Scope chain: faction (cached) -> settlement (cached) -> squad-instance -> unit-instance.
            // Each scope folds in only if the stat opts into it and context is supplied.
            if (settlement != null && stat.appliesToSettlements)
                value = CombineScoped(value, settlement.GetSettlementStatValue(stat), stat);
            if (squad != null && stat.appliesToSquads)
                value = CombineScoped(value, AccumulatePermanentModifiers(stat.IdentityValue, stat, squad.statModifiers), stat);
            if (unit != null && stat.appliesToUnits)
                value = CombineScoped(value, AccumulatePermanentModifiers(stat.IdentityValue, stat, unit.statModifiers), stat);

            // Apply runtime-dependent behavior modifiers (uncached — may depend on settlement state)
            foreach (FCPolicyBehavior b in policyManager.CachedBehaviors)
            {
                try
                {
                    value = b.ModifyStat(stat, value, settlement);
                }
                catch (Exception e)
                {
                    LogUtil.Error($"Behavior ModifyStat error for stat '{stat.defName}': {e}");
                }
            }

            return value;
        }

        /// <summary>Combines two scope values per the stat's aggregation (sum or product).</summary>
        private static double CombineScoped(double a, double b, FCStatDef stat) =>
            stat.aggregation == FCStatAggregation.Additive ? a + b : a * b;

        /// <summary>Folds a per-entity PermanentStatModifier list (squad/unit scope) into a running value.</summary>
        private double AccumulatePermanentModifiers(double value, FCStatDef stat, List<PermanentStatModifier> mods)
        {
            if (mods == null) return value;
            foreach (PermanentStatModifier mod in mods)
                if (mod.stat == stat)
                    value = CombineScoped(value, mod.value, stat);
            return value;
        }

        /// <summary>
        /// The squad-instance contribution to a stat (squad scope only), relative to identity — i.e. just
        /// this squad's own statModifiers, not the faction/settlement parts. Returns IdentityValue (0 additive /
        /// 1 multiplicative) when the squad is null or the stat does not opt into squad scope. Use this to layer
        /// a squad-scoped delta onto a value that already carries the faction/settlement contribution elsewhere.
        /// </summary>
        public double GetSquadStatValue(FCStatDef stat, MercenarySquadFC squad) =>
            (squad != null && stat.appliesToSquads)
                ? AccumulatePermanentModifiers(stat.IdentityValue, stat, squad.statModifiers)
                : stat.IdentityValue;

        /// <summary>
        /// The unit-instance contribution to a stat (unit scope only), relative to identity — just this
        /// mercenary's own statModifiers. Returns IdentityValue when the unit is null or the stat does not opt
        /// into unit scope.
        /// </summary>
        public double GetUnitStatValue(FCStatDef stat, Mercenary unit) =>
            (unit != null && stat.appliesToUnits)
                ? AccumulatePermanentModifiers(stat.IdentityValue, stat, unit.statModifiers)
                : stat.IdentityValue;

        private double AccumulateStatModifiersValue(double value, FCStatDef stat, List<FCStatModifier> statModifiers)
        {
            foreach (FCStatModifier mod in statModifiers)
            {
                bool isAdditive = stat.aggregation == FCStatAggregation.Additive;
                if (mod.stat == stat)
                {
                    if (isAdditive)
                        value += mod.value;
                    else
                        value *= mod.value;
                }
            }

            return value;
        }

        /// <summary>
        /// Computes and caches the faction-level stat partial (policies, traits, edicts, and faction-wide events).
        /// Starts from stat.IdentityValue, applies only faction-level modifiers.
        /// </summary>
        public double GetFactionStatValue(FCStatDef stat)
        {
            if (cachedFactionStatValues.TryGetValue(stat, out double cached))
                return cached;

            double value = stat.IdentityValue;

            foreach (FCPolicy p in policyManager.policies)
            {
                if (p?.def is null) continue;
                value = AccumulateStatModifiersValue(value, stat, p.def.statModifiers);
            }
            foreach (FCPolicy p in policyManager.factionTraits)
            {
                if (p?.def is null || p.def == FCPolicyDefOf.empty) continue;
                value = AccumulateStatModifiersValue(value, stat, p.def.statModifiers);
            }
            foreach (FCPolicy edict in policyManager.edicts.Values)
            {
                if (edict?.def is null || !edict.IsFullyActive) continue;
                value = AccumulateStatModifiersValue(value, stat, edict.def.statModifiers);
            }
            foreach (FCEvent evt in Events)
            {
                if (evt?.def is null) continue;
                if (evt.settlementTraitLocations.Count > 0) continue;
                value = AccumulateStatModifiersValue(value, stat, evt.def.statModifiers);
            }

            cachedFactionStatValues[stat] = value;
            return value;
        }
        private string AccumulateStatModifiersDesc(string desc, FCStatDef stat, List<FCStatModifier> statModifiers, string label, bool hardinvert = false)
        {
            bool isAdditive = stat.aggregation == FCStatAggregation.Additive;
            bool invert = stat.invertedForDisplay;
            // hardinvert flips the displayed sign, so the color test must flip with it to
            // keep "harmful modifier = red". Additive-only; multiplier lines aren't sign-flipped.
            bool colorInvert = invert ^ hardinvert;
            foreach (FCStatModifier mod in statModifiers)
            {
                if (mod.stat != stat) continue;
                if (isAdditive)
                    desc += $"{TextUtil.ColorizeAdditiveBonus(mod.value, invert: colorInvert, hardinvert: hardinvert)} - {label}\n";
                else
                    desc += $"{TextUtil.ColorizeMultiplierBonus(mod.value, invert: invert)} - {label}\n";
            }

            return desc;
        }

        /// <summary>
        /// Builds a description string for faction-level stat contributions (policies, traits, edicts, and faction-wide events).
        /// Not cached — only used for UI tooltips.
        /// </summary>
        public string GetFactionStatDesc(FCStatDef stat, bool hardinvert = false)
        {
            string desc = "";
            bool isAdditive = stat.aggregation == FCStatAggregation.Additive;
            bool invert = stat.invertedForDisplay;

            foreach (FCPolicy p in policyManager.policies)
            {
                if (p?.def is null) continue;
                desc = AccumulateStatModifiersDesc(desc, stat, p.def.statModifiers, p.def.LabelCap, hardinvert);
            }
            foreach (FCPolicy p in policyManager.factionTraits)
            {
                if (p?.def is null || p.def == FCPolicyDefOf.empty) continue;
                desc = AccumulateStatModifiersDesc(desc, stat, p.def.statModifiers, p.def.LabelCap, hardinvert);
            }
            foreach (FCPolicy edict in policyManager.edicts.Values)
            {
                if (edict?.def is null || !edict.IsFullyActive) continue;
                desc = AccumulateStatModifiersDesc(desc, stat, edict.def.statModifiers, $"{edict.def.LabelCap} ({"FCEdict".Translate()})", hardinvert);
            }
            foreach (FCEvent evt in Events)
            {
                if (evt?.def is null) continue;
                if (evt.settlementTraitLocations.Count > 0) continue;
                desc = AccumulateStatModifiersDesc(desc, stat, evt.def.statModifiers, $"{evt.def.LabelCap} ({"FCEvent".Translate()})", hardinvert);
            }

            return desc;
        }

        /// <summary>
        /// Clears the faction-level stat cache and dirties resource/desc caches on all settlements
        /// (since final combined stat values have changed).
        /// Does NOT clear settlement stat value caches — settlement-level modifiers are unaffected.
        /// </summary>
        public void InvalidateFactionStatCache()
        {
            cachedFactionStatValues.Clear();
            ScrubNullSettlements("InvalidateFactionStatCache");
            foreach (WorldSettlementFC s in settlements)
            {
                s.InvalidateDescCache();
                s.InvalidateResourceCaches();
            }
        }

        /// <summary>
        /// Invalidates stat and resource caches on all settlements.
        /// Called after faction-wide events (e.g., research completion) that may affect
        /// settlement-level stat or resource production providers.
        /// </summary>
        public void InvalidateAllSettlementStatCaches()
        {
            ScrubNullSettlements("InvalidateAllSettlementStatCaches");
            foreach (WorldSettlementFC s in settlements)
                s.InvalidateStatCache();
        }

        #endregion

        /* Policy/edict/trait/behavior state and methods moved to PolicyManager.
         * Access via FindFC.PolicyManager or this.policyManager. */

        #region Cross-System Check Aggregators

        /* Aggregator checks that combine contributions from every relevant system. */

        /// <summary>
        /// True if all contributing systems permit this action.
        /// </summary>
        public bool IsActionAllowed(FCActionType action)
        {
            if (!policyManager.IsActionAllowed(action)) return false;
            return true;
        }

        /// <summary>
        /// True if all contributing systems permit this military job.
        /// </summary>
        public bool IsMilitaryJobAllowed(MilitaryJobDef job)
        {
            if (!policyManager.IsMilitaryJobAllowed(job)) return false;
            return true;
        }

        /// <summary>
        /// True if any contributing system prevents building destruction on battle loss.
        /// </summary>
        public bool IsBuildingDestructionPrevented()
        {
            if (policyManager.AnyPolicyPreventsBuildingDestruction()) return true;
            return false;
        }

        /// <summary>
        /// True if any contributing system suppresses the member-death penalty.
        /// </summary>
        public bool IsMemberDeathPenaltySuppressed()
        {
            if (policyManager.AnyPolicySuppressesMemberDeathPenalty()) return true;
            return false;
        }

        #endregion

        #region Event Manager Passthroughs

        public void RecordEventCooldown(FCEventDef def) => eventManager.RecordCooldown(def);
        public bool IsEventOnCooldown(FCEventDef def) => eventManager.IsOnCooldown(def);
        public void RecordEventFired(FCEventDef def) => eventManager.RecordFired(def);
        public bool HasReachedMaxFireCount(FCEventDef def) => eventManager.HasReachedMaxFireCount(def);

        #endregion

        #region Lifecycle Dispatch

        // Bridges registry dispatch to policy behaviors so ColonyUtil only needs one call path.

        void ISettlementListener.OnSettlementCreated(WorldSettlementFC settlement)
        {
            policyManager.ForEachBehavior(b => b.OnSettlementCreated(this, settlement));
        }

        void ISettlementListener.OnSettlementRemoved(WorldSettlementFC settlement)
        {
            policyManager.ForEachBehavior(b => b.OnSettlementRemoved(this, settlement));
        }

        void ISettlementListener.OnSettlementUpgraded(WorldSettlementFC settlement, int oldLevel, int newLevel)
        {
            policyManager.ForEachBehavior(b => b.OnSettlementUpgraded(this, settlement, newLevel));
        }

        void ISettlementListener.OnSettlementTypeChanged(WorldSettlementFC settlement, WorldSettlementDef oldDef, WorldSettlementDef newDef)
        {
            policyManager.ForEachBehavior(b => b.OnSettlementTypeChanged(this, settlement, oldDef, newDef));
        }

        void ISettlementListener.OnBuildingConstructed(WorldSettlementFC settlement, BuildingFCDef building, int slot)
        {
            policyManager.ForEachBehavior(b => b.OnBuildingConstructed(this, settlement, building, slot));
        }

        void ISettlementListener.OnBuildingDeconstructed(WorldSettlementFC settlement, BuildingFCDef building, int slot)
        {
            policyManager.ForEachBehavior(b => b.OnBuildingDeconstructed(this, settlement, building, slot));
        }

        void IResearchListener.OnResearchCompleted(ResearchProjectDef project)
        {
            policyManager.ForEachBehavior(b => b.OnResearchCompleted(this, project));
        }

        void IMercenarySquadListener.OnMercenaryDeath(MercenaryDeathEvent evt)
        {
            // No policy behavior hook for merc death currently — submods handle this via their own listener
        }

        /* -*-*-*-*- Military hooks -*-*-*-*-
         * The comp's military-related properties (militaryBusy / militaryJob / militaryLocation /
         * militaryEnemy / isUnderAttack) are read-only and derived from MilitaryOperationManager's
         * op indices, so these hooks have no comp-side state to update. They just dispatch to
         * policy behaviors and run squad injury bookkeeping.
         */

        void IMilitaryOperationListener.OnOperationCreated(MilitaryOperation op)
        {
            if (op is null) return;

            // Fire on both sides when distinct: a foreign-defender op commits two settlements
            // (aggressor's home and defender's home), and listeners that track per-settlement
            // commitment need both notifications.
            WorldSettlementFC aggressorHome = op.aggressor?.homeSettlement;
            WorldSettlementFC defenderHome = op.defender?.homeSettlement;
            if (aggressorHome is object)
            {
                bool isExtra = op.aggressor?.squad?.isExtraSquad ?? false;
                policyManager.ForEachBehavior(b => b.OnSquadDeployed(this, op, aggressorHome, isExtra));
            }
            if (defenderHome is object && defenderHome != aggressorHome)
            {
                bool isExtra = op.defender?.squad?.isExtraSquad ?? false;
                policyManager.ForEachBehavior(b => b.OnSquadDeployed(this, op, defenderHome, isExtra));
            }
        }

        void IMilitaryOperationListener.OnOperationResolved(MilitaryOperation op)
        {
            if (op is null) return;

            // Squad injuries are registered earlier, in op.CompleteBattle, so OnBattleResolved
            // listeners observe the post-battle injury counts.

            // Symmetric with OnOperationCreated: recall both sides when they're distinct settlements.
            WorldSettlementFC aggressorHome = op.aggressor?.homeSettlement;
            WorldSettlementFC defenderHome = op.defender?.homeSettlement;
            if (aggressorHome is object)
                policyManager.ForEachBehavior(b => b.OnSquadRecalled(this, op, aggressorHome));
            if (defenderHome is object && defenderHome != aggressorHome)
                policyManager.ForEachBehavior(b => b.OnSquadRecalled(this, op, defenderHome));
        }

        void IMilitaryOperationListener.OnBattleResolved(MilitaryOperation op, bool victory, BattleResult result)
        {
            if (op is null) return;
            WorldSettlementFC settlement = op.aggressor?.homeSettlement ?? op.defender?.homeSettlement;
            if (settlement is null) return;

            policyManager.ForEachBehavior(b => b.OnBattleResolved(this, settlement, op.kind, victory, result));
        }

        void IMercenarySquadListener.OnSquadHired(MercenarySquadFC squad)
        {
            if (squad is null) return;
            policyManager.ForEachBehavior(b => b.OnSquadHired(this, squad));
        }

        void IMercenarySquadListener.OnSquadDismissed(MercenarySquadFC squad)
        {
            if (squad is null) return;
            policyManager.ForEachBehavior(b => b.OnSquadDismissed(this, squad));
        }

        void IMercenarySquadListener.OnSquadUpgraded(MercenarySquadFC squad)
        {
            if (squad is null) return;
            MilitaryFC.NotifyIfUnderfunded(squad);
            policyManager.ForEachBehavior(b => b.OnSquadUpgraded(this, squad));
        }

        #endregion

        #region Settlement Management

        public void AddSettlement(WorldSettlementFC settlement)
        {
            settlements.Add(settlement);
            DirtyFactionProfitCache();
            DirtyAveragesCache();
        }

        public WorldSettlementFC ReturnSettlementByLocation(PlanetTile location)
        {
            foreach (WorldSettlementFC settlement in settlements)
            {
                if (settlement.Tile == location)
                    return settlement;
            }

            return null;
        }

        public string GetSettlementName(PlanetTile location)
        {
            return ReturnSettlementByLocation(location)?.Name ?? "Null";
        }

        public void UpdateSettlementStats()
        {
            foreach (WorldSettlementFC settlement in settlements)
            {
                settlement.UpdateHappiness();
                settlement.UpdateLoyalty();
                settlement.UpdateUnrest();
                settlement.UpdateProsperity();
                // Advance decaying pawn/caravan-loss penalties AFTER this day's slice has been applied above.
                settlement.TickDecayingPenalties();
            }
        }

        public int ReturnHighestMilitaryLevel()
        {
            int max = 1;
            foreach (WorldSettlementFC settlement in settlements)
            {
                max = Math.Max(max, settlement.settlementMilitaryLevel);
            }

            return max;
        }

        #endregion

        #region Tax & Billing

        // AddTax moved to TaxLedger. WorldComponentTick now calls
        // taxLedger.TaxTick(this, faction) directly. Aggregate income/upkeep/profit
        // accessors remain here because they read from per-settlement caches.
        // Prisoner daily health updates live on WorldObjectComp_SettlementPrisoners.CompTick.

        public double GetTotalIncome() => income;
        public double GetTotalUpkeep() => upkeep;
        public double GetTotalProfit() => profit;

        #endregion

        #region Events

        // AddEvent moved to FCEventManager.AddEvent. Call faction.eventManager.AddEvent(evt)
        // directly. The manager owns goods consolidation, tax delivery interception, the
        // raw queue append, and the FCEventHandlerExtension OnEventQueued dispatch.

        // Thin delegators to FCEventManager. Invariants (fired flag, version bump)
        // are enforced by the manager; see FCEventManager.Remove / RemoveWhere.
        public bool RemoveEvent(FCEvent evt) => eventManager.Remove(evt);
        public int RemoveEventsWhere(Predicate<FCEvent> match) => eventManager.RemoveWhere(match);

        // Indexed event queries — O(1) via FCEventManager's internal indexes.
        public IReadOnlyList<FCEvent> GetEventsByDef(FCEventDef def) => eventManager.GetByDef(def);
        public FCEvent FindEventByDefAndLocation(FCEventDef def, PlanetTile tile) => eventManager.FindFirstByDefAndLocation(def, tile);
        public IReadOnlyList<FCEvent> FindAllEventsByDefAndLocation(FCEventDef def, PlanetTile tile) => eventManager.GetByDefAndLocation(def, tile);
        public bool HasEventWithDefAndLocation(FCEventDef def, PlanetTile tile) => eventManager.AnyWithDefAndLocation(def, tile);

        private void MakeRandomEvent()
        {
            if (RandomEventsDisabledOrNoSettlements()) return;

            randomEventLastAdded += 1f;

            // Bar new roots while a multi-step chain resolves; supersedes the frequency setting.
            // Hold the timer at 0 so a fresh min/max wait applies once the chain clears.
            if (FCSettings.blockEventsDuringChain && FCEventMaker.IsRandomChainInProgress(this))
            {
                randomEventLastAdded = 0f;
                return;
            }

            if (CanMakeRandomEventNow())
            {
                FCEvent tmpEvt = FCEventMaker.MakeRandomEvent(FCEventMaker.ReturnRandomEvent(), null);
                if (tmpEvt != null)
                {
                    eventManager.AddEvent(tmpEvt);
                    randomEventLastAdded = 0f;

                    Find.LetterStack.ReceiveLetter("FCRandomEventLetterLabel".Translate(), FCEventMaker.BuildEventLetterBody(tmpEvt), LetterDefOf.NeutralEvent);
                }
            }
        }

        private bool CanMakeRandomEventNow()
        {
            if ((FCSettings.maxDaysTillRandomEvent - FCSettings.minDaysTillRandomEvent) == 0)
            {
                return randomEventLastAdded >= FCSettings.minDaysTillRandomEvent;
            }
            else
            {
                return Rand.Chance((randomEventLastAdded - FCSettings.minDaysTillRandomEvent) / (FCSettings.maxDaysTillRandomEvent - FCSettings.minDaysTillRandomEvent));
            }
        }

        private bool RandomEventsDisabledOrNoSettlements() => settlements.Count == 0 || FCSettings.disableRandomEvents;

        #endregion

        #region Resources

        private void EnsureResourcePools()
        {
            // Protection against save corruption. Though if resourcePools has null fields on a load, then there are likely
            // other, bigger problems hiding elsewhere...
            int numNull = resourcePools.RemoveAll(p => p is null);
            if (numNull > 0)
            {
                LogUtil.Warning($"[EnsureResourePools] Removed {numNull} null items from resourcePools");
            }
            foreach (ResourceTypeDef def in FactionCache.PoolResourceTypeDefs)
            {
                if (!resourcePools.Any(p => p.resource == def))
                {
                    resourcePools.Add(new ResourcePool { resource = def, pool = 0 });
                }
            }
        }

        public void AddResourcePool(ResourcePool pool)
        {
            if (pool is null || pool.pool == 0) return;

            /* If the pool wants to do any pre-adding-to-global-pool shenanigans, let it do so now. */
            pool.pool = pool.resource.PreAddToGlobalPool(pool.pool);

            ResourcePool rpool = resourcePools.Find((ResourcePool p) => p.resource == pool.resource);
            if (rpool != null)
            {
                rpool.pool += pool.pool;
            }
            else
            {
                resourcePools.Add(pool);
            }
            pool.resource.AddedToGlobalPool(pool.pool);
        }
        public void AddResourcePools(List<ResourcePool> pools)
        {
            foreach (ResourcePool pool in pools)
            {
                AddResourcePool(pool);
            }
        }
        public double GetResourcePoolValue(ResourceTypeDef res)
        {
            ResourcePool rpool = resourcePools.Find((ResourcePool p) => p.resource == res);
            if (rpool is null)
            {
                LogUtil.Warning($"Tried to get resource pool value for ResourceTypeDef {res}, but there was no faction resource pool");
                return 0;
            }
            return rpool.pool;
        }

        public IEnumerable<FloatMenuOption> GetFactionMenuResourcePoolFloatMenuOptions()
        {
            foreach (ResourcePool pool in resourcePools)
            {
                IEnumerable<FloatMenuOption> options = pool.resource.GetFactionMenuFloatMenuOptions(pool);
                if (options != null)
                {
                    foreach (FloatMenuOption option in options)
                    {
                        yield return option;
                    }
                }
            }
        }
        private void AccumulateDailyProduction()
        {
            foreach (WorldSettlementFC settlement in settlements)
            {
                settlement.AccumulateDailyProduction();
            }
            // After every settlement has accrued and deposits have landed, run daily consumers.
            DailyAccrualRegistry.InvokePostDailyAccrual(this);
        }

        public void UpdateDailyResourcePools()
        {
            foreach (ResourcePool pool in resourcePools)
            {
                LogUtil.Message($"Daily ResourcePool update for resourceTypeDef {pool.resource.defName}. Pool size: {pool.pool}");
                pool.resource.DailyUpdate(pool);
                LogUtil.Message($"Post-Daily ResourcePool update for resourceTypeDef {pool.resource.defName}. New Pool size: {pool.pool}");
            }
        }

        public ResourceDisplay ReturnResource(ResourceTypeDef resourceTypeDef)
        {
            ResourceDisplay res = factionResources.Find((ResourceDisplay rfc) => rfc.resourceDef == resourceTypeDef);
            if (res == null)
            {
                /* This should never happen! */
                LogUtil.Error($"Requested resource {resourceTypeDef.defName} is not in the list of faction resources!");
            }
            return res;
        }

        public void SetDirtyResourceDisplayCache(ResourceTypeDef rdef)
        {
            ResourceDisplay rdisplay = factionResources.Find((ResourceDisplay rd) => rd.resourceDef == rdef);
            if (rdisplay != null)
            {
                rdisplay.SetDirtyCache();
            }
        }

        public void SetAllDirtyResourceDisplayCaches()
        {
            foreach (ResourceDisplay rdis in factionResources)
            {
                rdis.SetDirtyCache();
            }
        }

        #endregion

        #region ID Generation

        public int GetNextPrisonerID()
        {
            nextPrisonerID++;
            return nextPrisonerID;
        }

        #endregion

        #region Leveling

        public float UpdateFactionLevelGoalXP(int currentLevel)
        {
            return SettlementFormulas.CalculateFactionLevelGoalXP(currentLevel);
        }

        public bool AddExperienceToFactionLevel(float xp)
        {
            if (factionLevel >= MaxFactionLevel)
            {
                factionXPCurrent = factionXPGoal;
                return false;
            }

            bool leveled = false;
            factionXPCurrent += xp;

            while (factionXPCurrent >= factionXPGoal && factionLevel < MaxFactionLevel)
            {
                factionXPCurrent -= factionXPGoal;
                factionLevel += 1;
                LogUtil.Message($"Faction leveled up to {factionLevel} at tick {Find.TickManager.TicksGame} (gained {xp} XP)");
                Find.LetterStack.ReceiveLetter("FCFactionLevelUp".Translate(),
                    "FCFactionLevelUpDesc".Translate(name, factionLevel), LetterDefOf.PositiveEvent);
                leveled = true;
                factionXPGoal = UpdateFactionLevelGoalXP(factionLevel);
            }

            if (factionLevel >= MaxFactionLevel)
            {
                factionXPCurrent = factionXPGoal;
            }

            return leveled;
        }

        #endregion

        #region Capital Management

        public void SetCapital()
        {
            // Check if there's an active capital spot first
            Building_CapitalSpot activeCapitalSpot = GetActiveCapitalSpot();
            if (activeCapitalSpot != null)
            {
                Messages.Message("FCCapitalAlreadyEstablished".Translate(activeCapitalSpot.Map.Parent.LabelCap, FindFC.EmpireTitle.CapitalizeFirst()), MessageTypeDefOf.RejectInput);
                return;
            }

            if (Find.CurrentMap != null && Find.CurrentMap.IsPlayerHome)
            {
                capitalLocation = Find.CurrentMap.Parent.Tile;

                Messages.Message("FCSetAsFactionCapital".Translate(Find.CurrentMap.Parent.LabelCap), MessageTypeDefOf.NeutralEvent);
            }
            else
            {
                Messages.Message("FCUnableToSetCapitalHere".Translate(), MessageTypeDefOf.NegativeEvent);
            }
        }

        public bool HasActiveCapitalSpot()
        {
            return !(GetActiveCapitalSpot() is null);
        }

        public Building_CapitalSpot GetActiveCapitalSpot()
        {
            foreach (Map map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;

                foreach (Building building in map.listerBuildings.allBuildingsColonist)
                {
                    if (building is Building_CapitalSpot capitalSpot && capitalSpot.IsActiveCapitalSpot)
                    {
                        return capitalSpot;
                    }
                }
            }
            return null;
        }

        public Map ReturnCapitalMap()
        {
            for (int i = 0; i < Find.Maps.Count; i++)
            {
                if (Find.Maps[i].Tile == capitalLocation)
                {
                    return Find.Maps[i];
                }
            }

            LogUtil.Message("FCCouldNotFindMapOfCapital".Translate());
            return null;
        }

        #endregion

        #region Faction Definition

        public Color ResolveApparelColor(SavedThing savedThing)
        {
            if (savedThing.hasColor)
                return savedThing.color;
            return ResolveApparelColor(savedThing.thing);
        }

        public Color ResolveApparelColor(ThingDef apparelDef)
        {
            // Primary = outer/armor layers (Middle, Shell)
            // Secondary = base clothing + accessories (OnSkin, Belt, Overhead, EyeCover)
            bool isPrimarySlot = apparelDef != null
                && apparelDef.IsApparel
                && (apparelDef.apparel.layers.Contains(ApparelLayerDefOf.Middle)
                    || apparelDef.apparel.layers.Contains(ApparelLayerDefOf.Shell));

            if (isPrimarySlot)
            {
                if (hasFactionColor) return factionColorPrimary;
            }
            else
            {
                if (hasFactionColorSecondary) return factionColorSecondary;
                if (hasFactionColor) return factionColorPrimary;
            }

            return Color.white;
        }

        public void UpdateFactionIcon(ref Faction faction, string iconPath)
        {
            LogUtil.Message("Updated Icon - " + iconPath);
            if (faction?.def != null)
            {
                faction.def.factionIconPath = iconPath;
                factionDefIconCache?.SetValue(faction.def, null);
            }
            if (settlements.Any() && settlements[0]?.def != null && UnityData.IsInMainThread)
            {
                WorldSettlementFC.traitCachedIcon.SetValue(settlements[0].def, ContentFinder<Texture2D>.Get(iconPath));
            }

            foreach (WorldSettlementFC settlement in settlements)
            {
                if (settlement?.def != null)
                {
                    settlement.def.expandingIconTexture = iconPath;
                }
                if (settlement?.Faction?.def != null)
                {
                    settlement.Faction.def.factionIconPath = iconPath;
                    factionDefIconCache?.SetValue(settlement.Faction.def, null);
                }
            }
        }

        public void UpdateFactionDef(TechLevel tech, ref Faction faction)
        {
            FactionDef replacingDef;
            ThingFilter apparelStuffFilter = new ThingFilter();
            FactionDef def = faction.def;

            switch (tech)
            {
                case TechLevel.Archotech:
                case TechLevel.Ultra:
                case TechLevel.Spacer:
                    replacingDef = DefDatabase<FactionDef>.GetNamedSilentFail("OutlanderCivil");
                    break;
                case TechLevel.Industrial:
                    replacingDef = DefDatabase<FactionDef>.GetNamedSilentFail("OutlanderCivil");
                    break;
                case TechLevel.Medieval:
                    if (FCSettings.IsModLoaded("OskarPotocki.VanillaFactionsExpanded.MedievalModule"))
                    {
                        replacingDef = DefDatabase<FactionDef>.GetNamedSilentFail("VFEM_KingdomCivil");
                    }
                    else
                    {
                        replacingDef = DefDatabase<FactionDef>.GetNamedSilentFail("TribeCivil");
                    }

                    break;
                default:
                    replacingDef = DefDatabase<FactionDef>.GetNamedSilentFail("TribeCivil");
                    break;
            }
            if (replacingDef.backstoryFilters != null && replacingDef.backstoryFilters.Count != 0)
                def.backstoryFilters = replacingDef.backstoryFilters;
            def.techLevel = tech;
            def.basicMemberKind = replacingDef.basicMemberKind;
            if (replacingDef.apparelStuffFilter != null)
                def.apparelStuffFilter = replacingDef.apparelStuffFilter;


            if (tech >= TechLevel.Spacer && def.apparelStuffFilter != null)
            {
                def.apparelStuffFilter.SetAllow(DefDatabase<StuffCategoryDef>.GetNamedSilentFail("Synthread"), true);
                def.apparelStuffFilter.SetAllow(DefDatabase<StuffCategoryDef>.GetNamedSilentFail("Hyperweave"), true);
                def.apparelStuffFilter.SetAllow(DefDatabase<StuffCategoryDef>.GetNamedSilentFail("Plasteel"), true);
            }
            UpdateFactionIcon(ref faction, "FactionIcons/" + factionIconPath);

            LogUtil.Message("FactionFC.UpdateFactionDef - Completed tech update");
        }

        private void InitEnabledCaravanTypes()
        {
            enabledCaravanTypes = new List<string>();
            foreach (ResourceTypeDef rtd in FactionCache.TitheableResourceTypeDefs)
            {
                if (rtd.ResourceTypeAllowedByTech(_techLevel))
                    enabledCaravanTypes.Add(rtd.defName);
            }
        }

        /// <summary>
        /// Rebuilds <c>faction.def.caravanTraderKinds</c> from current state. If the build returns
        /// an empty list (e.g., 0 production across all enabled resource types, or called too early
        /// during load before production caches are settled), the existing list is left intact so
        /// the FactionDef's XML default isn't clobbered with an empty list.
        /// </summary>
        public void RebuildCaravanTraderKinds()
        {
            Faction faction = FindFC.EmpireFaction;
            if (faction is null) return;
            List<TraderKindDef> result = BuildCaravanTraderKinds(techLevel);
            if (result.Count > 0)
                faction.def.caravanTraderKinds = result;

            if (result.Count == 0)
                LogUtil.Warning($"RebuildCaravanTraderKinds produced an empty list. enabledCaravanTypes: {enabledCaravanTypes?.Count ?? 0}");
            else
                LogUtil.Message($"RebuildCaravanTraderKinds produced a list of {enabledCaravanTypes?.Count ?? 0} caravan types");
        }

        /// <summary>
        /// Builds the caravanTraderKinds list from <see cref="enabledCaravanTypes"/>.
        /// Resource types resolve to Caravan_Empire_{Name} defs.
        /// Exotic/Slaver resolve to tech-appropriate vanilla defs with policy/level gating.
        /// </summary>
        private List<TraderKindDef> BuildCaravanTraderKinds(TechLevel tech)
        {
            List<TraderKindDef> result = new List<TraderKindDef>();
            bool isNeolithic = tech <= TechLevel.Medieval;

            if (enabledCaravanTypes.NullOrEmpty())
            {
                LogUtil.Warning("enabledCaravanTypes null or empty in BuildCaravanTraderKinds");
                InitEnabledCaravanTypes();
            }

            foreach (string typeId in enabledCaravanTypes)
            {
                TraderKindDef resolved = null;

                if (typeId == "Exotic")
                {
                    bool hasLevel = factionLevel >= 4;
                    bool hasMercantile = policyManager.HasPolicy(FCPolicyDefOf.mercantile);
                    if (!hasLevel && !hasMercantile)
                        continue;

                    string defName = isNeolithic
                        ? "Caravan_Neolithic_ShamanMerchant"
                        : "Caravan_Outlander_Exotic";
                    resolved = DefDatabase<TraderKindDef>.GetNamedSilentFail(defName);
                }
                else if (typeId == "Slaver")
                {
                    if (policyManager.HasPolicy(FCPolicyDefOf.pacifist) || policyManager.HasPolicy(FCPolicyDefOf.egalitarian))
                        continue;

                    string defName = isNeolithic
                        ? "Caravan_Neolithic_Slaver"
                        : "Caravan_Outlander_PirateMerchant";
                    resolved = DefDatabase<TraderKindDef>.GetNamedSilentFail(defName);
                }
                else
                {
                    // Resource-based: RTD_Food -> FC_Caravan_Empire_Food
                    ResourceTypeDef rtd = DefDatabase<ResourceTypeDef>.GetNamedSilentFail(typeId);
                    if (rtd is null || !rtd.ResourceTypeAllowedByTech(tech))
                        continue;

                    // Skip if this resource has no production
                    if ((ReturnResource(rtd)?.amount ?? 0) == 0)
                        continue;

                    string suffix = rtd.defName.Replace("RTD_", "");
                    resolved = DefDatabase<TraderKindDef>.GetNamedSilentFail("FC_Caravan_Empire_" + suffix);
                }

                if (resolved is object)
                    result.Add(resolved);
            }

            if (result.Count == 0)
                LogUtil.Warning($"BuildCaravanTraderKinds produced an empty list. enabledCaravanTypes: {enabledCaravanTypes?.Count ?? 0}, techLevel: {tech}");

            return result;
        }

        public string ReturnNextTechToLevel()
        {
            // Medieval-only cap: once at (or past) Medieval there is no next tier to unlock.
            if (FCSettings.medievalTechOnly && techLevel >= TechLevel.Medieval)
                return "FCReachedMaxLevel".Translate();

            // Find the lowest barrier strictly above the current tech level; its research is what
            // unlocks the next tier.
            TechLevelBarrier nextBarrier = null;
            TechLevel nextLevel = TechLevel.Undefined;
            foreach (KeyValuePair<TechLevel, TechLevelBarrier> kvp in FactionCache.TechBarriers)
            {
                if (kvp.Key <= techLevel) continue;
                if (nextBarrier is null || kvp.Key < nextLevel)
                {
                    nextLevel = kvp.Key;
                    nextBarrier = kvp.Value;
                }
            }

            if (nextBarrier is null) return "FCReachedMaxLevel".Translate();
            string label = nextBarrier.DisplayLabel;
            return label.NullOrEmpty() ? (string)"FCReachedMaxLevel".Translate() : label.CapitalizeFirst();
        }

        #endregion

        #region Misc

        public void SetName(string name)
        {
            this.name = name;
        }

        public void GainHappiness(double amount)
        {
            foreach (WorldSettlementFC settlement in settlements)
            {
                settlement.GainHappiness(amount);
            }
        }

        public void GainUnrestForReason(Message msg, double amount)
        {
            Messages.Message(msg);
            foreach (WorldSettlementFC settlement in settlements)
            {
                settlement.GainUnrest(amount);
            }
        }

        /// <summary>
        /// True when the Empire is too unhappy/disloyal/restless to bother sending trade caravans.
        /// Gates both the Mercantile policy and organic trader-caravan incidents.
        /// </summary>
        public bool ShouldSuppressCaravans()
        {
            if (!settlements.Any()) return false;
            return averageHappiness < EmpireDeathPenaltyUtil.THRESHOLD_LOW
                || averageLoyalty < EmpireDeathPenaltyUtil.THRESHOLD_LOW
                || averageUnrest > EmpireDeathPenaltyUtil.THRESHOLD_HIGH_UNREST;
        }

        /* Caravan -> home-settlement tagging (populated by LordPatches at spawn). */

        public void RegisterCaravanHome(int lordLoadID, WorldSettlementFC home)
        {
            if (home is null) return;
            caravanHomeSettlements[lordLoadID] = home;
        }

        public WorldSettlementFC TryGetCaravanHome(int lordLoadID)
        {
            return caravanHomeSettlements.TryGetValue(lordLoadID, out WorldSettlementFC home) ? home : null;
        }

        public void UnregisterCaravanHome(int lordLoadID)
        {
            caravanHomeSettlements.Remove(lordLoadID);
        }

        /// <summary>
        /// Backstop cleanup for caravan-home entries whose Lord no longer exists on any map (the normal
        /// path is the LordManager.RemoveLord postfix). Cheap — the dictionary holds at most a few caravans.
        /// </summary>
        private void PruneCaravanHomeSettlements()
        {
            if (caravanHomeSettlements.Count == 0) return;
            List<int> toRemove = null;
            foreach (KeyValuePair<int, WorldSettlementFC> kvp in caravanHomeSettlements)
            {
                if (kvp.Value is null || !LordStillExists(kvp.Key))
                {
                    if (toRemove is null) toRemove = new List<int>();
                    toRemove.Add(kvp.Key);
                }
            }
            if (toRemove is null) return;
            foreach (int id in toRemove)
                caravanHomeSettlements.Remove(id);
        }

        private static bool LordStillExists(int loadID)
        {
            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                List<Verse.AI.Group.Lord> lords = maps[i].lordManager.lords;
                for (int j = 0; j < lords.Count; j++)
                    if (lords[j].loadID == loadID) return true;
            }
            return false;
        }

        public bool SendDiplomaticEnvoy(Faction faction)
        {
            if (faction.def.permanentEnemy)
            {
                Messages.Message("FCCannotImproveRelationsWithType".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }

            // Try new behavior system first
            bool handled = false;
            policyManager.ForEachBehavior(b =>
            {
                if (!handled)
                    handled = b.HandleDiplomaticEnvoy(this, faction);
            });
            return handled;
        }

        /// <summary>
        /// Syncs faction goodwill with average happiness. Should only be called from StatTick (daily).
        /// </summary>
        private void SyncGoodwillWithAverages()
        {
            if (settlements.Any() && FindFC.EmpireFaction != null)
            {
                FindFC.EmpireFaction.TryAffectGoodwillWith(Find.FactionManager.OfPlayer,
                    (Convert.ToInt32(averageHappiness) - FindFC.EmpireFaction.PlayerGoodwill));
            }
        }

        public bool CheckSettlementCaravansList(PlanetTile location) //list of destinations caravans gone to
        {
            for (int i = 0; i < settlementCaravansList.Count; i++)
            {
                if (location == settlementCaravansList[i] || Find.WorldGrid.IsNeighbor(location, settlementCaravansList[i]))
                {
                    return true; // is on list
                }
            }

            return false; //is not on list
        }
        /// <summary>
        /// Checks the settlementCaravansList to see if it has any orphaned tile locations, and removes them.
        /// </summary>
        /// <returns>TRUE if an orphaned location was found. FALSE otherwise.</returns>
        public bool ValidateSettlementCaravansList()
        {
            bool foundInvalidCaravan = false;
            List<PlanetTile> matched = new List<PlanetTile>();
            List<PlanetTile> toAdd = new List<PlanetTile>();
            List<PlanetTile> toRemove = new List<PlanetTile>();

            foreach (FCEvent evt in Events)
            {
                // FCEventMaker.ProcessEvents already warns and skips null-def events;
                // skip them here too to avoid an NRE that would abort daily validation.
                if (evt.def == null) continue;
                if (evt.def.defName == "settleNewColony")
                {
                    if (settlementCaravansList.Contains(evt.location))
                    {
                        matched.Add(evt.location);
                    }
                    else
                    {
                        LogUtil.Warning($"ValidateSettlementCaravansList: found settleNewColony event at tile {evt.location}, NOT in settlementCaravansList. Adding.");
                        toAdd.Add(evt.location);
                    }
                }
            }

            if (matched.Count != settlementCaravansList.Count)
            {
                foundInvalidCaravan = true;
                foreach (PlanetTile tile in settlementCaravansList)
                {
                    if (!matched.Contains(tile))
                    {
                        toRemove.Add(tile);
                    }
                }

                foreach (PlanetTile tile in toRemove)
                {
                    LogUtil.Warning($"ValidateSettlementCaravansList: removing orphaned tile {tile} from settlementCaravansList");
                    settlementCaravansList.Remove(tile);
                }
            }
            else if (toAdd.Count == 0)
            {
                LogUtil.Message($"ValidateSettlementCaravansList: all settleNewColony events have valid locations");
            }

            if (toAdd.Count > 0)
            {
                settlementCaravansList.AddRange(toAdd);
            }

            return foundInvalidCaravan;
        }

        private void RecoverOrphanedConstructions(int currentTick)
        {
            const int gracePeriod = 500;
            IReadOnlyList<FCEvent> constructEvents = eventManager.GetByDef(FCEventDefOf.constructBuilding);
            IReadOnlyList<FCEvent> upgradeEvents = eventManager.GetByDef(FCEventDefOf.upgradeSettlement);

            foreach (WorldSettlementFC settlement in settlements)
            {
                // Check for orphaned construction slots
                if (settlement.BuildingsComp is object)
                {
                    List<BuildingFC> buildings = settlement.BuildingsComp.Buildings;
                    for (int slot = 0; slot < buildings.Count; slot++)
                    {
                        BuildingFC building = buildings[slot];
                        if (building.def != BuildingFCDefOf.Construction) continue;
                        if (building.completionTick + gracePeriod >= currentTick) continue;

                        bool hasMatchingEvent = constructEvents.Any(evt => evt.source == settlement.Tile && evt.buildingSlot == slot);

                        if (!hasMatchingEvent)
                        {
                            try
                            {
                                settlement.ConstructBuilding(building.underConstructionDef, slot);
                                LogUtil.Warning($"RecoverOrphanedConstructions: auto-completed orphaned construction " +
                                    $"'{building.underConstructionDef?.defName ?? "NULL"}' in slot {slot} at {settlement.Name}");
                            }
                            catch (Exception ex)
                            {
                                LogUtil.Error($"RecoverOrphanedConstructions: failed to recover slot {slot} at {settlement.Name}: {ex}");
                            }
                        }
                    }
                }

                // Check for orphaned upgrade state
                if (settlement.IsUpgrading && settlement.FinishUpgradeTick + gracePeriod < currentTick)
                {
                    bool hasMatchingEvent = upgradeEvents.Any(t => t.location == settlement.Tile);

                    if (!hasMatchingEvent)
                    {
                        try
                        {
                            settlement.UpgradeSettlement(setFlags: true);
                            LogUtil.Warning($"RecoverOrphanedConstructions: auto-completed orphaned upgrade at {settlement.Name}");
                        }
                        catch (Exception ex)
                        {
                            LogUtil.Error($"RecoverOrphanedConstructions: failed to recover upgrade at {settlement.Name}: {ex}");
                        }
                    }
                }
            }
        }

        #endregion
    }
}
