namespace FactionColonies;

/* DESTRUCTIVE: live-settlement cover for the building slot cap, settlement removal and Destroy,
   prisoner sale and death, the drafted-NPC list without a battle, faction averages with null
   entries, the stat breakdown and the short-name save key. Creates transient settlements,
   generates pawns, and may move the game clock forward by up to 249 ticks. Not reverted, except
   for the settings each test overrides. */
public static class CoreWorldobjectsDestructiveTests
{
    /*-*-*-*-*/
    /* Helpers */
    /*-*-*-*-*/

    private static void WithMaxLevel(int level, Action body) =>
        TestUtil.WithOverride(() => FCSettings.settlementMaxLevel, v => FCSettings.settlementMaxLevel = v, level, body);

    // Buildings the settlement's type allows, other than the Empty and Construction placeholders.
    private static List<BuildingFCDef> BuildableFor(WorldSettlementFC settlement) =>
        DefDatabase<BuildingFCDef>.AllDefsListForReading
            .Where(d => d != BuildingFCDefOf.Empty && d != BuildingFCDefOf.Construction
                        && d.CanBeBuiltForSettlementType(settlement.settlementDef))
            .ToList();

    // Moves the clock forward to the settlement's next 250-tick hash interval, where its
    // buildings comp re-checks the slot cap.
    private static void AdvanceToCapCheckTick(WorldSettlementFC settlement)
    {
        for (int i = 0; i < 250 && !settlement.IsHashIntervalTick(250); i++)
            Find.TickManager.DebugSetTicksGame(Find.TickManager.TicksGame + 1);
        TestAssert.IsTrue(settlement.IsHashIntervalTick(250), "could not reach the settlement's 250-tick hash interval");
    }

    private static Faction NonEmpireHumanlikeFaction(Faction empire) =>
        Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f =>
            f is not null && !f.IsPlayer && f != empire && !f.Hidden && !f.defeated && f.def.humanlikeFaction)
        ?? empire;

    /*-*-*-*-*/
    /* Building slot cap */
    /*-*-*-*-*/

    [EmpireDestructiveTest("Destructive.Settlement")]
    public static void BuildingCap_Lowered_SaveLoadPadsOnly_TickDestroysExtras()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = DestructiveTestUtil.RequireTransientSettlement();
        WorldObjectComp_SettlementBuildings comp = TestAssert.NotNull(s.BuildingsComp, "the settlement has a buildings comp");
        List<BuildingFCDef> buildable = BuildableFor(s);
        TestAssert.IsNotEmpty(buildable, $"no building can be built on {s.settlementDef.defName}");

        WithMaxLevel(10, () =>
        {
            comp.ReinitBuildings();
            int fullCap = comp.FC_MAX_BUILDINGS;
            TestAssert.AreEqual(fullCap, comp.Buildings.Count, "the slot list is padded to the cap");

            BuildingFCDef kept = buildable[0];
            BuildingFCDef dropped = buildable[Math.Min(1, buildable.Count - 1)];
            comp.ConstructBuilding(kept, 0);
            comp.ConstructBuilding(dropped, fullCap - 1);

            WithMaxLevel(1, () =>
            {
                int lowCap = comp.FC_MAX_BUILDINGS;
                TestAssert.LessThan(lowCap, fullCap, "lowering the max level lowers the slot cap");

                // Save and load must not touch buildings: they only pad.
                comp.ReinitBuildings(allowShrink: false);
                TestAssert.AreEqual(fullCap, comp.Buildings.Count, "a save/load pass keeps slots past a lowered cap");
                TestAssert.IsTrue(comp.GetBuildingInSlot(fullCap - 1) == dropped, "the high-slot building survives a save/load pass");

                // Normal game time enforces the lowered cap.
                AdvanceToCapCheckTick(s);
                comp.CompTick();
                TestAssert.AreEqual(lowCap, comp.Buildings.Count, "the next cap check trims the slot list to the lowered cap");
                TestAssert.IsTrue(comp.GetBuildingInSlot(0) == kept, "buildings inside the cap are kept");
            });

            comp.ReinitBuildings();
            TestAssert.AreEqual(fullCap, comp.Buildings.Count, "raising the cap again pads the slot list back out");
            TestAssert.IsTrue(comp.GetBuildingInSlot(fullCap - 1) == BuildingFCDefOf.Empty,
                "the slot that was past the lowered cap comes back empty: its building was destroyed");
        });

        DestructiveTestUtil.AssertEmpireInvariants(f, "BuildingCap_Lowered_SaveLoadPadsOnly_TickDestroysExtras");
    }

    /* The slot list is trimmed to the cap computed from the lowered max level, so NumBuildingSlots
       must be capped the same way even though the settlement keeps its own higher level; otherwise
       every "for k < NumBuildingSlots" loop indexes past the end of the list. */
    [EmpireDestructiveTest("Destructive.Settlement")]
    public static void BuildingCap_Lowered_UnlockedSlotsNeverExceedSlotList()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = DestructiveTestUtil.RequireTransientSettlement();
        WorldObjectComp_SettlementBuildings comp = TestAssert.NotNull(s.BuildingsComp, "the settlement has a buildings comp");

        WithMaxLevel(10, () =>
        {
            s.settlementLevel = WorldSettlementFC.ClampSettlementLevel(10, s.settlementDef);
            comp.ReinitBuildings();

            WithMaxLevel(1, () =>
            {
                comp.ReinitBuildings();
                TestAssert.LessThanOrEqual(comp.NumBuildingSlots, comp.Buildings.Count,
                    $"NumBuildingSlots ({comp.NumBuildingSlots}) must not exceed the slot list ({comp.Buildings.Count}) after the cap drops");
                TestAssert.DoesNotThrow(() =>
                {
                    for (int k = 0; k < comp.NumBuildingSlots; k++) comp.BuildingSlotIsBuilding(k);
                }, "walking every unlocked slot must stay inside the slot list");
            });

            s.settlementLevel = 1;
            comp.ReinitBuildings();
        });

        DestructiveTestUtil.AssertEmpireInvariants(f, "BuildingCap_Lowered_UnlockedSlotsNeverExceedSlotList");
    }

    [EmpireDestructiveTest("Destructive.Settlement")]
    public static void ConstructBuilding_SlotPastTheList_MovesToFirstEmptySlot()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = DestructiveTestUtil.RequireTransientSettlement();
        WorldObjectComp_SettlementBuildings comp = TestAssert.NotNull(s.BuildingsComp, "the settlement has a buildings comp");
        List<BuildingFCDef> buildable = BuildableFor(s);
        TestAssert.IsNotEmpty(buildable, $"no building can be built on {s.settlementDef.defName}");

        comp.ReinitBuildings();
        int freeSlot = comp.Buildings.FindIndex(b => b.def == BuildingFCDefOf.Empty);
        TestAssert.GreaterThan(freeSlot, -1, "the transient settlement has an empty slot");

        // A construction queued for a slot a lowered cap has since removed.
        bool built = comp.ConstructBuilding(buildable[0], comp.Buildings.Count + 2);

        TestAssert.IsTrue(built, "a free slot was available, so the building should be built");
        TestAssert.IsTrue(comp.GetBuildingInSlot(freeSlot) == buildable[0], "the building lands in the first empty slot");
        TestAssert.AreEqual(comp.FC_MAX_BUILDINGS, comp.Buildings.Count, "the slot list keeps its size");

        DestructiveTestUtil.AssertEmpireInvariants(f, "ConstructBuilding_SlotPastTheList_MovesToFirstEmptySlot");
    }

    /*-*-*-*-*/
    /* Removal and Destroy */
    /*-*-*-*-*/

    [EmpireDestructiveTest("Destructive.Settlement")]
    public static void RemovePlayerSettlement_DestroysWorldObject()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = DestructiveTestUtil.RequireTransientSettlement();

        ColonyUtil.RemovePlayerSettlement(s);

        TestAssert.IsTrue(s.Destroyed, "the settlement is marked destroyed, not just unlisted");
        TestAssert.IsFalse(s.Spawned, "the settlement is no longer spawned");
        TestAssert.IsFalse(Find.WorldObjects.Contains(s), "the settlement is gone from the world");
        TestAssert.IsFalse(f.settlements.Contains(s), "the settlement is gone from the faction");
        DestructiveTestUtil.AssertEmpireInvariants(f, "RemovePlayerSettlement_DestroysWorldObject");
    }

    [EmpireDestructiveTest("Destructive.Settlement")]
    public static void Destroy_WithoutPrepare_NotUnderAttack_IsIgnored()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = DestructiveTestUtil.RequireTransientSettlement();
        TestAssert.IsFalse(s.MilitaryComp?.isUnderAttack == true, "a fresh settlement is not under attack");

        TestAssert.DoesNotThrow(() => s.Destroy(), "an unprepared Destroy must not throw");
        TestAssert.IsFalse(s.Destroyed, "an unprepared Destroy outside a battle leaves the settlement alive");
        TestAssert.IsTrue(Find.WorldObjects.Contains(s), "the settlement stays in the world");
        TestAssert.IsTrue(f.settlements.Contains(s), "the settlement stays in the faction");

        DestructiveTestUtil.SafeRemoveSettlement(s);
        TestAssert.IsTrue(s.Destroyed, "the deliberate removal afterwards still destroys it");
        DestructiveTestUtil.AssertEmpireInvariants(f, "Destroy_WithoutPrepare_NotUnderAttack_IsIgnored");
    }

    /*-*-*-*-*/
    /* Prisoners */
    /*-*-*-*-*/

    [EmpireDestructiveTest("Destructive.Settlement")]
    public static void SellPrisoner_HandsPawnBackToWorldPawns()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        Faction empire = DestructiveTestUtil.EnsureEmpireFaction();
        WorldSettlementFC s = DestructiveTestUtil.RequireTransientSettlement();
        WorldObjectComp_SettlementPrisoners comp = TestAssert.NotNull(s.PrisonerComp, "the settlement has a prisoners comp");

        Pawn pawn = PaymentUtil.GeneratePrisoner(NonEmpireHumanlikeFaction(empire));
        comp.AddPrisoner(pawn);
        FCPrisoner prisoner = TestAssert.NotNull(comp.prisonerList.FirstOrDefault(p => p?.prisoner == pawn), "the prisoner was added");
        TestAssert.IsFalse(Find.WorldPawns.Contains(pawn), "a held prisoner is owned by the comp, not WorldPawns");

        comp.SellPrisoner(prisoner);

        TestAssert.IsFalse(comp.prisonerList.Contains(prisoner), "the sold prisoner leaves the settlement");
        TestAssert.IsFalse(pawn.Destroyed, "a sold prisoner is not destroyed");
        TestAssert.IsTrue(Find.WorldPawns.Contains(pawn), "a sold prisoner is passed back to WorldPawns, so references to it resolve");
        TestAssert.IsFalse(pawn.IsPrisoner, "a sold prisoner is no longer a prisoner");
        DestructiveTestUtil.AssertEmpireInvariants(f, "SellPrisoner_HandsPawnBackToWorldPawns");
    }

    [EmpireDestructiveTest("Destructive.Settlement")]
    public static void DeadPrisoner_IsKilledAndPassedToWorld()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        Faction empire = DestructiveTestUtil.EnsureEmpireFaction();
        WorldSettlementFC s = DestructiveTestUtil.RequireTransientSettlement();
        WorldObjectComp_SettlementPrisoners comp = TestAssert.NotNull(s.PrisonerComp, "the settlement has a prisoners comp");

        Faction home = NonEmpireHumanlikeFaction(empire);
        Pawn pawn = PaymentUtil.GeneratePrisoner(home);
        comp.AddPrisoner(pawn);
        FCPrisoner prisoner = TestAssert.NotNull(comp.prisonerList.FirstOrDefault(p => p?.prisoner == pawn), "the prisoner was added");

        comp.DropDeadPrisoner(prisoner, out string name, out Faction reportedHome);

        TestAssert.IsFalse(comp.prisonerList.Contains(prisoner), "the dead prisoner leaves the settlement");
        TestAssert.IsTrue(pawn.Dead, "the prisoner's pawn is actually killed");
        TestAssert.IsTrue(Find.WorldPawns.Contains(pawn), "the dead pawn is kept by WorldPawns instead of vanishing");
        TestAssert.IsFalse(name.NullOrEmpty(), "the death letter gets the prisoner's name");
        TestAssert.IsTrue(reportedHome == home, "the home faction is reported for the goodwill penalty");
        DestructiveTestUtil.AssertEmpireInvariants(f, "DeadPrisoner_IsKilledAndPassedToWorld");
    }

    /*-*-*-*-*/
    /* Drafted NPCs without a battle */
    /*-*-*-*-*/

    [EmpireDestructiveTest("Destructive.Military")]
    public static void DraftedNPCs_WithoutBattlefield_IsThrowawayList()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC a = DestructiveTestUtil.RequireTransientSettlement();
        WorldSettlementFC b = DestructiveTestUtil.RequireTransientSettlement();
        TestAssert.IsNull(FindFC.MilitaryManager?.GetBattlefield(a.Tile), "a fresh settlement has no battlefield");
        TestAssert.IsNull(FindFC.MilitaryManager?.GetBattlefield(b.Tile), "a fresh settlement has no battlefield");

        Pawn pawn = DestructiveTestUtil.GenerateWorldPawn();
        a.MilitaryComp.draftedNPCs.Add(pawn);

        TestAssert.IsFalse(a.MilitaryComp.draftedNPCs.Contains(pawn), "without a battlefield nothing is kept");
        TestAssert.IsFalse(b.MilitaryComp.draftedNPCs.Contains(pawn), "a stray add must not leak into another settlement's list");
        DestructiveTestUtil.AssertEmpireInvariants(f, "DraftedNPCs_WithoutBattlefield_IsThrowawayList");
    }

    /*-*-*-*-*/
    /* Faction averages */
    /*-*-*-*-*/

    [EmpireDestructiveTest("Destructive.Faction")]
    public static void Averages_IgnoreNullSettlementEntries()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        DestructiveTestUtil.EnsureSettlements(2);

        List<WorldSettlementFC> live = f.settlements.Where(s => s is not null).ToList();
        double expected = live.Average(s => s.happiness);

        f.settlements.Add(null);
        try
        {
            f.DirtyAveragesCache();
            TestAssert.AreEqual(expected, f.averageHappiness, 0.0001,
                "a null entry must not count toward the average");
        }
        finally
        {
            f.settlements.RemoveAll(s => s is null);
            f.DirtyAveragesCache();
        }
        DestructiveTestUtil.AssertEmpireInvariants(f, "Averages_IgnoreNullSettlementEntries");
    }

    /*-*-*-*-*/
    /* Stat breakdown and short-name save key */
    /*-*-*-*-*/

    [EmpireDestructiveTest("Destructive.Settlement")]
    public static void StatDesc_ListsEverySettlementModifierSource()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = DestructiveTestUtil.RequireTransientSettlement();
        FCStatDef stat = new() { defName = "TestStat_Desc", aggregation = FCStatAggregation.Additive, appliesToSettlements = true };

        s.AddStatModifiers([new FCStatModifier { stat = stat, value = 2 }], "testTagged", "TaggedSourceLabel");
        s.AddPermanentModifiers([new FCStatModifier { stat = stat, value = 3 }], "testPermanent", "PermanentSourceLabel");
        s.AddDecayingPenalty(stat, 10, 5, "testDecaying", "DecayingSourceLabel");

        string desc = s.GetStatDesc(stat);
        TestAssert.IsTrue(desc.Contains("TaggedSourceLabel"), "the tagged modifier is listed");
        TestAssert.IsTrue(desc.Contains("PermanentSourceLabel (permanent)"), "the permanent modifier is listed and marked");
        TestAssert.IsTrue(desc.Contains("DecayingSourceLabel"), "the decaying penalty is listed");
        TestAssert.AreEqual(7.0, s.GetSettlementStatValue(stat), message: "the value adds the same three sources");

        s.RemoveStatModifiersBySource("testTagged");
        s.RemovePermanentModifiersBySource("testPermanent");
        DestructiveTestUtil.AssertEmpireInvariants(f, "StatDesc_ListsEverySettlementModifierSource");
    }

    [EmpireDestructiveTest("Destructive.Settlement")]
    public static void ShortName_OnlyCustomNameIsSaved()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = DestructiveTestUtil.RequireTransientSettlement();

        s.SetShortName("ZZCustom");
        string customXml = Scribe.saver.DebugOutputFor(s);
        TestAssert.IsTrue(customXml.Contains("<nameShort>ZZCustom</nameShort>"),
            "a custom short name is written to the save");

        s.SetShortName(TextUtil.ToShortName(s.Name));
        string derivedXml = Scribe.saver.DebugOutputFor(s);
        TestAssert.IsFalse(derivedXml.Contains("<nameShort>"),
            "a derived short name is not saved, so it keeps following the settlement's name");
        DestructiveTestUtil.AssertEmpireInvariants(f, "ShortName_OnlyCustomNameIsSaved");
    }
}
