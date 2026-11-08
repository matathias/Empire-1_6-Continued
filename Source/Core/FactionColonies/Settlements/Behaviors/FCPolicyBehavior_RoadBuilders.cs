namespace FactionColonies;

public class FCPolicyBehavior_RoadBuilders : FCPolicyBehavior
{
    public override void OnEnacted(FactionFC faction)
    {
        ResearchProjectDef researchDef = Ext<FCPolicyBehaviorExt_RoadBuilders>().autoUnlockResearch;
        if (researchDef is null)
            LogUtil.Error("Road research returned Null");
        else if (!researchDef.IsFinished)
            Find.ResearchManager.FinishProject(researchDef);
    }
}
