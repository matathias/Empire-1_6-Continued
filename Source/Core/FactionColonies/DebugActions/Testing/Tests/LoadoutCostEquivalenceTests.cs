namespace FactionColonies;

/* Tests for LoadoutUpgradeUtil's shared multiset comparison (animals, mechs, implants, inventory,
   psycasts), the mech work-mode comparison that decides whether mechs are rebuilt, and the unit
   design cost, which is now built from RaceCost plus the shared equipment sum. The cost tests
   recompute the design cost term by term the way UpdateEquipmentTotalCost always did, so any
   drift in the shared path shows up as a mismatch. Category "LoadoutUpgrade". */
public static class LoadoutCostEquivalenceTests
{
    private static PawnKindDef Kind(string defName) => new() { defName = defName };

    /* Multiset comparison */

    [EmpireTest("LoadoutUpgrade", GameState = TestGameState.Any)]
    public static void AnimalsEquivalent_NullEmptyAndKindlessRowsMatch()
    {
        List<SavedAnimal> kindless = [default, default];
        TestAssert.IsTrue(LoadoutUpgradeUtil.AnimalsEquivalent(null, null), "null vs null");
        TestAssert.IsTrue(LoadoutUpgradeUtil.AnimalsEquivalent(null, []), "null vs empty");
        TestAssert.IsTrue(LoadoutUpgradeUtil.AnimalsEquivalent(kindless, null), "Rows without a kind are ignored");
    }

    [EmpireTest("LoadoutUpgrade", GameState = TestGameState.Any)]
    public static void AnimalsEquivalent_OrderIndependentButCountSensitive()
    {
        PawnKindDef wolf = Kind("EmpireTest_Wolf");
        PawnKindDef boar = Kind("EmpireTest_Boar");

        List<SavedAnimal> a = [new(wolf, 2), new(boar, 1)];
        List<SavedAnimal> b = [new(boar, 1), default, new(wolf, 2)];
        TestAssert.IsTrue(LoadoutUpgradeUtil.AnimalsEquivalent(a, b), "Same rows in another order (plus an empty row) match");

        List<SavedAnimal> moreWolves = [new(wolf, 3), new(boar, 1)];
        TestAssert.IsFalse(LoadoutUpgradeUtil.AnimalsEquivalent(a, moreWolves), "A different count does not match");

        List<SavedAnimal> swapped = [new(wolf, 1), new(boar, 2)];
        TestAssert.IsFalse(LoadoutUpgradeUtil.AnimalsEquivalent(a, swapped), "Counts are paired with their kind");

        SavedAnimal zeroCount = new(wolf, 1) { count = 0 };
        TestAssert.IsTrue(LoadoutUpgradeUtil.AnimalsEquivalent([zeroCount], [new(wolf, 1)]), "A count of 0 counts as 1");
    }

    [EmpireTest("LoadoutUpgrade", GameState = TestGameState.Any)]
    public static void MechsEquivalent_GroupIsPartOfTheRow()
    {
        PawnKindDef militor = Kind("EmpireTest_Militor");
        PawnKindDef lifter = Kind("EmpireTest_Lifter");

        List<SavedMech> a = [new(militor, 2, 0), new(lifter, 1, 1)];
        List<SavedMech> b = [new(lifter, 1, 1), new(militor, 2, 0)];
        TestAssert.IsTrue(LoadoutUpgradeUtil.MechsEquivalent(a, b), "Same rows in another order match");

        List<SavedMech> regrouped = [new(militor, 2, 1), new(lifter, 1, 1)];
        TestAssert.IsFalse(LoadoutUpgradeUtil.MechsEquivalent(a, regrouped), "Moving a row to another group does not match");

        TestAssert.IsFalse(LoadoutUpgradeUtil.MechsEquivalent(a, [new(militor, 2, 0)]), "A missing row does not match");
        TestAssert.IsTrue(LoadoutUpgradeUtil.MechsEquivalent([default], null), "Rows without a kind are ignored");
    }

    [EmpireTest("LoadoutUpgrade", GameState = TestGameState.Any)]
    public static void ImplantsEquivalent_KeysOnSourcePartAndIndex()
    {
        RecipeDef bionicArm = new() { defName = "EmpireTest_InstallArm" };
        ThingDef selfInstall = new() { defName = "EmpireTest_SelfInstall" };
        BodyPartDef arm = new() { defName = "EmpireTest_Arm" };
        BodyPartDef brain = new() { defName = "EmpireTest_Brain" };

        List<SavedImplant> a = [new(bionicArm, arm, 0), new(bionicArm, arm, 1), new(selfInstall, brain, 0)];
        List<SavedImplant> b = [new(selfInstall, brain, 0), new(bionicArm, arm, 1), default, new(bionicArm, arm, 0)];
        TestAssert.IsTrue(LoadoutUpgradeUtil.ImplantsEquivalent(a, b), "Same implants in another order (plus an invalid row) match");

        List<SavedImplant> otherIndex = [new(bionicArm, arm, 0), new(bionicArm, arm, 0), new(selfInstall, brain, 0)];
        TestAssert.IsFalse(LoadoutUpgradeUtil.ImplantsEquivalent(a, otherIndex), "The body-part index distinguishes rows");

        List<SavedImplant> fewerLevels = [new(bionicArm, arm, 0), new(bionicArm, arm, 1)];
        TestAssert.IsFalse(LoadoutUpgradeUtil.ImplantsEquivalent(a, fewerLevels), "A missing implant does not match");
    }

    [EmpireTest("LoadoutUpgrade", GameState = TestGameState.Any)]
    public static void InventoryEquivalent_CountDistinguishesRows()
    {
        ThingDef meal = new() { defName = "EmpireTest_Meal" };
        ThingDef medicine = new() { defName = "EmpireTest_Medicine" };

        List<SavedThing> a = [new(meal, null, 5), new(medicine, null, 2)];
        List<SavedThing> b = [new(medicine, null, 2), new(meal, null, 5)];
        TestAssert.IsTrue(LoadoutUpgradeUtil.InventoryEquivalent(a, b), "Same stacks in another order match");

        List<SavedThing> fewerMeals = [new(meal, null, 4), new(medicine, null, 2)];
        TestAssert.IsFalse(LoadoutUpgradeUtil.InventoryEquivalent(a, fewerMeals), "A different stack count does not match");
        TestAssert.IsTrue(LoadoutUpgradeUtil.InventoryEquivalent([default], []), "Rows without a thing are ignored");
    }

    [EmpireTest("LoadoutUpgrade", GameState = TestGameState.Any)]
    public static void PsycastsEquivalent_LevelAndEveryFieldCount()
    {
        MilUnitFC a = new() { psylinkLevel = 3, psycasts = [new("sys", "Skip"), new("sys", "", "StatUpgrade", 2)] };
        MilUnitFC b = new() { psylinkLevel = 3, psycasts = [new("sys", "", "StatUpgrade", 2), new("sys", "Skip")] };
        TestAssert.IsTrue(LoadoutUpgradeUtil.PsycastsEquivalent(a, b), "Same psycasts in another order match");
        TestAssert.IsFalse(LoadoutUpgradeUtil.PsycastsChanged(a, b), "PsycastsChanged agrees");

        MilUnitFC moreStats = new() { psylinkLevel = 3, psycasts = [new("sys", "Skip"), new("sys", "", "StatUpgrade", 3)] };
        TestAssert.IsFalse(LoadoutUpgradeUtil.PsycastsEquivalent(a, moreStats), "A changed point count does not match");

        MilUnitFC otherSystem = new() { psylinkLevel = 3, psycasts = [new("other", "Skip"), new("sys", "", "StatUpgrade", 2)] };
        TestAssert.IsFalse(LoadoutUpgradeUtil.PsycastsEquivalent(a, otherSystem), "The owning system is part of the row");

        MilUnitFC higherLevel = new() { psylinkLevel = 4, psycasts = b.psycasts };
        TestAssert.IsFalse(LoadoutUpgradeUtil.PsycastsEquivalent(a, higherLevel), "A different psylink level does not match");

        TestAssert.IsTrue(LoadoutUpgradeUtil.PsycastsEquivalent(new MilUnitFC(), new MilUnitFC { psycasts = null }),
            "Empty and null psycast lists match");
    }

    /* Mech work modes */

    private static MilUnitFC Mechanitor(List<MechWorkModeDef> modes) => new() { isMechanitor = true, mechGroupWorkModes = modes };

    [EmpireTest("LoadoutUpgrade", GameState = TestGameState.Any, RequiresPackageId = ModContentPack.BiotechModPackageId)]
    public static void MechanitorChanged_DefaultWorkModesMatchMissingEntries()
    {
        MechWorkModeDef escort = TestAssert.NotNull(MechWorkModeDefOf.Escort, "Biotech's Escort work mode");

        // The designer pads the list with explicit Escort entries; an assigned snapshot may have none.
        TestAssert.IsFalse(LoadoutUpgradeUtil.MechanitorChanged(Mechanitor([escort, escort, escort]), Mechanitor([])),
            "Explicit Escort entries equal missing ones");
        TestAssert.IsFalse(LoadoutUpgradeUtil.MechanitorChanged(Mechanitor([escort, null]), Mechanitor([escort])),
            "A null entry resolves to Escort");
        TestAssert.IsFalse(LoadoutUpgradeUtil.MechanitorChanged(Mechanitor(null), Mechanitor([escort])),
            "A null list resolves to Escort everywhere");
    }

    [EmpireTest("LoadoutUpgrade", GameState = TestGameState.Any, RequiresPackageId = ModContentPack.BiotechModPackageId)]
    public static void MechanitorChanged_RealWorkModeChangeIsDetected()
    {
        MechWorkModeDef escort = TestAssert.NotNull(MechWorkModeDefOf.Escort, "Biotech's Escort work mode");
        MechWorkModeDef work = TestAssert.NotNull(MechWorkModeDefOf.Work, "Biotech's Work work mode");

        TestAssert.IsTrue(LoadoutUpgradeUtil.MechanitorChanged(Mechanitor([escort, work]), Mechanitor([escort])),
            "A non-default mode past the end of the other list is a change");
        TestAssert.IsTrue(LoadoutUpgradeUtil.MechanitorChanged(Mechanitor([work]), Mechanitor([escort])),
            "A different mode for the same group is a change");
        TestAssert.IsTrue(LoadoutUpgradeUtil.MechanitorChanged(Mechanitor([]), new MilUnitFC { isMechanitor = false }),
            "Turning the mechanitor flag off is a change");
    }

    /* Design cost */

    private static PawnKindDef AnyAnimalKind(PawnKindDef preferred, PawnKindDef exclude = null)
    {
        if (preferred?.race is not null && preferred != exclude) return preferred;
        return TestAssert.NotNull(DefDatabase<PawnKindDef>.AllDefs.FirstOrDefault(k => k.RaceProps?.Animal == true && k != exclude),
            "Some animal pawn kind should exist");
    }

    private static PawnKindDef AnyMechKind()
        => TestAssert.NotNull(PawnKindDefOf.Mech_Scyther?.race is not null
                ? PawnKindDefOf.Mech_Scyther
                : DefDatabase<PawnKindDef>.AllDefs.FirstOrDefault(k => k.RaceProps?.IsMechanoid == true),
            "Some mechanoid pawn kind should exist");

    /* Builds a unit that exercises every equipment term with fractional multipliers, so a dropped
     * or double-applied floor shows up. */
    private static MilUnitFC CostProbeUnit(out PawnKindDef animal, out PawnKindDef mount, out PawnKindDef mech)
    {
        animal = AnyAnimalKind(PawnKindDefOf.Muffalo);
        mount = AnyAnimalKind(PawnKindDefOf.Thrumbo, animal);
        mech = AnyMechKind();
        return new MilUnitFC
        {
            pawnKind = PawnKindDefOf.Colonist,
            inventory = [new(ThingDefOf.MedicineIndustrial, null, 3)],
            animals = [new(animal, 2)],
            mount = mount,
            isMechanitor = true,
            mechs = [new(mech, 2, 0)],
        };
    }

    /* The design cost exactly as UpdateEquipmentTotalCost summed it before the equipment terms moved
     * into LoadoutUpgradeUtil. A unit with no xenotype has a gene factor of 1. */
    private static double ExpectedDesignCost(MilUnitFC unit, PawnKindDef animal, PawnKindDef mount, PawnKindDef mech)
    {
        double total = Math.Floor(unit.pawnKind.race.BaseMarketValue * FCSettings.militaryRaceCostMultiplier * 1f);
        foreach (SavedThing inv in unit.inventory) total += inv.MarketValue;
        total += PsycastSystemRegistry.Active?.PsylinkCost(unit.psylinkLevel) ?? 0;
        total += Math.Floor(animal.race.BaseMarketValue * FCSettings.militaryAnimalCostMultiplier) * 2;
        total += Math.Floor(mount.race.BaseMarketValue * FCSettings.militaryAnimalCostMultiplier);
        if (ModsConfig.BiotechActive)
        {
            total += FCSettings.militaryMechlinkCost;
            total += Math.Floor(mech.race.BaseMarketValue * FCSettings.militaryMechCostMultiplier) * 2;
        }
        return total;
    }

    private static void WithFractionalMultipliers(Action body)
    {
        TestUtil.WithOverride(() => FCSettings.militaryRaceCostMultiplier, v => FCSettings.militaryRaceCostMultiplier = v, 0.137, () =>
            TestUtil.WithOverride(() => FCSettings.militaryAnimalCostMultiplier, v => FCSettings.militaryAnimalCostMultiplier = v, 1.37, () =>
                TestUtil.WithOverride(() => FCSettings.militaryMechCostMultiplier, v => FCSettings.militaryMechCostMultiplier = v, 0.73, () =>
                    TestUtil.WithOverride(() => FCSettings.militaryMechlinkCost, v => FCSettings.militaryMechlinkCost = v, 123.5, body))));
    }

    [EmpireTest("LoadoutUpgrade", GameState = TestGameState.Any)]
    public static void DesignCost_MatchesTermByTermFormula() => WithFractionalMultipliers(() =>
    {
        MilUnitFC unit = CostProbeUnit(out PawnKindDef animal, out PawnKindDef mount, out PawnKindDef mech);
        double expected = ExpectedDesignCost(unit, animal, mount, mech);

        unit.UpdateEquipmentTotalCost();
        TestAssert.AreEqual(Math.Ceiling(expected), unit.equipmentTotalCost, 0.0001, "Design cost is the ceiling of the term-by-term sum");
        TestAssert.AreEqual(expected - unit.RaceCost(), LoadoutUpgradeUtil.SumEquipmentCost(unit), 0.0001,
            "The equipment sum is the design cost minus the race term");
        TestAssert.AreEqual(Math.Floor(PawnKindDefOf.Colonist.race.BaseMarketValue * FCSettings.militaryRaceCostMultiplier),
            unit.RaceCost(), 0.0001, "RaceCost floors the scaled race value");
    });

    [EmpireTest("LoadoutUpgrade", GameState = TestGameState.Any)]
    public static void UnitCosts_FloorPerUnit() => WithFractionalMultipliers(() =>
    {
        PawnKindDef animal = AnyAnimalKind(PawnKindDefOf.Muffalo);
        PawnKindDef mech = AnyMechKind();

        TestAssert.AreEqual(Math.Floor(animal.race.BaseMarketValue * FCSettings.militaryAnimalCostMultiplier),
            MilUnitFC.AnimalUnitCost(animal), 0.0001, "Animal unit cost");
        TestAssert.AreEqual(Math.Floor(mech.race.BaseMarketValue * FCSettings.militaryMechCostMultiplier),
            MilUnitFC.MechUnitCost(mech), 0.0001, "Mech unit cost");
        TestAssert.AreEqual(0.0, MilUnitFC.AnimalUnitCost(null), 0.0001, "No kind costs nothing");
        TestAssert.AreEqual(0.0, MilUnitFC.MechUnitCost(new PawnKindDef()), 0.0001, "A kind without a race costs nothing");
    });

    [EmpireTest("LoadoutUpgrade", GameState = TestGameState.Any)]
    public static void DesignCost_BlankAndRacelessUnits() => WithFractionalMultipliers(() =>
    {
        MilUnitFC blank = CostProbeUnit(out _, out _, out _);
        blank.isBlank = true;
        blank.UpdateEquipmentTotalCost();
        TestAssert.AreEqual(0.0, blank.equipmentTotalCost, 0.0001, "A blank unit costs nothing");

        MilUnitFC raceless = CostProbeUnit(out PawnKindDef animal, out PawnKindDef mount, out PawnKindDef mech);
        double expected = ExpectedDesignCost(raceless, animal, mount, mech) - raceless.RaceCost();
        raceless.pawnKind = null;
        TestAssert.AreEqual(0.0, raceless.RaceCost(), 0.0001, "No pawn kind means no race term");
        raceless.UpdateEquipmentTotalCost();
        TestAssert.AreEqual(Math.Ceiling(expected), raceless.equipmentTotalCost, 0.0001, "Equipment still counts without a race");
    });

    [EmpireTest("LoadoutUpgrade", GameState = TestGameState.Any)]
    public static void UpgradeDiff_AddedAnimalCostsOneUnitEach() => WithFractionalMultipliers(() =>
    {
        MilUnitFC current = CostProbeUnit(out PawnKindDef animal, out _, out _);
        MilUnitFC target = CostProbeUnit(out _, out _, out _);
        target.animals = [new(animal, 3)];

        TestAssert.AreEqual(MilUnitFC.AnimalUnitCost(animal), LoadoutUpgradeUtil.RawEquipmentDiff(target, current), 0.0001,
            "One more animal costs one animal unit");
        TestAssert.AreEqual(0.0, LoadoutUpgradeUtil.RawEquipmentDiff(current, target), 0.0001, "A cheaper target is free");

        target.pawnKind = PawnKindDefOf.Villager;
        TestAssert.AreEqual(MilUnitFC.AnimalUnitCost(animal), LoadoutUpgradeUtil.RawEquipmentDiff(target, current), 0.0001,
            "A pawn-kind difference adds no race cost to the upgrade");
    });
}
