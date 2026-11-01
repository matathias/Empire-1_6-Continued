using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FactionColonies
{
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
                researchProjects = new List<ResearchProjectDef>(),
                mode = TechBarrierMode.All
            };
            TestAssert.IsTrue(allBarrier.IsSatisfied(),
                "Empty All-mode barrier should be satisfied (nothing required)");

            var anyBarrier = new TechLevelBarrier
            {
                researchProjects = new List<ResearchProjectDef>(),
                mode = TechBarrierMode.Any
            };
            TestAssert.IsFalse(anyBarrier.IsSatisfied(),
                "Empty Any-mode barrier should be unsatisfied (nothing can satisfy it)");
        }

        // Regression guard: a knowledge-cost project (baseCost == 0, knowledgeCost > 0) with no
        // progress must leave its All-mode barrier UNsatisfied. Skipped when Anomaly is active
        // because reading IsFinished then routes through ResearchManager.GetProgress, which would
        // insert this synthetic def into the live anomalyKnowledge dict (a non-destructive test
        // must leave game state untouched). The arithmetic under test is DLC-independent — Cost
        // falls back to knowledgeCost either way — so exercising it without Anomaly is faithful.
        [EmpireTest("TechProgression")]
        public static void Barrier_KnowledgeCostProject_NotSatisfiedWhileUnresearched()
        {
            if (Current.Game == null || Find.ResearchManager == null)
                TestAssert.Skip("No ResearchManager (no active game)");
            if (ModsConfig.AnomalyActive)
                TestAssert.Skip("Anomaly active — skipped to avoid mutating live anomalyKnowledge");

            var proj = new ResearchProjectDef
            {
                defName = "EmpireTest_KnowledgeCostProj",
                baseCost = 0f,
                knowledgeCost = 100f
            };
            var barrier = new TechLevelBarrier
            {
                techLevel = TechLevel.Industrial,
                researchProjects = new List<ResearchProjectDef> { proj },
                mode = TechBarrierMode.All
            };

            TestAssert.IsFalse(proj.IsFinished,
                "A knowledge-cost project with zero progress should not be finished");
            TestAssert.IsFalse(barrier.IsSatisfied(),
                "Barrier requiring an unresearched knowledge-cost project should NOT be satisfied");
        }

        // Contrast: a genuinely costless project (baseCost == 0 AND knowledgeCost == 0) has nothing
        // to research, so it reads as finished and its All-mode barrier is satisfied. GetProgress
        // returns 0 without mutating any dict here, so this is safe regardless of Anomaly.
        [EmpireTest("TechProgression")]
        public static void Barrier_ZeroCostProject_IsSatisfied()
        {
            if (Current.Game == null || Find.ResearchManager == null)
                TestAssert.Skip("No ResearchManager (no active game)");

            var proj = new ResearchProjectDef
            {
                defName = "EmpireTest_ZeroCostProj",
                baseCost = 0f,
                knowledgeCost = 0f
            };
            var barrier = new TechLevelBarrier
            {
                techLevel = TechLevel.Industrial,
                researchProjects = new List<ResearchProjectDef> { proj },
                mode = TechBarrierMode.All
            };

            TestAssert.IsTrue(proj.IsFinished,
                "A project with no cost at all should read as finished");
            TestAssert.IsTrue(barrier.IsSatisfied(),
                "All-mode barrier of a costless project should be satisfied");
        }
    }
}
