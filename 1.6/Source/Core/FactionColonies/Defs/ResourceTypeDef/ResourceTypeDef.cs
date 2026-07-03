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
    public class ResourceTypeDef : Def
    {
        public string iconPath;
        public Color color = new Color(0.65f, 0.65f, 0.65f);
        /// <summary>Description with {FACTION}/{FACTION_TITLE} tokens and [b]/[i] emphasis markup resolved for display.</summary>
        public string FormattedDesc => description.Format();

        /* For all Allow-Blocklist pairs, the allowlist is processed first, and then the blocklist is used to shave off blocked elements */
        /* NOTE: The thingAllowList is treated as the ultimate source of truth for the Things it specifies. That is:
         *         - If a Thing's category isn't allowed, but the Thing satisfies all of its own requirements, then that Thing is allowed
         *         - If a Thing's category is allowed, but the Thing does *not* satisfy all of its own requirements, then that Thing is disallowed
         */
        List<ResourceThingDefRestriction> thingAllowList = new List<ResourceThingDefRestriction>();
        List<ThingDef> thingBlockList = new List<ThingDef>();

        List<ResourceThingCategoryDefRestriction> thingCategoryAllowList = new List<ResourceThingCategoryDefRestriction>();
        List<ThingCategoryDef> thingCategoryBlockList = new List<ThingCategoryDef>();

        List<StuffCategoryDef> stuffCategoryAllowList = new List<StuffCategoryDef>();
        List<StuffCategoryDef> stuffCategoryBlockList = new List<StuffCategoryDef>();

        /* biomeAllowList and biomeBlockList are mutually exclusive */
        List<BiomeResourceDef> biomeAllowList = new List<BiomeResourceDef>();
        List<BiomeResourceDef> biomeBlockList = new List<BiomeResourceDef>();

        /// <summary>
        /// Minimum tech level to access or produce this resource type.
        /// <para>If minTechLevel is not undefined, and the empire faction does not satisfy it, then the resource cannot be produced.</para>
        /// </summary>
        public TechLevel minTechLevel = TechLevel.Undefined;
        /// <summary>
        /// Maximum tech level to access or produce this resource type.
        /// <para>If maxTechLevel is not undefined, and the empire faction does not satisfy it, then the resource cannot be produced.</para>
        /// </summary>
        public TechLevel maxTechLevel = TechLevel.Undefined;
        /// <summary>
        /// A list of research projects required to unlock access to this resource type.
        /// </summary>
        public List<ResearchProjectDef> researchProjectDefs = new List<ResearchProjectDef>();
        /// <summary>
        /// The pawn skills associated with producing this resource type. May be empty.
        /// </summary>
        public List<SkillDef> associatedSkills = new List<SkillDef>();
        /// <summary>
        /// Indicates whether the resource type needs to meet both the techlevel AND researchProjectDefs requirements to become available.
        /// </summary>
        public bool needsAllResearchRequirements = true;
        /// <summary>
        /// Indicates whether or not this resource should accumulate a special point pool instead of actual objects.
        /// E.g. a research point pool, or a power pool.
        /// </summary>
        public bool isPoolResource = false;
        /// <summary>
        /// If true, this resource is automatically included in any WorldSettlementDef that has defaultResources set to true.
        /// </summary>
        public bool isDefaultResource = false;
        /// <summary>
        /// If false, this resource cannot generate tithes (excluded from tithe UI and tithe generation).
        /// Pool resources are never titheable regardless of this flag. Defaults to true.
        /// </summary>
        public bool canTithe = true;

        public bool CanTithe => canTithe && !isPoolResource;

        /// <summary>
        /// Default additive production bonus for this resource in biomes that don't specify one.
        /// </summary>
        public double defaultBiomeAdditive = 1;
        /// <summary>
        /// Default multiplier production bonus for this resource in biomes that don't specify one.
        /// </summary>
        public double defaultBiomeMultiplier = 1;

        /// <summary>
        /// The FCStatDef used for additive production bonuses from buildings, events, and policies.
        /// Aggregated via the stat system; value is added to the base production from biome/extensions.
        /// </summary>
        public FCStatDef productionAdditiveStat;
        /// <summary>
        /// The FCStatDef used for multiplicative production bonuses from buildings, events, and policies.
        /// Aggregated via the stat system; value multiplies the final production alongside biome multipliers.
        /// </summary>
        public FCStatDef productionMultiplierStat;

        /// <summary>
        /// When generating tithes for this resource, the count range is set to (titheMinCount, titheMaxCountBase + (titheMaxCountScaler * multiplier)) where "multiplier" is set within
        /// the code.
        /// </summary>
        public int titheMinCount = 1;
        /// <summary>
        /// When generating tithes for this resource, the count range is set to (titheMinCount, titheMaxCountBase + (titheMaxCountScaler * multiplier)) where "multiplier" is set within
        /// the code.
        /// </summary>
        public int titheMaxCountBase = 1;
        /// <summary>
        /// When generating tithes for this resource, the count range is set to (titheMinCount, titheMaxCountBase + (titheMaxCountScaler * multiplier)) where "multiplier" is set within
        /// the code.
        /// </summary>
        public int titheMaxCountScaler = 1;
        /// <summary>
        /// Weight applied to this resource's production when calculating a settlement's defense bonus.
        /// A value of 0 means the resource does not contribute to defense. A value of 1.0 means it contributes
        /// its full effectiveRawTotalProduction. Values greater or less than 1.0 scale the contribution accordingly.
        /// </summary>
        public float defenseWeight = 0f;

        /// <summary>
        /// Determines the order that the resource is listed in the UI. Lower values = ealier in the list. Ties are broken randomly.
        /// </summary>
        public int uiPriority = 10000;

        /// <summary>
        /// Cached trade filter used by <see cref="AllowsForTrade"/>. Built lazily on first access
        /// via <see cref="FilterResourceForTrade"/>, which ignores research/recipe/tech-level
        /// gates — so the cache is purely Def-driven and never goes stale relative to player
        /// research progression.
        /// </summary>
        private ThingFilter _tradeFilter;

        /// <summary>
        /// Returns true if this resource type's allow/block lists include the given ThingDef.
        /// Used for buy-side trade filtering: traders are willing to buy any item that fits
        /// the resource category, regardless of whether the player has researched the recipe
        /// or has the tech level to produce it.
        /// </summary>
        public bool AllowsForTrade(ThingDef thingDef)
        {
            if (_tradeFilter is null)
            {
                _tradeFilter = new ThingFilter();
                FilterResourceForTrade(_tradeFilter);
            }
            return _tradeFilter.Allows(thingDef);
        }

        private Texture2D iconLoaded;

        public Texture2D Icon
        {
            get
            {
                if (iconLoaded != null) return iconLoaded;

                if (!iconPath.NullOrEmpty())
                {
                    iconLoaded = ContentFinder<Texture2D>.Get(iconPath);
                }
                else
                {
                    LogUtil.Error("Failed to load icon for ResourceType: " + LabelCap + " at " + (iconPath ?? "nullPath") + "!");
                    iconLoaded = TexLoad.questionmark;
                }
                return iconLoaded;
            }
        }

        /* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
         * Pool helper functions
         * These functions are wrappers for ResourcePoolExtension functions, meant to make it easier to invoke the extension.
         * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
        public bool PoolResourceResetsAtTaxTime()
        {
            if (!isPoolResource)
            {
                LogUtil.Error($"Called PoolResourceResetsAtTaxTime() for non-pool resource {this.defName}");
                return false;
            }
            /* ConfigErrors already checked that ResourcePoolExtensions exists if isPoolResource is set, so
             * we won't bother with null-checking here. */
            return GetModExtension<ResourcePoolExtension>().ResetAtTaxTime();
        }
        public double PreAddToGlobalPool(double value)
        {
            if (!isPoolResource)
            {
                LogUtil.Error($"Called PreAddToGlobalPool() for non-pool resource {this.defName}");
                return value;
            }
            /* ConfigErrors already checked that ResourcePoolExtensions exists if isPoolResource is set, so
             * we won't bother with null-checking here. */
            return GetModExtension<ResourcePoolExtension>().PreAddToGlobalPool(value);
        }
        public void AddedToGlobalPool(double value)
        {
            if (!isPoolResource)
            {
                LogUtil.Error($"Called AddedToGlobalPool() for non-pool resource {this.defName}");
                return;
            }
            /* ConfigErrors already checked that ResourcePoolExtensions exists if isPoolResource is set, so
             * we won't bother with null-checking here. */
            GetModExtension<ResourcePoolExtension>().AddedToGlobalPool(value);
        }
        public IEnumerable<FloatMenuOption> GetFactionMenuFloatMenuOptions(ResourcePool pool)
        {
            if (!isPoolResource)
            {
                LogUtil.Error($"Called GetFactionMenuFloatMenuOptions() for non-pool resource {this.defName}");
                yield break;
            }
            /* ConfigErrors already checked that ResourcePoolExtensions exists if isPoolResource is set, so
             * we won't bother with null-checking here. */
            IEnumerable<FloatMenuOption> options = GetModExtension<ResourcePoolExtension>().GetFactionMenuFloatMenuOptions(pool);
            if (options != null)
            {
                foreach (FloatMenuOption option in options)
                {
                    yield return option;
                }
            }
        }
        public void DailyUpdate(ResourcePool pool)
        {
            if (!isPoolResource)
            {
                LogUtil.Error($"Called DailyUpdate() for non-pool resource {this.defName}");
                return;
            }
            /* ConfigErrors already checked that ResourcePoolExtensions exists if isPoolResource is set, so
             * we won't bother with null-checking here. */
            GetModExtension<ResourcePoolExtension>().DailyUpdate(pool);
        }
        /* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
         * End of Pool helper functions
         * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

        public bool ResourceTypeAllowedByTech(TechLevel techlevel)
        {
            bool meetsTechlevelReq = false;
            if ((minTechLevel == TechLevel.Undefined || techlevel >= minTechLevel) &&
                (maxTechLevel == TechLevel.Undefined || techlevel <= maxTechLevel))
            {
                meetsTechlevelReq = true;
            }
            bool meetsResearchReqs = true;
            if (researchProjectDefs.Count > 0)
            {
                foreach (ResearchProjectDef research in researchProjectDefs)
                {
                    if (!research.IsFinished)
                    {
                        meetsResearchReqs = false;
                        break;
                    }
                }
            }

            if (needsAllResearchRequirements)
            {
                return meetsResearchReqs && meetsTechlevelReq;
            }
            else
            {
                return meetsResearchReqs || meetsTechlevelReq;
            }
        }
        public double GetExtensionAdditives(PlanetTile tile, WorldSettlementFC settlement = null)
        {
            double add = 0;
            if (modExtensions?.Count > 0)
            {
                foreach (ResourceProductionExtension prod in modExtensions.OfType<ResourceProductionExtension>())
                {
                    add += prod.GetAdditiveBonus(tile, settlement);
                }
            }
            return add;
        }
        public double GetExtensionMultipliers(PlanetTile tile, WorldSettlementFC settlement = null)
        {
            double mult = 1;
            if (modExtensions?.Count > 0)
            {
                foreach (ResourceProductionExtension prod in modExtensions.OfType<ResourceProductionExtension>())
                {
                    mult *= prod.GetMultiplierBonus(tile, settlement);
                }
            }
            return mult;
        }

        /// <summary>
        /// Sums additive bonuses contributed by TileMutatorResourceExtensions on every mutator
        /// present on the given tile, filtered to this resource.
        /// </summary>
        public double GetMutatorAdditives(PlanetTile tile)
        {
            if (tile == PlanetTile.Invalid) return 0;
            IList<TileMutatorDef> mutators = tile.Tile?.Mutators;
            if (mutators.NullOrEmpty()) return 0;

            double add = 0;
            foreach (TileMutatorDef mut in mutators)
            {
                TileMutatorResourceExtension ext = mut?.GetModExtension<TileMutatorResourceExtension>();
                if (ext?.bonuses is null) continue;
                foreach (TileResourceBonus entry in ext.bonuses)
                {
                    if (entry.resource == this) add += entry.additive;
                }
            }
            return add;
        }

        /// <summary>
        /// Product of multiplier bonuses contributed by TileMutatorResourceExtensions on every
        /// mutator present on the given tile, filtered to this resource.
        /// </summary>
        public double GetMutatorMultipliers(PlanetTile tile)
        {
            if (tile == PlanetTile.Invalid) return 1;
            IList<TileMutatorDef> mutators = tile.Tile?.Mutators;
            if (mutators.NullOrEmpty()) return 1;

            double mult = 1;
            foreach (TileMutatorDef mut in mutators)
            {
                TileMutatorResourceExtension ext = mut?.GetModExtension<TileMutatorResourceExtension>();
                if (ext?.bonuses is null) continue;
                foreach (TileResourceBonus entry in ext.bonuses)
                {
                    if (entry.resource == this) mult *= entry.multiplier;
                }
            }
            return mult;
        }

        /// <summary>
        /// Sums additive bonuses contributed by a TileLandmarkResourceExtension on the tile's
        /// landmark (if any), filtered to this resource. Odyssey-gated via Tile.Landmark.
        /// </summary>
        public double GetLandmarkAdditives(PlanetTile tile)
        {
            if (tile == PlanetTile.Invalid) return 0;
            Landmark landmark = tile.Tile?.Landmark;
            if (landmark?.def is null) return 0;

            TileLandmarkResourceExtension ext = landmark.def.GetModExtension<TileLandmarkResourceExtension>();
            if (ext?.bonuses is null) return 0;

            double add = 0;
            foreach (TileResourceBonus entry in ext.bonuses)
            {
                if (entry.resource == this) add += entry.additive;
            }
            return add;
        }

        /// <summary>
        /// Product of multiplier bonuses contributed by a TileLandmarkResourceExtension on the
        /// tile's landmark (if any), filtered to this resource. Odyssey-gated via Tile.Landmark.
        /// </summary>
        public double GetLandmarkMultipliers(PlanetTile tile)
        {
            if (tile == PlanetTile.Invalid) return 1;
            Landmark landmark = tile.Tile?.Landmark;
            if (landmark?.def is null) return 1;

            TileLandmarkResourceExtension ext = landmark.def.GetModExtension<TileLandmarkResourceExtension>();
            if (ext?.bonuses is null) return 1;

            double mult = 1;
            foreach (TileResourceBonus entry in ext.bonuses)
            {
                if (entry.resource == this) mult *= entry.multiplier;
            }
            return mult;
        }

        /// <summary>
        /// Invokes <paramref name="onEntry"/> once per non-zero additive bonus contributed by any
        /// TileMutatorResourceExtension on the tile, filtered to this resource. Label falls back to
        /// the mutator's LabelCap if the entry doesn't supply one.
        /// </summary>
        public void ForEachMutatorAdditive(PlanetTile tile, Action<string, double> onEntry)
        {
            if (onEntry is null || tile == PlanetTile.Invalid) return;
            IList<TileMutatorDef> mutators = tile.Tile?.Mutators;
            if (mutators.NullOrEmpty()) return;

            foreach (TileMutatorDef mut in mutators)
            {
                TileMutatorResourceExtension ext = mut?.GetModExtension<TileMutatorResourceExtension>();
                if (ext?.bonuses is null) continue;
                foreach (TileResourceBonus entry in ext.bonuses)
                {
                    if (entry.resource != this) continue;
                    if (entry.additive == 0) continue;
                    string mlabel = entry.label.NullOrEmpty() ? mut.LabelCap.ToString() : entry.label;
                    onEntry(mlabel, entry.additive);
                }
            }
        }

        /// <summary>
        /// Invokes <paramref name="onEntry"/> once per non-1 multiplier bonus contributed by any
        /// TileMutatorResourceExtension on the tile, filtered to this resource.
        /// </summary>
        public void ForEachMutatorMultiplier(PlanetTile tile, Action<string, double> onEntry)
        {
            if (onEntry is null || tile == PlanetTile.Invalid) return;
            IList<TileMutatorDef> mutators = tile.Tile?.Mutators;
            if (mutators.NullOrEmpty()) return;

            foreach (TileMutatorDef mut in mutators)
            {
                TileMutatorResourceExtension ext = mut?.GetModExtension<TileMutatorResourceExtension>();
                if (ext?.bonuses is null) continue;
                foreach (TileResourceBonus entry in ext.bonuses)
                {
                    if (entry.resource != this) continue;
                    if (entry.multiplier == 1) continue;
                    string mlabel = entry.label.NullOrEmpty() ? mut.LabelCap.ToString() : entry.label;
                    onEntry(mlabel, entry.multiplier);
                }
            }
        }

        /// <summary>
        /// Invokes <paramref name="onEntry"/> once per non-zero additive bonus contributed by the
        /// tile's landmark's TileLandmarkResourceExtension, filtered to this resource.
        /// </summary>
        public void ForEachLandmarkAdditive(PlanetTile tile, Action<string, double> onEntry)
        {
            if (onEntry is null || tile == PlanetTile.Invalid) return;
            Landmark landmark = tile.Tile?.Landmark;
            if (landmark?.def is null) return;

            TileLandmarkResourceExtension ext = landmark.def.GetModExtension<TileLandmarkResourceExtension>();
            if (ext?.bonuses is null) return;

            foreach (TileResourceBonus entry in ext.bonuses)
            {
                if (entry.resource != this) continue;
                if (entry.additive == 0) continue;
                string llabel = entry.label.NullOrEmpty() ? landmark.def.LabelCap.ToString() : entry.label;
                onEntry(llabel, entry.additive);
            }
        }

        /// <summary>
        /// Invokes <paramref name="onEntry"/> once per non-1 multiplier bonus contributed by the
        /// tile's landmark's TileLandmarkResourceExtension, filtered to this resource.
        /// </summary>
        public void ForEachLandmarkMultiplier(PlanetTile tile, Action<string, double> onEntry)
        {
            if (onEntry is null || tile == PlanetTile.Invalid) return;
            Landmark landmark = tile.Tile?.Landmark;
            if (landmark?.def is null) return;

            TileLandmarkResourceExtension ext = landmark.def.GetModExtension<TileLandmarkResourceExtension>();
            if (ext?.bonuses is null) return;

            foreach (TileResourceBonus entry in ext.bonuses)
            {
                if (entry.resource != this) continue;
                if (entry.multiplier == 1) continue;
                string llabel = entry.label.NullOrEmpty() ? landmark.def.LabelCap.ToString() : entry.label;
                onEntry(llabel, entry.multiplier);
            }
        }

        /// <summary>
        /// Invokes <paramref name="onEntry"/> for each non-zero additive bonus contributed by
        /// ResourceProductionExtensions on this def, using per-source labels from ContributeToBreakdown.
        /// </summary>
        public void ForEachExtensionAdditive(PlanetTile tile, Action<string, double> onEntry)
        {
            if (onEntry is null || modExtensions is null || modExtensions.Count <= 0) return;
            foreach (ResourceProductionExtension ext in modExtensions.OfType<ResourceProductionExtension>())
            {
                ext.ContributeToBreakdown(tile, null,
                    (suffix, value, alabel) => { if (value != 0) onEntry(alabel, value); },
                    (suffix, value, alabel) => { });
            }
        }

        /// <summary>
        /// Invokes <paramref name="onEntry"/> for each non-1 multiplier bonus contributed by
        /// ResourceProductionExtensions on this def, using per-source labels from ContributeToBreakdown.
        /// </summary>
        public void ForEachExtensionMultiplier(PlanetTile tile, Action<string, double> onEntry)
        {
            if (onEntry is null || modExtensions is null || modExtensions.Count <= 0) return;
            foreach (ResourceProductionExtension ext in modExtensions.OfType<ResourceProductionExtension>())
            {
                ext.ContributeToBreakdown(tile, null,
                    (suffix, value, mlabel) => { },
                    (suffix, value, mlabel) => { if (value != 1) onEntry(mlabel, value); });
            }
        }

        public bool ResourceAllowedForBiome(BiomeResourceDef bdef)
        {
            if (biomeAllowList.Count > 0)
            {
                /* If an allowList is specified, then the resource is *only* available in those biomes. */
                return biomeAllowList.Contains(bdef);
            }
            else if (biomeBlockList.Count > 0)
            {
                return !(biomeBlockList.Contains(bdef));
            }
            /* A resource is allowed in all Biomes by default */
            return true;
        }
        public void FilterResource(ThingFilter filter, TechLevel techlevel = TechLevel.Undefined, ResourceFC resource = null)
        {
            /* Category Allow lists */
            foreach (ResourceThingCategoryDefRestriction thingCategoryRestriction in thingCategoryAllowList)
            {
                thingCategoryRestriction.SetFilter(filter, techlevel);
            }
            foreach (StuffCategoryDef stuffCategoryDef in stuffCategoryAllowList)
            {
                filter.SetAllow(stuffCategoryDef, true);
            }
            /* Category Block Lists */
            foreach (ThingCategoryDef thingCategoryDef in thingCategoryBlockList)
            {
                filter.SetAllow(thingCategoryDef, false);
            }
            foreach (StuffCategoryDef stuffCategoryDef in stuffCategoryBlockList)
            {
                filter.SetAllow(stuffCategoryDef, false);
            }
            // Handle Things after the categories. This allows for more fine-grained control over when
            // individual things become available
            foreach (ResourceThingDefRestriction thingDefRestriction in thingAllowList)
            {
                thingDefRestriction.SetFilter(filter, techlevel);
            }
            /* Block lists */
            foreach (ThingDef thingDef in thingBlockList)
            {
                filter.SetAllow(thingDef, false);
            }

            /* Check for any ResourceExtensions, and process them now. */
            if (modExtensions != null)
            {
                foreach (ResourceFilterExtension ext in modExtensions.OfType<ResourceFilterExtension>())
                {
                    ext.SetFilter(filter, techlevel, resource);
                }
            }
        }

        /// <summary>
        /// Populates a ThingFilter with all items this resource can produce, ignoring research completion
        /// state and tech level checks. Used by the Codex to show a complete reference list.
        /// Also populates a restrictions dictionary with per-item tech/research requirements for display.
        /// </summary>
        public void FilterResourceForCodex(ThingFilter filter, out Dictionary<ThingDef, TitheRestrictionInfo> restrictions)
        {
            restrictions = new Dictionary<ThingDef, TitheRestrictionInfo>();

            // Category allow lists — allow everything, record category-level restrictions
            foreach (ResourceThingCategoryDefRestriction catRestriction in thingCategoryAllowList)
            {
                filter.SetAllow(catRestriction.thingCategoryDef, true);

                // Record per-item restriction info from the category
                if (catRestriction.hasDefinedTechLevel || catRestriction.hasResearchDefs)
                {
                    foreach (ThingDef thing in catRestriction.thingCategoryDef.DescendantThingDefs)
                    {
                        TitheRestrictionInfo info = new TitheRestrictionInfo();
                        info.minTechLevel = catRestriction.minTechLevel;
                        info.researchProjects = catRestriction.researchProjectDefs;
                        restrictions[thing] = info;
                    }
                }
            }

            // Stuff category allow lists
            foreach (StuffCategoryDef stuffCat in stuffCategoryAllowList)
                filter.SetAllow(stuffCat, true);

            // Category block lists
            foreach (ThingCategoryDef catBlock in thingCategoryBlockList)
                filter.SetAllow(catBlock, false);
            foreach (StuffCategoryDef stuffBlock in stuffCategoryBlockList)
                filter.SetAllow(stuffBlock, false);

            // Thing allow lists — override category restrictions with thing-specific ones
            foreach (ResourceThingDefRestriction thingRestriction in thingAllowList)
            {
                filter.SetAllow(thingRestriction.thingDef, true);

                TitheRestrictionInfo info = new TitheRestrictionInfo();
                info.minTechLevel = thingRestriction.minTechLevel;
                info.researchProjects = thingRestriction.researchProjectDefs;
                restrictions[thingRestriction.thingDef] = info;
            }

            // Thing block lists
            foreach (ThingDef thingBlock in thingBlockList)
            {
                filter.SetAllow(thingBlock, false);
                restrictions.Remove(thingBlock);
            }

            // Run filter extensions so dynamically-allowed items (e.g. crop outputs, animal races)
            // appear in the codex reference list regardless of research state, and so extensions can
            // record per-item research/tech requirements for display.
            if (modExtensions != null)
            {
                foreach (ResourceFilterExtension ext in modExtensions.OfType<ResourceFilterExtension>())
                {
                    ext.SetFilterForCodex(filter, restrictions);
                }
            }
        }

        /* Buy-side variant of FilterResource. Ignores all research/recipe/tech-level gates so
         * traders accept any item belonging to the resource category, regardless of whether the
         * player has researched the recipe or has the tech level to produce it. */
        public void FilterResourceForTrade(ThingFilter filter)
        {
            /* Allow whole categories outright */
            foreach (ResourceThingCategoryDefRestriction thingCategoryRestriction in thingCategoryAllowList)
            {
                filter.SetAllow(thingCategoryRestriction.thingCategoryDef, true);
            }
            foreach (StuffCategoryDef stuffCategoryDef in stuffCategoryAllowList)
            {
                filter.SetAllow(stuffCategoryDef, true);
            }
            /* Block categories */
            foreach (ThingCategoryDef thingCategoryDef in thingCategoryBlockList)
            {
                filter.SetAllow(thingCategoryDef, false);
            }
            foreach (StuffCategoryDef stuffCategoryDef in stuffCategoryBlockList)
            {
                filter.SetAllow(stuffCategoryDef, false);
            }
            /* Allow specific things outright (overrides category blocks) */
            foreach (ResourceThingDefRestriction thingDefRestriction in thingAllowList)
            {
                filter.SetAllow(thingDefRestriction.thingDef, true);
            }
            /* Block specific things (highest precedence) */
            foreach (ThingDef thingDef in thingBlockList)
            {
                filter.SetAllow(thingDef, false);
            }

            /* Run extensions at max tech level so they don't gate either. Pass a null ResourceFC
             * because there's no settlement context for an empire-wide buy filter. */
            if (modExtensions != null)
            {
                foreach (ResourceFilterExtension ext in modExtensions.OfType<ResourceFilterExtension>())
                {
                    ext.SetFilter(filter, TechLevel.Archotech, null);
                }
            }
        }

        public int CompareForUI(ResourceTypeDef compareDef)
        {
            return this.uiPriority - compareDef.uiPriority;
        }
        public static int SortForUI(ResourceTypeDef a, ResourceTypeDef b)
        {
            if (a == null)
            {
                return 1;
            }
            if (b == null)
            {
                return -1;
            }
            return a.CompareForUI(b);
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string item in base.ConfigErrors())
            {
                yield return item;
            }
            if (canTithe && thingAllowList.Count == 0 && thingCategoryAllowList.Count == 0 && stuffCategoryAllowList.Count == 0 && (modExtensions?.Count ?? 0) == 0)
            {
                yield return "ResourceTypeDef " + this.defName + " does not specify any allowed resources";
            }
            if (isPoolResource && (thingAllowList.Count > 0 || thingCategoryAllowList.Count > 0 || stuffCategoryAllowList.Count > 0))
            {
                yield return "ResourceTypeDef " + this.defName + " is set as a pool resource, but it also specifies allowlists for things, thingCategories, or stuffCategories";
            }
            if (thingAllowList.Count > 0)
            {
                foreach (ResourceThingDefRestriction thingDefRestriction in thingAllowList)
                {
                    if (thingDefRestriction.maxTechLevel != TechLevel.Undefined && thingDefRestriction.maxTechLevel < thingDefRestriction.minTechLevel)
                    {
                        yield return "maxTechLevel " + thingDefRestriction.maxTechLevel + " is earlier than minTechLevel " + thingDefRestriction.minTechLevel;
                    }
                    if (thingBlockList != null && thingBlockList.Contains(thingDefRestriction.thingDef))
                    {
                        yield return "thingDefRestriction " + thingDefRestriction.thingDef.defName + " appears in both thingAllowList and thingBlockList for ResourceTypeDef " + this.defName;
                    }
                }
            }
            if (thingCategoryAllowList.Count > 0)
            {
                foreach (ResourceThingCategoryDefRestriction thingCategoryDefRestriction in thingCategoryAllowList)
                {
                    if (thingCategoryDefRestriction.maxTechLevel != TechLevel.Undefined && thingCategoryDefRestriction.maxTechLevel < thingCategoryDefRestriction.minTechLevel)
                    {
                        yield return "maxTechLevel " + thingCategoryDefRestriction.maxTechLevel + " is earlier than minTechLevel " + thingCategoryDefRestriction.minTechLevel;
                    }
                    if (thingCategoryBlockList != null)
                    {
                        if (thingCategoryBlockList.Contains(thingCategoryDefRestriction.thingCategoryDef))
                        {
                            yield return "thingCategoryDefRestriction " + thingCategoryDefRestriction.thingCategoryDef.defName + " appears in both thingCategoryAllowList and thingCategoryBlockList for ResourceTypeDef " + this.defName;
                        }
                    }
                }
            }
            if (stuffCategoryAllowList.Count > 0 && stuffCategoryBlockList.Count > 0)
            {
                foreach (StuffCategoryDef stuffCategoryDefAllow in stuffCategoryAllowList)
                {
                    if (stuffCategoryBlockList.Contains(stuffCategoryDefAllow))
                    {
                        yield return "stuffCategoryDef " + stuffCategoryDefAllow.defName + " appears in both the stuffCategoryAllowList and stuffCategoryBlockList for ResourceTypeDef " + this.defName;
                    }
                }
            }
            if (biomeAllowList.Count > 0 && biomeBlockList.Count > 0)
            {
                yield return "biomeAllowList and biomeBlockList are both specified for ResourceTypeDef " + this.defName + ". Only one should be specified";
            }
            foreach (SkillDef skillDef in associatedSkills)
            {
                if (skillDef == null)
                {
                    yield return "associatedSkills contains an unresolved SkillDef entry in ResourceTypeDef " + this.defName;
                }
            }
            if (modExtensions?.Count > 0)
            {
                bool foundPoolExtension = false;
                foreach (ResourcePoolExtension ext in modExtensions.OfType<ResourcePoolExtension>())
                {
                    foundPoolExtension = true;
                    foreach (ResourcePoolExtension ext2 in modExtensions.OfType<ResourcePoolExtension>())
                    {
                        if (ext != ext2)
                        {
                            yield return "ResourcePoolExtension " + ext.ToStringSafe() + " appears more than once in defModExtensions for ResourceTypeDef " + this.defName;
                        }
                    }
                }
                foreach (ResourceFilterExtension ext in modExtensions.OfType<ResourceFilterExtension>())
                {
                    foreach (ResourceFilterExtension ext2 in modExtensions.OfType<ResourceFilterExtension>())
                    {
                        if (ext != ext2)
                        {
                            yield return "ResourceFilterExtension " + ext.ToStringSafe() + " appears more than once in defModExtensions for ResourceTypeDef " + this.defName;
                        }
                    }
                }
                foreach (ResourceTaxExtension ext in modExtensions.OfType<ResourceTaxExtension>())
                {
                    if (isPoolResource)
                    {
                        yield return "ResourceTaxExtension is specified for pool resource " + this.defName + ". ResourceTaxExtension only applies to non-pool resources.";
                    }
                    foreach (ResourceTaxExtension ext2 in modExtensions.OfType<ResourceTaxExtension>())
                    {
                        if (ext != ext2)
                        {
                            yield return "ResourceTaxExtension " + ext.ToStringSafe() + " appears more than once in defModExtensions for ResourceTypeDef " + this.defName;
                        }
                    }
                }
                if (isPoolResource && !foundPoolExtension)
                {
                    yield return "isPoolResource is TRUE but there is no ResourcePoolExtension in defModExtensions for ResourceTypeDef " + this.defName;
                }
                if (!isPoolResource && foundPoolExtension)
                {
                    yield return "isPoolResource is FALSE but there is a ResourcePoolExtension in defModExtensions for ResourceTypeDef " + this.defName;
                }
            }
            else
            {
                if (isPoolResource)
                {
                    yield return "isPoolResource is TRUE but there is no ResourcePoolExtension in defModExtensions for ResourceTypeDef " + this.defName;
                }
            }
            if (productionAdditiveStat == null)
            {
                yield return "productionAdditiveStat is not set for ResourceTypeDef " + this.defName;
            }
            else if (productionAdditiveStat.aggregation != FCStatAggregation.Additive)
            {
                yield return "productionAdditiveStat must have Additive aggregation for ResourceTypeDef " + this.defName;
            }
            if (productionMultiplierStat == null)
            {
                yield return "productionMultiplierStat is not set for ResourceTypeDef " + this.defName;
            }
            else if (productionMultiplierStat.aggregation != FCStatAggregation.Multiplicative)
            {
                yield return "productionMultiplierStat must have Multiplicative aggregation for ResourceTypeDef " + this.defName;
            }
        }
    }

    /// <summary>
    /// Per-item tech/research restriction info for Codex display.
    /// </summary>
    public struct TitheRestrictionInfo
    {
        public TechLevel minTechLevel;
        public List<ResearchProjectDef> researchProjects;
    }
}
