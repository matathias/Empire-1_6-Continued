using UnityEngine;

namespace FactionColonies;

/* Coverage for the logic behind the main windows and their shared UI helpers: accent colors and row
 * layout, the cached item/stuff picker view, the searchable picker base and its subclasses, the
 * export managers, the race list, unit cost helpers, and the pay-silver slider cap. None of these
 * draw anything; drawing code itself is not exercised here. */
public static class WindowsUiHelperTests
{
    /* AccentUtil.ColorForPowerStatus */

    [EmpireTest("Windows", GameState = TestGameState.Any)]
    public static void ColorForPowerStatus_MapsEachStatus()
    {
        Color amber = new(1f, 0.85f, 0.4f);
        TestAssert.AreEqual(AccentUtil.MilUnderAttack, AccentUtil.ColorForPowerStatus(SettlementPowerStatus.UnderAttack), "UnderAttack color");
        TestAssert.AreEqual(amber, AccentUtil.ColorForPowerStatus(SettlementPowerStatus.AllBusy), "AllBusy color");
        TestAssert.AreEqual(amber, AccentUtil.ColorForPowerStatus(SettlementPowerStatus.Ghost), "Ghost color");
        TestAssert.AreEqual(Color.gray, AccentUtil.ColorForPowerStatus(SettlementPowerStatus.NoMilitary), "NoMilitary color");
        TestAssert.AreEqual(Color.white, AccentUtil.ColorForPowerStatus(SettlementPowerStatus.Squad), "Squad color");
    }

    /* AccentRow layout */

    [EmpireTest("Windows", GameState = TestGameState.Any)]
    public static void AccentRow_SplitsRowIntoTwoLinesRightOfTheStrip()
    {
        AccentRow row = new(new Rect(0f, 46f, 300f, 44f), 1, Color.red);
        TestAssert.AreEqual(1, row.Index, "Index");
        TestAssert.AreEqual(Color.red, row.Accent, "Accent");
        TestAssert.AreEqual(AccentRow.AccentW + 6f, row.ContentX, 0.0001, "ContentX starts past the strip");
        TestAssert.AreEqual(300f - (AccentRow.AccentW + 6f) - 4f, row.ContentW, 0.0001, "ContentW");
        TestAssert.AreEqual(46f, row.TopY, 0.0001, "TopY is the row top");
        TestAssert.AreEqual(22f, row.LineH, 0.0001, "LineH is half the row height");
        TestAssert.AreEqual(68f, row.BotY, 0.0001, "BotY is the row middle");
    }

    /* TopY and BotY follow the row rect, so the content column should too. Every current caller
     * passes x = 0, which hides the difference. */
    [EmpireTest("Windows", GameState = TestGameState.Any)]
    public static void AccentRow_ContentX_FollowsRowX()
    {
        AccentRow row = new(new Rect(20f, 0f, 300f, 44f), 0, Color.red);
        TestAssert.AreEqual(20f + AccentRow.AccentW + 6f, row.ContentX, 0.0001,
            "ContentX should be measured from the row's left edge, like TopY is from its top edge");
        TestAssert.AreEqual(row.Rect.xMax - 4f, row.ContentX + row.ContentW, 0.0001,
            "The content column should end 4px inside the row's right edge");
    }

    /* LoadoutListRowUtil */

    [EmpireTest("Windows", GameState = TestGameState.Any)]
    public static void RemoveButtonRect_RightAlignedAndVerticallyCentered()
    {
        Rect row = new(10f, 100f, 200f, 28f);
        Rect remove = LoadoutListRowUtil.RemoveButtonRect(row, 208f);
        TestAssert.AreEqual(208f, remove.xMax, 0.0001, "Right edge sits at the given edge");
        TestAssert.AreEqual(LoadoutListRowUtil.RemoveButtonSize, remove.width, 0.0001, "Square width");
        TestAssert.AreEqual(LoadoutListRowUtil.RemoveButtonSize, remove.height, 0.0001, "Square height");
        TestAssert.AreEqual(row.center.y, remove.center.y, 0.0001, "Vertically centered in the row");
    }

    /* ThingDefPickerView */

    private static ThingDef FakeDef(string defName, string label) => new() { defName = defName, label = label };

    [EmpireTest("Windows", GameState = TestGameState.Any)]
    public static void ThingDefPickerView_SortsByNameBothWays()
    {
        List<ThingDef> source = [FakeDef("B", "beta"), FakeDef("A", "Alpha"), FakeDef("C", "charlie")];
        ThingDefPickerView view = new();

        List<ThingDef> az = view.Get(source, "");
        TestAssert.AreEqual("A,B,C", string.Join(",", az.Select(d => d.defName)), "Name A-Z, case-insensitive");

        view.SortIndex = 1;
        List<ThingDef> za = view.Get(source, "");
        TestAssert.AreEqual("C,B,A", string.Join(",", za.Select(d => d.defName)), "Name Z-A, case-insensitive");
        TestAssert.AreEqual(3, source.Count, "Sorting does not change the source list");
        TestAssert.AreEqual("B", source[0].defName, "Sorting does not reorder the source list");
    }

    [EmpireTest("Windows", GameState = TestGameState.Any)]
    public static void ThingDefPickerView_FiltersCaseInsensitivelyAndFallsBackToDefName()
    {
        List<ThingDef> source = [FakeDef("A", "Alpha"), FakeDef("B", "beta"), FakeDef("DeltaDef", null)];
        ThingDefPickerView view = new();

        List<ThingDef> alp = view.Get(source, "ALP");
        TestAssert.AreEqual(1, alp.Count, "Only Alpha matches 'ALP'");
        TestAssert.AreEqual("A", alp[0].defName, "Alpha matches 'ALP'");

        List<ThingDef> delta = view.Get(source, "delta");
        TestAssert.AreEqual(1, delta.Count, "A def without a label is matched on its defName");
        TestAssert.AreEqual("DeltaDef", delta[0].defName, "defName fallback");

        TestAssert.IsEmpty(view.Get(source, "zzz"), "No match gives an empty view");
    }

    [EmpireTest("Windows", GameState = TestGameState.Any)]
    public static void ThingDefPickerView_CachesUntilAnInputChanges()
    {
        List<ThingDef> source = [FakeDef("A", "Alpha"), FakeDef("B", "beta")];
        ThingDefPickerView view = new();

        List<ThingDef> first = view.Get(source, "");
        TestAssert.IsTrue(ReferenceEquals(first, view.Get(source, "")), "Same inputs return the cached view");

        List<ThingDef> searched = view.Get(source, "a");
        TestAssert.IsFalse(ReferenceEquals(first, searched), "A new search term rebuilds the view");

        view.SortIndex = 1;
        List<ThingDef> resorted = view.Get(source, "a");
        TestAssert.IsFalse(ReferenceEquals(searched, resorted), "A new sort index rebuilds the view");

        List<ThingDef> otherSource = [.. source];
        TestAssert.IsFalse(ReferenceEquals(resorted, view.Get(otherSource, "a")), "A different source list rebuilds the view");
    }

    /* The tithe window clears and refills its stuff list in place when a new item is picked, so the
     * cached view must never be the source list itself. */
    [EmpireTest("Windows", GameState = TestGameState.Any)]
    public static void ThingDefPickerView_ViewIsACopyOfTheSource()
    {
        List<ThingDef> source = [FakeDef("A", "Alpha"), FakeDef("B", "beta")];
        ThingDefPickerView view = new();

        List<ThingDef> result = view.Get(source, "");
        TestAssert.IsFalse(ReferenceEquals(source, result), "The view is a separate list, even with no search and the default sort");
        source.Clear();
        TestAssert.AreEqual(2, result.Count, "Clearing the source does not empty an already returned view");
    }

    [EmpireTest("Windows", GameState = TestGameState.Any)]
    public static void ThingDefPickerView_PriceSortWithoutItemUsesBaseMarketValue()
    {
        List<ThingDef> source = [ThingDefOf.Gold, ThingDefOf.Silver, ThingDefOf.Plasteel, ThingDefOf.Steel];
        ThingDefPickerView view = new() { SortIndex = 2 };

        List<ThingDef> low = view.Get(source, "");
        for (int i = 1; i < low.Count; i++)
            TestAssert.LessThanOrEqual(low[i - 1].BaseMarketValue, low[i].BaseMarketValue,
                $"Price Low-High: {low[i - 1].defName} should not cost more than {low[i].defName}");

        view.SortIndex = 3;
        List<ThingDef> high = view.Get(source, "");
        for (int i = 1; i < high.Count; i++)
            TestAssert.LessThanOrEqual(high[i].BaseMarketValue, high[i - 1].BaseMarketValue,
                $"Price High-Low: {high[i].defName} should not cost more than {high[i - 1].defName}");
    }

    [EmpireTest("Windows", GameState = TestGameState.Any)]
    public static void ThingDefPickerView_StuffPriceSortUsesTheItemValueAndRebuildsOnQuality()
    {
        ThingDef item = ThingDefOf.Apparel_Parka;
        List<ThingDef> stuffs = GenStuff.AllowedStuffsFor(item).ToList();
        if (stuffs.Count < 2)
            TestAssert.Fail($"{item.defName} should allow at least two stuffs, found {stuffs.Count}");

        ThingDefPickerView view = new() { SortIndex = 2 };
        List<ThingDef> normal = view.Get(stuffs, "", item, QualityCategory.Normal);
        for (int i = 1; i < normal.Count; i++)
        {
            float prev = CraftUtil.ThingValue(item, normal[i - 1], QualityCategory.Normal);
            float cur = CraftUtil.ThingValue(item, normal[i], QualityCategory.Normal);
            TestAssert.LessThanOrEqual(prev, cur,
                $"Stuff price Low-High: a parka of {normal[i - 1].defName} should not cost more than one of {normal[i].defName}");
        }

        TestAssert.IsTrue(ReferenceEquals(normal, view.Get(stuffs, "", item, QualityCategory.Normal)),
            "Same item and quality return the cached view");
        TestAssert.IsFalse(ReferenceEquals(normal, view.Get(stuffs, "", item, QualityCategory.Legendary)),
            "A new quality rebuilds the view");
        TestAssert.IsFalse(ReferenceEquals(normal, view.Get(stuffs, "", ThingDefOf.Apparel_Tuque, QualityCategory.Legendary)),
            "A new price item rebuilds the view");
    }

    /* FactionCache.HumanlikeRaceKinds (race picker list) */

    [EmpireTest("Windows", GameState = TestGameState.Any)]
    public static void HumanlikeRaceKinds_HumanIsRepresentedByColonistOnly()
    {
        List<PawnKindDef> kinds = FactionCache.HumanlikeRaceKinds;
        TestAssert.IsNotEmpty(kinds, "There is always at least one humanlike race");
        TestAssert.Contains(kinds, PawnKindDefOf.Colonist, "Human must be offered, through the Colonist kind");
        TestAssert.IsFalse(kinds.Any(k => k.race == ThingDefOf.Human && k != PawnKindDefOf.Colonist),
            "No Human kind other than Colonist is listed");
    }

    [EmpireTest("Windows", GameState = TestGameState.Any)]
    public static void HumanlikeRaceKinds_OnePerRaceSortedByLabel()
    {
        List<PawnKindDef> kinds = FactionCache.HumanlikeRaceKinds;
        List<string> labels = kinds.Select(k => (k.race.label ?? k.race.defName).CapitalizeFirst()).ToList();
        TestAssert.AreEqual(labels.Count, labels.Distinct().Count(), "Each race label appears once");
        for (int i = 1; i < labels.Count; i++)
            TestAssert.IsTrue(string.Compare(labels[i - 1], labels[i], StringComparison.OrdinalIgnoreCase) <= 0,
                $"Sorted by race label: '{labels[i - 1]}' should come before '{labels[i]}'");
        TestAssert.IsTrue(kinds.All(k => k.IsHumanLikeRace()), "Only humanlike kinds are listed");
        TestAssert.IsTrue(ReferenceEquals(kinds, FactionCache.HumanlikeRaceKinds), "The list is cached");
    }

    /* MilUnitFC unit cost helpers used by the animal, mount and mech pickers */

    [EmpireTest("Windows", GameState = TestGameState.Any)]
    public static void AnimalAndMechUnitCost_NullIsFree()
    {
        TestAssert.AreEqual(0.0, MilUnitFC.AnimalUnitCost(null), 0.0001, "Null animal kind costs nothing");
        TestAssert.AreEqual(0.0, MilUnitFC.MechUnitCost(null), 0.0001, "Null mech kind costs nothing");
        TestAssert.AreEqual(0.0, MilUnitFC.AnimalUnitCost(new PawnKindDef()), 0.0001, "A kind without a race costs nothing");
    }

    [EmpireTest("Windows", GameState = TestGameState.Any)]
    public static void AnimalUnitCost_IsFlooredMarketValueTimesMultiplier()
    {
        PawnKindDef animal = TestAssert.NotNull(
            DefDatabase<PawnKindDef>.AllDefsListForReading.FirstOrDefault(k => k.race?.race?.Animal == true && k.race.BaseMarketValue > 0),
            "No animal pawn kind with a market value found");
        double expected = Math.Floor(animal.race.BaseMarketValue * FCSettings.militaryAnimalCostMultiplier);
        TestAssert.AreEqual(expected, MilUnitFC.AnimalUnitCost(animal), 0.0001, $"Cost of {animal.defName}");

        double expectedMech = Math.Floor(animal.race.BaseMarketValue * FCSettings.militaryMechCostMultiplier);
        TestAssert.AreEqual(expectedMech, MilUnitFC.MechUnitCost(animal), 0.0001, "Mech cost uses the mech multiplier");
    }

    /* FCWindow_SearchablePicker selection and confirm */

    private sealed class ProbePicker : FCWindow_SearchablePicker<string>
    {
        public readonly List<string> picked = [];

        protected override string Title => "probe";
        protected override string EmptyText => null;
        protected override List<string> Items() => ["a", "b"];
        protected override string SearchText(string item) => item;
        protected override void DrawRow(Rect row, string item) { }
        protected override void Pick(string item) => picked.Add(item);

        public bool ProbeCanConfirm => CanConfirm;
        public bool ProbeIsSelected(string item) => IsSelected(item);
        public void ProbeSelect(string item) => Select(item);
        public void ProbeConfirm() => Confirm();
    }

    [EmpireTest("Windows", GameState = TestGameState.Any)]
    public static void SearchablePicker_ConfirmPicksTheSelection()
    {
        ProbePicker picker = new();
        TestAssert.IsFalse(picker.ProbeCanConfirm, "Nothing selected: Confirm is disabled");
        TestAssert.IsFalse(picker.ProbeIsSelected("a"), "Nothing selected: no row is selected");

        picker.ProbeSelect("b");
        TestAssert.IsTrue(picker.ProbeCanConfirm, "A selection enables Confirm");
        TestAssert.IsTrue(picker.ProbeIsSelected("b"), "The selected row reads as selected");
        TestAssert.IsFalse(picker.ProbeIsSelected("a"), "Other rows do not");

        picker.ProbeConfirm();
        TestAssert.AreEqual(1, picker.picked.Count, "Confirm picks exactly once");
        TestAssert.AreEqual("b", picker.picked[0], "Confirm picks the selected row");
    }

    private sealed class ProbeMountPicker : FCWindow_MountPicker
    {
        public ProbeMountPicker(PawnKindDef initialMount, Action<PawnKindDef> onConfirm)
            : base(initialMount, onConfirm, null) { }

        public bool ProbeCanConfirm => CanConfirm;
        public void ProbeConfirm() => Confirm();
    }

    [EmpireTest("Windows", GameState = TestGameState.Any)]
    public static void MountPicker_StartsFromTheCurrentMount()
    {
        TestAssert.IsFalse(new ProbeMountPicker(null, _ => { }).ProbeCanConfirm,
            "Without a current mount nothing is selected, so Confirm is disabled");

        PawnKindDef mount = TestAssert.NotNull(
            DefDatabase<PawnKindDef>.AllDefsListForReading.FirstOrDefault(k => k.race?.race?.Animal == true),
            "No animal pawn kind found");
        PawnKindDef confirmed = null;
        ProbeMountPicker picker = new(mount, k => confirmed = k);
        TestAssert.IsTrue(picker.ProbeCanConfirm, "The current mount starts selected");
        picker.ProbeConfirm();
        TestAssert.AreEqual(mount, confirmed, "Confirming without a change hands back the current mount");
    }

    /* Dialog_ManageExportsFC<TSaved> */

    private sealed class ProbeUnitExports : Dialog_ManageUnitExportsFC
    {
        public ProbeUnitExports(List<SavedUnitFC> saved) : base(saved) { }

        public List<string> ProbeAll => GetAll().ToList();
        public bool ProbeDegraded(string name) => IsEntryDegraded(name);
        public string ProbeTooltip(string name) => GetDegradedTooltip(name);
    }

    private sealed class ProbeFireSupportExports : Dialog_ManageFireSupportExportsFC
    {
        public ProbeFireSupportExports(List<SavedFireSupportFC> saved) : base(saved) { }

        public string ProbeTooltip(string name) => GetDegradedTooltip(name);
    }

    [EmpireTest("Windows", GameState = TestGameState.Any)]
    public static void ExportManager_ListsNamesAndFlagsDegradedEntries()
    {
        List<SavedUnitFC> saved =
        [
            new() { name = "Clean", isDegraded = false, missingDefs = [] },
            new() { name = "Broken", isDegraded = true, missingDefs = ["MissingGunDef", "MissingHatDef"] },
        ];
        ProbeUnitExports dialog = new(saved);

        TestAssert.AreEqual("Clean,Broken", string.Join(",", dialog.ProbeAll), "Lists every saved name, in order");
        TestAssert.IsFalse(dialog.ProbeDegraded("Clean"), "A clean template is not degraded");
        TestAssert.IsTrue(dialog.ProbeDegraded("Broken"), "A degraded template is flagged");
        TestAssert.IsFalse(dialog.ProbeDegraded("NoSuchTemplate"), "An unknown name is not degraded");

        TestAssert.IsNull(dialog.ProbeTooltip("Clean"), "No tooltip for a clean template");
        TestAssert.IsNull(dialog.ProbeTooltip("NoSuchTemplate"), "No tooltip for an unknown name");
        string tip = TestAssert.NotNull(dialog.ProbeTooltip("Broken"), "A degraded template has a tooltip");
        TestAssert.IsTrue(tip.Contains("MissingGunDef") && tip.Contains("MissingHatDef"), $"Tooltip names every missing def: {tip}");
        TestAssert.IsTrue(tip.Contains("substitute defaults"), $"Unit tooltip explains that defaults are substituted: {tip}");
    }

    [EmpireTest("Windows", GameState = TestGameState.Any)]
    public static void ExportManager_FireSupportTooltipSaysProjectilesAreDropped()
    {
        List<SavedFireSupportFC> saved = [new() { name = "Barrage", isDegraded = true, missingDefs = ["MissingShellDef"] }];
        string tip = TestAssert.NotNull(new ProbeFireSupportExports(saved).ProbeTooltip("Barrage"), "Degraded fire support has a tooltip");
        TestAssert.IsTrue(tip.Contains("MissingShellDef"), $"Tooltip names the missing def: {tip}");
        TestAssert.IsTrue(tip.Contains("drop missing projectiles"), $"Fire-support tooltip keeps its own import note: {tip}");
    }

    /* SettlementWindowFc.SettlementStillActive */

    [EmpireTest("Windows", GameState = TestGameState.Any)]
    public static void SettlementStillActive_NullIsInactive()
    {
        TestAssert.IsFalse(SettlementWindowFc.SettlementStillActive(null), "A null settlement is never active");
    }

    [EmpireTest("Windows")]
    public static void SettlementStillActive_SettlementOutsideTheFactionIsInactive()
    {
        WorldSettlementFC orphan = new();
        TestAssert.IsFalse(SettlementWindowFc.SettlementStillActive(orphan),
            "A settlement that is not in the faction's settlement list is not active");
    }

    /* FCWindow_Pay_Silver slider cap */

    [EmpireTest("Windows")]
    public static void PaySilver_BaseWindowHasNoCap()
    {
        TestAssert.AreEqual(int.MaxValue, new FCWindow_Pay_Silver(new WorldSettlementFC()).MaxUsefulSilver,
            "The plain pay-silver window does not cap the slider");
    }

    [EmpireTest("Windows")]
    public static void PaySilverLoyalty_CapsAtTheSilverThatFillsLoyalty()
    {
        WorldSettlementFC settlement = new();
        FCWindow_Pay_Silver_Loyalty window = new(settlement);

        settlement.loyalty = 40;
        TestAssert.AreEqual(6000, window.MaxUsefulSilver, "60 missing loyalty at 100 silver each");
        settlement.loyalty = 0;
        TestAssert.AreEqual(10000, window.MaxUsefulSilver, "Empty loyalty takes 10000 silver");
        settlement.loyalty = 100;
        TestAssert.AreEqual(0, window.MaxUsefulSilver, "Full loyalty leaves nothing to buy");

        settlement.loyalty = 37.3;
        int cap = window.MaxUsefulSilver;
        TestAssert.IsTrue(cap is 6270 or 6271, $"Fractional loyalty rounds the cap up, got {cap}");
        TestAssert.GreaterThan(settlement.loyalty + window.ReturnValue(cap), 99.999,
            "Paying the cap fills loyalty to 100");
    }

    /* Searchable_FloatMenu */

    [EmpireTest("Windows")]
    public static void SearchableFloatMenu_LeavesTheCallersListAlone()
    {
        List<FloatMenuOption> options = [new("first", null), new("second", null)];
        Searchable_FloatMenu menu = new(options);
        TestAssert.IsNotNull(menu, "Menu constructed");
        TestAssert.AreEqual(2, options.Count, "Constructing the menu does not clear the caller's option list");
    }
}
