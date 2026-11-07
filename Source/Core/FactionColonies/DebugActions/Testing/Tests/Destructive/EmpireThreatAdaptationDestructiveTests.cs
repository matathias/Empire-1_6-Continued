namespace FactionColonies;

/* DESTRUCTIVE: temporarily swaps the active storyteller def's adaptation curves and the
   difficulty's adaptationEffectFactor (restored afterwards), so every EmpireThreatAdaptation
   branch runs regardless of which storyteller the save uses. */
public static class EmpireThreatAdaptationDestructiveTests
{
    /// <summary>Linear 1 -> 3 over adaptDays [0, 1], with a 0.5 floor at -100 so clamps below zero are visible.</summary>
    private static SimpleCurve SteepPointsCurve() =>
    [
        new CurvePoint(-100f, 0.5f),
        new CurvePoint(0f, 1f),
        new CurvePoint(1f, 3f),
    ];

    /// <summary>Runs <paramref name="body"/> with the given curves installed on the live storyteller
    /// def and an effect factor of 1, restoring the originals afterwards.</summary>
    private static void WithStorytellerCurves(SimpleCurve pointsCurve, SimpleCurve lossCurve, Action body)
    {
        StorytellerDef def = TestAssert.NotNull(Find.Storyteller?.def, "No storyteller def in a loaded game");
        Difficulty difficulty = TestAssert.NotNull(Find.Storyteller.difficulty, "No storyteller difficulty in a loaded game");

        SimpleCurve savedPoints = def.pointsFactorFromAdaptDays;
        SimpleCurve savedLoss = def.adaptDaysLossFromColonistLostByPostPopulation;
        float savedEffect = difficulty.adaptationEffectFactor;
        def.pointsFactorFromAdaptDays = pointsCurve;
        def.adaptDaysLossFromColonistLostByPostPopulation = lossCurve;
        difficulty.adaptationEffectFactor = 1f;
        try { body(); }
        finally
        {
            def.pointsFactorFromAdaptDays = savedPoints;
            def.adaptDaysLossFromColonistLostByPostPopulation = savedLoss;
            difficulty.adaptationEffectFactor = savedEffect;
        }
    }

    [EmpireDestructiveTest("Destructive.ThreatAdaptation")]
    public static void ThreatFactor_NullPointsCurve_ReturnsOne()
    {
        // Defensive guard in ThreatFactor — null curve short-circuits to 1.0.
        WithStorytellerCurves(null, null, () =>
        {
            var adapt = new EmpireThreatAdaptation();
            for (int i = 0; i < 20; i++) adapt.Notify_BattleWon();
            TestAssert.AreEqual(1.0, adapt.ThreatFactor, message: "ThreatFactor should be 1.0 with no points curve");
        });
    }

    [EmpireDestructiveTest("Destructive.ThreatAdaptation")]
    public static void NotifyBattleWon_ChangesThreatFactor()
    {
        DestructiveTestUtil.RequireFaction();
        WithStorytellerCurves(SteepPointsCurve(), null, () =>
        {
            // 100 wins push adaptDays from 0 to min(200 * growthMult, adaptDaysMax).
            StorytellerDef sdef = Find.Storyteller.def;
            float growthMult = (float)FindFC.FactionComp.GetStatValue(FCStatDefOf.threatAdaptationGrowthMultiplier);
            TestAssert.GreaterThan(growthMult, 0, "threatAdaptationGrowthMultiplier should be positive");
            TestAssert.GreaterThan(sdef.adaptDaysMax, 0, "Storyteller adaptDaysMax should be positive");
            float endAdaptDays = Math.Min(200f * growthMult, sdef.adaptDaysMax);
            float predictedAfter = sdef.pointsFactorFromAdaptDays.Evaluate(endAdaptDays);

            var adapt = new EmpireThreatAdaptation();
            double before = adapt.ThreatFactor;
            for (int i = 0; i < 100; i++) adapt.Notify_BattleWon();
            double after = adapt.ThreatFactor;

            TestAssert.AreEqual(1.0, before, tolerance: 0.0001, message: "Fresh instance should sit at curve(0) = 1");
            TestAssert.AreEqual(predictedAfter, after, tolerance: 0.0001,
                message: $"ThreatFactor after 100 wins should be curve({endAdaptDays})");
            TestAssert.IsTrue(after - before > 0.0001,
                $"Expected ThreatFactor to rise after 100 BattleWon calls; before={before}, after={after}");
        });
    }

    [EmpireDestructiveTest("Destructive.ThreatAdaptation")]
    public static void NotifyBattleLost_NullCurve_NoOp()
    {
        // Guard in Notify_BattleLost — null curve makes the call a silent no-op.
        DestructiveTestUtil.RequireFaction();
        WithStorytellerCurves(SteepPointsCurve(), null, () =>
        {
            var adapt = new EmpireThreatAdaptation();
            // Push adaptDays up so a non-no-op call would visibly lower it.
            for (int i = 0; i < 20; i++) adapt.Notify_BattleWon();
            double before = adapt.ThreatFactor;

            TestAssert.DoesNotThrow(() => adapt.Notify_BattleLost());
            double after = adapt.ThreatFactor;

            TestAssert.AreEqual(before, after, tolerance: 0.0001,
                "Notify_BattleLost should be a no-op when the storyteller has no loss curve");
        });
    }

    [EmpireDestructiveTest("Destructive.ThreatAdaptation")]
    public static void NotifyBattleLost_ClampsAtMin()
    {
        // Mirror of NotifyBattleWon_ClampsAtMax: spam BattleLost beyond the floor.
        DestructiveTestUtil.RequireFaction();
        SimpleCurve constantLoss = [new CurvePoint(0f, 5f), new CurvePoint(1000f, 5f)];
        WithStorytellerCurves(SteepPointsCurve(), constantLoss, () =>
        {
            float floor = Find.Storyteller.def.adaptDaysMin;
            double predicted = Find.Storyteller.def.pointsFactorFromAdaptDays.Evaluate(floor);

            var adapt = new EmpireThreatAdaptation();
            for (int i = 0; i < 1000; i++) adapt.Notify_BattleLost();
            double after1 = adapt.ThreatFactor;
            for (int i = 0; i < 1000; i++) adapt.Notify_BattleLost();
            double after2 = adapt.ThreatFactor;

            TestAssert.AreEqual(predicted, after1, tolerance: 0.0001,
                $"ThreatFactor should sit at curve(adaptDaysMin={floor}) after spamming losses");
            TestAssert.AreEqual(after1, after2, tolerance: 0.0001,
                "ThreatFactor should stabilize once adaptDays is clamped at min");
        });
    }
}
