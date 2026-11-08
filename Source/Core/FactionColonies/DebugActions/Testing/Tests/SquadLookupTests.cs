namespace FactionColonies;

/* Pure tests for the squad-level pawn lookups, the rider-to-mount map, the deployed check and the
   sub-pawn refill filters. Squads, mercs and pawns are bare objects that are never generated,
   spawned or registered anywhere, so no game state is touched. */
public static class SquadLookupTests
{
    private static int nextFakeId = -7_000_000;

    /// <summary>A bare, never-generated pawn. Its def and a unique id are set so it works as a
    /// dictionary key (Pawn equality compares def names, then ids).</summary>
    internal static Pawn FakePawn() => new() { def = ThingDefOf.Human, thingIDNumber = nextFakeId-- };

    internal static Mercenary SlotMerc(MercenarySquadFC squad, Pawn pawn)
    {
        Mercenary merc = new() { pawn = pawn, squad = squad };
        squad.mercenaries.Add(merc);
        return merc;
    }

    internal static Mercenary AddSub(Mercenary owner, Mercenary.SubPawnType type, Pawn pawn, PawnKindDef kind = null)
    {
        Mercenary sub = new() { pawn = pawn, handler = owner, squad = owner.squad, subPawnType = type, subPawnKind = kind };
        if (type == Mercenary.SubPawnType.Mech) owner.mechs.Add(sub);
        else owner.animals.Add(sub);
        return sub;
    }

    internal static MercenarySquadFC NewSquad() => new() { mercenaries = [] };

    /* ReturnPawn / FindSubPawn / FindMercOrSubPawn */

    [EmpireTest("Military", GameState = TestGameState.Any)]
    public static void ReturnPawn_NullPawn_DoesNotMatchEmptySlot()
    {
        MercenarySquadFC squad = NewSquad();
        SlotMerc(squad, null);
        TestAssert.IsNull(squad.ReturnPawn(null), "A null pawn must never match an empty slot");
        TestAssert.IsNull(squad.FindSubPawn(null), "A null pawn must never match a sub-pawn wrapper");
        TestAssert.IsNull(squad.FindMercOrSubPawn(null), "A null pawn must never match anything");
    }

    [EmpireTest("Military", GameState = TestGameState.Any)]
    public static void ReturnPawn_NullMercList_ReturnsNull()
    {
        MercenarySquadFC squad = new() { mercenaries = null };
        TestAssert.DoesNotThrow(() => squad.ReturnPawn(FakePawn()), "ReturnPawn threw on a squad without a merc list");
        TestAssert.IsNull(squad.ReturnPawn(FakePawn()));
        TestAssert.IsNull(squad.FindMercOrSubPawn(FakePawn()));
    }

    [EmpireTest("Military", GameState = TestGameState.Any)]
    public static void Lookups_SlotOnlySubOnlyAndCombined_StayDistinct()
    {
        MercenarySquadFC squad = NewSquad();
        Pawn mercPawn = FakePawn();
        Pawn animalPawn = FakePawn();
        Pawn mechPawn = FakePawn();
        Mercenary merc = SlotMerc(squad, mercPawn);
        Mercenary animal = AddSub(merc, Mercenary.SubPawnType.Animal, animalPawn);
        Mercenary mech = AddSub(merc, Mercenary.SubPawnType.Mech, mechPawn);

        TestAssert.IsTrue(squad.ReturnPawn(mercPawn) == merc, "ReturnPawn should find the slot merc");
        TestAssert.IsNull(squad.ReturnPawn(animalPawn), "ReturnPawn must not search sub-pawns");
        TestAssert.IsNull(squad.FindSubPawn(mercPawn), "FindSubPawn must not search slot mercs");
        TestAssert.IsTrue(squad.FindSubPawn(animalPawn) == animal, "FindSubPawn should find an animal");
        TestAssert.IsTrue(squad.FindSubPawn(mechPawn) == mech, "FindSubPawn should find a mech");
        TestAssert.IsTrue(squad.FindMercOrSubPawn(mercPawn) == merc, "Combined lookup should find the slot merc");
        TestAssert.IsTrue(squad.FindMercOrSubPawn(mechPawn) == mech, "Combined lookup should find the mech");
        TestAssert.IsNull(squad.FindMercOrSubPawn(FakePawn()), "A stranger pawn should match nothing");
    }

    [EmpireTest("Military", GameState = TestGameState.Playing)]
    public static void ReturnMercenaryFromUnit_NullSquad_ReturnsNull()
    {
        MilitaryFC mil = TestAssert.NotNull(FindFC.Military, "No MilitaryFC in a loaded game");
        TestAssert.DoesNotThrow(() => mil.ReturnMercenaryFromUnit(FakePawn(), null), "ReturnMercenaryFromUnit threw on a null squad");
        TestAssert.IsNull(mil.ReturnMercenaryFromUnit(FakePawn(), null));
        TestAssert.IsNull(mil.ReturnSquadFromUnit(null), "ReturnSquadFromUnit(null) should return null");
    }

    /* BuildMountMap */

    [EmpireTest("Military", GameState = TestGameState.Any)]
    public static void BuildMountMap_OnlyMountsCount_CompanionsLeftOut()
    {
        MercenarySquadFC squad = NewSquad();
        Pawn rider = FakePawn();
        Pawn mount = FakePawn();
        Mercenary merc = SlotMerc(squad, rider);
        AddSub(merc, Mercenary.SubPawnType.Animal, FakePawn());
        AddSub(merc, Mercenary.SubPawnType.Mount, mount);

        Dictionary<Pawn, Pawn> map = squad.BuildMountMap();
        TestAssert.AreEqual(1, map.Count, "Only the mount should be mapped");
        TestAssert.IsTrue(map.TryGetValue(rider, out Pawn mapped) && mapped == mount, "The rider should map to its mount");
    }

    [EmpireTest("Military", GameState = TestGameState.Any)]
    public static void BuildMountMap_RiderWithTwoMounts_FirstWins()
    {
        MercenarySquadFC squad = NewSquad();
        Pawn rider = FakePawn();
        Pawn first = FakePawn();
        Pawn second = FakePawn();
        Mercenary merc = SlotMerc(squad, rider);
        AddSub(merc, Mercenary.SubPawnType.Mount, first);
        AddSub(merc, Mercenary.SubPawnType.Mount, second);

        Dictionary<Pawn, Pawn> map = null;
        TestAssert.DoesNotThrow(() => map = squad.BuildMountMap(), "A rider with two mounts must not throw a duplicate-key error");
        TestAssert.AreEqual(1, map.Count);
        TestAssert.IsTrue(map[rider] == first, "The first mount should win");
    }

    [EmpireTest("Military", GameState = TestGameState.Any)]
    public static void BuildMountMap_SkipsDeadMountsAndRiderlessMounts()
    {
        MercenarySquadFC squad = NewSquad();
        Mercenary emptySlot = SlotMerc(squad, null);
        AddSub(emptySlot, Mercenary.SubPawnType.Mount, FakePawn());   // the rider's slot is empty
        Mercenary merc = SlotMerc(squad, FakePawn());
        AddSub(merc, Mercenary.SubPawnType.Mount, null);              // the mount is dead

        TestAssert.AreEqual(0, squad.BuildMountMap().Count, "Neither a riderless mount nor a dead mount should be mapped");
    }

    [EmpireTest("Military", GameState = TestGameState.Any)]
    public static void BuildMountMap_RiderFilter_SkipsRidersNotSpawning()
    {
        MercenarySquadFC squad = NewSquad();
        Pawn spawningRider = FakePawn();
        Pawn homeRider = FakePawn();
        Pawn spawningMount = FakePawn();
        AddSub(SlotMerc(squad, spawningRider), Mercenary.SubPawnType.Mount, spawningMount);
        AddSub(SlotMerc(squad, homeRider), Mercenary.SubPawnType.Mount, FakePawn());

        Dictionary<Pawn, Pawn> map = squad.BuildMountMap([spawningRider, spawningMount]);
        TestAssert.AreEqual(1, map.Count, "A mount whose rider stays home should be skipped");
        TestAssert.IsTrue(map.ContainsKey(spawningRider));
        TestAssert.IsFalse(map.ContainsKey(homeRider));
    }

    /* IsPhysicallyDeployed (off-map half; the on-map half is a destructive test) */

    [EmpireTest("Military", GameState = TestGameState.Any)]
    public static void IsPhysicallyDeployed_AllOffMap_False()
    {
        MercenarySquadFC squad = NewSquad();
        Mercenary merc = SlotMerc(squad, FakePawn());
        AddSub(merc, Mercenary.SubPawnType.Mech, FakePawn());
        SlotMerc(squad, null);
        TestAssert.IsFalse(squad.Deployment.IsPhysicallyDeployed(), "No pawn is on a map");

        MercenarySquadFC noList = new() { mercenaries = null };
        TestAssert.IsFalse(noList.Deployment.IsPhysicallyDeployed(), "A squad without a merc list is not deployed");
    }

    /* Sub-pawn refill filters */

    [EmpireTest("Military", GameState = TestGameState.Any)]
    public static void MissingSubPawns_UnresolvedKind_NotRefilled()
    {
        MercenarySquadFC squad = NewSquad();
        Mercenary merc = SlotMerc(squad, FakePawn());
        Mercenary lostKind = AddSub(merc, Mercenary.SubPawnType.Animal, null, kind: null);
        Mercenary knownKind = AddSub(merc, Mercenary.SubPawnType.Animal, null, kind: PawnKindDefOf.Muffalo);

        List<Mercenary> missing = squad.MissingSubPawns().ToList();
        TestAssert.IsFalse(missing.Contains(lostKind), "A sub-pawn whose kind no longer resolves can't be recreated");
        TestAssert.IsTrue(missing.Contains(knownKind), "A dead sub-pawn with a known kind should still be refilled");
        TestAssert.AreEqual(0, SquadCostExtensions.SubPawnReplaceCost(lostKind), "An unresolvable kind costs nothing");
    }

    [EmpireTest("Military", GameState = TestGameState.Any)]
    public static void MissingSubPawns_OwnerSlotEmpty_NotRefilled()
    {
        MercenarySquadFC squad = NewSquad();
        Mercenary emptyOwner = SlotMerc(squad, null);
        AddSub(emptyOwner, Mercenary.SubPawnType.Animal, null, kind: PawnKindDefOf.Muffalo);
        TestAssert.IsEmpty(squad.MissingSubPawns(), "Sub-pawns of an empty slot are rebuilt with the slot, not on their own");
    }

    [EmpireTest("Military", GameState = TestGameState.Playing)]
    public static void CreateNewAnimal_NullKind_LeavesSlotEmpty()
    {
        MercenarySquadFC squad = NewSquad();
        Mercenary sub = new() { subPawnType = Mercenary.SubPawnType.Animal };
        TestAssert.DoesNotThrow(() => MercenaryPawnFactory.CreateNewAnimal(squad, ref sub, null),
            "CreateNewAnimal threw on a null kind");
        TestAssert.IsNull(sub.pawn, "No pawn should be generated without a kind");
    }

    /* StripOverseerRelations */

    [EmpireTest("Military", GameState = TestGameState.Any, RequiresPackageId = "Ludeon.RimWorld.Biotech")]
    public static void StripOverseerRelations_RemovesOnlyMatchingOverseerEntries()
    {
        Pawn overseer = FakePawn();
        overseer.relations = new Pawn_RelationsTracker(overseer);
        Pawn keptMech = FakePawn();
        Pawn strippedMech = FakePawn();
        Pawn bonded = FakePawn();
        List<DirectPawnRelation> rels = overseer.relations.DirectRelations;
        rels.Add(new DirectPawnRelation(PawnRelationDefOf.Overseer, keptMech, 0));
        rels.Add(new DirectPawnRelation(PawnRelationDefOf.Overseer, strippedMech, 0));
        rels.Add(new DirectPawnRelation(PawnRelationDefOf.Bond, strippedMech, 0));
        rels.Add(null);
        rels.Add(new DirectPawnRelation(null, bonded, 0));

        MercenaryPawnFactory.StripOverseerRelations(overseer, other => other == strippedMech);
        TestAssert.AreEqual(4, rels.Count, "Only the matching Overseer entry should go; malformed entries stay by default");
        TestAssert.IsTrue(rels.Any(r => r?.def == PawnRelationDefOf.Overseer && r.otherPawn == keptMech), "A non-matching Overseer entry stays");
        TestAssert.IsTrue(rels.Any(r => r?.def == PawnRelationDefOf.Bond && r.otherPawn == strippedMech), "Non-Overseer relations stay");

        MercenaryPawnFactory.StripOverseerRelations(overseer, _ => false, dropMalformed: true);
        TestAssert.AreEqual(2, rels.Count, "dropMalformed should remove the null entry and the entry without a def");
        TestAssert.IsFalse(rels.Any(r => r is null || r.def is null));
    }

    [EmpireTest("Military", GameState = TestGameState.Any)]
    public static void StripOverseerRelations_NoTracker_DoesNotThrow()
    {
        TestAssert.DoesNotThrow(() => MercenaryPawnFactory.StripOverseerRelations(null, _ => true));
        TestAssert.DoesNotThrow(() => MercenaryPawnFactory.StripOverseerRelations(FakePawn(), _ => true));
    }
}
