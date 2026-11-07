namespace FactionColonies;

/* DESTRUCTIVE: tithe-income math against a live settlement. Creates a transient settlement when the
   player has none and may assign a worker to force a precondition. Test modifiers and tithe entries
   are removed in finally blocks; created settlements and assigned workers are not. */
public static class TitheIncomeDestructiveTests
{
    // ============================
    // Helpers
    // ============================

    /// <summary>A settlement with a titheable resource (creating one if needed), and that resource.</summary>
    private static (WorldSettlementFC settlement, ResourceFC resource) RequireTitheTarget()
    {
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement(s => s.Resources.Any(r => r.canTithe));
        return (settlement, ForcedStateTestUtilA.RequireTitheableResource(settlement));
    }

    private static void WithSettlementModifier(WorldSettlementFC settlement, FCStatDef stat, double value, Action action)
    {
        var mods = new List<FCStatModifier> { new() { stat = stat, value = value } };
        settlement.AddStatModifiers(mods, "titheTest");
        try
        {
            action();
        }
        finally
        {
            settlement.RemoveStatModifiersBySource("titheTest");
        }
    }

    // ============================
    // Tests
    // ============================

    [EmpireDestructiveTest("Destructive.TitheIncome")]
    public static void TitheModifierPerWorker_MatchesStatPlusSetting()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        (WorldSettlementFC settlement, ResourceFC resource) = RequireTitheTarget();

        double expected = settlement.GetStatValue(FCStatDefOf.taxBaseRandomModifier)
                        + FCSettings.productionTitheMod;
        double actual = resource.GetTitheModifierPerWorker();

        TestAssert.AreEqual(expected, actual,
            message: "GetTitheModifierPerWorker should equal taxBaseRandomModifier stat + productionTitheMod setting");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "TitheModifierPerWorker_MatchesStatPlusSetting");
    }

    [EmpireDestructiveTest("Destructive.TitheIncome")]
    public static void TitheIncome_ZeroWorkers_EqualsBaseTimesMultiplier()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        (WorldSettlementFC settlement, ResourceFC resource) = RequireTitheTarget();

        int savedWorkers = resource.assignedWorkers;
        try
        {
            resource.assignedWorkers = 0;
            resource.SetDirtyCache();

            double multForTotal = faction.GetStatValue(FCStatDefOf.titheValueMultiplier, settlement);
            double expected = resource.taxableProductionMarketValue * multForTotal + resource.DailyExternalTitheBudget;
            double actual = resource.GetTitheIncome();

            TestAssert.AreEqual(expected, actual,
                message: "With 0 workers, tithe income should equal taxableProductionMarketValue * titheValueMultiplier + DailyExternalTitheBudget");
        }
        finally
        {
            resource.assignedWorkers = savedWorkers;
            resource.SetDirtyCache();
        }
        DestructiveTestUtil.AssertEmpireInvariants(faction, "TitheIncome_ZeroWorkers_EqualsBaseTimesMultiplier");
    }

    [EmpireDestructiveTest("Destructive.TitheIncome")]
    public static void TitheIncome_FormulaConsistency()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        (WorldSettlementFC settlement, ResourceFC resource) = RequireTitheTarget();

        // Manually compute using the same formula that GetTitheIncome should use
        double workerMod = resource.GetTitheModifierPerWorker() * resource.assignedWorkers;
        double multForTotal = faction.GetStatValue(FCStatDefOf.titheValueMultiplier, settlement);
        double expected = (resource.taxableProductionMarketValue + workerMod) * multForTotal + resource.DailyExternalTitheBudget;
        double actual = resource.GetTitheIncome();

        TestAssert.AreEqual(expected, actual,
            message: "GetTitheIncome should equal (taxableMarketValue + workerMod) * titheValueMultiplier + DailyExternalTitheBudget");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "TitheIncome_FormulaConsistency");
    }

    [EmpireDestructiveTest("Destructive.TitheIncome")]
    public static void TitheIncome_MultForTotal_ChangesWithStat()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        (WorldSettlementFC settlement, ResourceFC resource) = RequireTitheTarget();
        ForcedStateTestUtilA.EnsureWorkers(settlement, resource);

        double incomeBefore = resource.GetTitheIncome();

        // titheValueMultiplier is multiplicative, so adding 1.5 means multiplying by 1.5
        WithSettlementModifier(settlement, FCStatDefOf.titheValueMultiplier, 1.5, () =>
        {
            resource.SetDirtyCache();
            double incomeAfter = resource.GetTitheIncome();

            // The multiplier scales everything except the external budget, so income can only rise,
            // and must strictly rise when there is a multiplied part to scale.
            TestAssert.IsTrue(incomeAfter >= incomeBefore,
                $"Tithe income should not drop with higher titheValueMultiplier (before={incomeBefore:F2}, after={incomeAfter:F2})");
            if (incomeBefore - resource.DailyExternalTitheBudget > 0)
            {
                TestAssert.GreaterThan(incomeAfter, incomeBefore,
                    $"Tithe income should increase with higher titheValueMultiplier (before={incomeBefore:F2}, after={incomeAfter:F2})");
            }
        });

        resource.SetDirtyCache();
        double incomeRestored = resource.GetTitheIncome();
        TestAssert.AreEqual(incomeBefore, incomeRestored,
            message: "Tithe income should restore after removing modifier");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "TitheIncome_MultForTotal_ChangesWithStat");
    }

    [EmpireDestructiveTest("Destructive.TitheIncome")]
    public static void GetTitheValueMultiplier_MatchesStat()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        (WorldSettlementFC settlement, ResourceFC resource) = RequireTitheTarget();

        double expected = faction.GetStatValue(FCStatDefOf.titheValueMultiplier, settlement);
        double actual = resource.GetTitheValueMultiplier();

        TestAssert.AreEqual(expected, actual,
            message: "GetTitheValueMultiplier should equal the titheValueMultiplier stat for the settlement");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "GetTitheValueMultiplier_MatchesStat");
    }

    [EmpireDestructiveTest("Destructive.TitheIncome")]
    public static void TitheIncome_PostMultiplierComponents_Reconcile()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        (_, ResourceFC resource) = RequireTitheTarget();

        // Mirrors how the tithing screen header now displays its three numbers:
        // each shown post-multiplier, summing (with external budget) to the total tithe budget.
        double mult = resource.GetTitheValueMultiplier();
        double prodComponent = resource.taxableProductionMarketValue * mult;
        double workerComponent = resource.GetTotalTitheModifierForWorkers() * mult;
        double expected = prodComponent + workerComponent + resource.DailyExternalTitheBudget;
        double actual = resource.GetTitheIncome();

        TestAssert.AreEqual(expected, actual,
            message: "Post-multiplier production + worker components + external budget should reconcile to GetTitheIncome");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "TitheIncome_PostMultiplierComponents_Reconcile");
    }

    [EmpireDestructiveTest("Destructive.TitheIncome")]
    public static void TitheIncome_AllResources_NonNegative()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        DestructiveTestUtil.EnsureSettlement();

        foreach (var settlement in faction.settlements)
        {
            foreach (var resource in settlement.Resources)
            {
                double tithe = resource.GetTitheIncome();
                TestAssert.IsTrue(tithe >= 0,
                    $"{settlement.Name}/{resource.def?.defName}: tithe income should be >= 0, got {tithe:F2}");
                TestAssert.IsFalse(double.IsNaN(tithe),
                    $"{settlement.Name}/{resource.def?.defName}: tithe income should not be NaN");
            }
        }
        DestructiveTestUtil.AssertEmpireInvariants(faction, "TitheIncome_AllResources_NonNegative");
    }

    [EmpireDestructiveTest("Destructive.TitheIncome")]
    public static void TitheList_AddAndReorder_PreservesOrder()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        (_, ResourceFC resource) = RequireTitheTarget();

        int before = resource.Tithes.Count;
        var a = new ThingQualityTuple { thingDef = RimWorld.ThingDefOf.MealSimple };
        var b = new ThingQualityTuple { thingDef = RimWorld.ThingDefOf.Silver };
        try
        {
            resource.AddTitheEntry(a, 1);
            resource.AddTitheEntry(b, 1);
            int idxA = System.Array.FindIndex(System.Linq.Enumerable.ToArray(resource.Tithes), e => e.thing == a);
            int idxB = System.Array.FindIndex(System.Linq.Enumerable.ToArray(resource.Tithes), e => e.thing == b);
            TestAssert.IsTrue(idxA < idxB, "first-added entry should be higher priority (lower index)");

            resource.MoveTitheEntry(idxB, idxA); // move b above a
            int newIdxB = System.Array.FindIndex(System.Linq.Enumerable.ToArray(resource.Tithes), e => e.thing == b);
            TestAssert.AreEqual(idxA, newIdxB, message: "reorder should move b to a's index");
        }
        finally
        {
            resource.RemoveTitheAt(before); // remove the two appended entries (regardless of order)
            resource.RemoveTitheAt(before);
        }
        // Verify cleanup outside finally so an in-body failure isn't masked by this assertion.
        TestAssert.AreEqual(before, resource.Tithes.Count, message: "cleanup should restore the list");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "TitheList_AddAndReorder_PreservesOrder");
    }

    [EmpireDestructiveTest("Destructive.TitheIncome")]
    public static void TitheList_AllowsDuplicateEntries()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        (_, ResourceFC resource) = RequireTitheTarget();

        int before = resource.Tithes.Count;
        var a = new ThingQualityTuple { thingDef = RimWorld.ThingDefOf.MealSimple };
        try
        {
            // The same thing may appear multiple times; each AddTitheEntry appends an independent entry.
            resource.AddTitheEntry(a, 2);
            resource.AddTitheEntry(a, 100);
            TestAssert.AreEqual(before + 2, resource.Tithes.Count, message: "both duplicate entries should persist");
            TestAssert.AreEqual(2, resource.Tithes[before].quantity, message: "first duplicate keeps its quantity");
            TestAssert.AreEqual(100, resource.Tithes[before + 1].quantity, message: "second duplicate keeps its quantity");

            // Editing one duplicate by index must not touch the other.
            resource.SetTitheQuantityAt(before, 5);
            TestAssert.AreEqual(5, resource.Tithes[before].quantity, message: "first duplicate updated");
            TestAssert.AreEqual(100, resource.Tithes[before + 1].quantity, message: "second duplicate unchanged");
        }
        finally
        {
            resource.RemoveTitheAt(before);
            resource.RemoveTitheAt(before);
        }
        // Verify cleanup outside finally so an in-body failure isn't masked by this assertion.
        TestAssert.AreEqual(before, resource.Tithes.Count, message: "cleanup should restore the list");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "TitheList_AllowsDuplicateEntries");
    }
}
