namespace FactionColonies
{
    public class FCPolicyBehavior_Expansionist : FCPolicyBehavior
    {
        public override void OnSettlementCreated(FactionFC faction, WorldSettlementFC settlement)
        {
            var ext = Ext<FCPolicyBehaviorExt_Expansionist>();

            int targetLevel = ext.autoUpgradeToLevel;
            if (settlement.settlementLevel < targetLevel)
                settlement.UpgradeSettlement();

            settlement.prosperity = ext.startingProsperity;
        }
    }
}
