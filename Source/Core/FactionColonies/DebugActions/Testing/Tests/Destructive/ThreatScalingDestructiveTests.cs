namespace FactionColonies;

/* DESTRUCTIVE: threat-level, handicap-cap and attack-interval math against the live faction. Creates a
   transient settlement when the player has none (these computations need at least one). Settings and
   registry contributors changed here are restored in finally blocks; created settlements are not. */
public static class ThreatScalingDestructiveTests
{
    private class TestContributor : IThreatScalingContributor
    {
        public double Additive;
        public double Multiplicative = 1.0;
        public double GetAdditiveContribution(FactionFC f) => Additive;
        public double GetMultiplicativeContribution(FactionFC f) => Multiplicative;
    }

    // ============================
    // Tier 3: ETL Computation (requires a settlement)
    // DORMANT — ComputeEmpireThreatLevel is no longer in the live raid path (removed in the
    // 2026-06-14 threat-scaling simplification); retained for a future threat-scaling submod.
    // The live raid path is covered by Tier 7 / Tier 8 in ThreatScalingTests.
    // ============================

    [EmpireDestructiveTest("Destructive.ThreatScaling")]
    public static void ETL_WithSettlements_IsAtLeastOne()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        DestructiveTestUtil.EnsureSettlement();

        double etl = ThreatScalingUtil.ComputeEmpireThreatLevel(faction);
        TestAssert.IsTrue(etl >= 1.0,
            $"ETL should be >= 1.0, got {etl}");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "ETL_WithSettlements_IsAtLeastOne");
    }

    [EmpireDestructiveTest("Destructive.ThreatScaling")]
    public static void ETL_WithSettlements_IsCappedByMaxThreatMultiplier()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        DestructiveTestUtil.EnsureSettlement();

        double etl = ThreatScalingUtil.ComputeEmpireThreatLevel(faction);
        TestAssert.LessThanOrEqual(etl, FCSettings.maxThreatMultiplier,
            $"ETL should be <= maxThreatMultiplier ({FCSettings.maxThreatMultiplier}), got {etl}");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "ETL_WithSettlements_IsCappedByMaxThreatMultiplier");
    }

    [EmpireDestructiveTest("Destructive.ThreatScaling")]
    public static void ETL_IsFiniteNumber()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        DestructiveTestUtil.EnsureSettlement();

        double etl = ThreatScalingUtil.ComputeEmpireThreatLevel(faction);
        TestAssert.IsFalse(double.IsNaN(etl), "ETL should not be NaN");
        TestAssert.IsFalse(double.IsInfinity(etl), "ETL should not be infinite");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "ETL_IsFiniteNumber");
    }

    [EmpireDestructiveTest("Destructive.ThreatScaling")]
    public static void ETL_LoweredCap_ClampsProperly()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        DestructiveTestUtil.EnsureSettlement();

        float original = FCSettings.maxThreatMultiplier;
        try
        {
            FCSettings.maxThreatMultiplier = 1.0f;
            double etl = ThreatScalingUtil.ComputeEmpireThreatLevel(faction);
            TestAssert.AreEqual(1.0, etl,
                message: $"ETL should be clamped to 1.0 when cap is 1.0, got {etl}");
        }
        finally
        {
            FCSettings.maxThreatMultiplier = original;
        }
        DestructiveTestUtil.AssertEmpireInvariants(faction, "ETL_LoweredCap_ClampsProperly");
    }

    [EmpireDestructiveTest("Destructive.ThreatScaling")]
    public static void ETL_RegistryAdditive_IncreasesETL()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        DestructiveTestUtil.EnsureSettlement();

        double baseline = ThreatScalingUtil.ComputeEmpireThreatLevel(faction);
        var c = new TestContributor { Additive = 0.5 };
        ThreatScalingRegistry.Register(c);
        try
        {
            double boosted = ThreatScalingUtil.ComputeEmpireThreatLevel(faction);
            TestAssert.IsTrue(boosted >= baseline,
                $"ETL with additive contributor ({boosted}) should be >= baseline ({baseline})");
        }
        finally
        {
            ThreatScalingRegistry.Unregister(c);
        }
        DestructiveTestUtil.AssertEmpireInvariants(faction, "ETL_RegistryAdditive_IncreasesETL");
    }

    [EmpireDestructiveTest("Destructive.ThreatScaling")]
    public static void ETL_RegistryMultiplicative_IncreasesETL()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        DestructiveTestUtil.EnsureSettlement();

        double baseline = ThreatScalingUtil.ComputeEmpireThreatLevel(faction);
        var c = new TestContributor { Multiplicative = 1.5 };
        ThreatScalingRegistry.Register(c);
        try
        {
            double boosted = ThreatScalingUtil.ComputeEmpireThreatLevel(faction);
            TestAssert.IsTrue(boosted >= baseline,
                $"ETL with multiplicative contributor ({boosted}) should be >= baseline ({baseline})");
        }
        finally
        {
            ThreatScalingRegistry.Unregister(c);
        }
        DestructiveTestUtil.AssertEmpireInvariants(faction, "ETL_RegistryMultiplicative_IncreasesETL");
    }

    // ============================
    // Tier 4: Handicap Cap (requires a settlement)
    // DORMANT — ComputeHandicapCap is no longer in the live raid path (incoming raids use the
    // early-game raid cap, covered in Tier 8); retained for a future threat-scaling submod.
    // ============================

    [EmpireDestructiveTest("Destructive.ThreatScaling")]
    public static void HandicapCap_IsAtLeastTwo()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        DestructiveTestUtil.EnsureSettlement();

        double cap = ThreatScalingUtil.ComputeHandicapCap(faction);
        TestAssert.IsTrue(cap >= 2.0,
            $"Handicap cap should be >= 2.0, got {cap}");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "HandicapCap_IsAtLeastTwo");
    }

    [EmpireDestructiveTest("Destructive.ThreatScaling")]
    public static void HandicapCap_IsFiniteNumber()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        DestructiveTestUtil.EnsureSettlement();

        double cap = ThreatScalingUtil.ComputeHandicapCap(faction);
        TestAssert.IsFalse(double.IsNaN(cap), "Handicap cap should not be NaN");
        TestAssert.IsFalse(double.IsInfinity(cap), "Handicap cap should not be infinite");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "HandicapCap_IsFiniteNumber");
    }

    // ============================
    // Tier 5: Frequency Scaling (requires a settlement)
    // ============================

    [EmpireDestructiveTest("Destructive.ThreatScaling")]
    public static void AttackInterval_RespectsMinBound()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        DestructiveTestUtil.EnsureSettlement();

        int interval = ThreatScalingUtil.ComputeScaledAttackInterval(faction);
        TestAssert.IsTrue(interval >= FCSettings.minMaxDaysTillMilitaryAction.min,
            $"Interval ({interval}) should be >= min ({FCSettings.minMaxDaysTillMilitaryAction.min})");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "AttackInterval_RespectsMinBound");
    }

    [EmpireDestructiveTest("Destructive.ThreatScaling")]
    public static void AttackInterval_RespectsMaxBound()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        DestructiveTestUtil.EnsureSettlement();

        int interval = ThreatScalingUtil.ComputeScaledAttackInterval(faction);
        TestAssert.IsTrue(interval <= FCSettings.minMaxDaysTillMilitaryAction.max,
            $"Interval ({interval}) should be <= max ({FCSettings.minMaxDaysTillMilitaryAction.max})");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "AttackInterval_RespectsMaxBound");
    }

    [EmpireDestructiveTest("Destructive.ThreatScaling")]
    public static void AttackInterval_RunMultiple_AllWithinBounds()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        DestructiveTestUtil.EnsureSettlement();

        int min = FCSettings.minMaxDaysTillMilitaryAction.min;
        int max = FCSettings.minMaxDaysTillMilitaryAction.max;

        for (int i = 0; i < 20; i++)
        {
            int interval = ThreatScalingUtil.ComputeScaledAttackInterval(faction);
            TestAssert.IsTrue(interval >= min,
                $"Iteration {i}: interval ({interval}) below min ({min})");
            TestAssert.IsTrue(interval <= max,
                $"Iteration {i}: interval ({interval}) above max ({max})");
        }
        DestructiveTestUtil.AssertEmpireInvariants(faction, "AttackInterval_RunMultiple_AllWithinBounds");
    }
}
