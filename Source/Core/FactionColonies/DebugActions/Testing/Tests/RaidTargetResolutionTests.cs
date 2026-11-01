using System;
using RimWorld.Planet;

namespace FactionColonies
{
    /* Regression tests for MilitaryOperation.CompleteBattle's IRaidTarget dispatch (review issue H3).
       Before the fix, an externally-registered IRaidTarget attacked by an enemy never received
       OnRaidWon/OnRaidLost and its IsUnderAttack flag was never cleared, so the raid-selection pool
       (FactionFC filters on !IsUnderAttack) permanently excluded it for the rest of the save.

       These build a minimal Engaged op whose targetObject is the target's WorldObject, drive
       CompleteBattle, and assert the correct callback fired and the flag was cleared. defender.faction
       is set to the empire so IsDefensive is true and the op's `victory` == DefenderVictory (the raid
       target is the defender). Squads are left null, so CompleteBattle's EnterCooldown short-circuits
       to a safe Resolve() on this never-registered synthetic op. */
    public static class RaidTargetResolutionTests
    {
        private class CountingRaidTarget : IRaidTarget
        {
            private readonly WorldObject _obj;
            public int WonCount;
            public int LostCount;
            public bool ThrowOnCallback;

            public CountingRaidTarget(WorldObject obj) { _obj = obj; }

            public WorldObject WorldObject => _obj;
            public string Name => "TestCountingRaidTarget";
            public PlanetTile Tile => PlanetTile.Invalid;
            public int MilitaryLevel => 1;
            public bool IsUnderAttack { get; set; }

            public void OnRaidWon(BattleResult result)
            {
                WonCount++;
                if (ThrowOnCallback) throw new InvalidOperationException("test");
            }

            public void OnRaidLost(BattleResult result)
            {
                LostCount++;
                if (ThrowOnCallback) throw new InvalidOperationException("test");
            }
        }

        /// <summary>
        /// Minimal Engaged defensive op whose targetObject is <paramref name="targetObj"/>. Not
        /// registered with the manager — a transient stand-in that resolves itself in CompleteBattle.
        /// </summary>
        private static MilitaryOperation MakeEngagedRaidOp(WorldObject targetObj)
        {
            var op = new MilitaryOperation(-1, null, PlanetTile.Invalid, targetObj);
            op.defender.faction = FindFC.EmpireFaction; // IsDefensive => victory == DefenderVictory
            op.phase = MilitaryOperationPhase.Engaged;  // CompleteBattle requires Engaged
            return op;
        }

        private static BattleResult MakeResult(BattleWinner winner) => new BattleResult { winner = winner };

        [EmpireTest("Battle")]
        public static void RaidTarget_DefenderVictory_FiresOnRaidWon_AndClearsFlag()
        {
            if (FindFC.EmpireFaction is null) TestAssert.Skip("No player colony faction");

            var obj = new WorldObject();
            var target = new CountingRaidTarget(obj) { IsUnderAttack = true };
            RaidTargetRegistry.Register(target);
            try
            {
                MakeEngagedRaidOp(obj).CompleteBattle(MakeResult(BattleWinner.Defender));
                TestAssert.AreEqual(1, target.WonCount, "OnRaidWon should fire on a defender victory");
                TestAssert.AreEqual(0, target.LostCount, "OnRaidLost should not fire on a defender victory");
                TestAssert.IsFalse(target.IsUnderAttack, "IsUnderAttack should be cleared on resolution");
            }
            finally { RaidTargetRegistry.Unregister(target); }
        }

        [EmpireTest("Battle")]
        public static void RaidTarget_AttackerVictory_FiresOnRaidLost_AndClearsFlag()
        {
            if (FindFC.EmpireFaction is null) TestAssert.Skip("No player colony faction");

            var obj = new WorldObject();
            var target = new CountingRaidTarget(obj) { IsUnderAttack = true };
            RaidTargetRegistry.Register(target);
            try
            {
                MakeEngagedRaidOp(obj).CompleteBattle(MakeResult(BattleWinner.Attacker));
                TestAssert.AreEqual(1, target.LostCount, "OnRaidLost should fire on an attacker victory");
                TestAssert.AreEqual(0, target.WonCount, "OnRaidWon should not fire on an attacker victory");
                TestAssert.IsFalse(target.IsUnderAttack, "IsUnderAttack should be cleared on resolution");
            }
            finally { RaidTargetRegistry.Unregister(target); }
        }

        [EmpireTest("Battle")]
        public static void RaidTarget_ErrorResult_SkipsCallbacks_ButClearsFlag()
        {
            if (FindFC.EmpireFaction is null) TestAssert.Skip("No player colony faction");

            var obj = new WorldObject();
            var target = new CountingRaidTarget(obj) { IsUnderAttack = true };
            RaidTargetRegistry.Register(target);
            try
            {
                MakeEngagedRaidOp(obj).CompleteBattle(MakeResult(BattleWinner.Error));
                TestAssert.AreEqual(0, target.WonCount, "No win/loss callback on an Error result");
                TestAssert.AreEqual(0, target.LostCount, "No win/loss callback on an Error result");
                TestAssert.IsFalse(target.IsUnderAttack,
                    "IsUnderAttack must still be cleared on an Error result so the target isn't stranded");
            }
            finally { RaidTargetRegistry.Unregister(target); }
        }

        [EmpireTest("Battle")]
        public static void RaidTarget_ThrowingHandler_StillClearsFlag()
        {
            if (FindFC.EmpireFaction is null) TestAssert.Skip("No player colony faction");

            var obj = new WorldObject();
            var target = new CountingRaidTarget(obj) { IsUnderAttack = true, ThrowOnCallback = true };
            RaidTargetRegistry.Register(target);
            try
            {
                // The dispatch catches third-party handler exceptions; the flag is cleared in a finally.
                TestAssert.DoesNotThrow(() =>
                    MakeEngagedRaidOp(obj).CompleteBattle(MakeResult(BattleWinner.Defender)));
                TestAssert.AreEqual(1, target.WonCount, "Handler should have been invoked");
                TestAssert.IsFalse(target.IsUnderAttack,
                    "IsUnderAttack must be cleared even when the handler throws");
            }
            finally { RaidTargetRegistry.Unregister(target); }
        }

        /* -*- HasActiveOpTargeting: the un-stick detector used by MilitaryTick's orphan sweep -*-
           Uses a transient MilitaryOperationManager and its public `active` list, so these don't
           touch the live faction's operations. */

        [EmpireTest("Battle")]
        public static void HasActiveOpTargeting_LiveOp_ReturnsTrue()
        {
            var mgr = new MilitaryOperationManager();
            var obj = new WorldObject();
            mgr.active.Add(new MilitaryOperation(-1, null, PlanetTile.Invalid, obj)
            {
                phase = MilitaryOperationPhase.Engaged
            });
            TestAssert.IsTrue(mgr.HasActiveOpTargeting(obj),
                "A live op targeting the object should be detected");
        }

        [EmpireTest("Battle")]
        public static void HasActiveOpTargeting_NoOp_ReturnsFalse()
        {
            var mgr = new MilitaryOperationManager();
            TestAssert.IsFalse(mgr.HasActiveOpTargeting(new WorldObject()),
                "No op means the flag is orphaned — detector should report no live op");
        }

        [EmpireTest("Battle")]
        public static void HasActiveOpTargeting_FinishedOps_ReturnsFalse()
        {
            var mgr = new MilitaryOperationManager();
            var obj = new WorldObject();
            mgr.active.Add(new MilitaryOperation(-1, null, PlanetTile.Invalid, obj)
            {
                phase = MilitaryOperationPhase.CooldownPending
            });
            mgr.active.Add(new MilitaryOperation(-2, null, PlanetTile.Invalid, obj)
            {
                phase = MilitaryOperationPhase.Resolved
            });
            TestAssert.IsFalse(mgr.HasActiveOpTargeting(obj),
                "Ops whose battle is over should not keep a target flagged as under attack");
        }
    }
}
