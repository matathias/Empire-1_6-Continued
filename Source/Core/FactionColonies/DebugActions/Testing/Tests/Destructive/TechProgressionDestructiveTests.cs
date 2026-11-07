namespace FactionColonies;

/* DESTRUCTIVE: with Anomaly active, reading a knowledge-cost project's progress inserts it into the
   live ResearchManager.anomalyKnowledge dictionary. The synthetic project is removed again afterwards
   so the save never references an unknown def. */
public static class TechProgressionDestructiveTests
{
    // Regression guard: a knowledge-cost project (baseCost == 0, knowledgeCost > 0) with no
    // progress must leave its All-mode barrier UNsatisfied. Cost falls back to knowledgeCost with
    // or without Anomaly, so the arithmetic under test is the same either way; only the
    // progress lookup differs.
    [EmpireDestructiveTest("Destructive.TechProgression")]
    public static void Barrier_KnowledgeCostProject_NotSatisfiedWhileUnresearched()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        ResearchManager research = TestAssert.NotNull(Find.ResearchManager, "No ResearchManager in a loaded game");

        var proj = new ResearchProjectDef
        {
            defName = "EmpireTest_KnowledgeCostProj",
            baseCost = 0f,
            knowledgeCost = 100f
        };
        var barrier = new TechLevelBarrier
        {
            techLevel = TechLevel.Industrial,
            researchProjects = [proj],
            mode = TechBarrierMode.All
        };

        try
        {
            TestAssert.IsFalse(proj.IsFinished,
                "A knowledge-cost project with zero progress should not be finished");
            TestAssert.IsFalse(barrier.IsSatisfied(),
                "Barrier requiring an unresearched knowledge-cost project should NOT be satisfied");
        }
        finally
        {
            PrivateFieldUtil.AnomalyKnowledge(research)?.Remove(proj);
        }
        TestAssert.IsFalse(PrivateFieldUtil.AnomalyKnowledge(research)?.ContainsKey(proj) ?? false,
            "The synthetic project should not be left in the live anomaly knowledge table");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "Barrier_KnowledgeCostProject_NotSatisfiedWhileUnresearched");
    }
}
