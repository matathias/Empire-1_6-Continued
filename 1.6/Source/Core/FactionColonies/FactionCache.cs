using FactionColonies.util;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Static cache to hold on to frequently-accessed fields that change infrequently, or never.
    ///
    /// <para>This cache needs to be invalidated any time the game loads or changes. Presently, this is done through a Harmony Postfix on Game.Dispose().</para>
    /// <para>NOTE: DefDatabase[PawnKindDef].AllDefsListForReading is cached here, filtered through <see cref="PawnKindDefExtensions.ValidPawnKindDef"/> so malformed defs from other mods never reach downstream consumers. Nevertheless, def caching means that def hotloading is a no-no.</para>
    /// </summary>
    public static class FactionCache
    {
        private static List<PawnKindDef> _cachedPawnKindDefs = null;
        private static Dictionary<(Type, string), FieldInfo> _cachedFields = new Dictionary<(Type, string), FieldInfo>();
        private static List<XenotypeDef> _cachedXenotypeList = null;
        private static List<XenotypeDef> _cachedViolentXenotypeList = null;
        private static List<CustomXenotype> _cachedCustomXenotypeList = null;
        private static List<CustomXenotype> _cachedViolentCustomXenotypeList = null;
        private static Dictionary<string, CustomXenotype> _cachedCustomXenotypeDecoder = null;
        private static List<ThingDef> _cachedRaceList = null;
        private static List<PawnKindDef> _cachedAnimalKinds = null;
        private static List<PawnKindDef> _cachedCombatAnimalKinds = null;
        private static List<PawnKindDef> _cachedPackAnimalKinds = null;
        private static List<PawnKindDef> _cachedControllableMechKinds = null;
        private static Dictionary<ThingDef, List<RecipeDef>> _cachedMechGestationRecipes = null;
        private static bool _checkedForNonViolentXenos = false;
        private static bool _cachedNonViolentXenosExist = false;
        private static Dictionary<XenotypeDef, bool> _cachedXenotypeViolenceDict = null;
        private static Dictionary<string, bool> _cachedCustomXenotypeViolenceDict = null;
        private static List<FCPolicyDef> _cachedFCPolicyDefs = null;
        private static Dictionary<FCPolicyDef, string> _cachedFCPolicyDescs = null;
        private static Dictionary<BuildingFCDef, List<BuildingUpgradeEntry>> _cachedUpgradeTrees = null;
        private static Dictionary<BuildingFCDef, HashSet<BuildingFCDef>> _cachedUpgradeDescendants = null;
        private static Dictionary<BuildingFCDef, List<BuildingFCDef>> _cachedRequiredByMap = null;
        private static List<FCEventCategoryDef> _cachedEventCategoryDefs = null;
        private static List<MilitaryJobDef> _cachedHostileMilitaryJobs = null;
        private static Dictionary<TechLevel, TechLevelBarrier> _cachedTechBarriers = null;
        private static ResearchProjectDef _cachedTransportPods = null;
        private static List<ResourceTypeDef> _cachedResourceTypeDefs = null;
        private static List<ResourceTypeDef> _cachedPoolResourceTypeDefs = null;
        private static List<ResourceTypeDef> _cachedNonPoolResourceTypeDefs = null;
        private static List<ResourceTypeDef> _cachedTitheableResourceTypeDefs = null;
        private static List<ResourceTypeDef> _cachedSortedResourceTypeDefsForUI = null;
        private static Dictionary<FCPolicyCategory, List<FCPolicyDef>> _cachedPoliciesByCategory = null;
        private static List<WorldSettlementDef> _cachedAvailableWorldSettlementDefs = null;
        private static HashSet<string> _cachedEventFollowUpDefNames = null;
        private static List<FCEventDef> _cachedRandomRollableEvents = null;
        private static List<FCEventDef> _cachedAllRandomEventDefs = null;
        private static HashSet<string> _cachedEventDefNamesWithOptionsInChain = null;
        private static HashSet<FCEventDef> _cachedRandomChainMemberDefs = null;
        private static Dictionary<FCEventDef, FCEventDef> _cachedChainCooldownRootByMember = null;
        private static HashSet<BiomeResourceDef> _cachedBiomeResourceDefSet = null;
        private static Dictionary<TechLevel, List<BuildingFCDef>> _cachedBuildingDefsByTechLevel = null;
        private static List<FCSituationDef> _cachedFactionConditionedSituationDefs = null;
        private static List<FCSituationDef> _cachedSettlementConditionedSituationDefs = null;

        public static List<PawnKindDef> AllPawnKindDefs
        {
            get
            {
                if (_cachedPawnKindDefs is null || _cachedPawnKindDefs.Count == 0)
                {
                    List<PawnKindDef> all = DefDatabase<PawnKindDef>.AllDefsListForReading;
                    _cachedPawnKindDefs = new List<PawnKindDef>(all.Count);
                    int skipped = 0;
                    foreach (PawnKindDef def in all)
                    {
                        if (def.ValidPawnKindDef()) _cachedPawnKindDefs.Add(def);
                        else skipped++;
                    }
                    if (skipped > 0)
                        LogUtil.Warning($"FactionCache.AllPawnKindDefs: skipped {skipped} malformed PawnKindDef(s). See preceding warnings for offending defs and source mods.");
                }
                return _cachedPawnKindDefs;
            }
        }
        public static Dictionary<(Type, string), FieldInfo> FieldCache => _cachedFields;
        public static FieldInfo GetFieldCacheValue(Type typ, string field)
        {
            if (FieldCache.TryGetValue((typ, field), out FieldInfo fieldInfo))
            {
                return fieldInfo;
            }
            fieldInfo = typ.GetField(field);
            if (fieldInfo == null)
            {
                LogUtil.Warning($"FactionCache.GetFieldCacheValue: field '{field}' not found on type '{typ.FullName}'");
            }
            FieldCache.Add((typ, field), fieldInfo);
            return fieldInfo;
        }

        /// <summary>FCSituationDefs that are faction-scoped and carry a faction condition extension —
        /// the spawn pass iterates these instead of the whole database.</summary>
        public static List<FCSituationDef> FactionConditionedSituationDefs
        {
            get
            {
                if (_cachedFactionConditionedSituationDefs is null)
                {
                    _cachedFactionConditionedSituationDefs = new List<FCSituationDef>();
                    foreach (FCSituationDef def in DefDatabase<FCSituationDef>.AllDefsListForReading)
                        if (def.scope == FCSituationScope.Faction && def.FactionCondition != null)
                            _cachedFactionConditionedSituationDefs.Add(def);
                }
                return _cachedFactionConditionedSituationDefs;
            }
        }

        /// <summary>FCSituationDefs that are settlement-scoped and carry a settlement condition extension.</summary>
        public static List<FCSituationDef> SettlementConditionedSituationDefs
        {
            get
            {
                if (_cachedSettlementConditionedSituationDefs is null)
                {
                    _cachedSettlementConditionedSituationDefs = new List<FCSituationDef>();
                    foreach (FCSituationDef def in DefDatabase<FCSituationDef>.AllDefsListForReading)
                        if (def.scope == FCSituationScope.Settlement && def.SettlementCondition != null)
                            _cachedSettlementConditionedSituationDefs.Add(def);
                }
                return _cachedSettlementConditionedSituationDefs;
            }
        }

        public static List<XenotypeDef> XenotypeDefs => _cachedXenotypeList ?? (_cachedXenotypeList = DefDatabase<XenotypeDef>.AllDefsListForReading);
        public static List<CustomXenotype> CustomXenotypes
        {
            get
            {
                if (_cachedCustomXenotypeList != null)
                    return _cachedCustomXenotypeList;

                List<CustomXenotype> list = BuildMergedCustomXenotypeList();

                // Only cache when the Scribe is inactive. During loading, the disk read is
                // skipped (see BuildMergedCustomXenotypeList), so the list is incomplete.
                // Returning without caching ensures the next post-load access rebuilds fully.
                if (Scribe.mode == LoadSaveMode.Inactive)
                    _cachedCustomXenotypeList = list;

                return list;
            }
        }

        private static List<CustomXenotype> BuildMergedCustomXenotypeList()
        {
            var perSave = Current.Game?.customXenotypeDatabase?.customXenotypes;
            var merged = new List<CustomXenotype>();
            var seenNames = new HashSet<string>();

            // Per-save entries first (preferred)
            if (perSave != null)
            {
                foreach (CustomXenotype x in perSave)
                {
                    if (x?.name != null && seenNames.Add(x.name))
                        merged.Add(x);
                }
            }

            // Global disk entries (fill in anything not already in per-save).
            // Skip during active Scribe loading. CharacterCardUtility.CustomXenotypesForReading
            // reads files via InitLoadingMetaHeaderOnly, which calls Scribe.ForceStop() when the
            // Scribe is already active, destroying the entire save-load pipeline.
            if (Scribe.mode == LoadSaveMode.Inactive)
            {
                try
                {
                    List<CustomXenotype> disk = CharacterCardUtility.CustomXenotypesForReading;
                    if (disk != null)
                    {
                        foreach (CustomXenotype x in disk)
                        {
                            if (x?.name != null && seenNames.Add(x.name))
                                merged.Add(x);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.Warning($"Failed to load disk custom xenotypes: {ex.Message}");
                }
            }
            else
            {
                LogUtil.Warning($"BuildMergedCustomXenotypeList called while Scribe mode is not Inactive. Skipping CustomXenotypesForReading");
            }

            return merged;
        }

        /// <summary>
        /// Adds a CustomXenotype to the per-save database if not already present.
        /// This ensures disk-only xenotypes are persisted in the save file when used.
        /// </summary>
        public static void EnsureInGameDatabase(CustomXenotype xenotype)
        {
            if (xenotype is null) return;
            List<CustomXenotype> db = Current.Game?.customXenotypeDatabase?.customXenotypes;
            if (db is null) return;

            foreach (CustomXenotype existing in db)
            {
                if (existing.name == xenotype.name)
                    return;
            }

            db.Add(xenotype);
            InvalidateCustomXenotypeCache();
        }
        public static Dictionary<string, CustomXenotype> CustomXenotypesDecoder
        {
            get
            {
                if (_cachedCustomXenotypeDecoder is null)
                {
                    Dictionary<string, CustomXenotype> decoder = new Dictionary<string, CustomXenotype>();
                    List<CustomXenotype> xenos = CustomXenotypes;
                    if (xenos != null)
                    {
                        foreach (CustomXenotype xenotype in xenos)
                        {
                            decoder[xenotype.name] = xenotype;
                        }
                    }
                    // Only cache when Scribe is inactive (matches CustomXenotypes behavior).
                    // During loading, disk xenotypes are unavailable so the decoder is incomplete.
                    if (Scribe.mode == LoadSaveMode.Inactive)
                        _cachedCustomXenotypeDecoder = decoder;
                    return decoder;
                }
                return _cachedCustomXenotypeDecoder;
            }
        }
        public static CustomXenotype GetCustomXenotype(string name)
        {
            CustomXenotype xenotype = null;
            if (!CustomXenotypesDecoder.TryGetValue(name, out xenotype))
            {
                LogUtil.Warning($"Custom xenotype {name} does not appear in the xenotype decoder dictionary");
            }
            return xenotype;
        }
        public static List<ThingDef> HumanlikeRaces
        {
            get
            {
                if (_cachedRaceList == null)
                {
                    _cachedRaceList = new List<ThingDef>();
                    foreach (PawnKindDef pawnKind in AllPawnKindDefs)
                    {
                        if (pawnKind.race != null && !_cachedRaceList.Any(r => r.defName == pawnKind.race.defName) && (pawnKind.race == ThingDefOf.Human || pawnKind.IsHumanLikeRace()))
                        {
                            _cachedRaceList.Add(pawnKind.race);
                        }
                    }
                }
                return _cachedRaceList;
            }
        }
        // Technically there should *always* be at least one race: ThingDefOf.Human. But it probably can't hurt to null-check, just in case of edge cases...
        public static int HumanlikeRacesCount => HumanlikeRaces?.Count ?? 0;
        public static List<PawnKindDef> AllAnimalKindDefs => _cachedAnimalKinds ??
                                                             (_cachedAnimalKinds = AllPawnKindDefs.Where(kind => kind.IsAnimalAndAllowed()).ToList());
        public static List<PawnKindDef> AllCombatAnimalKindDefs => _cachedCombatAnimalKinds ??
                                                                   (_cachedCombatAnimalKinds = AllAnimalKindDefs.Where(kind => kind.IsCombatAnimal()).ToList());
        public static List<PawnKindDef> AllPackAnimalKinds => _cachedPackAnimalKinds ??
                                                              (_cachedPackAnimalKinds = AllAnimalKindDefs.Where(kind => kind.IsPackAnimal()).ToList());
        /// <summary>All player-controllable mechanoid PawnKindDefs (mechs with an OverseerSubject comp
        /// and a BandwidthCost stat) that a mechanitor merc can be assigned. Empty when Biotech is off.
        /// Research-independent — use <see cref="UnlockedControllableMechKinds"/> for the picker.</summary>
        public static List<PawnKindDef> AllControllableMechKinds => _cachedControllableMechKinds ??
            (_cachedControllableMechKinds = (!ModsConfig.BiotechActive
                ? new List<PawnKindDef>()
                : AllPawnKindDefs.Where(kind => kind?.race?.race != null
                        && kind.race.race.IsMechanoid
                        && kind.race.GetCompProperties<CompProperties_OverseerSubject>() != null
                        && kind.race.statBases != null
                        && kind.race.statBases.Any(s => s.stat == StatDefOf.BandwidthCost))
                    .ToList()));

        /// <summary>Map of mech ThingDef -> its mechanitor gestation recipe(s). Research-independent
        /// (which recipes exist never changes), so it's safely cached; the research check itself
        /// (<see cref="IsMechResearchUnlocked"/>) reads live research state.</summary>
        public static Dictionary<ThingDef, List<RecipeDef>> MechGestationRecipes => _cachedMechGestationRecipes ??
            (_cachedMechGestationRecipes = BuildMechGestationRecipes());

        private static Dictionary<ThingDef, List<RecipeDef>> BuildMechGestationRecipes()
        {
            Dictionary<ThingDef, List<RecipeDef>> map = new Dictionary<ThingDef, List<RecipeDef>>();
            if (!ModsConfig.BiotechActive) return map;
            foreach (RecipeDef r in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                if (!r.mechanitorOnlyRecipe) continue;
                ThingDef product = r.ProducedThingDef;
                if (product is null) continue;
                List<RecipeDef> list;
                if (!map.TryGetValue(product, out list)) { list = new List<RecipeDef>(); map[product] = list; }
                list.Add(r);
            }
            return map;
        }

        /// <summary>True when the player has researched the means to build this mech — i.e. it has a
        /// gestation recipe whose research prerequisites are all complete. A mech with no gestation
        /// recipe at all (e.g. boss-only mechs the player can never build) is NOT assignable.</summary>
        public static bool IsMechResearchUnlocked(PawnKindDef kind)
        {
            if (!ModsConfig.BiotechActive || kind?.race is null) return false;
            List<RecipeDef> recipes;
            if (!MechGestationRecipes.TryGetValue(kind.race, out recipes) || recipes.NullOrEmpty())
                return false; // no gestation recipe — the player can't build this mech at all
            foreach (RecipeDef r in recipes)
            {
                if (r.researchPrerequisite != null && !r.researchPrerequisite.IsFinished) continue;
                if (r.researchPrerequisites != null && r.researchPrerequisites.Any(p => !p.IsFinished)) continue;
                return true;
            }
            return false;
        }

        /// <summary>The subset of <see cref="AllControllableMechKinds"/> the player has actually
        /// unlocked through research. Recomputed each call (research completes mid-game), not cached.</summary>
        public static List<PawnKindDef> UnlockedControllableMechKinds =>
            AllControllableMechKinds.Where(IsMechResearchUnlocked).ToList();
        public static bool NonViolentXenotypesExist
        {
            get
            {
                if (!_checkedForNonViolentXenos)
                {
                    bool exists = false;
                    if (XenotypeDefs?.Count > 0)
                    {
                        foreach (XenotypeDef xenotype in XenotypeDefs)
                        {
                            if (XenotypeFilter.IsXenotypeNonViolent(xenotype))
                            {
                                exists = true;
                                break;
                            }
                        }
                    }
                    if (!exists && CustomXenotypes?.Count > 0)
                    {
                        foreach (CustomXenotype xenotype in CustomXenotypes)
                        {
                            if (XenotypeFilter.IsCustomXenotypeNonViolent(xenotype.name))
                            {
                                exists = true;
                                break;
                            }
                        }
                    }

                    // Only latch when the Scribe is inactive. During loading, CustomXenotypes omits disk
                    // xenotypes (see BuildMergedCustomXenotypeList), so the result is incomplete — returning
                    // without latching lets the next post-load access recompute fully.
                    if (Scribe.mode == LoadSaveMode.Inactive)
                    {
                        _cachedNonViolentXenosExist = exists;
                        _checkedForNonViolentXenos = true;
                    }
                    return exists;
                }
                return _cachedNonViolentXenosExist;
            }
        }
        public static Dictionary<XenotypeDef, bool> XenotypeViolence
        {
            get
            {
                if (_cachedXenotypeViolenceDict is null && XenotypeDefs?.Count > 0)
                {
                    var dict = new Dictionary<XenotypeDef, bool>();
                    foreach (XenotypeDef xenotype in XenotypeDefs)
                    {
                        dict.Add(xenotype, !XenotypeFilter.IsXenotypeNonViolent(xenotype));
                    }
                    // Built from XenotypeDefs only (always fully loaded), but guarded for consistency with the
                    // CustomXenotypes-dependent caches so nothing latches mid-Scribe.
                    if (Scribe.mode == LoadSaveMode.Inactive)
                        _cachedXenotypeViolenceDict = dict;
                    return dict;
                }
                return _cachedXenotypeViolenceDict;
            }
        }
        public static Dictionary<string, bool> CustomXenotypeViolence
        {
            get
            {
                if (_cachedCustomXenotypeViolenceDict is null && CustomXenotypes?.Count > 0)
                {
                    var dict = new Dictionary<string, bool>();
                    foreach (CustomXenotype xenotype in CustomXenotypes)
                    {
                        dict.Add(xenotype.name, !XenotypeFilter.IsCustomXenotypeNonViolent(xenotype.name));
                    }
                    // Only cache when the Scribe is inactive — during loading CustomXenotypes omits disk
                    // xenotypes, so the dict is incomplete (see CustomXenotypes accessor).
                    if (Scribe.mode == LoadSaveMode.Inactive)
                        _cachedCustomXenotypeViolenceDict = dict;
                    return dict;
                }
                return _cachedCustomXenotypeViolenceDict;
            }
        }
        public static bool XenotypeIsNonViolent(XenotypeDef xenotype)
        {
            if (XenotypeViolence?.TryGetValue(xenotype, out bool violent) == true)
            {
                return !violent;
            }
            return false;
        }
        public static bool CustomXenotypeIsNonViolent(string xenotypeName)
        {
            if (CustomXenotypeViolence?.TryGetValue(xenotypeName, out bool violent) == true)
            {
                return !violent;
            }
            return false;
        }
        public static bool CustomXenotypeIsNonViolent(CustomXenotype xenotype)
        {
            return CustomXenotypeIsNonViolent(xenotype.name);
        }
        public static List<FCPolicyDef> AllFCPolicies => _cachedFCPolicyDefs ?? (_cachedFCPolicyDefs = DefDatabase<FCPolicyDef>.AllDefsListForReading);
        public static Dictionary<FCPolicyDef, string> FCPolicyDescs
        {
            get
            {
                if (_cachedFCPolicyDescs is null)
                {
                    _cachedFCPolicyDescs = new Dictionary<FCPolicyDef, string>();
                    foreach (FCPolicyDef policy in AllFCPolicies)
                    {
                        _cachedFCPolicyDescs.Add(policy, policy.PolicyDesc());
                    }
                }
                return _cachedFCPolicyDescs;
            }
        }

        /// <summary>Drops the cached policy descriptions so they re-resolve their tokens
        /// (e.g. {FACTION_TITLE}/{FACTION}) on next access. Call after the faction title or name changes.</summary>
        public static void InvalidatePolicyDescs() => _cachedFCPolicyDescs = null;
        public static List<XenotypeDef> ViolentXenotypeDefs => _cachedViolentXenotypeList ??
                                                               (_cachedViolentXenotypeList = XenotypeDefs.Where(x => !XenotypeIsNonViolent(x)).ToList());
        public static List<CustomXenotype> ViolentCustomXenotypes
        {
            get
            {
                if (_cachedViolentCustomXenotypeList != null)
                    return _cachedViolentCustomXenotypeList;

                List<CustomXenotype> list = CustomXenotypes.Where(x => !CustomXenotypeIsNonViolent(x)).ToList();

                // Only cache when the Scribe is inactive — CustomXenotypes omits disk xenotypes during loading,
                // so the list is incomplete (mirrors the CustomXenotypes accessor guard).
                if (Scribe.mode == LoadSaveMode.Inactive)
                    _cachedViolentCustomXenotypeList = list;

                return list;
            }
        }

        /* Tech caching */
        public static Dictionary<TechLevel, TechLevelBarrier> TechBarriers
        {
            get
            {
                if (_cachedTechBarriers is null)
                {
                    _cachedTechBarriers = new Dictionary<TechLevel, TechLevelBarrier>();
                    foreach (TechProgressionDef def in DefDatabase<TechProgressionDef>.AllDefsListForReading)
                    {
                        if (def.barriers is null) continue;
                        foreach (TechLevelBarrier b in def.barriers)
                        {
                            // Last-writer-wins if multiple Defs declare the same level, so override Defs take priority.
                            _cachedTechBarriers[b.techLevel] = b;
                        }
                    }
                }
                return _cachedTechBarriers;
            }
        }

        public static TechLevelBarrier GetTechBarrier(TechLevel level)
        {
            return TechBarriers.TryGetValue(level, out TechLevelBarrier b) ? b : null;
        }

        public static ResearchProjectDef TechTransportPods => _cachedTransportPods ??
                                                              (_cachedTransportPods = DefDatabase<ResearchProjectDef>.GetNamed("TransportPod", false));

        /// <summary>
        /// For each building, a flattened list of all upgrades reachable through the upgrade tree.
        /// </summary>
        public static Dictionary<BuildingFCDef, List<BuildingUpgradeEntry>> UpgradeTrees
        {
            get
            {
                if (_cachedUpgradeTrees is null)
                {
                    _cachedUpgradeTrees = new Dictionary<BuildingFCDef, List<BuildingUpgradeEntry>>();
                    foreach (BuildingFCDef building in DefDatabase<BuildingFCDef>.AllDefsListForReading)
                    {
                        if (building.upgrades is null || building.upgrades.Count == 0) continue;
                        List<BuildingUpgradeEntry> tree = new List<BuildingUpgradeEntry>();
                        CollectUpgradeTree(building, 0, null, tree);
                        _cachedUpgradeTrees[building] = tree;
                    }
                }
                return _cachedUpgradeTrees;
            }
        }

        private static void CollectUpgradeTree(BuildingFCDef building, int depth, BuildingFCDef parent, List<BuildingUpgradeEntry> result)
        {
            if (building.upgrades is null) return;
            foreach (BuildingFCDef upgrade in building.upgrades)
            {
                result.Add(new BuildingUpgradeEntry { def = upgrade, depth = depth, parent = parent ?? building });
                CollectUpgradeTree(upgrade, depth + 1, upgrade, result);
            }
        }

        /// <summary>
        /// For each building that has upgrades, the set of all transitive upgrade descendants.
        /// Used for O(1) "does this building satisfy a requirement for that building?" checks.
        /// </summary>
        public static Dictionary<BuildingFCDef, HashSet<BuildingFCDef>> UpgradeDescendants
        {
            get
            {
                if (_cachedUpgradeDescendants is null)
                {
                    _cachedUpgradeDescendants = new Dictionary<BuildingFCDef, HashSet<BuildingFCDef>>();
                    foreach (var kvp in UpgradeTrees)
                    {
                        HashSet<BuildingFCDef> set = new HashSet<BuildingFCDef>();
                        foreach (BuildingUpgradeEntry entry in kvp.Value)
                        {
                            set.Add(entry.def);
                        }
                        _cachedUpgradeDescendants[kvp.Key] = set;
                    }
                }
                return _cachedUpgradeDescendants;
            }
        }

        /// <summary>
        /// Returns true if <paramref name="candidate"/> is the same as <paramref name="required"/>,
        /// or is a transitive upgrade of it.
        /// </summary>
        public static bool SatisfiesRequirementFor(BuildingFCDef candidate, BuildingFCDef required)
        {
            if (candidate == required) return true;
            if (UpgradeDescendants.TryGetValue(required, out HashSet<BuildingFCDef> descendants))
                return descendants.Contains(candidate);
            return false;
        }

        /// <summary>
        /// Returns true if <paramref name="candidate"/> satisfies any entry in the given requirements list
        /// (i.e. equals or is an upgrade of any required building).
        /// </summary>
        public static bool SatisfiesAnyRequirement(BuildingFCDef candidate, List<BuildingFCDef> requirements)
        {
            if (requirements is null || requirements.Count == 0) return false;
            foreach (BuildingFCDef req in requirements)
            {
                if (SatisfiesRequirementFor(candidate, req)) return true;
            }
            return false;
        }

        /// <summary>
        /// Reverse lookup: for each building, all buildings that list it in their requiredBuildings.
        /// </summary>
        public static Dictionary<BuildingFCDef, List<BuildingFCDef>> RequiredByBuildingMap
        {
            get
            {
                if (_cachedRequiredByMap is null)
                {
                    _cachedRequiredByMap = new Dictionary<BuildingFCDef, List<BuildingFCDef>>();
                    foreach (BuildingFCDef building in DefDatabase<BuildingFCDef>.AllDefsListForReading)
                    {
                        if (building.requiredBuildings == null) continue;
                        foreach (BuildingFCDef req in building.requiredBuildings)
                        {
                            if (!_cachedRequiredByMap.TryGetValue(req, out List<BuildingFCDef> list))
                            {
                                list = new List<BuildingFCDef>();
                                _cachedRequiredByMap[req] = list;
                            }
                            list.Add(building);
                        }
                    }
                    // Propagate: if X requires Beta, then Beta_V2 (upgrade of Beta) also
                    // effectively satisfies that requirement — so show X in Beta_V2's
                    // "Required By" list as well.
                    foreach (var kvp in UpgradeDescendants)
                    {
                        if (!_cachedRequiredByMap.TryGetValue(kvp.Key, out List<BuildingFCDef> baseRequiredBy)) continue;
                        foreach (BuildingFCDef descendant in kvp.Value)
                        {
                            if (!_cachedRequiredByMap.TryGetValue(descendant, out List<BuildingFCDef> descList))
                            {
                                descList = new List<BuildingFCDef>();
                                _cachedRequiredByMap[descendant] = descList;
                            }
                            foreach (BuildingFCDef dep in baseRequiredBy)
                            {
                                if (!descList.Contains(dep)) descList.Add(dep);
                            }
                        }
                    }
                }
                return _cachedRequiredByMap;
            }
        }
        public static List<FCEventCategoryDef> FCEventCategoryDefs => _cachedEventCategoryDefs ??
                                    (_cachedEventCategoryDefs = DefDatabase<FCEventCategoryDef>.AllDefsListForReading);

        /// <summary>
        /// MilitaryJobDefs that have a floatMenuLabelKey, i.e. hostile operations shown in the world gizmo menu.
        /// </summary>
        public static List<MilitaryJobDef> HostileMilitaryJobs
        {
            get
            {
                if (_cachedHostileMilitaryJobs is null)
                {
                    _cachedHostileMilitaryJobs = new List<MilitaryJobDef>();
                    foreach (MilitaryJobDef job in DefDatabase<MilitaryJobDef>.AllDefsListForReading)
                    {
                        if (job.floatMenuLabelKey != null)
                            _cachedHostileMilitaryJobs.Add(job);
                    }
                }
                return _cachedHostileMilitaryJobs;
            }
        }

        /*-*-*-*-*/
        /* ResourceTypeDef caches */
        /*-*-*-*-*/

        public static List<ResourceTypeDef> AllResourceTypeDefs => _cachedResourceTypeDefs ??
                                                                   (_cachedResourceTypeDefs = DefDatabase<ResourceTypeDef>.AllDefsListForReading);

        public static List<ResourceTypeDef> PoolResourceTypeDefs
        {
            get
            {
                if (_cachedPoolResourceTypeDefs is null)
                {
                    _cachedPoolResourceTypeDefs = new List<ResourceTypeDef>();
                    foreach (ResourceTypeDef rtd in AllResourceTypeDefs)
                    {
                        if (rtd.isPoolResource) _cachedPoolResourceTypeDefs.Add(rtd);
                    }
                }
                return _cachedPoolResourceTypeDefs;
            }
        }

        public static List<ResourceTypeDef> NonPoolResourceTypeDefs
        {
            get
            {
                if (_cachedNonPoolResourceTypeDefs is null)
                {
                    _cachedNonPoolResourceTypeDefs = new List<ResourceTypeDef>();
                    foreach (ResourceTypeDef rtd in AllResourceTypeDefs)
                    {
                        if (!rtd.isPoolResource) _cachedNonPoolResourceTypeDefs.Add(rtd);
                    }
                }
                return _cachedNonPoolResourceTypeDefs;
            }
        }

        /// <summary>
        /// Non-pool resources that allow tithing. Tech-level eligibility is NOT applied here —
        /// callers must still filter by <c>ResourceTypeAllowedByTech(techLevel)</c> when tech matters.
        /// </summary>
        public static List<ResourceTypeDef> TitheableResourceTypeDefs
        {
            get
            {
                if (_cachedTitheableResourceTypeDefs is null)
                {
                    _cachedTitheableResourceTypeDefs = new List<ResourceTypeDef>();
                    foreach (ResourceTypeDef rtd in AllResourceTypeDefs)
                    {
                        if (!rtd.isPoolResource && rtd.CanTithe) _cachedTitheableResourceTypeDefs.Add(rtd);
                    }
                }
                return _cachedTitheableResourceTypeDefs;
            }
        }

        public static List<ResourceTypeDef> SortedResourceTypeDefsForUI => _cachedSortedResourceTypeDefsForUI ??
                                                                           (_cachedSortedResourceTypeDefsForUI = AllResourceTypeDefs
                                                                               .OrderBy(d => d.uiPriority)
                                                                               .ThenBy(d => d.LabelCap.RawText)
                                                                               .ToList());

        /*-*-*-*-*/
        /* Policy / Settlement / Event / Biome caches */
        /*-*-*-*-*/

        public static Dictionary<FCPolicyCategory, List<FCPolicyDef>> PoliciesByCategory
        {
            get
            {
                if (_cachedPoliciesByCategory is null)
                {
                    _cachedPoliciesByCategory = new Dictionary<FCPolicyCategory, List<FCPolicyDef>>();
                    foreach (FCPolicyDef policy in AllFCPolicies)
                    {
                        if (!_cachedPoliciesByCategory.TryGetValue(policy.category, out List<FCPolicyDef> list))
                        {
                            list = new List<FCPolicyDef>();
                            _cachedPoliciesByCategory[policy.category] = list;
                        }
                        list.Add(policy);
                    }
                }
                return _cachedPoliciesByCategory;
            }
        }

        public static List<FCPolicyDef> GetPoliciesByCategory(FCPolicyCategory cat)
        {
            return PoliciesByCategory.TryGetValue(cat, out List<FCPolicyDef> list) ? list : new List<FCPolicyDef>();
        }

        public static List<WorldSettlementDef> AvailableWorldSettlementDefs
        {
            get
            {
                if (_cachedAvailableWorldSettlementDefs is null)
                {
                    _cachedAvailableWorldSettlementDefs = new List<WorldSettlementDef>();
                    foreach (WorldSettlementDef def in DefDatabase<WorldSettlementDef>.AllDefsListForReading)
                    {
                        if (def.available) _cachedAvailableWorldSettlementDefs.Add(def);
                    }
                }
                return _cachedAvailableWorldSettlementDefs;
            }
        }

        /// <summary>
        /// defNames of events referenced as <c>followingEvent</c> / <c>followingEvent2</c> by some other event,
        /// i.e. events that are continuations, not independent triggers.
        /// </summary>
        public static HashSet<string> EventFollowUpDefNames
        {
            get
            {
                if (_cachedEventFollowUpDefNames is null)
                {
                    _cachedEventFollowUpDefNames = new HashSet<string>();
                    foreach (FCEventDef def in DefDatabase<FCEventDef>.AllDefsListForReading)
                    {
                        if (def.followingEvent != null) _cachedEventFollowUpDefNames.Add(def.followingEvent.defName);
                        if (def.followingEvent2 != null) _cachedEventFollowUpDefNames.Add(def.followingEvent2.defName);
                    }
                }
                return _cachedEventFollowUpDefNames;
            }
        }

        /// <summary>
        /// Events that are eligible to be picked as a top-level random event:
        /// not a follow-up of any other event, and either <c>activateAtStart</c> or
        /// a no-option random event. Per-event validity (faction/settlement gating, weight, etc.)
        /// is still checked at the call site.
        /// </summary>
        public static List<FCEventDef> RandomRollableEvents
        {
            get
            {
                if (_cachedRandomRollableEvents is null)
                {
                    HashSet<string> followUps = EventFollowUpDefNames;
                    _cachedRandomRollableEvents = new List<FCEventDef>();
                    foreach (FCEventDef def in DefDatabase<FCEventDef>.AllDefsListForReading)
                    {
                        if (followUps.Contains(def.defName)) continue;
                        if (def.activateAtStart || (def.isRandomEvent && def.options.Count == 0))
                        {
                            _cachedRandomRollableEvents.Add(def);
                        }
                    }
                }
                return _cachedRandomRollableEvents;
            }
        }

        /// <summary>
        /// All events with <c>isRandomEvent == true</c>. Used by the random-event picker; per-event
        /// validity (settings toggle, stat ranges, settlement state, etc.) is still applied at the call site.
        /// </summary>
        public static List<FCEventDef> AllRandomEventDefs
        {
            get
            {
                if (_cachedAllRandomEventDefs is null)
                {
                    _cachedAllRandomEventDefs = new List<FCEventDef>();
                    foreach (FCEventDef def in DefDatabase<FCEventDef>.AllDefsListForReading)
                    {
                        if (def.isRandomEvent) _cachedAllRandomEventDefs.Add(def);
                    }
                }
                return _cachedAllRandomEventDefs;
            }
        }

        /// <summary>
        /// defNames of events whose reachable chain (following <c>eventFollows</c> / <c>followingEvent</c>
        /// / <c>followingEvent2</c>) contains at least one event with options. A def with its own options
        /// is trivially tainted. Used to gate chains when <c>FCSettings.disableEventsWithOptions</c> is on.
        /// </summary>
        public static HashSet<string> EventDefNamesWithOptionsInChain
        {
            get
            {
                if (_cachedEventDefNamesWithOptionsInChain is null)
                {
                    _cachedEventDefNamesWithOptionsInChain = new HashSet<string>();
                    foreach (FCEventDef def in DefDatabase<FCEventDef>.AllDefsListForReading)
                    {
                        if (ChainHasOptions(def, new HashSet<FCEventDef>()))
                            _cachedEventDefNamesWithOptionsInChain.Add(def.defName);
                    }
                }
                return _cachedEventDefNamesWithOptionsInChain;
            }
        }

        private static bool ChainHasOptions(FCEventDef def, HashSet<FCEventDef> visited)
        {
            if (def is null || !visited.Add(def)) return false;
            if (def.options is object && def.options.Count > 0) return true;
            if (!def.HasFollowUp) return false;
            if (ChainHasOptions(def.followingEvent, visited)) return true;
            if (def.splitEventFollows && ChainHasOptions(def.followingEvent2, visited)) return true;
            return false;
        }

        /// <summary>
        /// Every event def that can appear in the queue as part of a <i>multi-step</i> random chain:
        /// each random root that actually leads to a follow-up, plus the full closure of its
        /// <c>eventFollows</c> targets and option <c>successEvent</c> / <c>failEvent</c> targets.
        /// Used by the <c>blockEventsDuringChain</c> setting to detect an in-progress chain. One-shot
        /// random events (no follow-up) are intentionally excluded so they never block new roots.
        /// </summary>
        public static HashSet<FCEventDef> RandomChainMemberDefs
        {
            get
            {
                if (_cachedRandomChainMemberDefs is null)
                {
                    _cachedRandomChainMemberDefs = new HashSet<FCEventDef>();
                    foreach (FCEventDef root in AllRandomEventDefs)
                    {
                        if (!LeadsToFollowUp(root)) continue; // one-shot roots never block
                        CollectChainMembers(root, _cachedRandomChainMemberDefs);
                    }
                }
                return _cachedRandomChainMemberDefs;
            }
        }

        // "multi-step" = can actually spawn a follow-up (an auto-follow with a target, or an option
        // with a success/fail event). An options-only event with no follow-up does not count.
        private static bool LeadsToFollowUp(FCEventDef def)
        {
            if (def.HasFollowUp && (def.followingEvent != null ||
                (def.splitEventFollows && def.followingEvent2 != null))) return true;
            if (def.options != null)
            {
                foreach (FCOptionDef opt in def.options)
                    if (opt.successEvent != null || opt.failEvent != null) return true;
            }
            return false;
        }

        // acc doubles as the visited set: HashSet.Add returns false if the def is already present,
        // which both breaks cycles and skips already-traversed shared subtrees.
        private static void CollectChainMembers(FCEventDef def, HashSet<FCEventDef> acc)
        {
            if (def is null || !acc.Add(def)) return;
            if (def.HasFollowUp)
            {
                CollectChainMembers(def.followingEvent, acc);
                if (def.splitEventFollows) CollectChainMembers(def.followingEvent2, acc);
            }
            if (def.options != null)
            {
                foreach (FCOptionDef opt in def.options)
                {
                    CollectChainMembers(opt.successEvent, acc);
                    CollectChainMembers(opt.failEvent, acc);
                }
            }
        }

        /// <summary>
        /// Maps every event def in a cooldown-bearing random chain back to that chain's root (the def
        /// that declares <c>cooldownTicks</c>). One-shot cooldown roots map to themselves. Returns null
        /// for defs not in any such chain. Used to defer the cooldown stamp to the last chain step that
        /// fires (= resolves), so a chain's cooldown window effectively starts when the chain concludes.
        /// </summary>
        public static FCEventDef ChainCooldownRootOf(FCEventDef member)
        {
            if (member is null) return null;
            if (_cachedChainCooldownRootByMember is null)
            {
                _cachedChainCooldownRootByMember = new Dictionary<FCEventDef, FCEventDef>();
                foreach (FCEventDef root in AllRandomEventDefs)
                {
                    if (root.cooldownTicks <= 0) continue;
                    HashSet<FCEventDef> members = new HashSet<FCEventDef>();
                    CollectChainMembers(root, members);
                    foreach (FCEventDef m in members)
                        if (!_cachedChainCooldownRootByMember.ContainsKey(m)) // first writer wins; base chains are disjoint
                            _cachedChainCooldownRootByMember[m] = root;
                }
            }
            return _cachedChainCooldownRootByMember.TryGetValue(member, out FCEventDef r) ? r : null;
        }

        public static HashSet<BiomeResourceDef> BiomeResourceDefSet => _cachedBiomeResourceDefSet ??
                                                                       (_cachedBiomeResourceDefSet = new HashSet<BiomeResourceDef>(DefDatabase<BiomeResourceDef>.AllDefsListForReading));

        /// <summary>
        /// All buildable BuildingFCDefs (excludes "Empty" and "Construction") grouped by tech level,
        /// inner lists sorted by LabelCap.
        /// </summary>
        public static Dictionary<TechLevel, List<BuildingFCDef>> BuildingDefsByTechLevel
        {
            get
            {
                if (_cachedBuildingDefsByTechLevel is null)
                {
                    _cachedBuildingDefsByTechLevel = new Dictionary<TechLevel, List<BuildingFCDef>>();
                    foreach (BuildingFCDef building in DefDatabase<BuildingFCDef>.AllDefsListForReading)
                    {
                        if (building.defName == "Empty" || building.defName == "Construction") continue;
                        if (!_cachedBuildingDefsByTechLevel.TryGetValue(building.techLevel, out List<BuildingFCDef> list))
                        {
                            list = new List<BuildingFCDef>();
                            _cachedBuildingDefsByTechLevel[building.techLevel] = list;
                        }
                        list.Add(building);
                    }
                    foreach (var kvp in _cachedBuildingDefsByTechLevel)
                    {
                        kvp.Value.Sort((a, b) => string.Compare(a.LabelCap.RawText, b.LabelCap.RawText, StringComparison.OrdinalIgnoreCase));
                    }
                }
                return _cachedBuildingDefsByTechLevel;
            }
        }

        public static void InvalidateCache()
        {
            LogUtil.Message("Invalidating FactionCache...");
            _cachedPawnKindDefs = null;
            _cachedFields.Clear();
            _cachedRaceList = null;
            _cachedXenotypeList = null;
            _cachedViolentXenotypeList = null;
            _cachedAnimalKinds = null;
            _cachedCombatAnimalKinds = null;
            _cachedPackAnimalKinds = null;
            _cachedControllableMechKinds = null;
            _cachedMechGestationRecipes = null;
            _cachedXenotypeViolenceDict = null;
            _cachedFCPolicyDefs = null;
            _cachedFCPolicyDescs = null;
            _cachedUpgradeTrees = null;
            _cachedUpgradeDescendants = null;
            _cachedRequiredByMap = null;
            BuildingFCDef.ClearCompatibleSettlementCache();
            _cachedEventCategoryDefs = null;
            _cachedHostileMilitaryJobs = null;

            _cachedTechBarriers = null;
            _cachedTransportPods = null;

            _cachedResourceTypeDefs = null;
            _cachedPoolResourceTypeDefs = null;
            _cachedNonPoolResourceTypeDefs = null;
            _cachedTitheableResourceTypeDefs = null;
            _cachedSortedResourceTypeDefsForUI = null;
            _cachedPoliciesByCategory = null;
            _cachedAvailableWorldSettlementDefs = null;
            _cachedEventFollowUpDefNames = null;
            _cachedRandomRollableEvents = null;
            _cachedAllRandomEventDefs = null;
            _cachedEventDefNamesWithOptionsInChain = null;
            _cachedRandomChainMemberDefs = null;
            _cachedChainCooldownRootByMember = null;
            _cachedBiomeResourceDefSet = null;
            _cachedBuildingDefsByTechLevel = null;
            _cachedFactionConditionedSituationDefs = null;
            _cachedSettlementConditionedSituationDefs = null;

            CodexTab_Info.InvalidateTreeCache();

            InvalidateCustomXenotypeCache();
        }
        /* Custom xenotypes are actually expected to change while the game is loaded, and thus we may have to refresh that specific cache more frequently than the rest.
         * Hence, it gets its own function. */
        public static void InvalidateCustomXenotypeCache()
        {
            _cachedCustomXenotypeList = null;
            _cachedViolentCustomXenotypeList = null;
            _cachedCustomXenotypeDecoder = null;
            _cachedCustomXenotypeViolenceDict = null;

            _checkedForNonViolentXenos = false;
            _cachedNonViolentXenosExist = false;

            GeneValuationUtil.InvalidateCustomXenotypeCache();
        }
    }
}
