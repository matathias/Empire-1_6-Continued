using RimWorld.Planet;
using System;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// This class allows resources to specify conditional additives/multipliers.
    /// </summary>
    public abstract class ResourceProductionExtension : DefModExtension
    {
        public string extName;
        public string extDesc;
        public virtual double GetAdditiveBonus(PlanetTile tile, WorldSettlementFC settlement = null)
        {
            return 0;
        }
        public virtual double GetMultiplierBonus(PlanetTile tile, WorldSettlementFC settlement = null)
        {
            return 1;
        }

        /// <summary>
        /// Emits one or more labeled contributions to the production breakdown. The default
        /// implementation funnels the aggregate GetAdditiveBonus / GetMultiplierBonus values
        /// into a single row each, preserving legacy single-row extension behavior. Subclasses
        /// that compute their bonus from multiple sources (e.g. per-curve tile fields) should
        /// override this to emit a row per source so the breakdown UI can show per-factor detail.
        /// Callback parameters: (idSuffix, value, label).
        /// </summary>
        public virtual void ContributeToBreakdown(
            PlanetTile tile,
            WorldSettlementFC settlement,
            Action<string, double, string> addAdditive,
            Action<string, double, string> addMultiplier)
        {
            double add = GetAdditiveBonus(tile, settlement);
            if (add != 0) addAdditive(extName, add, extName);
            double mult = GetMultiplierBonus(tile, settlement);
            if (mult != 1) addMultiplier(extName, mult, extDesc);
        }
    }
}
