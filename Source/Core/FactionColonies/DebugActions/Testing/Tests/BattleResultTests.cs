namespace FactionColonies
{
    /* Pure-property tests for BattleResult: IsOverwhelmingVictory, IsCrushingDefeat
       (+ ForAttacker/ForDefender variants), and IsComplete. All assertions exercise
       constructor-and-read; no game state, no Skip guards. */
    public static class BattleResultTests
    {
        private static BattleResult MakeResult(BattleWinner winner,
            double atkInitial, double atkRemaining,
            double defInitial, double defRemaining)
        {
            return new BattleResult
            {
                winner = winner,
                attackerInitialForce = atkInitial,
                attackerForceRemaining = atkRemaining,
                defenderInitialForce = defInitial,
                defenderForceRemaining = defRemaining
            };
        }

        // -*- IsOverwhelmingVictory -*-

        [EmpireTest("Battle")]
        public static void IsOverwhelmingVictory_AttackerWinsZeroLoss_True()
        {
            // Attacker started with 10, ended with 10 (no casualties), and won.
            var r = MakeResult(BattleWinner.Attacker, 10, 10, 5, 0);
            TestAssert.IsTrue(r.IsOverwhelmingVictory);
        }

        [EmpireTest("Battle")]
        public static void IsOverwhelmingVictory_AttackerWinsPartial_False()
        {
            // Attacker won but took losses (10 -> 7).
            var r = MakeResult(BattleWinner.Attacker, 10, 7, 5, 0);
            TestAssert.IsFalse(r.IsOverwhelmingVictory);
        }

        [EmpireTest("Battle")]
        public static void IsOverwhelmingVictory_DefenderWinsZeroLoss_True()
        {
            var r = MakeResult(BattleWinner.Defender, 8, 0, 6, 6);
            TestAssert.IsTrue(r.IsOverwhelmingVictory);
        }

        [EmpireTest("Battle")]
        public static void IsOverwhelmingVictory_DefenderWinsPartial_False()
        {
            var r = MakeResult(BattleWinner.Defender, 8, 0, 6, 4);
            TestAssert.IsFalse(r.IsOverwhelmingVictory);
        }

        [EmpireTest("Battle")]
        public static void IsOverwhelmingVictory_ZeroAttackerInitialForce_False()
        {
            // Guard in IsOverwhelmingVictory: attackerInitialForce > 0 required.
            var r = MakeResult(BattleWinner.Attacker, 0, 0, 5, 0);
            TestAssert.IsFalse(r.IsOverwhelmingVictory);
        }

        [EmpireTest("Battle")]
        public static void IsOverwhelmingVictory_ZeroDefenderInitialForce_False()
        {
            // Mirror guard in IsOverwhelmingVictory: defenderInitialForce > 0 required.
            var r = MakeResult(BattleWinner.Defender, 5, 0, 0, 0);
            TestAssert.IsFalse(r.IsOverwhelmingVictory);
        }

        [EmpireTest("Battle")]
        public static void IsOverwhelmingVictory_ErrorWinner_False()
        {
            var r = MakeResult(BattleWinner.Error, 10, 10, 10, 10);
            TestAssert.IsFalse(r.IsOverwhelmingVictory);
        }

        // -*- IsCrushingDefeat aliases -*-

        [EmpireTest("Battle")]
        public static void IsCrushingDefeat_MirrorsIsOverwhelmingVictory_OnAttackerOV()
        {
            var r = MakeResult(BattleWinner.Attacker, 10, 10, 5, 0);
            TestAssert.AreEqual(r.IsOverwhelmingVictory, r.IsCrushingDefeat,
                "IsCrushingDefeat is a semantic alias of IsOverwhelmingVictory");
        }

        [EmpireTest("Battle")]
        public static void IsCrushingDefeat_MirrorsIsOverwhelmingVictory_OnDefenderOV()
        {
            var r = MakeResult(BattleWinner.Defender, 8, 0, 6, 6);
            TestAssert.AreEqual(r.IsOverwhelmingVictory, r.IsCrushingDefeat);
        }

        [EmpireTest("Battle")]
        public static void IsCrushingDefeatForDefender_AttackerWinsZeroLoss_True()
        {
            // From the defender's perspective: defender lost, attacker took 0 casualties.
            var r = MakeResult(BattleWinner.Attacker, 10, 10, 5, 0);
            TestAssert.IsTrue(r.IsCrushingDefeatForDefender);
            TestAssert.IsFalse(r.IsCrushingDefeatForAttacker);
        }

        [EmpireTest("Battle")]
        public static void IsCrushingDefeatForAttacker_DefenderWinsZeroLoss_True()
        {
            var r = MakeResult(BattleWinner.Defender, 8, 0, 6, 6);
            TestAssert.IsTrue(r.IsCrushingDefeatForAttacker);
            TestAssert.IsFalse(r.IsCrushingDefeatForDefender);
        }

        [EmpireTest("Battle")]
        public static void IsCrushingDefeatForAttacker_AttackerWins_False()
        {
            // Pinned perspective: when the attacker wins, can't be a crushing-defeat-for-attacker.
            var r = MakeResult(BattleWinner.Attacker, 10, 10, 5, 0);
            TestAssert.IsFalse(r.IsCrushingDefeatForAttacker);
        }

        [EmpireTest("Battle")]
        public static void IsCrushingDefeatForDefender_DefenderWins_False()
        {
            var r = MakeResult(BattleWinner.Defender, 8, 0, 6, 6);
            TestAssert.IsFalse(r.IsCrushingDefeatForDefender);
        }

        [EmpireTest("Battle")]
        public static void IsCrushingDefeat_BothPartialLosses_False()
        {
            // Neither side overwhelming: attacker won 10->7 vs defender 5->0.
            var r = MakeResult(BattleWinner.Attacker, 10, 7, 5, 0);
            TestAssert.IsFalse(r.IsCrushingDefeat);
            TestAssert.IsFalse(r.IsCrushingDefeatForDefender);
            TestAssert.IsFalse(r.IsCrushingDefeatForAttacker);
        }

        // -*- IsComplete -*-

        [EmpireTest("Battle")]
        public static void IsComplete_AttackerZero_True()
        {
            var r = MakeResult(BattleWinner.Defender, 10, 0, 5, 3);
            TestAssert.IsTrue(r.IsComplete);
        }

        [EmpireTest("Battle")]
        public static void IsComplete_DefenderZero_True()
        {
            var r = MakeResult(BattleWinner.Attacker, 10, 8, 5, 0);
            TestAssert.IsTrue(r.IsComplete);
        }

        [EmpireTest("Battle")]
        public static void IsComplete_BothNegative_True()
        {
            // Extreme/contrived: both sides simultaneously below zero. Still IsComplete.
            var r = MakeResult(BattleWinner.Error, 5, -1, 5, -2);
            TestAssert.IsTrue(r.IsComplete);
        }

        [EmpireTest("Battle")]
        public static void IsComplete_BothPositive_False()
        {
            // Battle still in progress.
            var r = MakeResult(BattleWinner.Error, 10, 7, 5, 3);
            TestAssert.IsFalse(r.IsComplete);
        }
    }
}
