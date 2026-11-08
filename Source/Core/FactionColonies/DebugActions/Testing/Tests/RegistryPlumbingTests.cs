namespace FactionColonies;

/* Tests for the registry plumbing: RegistryList's change counter and the sorted views built on it,
   the table-driven EmpireRegistry routing, and the exception isolation of the animal-picker and
   psycast-system registries. Every test removes what it registers, so live registries end as they
   started. Category "Registry". */
public static class RegistryPlumbingTests
{
    private static int Occurrences<T>(IEnumerable<T> items, object item) where T : class
        => items.Count(x => ReferenceEquals(x, item));

    /* RegistryList.Version */

    private class Item { }

    [EmpireTest("Registry", GameState = TestGameState.Any)]
    public static void RegistryList_Version_BumpsOnlyOnRealChanges()
    {
        RegistryList<Item> list = new();
        Item a = new();
        Item b = new();
        int v = list.Version;

        list.Register(a);
        TestAssert.AreEqual(v + 1, list.Version, "Adding an item bumps the version");
        list.Register(a);
        TestAssert.AreEqual(v + 1, list.Version, "A duplicate registration changes nothing");
        list.Register(null);
        TestAssert.AreEqual(v + 1, list.Version, "A null registration changes nothing");
        list.Unregister(b);
        TestAssert.AreEqual(v + 1, list.Version, "Removing an absent item changes nothing");
        list.Unregister(null);
        TestAssert.AreEqual(v + 1, list.Version, "Removing null changes nothing");

        list.Register(b);
        list.Unregister(a);
        TestAssert.AreEqual(v + 3, list.Version, "Each add and each removal bumps once");

        list.ClearAll();
        TestAssert.AreEqual(v + 4, list.Version, "ClearAll bumps the version");
        TestAssert.AreEqual(0, list.Count, "ClearAll empties the list");
    }

    /* Sorted views rebuilt from the version */

    private class OrderedSection(int order) : ISquadInspectionSection
    {
        int ISquadInspectionSection.Order => order;
    }

    [EmpireTest("Registry", GameState = TestGameState.Any)]
    public static void SquadInspection_Sections_SortedByOrderThenRegistration()
    {
        OrderedSection late = new(int.MaxValue - 1);
        OrderedSection early = new(int.MinValue + 1);
        OrderedSection lateTie = new(int.MaxValue - 1);
        try
        {
            SquadInspectionRegistry.Register(late);
            IReadOnlyList<ISquadInspectionSection> before = SquadInspectionRegistry.Sections;
            TestAssert.AreEqual(1, Occurrences(before, late), "A registered section shows up");

            SquadInspectionRegistry.Register(early);
            SquadInspectionRegistry.Register(lateTie);
            List<ISquadInspectionSection> mine = SquadInspectionRegistry.Sections
                .Where(s => s == late || s == early || s == lateTie).ToList();
            TestAssert.AreEqual(3, mine.Count, "Registering after a read rebuilds the cached view");
            TestAssert.AreEqual(early, mine[0], "Lowest Order first");
            TestAssert.AreEqual(late, mine[1], "Equal Order keeps registration order");
            TestAssert.AreEqual(lateTie, mine[2], "The later registration of a tie comes second");

            SquadInspectionRegistry.Unregister(early);
            TestAssert.AreEqual(0, Occurrences(SquadInspectionRegistry.Sections, early), "Unregistering rebuilds the cached view");
        }
        finally
        {
            SquadInspectionRegistry.Unregister(late);
            SquadInspectionRegistry.Unregister(early);
            SquadInspectionRegistry.Unregister(lateTie);
        }
        TestAssert.AreEqual(0, Occurrences(SquadInspectionRegistry.Sections, late), "Cleanup removed the test sections");
    }

    /* EmpireRegistry routing table */

    /* Each exposed registry list a facade registration should reach, by name. Lifecycle, raid
     * targets, auto defenders and military-tab entries hide or filter their lists, so they are
     * covered by the dispatch tests instead. */
    private static IEnumerable<(string name, Func<object, int> count)> ExposedLists()
    {
        yield return ("DefenseValidator", p => Occurrences(DefenseValidatorRegistry.Validators, p));
        yield return ("SquadAssignment", p => Occurrences(SquadAssignmentRegistry.Validators, p));
        yield return ("FoundingValidator", p => Occurrences(FoundingValidatorRegistry.Validators, p));
        yield return ("BattleModifier", p => Occurrences(BattleModifierRegistry.BattleModifiers, p));
        yield return ("FactionPowerModifier", p => Occurrences(BattleModifierRegistry.FactionModifiers, p));
        yield return ("SettlementPowerModifier", p => Occurrences(BattleModifierRegistry.SettlementModifiers, p));
        yield return ("SilverPayment", p => Occurrences(SilverPaymentRegistry.Modifiers, p));
        yield return ("SquadPower", p => Occurrences(SquadPowerRegistry.Modifiers, p));
        yield return ("RaidWeight", p => Occurrences(RaidWeightRegistry.Providers, p));
        yield return ("ThreatScaling", p => Occurrences(ThreatScalingRegistry.Contributors, p));
        yield return ("MercAutoTend", p => Occurrences(MercAutoTendRegistry.Providers, p));
        yield return ("TaxTick", p => Occurrences(TaxTickRegistry.Taxers, p));
        yield return ("DailyAccrual", p => Occurrences(DailyAccrualRegistry.Participants, p));
        yield return ("TaxDelivery", p => Occurrences(TaxDeliveryRegistry.Interceptors, p));
        yield return ("MainTable", p => Occurrences(MainTableRegistry.Tabs, p));
        yield return ("SettlementButton", p => Occurrences(SettlementButtonRegistry.Entries, p));
        yield return ("SquadInspection", p => Occurrences(SquadInspectionRegistry.Sections, p));
        yield return ("RoadNodeProvider", p => Occurrences(RoadNodeProviderRegistry.Providers, p));
        yield return ("AnimalPickerFilter", p => Occurrences(AnimalPickerFilterRegistry.Filters, p));
    }

    [EmpireTest("Registry", GameState = TestGameState.Any)]
    public static void EmpireRegistry_RoutesEveryInterfaceOnceAndUnregistersAll()
    {
        RegistryTests.BareParticipant bare = new();
        try
        {
            EmpireRegistry.Register(bare);
            EmpireRegistry.Register(bare);
            foreach ((string name, Func<object, int> count) in ExposedLists())
                TestAssert.AreEqual(1, count(bare), $"{name}: registered exactly once through the facade");
        }
        finally { EmpireRegistry.Unregister(bare); }

        foreach ((string name, Func<object, int> count) in ExposedLists())
            TestAssert.AreEqual(0, count(bare), $"{name}: removed by the facade's Unregister");
    }

    [EmpireTest("Registry", GameState = TestGameState.Any)]
    public static void EmpireRegistry_RoutesBuildingFilterByClass()
    {
        BuildingFilter filter = new("EmpireTest filter", null, _ => true);
        try
        {
            EmpireRegistry.Register(filter);
            TestAssert.AreEqual(1, Occurrences(BuildingFilterRegistry.Filters, filter), "A BuildingFilter is routed by class");
        }
        finally { EmpireRegistry.Unregister(filter); }
        TestAssert.AreEqual(0, Occurrences(BuildingFilterRegistry.Filters, filter), "And unregistered by class");
    }

    /* Animal picker filters fail open */

    private class ThrowingAnimalFilter : IAnimalPickerFilter
    {
        bool IAnimalPickerFilter.IsAnimalAllowed(PawnKindDef animal) => throw new InvalidOperationException("test filter");
    }

    private class RejectingAnimalFilter : IAnimalPickerFilter
    {
        bool IAnimalPickerFilter.IsAnimalAllowed(PawnKindDef animal) => false;
    }

    [EmpireTest("Registry", GameState = TestGameState.Any)]
    public static void AnimalPickerFilter_ThrowingFilterFailsOpen()
    {
        ThrowingAnimalFilter throwing = new();
        RejectingAnimalFilter rejecting = new();
        PawnKindDef kind = new() { defName = "EmpireTest_PickerAnimal" };
        bool liveAllows = AnimalPickerFilterRegistry.IsAllowed(kind);
        try
        {
            AnimalPickerFilterRegistry.Register(throwing);
            bool allowed = false;
            TestAssert.DoesNotThrow(() => allowed = AnimalPickerFilterRegistry.IsAllowed(kind), "A throwing filter must not escape");
            TestAssert.AreEqual(liveAllows, allowed, "A throwing filter counts as allowing the kind");

            AnimalPickerFilterRegistry.Register(rejecting);
            TestAssert.IsFalse(AnimalPickerFilterRegistry.IsAllowed(kind), "A rejecting filter still vetoes after a throwing one");
        }
        finally
        {
            AnimalPickerFilterRegistry.Unregister(throwing);
            AnimalPickerFilterRegistry.Unregister(rejecting);
        }
    }

    /* Psycast system providers */

    private class TestPsycastProvider(string key, int priority, bool active = true) : IPsycastSystemProvider
    {
        public bool throwOnUse;
        string IPsycastSystemProvider.Key => throwOnUse ? throw new InvalidOperationException("test key") : key;
        bool IPsycastSystemProvider.IsActive => throwOnUse ? throw new InvalidOperationException("test active") : active;
        int IPsycastSystemProvider.Priority => priority;
    }

    private static void WithProviders(Action body, params IPsycastSystemProvider[] providers)
    {
        try
        {
            foreach (IPsycastSystemProvider p in providers) PsycastSystemRegistry.Register(p);
            body();
        }
        finally
        {
            foreach (IPsycastSystemProvider p in providers) PsycastSystemRegistry.Unregister(p);
        }
    }

    [EmpireTest("Registry", GameState = TestGameState.Any)]
    public static void PsycastActive_HighestPriorityWinsAndTiesKeepRegistrationOrder()
    {
        TestPsycastProvider first = new("empire.test.psy.first", int.MaxValue);
        TestPsycastProvider second = new("empire.test.psy.second", int.MaxValue);
        TestPsycastProvider inactive = new("empire.test.psy.inactive", int.MaxValue, active: false);
        WithProviders(() =>
        {
            TestAssert.AreEqual(first, PsycastSystemRegistry.Active, "Among equal top priorities the first registered wins");
        }, inactive, first, second);
    }

    [EmpireTest("Registry", GameState = TestGameState.Any)]
    public static void PsycastActive_ThrowingProviderIsSkipped()
    {
        TestPsycastProvider broken = new("empire.test.psy.broken", int.MaxValue);
        TestPsycastProvider healthy = new("empire.test.psy.healthy", int.MaxValue - 1);
        WithProviders(() =>
        {
            broken.throwOnUse = true;
            IPsycastSystemProvider active = null;
            TestAssert.DoesNotThrow(() => active = PsycastSystemRegistry.Active, "A throwing provider must not escape Active");
            TestAssert.AreEqual(healthy, active, "The throwing provider is skipped, not picked");

            IPsycastSystemProvider byKey = null;
            TestAssert.DoesNotThrow(() => byKey = PsycastSystemRegistry.ByKey("empire.test.psy.healthy"), "A throwing Key must not escape ByKey");
            TestAssert.AreEqual(healthy, byKey, "ByKey still finds the healthy provider");
            TestAssert.IsNull(PsycastSystemRegistry.ByKey("empire.test.psy.broken"), "A provider whose Key throws is never matched");
        }, broken, healthy);
    }
}
