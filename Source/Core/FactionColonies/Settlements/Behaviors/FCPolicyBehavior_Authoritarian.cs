using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    public class FCPolicyBehavior_Authoritarian : FCPolicyBehavior
    {
        public override void OnSettlementCreated(FactionFC faction, WorldSettlementFC settlement)
        {
            settlement.loyalty = Ext<FCPolicyBehaviorExt_Authoritarian>().startingLoyalty;
        }

        public override IEnumerable<FloatMenuOption> GetSettlementActions(FactionFC faction, WorldSettlementFC settlement)
        {
            yield return new FloatMenuOption("FCBuyLoyalty".Translate(),
                delegate { Find.WindowStack.Add(new FCWindow_Pay_Silver_Loyalty(settlement)); });
        }
    }
}
