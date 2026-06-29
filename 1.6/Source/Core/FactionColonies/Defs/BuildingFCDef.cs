using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Represents an entry in a flattened upgrade tree.
    /// </summary>
    public struct BuildingUpgradeEntry
    {
        public BuildingFCDef def;
        public int depth;
        public BuildingFCDef parent;
    }

    public class BuildingFCDef : Def
    {
        public string desc;
        /// <summary>Description with {FACTION}/{FACTION_TITLE} tokens and [b]/[i] emphasis markup resolved for display.</summary>
        public string FormattedDesc => desc.Format();
        public double cost;
        public int constructionDuration;
        public TechLevel techLevel = TechLevel.Undefined;
        public List<FCStatModifier> statModifiers = new List<FCStatModifier>();
        public List<string> applicableBiomes = new List<string>();
        public int upkeep;
        /// <summary>
        /// True if this def's upkeep is authored at the post-rework per-day scale. Base-mod defs set this
        /// true (and divide their upkeep by 5). Third-party/legacy defs default false and are divided by
        /// LEGACY_UPKEEP_DIVISOR at runtime to approximate a per-day value.
        /// </summary>
        public bool postRework = false;
        /// <summary>
        /// Marks this as a military building for cost/upkeep classification (buildingCost*_Military,
        /// buildingUpkeepBase_Military, the Militaristic upkeep discount).
        /// </summary>
        public bool isMilitary = false;
        public string iconPath = "GUI/unrest";
        public Texture2D iconLoaded;
        public List<WorldSettlementDef> settlementTypeBlockList = new List<WorldSettlementDef>();
        public List<WorldSettlementDef> settlementTypeAllowList = new List<WorldSettlementDef>();
        public Hilliness minhilliness = Hilliness.Undefined;
        public Hilliness maxhilliness = Hilliness.Undefined;
        public List<TileMutatorDef> tileMutatorAllowList = new List<TileMutatorDef>();
        public List<TileMutatorDef> tileMutatorBlockList = new List<TileMutatorDef>();
        /// <summary>
        /// Determines if the building can be built directly from the building window, into an empty building slot.
        /// </summary>
        public bool baseBuilding = true;
        /// <summary>
        /// A list of buildings that this building can upgrade into.
        /// </summary>
        public List<BuildingFCDef> upgrades = new List<BuildingFCDef>();
        /// <summary>
        /// Buildings that must already be built in the settlement before this building can be constructed.
        /// </summary>
        public List<BuildingFCDef> requiredBuildings = new List<BuildingFCDef>();

        private bool didCacheBuildingAttributeDesc = false;
        private TaggedString cachedBuildingAttributeDesc = "";

        public double Upkeep
        {
            get
            {
                if (postRework) return upkeep;
                return upkeep / (double)FCSettings.LEGACY_UPKEEP_DIVISOR;
            }
        }

        public TaggedString AttributeDesc
        {
            get
            {
                if (!didCacheBuildingAttributeDesc)
                {
                    cachedBuildingAttributeDesc = FCStatModifier.GetDescription(statModifiers);
                    didCacheBuildingAttributeDesc = true;
                }
                return cachedBuildingAttributeDesc;
            }
        }

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
                    LogUtil.Error("Failed to load icon for building: " + LabelCap + " at " + (iconPath ?? "nullPath") + "!");
                    iconLoaded = TexLoad.questionmark;
                }
                return iconLoaded;
            }
        }

        public bool CanBeBuiltOnTile(PlanetTile tile)
        {
            bool hasAllow = !tileMutatorAllowList.NullOrEmpty();
            bool hasBlock = !tileMutatorBlockList.NullOrEmpty();
            if (!hasAllow && !hasBlock) return true;

            IList<TileMutatorDef> mutators = tile.Tile?.Mutators;

            if (hasBlock && mutators != null)
            {
                foreach (TileMutatorDef m in mutators)
                {
                    if (m is null) continue;
                    if (tileMutatorBlockList.Contains(m)) return false;
                }
            }

            if (hasAllow)
            {
                if (mutators.NullOrEmpty()) return false;
                foreach (TileMutatorDef m in mutators)
                {
                    if (m is null) continue;
                    if (tileMutatorAllowList.Contains(m)) return true;
                }
                return false;
            }

            return true;
        }

        public bool CanBeBuiltForSettlementType(WorldSettlementDef settlement)
        {
            bool meetsRequirement = true;

            int allowDepth = (settlementTypeAllowList?.Count > 0)
                ? settlement.DepthInList(settlementTypeAllowList) : -1;
            int blockDepth = (settlementTypeBlockList?.Count > 0)
                ? settlement.DepthInList(settlementTypeBlockList) : -1;

            if (allowDepth >= 0 || blockDepth >= 0)
            {
                if (allowDepth >= 0 && blockDepth >= 0)
                {
                    // Both matched — most specific (shallowest depth) wins. Tie goes to block.
                    meetsRequirement = allowDepth < blockDepth;
                }
                else if (allowDepth >= 0)
                {
                    meetsRequirement = true;
                }
                else
                {
                    meetsRequirement = false;
                }
            }
            else if (settlementTypeAllowList?.Count > 0)
            {
                // Allow list exists but didn't match — blocked by default
                meetsRequirement = false;
            }

            return meetsRequirement && MeetsResourceRequirement(settlement);
        }

        private List<WorldSettlementDef> cachedCompatibleSettlements;

        /// <summary>
        /// All available settlement types this building can be built in, accounting for
        /// allow/block list inheritance and resource requirements. Cached on first access.
        /// </summary>
        public List<WorldSettlementDef> CompatibleSettlementTypes
        {
            get
            {
                if (cachedCompatibleSettlements is null)
                {
                    cachedCompatibleSettlements = new List<WorldSettlementDef>();
                    foreach (WorldSettlementDef def in FactionCache.AvailableWorldSettlementDefs)
                    {
                        if (CanBeBuiltForSettlementType(def))
                            cachedCompatibleSettlements.Add(def);
                    }
                }
                return cachedCompatibleSettlements;
            }
        }

        public static void ClearCompatibleSettlementCache()
        {
            foreach (BuildingFCDef def in DefDatabase<BuildingFCDef>.AllDefsListForReading)
                def.cachedCompatibleSettlements = null;
        }

        private static Dictionary<BuildingFCDef, Dictionary<WorldSettlementDef, bool>> resourceMatchCache;

        /// <summary>
        /// If this building grants beneficial resource production bonuses, the settlement must
        /// produce at least one of those resources. Buildings with no resource production stats pass automatically.
        /// </summary>
        private bool MeetsResourceRequirement(WorldSettlementDef settlement)
        {
            if (resourceMatchCache == null)
                resourceMatchCache = new Dictionary<BuildingFCDef, Dictionary<WorldSettlementDef, bool>>();

            Dictionary<WorldSettlementDef, bool> inner;
            if (!resourceMatchCache.TryGetValue(this, out inner))
            {
                inner = new Dictionary<WorldSettlementDef, bool>();
                resourceMatchCache[this] = inner;
            }

            bool cached;
            if (inner.TryGetValue(settlement, out cached))
                return cached;

            bool result = true;
            if (statModifiers != null)
            {
                bool hasResourceStat = false;
                bool matchesAny = false;
                foreach (FCStatModifier mod in statModifiers)
                {
                    if (mod.stat != null && mod.stat.linkedResource != null && mod.IsBeneficial())
                    {
                        hasResourceStat = true;
                        if (settlement.GetSettlementResource(mod.stat.linkedResource) != null)
                        {
                            matchesAny = true;
                            break;
                        }
                    }
                }
                if (hasResourceStat && !matchesAny)
                    result = false;
            }

            inner[settlement] = result;
            return result;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var err in base.ConfigErrors())
            {
                yield return err;
            }
            if (HasCycle(this, d => d.upgrades))
            {
                yield return $"BuildingFCDef {defName} has a circular reference in its upgrades chain";
            }
            if (HasCycle(this, d => d.requiredBuildings))
            {
                yield return $"BuildingFCDef {defName} has a circular reference in its requiredBuildings chain";
            }
            foreach (string err in FCStatModifier.ConfigErrors(statModifiers, defName))
                yield return err;
        }

        private static bool HasCycle(BuildingFCDef start, Func<BuildingFCDef, List<BuildingFCDef>> getChildren)
        {
            HashSet<BuildingFCDef> visited = new HashSet<BuildingFCDef>();
            Stack<BuildingFCDef> stack = new Stack<BuildingFCDef>();
            stack.Push(start);
            while (stack.Count > 0)
            {
                BuildingFCDef current = stack.Pop();
                if (!visited.Add(current) && current == start) return true;
                List<BuildingFCDef> children = getChildren(current);
                if (children == null) continue;
                foreach (BuildingFCDef child in children)
                {
                    if (child == start) return true;
                    if (!visited.Contains(child)) stack.Push(child);
                }
            }
            return false;
        }
    }
}
