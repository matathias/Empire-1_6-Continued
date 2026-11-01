using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Def for WorldSettlementFC objects.
    /// </summary>
    public class WorldSettlementDef : WorldObjectDef
    {
        /// <summary>Description with {FACTION}/{FACTION_TITLE} tokens and [b]/[i] emphasis markup resolved for display.</summary>
        public string FormattedDesc => description.Format();
        public List<ResourceAvailability> resources = new List<ResourceAvailability>();
        /// <summary>
        /// If true, all ResourceTypeDefs with isDefaultResource set to true are automatically added to this settlement's resources list
        /// (unless already explicitly listed). Explicit entries take priority over defaults.
        /// </summary>
        public bool defaultResources = false;
        public int workersMaxBase = 0;
        public int workersMaxMult = 3;
        public int workersUltraMaxBase = 5;
        public int workersUltraMaxMult = 0;
        public List<BiomeDef> blockedBiomes = new List<BiomeDef>();
        public List<BiomeDef> allowedBiomes = new List<BiomeDef>();

        public List<FCStatModifier> statModifiers = new List<FCStatModifier>();
        /// <summary>
        /// If a biomeResourceOverride is specified, then the settlement will use the resources of the given override rather than the resources
        /// of the biome of the tile that it's on.
        /// </summary>
        public BiomeResourceDef biomeResourceOverride;

        public List<ResearchProjectDef> researchProjects = new List<ResearchProjectDef>();
        public TechLevel techLevel = TechLevel.Undefined;

        public List<PlanetLayerDef> planetLayers = new List<PlanetLayerDef>();

        /// <summary>
        /// Lower-case display label of the planet layer(s) this settlement can be founded on.
        /// Empty planetLayers means Surface-only (the WorldTileChecker convention). The Surface
        /// layer is shown as "surface" rather than its vanilla label ("planet").
        /// </summary>
        public string PlanetLayersLabel
        {
            get
            {
                if (planetLayers == null || planetLayers.Count == 0)
                    return "FCCodexLayerSurface".Translate().RawText;
                List<string> labels = new List<string>(planetLayers.Count);
                foreach (PlanetLayerDef layer in planetLayers)
                    labels.Add(layer == PlanetLayerDefOf.Surface ? "FCCodexLayerSurface".Translate().RawText : layer.label);
                return string.Join(", ", labels.ToArray());
            }
        }

        /// <summary>
        /// True if this settlement type can be founded on the planet surface. Mirrors WorldTileChecker:
        /// empty planetLayers means Surface-only; otherwise Surface must be among the allowed layers.
        /// Biome restrictions only apply to surface-foundable types, so non-surface types (orbital)
        /// must be excluded from per-biome foundability listings.
        /// </summary>
        public bool CanFoundOnSurface =>
            planetLayers == null || planetLayers.Count == 0 || planetLayers.Contains(PlanetLayerDefOf.Surface);

        /// <summary>
        /// True if this settlement type can be founded on the given tile's planet layer. Empty
        /// planetLayers means Surface-only (the WorldTileChecker convention); otherwise the tile's
        /// layer must be among the allowed layers. Callers append their own rejection reason.
        /// </summary>
        public bool AllowsTileLayer(PlanetTile tile)
        {
            if (planetLayers == null || planetLayers.Count == 0)
                return tile.Layer == Find.WorldGrid.Surface;
            return planetLayers.Contains(tile.Layer.Def);
        }

        /// <summary>
        /// Optional parent settlement type for inheritance-aware allow/block list checks on buildings.
        /// When a BuildingFCDef's allow/block list is checked, the chain of baseSettlementType references
        /// is walked upward, so subtypes automatically match their parent type.
        /// </summary>
        public WorldSettlementDef baseSettlementType;

        public int maxSettlementLevel = 99;
        public int maxBuildingCount = 99;
        public int baseUnlockedBuildings = 3;
        public float perLevelUnlockedBuildings = 0.5f;

        /// <summary>
        /// Optional key used for settlement-type-specific town titles.
        /// When set, GetTownTitle tries FCTitle_{titleKey}_{resourceDefName}_{level} first,
        /// falling back to FCTitle_{resourceDefName}_{level} if the type-specific key doesn't exist.
        /// </summary>
        public string titleKey;

        /// <summary>
        /// Optional key used for settlement-type-specific town level descriptions.
        /// When set, GetSettlementLevelDesc tries FCTownLevel_{descriptionKey}_{compressedLevel} first,
        /// falling back to FCTownLevel{compressedLevel} if the type-specific key doesn't exist.
        /// </summary>
        public string descriptionKey;

        /// <summary>
        /// Entirely flavor. Determines whether time to create is labeled in menus as "Construction Time" or "Travel Time".
        /// </summary>
        public bool isConstructed = false;

        public Color? accentColor;

        /// <summary>
        /// If false, this settlement type will not appear in the settlement type picker.
        /// Submods can XML-patch this to false to hide settlement types.
        /// </summary>
        public bool available = true;

        /// <summary>
        /// Multiplier applied to this settlement type's weight when the threat system selects raid targets.
        /// Higher values make settlements of this type more likely to be attacked.
        /// Default 1.0 = no change. Example: 2.0 = twice as likely to be targeted.
        /// </summary>
        public float raidTargetingWeight = 1.0f;

        /// <summary>
        /// If false, this settlement type is completely excluded from enemy raid targeting.
        /// Default true. Set to false for settlement types that should never be attacked.
        /// </summary>
        public bool canBeRaided = true;

        /// <summary>
        /// List of TileMutatorDefs that allow this settlement type to be founded on impassable tiles.
        /// If the tile has Hilliness.Impassable AND at least one of these mutators is present,
        /// the impassable restriction is bypassed. Null/empty = impassable tiles always blocked (default).
        /// </summary>
        public List<TileMutatorDef> impassableAllowedMutators = new List<TileMutatorDef>();

        /// <summary>
        /// If false, battles at this settlement type always auto-resolve, even if the player
        /// has selected Manual or Hybrid battle mode. Use for settlement types whose maps
        /// cannot be generated (e.g. orbital stations).
        /// </summary>
        public bool supportsManualBattle = true;

        public ResourceAvailability GetSettlementResource(ResourceTypeDef resourceTypeDef)
        {
            return resources.FirstOrDefault((ResourceAvailability b) => b.resourceDef == resourceTypeDef);
        }
        public List<ResourceTypeDef> GetResourceDefs()
        {
            List<ResourceTypeDef> list = new List<ResourceTypeDef>();
            if (resources != null)
            {
                foreach (ResourceAvailability rb in resources)
                {
                    list.Add(rb.resourceDef);
                }
            }
            return list;
        }
        public List<string> GetResourceDefNames()
        {
            List<string> list = new List<string>();
            if (resources != null)
            {
                foreach (ResourceAvailability rb in resources)
                {
                    list.Add(rb.resourceDef.defName);
                }
            }
            return list;
        }
        public SettlementTypeExtension GetSettlementTypeExtension()
        {
            if (modExtensions == null) return null;
            SettlementTypeExtension result = null;
            for (int i = 0; i < modExtensions.Count; i++)
            {
                if (modExtensions[i] is SettlementTypeExtension ext)
                    result = ext;
            }
            return result;
        }

        public bool IsUnlocked()
        {
            if (!available) return false;
            if (researchProjects?.Count > 0)
            {
                foreach (ResearchProjectDef researchProject in researchProjects)
                {
                    if (!researchProject.IsFinished)
                    {
                        return false;
                    }
                }
            }
            if (techLevel != TechLevel.Undefined)
            {
                Faction faction = FindFC.EmpireFaction;
                if (faction.def.techLevel < techLevel)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Returns false if the settlement type is locked, populating a human-readable reason string.
        /// Does not check the 'available' field; that controls visibility, not lock state.
        /// </summary>
        public bool IsUnlocked(out string lockedReason)
        {
            List<string> reasons = new List<string>();
            if (researchProjects?.Count > 0)
            {
                foreach (ResearchProjectDef rp in researchProjects)
                {
                    if (!rp.IsFinished)
                        reasons.Add("FCRequiresResearch".Translate(rp.LabelCap));
                }
            }
            if (techLevel != TechLevel.Undefined)
            {
                Faction faction = FindFC.EmpireFaction;
                if (faction.def.techLevel < techLevel)
                    reasons.Add("FCRequiresTechLevel".Translate(techLevel.ToStringHuman()));
            }
            if (reasons.Count > 0)
            {
                lockedReason = string.Join("\n", reasons.ToArray());
                return false;
            }
            lockedReason = null;
            return true;
        }

        public int GetCreationTime(PlanetTile tile)
        {
            SettlementTypeExtension ext = GetSettlementTypeExtension();
            if (ext is null) { LogUtil.Error($"WorldSettlementDef {defName} is missing a SettlementTypeExtension; returning 0 creation time."); return 0; }
            return ext.GetCreationTime(tile);
        }
        public int GetCreationCost()
        {
            SettlementTypeExtension ext = GetSettlementTypeExtension();
            if (ext is null) { LogUtil.Error($"WorldSettlementDef {defName} is missing a SettlementTypeExtension; returning 0 creation cost."); return 0; }
            return ext.GetCreationCost();
        }
        public PlanetTile GetTileForSettlement(PlanetTile tile)
        {
            SettlementTypeExtension ext = GetSettlementTypeExtension();
            if (ext is null) { LogUtil.Error($"WorldSettlementDef {defName} is missing a SettlementTypeExtension; returning the source tile unchanged."); return tile; }
            return ext.GetTileForSettlement(tile);
        }
        public TaxDeliveryMode GetTaxDeliveryMode(bool canUseShuttle, PlanetTile sourceTile)
        {
            SettlementTypeExtension ext = GetSettlementTypeExtension();
            if (ext is null) { LogUtil.Error($"WorldSettlementDef {defName} is missing a SettlementTypeExtension; returning the default tax delivery mode."); return default(TaxDeliveryMode); }
            return ext.GetTaxDeliveryMode(canUseShuttle, sourceTile);
        }
        public bool IsInList(List<WorldSettlementDef> deflist)
        {
            if (deflist is null || deflist.Count == 0)
            {
                return false;
            }

            if (deflist.Contains(this))
            {
                return true;
            }
            else if (!(baseSettlementType is null))
            {
                return baseSettlementType.IsInList(deflist);
            }
            return false;
        }

        /// <summary>
        /// Returns the depth at which this def matches a list entry, walking baseSettlementType.
        /// 0 = direct match on self, 1 = match on parent, 2 = grandparent, etc.
        /// Returns -1 if no match found.
        /// </summary>
        public int DepthInList(List<WorldSettlementDef> deflist)
        {
            if (deflist == null || deflist.Count == 0) return -1;
            if (deflist.Contains(this)) return 0;
            if (baseSettlementType != null)
            {
                int parentDepth = baseSettlementType.DepthInList(deflist);
                return parentDepth >= 0 ? parentDepth + 1 : -1;
            }
            return -1;
        }

        public override void ResolveReferences()
        {
            base.ResolveReferences();
            if (defaultResources)
            {
                foreach (ResourceTypeDef rtd in FactionCache.AllResourceTypeDefs)
                {
                    if (rtd.isDefaultResource && !resources.Any(rb => rb.resourceDef == rtd))
                    {
                        resources.Add(new ResourceAvailability { resourceDef = rtd });
                    }
                }
            }
            foreach (ResourceAvailability rb in resources)
            {
                if (double.IsNaN(rb.additive))
                {
                    rb.additive = 0;
                }
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            IEnumerable<string> errors = base.ConfigErrors();
            if (errors != null)
            {
                foreach (string error in errors)
                {
                    yield return error;
                }
            }

            if (resources != null)
            {
                foreach (ResourceAvailability rb in resources)
                {
                    if (resources.Any((ResourceAvailability b) => b != rb && b.resourceDef == rb.resourceDef))
                    {
                        yield return "ResourceTypeDef " + rb.resourceDef.defName + " is listed multiple times in WorldSettlmentDef " + defName;
                    }
                }
            }
            if (blockedBiomes?.Count > 0 && allowedBiomes?.Count > 0)
            {
                yield return "WorldSettlementDef " + defName + "specifies both blocked Biomes and allowed Biomes. Only one list should be specified";
            }
            if (GetSettlementTypeExtension() == null)
            {
                yield return "WorldSettlementDef " + defName + " does not specify a SettlementTypeExtension_Base";
            }
            if (baseSettlementType != null)
            {
                HashSet<WorldSettlementDef> visited = new HashSet<WorldSettlementDef> { this };
                WorldSettlementDef current = baseSettlementType;
                while (current != null)
                {
                    if (!visited.Add(current))
                    {
                        yield return "WorldSettlementDef " + defName + " has a circular baseSettlementType reference involving " + current.defName;
                        break;
                    }
                    current = current.baseSettlementType;
                }
            }
            if (baseUnlockedBuildings < 0)
            {
                yield return "WorldSettlementDef " + defName + " has baseUnlockedBuildings < 0";
            }
            if (perLevelUnlockedBuildings < 0f)
            {
                yield return "WorldSettlementDef " + defName + " has perLevelUnlockedBuildings < 0";
            }
            if (baseUnlockedBuildings > maxBuildingCount)
            {
                yield return "WorldSettlementDef " + defName + " has baseUnlockedBuildings (" + baseUnlockedBuildings + ") > maxBuildingCount (" + maxBuildingCount + ")";
            }
            foreach (string err in FCStatModifier.ConfigErrors(statModifiers, defName))
                yield return err;
        }
    }
}
