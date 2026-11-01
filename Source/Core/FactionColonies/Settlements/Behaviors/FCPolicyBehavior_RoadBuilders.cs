using Verse;

namespace FactionColonies
{
    public class FCPolicyBehavior_RoadBuilders : FCPolicyBehavior
    {
        public override void OnEnacted(FactionFC faction)
        {
            ResearchProjectDef researchDef = Ext<FCPolicyBehaviorExt_RoadBuilders>().autoUnlockResearch;
            if (researchDef == null)
                LogUtil.Error("Road research returned Null");
            else if (Find.ResearchManager.GetProgress(researchDef) != researchDef.baseCost)
                Find.ResearchManager.FinishProject(researchDef);
        }
    }
}
