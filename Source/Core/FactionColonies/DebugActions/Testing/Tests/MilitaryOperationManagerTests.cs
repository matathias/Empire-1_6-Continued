namespace FactionColonies;

/* Tests for MilitaryOperationManager queries and indices. Each test builds a fresh manager
   in isolation — no FactionCache.MilitaryManager interaction — so they don't perturb live
   game state. Synthetic ops are unregistered with the same fresh manager; nothing leaks. */
public static class MilitaryOperationManagerTests
{
    internal static MilitaryOperation MakeOp(MilitaryJobDef kind, PlanetTile tile, WorldSettlementFC home = null, WorldSettlementFC target = null)
    {
        var op = new MilitaryOperation(-1, kind, tile, target);
        if (home is not null) op.aggressor.homeSettlement = home;
        return op;
    }

    /* An engaged, auto-resolving defensive op: targets one settlement, defended by (possibly a
       foreign) squad billeted at another. battleResult non-null + phase Engaged is what the
       "watch battle" gizmo lookup (FindWatchBattleOp) filters on. */
    internal static MilitaryOperation MakeEngagedDefensiveOp(WorldSettlementFC target, WorldSettlementFC homeDefender)
    {
        var op = new MilitaryOperation(-1, null, target.Tile, target);
        op.defender.homeSettlement = homeDefender;
        op.phase = MilitaryOperationPhase.Engaged;
        op.battleResult = new BattleResult();
        return op;
    }

    // -*- Register / Unregister -*-

    [EmpireTest("Military")]
    public static void Register_AddsToActive()
    {
        var manager = new MilitaryOperationManager();
        var op = MakeOp(null, new PlanetTile(1));
        manager.Register(op);
        TestAssert.AreEqual(1, manager.Active.Count);
        TestAssert.IsTrue(System.Object.ReferenceEquals(op, manager.Active[0]));
    }

    [EmpireTest("Military")]
    public static void Register_Idempotent_DoesNotDuplicate()
    {
        var manager = new MilitaryOperationManager();
        var op = MakeOp(null, new PlanetTile(1));
        manager.Register(op);
        manager.Register(op);
        TestAssert.AreEqual(1, manager.Active.Count,
            "Registering the same op twice should be a no-op");
    }

    [EmpireTest("Military")]
    public static void Register_NullOp_NoOp()
    {
        var manager = new MilitaryOperationManager();
        TestAssert.DoesNotThrow(() => manager.Register(null));
        TestAssert.AreEqual(0, manager.Active.Count);
    }

    [EmpireTest("Military")]
    public static void Unregister_RemovesFromActive()
    {
        var manager = new MilitaryOperationManager();
        var op = MakeOp(null, new PlanetTile(1));
        manager.Register(op);
        manager.Unregister(op);
        TestAssert.AreEqual(0, manager.Active.Count);
    }

    [EmpireTest("Military")]
    public static void Unregister_RemovesFromByTileIndex()
    {
        var manager = new MilitaryOperationManager();
        var tile = new PlanetTile(42);
        var op = MakeOp(null, tile);
        manager.Register(op);
        manager.Unregister(op);
        TestAssert.IsEmpty(manager.GetOpsAt(tile));
    }

    [EmpireTest("Military")]
    public static void Unregister_NullOp_NoOp()
    {
        var manager = new MilitaryOperationManager();
        TestAssert.DoesNotThrow(() => manager.Unregister(null));
    }

    // -*- Index queries -*-

    [EmpireTest("Military")]
    public static void GetOpsAt_FindsRegisteredOp()
    {
        var manager = new MilitaryOperationManager();
        var tile = new PlanetTile(7);
        var op = MakeOp(null, tile);
        manager.Register(op);
        TestAssert.Contains(manager.GetOpsAt(tile), op);
    }

    [EmpireTest("Military")]
    public static void GetOpsAt_EmptyTile_ReturnsEmpty()
    {
        var manager = new MilitaryOperationManager();
        TestAssert.IsEmpty(manager.GetOpsAt(new PlanetTile(99)));
    }

    [EmpireTest("Military")]
    public static void GetOpsAt_MultipleOps_SameTile()
    {
        var manager = new MilitaryOperationManager();
        var tile = new PlanetTile(5);
        var op1 = MakeOp(null, tile);
        var op2 = MakeOp(null, tile);
        manager.Register(op1);
        manager.Register(op2);

        var ops = manager.GetOpsAt(tile);
        TestAssert.AreEqual(2, ((System.Collections.Generic.IReadOnlyList<MilitaryOperation>)ops).Count);
    }

    [EmpireTest("Military")]
    public static void GetOpsForSettlement_NullSettlement_ReturnsEmpty()
    {
        var manager = new MilitaryOperationManager();
        TestAssert.IsEmpty(manager.GetOpsForSettlement(null));
    }

    [EmpireTest("Military")]
    public static void HasOffensiveOpFrom_NullSettlement_ReturnsFalse()
    {
        var manager = new MilitaryOperationManager();
        TestAssert.IsFalse(manager.HasOffensiveOpFrom(null));
    }

    [EmpireTest("Military")]
    public static void IsTileOccupiedBy_MatchesKind()
    {
        var manager = new MilitaryOperationManager();
        var tile = new PlanetTile(1);
        var op = MakeOp(MilitaryJobDefOf.RaidEnemySettlement, tile);
        manager.Register(op);

        TestAssert.IsTrue(manager.IsTileOccupiedBy(tile, MilitaryJobDefOf.RaidEnemySettlement));
        TestAssert.IsFalse(manager.IsTileOccupiedBy(tile, MilitaryJobDefOf.Cooldown));
    }

    // -*- HasDefenseAt phase filter -*-

    [EmpireTest("Military")]
    public static void HasDefenseAt_NullSettlement_False()
    {
        var manager = new MilitaryOperationManager();
        TestAssert.IsFalse(manager.HasDefenseAt(null));
    }

    // -*- IsEmpty -*-

    [EmpireTest("Military")]
    public static void IsEmpty_FreshManager_True()
    {
        var manager = new MilitaryOperationManager();
        TestAssert.IsTrue(manager.IsEmpty);
    }

    [EmpireTest("Military")]
    public static void IsEmpty_WithRegisteredOp_False()
    {
        var manager = new MilitaryOperationManager();
        var op = MakeOp(null, new PlanetTile(1));
        manager.Register(op);
        TestAssert.IsFalse(manager.IsEmpty);
    }

    // -*- RebuildIndices -*-

    [EmpireTest("Military")]
    public static void RebuildIndices_RecoversByTileAfterClear()
    {
        // Simulates a post-load scenario where indices are empty but `active` holds ops.
        // The test path: register, unregister (clears indices), poke an op back onto active
        // directly, then RebuildIndices and check the index repopulated.
        var manager = new MilitaryOperationManager();
        var tile = new PlanetTile(13);
        var op = MakeOp(null, tile);
        manager.active.Add(op); // bypass Register to mimic post-load `active` collection
        manager.RebuildIndices();
        TestAssert.Contains(manager.GetOpsAt(tile), op);
    }
}
