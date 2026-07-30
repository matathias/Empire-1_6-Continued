using RimWorld.Planet;

namespace FactionColonies
{
    /* Tests for MilitaryOperationManager queries and indices. Each test builds a fresh manager
       in isolation — no FactionCache.MilitaryManager interaction — so they don't perturb live
       game state. Synthetic ops are unregistered with the same fresh manager; nothing leaks. */
    public static class MilitaryOperationManagerTests
    {
        private static MilitaryOperation MakeOp(MilitaryJobDef kind, PlanetTile tile, WorldSettlementFC home = null, WorldSettlementFC target = null)
        {
            var op = new MilitaryOperation(-1, kind, tile, target);
            if (home is object) op.aggressor.homeSettlement = home;
            return op;
        }

        /* An engaged, auto-resolving defensive op: targets one settlement, defended by (possibly a
           foreign) squad billeted at another. battleResult non-null + phase Engaged is what the
           "watch battle" gizmo lookup (FindWatchBattleOp) filters on. */
        private static MilitaryOperation MakeEngagedDefensiveOp(WorldSettlementFC target, WorldSettlementFC homeDefender)
        {
            var op = new MilitaryOperation(-1, null, target.Tile, target);
            op.defender.homeSettlement = homeDefender;
            op.phase = MilitaryOperationPhase.Engaged;
            op.battleResult = new BattleResult();
            return op;
        }

        private static WorldSettlementFC FirstSettlement()
        {
            var settlements = FindFC.Settlements;
            if (settlements is null || settlements.Count == 0) return null;
            return settlements[0];
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

        // -*- Watch-battle gizmo op selection (target-based, not defender-based) -*-

        /* Regression for the "only one settlement shows the battle-in-progress gizmo" bug: when a
           foreign auto-defender defends settlement B, both B's op and A's op carry
           defender.homeSettlement == A. FindWatchBattleOp must select by op.targetObject so B (the real
           target) gets its own watch op and A doesn't hijack B's battle. */
        [EmpireTest("Military")]
        public static void FindWatchBattleOp_MatchesTarget_NotDefenderHome()
        {
            var settlements = FindFC.Settlements;
            if (settlements is null || settlements.Count < 2) TestAssert.Skip("Needs >= 2 settlements");
            WorldSettlementFC a = settlements[0];
            WorldSettlementFC b = settlements[1];
            if (a is null || b is null || System.Object.ReferenceEquals(a, b))
                TestAssert.Skip("Need two distinct settlements");

            // op targeting A defended by A's own squad; op targeting B defended by a FOREIGN squad from A.
            // Both have defender.homeSettlement == A — the shape that broke the old defender-home matching.
            var opA = MakeEngagedDefensiveOp(a, homeDefender: a);
            var opB = MakeEngagedDefensiveOp(b, homeDefender: a);

            // opB first, so a defender-home match for A would wrongly return opB; target-based returns opA.
            var ops = new System.Collections.Generic.List<MilitaryOperation> { opB, opA };

            TestAssert.IsTrue(
                System.Object.ReferenceEquals(opA, WorldObjectComp_SettlementMilitary.FindWatchBattleOp(ops, a)),
                "A's watch op must be the op targeting A, not the foreign-defended op targeting B");
            TestAssert.IsTrue(
                System.Object.ReferenceEquals(opB, WorldObjectComp_SettlementMilitary.FindWatchBattleOp(ops, b)),
                "B (foreign-defended target) must get its own watch op, not be skipped");
        }

        [EmpireTest("Military")]
        public static void GetOpsForSettlement_HomeSide()
        {
            WorldSettlementFC settlement = FirstSettlement();
            if (settlement is null) TestAssert.Skip("No settlements");

            var manager = new MilitaryOperationManager();
            var op = MakeOp(null, new PlanetTile(1), home: settlement);
            manager.Register(op);
            TestAssert.Contains(manager.GetOpsForSettlement(settlement), op);
        }

        [EmpireTest("Military")]
        public static void GetOpsForSettlement_NullSettlement_ReturnsEmpty()
        {
            var manager = new MilitaryOperationManager();
            TestAssert.IsEmpty(manager.GetOpsForSettlement(null));
        }

        [EmpireTest("Military")]
        public static void HasOffensiveOpFrom_FindsByAggressorHome()
        {
            WorldSettlementFC settlement = FirstSettlement();
            if (settlement is null) TestAssert.Skip("No settlements");

            var manager = new MilitaryOperationManager();
            var op = MakeOp(MilitaryJobDefOf.RaidEnemySettlement, new PlanetTile(1), home: settlement);
            manager.Register(op);
            TestAssert.IsTrue(manager.HasOffensiveOpFrom(settlement));
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
        public static void HasDefenseAt_EngagedPhase_True()
        {
            WorldSettlementFC settlement = FirstSettlement();
            if (settlement is null) TestAssert.Skip("No settlements");

            var manager = new MilitaryOperationManager();
            var op = MakeOp(MilitaryJobDefOf.DefendOwnSettlement, settlement.Tile, target: settlement);
            op.phase = MilitaryOperationPhase.Engaged;
            manager.Register(op);

            TestAssert.IsTrue(manager.HasDefenseAt(settlement),
                "Engaged defensive op should count as 'under attack'");
        }

        [EmpireTest("Military")]
        public static void HasDefenseAt_ScheduledPhase_True()
        {
            WorldSettlementFC settlement = FirstSettlement();
            if (settlement is null) TestAssert.Skip("No settlements");

            var manager = new MilitaryOperationManager();
            var op = MakeOp(MilitaryJobDefOf.DefendOwnSettlement, settlement.Tile, target: settlement);
            op.phase = MilitaryOperationPhase.Scheduled;
            manager.Register(op);

            TestAssert.IsTrue(manager.HasDefenseAt(settlement),
                "Scheduled defensive op (warning still pending) should count as 'under attack'");
        }

        [EmpireTest("Military")]
        public static void HasDefenseAt_CooldownPending_False()
        {
            // Phase filter in HasDefenseAt — CooldownPending/Resolved excluded.
            WorldSettlementFC settlement = FirstSettlement();
            if (settlement is null) TestAssert.Skip("No settlements");

            var manager = new MilitaryOperationManager();
            var op = MakeOp(MilitaryJobDefOf.DefendOwnSettlement, settlement.Tile, target: settlement);
            op.phase = MilitaryOperationPhase.CooldownPending;
            manager.Register(op);

            TestAssert.IsFalse(manager.HasDefenseAt(settlement),
                "Battle in cooldown is no longer 'under attack'");
        }

        [EmpireTest("Military")]
        public static void HasDefenseAt_Resolved_False()
        {
            WorldSettlementFC settlement = FirstSettlement();
            if (settlement is null) TestAssert.Skip("No settlements");

            var manager = new MilitaryOperationManager();
            var op = MakeOp(MilitaryJobDefOf.DefendOwnSettlement, settlement.Tile, target: settlement);
            op.phase = MilitaryOperationPhase.Resolved;
            manager.Register(op);

            TestAssert.IsFalse(manager.HasDefenseAt(settlement));
        }

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
}
