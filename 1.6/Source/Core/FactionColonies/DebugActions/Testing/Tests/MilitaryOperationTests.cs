using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace FactionColonies
{
    /* Tests for MilitaryOperation phase-machine guards and BuildBattleContext. Constructed
       as standalone ops (no manager registration, no game state mutation). */
    public static class MilitaryOperationTests
    {
        private static MilitaryOperation Make(MilitaryOperationPhase phase = MilitaryOperationPhase.Scheduled)
        {
            var op = new MilitaryOperation(-1, null, PlanetTile.Invalid, null);
            op.phase = phase;
            return op;
        }

        // -*- CompleteBattle re-entry guard -*-

        [EmpireTest("Military")]
        public static void CompleteBattle_NotEngagedPhase_Ignored()
        {
            // Re-entry guard in CompleteBattle: only Engaged ops accept it.
            var op = Make(MilitaryOperationPhase.Scheduled);
            TestAssert.DoesNotThrow(() => op.CompleteBattle(new BattleResult { winner = BattleWinner.Attacker }));
            TestAssert.IsNull(op.result, "Result should not be assigned on out-of-phase CompleteBattle");
            TestAssert.AreEqual((object)MilitaryOperationPhase.Scheduled, (object)op.phase,
                "Phase should remain Scheduled");
        }

        [EmpireTest("Military")]
        public static void CompleteBattle_ResolvedPhase_Ignored()
        {
            var op = Make(MilitaryOperationPhase.Resolved);
            int phaseStartTickBefore = op.phaseStartedTick;
            TestAssert.DoesNotThrow(() => op.CompleteBattle(new BattleResult { winner = BattleWinner.Attacker }));
            TestAssert.IsNull(op.result);
            TestAssert.AreEqual((object)MilitaryOperationPhase.Resolved, (object)op.phase);
        }

        // -*- EnterCooldown re-entry guard -*-

        [EmpireTest("Military")]
        public static void EnterCooldown_AlreadyCooldownPending_Ignored()
        {
            // Re-entry guard in EnterCooldown — early-return on CooldownPending/Resolved.
            var op = Make(MilitaryOperationPhase.CooldownPending);
            int phaseStartBefore = op.phaseStartedTick;
            TestAssert.DoesNotThrow(() => op.EnterCooldown());
            TestAssert.AreEqual((object)MilitaryOperationPhase.CooldownPending, (object)op.phase);
            TestAssert.AreEqual(phaseStartBefore, op.phaseStartedTick,
                "phaseStartedTick should not be re-stamped on re-entry");
        }

        [EmpireTest("Military")]
        public static void EnterCooldown_Resolved_Ignored()
        {
            var op = Make(MilitaryOperationPhase.Resolved);
            int phaseStartBefore = op.phaseStartedTick;
            TestAssert.DoesNotThrow(() => op.EnterCooldown());
            TestAssert.AreEqual((object)MilitaryOperationPhase.Resolved, (object)op.phase);
            TestAssert.AreEqual(phaseStartBefore, op.phaseStartedTick);
        }

        // -*- Resolve idempotency -*-

        [EmpireTest("Military")]
        public static void Resolve_AlreadyResolved_NoOp()
        {
            // Idempotency guard in Resolve — already-Resolved is a no-op.
            var op = Make(MilitaryOperationPhase.Resolved);
            int phaseStartBefore = op.phaseStartedTick;
            TestAssert.DoesNotThrow(() => op.Resolve());
            TestAssert.AreEqual((object)MilitaryOperationPhase.Resolved, (object)op.phase);
            TestAssert.AreEqual(phaseStartBefore, op.phaseStartedTick,
                "phaseStartedTick should not change on idempotent Resolve");
        }

        // -*- Resolve releases a pledged external auto-defender (IAutoDefender contract) -*-

        [EmpireTest("Military")]
        public static void Resolve_PreBattleWithExternalDefender_FiresReplaced()
        {
            // Pre-battle teardown (e.g. the target settlement is removed during the warning window):
            // the pledged external defender must be released via OnDefenseReplaced, not left stuck.
            var stub = new StubAutoDefender { worldObject = new WorldObject() };
            AutoDefenderRegistry.Register(stub);
            try
            {
                var op = Make(MilitaryOperationPhase.Scheduled);
                op.externalDefenderSource = stub.worldObject;

                op.Resolve();

                TestAssert.IsTrue(stub.replacedCalled,
                    "OnDefenseReplaced should fire when a pledged op is torn down pre-battle");
                TestAssert.IsFalse(stub.completeCalled,
                    "OnDefenseComplete must not fire on a battle-less teardown");
                TestAssert.IsNull(op.externalDefenderSource,
                    "externalDefenderSource should be cleared after release");
            }
            finally
            {
                AutoDefenderRegistry.Unregister(stub);
            }
        }

        [EmpireTest("Military")]
        public static void Resolve_CooldownWithExternalDefender_DoesNotFireReplaced()
        {
            // Post-battle teardown: CompleteBattle already fired OnDefenseComplete, so Resolve must
            // NOT fire OnDefenseReplaced (that would be a double terminal callback).
            var stub = new StubAutoDefender { worldObject = new WorldObject() };
            AutoDefenderRegistry.Register(stub);
            try
            {
                var op = Make(MilitaryOperationPhase.CooldownPending);
                op.externalDefenderSource = stub.worldObject;

                op.Resolve();

                TestAssert.IsFalse(stub.replacedCalled,
                    "OnDefenseReplaced must not fire once the battle already completed (CooldownPending)");
            }
            finally
            {
                AutoDefenderRegistry.Unregister(stub);
            }
        }

        /* Minimal IAutoDefender that records which terminal callback fired and keys off a bare
           WorldObject sentinel (AutoDefenderRegistry.FindByWorldObject matches by reference). */
        private class StubAutoDefender : IAutoDefender
        {
            public WorldObject worldObject;
            public bool replacedCalled;
            public bool completeCalled;

            public WorldObject WorldObject => worldObject;
            public int MilitaryLevel => 0;
            public int Range => 0;
            public bool CanAutoDefend => false;
            public MilitaryForce CreateDefendingForce() => null;
            public void OnDefensePledged(WorldObject target) { }
            public void OnDefenseStarted(WorldObject target) { }
            public void OnDefenseComplete(bool won, BattleResult result) { completeCalled = true; }
            public void OnDefenseReplaced() { replacedCalled = true; }
            public List<Pawn> GetDefendingPawns() => null;
            public void ReturnDefendingPawns(List<Pawn> pawns) { }
        }

        // -*- BuildBattleContext -*-

        [EmpireTest("Military")]
        public static void BuildBattleContext_ReflectsParticipantsAndTarget()
        {
            var op = new MilitaryOperation(-1, MilitaryJobDefOf.RaidEnemySettlement, new PlanetTile(42), null);
            op.aggressor.faction = FindFC.EmpireFaction;

            BattleForceContext ctx = op.BuildBattleContext();
            TestAssert.IsNotNull(ctx);
            TestAssert.IsTrue(ReferenceEquals(MilitaryJobDefOf.RaidEnemySettlement, ctx.kind),
                "kind should propagate");
            TestAssert.IsTrue(ctx.targetTile.tileId == 42, "targetTile should propagate");
            TestAssert.IsTrue(ReferenceEquals(op.aggressor, ctx.aggressor),
                "aggressor participant should propagate by reference");
            TestAssert.IsTrue(ReferenceEquals(op.defender, ctx.defender),
                "defender participant should propagate by reference");
        }

        // -*- IsDefensive / IsOffensive -*-

        [EmpireTest("Military")]
        public static void IsDefensive_NoFaction_False()
        {
            var op = Make();
            TestAssert.IsFalse(op.IsDefensive);
            TestAssert.IsFalse(op.IsOffensive);
        }

        [EmpireTest("Military")]
        public static void IsDefensive_PlayerFactionAsDefender_True()
        {
            if (FindFC.EmpireFaction is null)
                TestAssert.Skip("No player colony faction");

            var op = Make();
            op.defender.faction = FindFC.EmpireFaction;
            TestAssert.IsTrue(op.IsDefensive);
            TestAssert.IsFalse(op.IsOffensive);
        }

        [EmpireTest("Military")]
        public static void IsOffensive_PlayerFactionAsAggressor_True()
        {
            if (FindFC.EmpireFaction is null)
                TestAssert.Skip("No player colony faction");

            var op = Make();
            op.aggressor.faction = FindFC.EmpireFaction;
            TestAssert.IsTrue(op.IsOffensive);
            TestAssert.IsFalse(op.IsDefensive);
        }

        // -*- HasMapPresence -*-

        [EmpireTest("Military")]
        public static void HasMapPresence_InvalidBattlefieldRef_False()
        {
            var op = Make();
            TestAssert.IsFalse(op.HasMapPresence);
        }

        [EmpireTest("Military")]
        public static void HasMapPresence_ValidBattlefieldRef_True()
        {
            var op = Make();
            op.battlefieldRef = new PlanetTile(5);
            TestAssert.IsTrue(op.HasMapPresence);
        }

        // -*- GetUniqueLoadID -*-

        [EmpireTest("Military")]
        public static void GetUniqueLoadID_IncludesId()
        {
            var op = new MilitaryOperation(123, null, PlanetTile.Invalid, null);
            string id = op.GetUniqueLoadID();
            TestAssert.AreEqual((object)"MilitaryOperation_123", (object)id);
        }
    }
}
