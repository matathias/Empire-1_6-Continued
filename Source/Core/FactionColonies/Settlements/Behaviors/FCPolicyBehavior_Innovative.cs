using RimWorld;
using System;
using Verse;

namespace FactionColonies
{
    public class FCPolicyBehavior_Innovative : FCPolicyBehavior
    {
        public override void OnTaxCollected(FactionFC faction, WorldSettlementFC settlement)
        {
            double profit = settlement.totalProfit;
            if (profit <= 0.0) return;

            ResourcePool pool = faction.resourcePools.Find(p => p.resource == ResourceTypeDefOf.RTD_Research);
            if (pool == null) return;
            double researchPoints = profit * Ext<FCPolicyBehaviorExt_Innovative>().profitToResearchRate;
            pool.pool += researchPoints;

            Messages.Message("FCInnovativeMessage".Translate(settlement.Name, Math.Round(researchPoints)), MessageTypeDefOf.PositiveEvent);
        }
    }
}
