using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// A special custom 'comp' class for use with BuildingFCDefs. A building can specify a SettlementBuildingComp through defmodextensions.
    /// When the building is constructed, its associated SettlementBuildingComp will be added to the settlement's WorldObjectComp_SettlementBuildings,
    /// which will handle all of the building processing.
    /// </summary>
    public abstract class SettlementBuildingComp : IExposable
    {
        public WorldSettlementFC settlement;
        public List<int> buildingSlots = new List<int>();
        public bool CanDestroy => buildingSlots.Count == 0;
        public WorldObjectComp_SettlementBuildings parentComp => settlement.GetComponent<WorldObjectComp_SettlementBuildings>();
        public virtual void ExposeData()
        {
            Scribe_References.Look(ref settlement, "settlement");
            Scribe_Collections.Look(ref buildingSlots, "buildingSlots", LookMode.Value);
        }

        /// <summary>
        /// A heavy-handed function to refresh the buildingSlots array. Should be called infrequently (currently only when saving).
        /// Meant to ensure that we don't have any memory leaks by leaving behind orphaned SettlementBuildingComps.
        /// </summary>
        public void RefreshBuildingSlots()
        {
            buildingSlots.Clear();
            if (parentComp?.Buildings is null)
            {
                LogUtil.Error($"SettlementBuildingComp {this.ToStringSafe()} has null parentComp during RefreshBuildingSlots");
                return;
            }
            for (int i = 0; i < parentComp.Buildings.Count; i++)
            {
                BuildingFCDef building = parentComp.GetBuildingInSlot(i);
                if (building.modExtensions?.Count > 0)
                {
                    foreach (BuildingFCExtension ext in building.modExtensions.OfType<BuildingFCExtension>())
                    {
                        if (ext.compClass != null)
                        {
                            SettlementBuildingComp comp = parentComp?.GetComponent(ext.compClass);

                            if (comp == this)
                            {
                                buildingSlots.Add(i);
                                continue;
                            }
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Refreshes the building slots array by calling RefreshBuildingSlots(). If the resulting building count is different from the
        /// initial building count, then we log a warning.
        /// </summary>
        public void RefreshBuildingSlotsWithErrorDetection()
        {
            int oldSlotCount = buildingSlots.Count;
            RefreshBuildingSlots();
            int newSlotCount = buildingSlots.Count;
            if (oldSlotCount != newSlotCount)
            {
                LogUtil.Warning($"Detected buildingSlot count discrepancy in SettlementBuildingComp {this.ToStringSafe()} | old count: {oldSlotCount} new count: {newSlotCount}");
            }
        }

        /// <summary>
        /// Called on every tick. Meant to handle any special tick-based processing for the building.
        /// </summary>
        public virtual void Tick()
        {
        }
        /// <summary>
        /// Called when a building is constructed.
        /// <para>This parent function should be called at the *beginning* of any subclass functions.</para>
        /// </summary>
        /// <param name="buildingSlot">Index into the settlement's buildings array, indicating where this building should be constructed.</param>
        public virtual void OnConstruct(int buildingSlot)
        {
            buildingSlots.Add(buildingSlot);
        }
        /// <summary>
        /// Called when a building is deconstructed.
        /// <para>This parent function should be called at the *end* of any subclass functions.</para>
        /// </summary>
        /// <param name="buildingSlot">Index into the settlement's buildings array, indicating where this building is currently built.</param>
        public virtual void OnDeconstruct(int buildingSlot)
        {
            buildingSlots.Remove(buildingSlot);
        }

        public virtual IEnumerable<Gizmo> GetGizmos()
        {
            return null;
        }
    }
}
