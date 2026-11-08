using UnityEngine;

namespace FactionColonies;

public static class RegistryTests
{
    // ============================
    // Helpers
    // ============================

    /// <summary>
    /// Registers <paramref name="item"/> for the duration of <paramref name="body"/>. Unregisters
    /// twice: if dedup regresses, both copies must be removed so a failing run can't leak a test
    /// double into the live session.
    /// </summary>
    private static void WithRegistered<T>(T item, Action<T> register, Action<T> unregister, Action body)
    {
        register(item);
        try { body(); }
        finally { unregister(item); unregister(item); }
    }

    /// <summary>
    /// Checks one registry's Register/Unregister wiring through its public API: a registered item is
    /// reached once, registering it again is ignored, and after Unregister it is no longer reached.
    /// <paramref name="timesReached"/> runs the registry's dispatch (or scans its exposed list) and
    /// returns how many times the item was reached by that single call.
    /// </summary>
    private static void AssertRegistrationContract<T>(T item, Action<T> register, Action<T> unregister, Func<int> timesReached)
    {
        try
        {
            register(item);
            TestAssert.AreEqual(1, timesReached(), "A registered item should be reached once");
            register(item);
            TestAssert.AreEqual(1, timesReached(), "A duplicate registration should be ignored");
            unregister(item);
            TestAssert.AreEqual(0, timesReached(), "An unregistered item should no longer be reached");
        }
        finally { unregister(item); unregister(item); }
    }

    /// <summary>Registers a throwing double and asserts the registry's dispatch swallows the exception.</summary>
    private static void AssertDispatchIsolatesThrowing<T>(T bad, Action<T> register, Action<T> unregister, params Action[] dispatches)
    {
        WithRegistered(bad, register, unregister, () =>
        {
            foreach (Action dispatch in dispatches)
                TestAssert.DoesNotThrow(dispatch);
        });
    }

    /// <summary>Runs <paramref name="dispatch"/> and returns how much <paramref name="read"/> grew.</summary>
    private static int CountDuring(Func<int> read, Action dispatch)
    {
        int before = read();
        dispatch();
        return read() - before;
    }

    private static int Occurrences<T>(IEnumerable<T> items, T item) where T : class
        => items.Count(x => ReferenceEquals(x, item));

    // ============================
    // Test Doubles
    // ============================

    private class TestLifecycleParticipant : ISettlementListener, IMilitaryOperationListener, IResearchListener
    {
        public int SettlementCreatedCount;
        public int SettlementRemovedCount;
        public int BuildingConstructedCount;
        public int BattleResolvedCount;
        public int ResearchCompletedCount;
        public void OnSettlementCreated(WorldSettlementFC s) => SettlementCreatedCount++;
        public void OnSettlementRemoved(WorldSettlementFC s) => SettlementRemovedCount++;
        public void OnBuildingConstructed(WorldSettlementFC s, BuildingFCDef b, int slot) => BuildingConstructedCount++;
        public void OnBattleResolved(MilitaryOperation op, bool victory, BattleResult result) => BattleResolvedCount++;
        public void OnResearchCompleted(ResearchProjectDef p) => ResearchCompletedCount++;
    }

    private class ThrowingLifecycleParticipant : ISettlementListener, IMilitaryOperationListener
    {
        public void OnSettlementCreated(WorldSettlementFC s) => throw new InvalidOperationException("test");
        public void OnSettlementRemoved(WorldSettlementFC s) => throw new InvalidOperationException("test");
        public void OnSettlementUpgraded(WorldSettlementFC s, int oldLevel, int newLevel) => throw new InvalidOperationException("test");
        public void OnSettlementTypeChanged(WorldSettlementFC s, WorldSettlementDef oldDef, WorldSettlementDef newDef) => throw new InvalidOperationException("test");
        public void OnBuildingConstructed(WorldSettlementFC s, BuildingFCDef b, int slot) => throw new InvalidOperationException("test");
        public void OnBuildingDeconstructed(WorldSettlementFC s, BuildingFCDef b, int slot) => throw new InvalidOperationException("test");
        public void OnOperationCreated(MilitaryOperation op) => throw new InvalidOperationException("test");
        public void OnOperationResolved(MilitaryOperation op) => throw new InvalidOperationException("test");
        public void OnBattleResolved(MilitaryOperation op, bool victory, BattleResult result) => throw new InvalidOperationException("test");
    }

    private class TestBattleModifier : IBattleModifier
    {
        public double LevelBonus;
        public int InvokeCount;
        public void ModifyForce(BattleForceContext ctx, MilitaryForce force, bool isAttacker)
        {
            InvokeCount++;
            force.militaryLevel += LevelBonus;
        }
    }

    private class ThrowingBattleModifier : IBattleModifier
    {
        public void ModifyForce(BattleForceContext ctx, MilitaryForce force, bool isAttacker) => throw new InvalidOperationException("test");
    }

    private class TestFactionPowerModifier : IFactionPowerModifier
    {
        public double LevelBonus;
        public int InvokeCount;
        public void ModifyFactionPower(RimWorld.Faction faction, EnemyPower power)
        {
            InvokeCount++;
            if (power is not null) power.level += LevelBonus;
        }
    }

    private class ThrowingFactionPowerModifier : IFactionPowerModifier
    {
        public void ModifyFactionPower(RimWorld.Faction faction, EnemyPower power) => throw new InvalidOperationException("test");
    }

    private class TestSettlementPowerModifier : ISettlementPowerModifier
    {
        public double LevelBonus;
        public int InvokeCount;
        public void ModifySettlementPower(RimWorld.Planet.Settlement settlement, EnemyPower power)
        {
            InvokeCount++;
            if (power is not null) power.level += LevelBonus;
        }
    }

    private class ThrowingSettlementPowerModifier : ISettlementPowerModifier
    {
        public void ModifySettlementPower(RimWorld.Planet.Settlement settlement, EnemyPower power) => throw new InvalidOperationException("test");
    }

    private class TestPaymentModifier : ISilverPaymentModifier
    {
        public int Discount;
        public int InvokeCount;
        public void ModifyPayment(SilverPaymentContext context)
        {
            InvokeCount++;
            context.Amount -= Discount;
        }
    }

    private class ThrowingPaymentModifier : ISilverPaymentModifier
    {
        public void ModifyPayment(SilverPaymentContext context) => throw new InvalidOperationException("test");
    }

    private class TestDefenseValidator : IDefenseValidator
    {
        public bool Allow = true;
        public int InvokeCount;
        public bool CanDefend(WorldSettlementFC defender, WorldSettlementFC target)
        {
            InvokeCount++;
            return Allow;
        }
    }

    private class ThrowingDefenseValidator : IDefenseValidator
    {
        public bool CanDefend(WorldSettlementFC defender, WorldSettlementFC target) => throw new InvalidOperationException("test");
    }

    private class TestTaxTicker : ITaxTickParticipant
    {
        public int PreTaxCount;
        public int PostTaxCount;
        public void PreTaxResolution(FactionFC f) => PreTaxCount++;
        public void PostTaxResolution(FactionFC f) => PostTaxCount++;
    }

    private class ThrowingTaxTicker : ITaxTickParticipant
    {
        public void PreTaxResolution(FactionFC f) => throw new InvalidOperationException("test");
        public void PostTaxResolution(FactionFC f) => throw new InvalidOperationException("test");
        public void PreSettlementCreateTax(WorldSettlementFC s) => throw new InvalidOperationException("test");
        public void PostSettlementCreateTax(WorldSettlementFC s, ref int a, List<Thing> t) => throw new InvalidOperationException("test");
    }

    private class TestSquadValidator : ISquadAssignmentValidator
    {
        public bool Allow = true;
        public string RejectReason = "test reject";
        public int InvokeCount;
        public bool CanAssign(WorldSettlementFC s, MercenarySquadFC sq, out string reason)
        {
            InvokeCount++;
            reason = Allow ? null : RejectReason;
            return Allow;
        }
    }

    private class ThrowingSquadValidator : ISquadAssignmentValidator
    {
        public bool CanAssign(WorldSettlementFC s, MercenarySquadFC sq, out string reason)
        {
            reason = null;
            throw new InvalidOperationException("test");
        }
    }

    private class TestMainTab : IMainTabWindowOverview
    {
        public int PostCloseCount;
        public void PostCloseWindow() => PostCloseCount++;
        public string TabName() => "TestTab";
    }

    private class ThrowingMainTab : IMainTabWindowOverview
    {
        public void PostCloseWindow() => throw new InvalidOperationException("test");
        public string TabName() => "ThrowingTab";
    }

    // ============================
    // RegistryList / RegistryDispatch contract
    // ============================
    // Every per-domain registry stores its participants in a RegistryList and dispatches through
    // RegistryDispatch, so dedup, unregister and exception isolation are tested here once, against
    // a private list that no live game code reads. Per-registry tests below only check each
    // registry's wiring and its own dispatch semantics.

    private class ContractItem
    {
        public bool Throws;
        public bool Verdict = true;
        public string Result = "x";
        public int InvokeCount;

        public bool Fire()
        {
            InvokeCount++;
            if (Throws) throw new InvalidOperationException("test");
            return Verdict;
        }
    }

    [EmpireTest("Registry")]
    public static void RegistryList_RegisterUnregister_DedupsAndIgnoresNull()
    {
        var list = new RegistryList<ContractItem>();
        var a = new ContractItem();
        var b = new ContractItem();

        list.Register(null);
        TestAssert.AreEqual(0, list.Count, "Registering null should be ignored");

        list.Register(a);
        list.Register(a);
        list.Register(b);
        TestAssert.AreEqual(2, list.Count, "A duplicate registration should be ignored");
        TestAssert.IsTrue(ReferenceEquals(a, list.Items[0]) && ReferenceEquals(b, list.Items[1]),
            "Items keep registration order");

        TestAssert.DoesNotThrow(() => list.Unregister(null), "Unregistering null should be a no-op");
        list.Unregister(a);
        TestAssert.AreEqual(1, list.Count);
        TestAssert.IsFalse(list.Items.Contains(a), "Unregistered item should be removed");
        list.Unregister(a);
        TestAssert.AreEqual(1, list.Count, "Unregistering an absent item should be a no-op");

        list.ClearAll();
        TestAssert.AreEqual(0, list.Count, "ClearAll should empty the list");
    }

    [EmpireTest("Registry")]
    public static void RegistryList_Valid_SkipsInvalidAndThrowingItems()
    {
        var list = new RegistryList<ContractItem>();
        var good = new ContractItem();
        var invalid = new ContractItem { Verdict = false };
        var throwing = new ContractItem { Throws = true };
        list.Register(good);

        TestAssert.IsTrue(ReferenceEquals(list.Items, list.Valid(i => i.Fire(), "test")),
            "When every item passes, the backing list itself is returned");

        list.Register(invalid);
        list.Register(throwing);
        IReadOnlyList<ContractItem> valid = list.Valid(i => i.Fire(), "test");
        TestAssert.AreEqual(1, valid.Count, "Invalid and throwing items should be skipped");
        TestAssert.IsTrue(ReferenceEquals(good, valid[0]));
        TestAssert.AreEqual(3, list.Count, "Valid does not unregister anything");
    }

    [EmpireTest("Registry")]
    public static void RegistryDispatch_Each_ThrowingItemDoesNotBlockOthers()
    {
        var list = new RegistryList<ContractItem>();
        var bad = new ContractItem { Throws = true };
        var good1 = new ContractItem();
        var good2 = new ContractItem();
        list.Register(bad);
        list.Register(good1);
        list.Register(good2);

        TestAssert.DoesNotThrow(() => RegistryDispatch.Each(list.Items, i => i.Fire(), "Fire"));
        TestAssert.AreEqual(1, good1.InvokeCount, "Items after a throwing one should still run");
        TestAssert.AreEqual(1, good2.InvokeCount, "Every registered item should run once");

        int invalidations = 0;
        TestAssert.DoesNotThrow(() => RegistryDispatch.EachInvalidating(list.Items, i => i.Fire(), () => invalidations++, "Fire"));
        TestAssert.AreEqual(2, good2.InvokeCount, "EachInvalidating should run every item");
        TestAssert.AreEqual(3, invalidations, "EachInvalidating should invalidate after every item, including a throwing one");
    }

    [EmpireTest("Registry")]
    public static void RegistryDispatch_All_ShortCircuitsAndFailsOpen()
    {
        var empty = new RegistryList<ContractItem>();
        TestAssert.IsTrue(RegistryDispatch.All(empty.Items, i => i.Fire(), "Fire"), "No items means default allow");

        var list = new RegistryList<ContractItem>();
        var bad = new ContractItem { Throws = true };
        var allow = new ContractItem();
        list.Register(bad);
        list.Register(allow);
        bool result = false;
        TestAssert.DoesNotThrow(() => result = RegistryDispatch.All(list.Items, i => i.Fire(), "Fire"));
        TestAssert.IsTrue(result, "A throwing predicate is skipped (fail open)");
        TestAssert.AreEqual(1, allow.InvokeCount, "Predicates after a throwing one should still run");

        var reject = new ContractItem { Verdict = false };
        var after = new ContractItem();
        list.Register(reject);
        list.Register(after);
        TestAssert.IsFalse(RegistryDispatch.All(list.Items, i => i.Fire(), "Fire"), "One rejection should return false");
        TestAssert.AreEqual(0, after.InvokeCount, "The walk should stop at the first rejection");
    }

    [EmpireTest("Registry")]
    public static void RegistryDispatch_AggregateAndFind_SkipThrowingItems()
    {
        var list = new RegistryList<ContractItem>();
        var bad = new ContractItem { Throws = true, Result = null };
        var miss = new ContractItem { Verdict = false, Result = null };
        var hit = new ContractItem();
        list.Register(bad);
        list.Register(miss);
        list.Register(hit);

        int sum = 0;
        TestAssert.DoesNotThrow(() => sum = RegistryDispatch.Aggregate(list.Items, 10, (acc, i) => i.Fire() ? acc + 1 : acc, "Fire"));
        TestAssert.AreEqual(11, sum, "A throwing reducer is skipped and keeps the running value");

        ContractItem first = null;
        TestAssert.DoesNotThrow(() => first = RegistryDispatch.First(list.Items, i => i.Fire(), "Fire"));
        TestAssert.IsTrue(ReferenceEquals(hit, first), "First skips throwing and non-matching items");
        TestAssert.IsNull(RegistryDispatch.First(list.Items, _ => false, "Fire"), "First returns null when nothing matches");

        ContractItem nonNull = null;
        TestAssert.DoesNotThrow(() => nonNull = RegistryDispatch.FirstNonNull(list.Items, i => { i.Fire(); return i.Result; }, "Fire"));
        TestAssert.IsTrue(ReferenceEquals(hit, nonNull), "FirstNonNull skips throwing and null-yielding items");
    }

    // ============================
    // LifecycleRegistry
    // ============================

    /// <summary>
    /// Builds a minimal <see cref="MilitaryOperation"/> for tests that exercise the op-aware
    /// registry overloads. Not registered with the manager — purely a transient stand-in.
    /// </summary>
    private static MilitaryOperation MakeSyntheticOp(WorldSettlementFC home)
    {
        var op = new MilitaryOperation(-1, null, home?.Tile ?? RimWorld.Planet.PlanetTile.Invalid, home);
        op.aggressor.homeSettlement = home;
        op.aggressor.faction = FindFC.EmpireFaction;
        return op;
    }

    [EmpireDestructiveTest("Registry")]
    public static void Lifecycle_RegistrationContract()
    {
        var settlement = DestructiveTestUtil.EnsureSettlement();
        var p = new TestLifecycleParticipant();
        AssertRegistrationContract(p, LifecycleRegistry.Register, LifecycleRegistry.Unregister,
            () => CountDuring(() => p.SettlementCreatedCount, () => LifecycleRegistry.InvokeOnSettlementCreated(settlement)));
    }

    [EmpireDestructiveTest("Registry")]
    public static void Lifecycle_Invoke_ReachesEachHook()
    {
        var settlement = DestructiveTestUtil.EnsureSettlement();
        var p = new TestLifecycleParticipant();
        WithRegistered(p, LifecycleRegistry.Register, LifecycleRegistry.Unregister, () =>
        {
            LifecycleRegistry.InvokeOnSettlementCreated(settlement);
            TestAssert.AreEqual(1, p.SettlementCreatedCount, "OnSettlementCreated");
            LifecycleRegistry.InvokeOnSettlementRemoved(settlement);
            TestAssert.AreEqual(1, p.SettlementRemovedCount, "OnSettlementRemoved");
            LifecycleRegistry.InvokeOnBuildingConstructed(settlement, null, 0);
            TestAssert.AreEqual(1, p.BuildingConstructedCount, "OnBuildingConstructed");
            LifecycleRegistry.InvokeOnBattleResolved(MakeSyntheticOp(settlement), true, null);
            TestAssert.AreEqual(1, p.BattleResolvedCount, "OnBattleResolved");
        });
    }

    [EmpireDestructiveTest("Registry")]
    public static void Lifecycle_ThrowingParticipant_DoesNotBlockOthers()
    {
        var settlement = DestructiveTestUtil.EnsureSettlement();
        var bad = new ThrowingLifecycleParticipant();
        var good1 = new TestLifecycleParticipant();
        var good2 = new TestLifecycleParticipant();
        WithRegistered(bad, LifecycleRegistry.Register, LifecycleRegistry.Unregister, () =>
        WithRegistered(good1, LifecycleRegistry.Register, LifecycleRegistry.Unregister, () =>
        WithRegistered(good2, LifecycleRegistry.Register, LifecycleRegistry.Unregister, () =>
        {
            TestAssert.DoesNotThrow(() => LifecycleRegistry.InvokeOnSettlementCreated(settlement));
            TestAssert.AreEqual(1, good1.SettlementCreatedCount,
                "Good participant should still be invoked after bad one throws");
            TestAssert.AreEqual(1, good2.SettlementCreatedCount, "Every participant should be invoked");
        })));
    }

    // ============================
    // BattleModifierRegistry
    // ============================

    private static MilitaryForce MakeForce() => new() { militaryLevel = 5, militaryEfficiency = 1.0, forceRemaining = 5 };

    [EmpireTest("Registry")]
    public static void BattleModifier_RegistrationContract()
    {
        var c = new TestBattleModifier();
        AssertRegistrationContract(c, BattleModifierRegistry.Register, BattleModifierRegistry.Unregister,
            () => CountDuring(() => c.InvokeCount, () => BattleModifierRegistry.InvokeBattleModifiers(null, MakeForce(), true)));
        AssertDispatchIsolatesThrowing(new ThrowingBattleModifier(), BattleModifierRegistry.Register, BattleModifierRegistry.Unregister,
            () => BattleModifierRegistry.InvokeBattleModifiers(null, MakeForce(), true));
    }

    [EmpireTest("Registry")]
    public static void BattleModifier_Register_ModifiesForce()
    {
        var c = new TestBattleModifier { LevelBonus = 2.0 };
        WithRegistered(c, BattleModifierRegistry.Register, BattleModifierRegistry.Unregister, () =>
        {
            MilitaryForce force = MakeForce();
            BattleModifierRegistry.InvokeBattleModifiers(null, force, true);
            TestAssert.AreEqual(7.0, force.militaryLevel, message: "Level should increase by 2");
        });
    }

    // ============================
    // BattleModifierRegistry — IFactionPowerModifier
    // ============================

    [EmpireTest("Registry")]
    public static void FactionPowerModifier_RegistrationContract()
    {
        RimWorld.Faction faction = Find.FactionManager.OfPlayer;
        var modifier = new TestFactionPowerModifier();
        AssertRegistrationContract(modifier, BattleModifierRegistry.Register, BattleModifierRegistry.Unregister,
            () => CountDuring(() => modifier.InvokeCount,
                () => BattleModifierRegistry.InvokeFactionPowerModifiers(faction, new EnemyPower { level = 5, efficiency = 1.0 })));
        AssertDispatchIsolatesThrowing(new ThrowingFactionPowerModifier(), BattleModifierRegistry.Register, BattleModifierRegistry.Unregister,
            () => BattleModifierRegistry.InvokeFactionPowerModifiers(faction, new EnemyPower { level = 5, efficiency = 1.0 }));
    }

    [EmpireTest("Registry")]
    public static void FactionPowerModifier_RegistersAndInvokes()
    {
        RimWorld.Faction faction = Find.FactionManager.OfPlayer;
        var modifier = new TestFactionPowerModifier { LevelBonus = 3.0 };
        WithRegistered(modifier, BattleModifierRegistry.Register, BattleModifierRegistry.Unregister, () =>
        {
            var power = new EnemyPower { level = 5, efficiency = 1.0 };
            BattleModifierRegistry.InvokeFactionPowerModifiers(faction, power);
            TestAssert.AreEqual(1, modifier.InvokeCount);
            TestAssert.AreEqual(8.0, power.level, message: "Level should increase by 3");
        });
    }

    // ============================
    // BattleModifierRegistry — ISettlementPowerModifier
    // ============================

    [EmpireTest("Registry")]
    public static void SettlementPowerModifier_RegistrationContract()
    {
        var modifier = new TestSettlementPowerModifier();
        AssertRegistrationContract(modifier, BattleModifierRegistry.Register, BattleModifierRegistry.Unregister,
            () => CountDuring(() => modifier.InvokeCount,
                () => BattleModifierRegistry.InvokeSettlementPowerModifiers(null, new EnemyPower { level = 4, efficiency = 1.0 })));
        AssertDispatchIsolatesThrowing(new ThrowingSettlementPowerModifier(), BattleModifierRegistry.Register, BattleModifierRegistry.Unregister,
            () => BattleModifierRegistry.InvokeSettlementPowerModifiers(null, new EnemyPower { level = 4, efficiency = 1.0 }));
    }

    [EmpireTest("Registry")]
    public static void SettlementPowerModifier_RegistersAndInvokes()
    {
        // Settlement parameter can be null — the registry just forwards it to the modifier,
        // which here doesn't dereference it.
        var modifier = new TestSettlementPowerModifier { LevelBonus = 2.5 };
        WithRegistered(modifier, BattleModifierRegistry.Register, BattleModifierRegistry.Unregister, () =>
        {
            var power = new EnemyPower { level = 4, efficiency = 1.0 };
            BattleModifierRegistry.InvokeSettlementPowerModifiers(null, power);
            TestAssert.AreEqual(1, modifier.InvokeCount);
            TestAssert.AreEqual(6.5, power.level, message: "Level should increase by 2.5");
        });
    }

    // ============================
    // BattleModifierRegistry — Cross-interface independence
    // ============================

    [EmpireTest("Registry")]
    public static void BattleModifierRegistry_DuplicateRegisterAcrossInterfaces_Independent()
    {
        // Three separate lists in BattleModifierRegistry. Registering an instance that
        // implements two interfaces should produce independent registrations — invoking
        // each chain hits the instance once per chain.
        //
        // We use distinct test doubles per interface here (no class implements both),
        // but the lists themselves must stay independent. Verify by registering on two
        // chains and confirming both fire.
        var faction = Find.FactionManager.OfPlayer;

        var battleMod = new TestBattleModifier { LevelBonus = 1 };
        var factionMod = new TestFactionPowerModifier { LevelBonus = 2 };
        WithRegistered(battleMod, BattleModifierRegistry.Register, BattleModifierRegistry.Unregister, () =>
        WithRegistered(factionMod, BattleModifierRegistry.Register, BattleModifierRegistry.Unregister, () =>
        {
            MilitaryForce battleForce = MakeForce();
            var factionPower = new EnemyPower { level = 5, efficiency = 1.0 };

            BattleModifierRegistry.InvokeBattleModifiers(null, battleForce, true);
            BattleModifierRegistry.InvokeFactionPowerModifiers(faction, factionPower);

            TestAssert.AreEqual(6.0, battleForce.militaryLevel, message: "Battle chain ran");
            TestAssert.AreEqual(7.0, factionPower.level, message: "Faction chain ran");
            TestAssert.AreEqual(1, factionMod.InvokeCount,
                "Faction modifier should fire exactly once per InvokeFactionPowerModifiers call");
        }));
    }

    // ============================
    // SilverPaymentRegistry
    // ============================

    [EmpireTest("Registry")]
    public static void SilverPayment_RegistrationContract()
    {
        var c = new TestPaymentModifier();
        AssertRegistrationContract(c, SilverPaymentRegistry.Register, SilverPaymentRegistry.Unregister,
            () => CountDuring(() => c.InvokeCount, () => SilverPaymentRegistry.InvokeModifiers(new SilverPaymentContext(200, "test"))));
        AssertDispatchIsolatesThrowing(new ThrowingPaymentModifier(), SilverPaymentRegistry.Register, SilverPaymentRegistry.Unregister,
            () => SilverPaymentRegistry.InvokeModifiers(new SilverPaymentContext(200, "test")));
    }

    [EmpireTest("Registry")]
    public static void SilverPayment_Register_ModifiesAmount()
    {
        var c = new TestPaymentModifier { Discount = 50 };
        WithRegistered(c, SilverPaymentRegistry.Register, SilverPaymentRegistry.Unregister, () =>
        {
            var ctx = new SilverPaymentContext(200, "test");
            SilverPaymentRegistry.InvokeModifiers(ctx);
            TestAssert.AreEqual(150, ctx.Amount, message: "Amount should be reduced by 50");
        });
    }

    [EmpireTest("Registry")]
    public static void SilverPayment_ReturnsContext()
    {
        var ctx = new SilverPaymentContext(100, "test");
        SilverPaymentContext returned = SilverPaymentRegistry.InvokeModifiers(ctx);
        TestAssert.IsTrue(ReferenceEquals(ctx, returned), "Should return the same context object");
    }

    // ============================
    // DefenseValidatorRegistry
    // ============================

    [EmpireTest("Registry")]
    public static void DefenseValidator_RegistrationContract()
    {
        var c = new TestDefenseValidator { Allow = true };
        AssertRegistrationContract(c, DefenseValidatorRegistry.Register, DefenseValidatorRegistry.Unregister,
            () => CountDuring(() => c.InvokeCount, () => DefenseValidatorRegistry.CanDefend(null, null)));
        AssertDispatchIsolatesThrowing(new ThrowingDefenseValidator(), DefenseValidatorRegistry.Register, DefenseValidatorRegistry.Unregister,
            () => DefenseValidatorRegistry.CanDefend(null, null));
    }

    [EmpireTest("Registry")]
    public static void DefenseValidator_Verdicts()
    {
        TestAssert.IsTrue(DefenseValidatorRegistry.CanDefend(null, null), "No validators means default allow");

        WithRegistered(new TestDefenseValidator { Allow = true }, DefenseValidatorRegistry.Register, DefenseValidatorRegistry.Unregister,
            () => TestAssert.IsTrue(DefenseValidatorRegistry.CanDefend(null, null), "An allowing validator should allow"));

        WithRegistered(new TestDefenseValidator { Allow = false }, DefenseValidatorRegistry.Register, DefenseValidatorRegistry.Unregister,
            () => TestAssert.IsFalse(DefenseValidatorRegistry.CanDefend(null, null), "A rejecting validator should reject"));

        TestAssert.IsTrue(DefenseValidatorRegistry.CanDefend(null, null), "After unregister, should allow again");
    }

    // ============================
    // TaxTickRegistry
    // ============================

    [EmpireDestructiveTest("Registry")]
    public static void TaxTick_RegistrationContract()
    {
        var c = new TestTaxTicker();
        AssertRegistrationContract(c, TaxTickRegistry.Register, TaxTickRegistry.Unregister,
            () => CountDuring(() => c.PreTaxCount, () => TaxTickRegistry.InvokePreTaxResolution(null)));
        AssertDispatchIsolatesThrowing(new ThrowingTaxTicker(), TaxTickRegistry.Register, TaxTickRegistry.Unregister,
            () => TaxTickRegistry.InvokePreTaxResolution(null),
            () => TaxTickRegistry.InvokePostTaxResolution(null));
    }

    [EmpireDestructiveTest("Registry")]
    public static void TaxTick_Invoke_ReachesPreAndPostTax()
    {
        var c = new TestTaxTicker();
        WithRegistered(c, TaxTickRegistry.Register, TaxTickRegistry.Unregister, () =>
        {
            TaxTickRegistry.InvokePreTaxResolution(null);
            TestAssert.AreEqual(1, c.PreTaxCount, "PreTaxResolution");
            TestAssert.AreEqual(0, c.PostTaxCount, "PreTax dispatch should not reach PostTaxResolution");
            TaxTickRegistry.InvokePostTaxResolution(null);
            TestAssert.AreEqual(1, c.PostTaxCount, "PostTaxResolution");
        });
    }

    // ============================
    // SquadAssignmentRegistry
    // ============================

    [EmpireTest("Registry")]
    public static void SquadAssignment_RegistrationContract()
    {
        var c = new TestSquadValidator { Allow = true };
        AssertRegistrationContract(c, SquadAssignmentRegistry.Register, SquadAssignmentRegistry.Unregister,
            () => CountDuring(() => c.InvokeCount, () => SquadAssignmentRegistry.CanAssign(null, null, out _)));
        AssertDispatchIsolatesThrowing(new ThrowingSquadValidator(), SquadAssignmentRegistry.Register, SquadAssignmentRegistry.Unregister,
            () => SquadAssignmentRegistry.CanAssign(null, null, out _));
    }

    [EmpireTest("Registry")]
    public static void SquadAssignment_Verdicts()
    {
        bool result = SquadAssignmentRegistry.CanAssign(null, null, out string reason);
        TestAssert.IsTrue(result, "No validators means default allow");
        TestAssert.IsNull(reason);

        WithRegistered(new TestSquadValidator { Allow = true }, SquadAssignmentRegistry.Register, SquadAssignmentRegistry.Unregister, () =>
        {
            TestAssert.IsTrue(SquadAssignmentRegistry.CanAssign(null, null, out string allowReason));
            TestAssert.IsNull(allowReason, "Reason should be null on allow");
        });

        WithRegistered(new TestSquadValidator { Allow = false, RejectReason = "too expensive" }, SquadAssignmentRegistry.Register, SquadAssignmentRegistry.Unregister, () =>
        {
            TestAssert.IsFalse(SquadAssignmentRegistry.CanAssign(null, null, out string rejectReason));
            TestAssert.AreEqual((object)"too expensive", (object)rejectReason);
        });
    }

    // ============================
    // MainTableRegistry
    // ============================

    [EmpireTest("Registry")]
    public static void MainTable_RegistrationContract()
    {
        var tab = new TestMainTab();
        AssertRegistrationContract(tab, MainTableRegistry.Register, MainTableRegistry.Unregister,
            () => Occurrences(MainTableRegistry.Tabs, tab));
        AssertRegistrationContract(tab, MainTableRegistry.Register, MainTableRegistry.Unregister,
            () => CountDuring(() => tab.PostCloseCount, MainTableRegistry.InvokePostCloseWindow));
        AssertDispatchIsolatesThrowing(new ThrowingMainTab(), MainTableRegistry.Register, MainTableRegistry.Unregister,
            MainTableRegistry.InvokePostCloseWindow);
    }

    // ============================
    // BuildingFilterRegistry
    // ============================

    [EmpireTest("Registry")]
    public static void BuildingFilter_RegistrationContract()
    {
        var filter = new BuildingFilter("Test", null, def => true);
        AssertRegistrationContract(filter, BuildingFilterRegistry.Register, BuildingFilterRegistry.Unregister,
            () => Occurrences(BuildingFilterRegistry.Filters, filter));
    }

    // ============================
    // AnimalPickerFilterRegistry
    // ============================

    /// <summary>Returns a fixed verdict regardless of input, so tests don't need a real PawnKindDef.</summary>
    private class StubAnimalPickerFilter : IAnimalPickerFilter
    {
        public bool Allow = true;
        public bool IsAnimalAllowed(PawnKindDef animal) => Allow;
    }

    [EmpireTest("Registry")]
    public static void AnimalPickerFilter_RegistrationContract()
    {
        StubAnimalPickerFilter filter = new();
        AssertRegistrationContract(filter, f => EmpireRegistry.Register(f), f => EmpireRegistry.Unregister(f),
            () => Occurrences(AnimalPickerFilterRegistry.Filters, filter));
    }

    [EmpireTest("Registry")]
    public static void AnimalPickerFilter_DisallowingFilter_Gates()
    {
        // AND semantics: one disallowing filter gates the kind regardless of any others.
        bool baseline = AnimalPickerFilterRegistry.IsAllowed(null);
        StubAnimalPickerFilter filter = new() { Allow = false };
        WithRegistered(filter, f => EmpireRegistry.Register(f), f => EmpireRegistry.Unregister(f),
            () => TestAssert.IsFalse(AnimalPickerFilterRegistry.IsAllowed(null), "A disallowing filter should gate the kind"));

        TestAssert.AreEqual(baseline, AnimalPickerFilterRegistry.IsAllowed(null),
            "Unregistering should restore the prior verdict");
    }

    // ============================
    // EmpireCacheUtil (external invalidators)
    // ============================

    // NOTE: EmpireCacheUtil.InvalidateAll() calls EmpireRegistry.ClearAll(), which wipes every
    // live registry (FactionFC listeners, built-in validators, submod hooks) that only
    // re-registers on save load. These CacheInvalidator tests are therefore DESTRUCTIVE: a
    // pass still leaves the session's registries gutted until a reload.
    [EmpireDestructiveTest("Registry")]
    public static void CacheInvalidator_Register_InvokesOnInvalidateAll()
    {
        int count = 0;
        EmpireCacheUtil.RegisterCacheInvalidator("_test", () => count++);
        try
        {
            EmpireCacheUtil.InvalidateAll();
            TestAssert.AreEqual(1, count);
        }
        finally { EmpireCacheUtil.UnregisterCacheInvalidator("_test"); }
    }

    [EmpireDestructiveTest("Registry")]
    public static void CacheInvalidator_SurvivesInvalidateAll()
    {
        int count = 0;
        EmpireCacheUtil.RegisterCacheInvalidator("_test", () => count++);
        try
        {
            EmpireCacheUtil.InvalidateAll();
            EmpireCacheUtil.InvalidateAll();
            TestAssert.AreEqual(2, count, "Callback should survive across InvalidateAll calls");
        }
        finally { EmpireCacheUtil.UnregisterCacheInvalidator("_test"); }
    }

    [EmpireDestructiveTest("Registry")]
    public static void CacheInvalidator_DuplicateKey_ReplacesOld()
    {
        int oldCount = 0;
        int newCount = 0;
        EmpireCacheUtil.RegisterCacheInvalidator("_test", () => oldCount++);
        EmpireCacheUtil.RegisterCacheInvalidator("_test", () => newCount++);
        try
        {
            EmpireCacheUtil.InvalidateAll();
            TestAssert.AreEqual(0, oldCount, "Old callback should not fire");
            TestAssert.AreEqual(1, newCount, "New callback should fire");
        }
        finally { EmpireCacheUtil.UnregisterCacheInvalidator("_test"); }
    }

    [EmpireDestructiveTest("Registry")]
    public static void CacheInvalidator_Exception_DoesNotBlockOthers()
    {
        int count = 0;
        EmpireCacheUtil.RegisterCacheInvalidator("_test_bad", () => throw new InvalidOperationException("test"));
        EmpireCacheUtil.RegisterCacheInvalidator("_test_good", () => count++);
        try
        {
            TestAssert.DoesNotThrow(() => EmpireCacheUtil.InvalidateAll());
            TestAssert.AreEqual(1, count, "Good callback should still fire after bad one throws");
        }
        finally
        {
            EmpireCacheUtil.UnregisterCacheInvalidator("_test_bad");
            EmpireCacheUtil.UnregisterCacheInvalidator("_test_good");
        }
    }

    [EmpireDestructiveTest("Registry")]
    public static void CacheInvalidator_Unregister_StopsInvocations()
    {
        int count = 0;
        EmpireCacheUtil.RegisterCacheInvalidator("_test", () => count++);
        EmpireCacheUtil.UnregisterCacheInvalidator("_test");
        EmpireCacheUtil.InvalidateAll();
        TestAssert.AreEqual(0, count, "Callback should not fire after unregister");
    }

    // ============================
    // RaidWeightRegistry
    // ============================

    private class FixedRaidWeightProvider : IRaidWeightProvider
    {
        private readonly float _weight;
        public int InvokeCount;
        public FixedRaidWeightProvider(float weight) { _weight = weight; }
        public float GetSettlementRaidWeight(WorldSettlementFC settlement, RimWorld.Faction attackingFaction)
        {
            InvokeCount++;
            return _weight;
        }
    }

    private class ThrowingRaidWeightProvider : IRaidWeightProvider
    {
        public float GetSettlementRaidWeight(WorldSettlementFC settlement, RimWorld.Faction attackingFaction)
            => throw new InvalidOperationException("test");
    }

    [EmpireTest("Registry")]
    public static void RaidWeight_RegistrationContract()
    {
        var p = new FixedRaidWeightProvider(2.0f);
        AssertRegistrationContract(p, RaidWeightRegistry.Register, RaidWeightRegistry.Unregister,
            () => CountDuring(() => p.InvokeCount, () => RaidWeightRegistry.GetCombinedWeight(null, null)));
        AssertDispatchIsolatesThrowing(new ThrowingRaidWeightProvider(), RaidWeightRegistry.Register, RaidWeightRegistry.Unregister,
            () => RaidWeightRegistry.GetCombinedWeight(null, null));
    }

    [EmpireTest("Registry")]
    public static void RaidWeight_CombinesAsProduct()
    {
        WithRegistered(new FixedRaidWeightProvider(2.5f), RaidWeightRegistry.Register, RaidWeightRegistry.Unregister,
            () => TestAssert.AreEqual(2.5, RaidWeightRegistry.GetCombinedWeight(null, null), 0.0001, "A single provider's weight"));

        WithRegistered(new FixedRaidWeightProvider(2.0f), RaidWeightRegistry.Register, RaidWeightRegistry.Unregister, () =>
        WithRegistered(new FixedRaidWeightProvider(1.5f), RaidWeightRegistry.Register, RaidWeightRegistry.Unregister,
            () => TestAssert.AreEqual(3.0, RaidWeightRegistry.GetCombinedWeight(null, null), 0.0001,
                "Combined weight should be the product (2.0 * 1.5)")));

        WithRegistered(new FixedRaidWeightProvider(0f), RaidWeightRegistry.Register, RaidWeightRegistry.Unregister,
            () => TestAssert.AreEqual(0.0, RaidWeightRegistry.GetCombinedWeight(null, null), 0.0001,
                "Returning 0 should exclude the settlement"));
    }

    // ============================
    // RaidTargetRegistry
    // ============================

    private class StubRaidTarget : IRaidTarget
    {
        // Bare WorldObject sentinel: RaidTargetRegistry skips targets without one (or without a
        // valid Tile), and FindByWorldObject matches by reference.
        private readonly WorldObject _obj = new();
        public WorldObject WorldObject => _obj;
        public string Name => "TestRaidTarget";
        public PlanetTile Tile => new(0);
        public int MilitaryLevel => 1;
        public bool IsUnderAttack { get; set; }
    }

    private class ThrowingRaidTarget : IRaidTarget
    {
        public WorldObject WorldObject => throw new InvalidOperationException("test");
        public string Name => throw new InvalidOperationException("test");
        public PlanetTile Tile => throw new InvalidOperationException("test");
        public int MilitaryLevel => throw new InvalidOperationException("test");
        public bool IsUnderAttack { get => throw new InvalidOperationException("test"); set => throw new InvalidOperationException("test"); }
    }

    [EmpireTest("Registry")]
    public static void RaidTarget_RegistrationContract()
    {
        var t = new StubRaidTarget();
        AssertRegistrationContract(t, RaidTargetRegistry.Register, RaidTargetRegistry.Unregister,
            () => Occurrences<IRaidTarget>(RaidTargetRegistry.Targets, t));
    }

    [EmpireTest("Registry")]
    public static void RaidTarget_FindByWorldObject_NullObj_ReturnsNull()
    {
        // A target that leaves WorldObject at its null default must not match a null lookup.
        WithRegistered(new BareParticipant(), RaidTargetRegistry.Register, RaidTargetRegistry.Unregister,
            () => TestAssert.IsNull(RaidTargetRegistry.FindByWorldObject(null)));
    }

    [EmpireTest("Registry")]
    public static void RaidTarget_FindByWorldObject_Exception_DoesNotCrash()
    {
        var bad = new ThrowingRaidTarget();
        WithRegistered(bad, RaidTargetRegistry.Register, RaidTargetRegistry.Unregister, () =>
        {
            TestAssert.DoesNotThrow(() => RaidTargetRegistry.FindByWorldObject(new WorldObject()));
            TestAssert.IsFalse(RaidTargetRegistry.Targets.Contains(bad), "A target whose identity check throws is skipped");
        });
    }

    // ============================
    // AutoDefenderRegistry
    // ============================

    private class StubAutoDefender : IAutoDefender
    {
        public int _militaryLevel;
        public int _range = 999;
        public bool _canAutoDefend = true;

        // Bare WorldObject sentinel: AutoDefenderRegistry skips defenders without one.
        private readonly WorldObject _obj = new();
        public WorldObject WorldObject => _obj;
        public int MilitaryLevel => _militaryLevel;
        public int Range => _range;
        public bool CanAutoDefend => _canAutoDefend;
    }

    [EmpireTest("Registry")]
    public static void AutoDefender_RegistrationContract()
    {
        var d = new StubAutoDefender();
        AssertRegistrationContract(d, AutoDefenderRegistry.Register, AutoDefenderRegistry.Unregister,
            () => Occurrences<IAutoDefender>(AutoDefenderRegistry.Defenders, d));
    }

    [EmpireTest("Registry")]
    public static void AutoDefender_FindByWorldObject_NullObj_ReturnsNull()
    {
        // Explicit guard in FindByWorldObject: null obj returns null short-circuit.
        TestAssert.IsNull(AutoDefenderRegistry.FindByWorldObject(null));
    }

    [EmpireTest("Registry")]
    public static void AutoDefender_FindBestDefender_IneligibleDefenders_Skipped()
    {
        // The CanAutoDefend == false and below-minimum-level branches short-circuit before any
        // tile lookup, so this is safe to test without real world geometry.
        WithRegistered(new StubAutoDefender { _militaryLevel = 10, _canAutoDefend = false }, AutoDefenderRegistry.Register, AutoDefenderRegistry.Unregister,
            () => TestAssert.IsNull(AutoDefenderRegistry.FindBestDefender(RimWorld.Planet.PlanetTile.Invalid, 0),
                "Disabled defender should not be selected"));

        WithRegistered(new StubAutoDefender { _militaryLevel = 3, _canAutoDefend = true }, AutoDefenderRegistry.Register, AutoDefenderRegistry.Unregister,
            () => TestAssert.IsNull(AutoDefenderRegistry.FindBestDefender(RimWorld.Planet.PlanetTile.Invalid, 5),
                "Defender below minMilitaryLevel should not be selected"));
    }

    // ============================
    // EmpireRegistry (unified facade)
    // ============================

    /// <summary>
    /// Test double implementing multiple unrelated interfaces so we can verify
    /// <see cref="EmpireRegistry.Register"/> routes a single registration to every
    /// matching domain.
    /// </summary>
    private class MultiInterfaceParticipant : ISettlementListener, ITaxTickParticipant, IDefenseValidator
    {
        public int SettlementCreated;
        public int PreTax;
        public int CanDefendCalls;

        // ISettlementListener
        public void OnSettlementCreated(WorldSettlementFC s) => SettlementCreated++;

        // ITaxTickParticipant
        public void PreTaxResolution(FactionFC f) => PreTax++;

        // IDefenseValidator
        public bool CanDefend(WorldSettlementFC defender, WorldSettlementFC target)
        {
            CanDefendCalls++;
            return true;
        }
    }

    /// <summary>
    /// Class that implements zero registered interfaces, used to verify the
    /// "matched no registry" warning path.
    /// </summary>
    private class NonParticipant { }

    [EmpireDestructiveTest("Registry")]
    public static void EmpireRegistry_Register_RoutesToAllMatchingDomains()
    {
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        MultiInterfaceParticipant p = new();
        EmpireRegistry.Register(p);
        try
        {
            // ISettlementListener routing
            LifecycleRegistry.InvokeOnSettlementCreated(settlement);
            TestAssert.AreEqual(1, p.SettlementCreated, "Should fire via LifecycleRegistry");

            // ITaxTickParticipant routing
            TaxTickRegistry.InvokePreTaxResolution(null);
            TestAssert.AreEqual(1, p.PreTax, "Should fire via TaxTickRegistry");

            // IDefenseValidator routing
            DefenseValidatorRegistry.CanDefend(null, null);
            TestAssert.AreEqual(1, p.CanDefendCalls, "Should fire via DefenseValidatorRegistry");
        }
        finally { EmpireRegistry.Unregister(p); }
    }

    [EmpireDestructiveTest("Registry")]
    public static void EmpireRegistry_Unregister_RemovesFromAllMatchingDomains()
    {
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        MultiInterfaceParticipant p = new();
        EmpireRegistry.Register(p);
        EmpireRegistry.Unregister(p);

        LifecycleRegistry.InvokeOnSettlementCreated(settlement);
        TaxTickRegistry.InvokePreTaxResolution(null);
        DefenseValidatorRegistry.CanDefend(null, null);

        TestAssert.AreEqual(0, p.SettlementCreated, "Settlement hook should be removed");
        TestAssert.AreEqual(0, p.PreTax, "Tax hook should be removed");
        TestAssert.AreEqual(0, p.CanDefendCalls, "Defense validator should be removed");
    }

    [EmpireDestructiveTest("Registry")]
    public static void EmpireRegistry_ClearAll_ClearsEveryFacadeManagedRegistry()
    {
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();
        MultiInterfaceParticipant p = new();
        EmpireRegistry.Register(p);
        EmpireRegistry.ClearAll();

        // After ClearAll, none of the domain registries should still hold p.
        // We can't easily enumerate every list, but we can verify that none of
        // p's hooks fire after ClearAll.
        LifecycleRegistry.InvokeOnSettlementCreated(settlement);
        TaxTickRegistry.InvokePreTaxResolution(null);
        DefenseValidatorRegistry.CanDefend(null, null);

        TestAssert.AreEqual(0, p.SettlementCreated, "Settlement hook cleared");
        TestAssert.AreEqual(0, p.PreTax, "Tax hook cleared");
        TestAssert.AreEqual(0, p.CanDefendCalls, "Defense validator cleared");
    }

    [EmpireDestructiveTest("Registry")]
    public static void EmpireRegistry_RegisterPersistent_SurvivesClearAll()
    {
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();
        MultiInterfaceParticipant p = new();
        EmpireRegistry.RegisterPersistent(p);
        try
        {
            EmpireRegistry.ClearAll();
            LifecycleRegistry.InvokeOnSettlementCreated(settlement);
            TestAssert.AreEqual(1, p.SettlementCreated, "Persistent participant re-registered after ClearAll");
        }
        finally { EmpireRegistry.Unregister(p); }

        EmpireRegistry.ClearAll();
        LifecycleRegistry.InvokeOnSettlementCreated(settlement);
        TestAssert.AreEqual(1, p.SettlementCreated, "Unregister drops the persistent hold");
    }

    [EmpireTest("Registry")]
    public static void EmpireRegistry_Register_NonParticipant_NoOp()
    {
        // Passing an object that implements zero registered interfaces should
        // log a warning but not throw. We don't capture the log here; just
        // verify no exception escapes and no domain registry is affected.
        NonParticipant junk = new();
        TestAssert.DoesNotThrow(() => EmpireRegistry.Register(junk));
        TestAssert.DoesNotThrow(() => EmpireRegistry.Unregister(junk));
    }

    [EmpireTest("Registry")]
    public static void EmpireRegistry_Register_Null_NoOp()
    {
        TestAssert.DoesNotThrow(() => EmpireRegistry.Register(null));
        TestAssert.DoesNotThrow(() => EmpireRegistry.Unregister(null));
    }

    // ============================
    // RegistryDispatch re-entrancy
    // ============================
    // Exercises RegistryDispatch against a private RegistryList so a participant can
    // unregister itself mid-callback without fanning out to live listeners or the real
    // settlement. Before the snapshot fix, the self-removal invalidated the foreach
    // enumerator and threw InvalidOperationException out past the per-item try/catch.

    private class SelfUnregisteringParticipant
    {
        public RegistryList<SelfUnregisteringParticipant> Owner;
        public bool UnregisterSelf;
        public int InvokeCount;

        public void Fire()
        {
            InvokeCount++;
            if (UnregisterSelf) Owner.Unregister(this);
        }
    }

    [EmpireTest("Registry")]
    public static void RegistryDispatch_Each_SelfUnregisterMidCallback_NoThrowRemainingRun()
    {
        var list = new RegistryList<SelfUnregisteringParticipant>();
        var a = new SelfUnregisteringParticipant { Owner = list, UnregisterSelf = true };
        var b = new SelfUnregisteringParticipant { Owner = list };
        var c = new SelfUnregisteringParticipant { Owner = list };
        list.Register(a);
        list.Register(b);
        list.Register(c);

        TestAssert.DoesNotThrow(() =>
            RegistryDispatch.Each(list.Items, p => p.Fire(), "Fire"),
            "self-unregister mid-callback must not invalidate the iteration");

        TestAssert.AreEqual(1, a.InvokeCount, "self-unregistering item still fires once");
        TestAssert.AreEqual(1, b.InvokeCount, "later items still run after a mid-callback unregister");
        TestAssert.AreEqual(1, c.InvokeCount, "later items still run after a mid-callback unregister");
        TestAssert.AreEqual(2, list.Count, "the self-unregister still took effect");
    }

    [EmpireTest("Registry")]
    public static void RegistryDispatch_All_SelfUnregisterMidPredicate_NoThrow()
    {
        var list = new RegistryList<SelfUnregisteringParticipant>();
        var a = new SelfUnregisteringParticipant { Owner = list, UnregisterSelf = true };
        var b = new SelfUnregisteringParticipant { Owner = list };
        list.Register(a);
        list.Register(b);

        bool result = true;
        TestAssert.DoesNotThrow(() =>
            result = RegistryDispatch.All(list.Items, p => { p.Fire(); return true; }, "Fire"),
            "self-unregister mid-predicate must not invalidate the iteration");

        TestAssert.IsTrue(result, "no predicate returned false");
        TestAssert.AreEqual(1, a.InvokeCount);
        TestAssert.AreEqual(1, b.InvokeCount, "later validators still run after a mid-callback unregister");
    }

    // ============================
    // Default interface members
    // ============================
    // Every extensibility interface member has a default body that leaves the game unaffected.
    // BareParticipant implements every interface and overrides nothing, so it exercises the
    // defaults alone. A new interface or member must be added here too.

    internal class BareParticipant :
        IDailyAccrualParticipant, ISilverPaymentModifier, ITaxDeliveryInterceptor,
        IMercAutoTendProvider, IMercenarySquadListener, IMilitaryOperationListener, IResearchListener,
        ISettlementFoundingValidator, ISettlementListener, ISettlementPostLoadInit, ITaxTickParticipant,
        IAnimalPickerFilter, IBattleModifier, IDefenseValidator, IFactionPowerModifier,
        ISettlementPowerModifier, ISquadAssignmentValidator, ISquadPowerModifier, IPsycastSystemProvider,
        IProfitContributor, IResourceProductionModifier, IStatModifierProvider, ITitheBudgetModifier,
        IAutoDefender, IRaidTarget, IRaidWeightProvider, IThreatScalingContributor, IRoadNodeProvider,
        IBuildingDetailSection, ISquadInspectionSection, ICodexDynamicProvider, IFoundingCompanionWindow,
        IMainTabWindowOverview, ISettlementWindowOverview, IMilitaryTabEntry, ISettlementWindowButton
    { }

    [EmpireTest("Registry")]
    public static void DefaultMembers_BareParticipant_EveryMemberIsInert()
    {
        var bare = new BareParticipant();
        var additiveStat = new FCStatDef { aggregation = FCStatAggregation.Additive };
        var multStat = new FCStatDef { aggregation = FCStatAggregation.Multiplicative };
        var power = new SquadPower(3, 0.8);
        var selections = new List<SavedPsycast>();
        int silver = 100;
        string reason = "unset";

        TestAssert.DoesNotThrow(() =>
        {
            /* Economy */
            ((IDailyAccrualParticipant)bare).PostDailyAccrual(null);
            ((ISilverPaymentModifier)bare).ModifyPayment(null);
            ((ITaxDeliveryInterceptor)bare).OnTaxEventCreated(null);
            TestAssert.IsFalse(((ITaxDeliveryInterceptor)bare).TryDeliverGoods(null), "TryDeliverGoods");

            /* Lifecycle */
            IMercAutoTendProvider tend = bare;
            TestAssert.IsNull(tend.ProvideTendingDoctor(null, null), "ProvideTendingDoctor");
            TestAssert.AreEqual(ThingDefOf.MedicineIndustrial, tend.OverrideTendingMedicine(null, null, ThingDefOf.MedicineIndustrial), "OverrideTendingMedicine passes through");

            IMercenarySquadListener squads = bare;
            squads.OnMercenaryDeath(null);
            squads.OnSquadHired(null);
            squads.OnSquadDismissed(null);
            squads.OnSquadUpgraded(null);

            IMilitaryOperationListener ops = bare;
            ops.OnOperationCreated(null);
            ops.OnOperationResolved(null);
            ops.OnBattleResolved(null, true, null);

            ((IResearchListener)bare).OnResearchCompleted(null);

            ISettlementFoundingValidator founding = bare;
            TestAssert.IsTrue(founding.CanFoundSettlement(PlanetTile.Invalid, null, out reason, 1f), "CanFoundSettlement");
            TestAssert.IsNull(reason, "CanFoundSettlement reason");
            TestAssert.IsNull(founding.GetAdditionalCostDescription(PlanetTile.Invalid, null, 1f), "GetAdditionalCostDescription");
            founding.OnSettlementFounded(PlanetTile.Invalid, null, 1f);

            ISettlementListener settlements = bare;
            settlements.OnSettlementCreated(null);
            settlements.OnSettlementRemoved(null);
            settlements.OnSettlementUpgraded(null, 1, 2);
            settlements.OnSettlementTypeChanged(null, null, null);
            settlements.OnBuildingConstructed(null, null, 0);
            settlements.OnBuildingDeconstructed(null, null, 0);

            ((ISettlementPostLoadInit)bare).PostSettlementLoadInit(null);

            ITaxTickParticipant tax = bare;
            tax.PreTaxResolution(null);
            tax.PostTaxResolution(null);
            tax.PreSettlementCreateTax(null);
            tax.PostSettlementCreateTax(null, ref silver, null);
            TestAssert.AreEqual(100, silver, "PostSettlementCreateTax leaves silver untouched");

            /* Military */
            TestAssert.IsTrue(((IAnimalPickerFilter)bare).IsAnimalAllowed(null), "IsAnimalAllowed");
            ((IBattleModifier)bare).ModifyForce(null, null, true);
            TestAssert.IsTrue(((IDefenseValidator)bare).CanDefend(null, null), "CanDefend");
            ((IFactionPowerModifier)bare).ModifyFactionPower(null, null);
            ((ISettlementPowerModifier)bare).ModifySettlementPower(null, null);

            reason = "unset";
            TestAssert.IsTrue(((ISquadAssignmentValidator)bare).CanAssign(null, null, out reason), "CanAssign");
            TestAssert.IsNull(reason, "CanAssign reason");

            ISquadPowerModifier squadPower = bare;
            TestAssert.AreEqual(0, squadPower.Priority, "ISquadPowerModifier.Priority");
            SquadPower modified = squadPower.ModifyPower(null, power);
            TestAssert.AreEqual(power.militaryLevel, modified.militaryLevel, message: "ModifyPower level passes through");
            TestAssert.AreEqual(power.militaryEfficiency, modified.militaryEfficiency, message: "ModifyPower efficiency passes through");

            IPsycastSystemProvider psy = bare;
            TestAssert.IsNull(psy.Key, "Key");
            TestAssert.IsNull(psy.Label, "Label");
            TestAssert.IsFalse(psy.IsActive, "IsActive");
            TestAssert.AreEqual(0, psy.Priority, "IPsycastSystemProvider.Priority");
            TestAssert.AreEqual(0, psy.MaxPsylinkLevel, "MaxPsylinkLevel");
            TestAssert.IsFalse(psy.SupportsExplicitSelection, "SupportsExplicitSelection");
            psy.OpenEditor(null, null);
            TestAssert.IsFalse(psy.TryGetDisplay(default, out PsycastPickEntry display), "TryGetDisplay");
            TestAssert.IsNull(display, "TryGetDisplay display");
            psy.ApplyPsylink(null, 1);
            TestAssert.AreEqual(0.0, psy.PsylinkCost(3), message: "PsylinkCost");
            psy.GrantPsycast(null, default);
            TestAssert.IsTrue(ReferenceEquals(selections, psy.ClampSelectionsToBudget(selections, 1)), "ClampSelectionsToBudget passes through");
            TestAssert.IsFalse(psy.TryGetPointBudget(null, out int spent, out int budget), "TryGetPointBudget");
            TestAssert.AreEqual(0, spent, "TryGetPointBudget spent");
            TestAssert.AreEqual(0, budget, "TryGetPointBudget budget");
            psy.ReconcilePsycasts(null, null);

            /* Production */
            IProfitContributor profit = bare;
            TestAssert.AreEqual(0.0, profit.GetDailyUpkeepContribution(), message: "GetDailyUpkeepContribution");
            TestAssert.IsNull(profit.GetDailyUpkeepContributionDesc(), "GetDailyUpkeepContributionDesc");
            TestAssert.AreEqual(0.0, profit.GetDailyIncomeContribution(), message: "GetDailyIncomeContribution");
            TestAssert.IsNull(profit.GetDailyIncomeContributionDesc(), "GetDailyIncomeContributionDesc");

            IResourceProductionModifier production = bare;
            TestAssert.AreEqual(0.0, production.GetResourceAdditiveModifier(null), message: "GetResourceAdditiveModifier");
            TestAssert.AreEqual(1.0, production.GetResourceMultiplierModifier(null), message: "GetResourceMultiplierModifier");
            TestAssert.IsNull(production.GetResourceAdditiveDesc(null), "GetResourceAdditiveDesc");
            TestAssert.IsNull(production.GetResourceMultiplierDesc(null), "GetResourceMultiplierDesc");

            IStatModifierProvider stats = bare;
            TestAssert.AreEqual(0.0, stats.GetStatModifier(additiveStat), message: "GetStatModifier additive identity");
            TestAssert.AreEqual(1.0, stats.GetStatModifier(multStat), message: "GetStatModifier multiplicative identity");
            TestAssert.IsNull(stats.GetStatModifierDesc(additiveStat), "GetStatModifierDesc");

            ITitheBudgetModifier tithe = bare;
            TestAssert.AreEqual(0.0, tithe.GetDailyExternalTitheBudget(null), message: "GetDailyExternalTitheBudget");
            TestAssert.IsNull(tithe.GetExternalTitheBudgetDesc(null), "GetExternalTitheBudgetDesc");

            /* Raid */
            IAutoDefender defender = bare;
            TestAssert.IsNull(defender.WorldObject, "IAutoDefender.WorldObject");
            TestAssert.AreEqual(0, defender.MilitaryLevel, "IAutoDefender.MilitaryLevel");
            TestAssert.AreEqual(0, defender.Range, "Range");
            TestAssert.IsFalse(defender.CanAutoDefend, "CanAutoDefend");
            TestAssert.IsNull(defender.CreateDefendingForce(), "CreateDefendingForce");
            defender.OnDefensePledged(null);
            defender.OnDefenseStarted(null);
            defender.OnDefenseComplete(true, null);
            defender.OnDefenseReplaced();
            TestAssert.IsNull(defender.GetDefendingPawns(), "GetDefendingPawns");
            defender.ReturnDefendingPawns(null);

            IRaidTarget target = bare;
            TestAssert.IsNull(target.WorldObject, "IRaidTarget.WorldObject");
            TestAssert.IsNull(target.Name, "IRaidTarget.Name");
            TestAssert.IsFalse(target.Tile.Valid, "IRaidTarget.Tile");
            TestAssert.AreEqual(0, target.MilitaryLevel, "IRaidTarget.MilitaryLevel");
            target.IsUnderAttack = true;
            TestAssert.IsFalse(target.IsUnderAttack, "IRaidTarget.IsUnderAttack");
            target.OnRaidWon(null);
            target.OnRaidLost(null);

            TestAssert.AreEqual(1.0, ((IRaidWeightProvider)bare).GetSettlementRaidWeight(null, null), message: "GetSettlementRaidWeight");

            IThreatScalingContributor threat = bare;
            TestAssert.AreEqual(0.0, threat.GetAdditiveContribution(null), message: "GetAdditiveContribution");
            TestAssert.AreEqual(1.0, threat.GetMultiplicativeContribution(null), message: "GetMultiplicativeContribution");

            /* Roads */
            TestAssert.IsFalse(((IRoadNodeProvider)bare).GetRoadNodeTiles().Any(), "GetRoadNodeTiles");

            /* Windows */
            IBuildingDetailSection buildingSection = bare;
            TestAssert.IsNull(buildingSection.SectionLabel, "IBuildingDetailSection.SectionLabel");
            TestAssert.AreEqual(0.0, buildingSection.GetSectionHeight(null, 100f), message: "IBuildingDetailSection.GetSectionHeight");
            buildingSection.DrawSection(null, Rect.zero);
            TestAssert.IsNull(buildingSection.GetCardDescription(null), "GetCardDescription");

            ISquadInspectionSection squadSection = bare;
            TestAssert.IsNull(squadSection.SectionLabel, "ISquadInspectionSection.SectionLabel");
            TestAssert.AreEqual(0.0, squadSection.GetSectionHeight(null, 100f), message: "ISquadInspectionSection.GetSectionHeight");
            squadSection.DrawSection(null, Rect.zero);
            TestAssert.AreEqual(0, squadSection.Order, "Order");

            TestAssert.IsNull(((ICodexDynamicProvider)bare).GetDynamicContent(null), "GetDynamicContent");
            TestAssert.AreEqual(0, ((IFoundingCompanionWindow)bare).CompanionOrder, "CompanionOrder");

            IMainTabWindowOverview mainTab = bare;
            mainTab.PreOpenWindow(null);
            mainTab.OnTabSwitch();
            mainTab.DrawOverviewTab(Rect.zero);
            mainTab.PostCloseWindow();
            TestAssert.IsNull(mainTab.TabName(), "TabName");

            ISettlementWindowOverview settlementTab = bare;
            settlementTab.PreOpenWindow(null);
            settlementTab.OnTabSwitch();
            settlementTab.DrawOverviewTab(Rect.zero);
            settlementTab.PostCloseWindow();
            TestAssert.IsNull(settlementTab.OverviewTabName(), "OverviewTabName");
            TestAssert.IsFalse(settlementTab.ShouldShowOverviewTab(null), "ShouldShowOverviewTab");

            IMilitaryTabEntry tabEntry = bare;
            TestAssert.IsNull(tabEntry.WorldObject, "IMilitaryTabEntry.WorldObject");
            TestAssert.IsNull(tabEntry.Name, "IMilitaryTabEntry.Name");
            TestAssert.AreEqual(0, tabEntry.MilitaryLevel, "IMilitaryTabEntry.MilitaryLevel");
            tabEntry.AutoDefend = true;
            TestAssert.IsFalse(tabEntry.AutoDefend, "AutoDefend");
            TestAssert.IsFalse(tabEntry.IsUnderAttack, "IMilitaryTabEntry.IsUnderAttack");
            TestAssert.IsFalse(tabEntry.IsBusy, "IsBusy");
            TestAssert.IsNull(tabEntry.StatusLabel, "StatusLabel");
            TestAssert.IsTrue(tabEntry.AccentColor == Color.white, "AccentColor");

            ISettlementWindowButton button = bare;
            TestAssert.IsNull(button.Label(null), "Label");
            button.OnClick(null);
            TestAssert.IsTrue(button.IsEnabled(null), "IsEnabled");
            TestAssert.IsFalse(button.IsVisible(null), "IsVisible");
        }, "default interface members must not throw");
    }
}
