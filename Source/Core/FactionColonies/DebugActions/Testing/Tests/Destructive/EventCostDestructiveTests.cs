namespace FactionColonies;

/* DESTRUCTIVE: event-cost math that reads live settlements (affected-settlement counts and the
 * per-resource production delta). Creates transient settlements when too few exist and may assign a
 * food worker. Settings changed here are restored; created settlements and workers are not. */
public static class EventCostDestructiveTests
{
    /* Snapshot/restore the global event-cost multiplier so a test never leaks it into the next one. */
    private static void WithEventCostMultiplier(float value, Action action)
        => TestUtil.WithOverride(() => FCSettings.eventSilverCostMultiplier, v => FCSettings.eventSilverCostMultiplier = v, value, action);

    // ============================
    // FCEventScalingUtil.CountAffectedSettlements
    // ============================

    [EmpireDestructiveTest("Destructive.EventCost")]
    public static void CountAffected_CountsNonNullEntries()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        List<WorldSettlementFC> settlements = DestructiveTestUtil.EnsureSettlements(2);

        var list = new List<WorldSettlementFC> { settlements[0], null, settlements[1] };
        var evt = new FCEvent { settlementTraitLocations = list };
        TestAssert.AreEqual(2, FCEventScalingUtil.CountAffectedSettlements(evt));
        DestructiveTestUtil.AssertEmpireInvariants(faction, "CountAffected_CountsNonNullEntries");
    }

    // ============================
    // FCOptionCostUtil.ComputeScaledCost (production delta)
    // ============================

    /* Shared setup for the production-delta tests: the food resource def, the non-null current
     * settlements, and helpers that build the option and compute the expected pre-coefficient delta
     * from the same public ResourceFC accessors the util uses (so the assertions check wiring/scope,
     * not a hardcoded constant). */
    private static ResourceTypeDef RequireFood() =>
        TestAssert.NotNull(ResourceTypeDefOf.RTD_Food, "RTD_Food is not loaded");

    private static List<WorldSettlementFC> DeltaTestSettlements()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        DestructiveTestUtil.EnsureSettlement();
        return faction.settlements.Where(s => s is not null).ToList();
    }

    private static double ExpectedDelta(List<WorldSettlementFC> list, ResourceTypeDef res, float additiveDelta)
    {
        double sum = 0;
        foreach (WorldSettlementFC s in list)
        {
            ResourceFC r = s.GetResource(res);
            if (r is null) continue;
            double d = additiveDelta * r.productionMult * r.assignedWorkers;
            if (d > 0) sum += d;
        }
        return sum;
    }

    private static FCOptionDef DeltaOption(ResourceTypeDef res, float additiveDelta, float coeff, FCResourceCostFraming framing)
    {
        var ext = new FCDynamicCostExtension
        {
            costPerResourceDelta =
            [
                new() { resource = res, additiveDelta = additiveDelta, coefficient = coeff, framing = framing }
            ]
        };
        return new FCOptionDef { silverCost = 100, modExtensions = [ext] };
    }

    [EmpireDestructiveTest("Destructive.EventCost")]
    public static void ComputeScaledCost_ResourceDelta_ChargesMarginalProduction()
    {
        // The resource term charges for the production the option changes, not total output:
        // per settlement, additiveDelta * productionMult * assignedWorkers, summed over the
        // affected settlements, times silverPerResource times the coefficient (plus base * affected).
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        ResourceTypeDef food = RequireFood();
        var list = DeltaTestSettlements();

        const float additive = 0.5f;
        const float coeff = 0.5f; // fraction of silver-per-unit value
        double delta = ExpectedDelta(list, food, additive);
        var evt = new FCEvent { settlementTraitLocations = list };

        WithEventCostMultiplier(1f, () =>
        {
            double raw = 100.0 * list.Count + delta * FCSettings.silverPerResource * coeff;
            int expected = Math.Max(0, (int)Math.Round(raw, MidpointRounding.AwayFromZero));
            TestAssert.AreEqual(expected, FCOptionCostUtil.ComputeScaledCost(
                DeltaOption(food, additive, coeff, FCResourceCostFraming.Mitigated), evt));
        });
        DestructiveTestUtil.AssertEmpireInvariants(faction, "ComputeScaledCost_ResourceDelta_ChargesMarginalProduction");
    }

    [EmpireDestructiveTest("Destructive.EventCost")]
    public static void ComputeScaledCost_Framing_DoesNotAffectCost()
    {
        // Framing is presentational only — Added vs Mitigated must yield identical silver.
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        ResourceTypeDef food = RequireFood();
        var list = DeltaTestSettlements();
        var evt = new FCEvent { settlementTraitLocations = list };

        WithEventCostMultiplier(1f, () =>
        {
            int mit = FCOptionCostUtil.ComputeScaledCost(DeltaOption(food, 0.5f, 0.5f, FCResourceCostFraming.Mitigated), evt);
            int add = FCOptionCostUtil.ComputeScaledCost(DeltaOption(food, 0.5f, 0.5f, FCResourceCostFraming.Added), evt);
            TestAssert.AreEqual(mit, add);
        });
        DestructiveTestUtil.AssertEmpireInvariants(faction, "ComputeScaledCost_Framing_DoesNotAffectCost");
    }

    // ============================
    // FCOptionCostUtil.BuildCostBreakdown
    // ============================

    [EmpireDestructiveTest("Destructive.EventCost")]
    public static void BuildCostBreakdown_Framing_PicksWording()
    {
        // The breakdown line wording follows the framing flag. Case-insensitive so it passes
        // whether the keyed string is translated ("added") or falls back to the key ("...Added").
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        ResourceTypeDef food = RequireFood();

        // The resource line only appears for a non-zero contribution (delta * silverPerResource * coeff),
        // so force a settlement that produces food and has a worker on it.
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement(s => s.GetResource(food)?.productionMult > 0);
        ForcedStateTestUtilA.EnsureWorkers(settlement, settlement.GetResource(food));
        List<WorldSettlementFC> list = [settlement];
        TestAssert.GreaterThan(ExpectedDelta(list, food, 0.5f), 0, $"'{settlement.Name}' should have a positive food delta");
        var evt = new FCEvent { settlementTraitLocations = list };

        int originalSilverPerResource = FCSettings.silverPerResource;
        try
        {
            FCSettings.silverPerResource = Math.Max(1, originalSilverPerResource);
            WithEventCostMultiplier(1f, () =>
            {
                string added = FCOptionCostUtil.BuildCostBreakdown(DeltaOption(food, 0.5f, 0.5f, FCResourceCostFraming.Added), evt);
                string mit = FCOptionCostUtil.BuildCostBreakdown(DeltaOption(food, 0.5f, 0.5f, FCResourceCostFraming.Mitigated), evt);
                TestAssert.IsTrue(added is not null && added.ToLowerInvariant().Contains("added"), "Added framing should read 'added'");
                TestAssert.IsTrue(mit is not null && mit.ToLowerInvariant().Contains("mitigated"), "Mitigated framing should read 'mitigated'");
            });
        }
        finally
        {
            FCSettings.silverPerResource = originalSilverPerResource;
        }
        DestructiveTestUtil.AssertEmpireInvariants(faction, "BuildCostBreakdown_Framing_PicksWording");
    }
}
