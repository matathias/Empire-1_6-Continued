using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Similar in function to Verse.Find. This class offers helpful, short accessors to comps and notable comp-subfields.
    /// <para>This cache needs to be invalidated any time the game loads or changes. Presently, this is done through a Harmony Postfix on Game.Dispose().</para>
    /// <para>Each cached accessor only commits its value once loading has fully finished (Scribe.mode == Inactive).
    /// During a Scribe load, Rimworld first constructs shells of world components, and then later deep-loads those world
    /// components as new instances that replace the freshly-constructed shells. A value read mid-load (e.g. a world-object
    /// comp's Initialize() during LoadingVars, before world components are loaded) would then pin an empty shell instead
    /// of the actual world component for the whole session — making settlements vanish from the UI, among other things.</para>
    /// </summary>
    public static class FindFC
    {
        /*-*-*-*-*/
        /* Cached references: stable for the world/comp lifetime, invalidated in Invalidate() */
        /*-*-*-*-*/
        private static Faction _cachedColonyFaction = null;
        private static Faction _cachedPlayerFaction = null;
        private static FactionDef _cachedFactionDef = null;

        private static FactionFC _cachedFactionWorldComp = null;
        private static WorldComponent_EnemyPower _cachedEnemyPower = null;
        private static WorldComponent_Archive _cachedArchive = null;

        private static MilitaryOperationManager _cachedMilitaryManager = null;
        private static FCEventManager _cachedEventManager = null;
        private static TaxLedger _cachedTaxledger = null;
        private static PolicyManager _cachedPolicyManager = null;

        private static MilitaryFC _cachedMilitary = null;
        private static FCRoadBuilder _cachedRoadBuilder = null;
        private static EmpireThreatAdaptation _cachedThreatAdaptation = null;

        /*-*-*-*-*/
        /* Faction + def accessors */
        /*-*-*-*-*/
        public static Faction EmpireFaction
        {
            get
            {
                if (_cachedColonyFaction is object) return _cachedColonyFaction;
                Faction faction = Find.FactionManager?.FirstFactionOfDef(EmpireFactionDef);
                if (Scribe.mode == LoadSaveMode.Inactive) _cachedColonyFaction = faction;
                return faction;
            }
        }

        public static Faction PlayerFaction
        {
            get
            {
                if (_cachedPlayerFaction is object) return _cachedPlayerFaction;
                Faction faction = Find.FactionManager?.AllFactions?.FirstOrDefault(f => f.IsPlayer);
                if (Scribe.mode == LoadSaveMode.Inactive) _cachedPlayerFaction = faction;
                return faction;
            }
        }

        public static FactionDef EmpireFactionDef
        {
            get
            {
                if (_cachedFactionDef is object) return _cachedFactionDef;
                FactionDef def = DefDatabase<FactionDef>.GetNamed("PColony");
                if (Scribe.mode == LoadSaveMode.Inactive) _cachedFactionDef = def;
                return def;
            }
        }

        /// <summary>Live player-empire name; falls back to the comp's display name, then a generic label.
        /// Reads live (the name is mutable) — never cached.</summary>
        public static string EmpireName =>
            EmpireFaction?.Name
            ?? FactionComp?.name
            ?? "FCPlayerFaction".Translate().ToString();

        /// <summary>Live player-empire polity title (Empire / Kingdom / Republic / ...).
        /// No real-Faction equivalent exists; falls back to the comp's title then a generic label.
        /// Reads live (the title is mutable) — never cached.</summary>
        public static string EmpireTitle =>
            FactionComp?.title
            ?? "FCEmpire".Translate().ToString();

        /*-*-*-*-*/
        /* World components */
        /*-*-*-*-*/
        public static FactionFC FactionComp
        {
            get
            {
                if (_cachedFactionWorldComp is object) return _cachedFactionWorldComp;
                FactionFC comp = Find.World?.GetComponent<FactionFC>();
                if (Scribe.mode == LoadSaveMode.Inactive) _cachedFactionWorldComp = comp;
                return comp;
            }
        }

        public static WorldComponent_EnemyPower EnemyPower
        {
            get
            {
                if (_cachedEnemyPower is object) return _cachedEnemyPower;
                WorldComponent_EnemyPower comp = Find.World?.GetComponent<WorldComponent_EnemyPower>();
                if (Scribe.mode == LoadSaveMode.Inactive) _cachedEnemyPower = comp;
                return comp;
            }
        }

        public static WorldComponent_Archive Archive
        {
            get
            {
                if (_cachedArchive is object) return _cachedArchive;
                WorldComponent_Archive comp = Find.World?.GetComponent<WorldComponent_Archive>();
                if (Scribe.mode == LoadSaveMode.Inactive) _cachedArchive = comp;
                return comp;
            }
        }

        /*-*-*-*-*/
        /* FactionFC sub-managers (stable field references, cached) */
        /*-*-*-*-*/
        public static MilitaryOperationManager MilitaryManager
        {
            get
            {
                if (_cachedMilitaryManager is object) return _cachedMilitaryManager;
                MilitaryOperationManager val = FactionComp?.militaryOperationManager;
                if (Scribe.mode == LoadSaveMode.Inactive) _cachedMilitaryManager = val;
                return val;
            }
        }

        public static FCEventManager EventManager
        {
            get
            {
                if (_cachedEventManager is object) return _cachedEventManager;
                FCEventManager val = FactionComp?.eventManager;
                if (Scribe.mode == LoadSaveMode.Inactive) _cachedEventManager = val;
                return val;
            }
        }

        public static TaxLedger TaxLedger
        {
            get
            {
                if (_cachedTaxledger is object) return _cachedTaxledger;
                TaxLedger val = FactionComp?.taxLedger;
                if (Scribe.mode == LoadSaveMode.Inactive) _cachedTaxledger = val;
                return val;
            }
        }

        public static PolicyManager PolicyManager
        {
            get
            {
                if (_cachedPolicyManager is object) return _cachedPolicyManager;
                PolicyManager val = FactionComp?.policyManager;
                if (Scribe.mode == LoadSaveMode.Inactive) _cachedPolicyManager = val;
                return val;
            }
        }

        public static MilitaryFC Military
        {
            get
            {
                if (_cachedMilitary is object) return _cachedMilitary;
                MilitaryFC val = FactionComp?.military;
                if (Scribe.mode == LoadSaveMode.Inactive) _cachedMilitary = val;
                return val;
            }
        }

        public static FCRoadBuilder RoadBuilder
        {
            get
            {
                if (_cachedRoadBuilder is object) return _cachedRoadBuilder;
                FCRoadBuilder val = FactionComp?.roadBuilder;
                if (Scribe.mode == LoadSaveMode.Inactive) _cachedRoadBuilder = val;
                return val;
            }
        }

        public static EmpireThreatAdaptation ThreatAdaptation
        {
            get
            {
                if (_cachedThreatAdaptation is object) return _cachedThreatAdaptation;
                EmpireThreatAdaptation val = FactionComp?.threatAdaptation;
                if (Scribe.mode == LoadSaveMode.Inactive) _cachedThreatAdaptation = val;
                return val;
            }
        }

        /*-*-*-*-*/
        /* Mutable passthroughs: scalar/list-content fields that change at runtime; NEVER cache */
        /*-*-*-*-*/
        public static Map TaxMap => FactionComp?.TaxMap;
        public static TechLevel TechLevel => FactionComp?.techLevel ?? TechLevel.Undefined;
        public static int FactionLevel => FactionComp?.factionLevel ?? 0;
        public static IReadOnlyList<FCEvent> Events => FactionComp?.Events;
        public static PlanetTile CapitalLocation => FactionComp?.capitalLocation ?? PlanetTile.Invalid;
        public static List<WorldSettlementFC> Settlements => FactionComp?.settlements;

        public static void Invalidate()
        {
            _cachedColonyFaction = null;
            _cachedPlayerFaction = null;
            _cachedFactionDef = null;

            _cachedFactionWorldComp = null;
            _cachedEnemyPower = null;
            _cachedArchive = null;

            _cachedMilitaryManager = null;
            _cachedEventManager = null;
            _cachedTaxledger = null;
            _cachedPolicyManager = null;

            _cachedMilitary = null;
            _cachedRoadBuilder = null;
            _cachedThreatAdaptation = null;
        }
        public static bool IsEmpireFaction(Faction f) => !(EmpireFaction is null) && f == EmpireFaction;
    }
}
