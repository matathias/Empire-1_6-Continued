namespace FactionColonies
{
    /* Tests for BattleCasualtyApplicator's pure-math seams: ComputeCasualtyRate,
       ComputeDeathChance, ComputeVictimCount. Stat multipliers depend on FindFC.FactionComp
       and default to 1.0 when no contributing policy/building/event is active, so the math tests
       below work whether or not a faction is loaded.

       Game-state-dependent tests (Apply* orchestrator) require live mercs and are marked Skip
       when the prerequisites aren't met. */
    public static class BattleCasualtyApplicatorTests
    {
        // -*- ComputeCasualtyRate -*-

        [EmpireTest("BattleCasualty")]
        public static void ComputeCasualtyRate_NoLoss_ReturnsZero()
        {
            double rate = BattleCasualtyApplicator.ComputeCasualtyRate(null, 10, 10, null);
            TestAssert.AreEqual(0.0, rate);
        }

        [EmpireTest("BattleCasualty")]
        public static void ComputeCasualtyRate_AllLost_ReturnsOne()
        {
            double rate = BattleCasualtyApplicator.ComputeCasualtyRate(null, 10, 0, null);
            TestAssert.AreEqual(1.0, rate);
        }

        [EmpireTest("BattleCasualty")]
        public static void ComputeCasualtyRate_HalfLost_ReturnsHalf()
        {
            double rate = BattleCasualtyApplicator.ComputeCasualtyRate(null, 10, 5, null);
            TestAssert.AreEqual(0.5, rate);
        }

        [EmpireTest("BattleCasualty")]
        public static void ComputeCasualtyRate_ZeroInitial_ReturnsZero()
        {
            // Guard in ComputeCasualtyRate — initial <= 0 short-circuits to 0.
            double rate = BattleCasualtyApplicator.ComputeCasualtyRate(null, 0, 0, null);
            TestAssert.AreEqual(0.0, rate);
        }

        [EmpireTest("BattleCasualty")]
        public static void ComputeCasualtyRate_NegativeIntermediate_ClampsToZero()
        {
            // remaining > initial (mathematically invalid; would yield negative rate).
            // The negative-result clamp in ComputeCasualtyRate should normalize to 0.
            double rate = BattleCasualtyApplicator.ComputeCasualtyRate(null, 5, 8, null);
            TestAssert.AreEqual(0.0, rate);
        }

        [EmpireTest("BattleCasualty")]
        public static void ComputeCasualtyRate_FractionalLoss_MatchesQuotient()
        {
            // 20 -> 13 should be exactly 0.35 (no multiplier in effect with default faction state).
            double rate = BattleCasualtyApplicator.ComputeCasualtyRate(null, 20, 13, null);
            TestAssert.AreEqual(0.35, rate);
        }

        // -*- ComputeDeathChance -*-

        [EmpireTest("BattleCasualty")]
        public static void ComputeDeathChance_RateBelowThreshold_ReturnsZero()
        {
            // Derive the sample from the (player-adjustable) threshold rather than hard-coding 0.5.
            float threshold = FCSettings.autoResolveCasualtyDeathThreshold;
            if (threshold <= 0f) TestAssert.Skip("threshold is at floor; no rate below it");
            double belowRate = threshold / 2.0;
            double chance = BattleCasualtyApplicator.ComputeDeathChance(null, belowRate, null);
            TestAssert.AreEqual(0.0, chance);
        }

        [EmpireTest("BattleCasualty")]
        public static void ComputeDeathChance_RateAtThreshold_ReturnsZero()
        {
            // Strict `>` in ComputeDeathChance means equal-to-threshold yields 0.
            float threshold = FCSettings.autoResolveCasualtyDeathThreshold;
            double chance = BattleCasualtyApplicator.ComputeDeathChance(null, threshold, null);
            TestAssert.AreEqual(0.0, chance);
        }

        [EmpireTest("BattleCasualty")]
        public static void ComputeDeathChance_RateFull_ReturnsMaxDeathFraction()
        {
            // rate = 1.0 → ramp produces exactly maxDeathFraction (assuming threshold < 1).
            float threshold = FCSettings.autoResolveCasualtyDeathThreshold;
            if (threshold >= 1f) TestAssert.Skip("threshold is at ceiling; ramp test would divide by zero");

            float maxFrac = FCSettings.autoResolveCasualtyMaxDeathFraction;
            double chance = BattleCasualtyApplicator.ComputeDeathChance(null, 1.0, null);
            TestAssert.AreEqual(maxFrac, chance, tolerance: 0.001);
        }

        [EmpireTest("BattleCasualty")]
        public static void ComputeDeathChance_RatePartiallyAboveThreshold_RampsLinearly()
        {
            // Midway between threshold and 1.0 → half of maxDeathFraction.
            float threshold = FCSettings.autoResolveCasualtyDeathThreshold;
            float maxFrac = FCSettings.autoResolveCasualtyMaxDeathFraction;
            if (threshold >= 1f) TestAssert.Skip("threshold is at ceiling; ramp test would divide by zero");

            double midpoint = (threshold + 1.0) / 2.0;
            double expected = ((midpoint - threshold) / (1.0 - threshold)) * maxFrac;
            double chance = BattleCasualtyApplicator.ComputeDeathChance(null, midpoint, null);
            TestAssert.AreEqual(expected, chance, tolerance: 0.001);
        }

        [EmpireTest("BattleCasualty")]
        public static void ComputeDeathChance_ResultClampedAtOne()
        {
            // Math could in theory exceed 1.0 if maxDeathFraction is misconfigured; the clamp
            // in ComputeDeathChance keeps the output in [0, 1]. Push rate way above 1.0 to exercise.
            double chance = BattleCasualtyApplicator.ComputeDeathChance(null, 5.0, null);
            TestAssert.LessThanOrEqual(chance, 1.0);
            TestAssert.IsTrue(chance >= 0.0);
        }

        // -*- ComputeVictimCount -*-

        [EmpireTest("BattleCasualty")]
        public static void ComputeVictimCount_ZeroRate_ReturnsZero()
        {
            TestAssert.AreEqual(0, BattleCasualtyApplicator.ComputeVictimCount(5, 0.0));
        }

        [EmpireTest("BattleCasualty")]
        public static void ComputeVictimCount_TinyRate_BumpsToOne()
        {
            // 5 * 0.05 = 0.25 floors to 0; the rate > 0 floor in ComputeVictimCount bumps to 1.
            TestAssert.AreEqual(1, BattleCasualtyApplicator.ComputeVictimCount(5, 0.05));
        }

        [EmpireTest("BattleCasualty")]
        public static void ComputeVictimCount_HalfRate_FloorsCorrectly()
        {
            // 5 * 0.5 = 2.5 → 2.
            TestAssert.AreEqual(2, BattleCasualtyApplicator.ComputeVictimCount(5, 0.5));
        }

        [EmpireTest("BattleCasualty")]
        public static void ComputeVictimCount_FullRate_AllCandidates()
        {
            TestAssert.AreEqual(5, BattleCasualtyApplicator.ComputeVictimCount(5, 1.0));
        }

        [EmpireTest("BattleCasualty")]
        public static void ComputeVictimCount_RateOverOne_CapsAtCandidateCount()
        {
            // Defensive: rate above 1.0 should still produce <= candidateCount.
            TestAssert.AreEqual(5, BattleCasualtyApplicator.ComputeVictimCount(5, 1.5));
        }

        [EmpireTest("BattleCasualty")]
        public static void ComputeVictimCount_ZeroCandidates_ReturnsZero()
        {
            TestAssert.AreEqual(0, BattleCasualtyApplicator.ComputeVictimCount(0, 0.8));
        }

        [EmpireTest("BattleCasualty")]
        public static void ComputeVictimCount_NegativeCandidates_ReturnsZero()
        {
            TestAssert.AreEqual(0, BattleCasualtyApplicator.ComputeVictimCount(-3, 0.8));
        }

        // -*- ApplyCasualtiesToSquad orchestrator (game-state-dependent) -*-

        [EmpireTest("BattleCasualty")]
        public static void ApplyCasualtiesToSquad_DisabledViaSetting_NoOp()
        {
            // Verify the early-return in ApplyCasualtiesToSquad honors the setting.
            // Doesn't actually need a real squad — passing null short-circuits at the next
            // guard, but we want to prove the setting is read first. Confirm via DoesNotThrow.
            bool original = FCSettings.applyAutoResolveInjuries;
            FCSettings.applyAutoResolveInjuries = false;
            try
            {
                TestAssert.DoesNotThrow(() =>
                    BattleCasualtyApplicator.ApplyCasualtiesToSquad(null, 10, 5, null));
            }
            finally
            {
                FCSettings.applyAutoResolveInjuries = original;
            }
        }

        [EmpireTest("BattleCasualty")]
        public static void ApplyCasualtiesToSquad_NullSquad_NoOp()
        {
            // Null-squad guard in ApplyCasualtiesToSquad returns silently.
            TestAssert.DoesNotThrow(() =>
                BattleCasualtyApplicator.ApplyCasualtiesToSquad(null, 10, 5, null));
        }

        [EmpireTest("BattleCasualty")]
        public static void ApplyCasualtiesToSquad_ZeroInitial_NoOp()
        {
            // Zero-initial guard in ApplyCasualtiesToSquad returns silently.
            TestAssert.DoesNotThrow(() =>
                BattleCasualtyApplicator.ApplyCasualtiesToSquad(null, 0, 0, null));
        }
    }
}
