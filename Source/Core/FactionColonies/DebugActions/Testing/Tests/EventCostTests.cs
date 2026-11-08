namespace FactionColonies;

/* Coverage for the event dynamic-cost subsystem: FCEventScalingUtil.CountAffectedSettlements
 * and FCOptionCostUtil.ComputeScaledCost / BuildCostBreakdown. EventSystemTests already covers
 * event-def integrity and FCEventMaker, but not the cost-scaling math, which is pure. */
public static class EventCostTests
{
    /* Snapshot/restore the global event-cost multiplier so a test never leaks state into the
     * next one (mirrors StatTests.WithTestModifier's try/finally discipline). */
    private static void WithEventCostMultiplier(float value, Action action)
        => TestUtil.WithOverride(() => FCSettings.eventSilverCostMultiplier, v => FCSettings.eventSilverCostMultiplier = v, value, action);

    // ============================
    // FCEventScalingUtil.CountAffectedSettlements
    // ============================

    [EmpireTest("EventCost")]
    public static void CountAffected_NullEvent_ReturnsOne()
    {
        TestAssert.AreEqual(1, FCEventScalingUtil.CountAffectedSettlements(null));
    }

    [EmpireTest("EventCost")]
    public static void CountAffected_NullList_ReturnsOne()
    {
        var evt = new FCEvent { settlementTraitLocations = null };
        TestAssert.AreEqual(1, FCEventScalingUtil.CountAffectedSettlements(evt));
    }

    [EmpireTest("EventCost")]
    public static void CountAffected_EmptyList_ReturnsOne()
    {
        var evt = new FCEvent { settlementTraitLocations = [] };
        TestAssert.AreEqual(1, FCEventScalingUtil.CountAffectedSettlements(evt));
    }

    [EmpireTest("EventCost")]
    public static void CountAffected_AllNullEntries_FlooredAtOne()
    {
        var evt = new FCEvent { settlementTraitLocations = [null, null] };
        TestAssert.AreEqual(1, FCEventScalingUtil.CountAffectedSettlements(evt));
    }

    // ============================
    // FCOptionCostUtil.ComputeScaledCost
    // ============================

    [EmpireTest("EventCost")]
    public static void ComputeScaledCost_NullOption_ReturnsZero()
    {
        var evt = new FCEvent();
        TestAssert.AreEqual(0, FCOptionCostUtil.ComputeScaledCost(null, evt));
    }

    [EmpireTest("EventCost")]
    public static void ComputeScaledCost_NullEvent_UsesEffectiveSilverCost()
    {
        WithEventCostMultiplier(1f, () =>
        {
            var opt = new FCOptionDef { silverCost = 100 };
            TestAssert.AreEqual(opt.EffectiveSilverCost,
                FCOptionCostUtil.ComputeScaledCost(opt, null));
        });
    }

    [EmpireTest("EventCost")]
    public static void ComputeScaledCost_BaseTimesMultiplier()
    {
        var opt = new FCOptionDef { silverCost = 100 };
        var evt = new FCEvent(); // empty trait list -> affected = 1
        WithEventCostMultiplier(1f, () =>
            TestAssert.AreEqual(100, FCOptionCostUtil.ComputeScaledCost(opt, evt)));
        WithEventCostMultiplier(2f, () =>
            TestAssert.AreEqual(200, FCOptionCostUtil.ComputeScaledCost(opt, evt)));
    }

    [EmpireTest("EventCost")]
    public static void ComputeScaledCost_NegativeSilver_ClampsBaseToZero()
    {
        var opt = new FCOptionDef { silverCost = -50 };
        var evt = new FCEvent();
        WithEventCostMultiplier(1f, () =>
            TestAssert.AreEqual(0, FCOptionCostUtil.ComputeScaledCost(opt, evt)));
    }

    [EmpireTest("EventCost")]
    public static void ComputeScaledCost_RoundsAwayFromZero()
    {
        // base 1 * 2.5 = 2.5 -> AwayFromZero rounds to 3 (banker's rounding would give 2).
        // 2.5f is exactly representable, so this is not float-fragile.
        var opt = new FCOptionDef { silverCost = 1 };
        var evt = new FCEvent();
        WithEventCostMultiplier(2.5f, () =>
            TestAssert.AreEqual(3, FCOptionCostUtil.ComputeScaledCost(opt, evt)));
    }

    // The production-delta cost tests need live settlements and live in EventCostDestructiveTests.

    // ============================
    // FCOptionCostUtil.BuildCostBreakdown
    // ============================

    [EmpireTest("EventCost")]
    public static void BuildCostBreakdown_NullEvent_ReturnsNull()
    {
        var opt = new FCOptionDef { silverCost = 100 };
        TestAssert.IsNull(FCOptionCostUtil.BuildCostBreakdown(opt, null));
    }

    [EmpireTest("EventCost")]
    public static void BuildCostBreakdown_UnscaledSingleLine_ReturnsNull()
    {
        // affected = 1 and multiplier = 1 -> only the base line, so the tooltip is suppressed.
        var opt = new FCOptionDef { silverCost = 100 };
        var evt = new FCEvent();
        WithEventCostMultiplier(1f, () =>
            TestAssert.IsNull(FCOptionCostUtil.BuildCostBreakdown(opt, evt)));
    }
}
