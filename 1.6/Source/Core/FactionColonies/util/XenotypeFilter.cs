using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FactionColonies.util
{
    public partial class XenotypeFilter : IExposable
    {
        private FactionDef faction;
        private FactionFC factionFc;
        private MilitaryFC militaryFC;
        private bool _initialized = false;
        public bool IsInitialized => _initialized;

        /* Weight collections. Mutations on the xeno/customXeno sets must invalidate
         * the OnlyNonViolentXenos cache; race mutations don't. The onChanged callback
         * passed to WeightedSet keeps that asymmetry out of the helper. */
        private WeightedSet<XenotypeDef> xenotypes;
        private WeightedSet<string> customXenotypes;
        private WeightedSet<ThingDef> races;

        public Dictionary<XenotypeDef, float> XenotypeWeights => xenotypes.Weights;
        public float XenotypeTotalWeight => xenotypes.TotalWeight;

        public Dictionary<string, float> CustomXenotypeWeights => customXenotypes.Weights;
        public float CustomXenotypeTotalWeight => customXenotypes.TotalWeight;

        public float XenoCompleteWeight => XenotypeTotalWeight + CustomXenotypeTotalWeight;

        public Dictionary<ThingDef, float> RaceWeights => races.Weights;
        public float RaceTotalWeight => races.TotalWeight;

        private bool checkedForNonViolent = false;
        private bool cachedHasOnlyNonViolent = false;
        public bool OnlyNonViolentXenos
        {
            get
            {
                if (!checkedForNonViolent)
                {
                    cachedHasOnlyNonViolent = true;
                    if (xenotypes.Count > 0 && xenotypes.Weights.Any(kvp => kvp.Value > 0 && !IsXenotypeNonViolent(kvp.Key)))
                    {
                        cachedHasOnlyNonViolent = false;
                    }
                    if (cachedHasOnlyNonViolent && customXenotypes.Count > 0)
                    {
                        foreach (string xenotypeName in customXenotypes.Weights.Keys)
                        {
                            if (customXenotypes.Get(xenotypeName) > 0 && !IsCustomXenotypeNonViolent(xenotypeName))
                            {
                                cachedHasOnlyNonViolent = false;
                                break;
                            }
                        }
                    }
                    if (XenoCompleteWeight == 0)
                    {
                        cachedHasOnlyNonViolent = false;
                    }
                    checkedForNonViolent = true;
                }
                return cachedHasOnlyNonViolent;
            }
        }

        private Dictionary<ThingDef, List<XenotypeDef>> raceXenoAssociations = new Dictionary<ThingDef, List<XenotypeDef>>();


        public XenotypeFilter()
        {
            xenotypes = new WeightedSet<XenotypeDef>(InvalidateNonViolentCache);
            customXenotypes = new WeightedSet<string>(InvalidateNonViolentCache);
            races = new WeightedSet<ThingDef>();
        }

        public XenotypeFilter(FactionFC factionFc) : this()
        {
            LogUtil.Message("Creating new XenotypeFilter");
            this.factionFc = factionFc;
            militaryFC = factionFc.military;
            faction = FindFC.EmpireFactionDef;
        }

        private void InvalidateNonViolentCache()
        {
            checkedForNonViolent = false;
        }

        public void FinalizeInit(FactionFC factionFc)
        {
            this.factionFc = factionFc;
            militaryFC = factionFc.military;
            faction = FindFC.EmpireFactionDef;
            LogUtil.Message("XenotypeFilter FinalizeInit");

            if (XenoCompleteWeight == 0)
            {
                InitializeXenotypes();
            }
            if (RaceTotalWeight == 0)
            {
                InitializeRaceWeights();
            }

            RefreshPawnGroupMakers();
            PawnKindTemplateUtil.FixupPawnKindDefs(factionFc);
            _initialized = true;
        }

        /* Public weight mutators. Forward to the appropriate WeightedSet, which handles
         * null-checking, dirty-cache invalidation, and the OnlyNonViolentXenos callback. */
        public void AddXenotypeWithWeight(XenotypeDef xenotype, float weight) => xenotypes.Set(xenotype, weight);
        public void AddCustomXenotypeWithWeight(CustomXenotype xenotype, float weight)
        {
            if (xenotype is null) return;
            customXenotypes.Set(xenotype.name, weight);
        }
        public void AddRaceWithWeight(ThingDef race, float weight) => races.Set(race, weight);

        public bool RemoveXenotype(XenotypeDef xenotype) => xenotypes.Remove(xenotype);
        public bool RemoveCustomXenotype(string xenotype) => customXenotypes.Remove(xenotype);
        public bool RemoveRace(ThingDef race) => races.Remove(race);

        public void ClearXenotypeWeights() => xenotypes.Clear();
        public void ClearCustomXenotypeWeights() => customXenotypes.Clear();
        public void ClearRaceWeights() => races.Clear();

        public void CullXenotypeWeights()
        {
            // If the sum of the xenotypes is 0, then we've found ourselves in an invalid configuration. Re-enable all xenotypes.
            if (XenoCompleteWeight == 0)
            {
                LogUtil.Warning($"XenoCompleteWeight == 0 in CullXenotypeWeights. Re-enabling all xenotypes.");
                InitializeXenotypeWeights();
                InitializeCustomXenotypeWeights();
            }
            else
            {
                xenotypes.Cull();
            }
        }
        public void CullCustomXenotypeWeights()
        {
            if (XenoCompleteWeight == 0)
            {
                LogUtil.Warning($"XenoCompleteWeight == 0 in CullCustomXenotypeWeights. Re-enabling all xenotypes.");
                InitializeXenotypeWeights();
                InitializeCustomXenotypeWeights();
            }
            else
            {
                customXenotypes.Cull();
            }
        }
        public void CullRaceWeights()
        {
            if (RaceTotalWeight == 0)
            {
                LogUtil.Warning($"RaceTotalWeight == 0 in CullRaceWeights. Re-enabling all races.");
                InitializeRaceWeights();
            }
            else
            {
                races.Cull();
            }
        }
        public void CullWeights()
        {
            CullXenotypeWeights();
            CullCustomXenotypeWeights();
            CullRaceWeights();

            RefreshPawnGroupMakers();
        }
        public void DebugPrintXenotypeWeights()
        {
            foreach (XenotypeDef def in FactionCache.XenotypeDefs)
            {
                LogUtil.Message($"Xenotype: {def.LabelCap}, Weight: {GetXenotypeWeight(def, true)}");
            }
        }
        public void DebugPrintCustomXenotypeWeights()
        {
            foreach (CustomXenotype xeno in FactionCache.CustomXenotypes)
            {
                LogUtil.Message($"Custom Xenotype: {xeno.name}, Weight: {GetCustomXenotypeWeight(xeno.name, true)}");
            }
        }
        public void DebugPrintRaceWeights()
        {
            foreach (ThingDef race in FactionCache.HumanlikeRaces)
            {
                LogUtil.Message($"Race: {race.LabelCap}, Weight: {GetRaceWeight(race, true)}");
            }
        }
        public float GetXenotypeWeight(XenotypeDef xenotype, bool debug = false)
        {
            if (xenotypes.ContainsKey(xenotype))
            {
                return xenotypes.Get(xenotype);
            }
            if (debug)
            {
                LogUtil.Message($"Xenotype {xenotype.LabelCap} not present in the weights dictionary");
            }
            return 0f;
        }
        public float GetCustomXenotypeWeight(string xenotype, bool debug = false)
        {
            if (customXenotypes.ContainsKey(xenotype))
            {
                return customXenotypes.Get(xenotype);
            }
            if (debug)
            {
                LogUtil.Message($"Custom Xenotype {xenotype} not present in the weights dictionary");
            }
            return 0f;
        }
        public float GetRaceWeight(ThingDef race, bool debug = false)
        {
            if (races.ContainsKey(race))
            {
                return races.Get(race);
            }
            if (debug)
            {
                LogUtil.Message($"Race {race.LabelCap} not present in the weights dictionary");
            }
            return 0f;
        }
        public float GetXenotypeChance(XenotypeDef xenotype) => xenotypes.ChanceOf(xenotype, XenoCompleteWeight);
        public float GetCustomXenotypeChance(string xenotype) => customXenotypes.ChanceOf(xenotype, XenoCompleteWeight);
        public float GetRaceChance(ThingDef race) => races.ChanceOf(race);

        public void ValidateCustomXenotypes()
        {
            FactionCache.InvalidateCustomXenotypeCache();
            List<string> customs = customXenotypes.Weights.Keys.ToList();
            if (customs.Count > 0)
            {
                foreach (string xeno in customs)
                {
                    if (!FactionCache.CustomXenotypesDecoder.ContainsKey(xeno))
                    {
                        RemoveCustomXenotype(xeno);
                    }
                }
            }
        }
        private void InitializeXenotypeWeights(bool initAllTypes = true)
        {
            ClearXenotypeWeights();
            if (initAllTypes)
            {
                foreach (XenotypeDef xenotype in FactionCache.XenotypeDefs)
                {
                    if (xenotype.IsXenotypeWithLabel() && xenotype != XenotypeDefOf.Baseliner)
                    {
                        AddXenotypeWithWeight(xenotype, 1);
                    }
                }
            }
            // Always include Baseliner xenotype as a default.
            if (!xenotypes.ContainsKey(XenotypeDefOf.Baseliner))
            {
                AddXenotypeWithWeight(XenotypeDefOf.Baseliner, 1);
            }

            if (XenotypeTotalWeight == 0)
            {
                LogUtil.Error("No enabled xenotypes after InitializeXenotypeWeights()!");
            }
        }
        private void InitializeCustomXenotypeWeights(bool initAllTypes = true)
        {
            ClearCustomXenotypeWeights();
            if (initAllTypes && FactionCache.CustomXenotypes.Count > 0)
            {
                foreach (CustomXenotype xenotype in FactionCache.CustomXenotypes)
                {
                    AddCustomXenotypeWithWeight(xenotype, 1);
                }
            }
        }
        private void InitializeRaceWeights(bool initAllTypes = true)
        {
            ClearRaceWeights();
            if (initAllTypes)
            {
                foreach (ThingDef race in FactionCache.HumanlikeRaces)
                {
                    if (race != ThingDefOf.Human)
                    {
                        AddRaceWithWeight(race, 1);
                    }
                }
            }
            if (!races.ContainsKey(ThingDefOf.Human))
            {
                AddRaceWithWeight(ThingDefOf.Human, 1);
            }

            if (RaceTotalWeight == 0)
            {
                LogUtil.Error("No enabled races after InitializeRaceWeights()!");
            }
        }

        private void InitializeXenotypes(bool initAllTypes = true)
        {
            if (!ModsConfig.BiotechActive)
            {
                LogUtil.Message("Biotech not present. Bailing out of InitializeXenotypes");
                return;
            }
            LogUtil.Message("Initializing Xenotype Weights in XenotypeFilter...");
            InitializeXenotypeWeights(initAllTypes);
            InitializeCustomXenotypeWeights(initAllTypes);
        }

        public bool IsValidXenotypeForRace(ThingDef inputRace, XenotypeDef xenotype)
        {
            /* If the race is the default Human, then only reject the xenotype if it is associated with a non-human race.
             *   Meant to handle mods that add xenotypes for HAR races. */
            if (inputRace == ThingDefOf.Human)
            {
                /* Always allow Baseliner for Humans. */
                if (xenotype == XenotypeDefOf.Baseliner)
                {
                    return true;
                }
                if (raceXenoAssociations.Count > 0)
                {
                    foreach (ThingDef race in raceXenoAssociations.Keys)
                    {
                        if (race == ThingDefOf.Human)
                        {
                            continue;
                        }
                        if (raceXenoAssociations[race].Contains(xenotype))
                        {
                            return false;
                        }
                    }
                }
                return true;
            }
            /* If the race is NOT the default Human, then only accept the xenotype if it is associated with the given race */
            else
            {
                if (raceXenoAssociations.ContainsKey(inputRace))
                {
                    return raceXenoAssociations[inputRace].Contains(xenotype);
                }
                /* Race has no xenotype associations (common for HAR races that predate Biotech).
                 * Only allow Baseliner as a safe default. */
                return xenotype == XenotypeDefOf.Baseliner;
            }
        }
        public bool IsValidCustomXenotypeForRace(ThingDef race, string xenotype)
        {
            /* As far as I'm aware, you can't associate custom xenotypes with non-human races, even with HAR.
             * But just in case I'm wrong, or there's some other way around this, I've included this function as an
             * easy way to rectify the custom xenotype validity check.
             * For now, though, we simply return TRUE if the race is Human, and false otherwise. */
            return race == ThingDefOf.Human;
        }
        public bool IsValidXenotypeForRequest(PawnGenerationRequest request, XenotypeDef xenotype)
        {
            /* If the xenotype isn't even enabled in the filter, then exit now */
            if (xenotypes.Get(xenotype) <= 0)
            {
                return false;
            }
            if (!IsValidXenotypeForRace(request.KindDef.race, xenotype))
            {
                return false;
            }
            bool needsViolence = request.MustBeCapableOfViolence;
            if (needsViolence && FactionCache.XenotypeIsNonViolent(xenotype))
            {
                return false;
            }
            if (PawnKindXenotypeChance(request.KindDef, xenotype) <= 0)
            {
                return false;
            }
            if (!CanGeneListDoRequiredWork(request.KindDef.requiredWorkTags, xenotype.genes))
            {
                return false;
            }

            return true;
        }
        public bool IsValidCustomXenotypeForRequest(PawnGenerationRequest request, string xenotypeName)
        {
            /* If the xenotype isn't even enabled in the filter, then exit now */
            if (customXenotypes.Get(xenotypeName) <= 0)
            {
                return false;
            }
            if (!IsValidCustomXenotypeForRace(request.KindDef.race, xenotypeName))
            {
                return false;
            }
            bool needsViolence = request.MustBeCapableOfViolence
                || (request.KindDef.weaponTags != null && request.KindDef.weaponTags.Count > 0)
                || (request.KindDef.requiredWorkTags & WorkTags.Violent) != WorkTags.None;
            if (needsViolence && FactionCache.CustomXenotypeIsNonViolent(xenotypeName))
            {
                return false;
            }
            if (!FactionCache.CustomXenotypesDecoder.TryGetValue(xenotypeName, out CustomXenotype xenotype))
            {
                return false;
            }
            if (!CanGeneListDoRequiredWork(request.KindDef.requiredWorkTags, xenotype.genes))
            {
                return false;
            }
            return true;
        }

        public static bool AreGenesNonViolent(List<GeneDef> genes)
        {
            if (!CanGeneListDoViolentWork(genes))
            {
                return true;
            }
            foreach (GeneDef gene in genes)
            {
                if (gene.statFactors != null)
                {
                    foreach (var statModifier in gene.statFactors)
                    {
                        // Check for severely reduced combat stats
                        if ((statModifier.stat == StatDefOf.ShootingAccuracyPawn ||
                             statModifier.stat == StatDefOf.MeleeHitChance ||
                             statModifier.stat == StatDefOf.MeleeDodgeChance) &&
                            statModifier.value < 0.5f)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        public static bool CanGeneListDoRequiredWork(WorkTags requiredTags, List<GeneDef> genes)
        {
            bool canDoWork = true;
            if (requiredTags != WorkTags.None && genes != null && genes.Count > 0)
            {
                WorkTags geneListDisabledTags = WorkTags.None;
                foreach (GeneDef gene in genes)
                {
                    geneListDisabledTags |= gene.disabledWorkTags;
                }
                if ((geneListDisabledTags & requiredTags) != WorkTags.None)
                {
                    canDoWork = false;
                }
            }
            return canDoWork;
        }
        public static bool CanGeneListDoViolentWork(List<GeneDef> genes)
        {
            if (!CanGeneListDoRequiredWork(WorkTags.Violent, genes))
            {
                return false;
            }
            if (!CanGeneListDoRequiredWork(WorkTags.AllWork, genes))
            {
                return false;
            }
            return true;
        }
        public static bool IsXenotypeNonViolent(XenotypeDef xenotype)
        {
            if (xenotype?.canGenerateAsCombatant == false)
            {
                return true;
            }
            if (xenotype?.genes is null)
            {
                return false;
            }

            // Check for genes that explicitly reduce combat effectiveness
            if (AreGenesNonViolent(xenotype.genes))
            {
                return true;
            }

            // For now, assume most xenotypes aren't non-violent unless specifically flagged
            return false;
        }
        public static bool IsCustomXenotypeNonViolent(string xenotypeName)
        {
            CustomXenotype xenotype = FactionCache.GetCustomXenotype(xenotypeName);
            if (xenotype?.genes is null) return false;

            // Check for genes that explicitly reduce combat effectiveness
            if (AreGenesNonViolent(xenotype.genes))
            {
                return true;
            }

            // For now, assume most xenotypes aren't non-violent unless specifically flagged
            return false;
        }

        public void ResetToAllXenotypes()
        {
            InitializeXenotypes();
            RefreshPawnGroupMakers();
        }
        public void ResetToBaselinerXenotypeOnly()
        {
            InitializeXenotypes(false);
            RefreshPawnGroupMakers();
        }
        public void ResetToAllRaces()
        {
            InitializeRaceWeights();
            RefreshPawnGroupMakers();
        }
        public void ResetToHumanRaceOnly()
        {
            InitializeRaceWeights(false);
            RefreshPawnGroupMakers();
        }

        /* Scans AllPawnKindDefs to build the raceXenoAssociations dictionary.
         * This preserves HAR xenotype-race association info without populating pawnGroupMakers. */
        private void BuildRaceXenoAssociations()
        {
            raceXenoAssociations.Clear();
            foreach (ThingDef race in races.Weights.Keys)
            {
                List<XenotypeDef> associatedXenotypes = new List<XenotypeDef>();
                foreach (PawnKindDef pawnKind in FactionCache.AllPawnKindDefs)
                {
                    if (pawnKind.race != race || pawnKind.xenotypeSet == null) continue;
                    for (int i = 0; i < pawnKind.xenotypeSet.Count; i++)
                    {
                        if (pawnKind.xenotypeSet[i].chance > 0)
                        {
                            associatedXenotypes.Add(pawnKind.xenotypeSet[i].xenotype);
                        }
                    }
                }
                if (associatedXenotypes.Count > 0)
                {
                    raceXenoAssociations[race] = associatedXenotypes.Distinct().ToList();
                }
            }
        }
        private float PawnKindXenotypeChance(PawnKindDef def, XenotypeDef xenotype)
        {
            float remainingChance = 1f;
            int xenoCount = def?.xenotypeSet?.Count ?? 0;
            if (xenoCount > 0)
            {
                for (int i = 0; i < def.xenotypeSet.Count; i++)
                {
                    if (def.xenotypeSet[i].xenotype == xenotype)
                    {
                        return def.xenotypeSet[i].chance;
                    }
                    else
                    {
                        remainingChance -= def.xenotypeSet[i].chance;
                    }
                }
            }
            // Pawnkinds that have a xenotype in their xenotypeSet that is disallowed in the xenotype filter should have been culled by this point.
            //   So we're going to assume that every xenotype we see in the set is one that would be valid.
            int numXenosRemaining = xenotypes.Count - xenoCount;
            if (numXenosRemaining <= 0)
            {
                return remainingChance;
            }
            else
            {
                return remainingChance / (float)(numXenosRemaining);
            }
        }
        private void ReweightPawnGenOptionsForRace(List<PawnGenOption> options, ThingDef race)
        {
            if (races.ContainsKey(race))
            {
                float raceWeight = races.Get(race);
                float numOptionsForRace = options.Count((PawnGenOption op) => op.kind.race == race);
                float newWeight = numOptionsForRace == 0 ? 0 : raceWeight / numOptionsForRace;

                foreach (PawnGenOption op in options)
                {
                    if (op.kind.race == race)
                        op.selectionWeight = newWeight;
                }
            }
        }
        private void ReweightPawnGroupMakers()
        {
            foreach (ThingDef race in races.Weights.Keys)
            {
                ReweightPawnGenOptionsForRace(faction.pawnGroupMakers[0].options, race);
                ReweightPawnGenOptionsForRace(faction.pawnGroupMakers[1].options, race);
                ReweightPawnGenOptionsForRace(faction.pawnGroupMakers[1].guards, race);
                ReweightPawnGenOptionsForRace(faction.pawnGroupMakers[1].traders, race);
                ReweightPawnGenOptionsForRace(faction.pawnGroupMakers[2].options, race);
                ReweightPawnGenOptionsForRace(faction.pawnGroupMakers[3].options, race);
            }
        }
        private void ValidatePawnGroupMakers()
        {
            if (!faction.pawnGroupMakers[0].options.Any())
                LogUtil.Error("RefreshPawnGroupMakers: combat pawnGroupMaker has no options after template population");
            if (!faction.pawnGroupMakers[1].traders.Any())
                LogUtil.Error("RefreshPawnGroupMakers: trader pawnGroupMaker has no traders after template population");
            if (!faction.pawnGroupMakers[1].carriers.Any())
                LogUtil.Error("RefreshPawnGroupMakers: trader pawnGroupMaker has no carriers after pack-animal population");
            if (!faction.pawnGroupMakers[3].options.Any())
                LogUtil.Error("RefreshPawnGroupMakers: peaceful pawnGroupMaker has no options after template population");
            if (faction.baseTraderKinds is null || !faction.baseTraderKinds.Any())
                LogUtil.Error("RefreshPawnGroupMakers: faction has no baseTraderKinds after refresh");
            DiagnoseCarrierForActiveColony();
        }

        /* Independent check against the player's active colony biome specifically. If no carrier is
           valid there, every trade caravan to that colony fails ("no usable PawnGroupMakers for Trader").
           Skipped silently when no player home map is loaded yet (e.g. during world gen / early load).
           Exception-safe: a diagnostic must never break a refresh. */
        private void DiagnoseCarrierForActiveColony()
        {
            try
            {
                List<PawnGenOption> carriers = faction.pawnGroupMakers[1].carriers;
                if (carriers is null || !carriers.Any()) return; // empty case already reported above

                Map colony = (Find.CurrentMap is object && Find.CurrentMap.IsPlayerHome)
                    ? Find.CurrentMap
                    : Find.AnyPlayerHomeMap;
                if (colony is null) return; // no active colony loaded; nothing to check

                BiomeDef biome = colony.Biome;
                if (biome is null) return;

                bool anyValid = carriers.Any(c =>
                    c.kind is object && c.kind.race is object && biome.IsPackAnimalAllowed(c.kind.race));

                if (!anyValid)
                    LogUtil.Warning("RefreshPawnGroupMakers: none of the trader carriers are allowed in the "
                        + "active colony's biome (" + biome.defName + ") - trade caravans to it will fail vanilla "
                        + "CanGenerateFrom (\"no usable PawnGroupMakers for Trader\").");
                else
                    LogUtil.Message("RefreshPawnGroupMakers: trader carriers cover the active colony biome ("
                        + biome.defName + ").");
            }
            catch (Exception e)
            {
                LogUtil.Warning("DiagnoseCarrierForActiveColony threw, skipping active-colony carrier diagnostic: " + e);
            }
        }
        private void SetPawnGroupMakers()
        {
            BuildRaceXenoAssociations();

            TechLevel techLevel = factionFc.techLevel;
            foreach (ThingDef race in races.Weights.Keys)
            {
                List<PawnKindDef> clones = PawnKindTemplateUtil.GetOrCreateClonesForRace(race, techLevel);
                float raceWeight = races.Get(race);

                foreach (PawnKindDef clone in clones)
                {
                    // Each list gets its OWN PawnGenOption instance: ReweightPawnGroupMakers rewrites
                    // selectionWeight per list, so a shared object would have the last-written list's
                    // weight leak into every other list it was added to.
                    PawnGenOption Opt() => new PawnGenOption
                    {
                        kind = clone,
                        selectionWeight = raceWeight
                    };

                    if (clone.isFighter)
                    {
                        faction.pawnGroupMakers[0].options.Add(Opt()); // Combat
                        faction.pawnGroupMakers[1].guards.Add(Opt()); // Trader guards
                        faction.pawnGroupMakers[2].options.Add(Opt()); // Settlement
                    }

                    if (clone.factionLeader)
                    {
                        faction.pawnGroupMakers[0].options.Add(Opt()); // Combat (for TryGenerateNewLeader)
                        faction.pawnGroupMakers[2].options.Add(Opt()); // Settlement
                    }

                    if (clone.trader)
                    {
                        faction.pawnGroupMakers[1].traders.Add(Opt());
                    }

                    // Non-fighter, non-trader: civilian types go in Trader options, Peaceful, and Settlement
                    if (!clone.isFighter && !clone.trader && !clone.factionLeader)
                    {
                        faction.pawnGroupMakers[1].options.Add(Opt()); // Trader
                        faction.pawnGroupMakers[3].options.Add(Opt()); // Peaceful
                        faction.pawnGroupMakers[2].options.Add(Opt()); // Settlement
                    }
                }
            }

            ReweightPawnGroupMakers();
        }
        internal void RefreshPawnGroupMakers()
        {
            if (faction is null || factionFc is null) return;
            LogUtil.Message("Refreshing pawn group makers");

            PawnKindTemplateUtil.InvalidateCache();

            // Clear existing pawn group makers
            faction.pawnGroupMakers = new List<PawnGroupMaker>
            {
                new PawnGroupMaker { kindDef = PawnGroupKindDefOf.Combat },
                new PawnGroupMaker { kindDef = PawnGroupKindDefOf.Trader },
                new PawnGroupMaker { kindDef = PawnGroupKindDefOf.Settlement },
                new PawnGroupMaker { kindDef = PawnGroupKindDefOf.Peaceful }
            };
            /* Reset the customXenotype cache, just in case xenotypes have been removed or added since the last time we were here */
            ValidateCustomXenotypes();

            if (RaceTotalWeight == 0)
            {
                InitializeRaceWeights();
            }
            if (XenoCompleteWeight == 0)
            {
                InitializeXenotypes();
            }

            SetPawnGroupMakers();

            // Add pack animals for caravans
            var packPool = factionFc?.animalFilter?.AllowedPackAnimals ?? FactionCache.AllPackAnimalKinds;
            foreach (PawnKindDef animalKindDef in packPool)
            {
                faction.pawnGroupMakers[1].carriers.Add(new PawnGenOption { kind = animalKindDef, selectionWeight = 1 });
            }

            ValidatePawnGroupMakers();

            /* MessageForce so we can observe whether Refresh ran on this session in
               player logs that have PrintDebug disabled — the entry-point log at
               line ~997 is Message-level and otherwise invisible. */
            LogUtil.MessageForce("RefreshPawnGroupMakers complete | "
                + "combat.opt=" + faction.pawnGroupMakers[0].options.Count
                + " trader.trd=" + faction.pawnGroupMakers[1].traders.Count
                + " trader.car=" + faction.pawnGroupMakers[1].carriers.Count
                + " trader.grd=" + faction.pawnGroupMakers[1].guards.Count
                + " settlement.opt=" + faction.pawnGroupMakers[2].options.Count
                + " peaceful.opt=" + faction.pawnGroupMakers[3].options.Count);

            RefreshMercenaryPawnGenOptions();
        }

        private void RefreshMercenaryPawnGenOptions()
        {
            if (militaryFC?.mercenarySquads == null || militaryFC?.mercenarySquads.Count == 0) return;

            foreach (MercenarySquadFC mercenarySquadFc in militaryFC.mercenarySquads)
            {
                List<Mercenary> newMercs = new List<Mercenary>();
                foreach (Mercenary mercenary in mercenarySquadFc.mercenaries)
                {
                    // For now, keep existing mercenaries but could be updated to use xenotype system
                    newMercs.Add(mercenary);
                }
                mercenarySquadFc.mercenaries = newMercs;
            }
        }

        public void GetFirstXenotypesForRequest(PawnGenerationRequest request, out XenotypeDef xenotype, out CustomXenotype customXenotype)
        {
            XenotypeDef chosenXenotype = null;
            CustomXenotype chosenCustomXenotype = null;

            if (xenotypes.Count > 0)
            {
                foreach (XenotypeDef allowedXenotype in xenotypes.Weights.Keys)
                {
                    if (IsValidXenotypeForRequest(request, allowedXenotype))
                    {
                        chosenXenotype = allowedXenotype;
                        break;
                    }
                }
            }
            if (customXenotypes.Count > 0)
            {
                foreach (string allowedXenotype in customXenotypes.Weights.Keys)
                {
                    if (IsValidCustomXenotypeForRequest(request, allowedXenotype))
                    {
                        chosenCustomXenotype = FactionCache.GetCustomXenotype(allowedXenotype);
                        if (chosenCustomXenotype is object)
                        {
                            break;
                        }
                    }
                }
            }
            xenotype = chosenXenotype;
            customXenotype = chosenCustomXenotype;
        }
        public List<XenotypeDef> GetValidXenotypesForRequest(PawnGenerationRequest request)
        {
            List<XenotypeDef> output = new List<XenotypeDef>();
            if (xenotypes.Count == 0)
            {
                /* We really shouldn't ever end up in this case, but *just* in case, we've included a bail-out. */
                LogUtil.Error("XenotypeWeights.Count == 0 in GetValidXenotypesForRequest");
                return output;
            }
            foreach (XenotypeDef xenotype in xenotypes.Weights.Keys)
            {
                if (IsValidXenotypeForRequest(request, xenotype))
                {
                    output.Add(xenotype);
                }
            }
            return output;
        }
        public float GetTotalWeightForXenotypeList(List<XenotypeDef> xenotypeList)
        {
            float output = 0;
            if (xenotypeList is null || xenotypeList.Count == 0)
            {
                return 0;
            }
            foreach (XenotypeDef xenotype in xenotypeList)
            {
                output += GetXenotypeWeight(xenotype);
            }
            return output;
        }
        public List<string> GetValidCustomXenotypesForRequest(PawnGenerationRequest request)
        {
            List<string> output = new List<string>();
            if (customXenotypes.Count == 0)
            {
                return output;
            }
            foreach (string xenotype in customXenotypes.Weights.Keys)
            {
                if (IsValidCustomXenotypeForRequest(request, xenotype))
                {
                    output.Add(xenotype);
                }
            }
            return output;
        }
        public float GetTotalWeightForCustomXenotypeList(List<string> xenotypeList)
        {
            float output = 0;
            if (xenotypeList is null || xenotypeList.Count == 0)
            {
                return 0;
            }
            foreach (string xenotype in xenotypeList)
            {
                output += GetCustomXenotypeWeight(xenotype);
            }
            return output;
        }

        public void GetRandomXenotypeForRequest(PawnGenerationRequest request, out XenotypeDef xenotype, out CustomXenotype customXenotype)
        {
            XenotypeDef chosenXenotype = null;
            CustomXenotype chosenCustomXenotype = null;

            if (!ModsConfig.BiotechActive)
            {
                LogUtil.Message($"Biotech inactive or not installed. Returning null from GetRandomXenotypeForRequest");
                xenotype = null;
                customXenotype = null;
                return;
            }

            List<XenotypeDef> validXenotypes = GetValidXenotypesForRequest(request);
            List<string> validCustomXenotypes = GetValidCustomXenotypesForRequest(request);

            float cumulative = 0;
            float weightTotal = GetTotalWeightForXenotypeList(validXenotypes) + GetTotalWeightForCustomXenotypeList(validCustomXenotypes);
            float xenoRand = Rand.Value;

            if (validXenotypes.Count > 0)
            {
                foreach (XenotypeDef allowedXenotype in validXenotypes)
                {
                    float thisWeight = GetXenotypeWeight(allowedXenotype);
                    float thisChance = (cumulative + thisWeight) / weightTotal;

                    if (xenoRand < thisChance)
                    {
                        chosenXenotype = allowedXenotype;
                        break;
                    }
                    cumulative += thisWeight;
                }
            }
            if (chosenXenotype is null && validCustomXenotypes.Count > 0)
            {
                foreach (string allowedXenotype in validCustomXenotypes)
                {
                    float thisWeight = GetCustomXenotypeWeight(allowedXenotype);
                    float thisChance = (cumulative + thisWeight) / weightTotal;

                    if (xenoRand < thisChance)
                    {
                        chosenCustomXenotype = FactionCache.GetCustomXenotype(allowedXenotype);
                        if (chosenCustomXenotype is object)
                        {
                            break;
                        }
                    }
                    cumulative += thisWeight;
                }
            }

            if (chosenXenotype is null && chosenCustomXenotype is null)
            {
                /* Just in case there was some weird ordering issue, try to get the first xenotype in the allowed lists that would satisfy the request */
                GetFirstXenotypesForRequest(request, out chosenXenotype, out chosenCustomXenotype);
                /* If both are *still* null, then fall back onto baseliner */
                if (chosenXenotype is null && chosenCustomXenotype is null)
                {
                    LogUtil.Warning($"XenotypeFilter.GetRandomXenotypeForRequest failed to chose a random xenotype. Falling back onto baseliner");
                    chosenXenotype = XenotypeDefOf.Baseliner;
                }
            }

            xenotype = chosenXenotype;
            customXenotype = chosenCustomXenotype;
        }
        public ThingDef GetRandomRace()
        {
            ThingDef outputRace = null;
            float cumulative = 0;
            float weightTotal = RaceTotalWeight;
            float raceRand = Rand.Value;

            if (races.Count > 0)
            {
                foreach (ThingDef race in races.Weights.Keys)
                {
                    float thisWeight = races.Get(race);
                    if (thisWeight == 0)
                    {
                        continue;
                    }
                    if (raceRand < (cumulative + thisWeight) / weightTotal)
                    {
                        outputRace = race;
                        break;
                    }
                    cumulative += thisWeight;
                }
            }

            return outputRace;
        }

        /* Cordoned security-guard fields/methods/serialization live in
         * XenotypeFilter.SecurityGuards.cs (partial class). The partial method below is a
         * no-op unless that file provides an implementation. */
        partial void ExposeSecurityGuardsData();

        public void ExposeData()
        {
            xenotypes.Expose("xenotypeWeights", LookMode.Def);
            customXenotypes.Expose("customXenotypeWeights", LookMode.Value);
            races.Expose("raceWeights", LookMode.Def);
            ExposeSecurityGuardsData();
        }
    }
}
