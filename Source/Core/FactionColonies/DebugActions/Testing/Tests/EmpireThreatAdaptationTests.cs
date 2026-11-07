namespace FactionColonies;

/* Tests for EmpireThreatAdaptation. The internal adaptDays field is private, so most tests
   observe behavior through ThreatFactor (which reads from Find.Storyteller's curves) or
   through pure exception-safety on Notify_*. Tests that need specific storyteller curves
   swap them in and live in EmpireThreatAdaptationDestructiveTests. */
public static class EmpireThreatAdaptationTests
{
    // -*- ThreatFactor -*-

    [EmpireTest("ThreatAdaptation")]
    public static void ThreatFactor_FreshInstance_FiniteAndPositive()
    {
        var adapt = new EmpireThreatAdaptation();
        double factor = adapt.ThreatFactor;
        TestAssert.IsFalse(double.IsNaN(factor), "ThreatFactor should not be NaN");
        TestAssert.IsFalse(double.IsInfinity(factor), "ThreatFactor should not be infinite");
        TestAssert.GreaterThan(factor, 0, "ThreatFactor should be positive");
    }

    // -*- Notify_BattleWon -*-

    [EmpireTest("ThreatAdaptation")]
    public static void NotifyBattleWon_DoesNotThrow()
    {
        var adapt = new EmpireThreatAdaptation();
        TestAssert.DoesNotThrow(() => adapt.Notify_BattleWon());
    }

    [EmpireTest("ThreatAdaptation")]
    public static void NotifyBattleWon_ClampsAtMax()
    {
        // Spam BattleWon way past the storyteller's adaptDaysMax; subsequent ThreatFactor
        // calls should stabilize (idempotent at the clamp).
        var adapt = new EmpireThreatAdaptation();
        for (int i = 0; i < 1000; i++) adapt.Notify_BattleWon();
        double afterFirstSpam = adapt.ThreatFactor;
        for (int i = 0; i < 1000; i++) adapt.Notify_BattleWon();
        double afterSecondSpam = adapt.ThreatFactor;

        TestAssert.AreEqual(afterFirstSpam, afterSecondSpam, tolerance: 0.0001,
            "ThreatFactor should stabilize once adaptDays is clamped at max");
    }

    // -*- Notify_BattleLost -*-

    [EmpireTest("ThreatAdaptation")]
    public static void NotifyBattleLost_DoesNotThrow_RequiresFactionFC()
    {
        // Notify_BattleLost reads FindFC.Settlements.Count for the curve sample.
        DestructiveTestUtil.RequireFaction();

        var adapt = new EmpireThreatAdaptation();
        TestAssert.DoesNotThrow(() => adapt.Notify_BattleLost());
    }

    // -*- ExposeData round-trip -*-

    [EmpireTest("ThreatAdaptation")]
    public static void ExposeData_DoesNotThrow_OnFreshInstance()
    {
        // The Scribe machinery has no test driver in this framework; we only verify the
        // method shape is invokable without runtime errors in the no-op (no active scribe
        // mode) case.
        var adapt = new EmpireThreatAdaptation();
        TestAssert.DoesNotThrow(() => adapt.ExposeData());
    }
}
