namespace FactionColonies;

/* DESTRUCTIVE: may create real player settlements to stand in as op homes/targets. Each test
   still queries a fresh, unregistered MilitaryOperationManager, so no op reaches the live one.
   Settlements are not removed. */
public static class MilitaryOperationManagerDestructiveTests
{
    /* Regression for the "only one settlement shows the battle-in-progress gizmo" bug: when a
       foreign auto-defender defends settlement B, both B's op and A's op carry
       defender.homeSettlement == A. FindWatchBattleOp must select by op.targetObject so B (the real
       target) gets its own watch op and A doesn't hijack B's battle. */
    [EmpireDestructiveTest("Destructive.Military")]
    public static void FindWatchBattleOp_MatchesTarget_NotDefenderHome()
    {
        var settlements = DestructiveTestUtil.EnsureSettlements(2);
        WorldSettlementFC a = settlements[0];
        WorldSettlementFC b = settlements[1];
        TestAssert.IsFalse(System.Object.ReferenceEquals(a, b), "EnsureSettlements(2) should return two distinct settlements");

        // op targeting A defended by A's own squad; op targeting B defended by a FOREIGN squad from A.
        // Both have defender.homeSettlement == A — the shape that broke the old defender-home matching.
        var opA = MilitaryOperationManagerTests.MakeEngagedDefensiveOp(a, homeDefender: a);
        var opB = MilitaryOperationManagerTests.MakeEngagedDefensiveOp(b, homeDefender: a);

        // opB first, so a defender-home match for A would wrongly return opB; target-based returns opA.
        var ops = new System.Collections.Generic.List<MilitaryOperation> { opB, opA };

        TestAssert.IsTrue(
            System.Object.ReferenceEquals(opA, WorldObjectComp_SettlementMilitary.FindWatchBattleOp(ops, a)),
            "A's watch op must be the op targeting A, not the foreign-defended op targeting B");
        TestAssert.IsTrue(
            System.Object.ReferenceEquals(opB, WorldObjectComp_SettlementMilitary.FindWatchBattleOp(ops, b)),
            "B (foreign-defended target) must get its own watch op, not be skipped");
    }

    [EmpireDestructiveTest("Destructive.Military")]
    public static void GetOpsForSettlement_HomeSide()
    {
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        var manager = new MilitaryOperationManager();
        var op = MilitaryOperationManagerTests.MakeOp(null, new PlanetTile(1), home: settlement);
        manager.Register(op);
        TestAssert.Contains(manager.GetOpsForSettlement(settlement), op);
    }

    [EmpireDestructiveTest("Destructive.Military")]
    public static void HasOffensiveOpFrom_FindsByAggressorHome()
    {
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        var manager = new MilitaryOperationManager();
        var op = MilitaryOperationManagerTests.MakeOp(MilitaryJobDefOf.RaidEnemySettlement, new PlanetTile(1), home: settlement);
        manager.Register(op);
        TestAssert.IsTrue(manager.HasOffensiveOpFrom(settlement));
    }

    [EmpireDestructiveTest("Destructive.Military")]
    public static void HasDefenseAt_EngagedPhase_True()
    {
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        var manager = new MilitaryOperationManager();
        var op = MilitaryOperationManagerTests.MakeOp(MilitaryJobDefOf.DefendOwnSettlement, settlement.Tile, target: settlement);
        op.phase = MilitaryOperationPhase.Engaged;
        manager.Register(op);

        TestAssert.IsTrue(manager.HasDefenseAt(settlement),
            "Engaged defensive op should count as 'under attack'");
    }

    [EmpireDestructiveTest("Destructive.Military")]
    public static void HasDefenseAt_ScheduledPhase_True()
    {
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        var manager = new MilitaryOperationManager();
        var op = MilitaryOperationManagerTests.MakeOp(MilitaryJobDefOf.DefendOwnSettlement, settlement.Tile, target: settlement);
        op.phase = MilitaryOperationPhase.Scheduled;
        manager.Register(op);

        TestAssert.IsTrue(manager.HasDefenseAt(settlement),
            "Scheduled defensive op (warning still pending) should count as 'under attack'");
    }

    [EmpireDestructiveTest("Destructive.Military")]
    public static void HasDefenseAt_CooldownPending_False()
    {
        // Phase filter in HasDefenseAt — CooldownPending/Resolved excluded.
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        var manager = new MilitaryOperationManager();
        var op = MilitaryOperationManagerTests.MakeOp(MilitaryJobDefOf.DefendOwnSettlement, settlement.Tile, target: settlement);
        op.phase = MilitaryOperationPhase.CooldownPending;
        manager.Register(op);

        TestAssert.IsFalse(manager.HasDefenseAt(settlement),
            "Battle in cooldown is no longer 'under attack'");
    }

    [EmpireDestructiveTest("Destructive.Military")]
    public static void HasDefenseAt_Resolved_False()
    {
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        var manager = new MilitaryOperationManager();
        var op = MilitaryOperationManagerTests.MakeOp(MilitaryJobDefOf.DefendOwnSettlement, settlement.Tile, target: settlement);
        op.phase = MilitaryOperationPhase.Resolved;
        manager.Register(op);

        TestAssert.IsFalse(manager.HasDefenseAt(settlement));
    }
}
