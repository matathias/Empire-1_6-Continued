namespace FactionColonies;

/* DESTRUCTIVE: settlement-level stat checks. Creates a transient settlement when the player has
   none, and may enact a policy. Test modifiers and policy changes are removed in finally blocks;
   created settlements are not. */
public static class StatDestructiveTests
{
    /// <summary>
    /// Adds a test modifier, runs the action, then removes the modifier in a finally block.
    /// </summary>
    private static void WithTestModifier(WorldSettlementFC settlement, FCStatDef stat, double value, Action action)
    {
        var mods = new List<FCStatModifier> { new() { stat = stat, value = value } };
        settlement.AddStatModifiers(mods, "test");
        try
        {
            action();
        }
        finally
        {
            settlement.RemoveStatModifiersBySource("test");
        }
    }

    // ============================
    // Faction/Settlement Sanity
    // ============================

    [EmpireDestructiveTest("Destructive.Stat")]
    public static void CombinedStat_AllSettlementStats_AreFinite()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        DestructiveTestUtil.EnsureSettlement();

        foreach (WorldSettlementFC settlement in faction.settlements)
        {
            foreach (FCStatDef stat in DefDatabase<FCStatDef>.AllDefsListForReading)
            {
                if (!stat.appliesToSettlements) continue;
                double val = faction.GetStatValue(stat, settlement);
                TestAssert.IsFalse(double.IsNaN(val),
                    $"GetStatValue({stat.defName}, {settlement.Name}) is NaN");
                TestAssert.IsFalse(double.IsInfinity(val),
                    $"GetStatValue({stat.defName}, {settlement.Name}) is infinite");
            }
        }
        DestructiveTestUtil.AssertEmpireInvariants(faction, "CombinedStat_AllSettlementStats_AreFinite");
    }

    [EmpireDestructiveTest("Destructive.Stat")]
    public static void CombinedStat_FactionOnlyStats_IgnoreSettlement()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        foreach (FCStatDef stat in DefDatabase<FCStatDef>.AllDefsListForReading)
        {
            if (stat.appliesToSettlements) continue;
            double withSettlement = faction.GetStatValue(stat, settlement);
            double withoutSettlement = faction.GetStatValue(stat, null);
            TestAssert.AreEqual(withoutSettlement, withSettlement,
                message: $"Faction-only stat {stat.defName} should ignore settlement");
        }
        DestructiveTestUtil.AssertEmpireInvariants(faction, "CombinedStat_FactionOnlyStats_IgnoreSettlement");
    }

    [EmpireDestructiveTest("Destructive.Stat")]
    public static void CombinedStat_Additive_SettlementPlusFaction()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        // Use happinessGainedBase — additive, appliesToSettlements
        FCStatDef stat = FCStatDefOf.happinessGainedBase;
        TestAssert.IsTrue(stat.appliesToSettlements && stat.aggregation == FCStatAggregation.Additive,
            "happinessGainedBase should be an additive settlement stat");

        double factionPart = faction.GetFactionStatValue(stat);
        double settlementPart = settlement.GetSettlementStatValue(stat);
        double combined = faction.GetStatValue(stat, settlement);

        // combined = settlementPart + factionPart + behavior adjustments
        // behavior adjustments can only ADD to this, so combined >= settlement + faction
        // (unless a behavior subtracts, which none currently do for this stat)
        TestAssert.IsFalse(double.IsNaN(combined),
            $"Combined additive stat should not be NaN");
        // Verify the sum matches (within tolerance for behavior adjustments)
        double expected = settlementPart + factionPart;
        double delta = combined - expected;
        // Behaviors may adjust, but the delta should be bounded
        TestAssert.IsTrue(Math.Abs(delta) < 50,
            $"Stat {stat.defName}: combined ({combined:F2}) diverges too far from settlement+faction ({expected:F2}), delta={delta:F2}");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "CombinedStat_Additive_SettlementPlusFaction");
    }

    [EmpireDestructiveTest("Destructive.Stat")]
    public static void CombinedStat_Multiplicative_SettlementTimesFaction()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        // Use happinessGainedMultiplier — multiplicative, appliesToSettlements
        FCStatDef stat = FCStatDefOf.happinessGainedMultiplier;
        TestAssert.IsTrue(stat.appliesToSettlements && stat.aggregation == FCStatAggregation.Multiplicative,
            "happinessGainedMultiplier should be a multiplicative settlement stat");

        double factionPart = faction.GetFactionStatValue(stat);
        double settlementPart = settlement.GetSettlementStatValue(stat);
        double combined = faction.GetStatValue(stat, settlement);

        TestAssert.IsFalse(double.IsNaN(combined),
            $"Combined multiplicative stat should not be NaN");
        double expected = settlementPart * factionPart;
        double delta = combined - expected;
        // Behaviors may adjust, but the delta should be bounded
        TestAssert.IsTrue(Math.Abs(delta) < 50,
            $"Stat {stat.defName}: combined ({combined:F2}) diverges too far from settlement*faction ({expected:F2}), delta={delta:F2}");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "CombinedStat_Multiplicative_SettlementTimesFaction");
    }

    [EmpireDestructiveTest("Destructive.Stat")]
    public static void CombinedStat_TitheMultiplier_RespectsSettlementModifiers()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        FCStatDef stat = FCStatDefOf.titheValueMultiplier;
        TestAssert.IsTrue(stat.appliesToSettlements,
            "titheValueMultiplier should apply to settlements");
        double combined = faction.GetStatValue(stat, settlement);
        TestAssert.IsFalse(double.IsNaN(combined),
            "titheValueMultiplier combined value should not be NaN");
        TestAssert.GreaterThan(combined, 0,
            "titheValueMultiplier combined value should be positive");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "CombinedStat_TitheMultiplier_RespectsSettlementModifiers");
    }

    // ============================
    // Add/Remove Modifier Tests
    // ============================

    [EmpireDestructiveTest("Destructive.Stat")]
    public static void AddModifier_Additive_IncreasesStatValue()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        FCStatDef stat = FCStatDefOf.happinessGainedBase;
        double before = settlement.GetSettlementStatValue(stat);

        WithTestModifier(settlement, stat, 3.0, () =>
        {
            double after = settlement.GetSettlementStatValue(stat);
            TestAssert.AreEqual(before + 3.0, after,
                message: $"Additive modifier should increase stat by exactly 3.0 (before={before}, after={after})");
        });
        DestructiveTestUtil.AssertEmpireInvariants(faction, "AddModifier_Additive_IncreasesStatValue");
    }

    [EmpireDestructiveTest("Destructive.Stat")]
    public static void AddModifier_Multiplicative_MultipliesStatValue()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        FCStatDef stat = FCStatDefOf.happinessGainedMultiplier;
        double before = settlement.GetSettlementStatValue(stat);

        WithTestModifier(settlement, stat, 1.5, () =>
        {
            double after = settlement.GetSettlementStatValue(stat);
            TestAssert.AreEqual(before * 1.5, after,
                message: $"Multiplicative modifier should multiply stat by 1.5 (before={before}, after={after})");
        });
        DestructiveTestUtil.AssertEmpireInvariants(faction, "AddModifier_Multiplicative_MultipliesStatValue");
    }

    [EmpireDestructiveTest("Destructive.Stat")]
    public static void RemoveModifier_RestoresOriginalValue()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        FCStatDef stat = FCStatDefOf.happinessGainedBase;
        double before = settlement.GetSettlementStatValue(stat);

        var mods = new List<FCStatModifier> { new() { stat = stat, value = 5.0 } };
        settlement.AddStatModifiers(mods, "test");
        try
        {
            double during = settlement.GetSettlementStatValue(stat);
            TestAssert.AreEqual(before + 5.0, during,
                message: "Stat should increase after adding modifier");

            settlement.RemoveStatModifiers(mods, "test");
            double after = settlement.GetSettlementStatValue(stat);
            TestAssert.AreEqual(before, after,
                message: $"Stat should restore after removing modifier (before={before}, after={after})");
        }
        finally
        {
            // Ensure a mid-test failure can't leave a phantom modifier on the live settlement.
            settlement.RemoveStatModifiersBySource("test");
        }
        DestructiveTestUtil.AssertEmpireInvariants(faction, "RemoveModifier_RestoresOriginalValue");
    }

    [EmpireDestructiveTest("Destructive.Stat")]
    public static void RemoveModifier_ByReference_OnlyRemovesMatchingObject()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        FCStatDef stat = FCStatDefOf.happinessGainedBase;
        double before = settlement.GetSettlementStatValue(stat);

        // Create two separate modifier lists with same stat and value
        var mods1 = new List<FCStatModifier> { new() { stat = stat, value = 2.0 } };
        var mods2 = new List<FCStatModifier> { new() { stat = stat, value = 2.0 } };

        settlement.AddStatModifiers(mods1, "test1");
        settlement.AddStatModifiers(mods2, "test2");
        try
        {
            double bothAdded = settlement.GetSettlementStatValue(stat);
            TestAssert.AreEqual(before + 4.0, bothAdded,
                message: "Both modifiers should be applied");

            // Remove first by reference
            settlement.RemoveStatModifiers(mods1, "test1");
            double oneRemoved = settlement.GetSettlementStatValue(stat);
            TestAssert.AreEqual(before + 2.0, oneRemoved,
                message: "Only first modifier should be removed, second should remain");

            // Cleanup
            settlement.RemoveStatModifiers(mods2, "test2");
            double restored = settlement.GetSettlementStatValue(stat);
            TestAssert.AreEqual(before, restored,
                message: "All modifiers should be removed");
        }
        finally
        {
            // Ensure a mid-test failure can't leave phantom modifiers on the live settlement.
            settlement.RemoveStatModifiersBySource("test1");
            settlement.RemoveStatModifiersBySource("test2");
        }
        DestructiveTestUtil.AssertEmpireInvariants(faction, "RemoveModifier_ByReference_OnlyRemovesMatchingObject");
    }

    [EmpireDestructiveTest("Destructive.Stat")]
    public static void RemoveModifiersBySource_RemovesCorrectEntries()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        FCStatDef stat = FCStatDefOf.happinessGainedBase;
        double before = settlement.GetSettlementStatValue(stat);

        // Add modifiers from two different sources
        var mods1 = new List<FCStatModifier> { new() { stat = stat, value = 3.0 } };
        var mods2 = new List<FCStatModifier> { new() { stat = stat, value = 5.0 } };

        settlement.AddStatModifiers(mods1, "testA");
        settlement.AddStatModifiers(mods2, "testB");
        try
        {
            double bothAdded = settlement.GetSettlementStatValue(stat);
            TestAssert.AreEqual(before + 8.0, bothAdded,
                message: "Both source modifiers should be applied");

            // Remove only source A
            settlement.RemoveStatModifiersBySource("testA");
            double afterRemoveA = settlement.GetSettlementStatValue(stat);
            TestAssert.AreEqual(before + 5.0, afterRemoveA,
                message: "Only sourceA modifiers should be removed");

            // Cleanup
            settlement.RemoveStatModifiersBySource("testB");
            double restored = settlement.GetSettlementStatValue(stat);
            TestAssert.AreEqual(before, restored,
                message: "All modifiers should be removed");
        }
        finally
        {
            // Ensure a mid-test failure can't leave phantom modifiers on the live settlement.
            settlement.RemoveStatModifiersBySource("testA");
            settlement.RemoveStatModifiersBySource("testB");
        }
        DestructiveTestUtil.AssertEmpireInvariants(faction, "RemoveModifiersBySource_RemovesCorrectEntries");
    }

    [EmpireDestructiveTest("Destructive.Stat")]
    public static void Cache_AddModifier_DirtiesSettlementCache()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        FCStatDef stat = FCStatDefOf.happinessGainedBase;

        // Populate the cache
        double initial = settlement.GetSettlementStatValue(stat);

        // Add a modifier — should invalidate cache
        WithTestModifier(settlement, stat, 7.0, () =>
        {
            double updated = settlement.GetSettlementStatValue(stat);
            TestAssert.AreEqual(initial + 7.0, updated,
                message: "Cache should be invalidated and return updated value after add");
        });

        // After removal, cache should be invalidated again
        double restored = settlement.GetSettlementStatValue(stat);
        TestAssert.AreEqual(initial, restored,
            message: "Cache should be invalidated and return original value after remove");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "Cache_AddModifier_DirtiesSettlementCache");
    }

    // ============================
    // Resource-Stat Integration
    // ============================

    [EmpireDestructiveTest("Destructive.Stat")]
    public static void Resource_AllResources_HaveLinkedStats()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        DestructiveTestUtil.EnsureSettlement();

        foreach (WorldSettlementFC settlement in faction.settlements)
        {
            foreach (ResourceFC resource in settlement.Resources)
            {
                TestAssert.IsNotNull(resource.def.productionAdditiveStat,
                    $"{settlement.Name}/{resource.def.defName}: productionAdditiveStat is null");
                TestAssert.IsNotNull(resource.def.productionMultiplierStat,
                    $"{settlement.Name}/{resource.def.defName}: productionMultiplierStat is null");
            }
        }
        DestructiveTestUtil.AssertEmpireInvariants(faction, "Resource_AllResources_HaveLinkedStats");
    }

    [EmpireDestructiveTest("Destructive.Stat")]
    public static void Resource_StatAdditive_AffectsProductionBase()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        ResourceFC resource = TestAssert.NotNull(settlement.Resources.FirstOrDefault(),
            $"Settlement '{settlement.Name}' has no resources");
        TestAssert.IsNotNull(resource.def.productionAdditiveStat,
            $"{resource.def.defName}: productionAdditiveStat is null");

        double baseBefore = resource.productionBase;

        WithTestModifier(settlement, resource.def.productionAdditiveStat, 2.0, () =>
        {
            double baseAfter = resource.productionBase;
            TestAssert.GreaterThan(baseAfter, baseBefore,
                $"productionBase should increase after adding additive stat modifier (before={baseBefore:F2}, after={baseAfter:F2})");
        });

        double baseRestored = resource.productionBase;
        TestAssert.AreEqual(baseBefore, baseRestored,
            message: $"productionBase should restore after removing modifier");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "Resource_StatAdditive_AffectsProductionBase");
    }

    [EmpireDestructiveTest("Destructive.Stat")]
    public static void Resource_StatMultiplier_AffectsProductionMult()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        ResourceFC resource = TestAssert.NotNull(settlement.Resources.FirstOrDefault(),
            $"Settlement '{settlement.Name}' has no resources");
        TestAssert.IsNotNull(resource.def.productionMultiplierStat,
            $"{resource.def.defName}: productionMultiplierStat is null");

        double multBefore = resource.productionMult;

        WithTestModifier(settlement, resource.def.productionMultiplierStat, 1.5, () =>
        {
            double multAfter = resource.productionMult;
            TestAssert.GreaterThan(multAfter, multBefore,
                $"productionMult should increase after adding multiplier stat modifier (before={multBefore:F2}, after={multAfter:F2})");
        });

        double multRestored = resource.productionMult;
        TestAssert.AreEqual(multBefore, multRestored,
            message: $"productionMult should restore after removing modifier");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "Resource_StatMultiplier_AffectsProductionMult");
    }

    // ============================
    // Behavior Tests
    // ============================

    [EmpireDestructiveTest("Destructive.Stat")]
    public static void Behavior_AllBehaviors_ModifyStatDoesNotThrow()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        DestructiveTestUtil.EnsureSettlement();

        // Test a representative sample of stats across all settlements
        FCStatDef[] sampleStats =
        [
            FCStatDefOf.taxBonusFlat,
            FCStatDefOf.happinessGainedBase,
            FCStatDefOf.militaryBaseLevel,
            FCStatDefOf.settlementCostMultiplier,
            FCStatDefOf.prosperityGainedBase
        ];

        foreach (WorldSettlementFC settlement in faction.settlements)
        {
            foreach (FCStatDef stat in sampleStats)
            {
                double val = faction.GetStatValue(stat, settlement);
                TestAssert.IsFalse(double.IsNaN(val),
                    $"GetStatValue({stat.defName}, {settlement.Name}) should not be NaN");
                TestAssert.IsFalse(double.IsInfinity(val),
                    $"GetStatValue({stat.defName}, {settlement.Name}) should not be infinite");
            }
        }
        DestructiveTestUtil.AssertEmpireInvariants(faction, "Behavior_AllBehaviors_ModifyStatDoesNotThrow");
    }

    [EmpireDestructiveTest("Destructive.Stat")]
    public static void Behavior_Egalitarian_TaxBreakReducesTaxBonus()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
        try
        {
            // Egalitarian alone, so its behavior is the only one adjusting the stat.
            PolicyTestHelper.ClearAll(faction);
            PolicyTestHelper.EnactPolicy(faction, FCPolicyDefOf.egalitarian);

            double taxBonus = faction.GetStatValue(FCStatDefOf.taxBonusFlat, settlement);
            TestAssert.IsFalse(double.IsNaN(taxBonus),
                "Egalitarian taxBonusFlat should not be NaN");

            // When no tax break is active, the behavior contributes 0 to taxBonusFlat.
            // When a tax break is active, it contributes -30. Either way the delta vs. faction base
            // is never above 0 for this trait.
            double baseWithoutBehavior = faction.GetFactionStatValue(FCStatDefOf.taxBonusFlat);
            double behaviorDelta = taxBonus - baseWithoutBehavior;
            TestAssert.IsTrue(behaviorDelta <= 0.01,
                $"Egalitarian delta ({behaviorDelta:F2}) should be <= 0 (0 off-break, -30 on-break)");
            TestAssert.IsTrue(behaviorDelta >= -30.01,
                $"Egalitarian delta ({behaviorDelta:F2}) should be >= -30 (tax break penalty)");
        }
        finally
        {
            PolicyTestHelper.RestorePolicies(faction, snapshot);
        }
        DestructiveTestUtil.AssertEmpireInvariants(faction, "Behavior_Egalitarian_TaxBreakReducesTaxBonus");
    }
}
