namespace FactionColonies;

/* DESTRUCTIVE: hires/dismisses real mercenary squads (spends silver), assigns them, and
   creates/unregisters real deploy operations. Not reverted. */
public static class MilitaryDestructiveTests
{
    [EmpireDestructiveTest("Destructive.Military")]
    public static void HireSquad_AddsMercenarySquad()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        MilSquadFC template = DestructiveTestUtil.EnsureSquadTemplate();
        DestructiveTestUtil.EnsureSilverToHire(template);

        int before = FindFC.Military.mercenarySquads.Count;
        MercenarySquadFC squad = null;
        TestAssert.DoesNotThrow(() => squad = FindFC.Military.HireSquad(template), "HireSquad threw");
        TestAssert.NotNull(squad, "HireSquad returned null despite enough silver for the hire cost");

        TestAssert.AreEqual(before + 1, FindFC.Military.mercenarySquads.Count, "Hiring should add one squad");
        TestAssert.IsTrue(FindFC.Military.mercenarySquads.Contains(squad), "Hired squad should be in the pool");
        DestructiveTestUtil.AssertEmpireInvariants(f, "HireSquad_AddsMercenarySquad");
    }

    [EmpireDestructiveTest("Destructive.Military")]
    public static void DismissSquad_RemovesIt()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        MercenarySquadFC squad = DestructiveTestUtil.EnsureHiredSquad();

        bool dismissed = false;
        TestAssert.DoesNotThrow(() => dismissed = FindFC.Military.DismissSquad(squad), "DismissSquad threw");
        TestAssert.IsTrue(dismissed, "DismissSquad should succeed for a non-busy squad");
        TestAssert.IsFalse(FindFC.Military.mercenarySquads.Contains(squad), "Dismissed squad should be gone from the pool");
        DestructiveTestUtil.AssertEmpireInvariants(f, "DismissSquad_RemovesIt");
    }

    [EmpireDestructiveTest("Destructive.Military")]
    public static void AssignUnassign_RoundTrips()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        MercenarySquadFC squad = DestructiveTestUtil.EnsureHiredSquad();

        // A settlement with a free billet slot (a fresh one if every existing billet is full).
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement(s =>
            s.MilitaryComp is not null && SquadAssignmentRegistry.CanAssign(s, squad, out _));

        bool assigned = false;
        TestAssert.DoesNotThrow(() => assigned = FindFC.Military.AttemptToAssign(squad, settlement), "AttemptToAssign threw");
        TestAssert.IsTrue(assigned, "AttemptToAssign should succeed at a settlement whose validators accept the squad");

        TestAssert.IsTrue(squad.settlement == settlement, "Squad should be billeted at the settlement");
        bool unassigned = false;
        TestAssert.DoesNotThrow(() => unassigned = FindFC.Military.Unassign(squad), "Unassign threw");
        TestAssert.IsTrue(unassigned, "Unassign should succeed for a non-busy squad");
        TestAssert.IsNull(squad.settlement, "Squad should have no billet after unassign");
        DestructiveTestUtil.AssertEmpireInvariants(f, "AssignUnassign_RoundTrips");
    }

    [EmpireDestructiveTest("Destructive.Military")]
    public static void CreateDeployOp_RegistersThenUnregister()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        // Deploys from a morale-locked settlement are rejected, so use one that is not locked.
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement(s => !s.SquadDeploymentLocked);

        MercenarySquadFC squad = DestructiveTestUtil.EnsureHiredSquad();
        FindFC.Military.Assign(squad, settlement, true); // ensure source.settlement is set

        MilitaryOperation op = null;
        PlanetTile tile = settlement.Tile;
        TestAssert.DoesNotThrow(() => op = FindFC.MilitaryManager.CreateDeployOp(squad, tile), "CreateDeployOp threw");
        TestAssert.NotNull(op, "CreateDeployOp rejected a deploy from a settlement that is not morale-locked");

        TestAssert.IsTrue(FindFC.MilitaryManager.Active.Contains(op), "Deploy op should be registered as active");
        TestAssert.DoesNotThrow(() => FindFC.MilitaryManager.Unregister(op), "Unregister threw");
        TestAssert.IsFalse(FindFC.MilitaryManager.Active.Contains(op), "Op should be gone from active after Unregister");
        DestructiveTestUtil.AssertEmpireInvariants(f, "CreateDeployOp_RegistersThenUnregister");
    }

    /* Regression: a merc whose loadout is a real (non-blank) design but carries NO apparel,
       weapon, or animals must still count as equipped/spawnable. The old EquippedMercenaries
       predicate required worn apparel OR a weapon OR an animal, so an intentionally
       unarmed/unarmored unit was silently dropped from every spawn path. Generates one real
       pawn and destroys it best-effort afterward. */
    [EmpireDestructiveTest("Destructive.Military")]
    public static void EquippedMercenaries_IncludesUnarmedUnarmoredMerc()
    {
        DestructiveTestUtil.RequireFaction();
        TestAssert.NotNull(FindFC.Military?.blankUnit, "MilitaryFC has no blank unit");

        // A real, non-blank loadout that carries nothing. MilUnitFC(false) already has empty
        // apparel/weapon lists and a valid pawnKind; ClearAllEquipment makes the intent explicit.
        MilUnitFC loadout = MilTemplateFactory.CreateUnit(false);
        loadout.ClearAllEquipment();

        MercenarySquadFC squad = new();
        squad.mercenaries = [];
        Mercenary merc = new(true);
        TestAssert.DoesNotThrow(() => MercenaryPawnFactory.CreateNewPawn(squad, ref merc,
            loadout.pawnKind, loadout.xenotype, loadout.customXenotypeName, loadout),
            "CreateNewPawn threw");
        TestAssert.NotNull(merc?.pawn, "CreateNewPawn did not produce a pawn");
        merc.loadout = loadout;              // non-blank loadout => deployable == true
        squad.mercenaries.Add(merc);

        try
        {
            // Preconditions: the pawn genuinely carries nothing, yet is deployable.
            TestAssert.IsEmpty(merc.pawn.apparel.WornApparel, "test pawn should have no apparel");
            TestAssert.IsEmpty(merc.pawn.equipment.AllEquipmentListForReading, "test pawn should have no weapon");
            TestAssert.IsTrue(merc.deployable, "non-blank loadout should be deployable");

            // The fix: an unarmed/unarmored deployable merc must still be equipped + spawnable.
            TestAssert.Contains(squad.EquippedMercenaries, merc,
                "unarmed/unarmored deployable merc must be in EquippedMercenaries");
            TestAssert.Contains(squad.SpawnableMercenaryPawns, merc.pawn,
                "...and therefore spawnable");
        }
        finally
        {
            // Clean up the free-floating pawn we generated (destructive tests aren't auto-cleaned).
            TestAssert.DoesNotThrow(() =>
            {
                if (merc?.pawn is not null && !merc.pawn.Destroyed) merc.pawn.Destroy();
            });
        }
    }

    [EmpireDestructiveTest("Destructive.Military")]
    public static void HireDismiss_Repeated_x5()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        MilSquadFC template = DestructiveTestUtil.EnsureSquadTemplate();

        for (int i = 0; i < 5; i++)
        {
            DestructiveTestUtil.EnsureSilverToHire(template);
            MercenarySquadFC squad = null;
            TestAssert.DoesNotThrow(() => squad = FindFC.Military.HireSquad(template), $"HireSquad threw on cycle {i}");
            TestAssert.NotNull(squad, $"HireSquad returned null on cycle {i} despite enough silver for the hire cost");
            TestAssert.DoesNotThrow(() => FindFC.Military.DismissSquad(squad), $"DismissSquad threw on cycle {i}");
            DestructiveTestUtil.AssertEmpireInvariants(f, $"HireDismiss cycle {i}");
        }
    }
}
