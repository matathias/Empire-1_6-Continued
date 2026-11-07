namespace FactionColonies;

/* DESTRUCTIVE: chains multiple subsystems together to stress cross-system consistency — the
   harshest tier. Nothing is reverted; the goal is that after the thrash the faction is still
   internally consistent (AssertEmpireInvariants) and the game has not crashed. */
public static class CrossSystemThrashDestructiveTests
{
    [EmpireDestructiveTest("Destructive.Thrash")]
    public static void FullCycle_CreateHireTaxBattleEventLevelEdict()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();

        // 1. Settlement
        WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement() ?? DestructiveTestUtil.EnsureSettlement();

        // 2. Hire + assign a squad
        MercenarySquadFC hired = DestructiveTestUtil.EnsureHiredSquad();
        FindFC.Military.Assign(hired, s, true);

        // 3. Tax cycle
        TestAssert.DoesNotThrow(() => FindFC.TaxLedger.AddTax(f), "AddTax threw");
        TestAssert.DoesNotThrow(() => FindFC.TaxLedger.AutoresolveBills(), "AutoresolveBills threw");
        TestAssert.DoesNotThrow(() => FindFC.TaxLedger.ProcessBills(), "ProcessBills threw");

        // 4. Battle: apply a real defensive loss to the settlement
        var op = new MilitaryOperation(-1, MilitaryJobDefOf.DefendOwnSettlement, s.Tile, s);
        op.defender.faction = FindFC.EmpireFaction;
        op.defender.homeSettlement = s;
        op.defender.force = new MilitaryForce(5.0, 1.0, s, FindFC.EmpireFaction);
        op.aggressor.force = new MilitaryForce(8.0, 1.0, null, null);
        op.phase = MilitaryOperationPhase.Engaged;
        TestAssert.DoesNotThrow(() => op.CompleteBattle(new BattleResult { winner = BattleWinner.Attacker }),
            "CompleteBattle threw");
        DestructiveTestUtil.ResolveAndSweepOp(op);

        // 5. Event
        FCEvent evt = null;
        TestAssert.DoesNotThrow(() => evt = FCEventMaker.MakeEvent(FCEventDefOf.cooldownMilitary), "MakeEvent threw");
        if (evt is not null)
        {
            TestAssert.DoesNotThrow(() => FindFC.EventManager.AddEvent(evt), "AddEvent threw");
            TestAssert.DoesNotThrow(() => FindFC.EventManager.RemoveWhere(e => ReferenceEquals(e, evt)), "RemoveWhere threw");
        }

        // 6. Faction leveling
        TestAssert.DoesNotThrow(() => f.AddExperienceToFactionLevel(100000f), "AddExperienceToFactionLevel threw");

        // 7. Edicts
        TestAssert.DoesNotThrow(() =>
        {
            FCPolicyDef edict = DefDatabase<FCPolicyDef>.AllDefsListForReading
                .FirstOrDefault(d => d.IsEdict && !FindFC.PolicyManager.HasEdict(d));
            if (edict is not null) FindFC.PolicyManager.EnactEdict(edict);
            FindFC.PolicyManager.RevokeAllEdicts();
        }, "Edict enact/revoke threw");

        DestructiveTestUtil.AssertEmpireInvariants(f, "FullCycle_CreateHireTaxBattleEventLevelEdict");
    }

    [EmpireDestructiveTest("Destructive.Thrash")]
    public static void OrphanSweep_RemoveSettlementDuringOp()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();

        WorldSettlementFC s = DestructiveTestUtil.RequireTransientSettlement();

        // Stand up a deploy op from the settlement so removal must cancel it.
        MercenarySquadFC squad = DestructiveTestUtil.EnsureHiredSquad();
        FindFC.Military.Assign(squad, s, true);
        PlanetTile tile = s.Tile;
        MilitaryOperation op = null;
        TestAssert.DoesNotThrow(() => op = FindFC.MilitaryManager.CreateDeployOp(squad, tile), "CreateDeployOp threw");
        TestAssert.NotNull(op, "CreateDeployOp rejected a deploy from a freshly founded settlement");

        // Remove the settlement out from under any op referencing it.
        DestructiveTestUtil.SafeRemoveSettlement(s);

        if (FindFC.MilitaryManager?.Active is not null)
        {
            TestAssert.IsFalse(FindFC.MilitaryManager.Active.Any(o =>
                    o is not null && (o.aggressor?.homeSettlement == s || o.defender?.homeSettlement == s || o.targetObject == s)),
                "No active op should still reference the removed settlement");
        }
        DestructiveTestUtil.AssertEmpireInvariants(f, "OrphanSweep_RemoveSettlementDuringOp");
    }

    [EmpireDestructiveTest("Destructive.Thrash")]
    public static void RapidCreateDestroy_x3()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        int baseline = f.settlements.Count;

        for (int round = 0; round < 3; round++)
        {
            WorldSettlementFC a = DestructiveTestUtil.RequireTransientSettlement();
            WorldSettlementFC b = DestructiveTestUtil.RequireTransientSettlement();

            TestAssert.DoesNotThrow(() => FindFC.TaxLedger.AddTax(f), $"AddTax threw on round {round}");

            DestructiveTestUtil.SafeRemoveSettlement(a);
            DestructiveTestUtil.SafeRemoveSettlement(b);
            DestructiveTestUtil.AssertEmpireInvariants(f, $"RapidCreateDestroy round {round}");
        }

        TestAssert.AreEqual(baseline, f.settlements.Count, "Settlement count should return to baseline");
    }
}
