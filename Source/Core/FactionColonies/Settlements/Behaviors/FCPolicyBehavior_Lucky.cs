using Verse;

namespace FactionColonies
{
    public class FCPolicyBehavior_Lucky : FCPolicyBehavior
    {
        public override bool ShouldRerollEvent(FCEventDef eventDef)
        {
            if (!eventDef.isNegative) return false;
            if (!Rand.Chance(Ext<FCPolicyBehaviorExt_Lucky>().rerollChance)) return false;
            LogUtil.Message($"Lucky trait re-rolled negative event: {eventDef.defName}");
            return true;
        }
    }
}
