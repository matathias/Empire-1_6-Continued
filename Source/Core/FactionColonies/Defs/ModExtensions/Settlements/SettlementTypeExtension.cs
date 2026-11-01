using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// This extension for WorldSettlementDefs allows for control over various aspects of creating a new settlement.
    /// </summary>
    public class SettlementTypeExtension : DefModExtension
    {
        protected WorldSettlementDef parentDef;
        protected FactionFC faction => FindFC.FactionComp;

        public override void ResolveReferences(Def parentDef_l)
        {
            base.ResolveReferences(parentDef_l);
            LogUtil.Message($"Calling ResolveReferences in SettlementTypeExtension for def {parentDef_l.defName}");
            if (parentDef_l is WorldSettlementDef wpd)
            {
                this.parentDef = wpd;
            }
            else
            {
                LogUtil.Error($"SettlementTypeExtension has non-WorldSettlementDef parent {TextUtil.GetDefModInfo(parentDef_l)}! Setting to default");
                this.parentDef = WorldSettlementDefOf.WorldSettlementDef_Surface;
            }
        }
        /// <summary>
        /// Calls TileFinder.IsValidTileForNewSettlement() while temporarily masking the tile's
        /// hilliness so the base game's impassable rejection is skipped.
        /// </summary>
        protected static bool CallTileFinderIgnoringImpassable(PlanetTile tile, StringBuilder reason)
        {
            Tile tileData = Find.WorldGrid[tile];
            Hilliness original = tileData.hilliness;
            tileData.hilliness = Hilliness.Mountainous;
            bool result = TileFinder.IsValidTileForNewSettlement(tile, reason);
            tileData.hilliness = original;
            return result;
        }

        /// <summary>
        /// Returns true if the tile has a mutator listed in
        /// <see cref="WorldSettlementDef.impassableAllowedMutators"/>, allowing the impassable
        /// restriction to be bypassed.
        /// </summary>
        protected virtual bool TileHasImpassableOverride(Tile tile)
        {
            if (parentDef.impassableAllowedMutators is null || parentDef.impassableAllowedMutators.Count == 0)
                return false;
            foreach (TileMutatorDef mutator in tile.Mutators)
            {
                if (parentDef.impassableAllowedMutators.Contains(mutator))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Determines if the given tile is a valid location for a new settlement of this type.
        /// </summary>
        /// <param name="tile">The PlanetTile to check.</param>
        /// <param name="reason">A string stating the reason this tile is not valid.</param>
        /// <returns>TRUE if the given tile is valid for settlement. FALSE otherwise.</returns>
        public virtual bool TileIsValidForSettlement(PlanetTile tile, StringBuilder reason = null)
        {
            // If the tile is impassable but has a qualifying mutator, bypass TileFinder
            // (which unconditionally rejects impassable tiles) and use our own validation.
            bool impassableOverride = tile.Tile is object
                && tile.Tile.hilliness == Hilliness.Impassable
                && TileHasImpassableOverride(tile.Tile);

            if (impassableOverride)
            {
                if (!CallTileFinderIgnoringImpassable(tile, reason)) return false;
            }
            else
            {
                if (!TileFinder.IsValidTileForNewSettlement(tile, reason)) return false;
            }
            if (tile.Tile is null) return false;

            foreach (WorldSettlementFC settlement in Find.WorldObjects.AllWorldObjects.Where(obj => obj.GetType() == typeof(WorldSettlementFC)))
            {
                if (Find.WorldGrid.IsNeighborOrSame(settlement.Tile, tile))
                {
                    reason?.Append("FCFactionBaseAdjacent".Translate());
                    return false;
                }
            }
            if (parentDef.allowedBiomes?.Count > 0)
            {
                bool foundAllowedBiome = false;
                foreach (BiomeDef biome in tile.Tile.Biomes)
                {
                    if (parentDef.allowedBiomes.Contains(biome))
                    {
                        foundAllowedBiome = true;
                        break;
                    }
                }
                if (!foundAllowedBiome)
                {
                    reason?.Append("FCNotAllowedBiome".Translate(parentDef.LabelCap));
                    return false;
                }
            }
            if (parentDef.blockedBiomes?.Count > 0)
            {
                foreach (BiomeDef biome in tile.Tile.Biomes)
                {
                    if (parentDef.blockedBiomes.Contains(biome))
                    {
                        reason?.Append("FCNotAllowedBiome".Translate(parentDef.LabelCap));
                        return false;
                    }
                }
            }
            return true;
        }
        /// <summary>
        /// Takes a tile, and then returns the correct corresponding tile for this settlement type.
        /// <para>Meant to be used for settlement types that live on planet layers other than the surface.</para>
        /// </summary>
        /// <param name="tile"></param>
        /// <returns></returns>
        public virtual PlanetTile GetTileForSettlement(PlanetTile tile)
        {
            var worldGrid = Find.WorldGrid;
            if (tile.Layer == worldGrid.Surface)
            {
                return tile;
            }
            else
            {
                return new PlanetTile(tile.tileId, worldGrid.Surface);
            }
        }

        public virtual string GetSettlementName(string fallback = "Settlement")
        {
            Faction pfaction = FindFC.EmpireFaction;
            if (pfaction?.def.settlementNameMaker == null)
            {
                return fallback;
            }

            List<String> used = new List<string>();
            List<Settlement> settlements = Find.WorldObjects.Settlements;
            foreach (Settlement found in settlements)
            {
                used.Add(found.Name);
            }

            return NameGenerator.GenerateName(pfaction.def.settlementNameMaker, used, true);
        }

        /// <summary>
        /// Called at the very beginning of CreatePlayerColonySettlement(), before any code has run.
        /// </summary>
        public virtual void PreCreation(ref PlanetTile tile, ref WorldSettlementDef settlementType)
        {
        }

        /// <summary>
        /// Called at the very end of CreatePlayerColonySettlement(), after all code has run (but before the letter notification is sent).
        /// </summary>
        public virtual void PostCreation(WorldSettlementFC settlement)
        {
        }

        /// <summary>
        /// Determines how much it costs to found a new settlement of this type.
        /// </summary>
        /// <returns>The cost of a new settlement.</returns>
        public virtual int GetCreationCost()
        {
            if (faction == null)
            {
                return (int)FCSettings.silverToCreateSettlement;
            }
            else
            {
                double perSettlement = 500 + faction.GetStatValue(FCStatDefOf.settlementExpansionCostPerSettlement);
                return (int)Math.Max(FCSettings.silverToCreateSettlement + (perSettlement * (faction.settlements.Count() + faction.settlementCaravansList.Count())), 0);
            }
        }
        /// <summary>
        /// Determines how long it takes to create this settlement.
        /// </summary>
        /// <returns></returns>
        public virtual int GetCreationTime(PlanetTile destination)
        {
            return TravelUtil.ReturnTicksToArrive(faction.capitalLocation, destination);
        }

        public virtual string GetLocationText(WorldSettlementFC settlement)
        {
            // Single format key so translations control word order (the derived hilliness/biome labels are already localized by the engine).
            return "FCSettlementLocation".Translate(settlement.Tile.Tile.hilliness.GetLabel(), settlement.Tile.Tile.PrimaryBiome.LabelCap.ToLower());
        }

        public virtual TaxDeliveryMode GetTaxDeliveryMode(bool canUseShuttle, PlanetTile sourceTile)
        {
            if (FCSettings.forcedTaxDeliveryMode != default)
            {
                if (FCSettings.forcedTaxDeliveryMode == TaxDeliveryMode.Shuttle && !ModsConfig.RoyaltyActive)
                {
                    return FactionCache.TechTransportPods.IsFinished ? TaxDeliveryMode.DropPod : TaxDeliveryMode.Caravan;
                }
                return FCSettings.forcedTaxDeliveryMode;
            }

            if (FactionCache.TechTransportPods.IsFinished)
            {
                if (ModsConfig.RoyaltyActive && canUseShuttle)
                {
                    return TaxDeliveryMode.Shuttle;
                }
                return TaxDeliveryMode.DropPod;
            }
            return TaxDeliveryMode.Caravan;
        }

        /// <summary>
        /// Returns a description of the settlement's current level for display in the settlement window.
        /// If the parent def has a descriptionKey, tries a type-specific translation key first,
        /// falling back to the generic key if it doesn't exist.
        /// </summary>
        public virtual string GetSettlementLevelDesc(int level)
        {
            int compressed;
            switch (level)
            {
                case 1: compressed = 1; break;
                case 2: compressed = 2; break;
                case 3:
                case 4: compressed = 3; break;
                case 5:
                case 6: compressed = 4; break;
                default: compressed = 5; break;
            }

            string descKey = parentDef?.descriptionKey;
            if (descKey is object)
            {
                string typeSpecificKey = "FCTownLevel_" + descKey + "_" + compressed;
                if (typeSpecificKey.CanTranslate())
                    return typeSpecificKey.Translate();
            }

            return ("FCTownLevel" + compressed).Translate();
        }

        /// <summary>
        /// Called after a settlement's level changes (upgrade or delevel) and stats have been updated.
        /// </summary>
        public virtual void OnUpgrade(WorldSettlementFC settlement, int oldLevel, int newLevel)
        {
        }

        /// <summary>
        /// Returns the number of building slots available at the given settlement level.
        /// Override to customize building slot progression for this settlement type.
        /// </summary>
        public virtual int GetBuildingSlots(int level, int maxCount)
        {
            return SettlementFormulas.CalculateBuildingSlots(level, maxCount, parentDef.baseUnlockedBuildings, EffectivePerLevelSlots());
        }

        /// <summary>
        /// Per-level building slot growth, including the buildingSlotsPerLevelBonus stat (faction-wide).
        /// Used by both slot-count and slot-unlock-level math so they stay consistent.
        /// Note: a large negative bonus could strand buildings in newly-locked slots; the slot count is still
        /// clamped to maxBuildingCount.
        /// </summary>
        protected float EffectivePerLevelSlots()
        {
            return parentDef.perLevelUnlockedBuildings + (float)(faction?.GetStatValue(FCStatDefOf.buildingSlotsPerLevelBonus) ?? 0);
        }

        /// <summary>
        /// Returns the minimum settlement level required to unlock a given building slot index.
        /// Returns 0 if available at founding, or -1 if the slot can never be unlocked via leveling.
        /// Override to match custom building slot progression logic.
        /// </summary>
        public virtual int GetRequiredLevelForSlot(int slotIndex, int maxCount)
        {
            return SettlementFormulas.CalculateLevelForSlot(slotIndex, parentDef.baseUnlockedBuildings, EffectivePerLevelSlots());
        }

        /// <summary>
        /// Returns the silver cost to upgrade from the given settlement level.
        /// Override to customize upgrade costs for this settlement type.
        /// </summary>
        public virtual int GetUpgradeCost(int level, int baseCost)
        {
            return SettlementFormulas.CalculateUpgradeCost(level, baseCost);
        }

        /// <summary>
        /// Returns the time in ticks to upgrade from the given settlement level.
        /// Override to customize upgrade time for this settlement type.
        /// </summary>
        public virtual int GetUpgradeTime(int level, double buildTimeMult)
        {
            return SettlementFormulas.CalculateUpgradeTime(level, buildTimeMult);
        }

        /* Type Transition */
        /// <summary>
        /// Called on the OLD type's extension before the def swap happens.
        /// </summary>
        public virtual void PreTypeTransition(WorldSettlementFC settlement, WorldSettlementDef newDef)
        {
        }

        /// <summary>
        /// Called on the NEW type's extension after the def swap and full reconciliation.
        /// </summary>
        public virtual void PostTypeTransition(WorldSettlementFC settlement, WorldSettlementDef oldDef)
        {
        }

        /// <summary>
        /// Validates whether an existing settlement's tile is compatible with this type.
        /// Unlike <see cref="TileIsValidForSettlement"/>, this skips occupation and adjacency checks
        /// since the settlement already exists on the tile.
        /// Base impl checks: planet layer, hilliness, allowedBiomes, blockedBiomes.
        /// </summary>
        public virtual bool TileIsValidForTypeTransition(PlanetTile tile, StringBuilder reason = null)
        {
            if (!parentDef.AllowsTileLayer(tile))
            {
                reason?.Append("FCTileWrongPlanetLayer".Translate());
                return false;
            }

            if (tile.Tile?.hilliness == Hilliness.Impassable && !TileHasImpassableOverride(tile.Tile))
            {
                reason?.Append("FCImpassableMountains".Translate(parentDef.LabelCap));
                return false;
            }

            if (parentDef.allowedBiomes?.Count > 0)
            {
                bool foundAllowedBiome = false;
                foreach (BiomeDef biome in tile.Tile.Biomes)
                {
                    if (parentDef.allowedBiomes.Contains(biome))
                    {
                        foundAllowedBiome = true;
                        break;
                    }
                }
                if (!foundAllowedBiome)
                {
                    reason?.Append("FCNotAllowedBiome".Translate(parentDef.LabelCap));
                    return false;
                }
            }

            if (parentDef.blockedBiomes?.Count > 0)
            {
                foreach (BiomeDef biome in tile.Tile.Biomes)
                {
                    if (parentDef.blockedBiomes.Contains(biome))
                    {
                        reason?.Append("FCNotAllowedBiome".Translate(parentDef.LabelCap));
                        return false;
                    }
                }
            }

            return true;
        }

        /* Destruction */
        /// <summary>
        /// Called before a settlement is removed from the world.
        /// </summary>
        public virtual void PreDestruction(WorldSettlementFC settlement)
        {
        }

        /// <summary>
        /// Called at the start of tax collection, after pre-tax preparation (cache invalidation, resource pruning).
        /// </summary>
        public virtual void PreTax(WorldSettlementFC settlement)
        {
        }

        /// <summary>
        /// Called at the end of tax collection, after all calculations are complete.
        /// </summary>
        public virtual void PostTax(WorldSettlementFC settlement, ref int silverAmount, List<Thing> titheThings)
        {
        }

        /// <summary>
        /// Returns whether the given faction is eligible to raid settlements of this type.
        /// Called after enemy faction selection to filter the target pool.
        /// Base implementation returns true (any faction can raid).
        /// </summary>
        public virtual bool CanBeRaidedByFaction(Faction attackingFaction) => true;
    }
}
