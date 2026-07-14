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
    public class WorldObjectCompProperties_SettlementBuildings : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_SettlementBuildings()
        {
            compClass = typeof(WorldObjectComp_SettlementBuildings);
        }
        public override IEnumerable<string> ConfigErrors(WorldObjectDef parentDef)
        {
            foreach (string item in base.ConfigErrors(parentDef))
            {
                yield return item;
            }
            if (!typeof(MapParent).IsAssignableFrom(parentDef.worldObjectClass))
            {
                yield return parentDef.defName + " has WorldObjectCompProperties_SettlementBuildings but it's not MapParent.";
            }
        }
    }
    public class BuildingFilter
    {
        public string label;
        public Texture2D icon;
        public Func<BuildingFCDef, bool> predicate;

        public BuildingFilter(string label, Texture2D icon, Func<BuildingFCDef, bool> predicate)
        {
            this.label = label;
            this.icon = icon;
            this.predicate = predicate;
        }
    }

    /// <summary>
    /// A WorldObjectComp class for use with BuildingFCDefs. When a building is constructed, if it has a SettlementBuildingComp, then
    /// the comp is added to this comp's list and tracked.
    /// </summary>
    public class WorldObjectComp_SettlementBuildings : WorldObjectComp
    {
        public int FC_MAX_BUILDINGS
        {
            get
            {
                WorldSettlementDef def = WorldSettlement.settlementDef;
                int maxLevel = Math.Min(FCSettings.settlementMaxLevel, def.maxSettlementLevel);
                int slots = def.GetSettlementTypeExtension().GetBuildingSlots(maxLevel, def.maxBuildingCount);
                return Math.Min(slots, def.maxBuildingCount);
            }
        }
        private List<BuildingFC> buildings = new List<BuildingFC>();
        private List<SettlementBuildingComp> settlementBuildingComps = new List<SettlementBuildingComp>();
        // Subset of settlementBuildingComps whose type overrides Tick(). Derived, not scribed; kept in
        // lockstep with settlementBuildingComps via AddComp/RemoveComp/RebuildTickingList so CompTick can
        // iterate it directly without dispatching to no-op (data-only) building comps.
        [Unsaved] private List<SettlementBuildingComp> settlementBuildingComps_Ticking = new List<SettlementBuildingComp>();

        public List<BuildingFC> Buildings => buildings;

        public int NumBuildingSlots => WorldSettlement.GetBuildingSlots();

        private bool dirtyConstructionCache = true;
        private List<BuildingFC> constructionCache = new List<BuildingFC>();

        private WorldSettlementFC cachedWorldSettlementParent = null;
        public WorldSettlementFC WorldSettlement
        {
            get
            {
                if (cachedWorldSettlementParent != null)
                {
                    return cachedWorldSettlementParent;
                }
                if (parent is WorldSettlementFC ws)
                {
                    cachedWorldSettlementParent = ws;
                }
                else
                {
                    cachedWorldSettlementParent = null;
                    LogUtil.ErrorOnce($"WorldObjectComp_SettlementMilitary has a non-WorldSettlementFC parent", 93512107);
                }
                return cachedWorldSettlementParent;
            }
        }

        public string BuildingID(int buildingSlot)
        {
            if (buildingSlot >= buildings.Count)
            {
                return "null";
            }
            return buildings[buildingSlot].def.defName + buildingSlot.ToString();
        }
        /// <summary>
        /// Returns true if any currently-built building in this settlement
        /// has a requirement satisfied by the given building (exact match or upgrade).
        /// </summary>
        public bool IsBuildingRequiredByOther(BuildingFCDef building)
        {
            foreach (BuildingFC bfc in buildings)
            {
                if (bfc.def == BuildingFCDefOf.Empty || bfc.def == BuildingFCDefOf.Construction) continue;
                if (FactionCache.SatisfiesAnyRequirement(building, bfc.def.requiredBuildings))
                    return true;
            }
            return false;
        }
        /// <summary>
        /// Returns all currently-built buildings that have a requirement satisfied by the given building.
        /// </summary>
        public List<BuildingFCDef> GetBuildingsDependingOn(BuildingFCDef building)
        {
            List<BuildingFCDef> result = new List<BuildingFCDef>();
            foreach (BuildingFC bfc in buildings)
            {
                if (bfc.def == BuildingFCDefOf.Empty || bfc.def == BuildingFCDefOf.Construction) continue;
                if (FactionCache.SatisfiesAnyRequirement(building, bfc.def.requiredBuildings))
                    result.Add(bfc.def);
            }
            return result;
        }
        public BuildingFCDef GetBuildingInSlot(int buildingSlot)
        {
            if (buildingSlot >= buildings.Count)
            {
                return null;
            }
            return buildings[buildingSlot].def;
        }

        public SettlementBuildingComp GetComponent(Type type)
        {
            for (int i = 0; i < settlementBuildingComps.Count; i++)
            {
                if (type.IsInstanceOfType(settlementBuildingComps[i]))
                {
                    return settlementBuildingComps[i];
                }
            }
            return null;
        }

        public bool BuildingSlotIsEmpty(int buildingSlot)
        {
            return buildings[buildingSlot].def.defName == BuildingFCDefOf.Empty.defName;
        }
        public bool BuildingSlotIsConstruction(int buildingSlot)
        {
            return buildings[buildingSlot].def.defName == BuildingFCDefOf.Construction.defName;
        }
        public bool BuildingSlotIsBuilding(int buildingSlot)
        {
            return !BuildingSlotIsEmpty(buildingSlot) && !BuildingSlotIsConstruction(buildingSlot);
        }
        public string BuildingLabel(int buildingsSlot)
        {
            return buildings[buildingsSlot].def.LabelCap;
        }
        public List<BuildingFC> GetUnderConstructionBuildings()
        {
            if (dirtyConstructionCache)
            {
                List<BuildingFC> list = new List<BuildingFC>();
                foreach (BuildingFC building in buildings)
                {
                    if (building.def == BuildingFCDefOf.Construction)
                    {
                        list.Add(building);
                    }
                }

                constructionCache = list;
                dirtyConstructionCache = false;
            }

            return constructionCache;
        }

        public void InitBuildings()
        {
            for (int i = 0; i < FC_MAX_BUILDINGS; i++)
            {
                buildings.Add(new BuildingFC
                {
                    def = BuildingFCDefOf.Empty,
                    startedTick = -1,
                    completionTick = Find.TickManager.TicksGame
                });
            }
        }
        public void ReinitBuildings()
        {
            LogUtil.Message($"Reinitializing buildings for settlement {WorldSettlement.Name}. Max buildings: {FC_MAX_BUILDINGS}. Current buildings count: {buildings.Count}");
            if (buildings.Count > FC_MAX_BUILDINGS)
            {
                /* Remove slots, starting at the end and working backwards */
                for (int i = buildings.Count - 1; i >= FC_MAX_BUILDINGS && i >= 0; i--)
                {
                    DeconstructBuilding(i);
                    buildings.RemoveAt(i);
                }
            }
            else if (buildings.Count < FC_MAX_BUILDINGS)
            {
                for (int i = buildings.Count; i < FC_MAX_BUILDINGS; i++)
                {
                    buildings.Add(new BuildingFC
                    {
                        def = BuildingFCDefOf.Empty,
                        startedTick = -1,
                        completionTick = Find.TickManager.TicksGame
                    });
                }
            }
        }

        public void DeconstructAllBuildings()
        {
            for (int i = buildings.Count - 1; i >= 0; i--)
            {
                BuildingFCDef def = buildings[i].def;
                if (def == BuildingFCDefOf.Empty || def == BuildingFCDefOf.Construction) continue;
                DeconstructBuilding(i);
            }
        }

        public static SettlementBuildingComp MakeSettlementBuildingComp(Type compClass, WorldSettlementFC settlement)
        {
            SettlementBuildingComp comp = (SettlementBuildingComp)Activator.CreateInstance(compClass);
            comp.settlement = settlement;
            if (settlement == null)
            {
                LogUtil.Error($"Created new SettlementBuildingComp {compClass} with null SettlementFC");
            }
            return comp;
        }
        public bool HasBuilding(BuildingFCDef building)
        {
            foreach (BuildingFC slot in buildings)
            {
                if (slot.def == building) return true;
            }
            return false;
        }
        /// <summary>
        /// Returns true if the settlement has the given building or any transitive upgrade of it.
        /// </summary>
        public bool HasBuildingOrUpgrade(BuildingFCDef required)
        {
            foreach (BuildingFC slot in buildings)
            {
                if (FactionCache.SatisfiesRequirementFor(slot.def, required)) return true;
            }
            return false;
        }
        public bool ValidConstructBuilding(BuildingFCDef building, int buildingSlot)
        {
            bool valid = true;

            if (buildingSlot >= NumBuildingSlots)
            {
                valid = false;
                Messages.Message("FCBuildingLocked".Translate(), MessageTypeDefOf.RejectInput);
            }

            if (HasBuildingOrUpgrade(building))
            {
                valid = false;
                Messages.Message("FCBuildingAlreadyType".Translate() + "!", MessageTypeDefOf.RejectInput);
            }

            //check if the player can afford it (crediting this settlement's financing, if any)
            if (!PaymentUtil.CanAfford(GetBuildingCost(building), PaymentUtil.Reason_BuildingConstruction, WorldSettlement))
            {
                valid = false;
                Messages.Message("FCNotEnoughSilverConstructBuilding".Translate() + "!", MessageTypeDefOf.RejectInput);
            }

            //TODO: rework construction. This info should really be held in this comp here, rather than in the events queue.
            //      maybe there can still be a "constructing building" event that refers to the SettlementBuilding comp, but
            //      the comp should be the source of truth, not the event
            foreach (FCEvent event1 in FindFC.Events) //check if construction would match any already-occuring events
            {
                if (WorldSettlement.MilitaryComp?.isUnderAttack == true)
                {
                    valid = false;
                    Messages.Message("FCSettlementUnderAttack".Translate(), MessageTypeDefOf.RejectInput);
                }
                if (event1.source == WorldSettlement.Tile && event1.def.defName == "constructBuilding" &&
                    FactionCache.SatisfiesRequirementFor(event1.building, building))
                {
                    valid = false;
                    Messages.Message("FCBuildingBeingBuiltAlreadyType".Translate() + "!", MessageTypeDefOf.RejectInput);
                    break;
                }

                if (event1.source == WorldSettlement.Tile && event1.buildingSlot == buildingSlot &&
                    event1.def.defName == "constructBuilding"
                ) //check if there is already a building being constructed in that slot
                {
                    valid = false;
                    Messages.Message("FCBuildingAlreadyConstructed".Translate() + "!", MessageTypeDefOf.RejectInput);
                    break;
                }
            }

            if (building.minhilliness != Hilliness.Undefined && building.minhilliness > WorldSettlement.Tile.Tile.hilliness)
            {
                valid = false;
                Messages.Message("FCBuildingInvalidEnvironment".Translate(), MessageTypeDefOf.RejectInput);
            }

            if (building.maxhilliness != Hilliness.Undefined && building.maxhilliness < WorldSettlement.Tile.Tile.hilliness)
            {
                valid = false;
                Messages.Message("FCBuildingInvalidEnvironment".Translate(), MessageTypeDefOf.RejectInput);
            }

            if (building.applicableBiomes.Count > 0)
            {
                bool match = building.applicableBiomes.Contains(WorldSettlement.biome);

                //if found no matches
                if (match == false)
                {
                    valid = false;
                    Messages.Message("FCBuildingInvalidEnvironment".Translate(), MessageTypeDefOf.RejectInput);
                }
            }

            // Check settlement type restrictions
            if (!building.CanBeBuiltForSettlementType(WorldSettlement.settlementDef))
            {
                valid = false;
                Messages.Message("FCBuildingInvalidSettlement".Translate(building.LabelCap, WorldSettlement.settlementDef.LabelCap), MessageTypeDefOf.RejectInput);
            }

            // Check tile mutator restrictions
            if (!building.CanBeBuiltOnTile(WorldSettlement.Tile))
            {
                valid = false;
                Messages.Message("FCBuildingInvalidTileMutator".Translate(building.LabelCap), MessageTypeDefOf.RejectInput);
            }

            return valid;
        }
        /// <summary>
        /// Post-load recovery: scans all building slots for BuildingFCExtensions whose comps are missing
        /// (e.g. erroneously destroyed by a prior save due to empty buildingSlots). Re-creates the comp
        /// and populates its slot so downstream code (Tick, gizmos) works correctly.
        /// </summary>
        private void RecoverMissingBuildingComps()
        {
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingFCDef def = GetBuildingInSlot(i);
                if (def?.modExtensions is null) continue;

                foreach (BuildingFCExtension ext in def.modExtensions.OfType<BuildingFCExtension>())
                {
                    if (ext.compClass is null) continue;

                    SettlementBuildingComp comp = GetComponent(ext.compClass);
                    if (comp is null)
                    {
                        LogUtil.Warning($"Recovering missing SettlementBuildingComp {ext.compClass.Name} for building {def.defName} in slot {i}");
                        comp = MakeSettlementBuildingComp(ext.compClass, WorldSettlement);
                        AddComp(comp);
                    }

                    if (!comp.buildingSlots.Contains(i))
                    {
                        comp.buildingSlots.Add(i);
                    }
                }
            }
        }

        public void HandleOnConstructionComps(BuildingFCDef building, int buildingSlot)
        {
            AddBuildingStatModifiers(buildingSlot);

            if (buildings[buildingSlot].def.modExtensions?.Count > 0)
            {
                foreach (BuildingFCExtension ext in buildings[buildingSlot].def.modExtensions.OfType<BuildingFCExtension>())
                {
                    if (ext.compClass != null)
                    {
                        SettlementBuildingComp comp = GetComponent(ext.compClass);

                        if (comp == null)
                        {
                            comp = MakeSettlementBuildingComp(ext.compClass, WorldSettlement);
                            AddComp(comp);
                        }

                        comp.OnConstruct(buildingSlot);
                    }
                }
            }
        }
        public void StartConstruction(BuildingFCDef building, int buildingSlot, int completionTick)
        {
            DeconstructBuilding(buildingSlot);

            LogUtil.Message($"Starting construction of building {building.defName} in slot {buildingSlot} in settlement {WorldSettlement.Name}. Completes on tick {completionTick}");
            dirtyConstructionCache = true;

            buildings[buildingSlot] = new BuildingFC
            {
                def = BuildingFCDefOf.Construction,
                underConstructionDef = building,
                startedTick = Find.TickManager.TicksGame,
                completionTick = completionTick
            };

            // The Construction def shouldn't have traits or modExtensions, I think. But just in case we decide to do something funky,
            //   we'll leave this code here.
            HandleOnConstructionComps(building, buildingSlot);
        }
        /// <summary>
        /// <para>Handles any special processing when a building is first constructed.</para>
        /// </summary>
        public void ConstructBuilding(BuildingFCDef building, int buildingSlot)
        {
            DeconstructBuilding(buildingSlot);

            LogUtil.Message($"Constructing building {building.defName} in slot {buildingSlot} in settlement {WorldSettlement.Name}");
            dirtyConstructionCache = true;

            buildings[buildingSlot] = new BuildingFC
            {
                def = building,
                startedTick = -1,
                completionTick = Find.TickManager.TicksGame
            };

            HandleOnConstructionComps(building, buildingSlot);
            LifecycleRegistry.InvokeOnBuildingConstructed(WorldSettlement, building, buildingSlot);
        }
        /// <summary>
        /// <para>Handles any special processing when a building is deconstructed.</para>
        /// </summary>
        public void DeconstructBuilding(int buildingSlot)
        {
            BuildingFCDef deconstructedDef = buildings[buildingSlot].def;
            LogUtil.Message($"Deconstructing building {deconstructedDef.defName} in slot {buildingSlot} in settlement {WorldSettlement?.Name ?? "nullsettlement"}");
            dirtyConstructionCache = true;
            LifecycleRegistry.InvokeOnBuildingDeconstructed(WorldSettlement, deconstructedDef, buildingSlot);

            RemoveBuildingStatModifiers(buildingSlot);

            if (buildings[buildingSlot].def.modExtensions?.Count > 0)
            {
                foreach (BuildingFCExtension ext in buildings[buildingSlot].def.modExtensions.OfType<BuildingFCExtension>())
                {
                    if (ext.compClass != null)
                    {
                        SettlementBuildingComp comp = GetComponent(ext.compClass);

                        if (comp == null)
                        {
                            LogUtil.Error($"Found null comp for specificed compClass {ext.compClass} in OnDeconstruct. The comp should not be null yet.");
                        }
                        else
                        {
                            comp.OnDeconstruct(buildingSlot);

                            if (comp.CanDestroy)
                            {
                                RemoveComp(comp);
                            }
                        }
                    }
                }
            }

            buildings[buildingSlot].def = BuildingFCDefOf.Empty;
        }
        public void AddBuildingStatModifiers(int buildingSlot)
        {
            if (!buildings[buildingSlot].active) return; // dormant building contributes no stats
            BuildingFCDef def = buildings[buildingSlot].def;
            if (def == BuildingFCDefOf.Empty || def == BuildingFCDefOf.Construction) return;
            WorldSettlement.AddStatModifiers(def.statModifiers, BuildingID(buildingSlot), def.label);
        }

        /* Idempotent dormancy toggle. On change, add/remove the slot's stat modifiers and invalidate the
         * stat + resource caches so production/stat values recompute. capBonuses are intentionally NOT
         * gated (storage capacity is passive -- a mothballed warehouse still stores). */
        public void SetBuildingActive(int buildingSlot, bool active)
        {
            BuildingFC b = buildings[buildingSlot];
            if (b.active == active) return;
            if (active)
            {
                b.active = true;
                AddBuildingStatModifiers(buildingSlot); // re-add (now that active is true)
            }
            else
            {
                RemoveBuildingStatModifiers(buildingSlot); // remove while still registered
                b.active = false;
            }
            WorldSettlement.InvalidateStatCache();
            WorldSettlement.InvalidateResourceCaches();
        }
        public void RemoveBuildingStatModifiers(int buildingSlot)
        {
            BuildingFCDef def = buildings[buildingSlot].def;
            if (def == BuildingFCDefOf.Empty || def == BuildingFCDefOf.Construction) return;
            WorldSettlement.RemoveStatModifiers(def.statModifiers, BuildingID(buildingSlot));
        }
        /// <summary>
        /// Loops through all constructed buildings and applies their stat modifiers to the parent settlement.
        /// <para>Assumes that the parent settlement's stat modifier list has already been cleared.</para>
        /// </summary>
        public void ReapplyBuildingStatModifiers()
        {
            for (int i = 0; i < FC_MAX_BUILDINGS; i++)
            {
                AddBuildingStatModifiers(i);
            }
        }

        public int GetBuildingUpkeep(int buildingSlot)
        {
            if (!buildings[buildingSlot].active) return 0; // mothballed: no upkeep and no income
            return GetBuildingUpkeep(GetBuildingInSlot(buildingSlot));
        }
        public int GetBuildingUpkeep(BuildingFCDef building)
        {
            if (building == null)
                return 0;

            // building.Upkeep applies the legacy per-day divisor for non-postRework (external) defs.
            double upkeep = building.Upkeep + WorldSettlement.GetStatValue(FCStatDefOf.buildingUpkeepBase);
            upkeep += building.isMilitary
                ? WorldSettlement.GetStatValue(FCStatDefOf.buildingUpkeepBase_Military)
                : WorldSettlement.GetStatValue(FCStatDefOf.buildingUpkeepBase_Civilian);

            double mult = WorldSettlement.GetStatValue(FCStatDefOf.buildingUpkeepMultiplier)
                * (building.isMilitary
                    ? WorldSettlement.GetStatValue(FCStatDefOf.buildingUpkeepMultiplier_Military)
                    : WorldSettlement.GetStatValue(FCStatDefOf.buildingUpkeepMultiplier_Civilian));
            upkeep *= mult;

            upkeep = FindFC.PolicyManager.FoldBehaviors(upkeep, (b, u) => b.ModifyBuildingUpkeep(building, u, WorldSettlement));

            // Final per-difficulty factor (sign-preserving).
            upkeep *= FCSettings.buildingUpkeepDifficultyMult;

            // Floor at 0 only for normal buildings, so stat/policy modifiers can't push a regular building
            // negative. Buildings authored as income sources (Upkeep < 0) keep their signed value.
            if (building.Upkeep >= 0 && upkeep < 0) upkeep = 0;
            return (int)upkeep;
        }

        /// <summary>
        /// Stat-modified silver cost to construct the given building in this settlement.
        /// Formula: (def.cost + buildingCostBase + military/civilian base) * buildingCostMultiplier * military/civilian multiplier, floored at 0.
        /// Single source of truth for both the UI display and the actual payment.
        /// </summary>
        public int GetBuildingCost(BuildingFCDef building)
        {
            if (building is null)
                return 0;

            double baseCost = building.cost + WorldSettlement.GetStatValue(FCStatDefOf.buildingCostBase);
            baseCost += building.isMilitary
                ? WorldSettlement.GetStatValue(FCStatDefOf.buildingCostBase_Military)
                : WorldSettlement.GetStatValue(FCStatDefOf.buildingCostBase_Civilian);

            double mult = WorldSettlement.GetStatValue(FCStatDefOf.buildingCostMultiplier)
                * (building.isMilitary
                    ? WorldSettlement.GetStatValue(FCStatDefOf.buildingCostMultiplier_Military)
                    : WorldSettlement.GetStatValue(FCStatDefOf.buildingCostMultiplier_Civilian));

            double cost = baseCost * mult;

            if (cost < 0)
                cost = 0;

            return Convert.ToInt32(cost);
        }

        /// <summary>
        /// Duration in ticks to construct the given building in this settlement.
        /// Formula: def.constructionDuration * buildTimeMultiplier stat * the settings-driven
        /// buildingConstructTimeMultiplier (0 = instant). Single source of truth for both the
        /// UI estimate and the actual construction timer.
        /// </summary>
        public int GetBuildingConstructionTime(BuildingFCDef building)
        {
            if (building is null)
                return 0;

            return (int)(building.constructionDuration
                * WorldSettlement.GetStatValue(FCStatDefOf.buildTimeMultiplier)
                * FCSettings.buildingConstructTimeMultiplier);
        }

        public TaggedString GetBuildingDesc(BuildingFCDef building)
        {
            TaggedString desc = building.FormattedDesc + "\n";
            int buildingUpkeep = GetBuildingUpkeep(building);
            if (buildingUpkeep > 0)
            {
                desc += "\n" + "FCBuildingUpkeep".Translate(buildingUpkeep.ToString());
            }
            else if (buildingUpkeep < 0)
            {
                desc += "\n" + "FCBuildingIncome".Translate(Math.Abs(buildingUpkeep).ToString());
            }

            desc += "\n" + building.AttributeDesc;

            if (building.modExtensions != null)
            {
                foreach (IBuildingDetailSection section in building.modExtensions.OfType<IBuildingDetailSection>())
                {
                    string cardDesc = section.GetCardDescription(building);
                    if (!cardDesc.NullOrEmpty())
                        desc += "\n" + cardDesc;
                }
            }

            return desc.Trim();
        }
        public TaggedString GetBuildingDescFull(BuildingFCDef building)
        {
            TaggedString desc = building.LabelCap + "\n-----\n" + GetBuildingDesc(building);
            return desc;
        }

        public int TotalUpkeep()
        {
            int upkeep = 0;
            foreach (BuildingFC building in buildings)
            {
                if (!building.active) continue; // dormant: no upkeep and no income
                // Signed: income buildings (negative upkeep) reduce the total; a net-negative total is
                // routed to income by RecomputeProfit's buildingsUpkeep < 0 branch.
                upkeep += GetBuildingUpkeep(building.def);
            }
            return upkeep;
        }
        private List<BuildingFilter> filters;

        public void InvalidateFilters()
        {
            filters = null;
        }

        private void RebuildFilters()
        {
            filters = new List<BuildingFilter>();

            filters.Add(new BuildingFilter("FCBuildingFilterAll".Translate(), null, _ => true));

            filters.Add(new BuildingFilter("FCBuildingFilterHappiness".Translate(), TexLoad.iconHappiness, b =>
                b.statModifiers != null && b.statModifiers.Any(m =>
                    (m.stat == FCStatDefOf.happinessLostBase || m.stat == FCStatDefOf.happinessGainedBase ||
                     m.stat == FCStatDefOf.happinessLostMultiplier || m.stat == FCStatDefOf.happinessGainedMultiplier)
                    && m.IsBeneficial())));

            filters.Add(new BuildingFilter("FCBuildingFilterBasetax".Translate(), TexLoad.iconProsperity, b =>
                b.statModifiers != null && b.statModifiers.Any(m =>
                    (m.stat == FCStatDefOf.taxBasePercentage || m.stat == FCStatDefOf.taxBaseRandomModifier)
                    && m.IsBeneficial())));

            filters.Add(new BuildingFilter("FCBuildingFilterWorkers".Translate(), null, b =>
                b.statModifiers != null && b.statModifiers.Any(m =>
                    (m.stat == FCStatDefOf.workerBaseMax || m.stat == FCStatDefOf.workerBaseOverMax || m.stat == FCStatDefOf.workerBaseCost)
                    && m.IsBeneficial())));

            if (WorldSettlement.MilitaryComp != null)
            {
                filters.Add(new BuildingFilter("FCBuildingFilterMilitary".Translate(), TexLoad.iconMilitary, b =>
                    b.statModifiers != null && b.statModifiers.Any(m =>
                        (m.stat == FCStatDefOf.militaryBaseLevel || m.stat == FCStatDefOf.militaryCombatEfficiency)
                        && m.IsBeneficial())));
            }

            foreach (ResourceFC resource in WorldSettlement.Resources)
            {
                ResourceTypeDef resDef = resource.def;
                filters.Add(new BuildingFilter(resource.label, resDef.Icon, b =>
                    b.statModifiers != null && b.statModifiers.Any(m => m.stat != null && m.stat.linkedResource == resDef && m.IsBeneficial())));
            }

            foreach (BuildingFilter filter in BuildingFilterRegistry.Filters)
            {
                filters.Add(filter);
            }
        }

        public int GetFilterSize()
        {
            if (filters == null) RebuildFilters();
            return filters?.Count ?? 0;
        }

        public string GetLabelForFilter(int i)
        {
            if (filters == null) RebuildFilters();
            if (filters == null) return null;
            if (i < 0 || i >= filters.Count) return null;
            return filters[i].label;
        }

        public Texture2D GetIconForFilter(int i)
        {
            if (filters == null) RebuildFilters();
            if (filters == null) return null;
            if (i < 0 || i >= filters.Count) return null;
            return filters[i].icon;
        }

        public bool FilterBuilding(int i, BuildingFCDef building)
        {
            if (filters == null) RebuildFilters();
            if (filters == null) return true;
            if (i < 0 || i >= filters.Count) return true;
            return filters[i].predicate(building);
        }

        /* Comp list management. All membership changes to settlementBuildingComps must go through these
         * so settlementBuildingComps_Ticking stays in lockstep. */

        private void AddComp(SettlementBuildingComp comp)
        {
            settlementBuildingComps.Add(comp);
            if (TickOverrideUtil.Overrides(comp.GetType(), "Tick", typeof(SettlementBuildingComp)))
                settlementBuildingComps_Ticking.Add(comp);
        }

        private void RemoveComp(SettlementBuildingComp comp)
        {
            settlementBuildingComps.Remove(comp);
            settlementBuildingComps_Ticking.Remove(comp);
        }

        // Recovery point after bulk prunes / on load (where the [Unsaved] ticking list starts empty).
        private void RebuildTickingList()
        {
            settlementBuildingComps_Ticking.Clear();
            foreach (SettlementBuildingComp comp in settlementBuildingComps)
            {
                if (TickOverrideUtil.Overrides(comp.GetType(), "Tick", typeof(SettlementBuildingComp)))
                    settlementBuildingComps_Ticking.Add(comp);
            }
        }

        public override void CompTick()
        {
            // base.CompTick() is empty in Rimworld 1.6
            // Invariant: a comp's Tick() must not add/remove comps from this list -- building
            // construction/deconstruction is driven by the parent (HandleOnConstructionComps /
            // Add/RemoveComp), never from inside a tick. So iterating the live list directly is safe.
            foreach (SettlementBuildingComp comp in settlementBuildingComps_Ticking)
            {
                comp.Tick();
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            /* Before saving: prune orphaned comps so we don't write garbage. */
            if (Scribe.mode == LoadSaveMode.Saving)
                PrepareCompsForSave();

            Scribe_Collections.Look(ref buildings, "buildings", LookMode.Deep);
            Scribe_Collections.Look(ref settlementBuildingComps, "settlementBuildingComps", LookMode.Deep);

            /* After load: validate comps and recover any missing ones. Stays in
             * PostExposeData (not ISettlementPostLoadInit) because WorldSettlementFC.PostLoadInit
             * calls BuildingsComp.ReapplyBuildingStatModifiers — the comp list must be
             * validated before that runs. PostExposeData is called as part of base.ExposeData()
             * inside WorldSettlementFC.ExposeData, which runs before WorldSettlementFC.PostLoadInit. */
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                RecoverCompsOnLoad();
        }

        /* Orphan cleanup before save. Comps *should* be destroyed when their building is
         * deconstructed, but defensively drop any dead ones so they aren't persisted. */
        private void PrepareCompsForSave()
        {
            foreach (SettlementBuildingComp comp in settlementBuildingComps)
            {
                comp.RefreshBuildingSlotsWithErrorDetection();
            }
            settlementBuildingComps.RemoveAll(comp => comp.CanDestroy);
            ReinitBuildings();
            RebuildTickingList();
        }

        private void RecoverCompsOnLoad()
        {
            if (buildings is null)
                buildings = new List<BuildingFC>();
            if (settlementBuildingComps is null)
                settlementBuildingComps = new List<SettlementBuildingComp>();
            settlementBuildingComps.RemoveAll(c => c == null);
            foreach (SettlementBuildingComp comp in settlementBuildingComps)
            {
                comp.RefreshBuildingSlotsWithErrorDetection();
            }
            settlementBuildingComps.RemoveAll(comp => comp.CanDestroy);
            RecoverMissingBuildingComps();
            ReinitBuildings();
            RebuildTickingList();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            IEnumerable<Gizmo> gizmos = base.GetGizmos();
            if (gizmos != null)
            {
                foreach (Gizmo gizmo in gizmos)
                {
                    yield return gizmo;
                }
            }
            foreach (SettlementBuildingComp comp in settlementBuildingComps)
            {
                gizmos = comp.GetGizmos();
                if (gizmos == null)
                {
                    continue;
                }
                foreach (Gizmo gizmo in gizmos)
                {
                    yield return gizmo;
                }
            }
        }
    }
}
