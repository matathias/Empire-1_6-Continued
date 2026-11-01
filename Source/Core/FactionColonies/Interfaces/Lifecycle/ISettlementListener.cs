namespace FactionColonies
{
    /// <summary>
    /// Lifecycle hook for settlement and building events. Register implementations via
    /// <see cref="LifecycleRegistry"/>. Fired after the corresponding action completes,
    /// with caches already invalidated; settlement caches are invalidated again between
    /// participants so later ones see changes from earlier ones.
    /// </summary>
    public interface ISettlementListener
    {
        void OnSettlementCreated(WorldSettlementFC settlement);
        void OnSettlementRemoved(WorldSettlementFC settlement);
        void OnSettlementUpgraded(WorldSettlementFC settlement, int oldLevel, int newLevel);
        void OnSettlementTypeChanged(WorldSettlementFC settlement, WorldSettlementDef oldDef, WorldSettlementDef newDef);
        void OnBuildingConstructed(WorldSettlementFC settlement, BuildingFCDef building, int slot);
        void OnBuildingDeconstructed(WorldSettlementFC settlement, BuildingFCDef building, int slot);
    }
}
