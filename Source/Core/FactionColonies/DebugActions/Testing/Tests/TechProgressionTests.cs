namespace FactionColonies;

/// <summary>
/// Covers <see cref="TechLevelBarrier.IsSatisfied"/>. The regression of note: a project with
/// baseCost == 0 (Anomaly knowledge-cost projects) must not count as finished merely because
/// progress is "not below zero". The old code compared GetProgress &lt; baseCost (0 &lt; 0 == false)
/// and wrongly reported such barriers satisfied; the fix delegates to ResearchProjectDef.IsFinished.
/// </summary>
public static class TechProgressionTests
{
    // Empty-list semantics are pure (no game state needed): All is satisfied (nothing required),
    // Any is unsatisfied (nothing can satisfy it).
    [EmpireTest("TechProgression")]
    public static void Barrier_EmptyList_AllSatisfied_AnyUnsatisfied()
    {
        var allBarrier = new TechLevelBarrier
        {
            researchProjects = [],
            mode = TechBarrierMode.All
        };
        TestAssert.IsTrue(allBarrier.IsSatisfied(),
            "Empty All-mode barrier should be satisfied (nothing required)");

        var anyBarrier = new TechLevelBarrier
        {
            researchProjects = [],
            mode = TechBarrierMode.Any
        };
        TestAssert.IsFalse(anyBarrier.IsSatisfied(),
            "Empty Any-mode barrier should be unsatisfied (nothing can satisfy it)");
    }

    // The knowledge-cost regression guard reads ResearchManager progress, which under Anomaly writes
    // into live state, so it lives in TechProgressionDestructiveTests.

    // Contrast: a genuinely costless project (baseCost == 0 AND knowledgeCost == 0) has nothing
    // to research, so it reads as finished and its All-mode barrier is satisfied. GetProgress
    // returns 0 without mutating any dict here, so this is safe regardless of Anomaly.
    [EmpireTest("TechProgression")]
    public static void Barrier_ZeroCostProject_IsSatisfied()
    {
        var proj = new ResearchProjectDef
        {
            defName = "EmpireTest_ZeroCostProj",
            baseCost = 0f,
            knowledgeCost = 0f
        };
        var barrier = new TechLevelBarrier
        {
            techLevel = TechLevel.Industrial,
            researchProjects = [proj],
            mode = TechBarrierMode.All
        };

        TestAssert.IsTrue(proj.IsFinished,
            "A project with no cost at all should read as finished");
        TestAssert.IsTrue(barrier.IsSatisfied(),
            "All-mode barrier of a costless project should be satisfied");
    }
}
