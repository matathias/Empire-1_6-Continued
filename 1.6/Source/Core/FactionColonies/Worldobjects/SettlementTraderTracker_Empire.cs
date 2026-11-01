using RimWorld;
using RimWorld.Planet;

namespace FactionColonies
{
    /// <summary>
    /// Thin subclass of Settlement_TraderTracker that injects the settlement reference
    /// into EmpireStockGenerators before stock generation, enabling resource-weighted stock.
    /// </summary>
    public class SettlementTraderTracker_Empire : Settlement_TraderTracker
    {
        public SettlementTraderTracker_Empire(Settlement settlement) : base(settlement)
        {
        }

        protected override void RegenerateStock()
        {
            WorldSettlementFC worldSettlement = settlement as WorldSettlementFC;

            LogUtil.Message($"SettlementTraderTracker_Empire: Regenerating Stock");

            // Set settlement reference on each EmpireStockGenerator
            if (worldSettlement is object && TraderKind is object)
            {
                foreach (StockGenerator gen in TraderKind.stockGenerators)
                {
                    if (gen is EmpireStockGenerator empireGen)
                        empireGen.settlement = worldSettlement;
                }
            }

            base.RegenerateStock();

            // Clear settlement references to prevent serialization issues
            if (TraderKind is object)
            {
                foreach (StockGenerator gen in TraderKind.stockGenerators)
                {
                    if (gen is EmpireStockGenerator empireGen)
                        empireGen.settlement = null;
                }
            }
        }
    }
}
