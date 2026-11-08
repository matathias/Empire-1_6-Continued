namespace FactionColonies;

/* DESTRUCTIVE: settlement economy, event targeting, edict activation, squad bills and the exclusive
   spot buildings against live state. Creates transient settlements (removed in finally blocks,
   except those made by EnsureSettlement(s) when the player has too few), runs real tax cycles on them, enacts and revokes edicts, queues and removes test events, and
   spawns spot buildings on a player home map (destroyed afterwards, with the previous capital and
   active spots put back). Assigned workers, pause flags and tithe settings on the transient
   settlements are not reverted. */
public static class SettlementsBuildingsDestructiveTests
{
    /*-*-*-*-*/
    /* Helpers */
    /*-*-*-*-*/

    /// <summary>A comp that injects a fixed per-day external tithe budget into one resource.</summary>
    private sealed class FixedTitheBudgetComp : WorldObjectComp, ITitheBudgetModifier
    {
        public ResourceFC target;
        public double perDay;

        double ITitheBudgetModifier.GetDailyExternalTitheBudget(ResourceFC resource) => resource == target ? perDay : 0;
    }

    private static FixedTitheBudgetComp AttachExternalBudget(WorldSettlementFC s, ResourceFC target, double perDay)
    {
        FixedTitheBudgetComp comp = new() { parent = s, target = target, perDay = perDay };
        s.AllComps.Add(comp);
        target.SetDirtyCache();
        s.DirtyProfitCache();
        return comp;
    }

    private static void Detach(WorldSettlementFC s, WorldObjectComp comp)
    {
        if (comp is null) return;
        s.AllComps.Remove(comp);
        s.DirtyProfitCache();
    }

    /// <summary>
    /// Makes <paramref name="target"/> the only tithing resource of <paramref name="s"/>: every other
    /// resource is paused (its production is paid as plain silver), and the target has no specified
    /// tithes, no random tithe, no escrow and the given worker count.
    /// </summary>
    private static void Isolate(WorldSettlementFC s, ResourceFC target, int workers)
    {
        foreach (ResourceFC r in s.Resources)
            r.tithesPaused = !ReferenceEquals(r, target);

        for (int i = target.Tithes.Count - 1; i >= 0; i--) target.RemoveTitheAt(i);
        target.hasRandomTithe = false;
        target.autoMaxRandomTithe = false;
        target.randomTitheStock = 0;
        target.randomTitheStockSilver = 0;
        target.disburseTitheStock = false;

        if (target.assignedWorkers > workers)
            target.assignedWorkers = workers;
        else if (target.assignedWorkers < workers)
            ForcedStateTestUtilA.EnsureWorkers(s, target, workers);
        target.SetDirtyCache();
        s.DirtyProfitCache();
    }

    /// <summary>The titheable resource of <paramref name="s"/> that uses the default ThingSetMaker
    /// and can generate a thing matching <paramref name="thingMatch"/>, with that thing.</summary>
    private static (ResourceFC resource, ThingDef thing) RequireTitheResourceWith(WorldSettlementFC s, Func<ThingDef, bool> thingMatch, string what)
    {
        foreach (ResourceFC r in s.Resources)
        {
            if (!r.canTithe) continue;
            if (r.def.GetModExtension<ResourceFilterExtension>()?.GetThingSetMaker(out _, r) is not null) continue;
            if (r.def.titheMaxCountBase <= 0) continue;
            ThingDef thing = r.GenerateThingDefList()?.FirstOrDefault(thingMatch);
            if (thing is not null) return (r, thing);
        }
        TestAssert.Fail($"Settlement '{s.Name}' has no titheable resource that can generate {what}");
        return (null, null);
    }

    private static bool IsPlainThing(ThingDef d) =>
        d is not null && !CraftUtil.ThingHasQuality(d) && !d.MadeFromStuff && d.BaseMarketValue > 0;

    /// <summary>Zeroes one-time silver, accrues <paramref name="days"/> daily cycles, then taxes.</summary>
    private static int AccrueAndTax(WorldSettlementFC s, int days, out List<Thing> things)
    {
        s.oneTimeSilverIncome = 0;
        for (int d = 0; d < days; d++) s.AccumulateDailyProduction();
        things = s.CreateTax(out int silver);
        return silver;
    }

    private static int AccrueAndTax(WorldSettlementFC s, int days) => AccrueAndTax(s, days, out _);

    private static double TotalValue(IEnumerable<Thing> things) => things?.Sum(t => t.MarketValue * t.stackCount) ?? 0;

    /*-*-*-*-*/
    /* Tithe billing with an external budget */
    /*-*-*-*-*/

    [EmpireDestructiveTest("Destructive.Tax")]
    public static void ExternalBudget_RandomTitheEscrow_ChargesNoSilver()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = DestructiveTestUtil.RequireTransientSettlement();
        FixedTitheBudgetComp comp = null;
        try
        {
            ResourceFC target = ForcedStateTestUtilA.RequireTitheableResource(s);
            Isolate(s, target, workers: 0); // no own production: the whole budget is external
            const int days = 2;
            const double perDay = 200;

            int baseline = AccrueAndTax(s, days);

            comp = AttachExternalBudget(s, target, perDay);
            target.hasRandomTithe = true;
            target.autoMaxRandomTithe = true;
            target.randomTitheFilter.SetDisallowAll(); // budget rolls into escrow, no goods
            target.SetDirtyCache();
            int withEscrow = AccrueAndTax(s, days);

            TestAssert.AreEqual(perDay * days, target.randomTitheStock, tolerance: 1,
                message: "The external budget should be escrowed in full");
            TestAssert.AreEqual(0.0, target.randomTitheStockSilver, tolerance: 1,
                message: "No silver should be withheld for an escrow the settlement did not fund");
            TestAssert.AreEqual(baseline, withEscrow, tolerance: 2,
                message: $"An externally funded escrow must not cost silver (baseline {baseline}, with escrow {withEscrow})");

            // Disbursing that escrow pays back only what was withheld: nothing.
            target.hasRandomTithe = false;
            target.disburseTitheStock = true;
            target.SetDirtyCache();
            int disbursed = AccrueAndTax(s, days);
            TestAssert.AreEqual(baseline, disbursed, tolerance: 2,
                message: $"Disbursing an escrow that withheld no silver should pay none (baseline {baseline}, disbursed {disbursed})");
            TestAssert.AreEqual(0.0, target.randomTitheStock, message: "Disbursing should clear the escrow");
        }
        finally
        {
            Detach(s, comp);
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
        DestructiveTestUtil.AssertEmpireInvariants(f, "ExternalBudget_RandomTitheEscrow_ChargesNoSilver");
    }

    [EmpireDestructiveTest("Destructive.Tax")]
    public static void ExternalBudget_SpecifiedTithe_NotBilledInSilver()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = DestructiveTestUtil.RequireTransientSettlement();
        FixedTitheBudgetComp comp = null;
        try
        {
            (ResourceFC target, ThingDef thing) = RequireTitheResourceWith(s, IsPlainThing, "a thing without quality or stuff");
            Isolate(s, target, workers: 0);
            const int days = 2;
            double unit = thing.BaseMarketValue;
            double perDay = Math.Max(100, unit * 4);
            int quantity = Math.Max(1, (int)(perDay * days * 0.5 / unit));

            int baseline = AccrueAndTax(s, days);

            comp = AttachExternalBudget(s, target, perDay);
            target.AddTitheEntry(new ThingQualityTuple { thingDef = thing }, quantity);
            target.SetDirtyCache();

            for (int d = 0; d < days; d++) s.AccumulateDailyProduction();
            TestAssert.AreEqual(0.0, s.ProjectedTitheValue, tolerance: 1,
                message: "The projected tithe cost should leave out goods the external budget pays for");

            s.oneTimeSilverIncome = 0;
            List<Thing> things = s.CreateTax(out int withTithe);

            TestAssert.GreaterThan(TotalValue(things), 0, "The specified tithe should be delivered");
            TestAssert.AreEqual(baseline, withTithe, tolerance: 2,
                message: $"Externally funded tithe goods must not be billed in silver (baseline {baseline}, with tithe {withTithe}, goods {TotalValue(things):F0})");
        }
        finally
        {
            Detach(s, comp);
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
        DestructiveTestUtil.AssertEmpireInvariants(f, "ExternalBudget_SpecifiedTithe_NotBilledInSilver");
    }

    [EmpireDestructiveTest("Destructive.Tax")]
    public static void OwnProduction_SpecifiedTithe_BilledAtDeliveredValue()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = DestructiveTestUtil.RequireTransientSettlement();
        try
        {
            (ResourceFC target, ThingDef thing) = RequireTitheResourceWith(s, IsPlainThing, "a thing without quality or stuff");
            Isolate(s, target, workers: 3);
            const int days = 3;
            TestAssert.GreaterThan(target.taxableProductionMarketValue, 0, "The target resource should produce");

            int baseline = AccrueAndTax(s, days);

            // Stay well inside the cycle's own taxable value, so the whole tithe is own-funded.
            double ownValue = target.taxableProductionMarketValue * days;
            int quantity = (int)(ownValue * 0.5 / thing.BaseMarketValue);
            TestAssert.GreaterThan(quantity, 0, $"{thing.defName} is too expensive for the target's production");
            target.AddTitheEntry(new ThingQualityTuple { thingDef = thing }, quantity);
            target.SetDirtyCache();

            int withTithe = AccrueAndTax(s, days, out List<Thing> things);
            double delivered = TotalValue(things);

            TestAssert.GreaterThan(delivered, 0, "The specified tithe should be delivered");
            TestAssert.AreEqual(baseline - delivered, withTithe, tolerance: 2,
                message: $"Own-funded tithe goods cost their value in silver (baseline {baseline}, with tithe {withTithe}, goods {delivered:F0})");
        }
        finally
        {
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
        DestructiveTestUtil.AssertEmpireInvariants(f, "OwnProduction_SpecifiedTithe_BilledAtDeliveredValue");
    }

    /// <summary>
    /// The random tithe aims at the budget, but a non-stackable item can't fill it exactly, so less
    /// value than the budget is delivered. With an external budget the settlement funds none of it,
    /// so the cycle's silver must match a cycle with no tithe at all.
    /// </summary>
    [EmpireDestructiveTest("Destructive.Tax")]
    public static void ExternalBudget_RandomTitheUnderfilled_ChargesNoSilverAndPaysNone()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = DestructiveTestUtil.RequireTransientSettlement();
        FixedTitheBudgetComp comp = null;
        try
        {
            (ResourceFC target, ThingDef thing) = RequireTitheResourceWith(s,
                d => d is not null && d.stackLimit == 1 && d.BaseMarketValue >= 50, "a non-stackable thing");
            Isolate(s, target, workers: 0);
            const int days = 2;
            double unit = CraftUtil.ThingValue(thing, GenStuff.DefaultStuffFor(thing), QualityCategory.Normal);
            double perDay = unit * 1.25; // 2.5 units over the cycle: whole items can't hit it exactly

            int baseline = AccrueAndTax(s, days);

            comp = AttachExternalBudget(s, target, perDay);
            target.hasRandomTithe = true;
            target.autoMaxRandomTithe = true;
            target.randomTitheFilter.SetDisallowAll();
            target.randomTitheFilter.SetAllow(thing, true);
            target.SetDirtyCache();
            int withTithe = AccrueAndTax(s, days, out List<Thing> things);

            TestAssert.AreEqual(baseline, withTithe, tolerance: 2,
                message: $"An externally funded random tithe must leave silver unchanged (baseline {baseline}, with tithe {withTithe}, "
                    + $"budget {perDay * days:F0}, delivered {TotalValue(things):F0} in {things?.Count ?? 0} item(s))");
        }
        finally
        {
            Detach(s, comp);
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
        DestructiveTestUtil.AssertEmpireInvariants(f, "ExternalBudget_RandomTitheUnderfilled");
    }

    [EmpireDestructiveTest("Destructive.Tax")]
    public static void PausedTithes_DisburseRequest_PaysWithheldSilverAtTaxTime()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = DestructiveTestUtil.RequireTransientSettlement();
        try
        {
            ResourceFC target = ForcedStateTestUtilA.RequireTitheableResource(s);
            Isolate(s, target, workers: 0);
            target.tithesPaused = true;
            const int days = 1;

            target.randomTitheStock = 500;
            target.randomTitheStockSilver = 300;
            target.disburseTitheStock = false;
            int kept = AccrueAndTax(s, days);
            TestAssert.AreEqual(500.0, target.randomTitheStock, message: "Without a disburse request the stock stays");

            target.disburseTitheStock = true;
            int paid = AccrueAndTax(s, days);

            TestAssert.AreEqual(kept + 300, paid, tolerance: 1,
                message: $"A paused resource should still pay out the withheld silver (kept {kept}, paid {paid})");
            TestAssert.AreEqual(0.0, target.randomTitheStock, message: "The stock should be cleared");
            TestAssert.AreEqual(0.0, target.randomTitheStockSilver, message: "The withheld silver should be cleared");
        }
        finally
        {
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
        DestructiveTestUtil.AssertEmpireInvariants(f, "PausedTithes_DisburseRequest");
    }

    /*-*-*-*-*/
    /* AvailableTitheBudget and auto-max */
    /*-*-*-*-*/

    [EmpireDestructiveTest("Destructive.Tax")]
    public static void AvailableTitheBudget_MatchesBudgetMinusClaims()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = DestructiveTestUtil.RequireTransientSettlement();
        try
        {
            (ResourceFC target, ThingDef thing) = RequireTitheResourceWith(s, IsPlainThing, "a thing without quality or stuff");
            Isolate(s, target, workers: 3);
            target.AddTitheEntry(new ThingQualityTuple { thingDef = thing }, 1);
            target.hasRandomTithe = true;
            target.SetStoredRandomTitheBudget(40);

            target.autoMaxRandomTithe = false;
            target.SetDirtyCache();
            double specified = target.CalcTotalTitheValue();
            TestAssert.AreEqual(target.GetProjectedTitheBudget() - specified - 40, target.AvailableTitheBudget(), tolerance: 0.01,
                message: "Without auto-max, the stored random tithe budget counts against the available budget");

            target.autoMaxRandomTithe = true;
            target.SetDirtyCache();
            double available = target.AvailableTitheBudget();
            TestAssert.AreEqual(target.GetProjectedTitheBudget() - specified, available, tolerance: 0.01,
                message: "With auto-max, only the specified tithes count against the available budget");
            TestAssert.AreEqual(Math.Max(0, (int)available), target.randomTitheBudget,
                "Auto-max random tithe budget should be the available budget");

            target.SetAutoMaxRandomTithe(false);
            TestAssert.AreEqual(Math.Max(0, (int)available), target.storedRandomTitheBudget,
                "Turning auto-max off should keep the budget auto-max was using");
        }
        finally
        {
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
        DestructiveTestUtil.AssertEmpireInvariants(f, "AvailableTitheBudget_MatchesBudgetMinusClaims");
    }

    [EmpireDestructiveTest("Destructive.Tax")]
    public static void AutoMaxOff_WhilePaused_KeepsAutoMaxBudget()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = DestructiveTestUtil.RequireTransientSettlement();
        try
        {
            ResourceFC target = ForcedStateTestUtilA.RequireTitheableResource(s);
            Isolate(s, target, workers: 3);
            target.hasRandomTithe = true;
            target.autoMaxRandomTithe = true;
            target.tithesPaused = true;
            target.SetDirtyCache();

            int autoMaxBudget = target.randomTitheBudget;
            target.SetAutoMaxRandomTithe(false);

            TestAssert.AreEqual(autoMaxBudget, target.storedRandomTitheBudget,
                "Turning auto-max off on a paused resource should keep the budget auto-max was using");
            TestAssert.IsTrue(target.storedRandomTitheBudget <= Math.Max(0, (int)target.GetProjectedTitheBudget()),
                "The kept budget should never exceed the projected tithe budget");
        }
        finally
        {
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
        DestructiveTestUtil.AssertEmpireInvariants(f, "AutoMaxOff_WhilePaused_KeepsAutoMaxBudget");
    }

    /*-*-*-*-*/
    /* Base production bonuses */
    /*-*-*-*-*/

    [EmpireDestructiveTest("Destructive.Resource")]
    public static void RebuildBaseResourceBonuses_IsIdempotent_DropsStaleKeepsCustom()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = DestructiveTestUtil.RequireTransientSettlement();
        try
        {
            ResourceFC target = TestAssert.NotNull(s.Resources.FirstOrDefault(r => r.def is not null), "Settlement has no resources");
            double baseBefore = target.productionBase;
            double multBefore = target.productionMult;

            TestAssert.DoesNotThrow(() => target.RebuildBaseResourceBonuses(), "First rebuild threw");
            TestAssert.DoesNotThrow(() => target.RebuildBaseResourceBonuses(), "Second rebuild threw");
            TestAssert.AreEqual(baseBefore, target.productionBase, tolerance: 1e-6,
                message: "Rebuilding should re-derive the same additive bonuses, not stack them");
            TestAssert.AreEqual(multBefore, target.productionMult, tolerance: 1e-6,
                message: "Rebuilding should re-derive the same multipliers, not stack them");

            // A base bonus left over from an old settlement type goes; a bonus from elsewhere stays.
            string stale = $"{target.def.defName}_settle_EmpireTestOldType_{s.Name}";
            const string custom = "EmpireTest_customBonus";
            target.AddProductionAdditive(stale, 5, "stale");
            target.AddProductionMultiplier(stale, 3, "stale");
            target.AddProductionAdditive(custom, 2, "custom");
            try
            {
                target.RebuildBaseResourceBonuses();
                TestAssert.AreEqual(baseBefore + 2, target.productionBase, tolerance: 1e-6,
                    message: "The stale base additive should be dropped and the custom one kept");
                TestAssert.AreEqual(multBefore, target.productionMult, tolerance: 1e-6,
                    message: "The stale base multiplier should be dropped");
            }
            finally
            {
                target.RemoveProductionAdditiveById(custom);
                target.RemoveProductionAdditiveById(stale);
                target.RemoveProductionMultiplierById(stale);
            }
        }
        finally
        {
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
        DestructiveTestUtil.AssertEmpireInvariants(f, "RebuildBaseResourceBonuses");
    }

    /*-*-*-*-*/
    /* Event targeting and the default lifecycle handler */
    /*-*-*-*-*/

    private static FCEventDef MakeTargetAllDef(string name, ResourceTypeDef requiredResource = null) =>
        new() { defName = $"EmpireTest_{name}", label = name, targetAllSettlements = true, requiredResource = requiredResource };

    [EmpireDestructiveTest("Destructive.Events")]
    public static void TargetAllEvent_RequiredResourceMissing_TargetsNoSettlement()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        DestructiveTestUtil.EnsureSettlement();

        // A resource def no settlement can have, so GetResource returns null everywhere.
        ResourceTypeDef missing = new() { defName = "EmpireTest_MissingResource" };
        FCEvent evt = FCEventMaker.MakeRandomEvent(MakeTargetAllDef("NeedsMissing", missing), null);

        TestAssert.IsNull(evt, "No settlement has the required resource, so the event should not be made");
        DestructiveTestUtil.AssertEmpireInvariants(f, "TargetAllEvent_RequiredResourceMissing");
    }

    [EmpireDestructiveTest("Destructive.Events")]
    public static void TargetAllEvent_RequiredResource_OnlyProducingSettlements()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = DestructiveTestUtil.RequireTransientSettlement();
        try
        {
            ResourceFC res = ForcedStateTestUtilA.RequireTitheableResource(s);
            ForcedStateTestUtilA.EnsureWorkers(s, res, 1);
            TestAssert.GreaterThan(res.InstantaneousProduction, 0, "The resource should produce with a worker");
            FCEventDef def = MakeTargetAllDef("NeedsResource", res.def);

            FCEvent producing = TestAssert.NotNull(FCEventMaker.MakeRandomEvent(def, null), "The producing settlement should qualify");
            TestAssert.Contains(producing.settlementTraitLocations, s, "A producing settlement should be targeted");

            res.assignedWorkers = 0;
            res.SetDirtyCache();
            FCEvent idle = FCEventMaker.MakeRandomEvent(def, null);
            TestAssert.IsFalse(idle?.settlementTraitLocations.Contains(s) ?? false,
                "A settlement producing none of the resource should not be targeted");
        }
        finally
        {
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
        DestructiveTestUtil.AssertEmpireInvariants(f, "TargetAllEvent_RequiredResource");
    }

    [EmpireDestructiveTest("Destructive.Events")]
    public static void TargetAllEvent_ExcludesSettlementsOfSameOrIncompatibleActiveEvent()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        List<WorldSettlementFC> pair = DestructiveTestUtil.EnsureSettlements(2);
        WorldSettlementFC busy = pair[0];
        WorldSettlementFC free = pair[1];

        FCEventDef def = MakeTargetAllDef("Exclusion");
        FCEventDef blocker = MakeTargetAllDef("Blocker");
        blocker.incompatibleEvents.Add(def);

        FCEvent active = FCEventMaker.MakeEvent(def);
        active.settlementTraitLocations = [busy];
        FCEvent blocking = FCEventMaker.MakeEvent(blocker);
        blocking.settlementTraitLocations = [free];
        try
        {
            FindFC.EventManager.AddEvent(active);
            FCEvent sameDef = TestAssert.NotNull(FCEventMaker.MakeRandomEvent(def, null), "Other settlements should still qualify");
            TestAssert.IsFalse(sameDef.settlementTraitLocations.Contains(busy),
                "A settlement already hit by an active event of the same def should be excluded");
            TestAssert.Contains(sameDef.settlementTraitLocations, free, "An unaffected settlement should be targeted");

            FindFC.EventManager.AddEvent(blocking);
            FCEvent both = FCEventMaker.MakeRandomEvent(def, null);
            TestAssert.IsFalse(both?.settlementTraitLocations.Contains(free) ?? false,
                "A settlement under an active event that lists this def as incompatible should be excluded");
        }
        finally
        {
            FindFC.EventManager.RemoveWhere(e => e == active || e == blocking);
        }
        DestructiveTestUtil.AssertEmpireInvariants(f, "TargetAllEvent_Exclusion");
    }

    [EmpireDestructiveTest("Destructive.Events")]
    public static void EventWithoutHandler_DefaultAppliesAndClearRemovesStats()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = DestructiveTestUtil.RequireTransientSettlement();
        try
        {
            FCStatDef stat = FCStatDefOf.militaryBaseLevel;
            FCEventDef def = new()
            {
                defName = "EmpireTest_DefaultHandlerEvent",
                label = "default handler",
                statModifiers = [new() { stat = stat, value = 7 }],
            };
            FCEvent evt = FCEventMaker.MakeEvent(def);
            evt.settlementTraitLocations = [s];

            double before = s.GetSettlementStatValue(stat);
            FCEventManager manager = new(); // isolated queue: Clear() must not touch the player's events
            manager.AddEvent(evt);
            TestAssert.AreEqual(before + 7, s.GetSettlementStatValue(stat), tolerance: 1e-6,
                message: "Queueing an event without a handler extension should apply its stat modifiers");

            manager.Clear();
            TestAssert.AreEqual(0, manager.Count, "Clear should empty the queue");
            TestAssert.AreEqual(before, s.GetSettlementStatValue(stat), tolerance: 1e-6,
                message: "Clear should run each event's expiry, removing its stat modifiers");
        }
        finally
        {
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
        DestructiveTestUtil.AssertEmpireInvariants(f, "EventWithoutHandler_DefaultHandler");
    }

    /*-*-*-*-*/
    /* Edicts during enactment */
    /*-*-*-*-*/

    [EmpireDestructiveTest("Destructive.Policy")]
    public static void EdictBeingEnacted_HasNoBehaviorUntilActive()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        PolicyManager pm = FindFC.PolicyManager;
        DestructiveTestUtil.EnsureFactionLevel(FactionFC.MaxFactionLevel);

        FCPolicyDef enacted = null;
        foreach (FCPolicyDef d in DefDatabase<FCPolicyDef>.AllDefsListForReading
                     .Where(p => p.IsEdict && p.enactDuration > 0 && p.HasBehavior))
        {
            if (pm.HasEdict(d)) pm.RevokeEdict(d.category, silent: true);
            pm.EnactEdict(d);
            if (pm.HasEdict(d)) { enacted = d; break; }
        }
        TestAssert.NotNull(enacted, "No edict with an enactment time and a behavior could be enacted");
        try
        {
            FCPolicy edict = pm.edicts[enacted.category];
            TestAssert.IsFalse(edict.IsFullyActive, "A freshly enacted edict should still be enacting");
            TestAssert.IsTrue(pm.pendingEdictActivations.Contains(enacted.category), "The edict should wait for activation");
            TestAssert.IsFalse(pm.CachedBehaviors.Contains(edict.behavior),
                "An edict still being enacted must not run its behavior");
            TestAssert.IsFalse(pm.ActivePolicies().Contains(edict), "An edict still being enacted is not active");
            TestAssert.IsFalse(pm.ActivePolicies().Any(p => p.def == FCPolicyDefOf.empty), "Empty trait slots are never active");

            // Finish the enactment and let the rare-tick check pick it up.
            edict.timeEnacted = Find.TickManager.TicksGame - enacted.enactDuration;
            pm.CheckEdictActivations();

            TestAssert.IsFalse(pm.pendingEdictActivations.Contains(enacted.category), "The edict should no longer be pending");
            TestAssert.IsTrue(pm.CachedBehaviors.Contains(edict.behavior),
                "Once active, the edict's behavior should be in the behavior cache");
            TestAssert.IsTrue(pm.ActivePolicies().Contains(edict), "Once active, the edict should be listed as active");
        }
        finally
        {
            pm.RevokeEdict(enacted.category, silent: true);
        }
        DestructiveTestUtil.AssertEmpireInvariants(f, "EdictBeingEnacted_HasNoBehaviorUntilActive");
    }

    /*-*-*-*-*/
    /* Squad cost bills */
    /*-*-*-*-*/

    [EmpireDestructiveTest("Destructive.Tax")]
    public static void RestockCostBill_ChargesHomeWithScaledPenalties()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC home = DestructiveTestUtil.RequireTransientSettlement();
        BillFC bill = null;
        try
        {
            MercenarySquadFC squad = new() { settlement = home };
            TestUtil.WithOverride(() => DebugSettings.godMode, v => DebugSettings.godMode = v, false, () =>
            {
                TestAssert.IsNull(FindFC.TaxLedger.CreateRestockCostBill(squad, 0), "A zero cost should make no bill");
                bill = FindFC.TaxLedger.CreateRestockCostBill(squad, 120);
            });
            TestAssert.NotNull(bill, "A positive cost should make a bill");

            TestAssert.AreEqual(home, bill.settlement, "The bill should be charged to the squad's home");
            TestAssert.AreEqual(-120, bill.taxes.silverAmount, "The bill should charge the cost");
            TestAssert.AreEqual("FCBillKindSquadRestock".Translate().ToString(), bill.label, "The bill should carry the restock label");
            TestAssert.Contains(FindFC.TaxLedger.Bills, bill, "The bill should be on the ledger");

            double income = Math.Max(1.0, home.totalIncome);
            BillStatPenalty unpaidUnrest = bill.unpaidPenalties.FirstOrDefault(p => p.stat == BillPenaltyStat.Unrest);
            BillStatPenalty lateUnrest = bill.latePaidPenalties.FirstOrDefault(p => p.stat == BillPenaltyStat.Unrest);
            TestAssert.NotNull(unpaidUnrest, "An unpaid unrest penalty should be set");
            TestAssert.NotNull(lateUnrest, "A late-paid unrest penalty should be set");
            TestAssert.AreEqual(15 * 120 / income, unpaidUnrest.amount, tolerance: 1e-6, message: "Unpaid penalty uses the restock factor");
            TestAssert.AreEqual(3 * 120 / income, lateUnrest.amount, tolerance: 1e-6, message: "Late penalty uses the restock factor");
            TestAssert.AreEqual(2, bill.unpaidPenalties.Count, "Unrest and happiness penalties when unpaid");
            TestAssert.AreEqual(2, bill.latePaidPenalties.Count, "Unrest and happiness penalties when paid late");
        }
        finally
        {
            if (bill is not null) FindFC.TaxLedger.RemoveBill(bill);
            DestructiveTestUtil.SafeRemoveSettlement(home);
        }
        DestructiveTestUtil.AssertEmpireInvariants(f, "RestockCostBill_ChargesHome");
    }

    /*-*-*-*-*/
    /* Exclusive spot buildings */
    /*-*-*-*-*/

    private static T SpawnPlayerSpot<T>(Map map) where T : Building
    {
        ThingDef def = TestAssert.NotNull(DefDatabase<ThingDef>.AllDefsListForReading.FirstOrDefault(d => d.thingClass == typeof(T)),
            $"No ThingDef uses {typeof(T).Name}");
        bool found = CellFinder.TryFindRandomCellNear(map.Center, map, 40,
            c => c.Standable(map) && c.GetEdifice(map) is null && c.GetFirstItem(map) is null
                && c.GetFirstBuilding(map) is null && c.GetFirstPawn(map) is null, out IntVec3 cell);
        TestAssert.IsTrue(found, $"No free cell on {map} for a {def.defName}");
        Thing thing = ThingMaker.MakeThing(def);
        thing.SetFaction(Faction.OfPlayer);
        return (T)GenSpawn.Spawn(thing, cell, map);
    }

    private static List<T> ActiveSpots<T>(Func<T, bool> isActive) where T : Building =>
        [.. Find.Maps.SelectMany(m => m.listerBuildings.allBuildingsColonist).OfType<T>().Where(isActive)];

    [EmpireDestructiveTest("Destructive.Buildings")]
    public static void CapitalSpot_TogglingOffClearsCapital_AndOnlyOneIsActive()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        Map map = TestAssert.NotNull(Find.AnyPlayerHomeMap, "No player home map to place a capital seat on");
        PlanetTile originalCapital = f.capitalLocation;
        List<Building_CapitalSpot> originallyActive = ActiveSpots<Building_CapitalSpot>(b => b.IsActiveCapitalSpot);

        Building_CapitalSpot first = null;
        Building_CapitalSpot second = null;
        try
        {
            first = SpawnPlayerSpot<Building_CapitalSpot>(map);
            second = SpawnPlayerSpot<Building_CapitalSpot>(map);

            first.IsActiveCapitalSpot = true;
            TestAssert.AreEqual(map.Tile, f.capitalLocation, "Activating a seat should move the capital to its tile");
            TestAssert.IsTrue(originallyActive.All(b => !b.IsActiveCapitalSpot), "Activating a seat should turn off every other seat");

            second.IsActiveCapitalSpot = true;
            TestAssert.IsFalse(first.IsActiveCapitalSpot, "Activating the second seat should turn off the first");
            TestAssert.AreEqual(map.Tile, f.capitalLocation, "The capital should stay on the active seat's tile");

            second.IsActiveCapitalSpot = false;
            TestAssert.IsFalse(f.capitalLocation.Valid, "Turning the active seat off should clear the capital");
        }
        finally
        {
            if (first is not null && !first.Destroyed) { first.IsActiveCapitalSpot = false; first.Destroy(); }
            if (second is not null && !second.Destroyed) { second.IsActiveCapitalSpot = false; second.Destroy(); }
            foreach (Building_CapitalSpot b in originallyActive.Where(b => b.Spawned))
                b.IsActiveCapitalSpot = true;
            f.capitalLocation = originalCapital;
        }
        DestructiveTestUtil.AssertEmpireInvariants(f, "CapitalSpot_Toggle");
    }

    [EmpireDestructiveTest("Destructive.Buildings")]
    public static void CapitalSpot_TurningOffAnInactiveSeat_KeepsCapital()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        Map map = TestAssert.NotNull(Find.AnyPlayerHomeMap, "No player home map to place a capital seat on");
        PlanetTile originalCapital = f.capitalLocation;
        List<Building_CapitalSpot> originallyActive = ActiveSpots<Building_CapitalSpot>(b => b.IsActiveCapitalSpot);

        Building_CapitalSpot seat = null;
        try
        {
            seat = SpawnPlayerSpot<Building_CapitalSpot>(map);
            f.capitalLocation = map.Tile;
            seat.IsActiveCapitalSpot = false; // already off: no transition, nothing to clear
            TestAssert.AreEqual(map.Tile, f.capitalLocation, "Setting an inactive seat to off must not clear the capital");
        }
        finally
        {
            if (seat is not null && !seat.Destroyed) seat.Destroy();
            foreach (Building_CapitalSpot b in originallyActive.Where(b => b.Spawned))
                b.IsActiveCapitalSpot = true;
            f.capitalLocation = originalCapital;
        }
        DestructiveTestUtil.AssertEmpireInvariants(f, "CapitalSpot_InactiveOff");
    }

    [EmpireDestructiveTest("Destructive.Buildings")]
    public static void TaxSpot_ActivatingOneTurnsOffTheOthers()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        Map map = TestAssert.NotNull(Find.AnyPlayerHomeMap, "No player home map to place a tax spot on");
        List<Building_TaxSpot> originallyActive = ActiveSpots<Building_TaxSpot>(b => b.IsActiveTaxDeliverySpot);

        Building_TaxSpot first = null;
        Building_TaxSpot second = null;
        try
        {
            first = SpawnPlayerSpot<Building_TaxSpot>(map);
            second = SpawnPlayerSpot<Building_TaxSpot>(map);

            first.IsActiveTaxDeliverySpot = true;
            TestAssert.IsTrue(originallyActive.All(b => !b.IsActiveTaxDeliverySpot), "Activating a spot should turn off every other spot");
            second.IsActiveTaxDeliverySpot = true;
            TestAssert.IsFalse(first.IsActiveTaxDeliverySpot, "Activating the second spot should turn off the first");
            TestAssert.IsTrue(PaymentUtil.TryFindActiveTaxSpot(map, out Building_TaxSpot found) && found == second,
                "The second spot should be the active delivery spot");
        }
        finally
        {
            if (first is not null && !first.Destroyed) first.Destroy();
            if (second is not null && !second.Destroyed) second.Destroy();
            foreach (Building_TaxSpot b in originallyActive.Where(b => b.Spawned))
                b.IsActiveTaxDeliverySpot = true;
        }
        DestructiveTestUtil.AssertEmpireInvariants(f, "TaxSpot_Exclusive");
    }
}
