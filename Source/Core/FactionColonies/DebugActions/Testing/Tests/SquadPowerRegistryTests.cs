namespace FactionColonies
{
    /* Tests for SquadPowerRegistry: LevelFromCost pure math, default SquadPower for null/unassigned
       squads, and chained-modifier ordering for stationed squads. The chaining tests Skip when
       no stationed squad is available. */
    public static class SquadPowerRegistryTests
    {
        /* Test doubles for the priority-ordering tests. Each modifier records the running power
           it received so the test can assert chaining order from the captured trace. */

        private class AddLevelModifier : ISquadPowerModifier
        {
            public int Priority { get; }
            private readonly double _add;
            public double LastSeenLevel = double.NaN;

            public AddLevelModifier(int priority, double add)
            {
                Priority = priority;
                _add = add;
            }

            public SquadPower ModifyPower(MercenarySquadFC squad, SquadPower currentPower)
            {
                LastSeenLevel = currentPower.militaryLevel;
                return new SquadPower(currentPower.militaryLevel + _add, currentPower.militaryEfficiency);
            }
        }

        private class MultLevelModifier : ISquadPowerModifier
        {
            public int Priority { get; }
            private readonly double _mult;
            public double LastSeenLevel = double.NaN;

            public MultLevelModifier(int priority, double mult)
            {
                Priority = priority;
                _mult = mult;
            }

            public SquadPower ModifyPower(MercenarySquadFC squad, SquadPower currentPower)
            {
                LastSeenLevel = currentPower.militaryLevel;
                return new SquadPower(currentPower.militaryLevel * _mult, currentPower.militaryEfficiency);
            }
        }

        private class ThrowingModifier : ISquadPowerModifier
        {
            public int Priority => 0;
            public SquadPower ModifyPower(MercenarySquadFC squad, SquadPower currentPower)
            {
                throw new System.InvalidOperationException("test");
            }
        }

        private static MercenarySquadFC FindStationedSquad()
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction is null) return null;
            foreach (WorldSettlementFC s in faction.settlements)
            {
                foreach (MercenarySquadFC squad in s.StationedSquads)
                {
                    if (squad?.settlement is object) return squad;
                }
            }
            return null;
        }

        // -*- LevelFromCost (pure math) -*-

        [EmpireTest("Registry")]
        public static void LevelFromCost_BelowMinimum_ReturnsOne()
        {
            // Guard in LevelFromCost — cost <= 1000 returns 1.0.
            TestAssert.AreEqual(1.0, SquadPowerRegistry.LevelFromCost(500));
        }

        [EmpireTest("Registry")]
        public static void LevelFromCost_AtThreshold_ReturnsOne()
        {
            // Boundary: cost == 1000 still returns 1.
            TestAssert.AreEqual(1.0, SquadPowerRegistry.LevelFromCost(1000));
        }

        [EmpireTest("Registry")]
        public static void LevelFromCost_AboveOneBudgetPlateau_GreaterThanOne()
        {
            // The early-return at cost<=1000 is just a fast path; the L=1 floor in the math
            // actually extends up to cost~2100 (the inverse formula yields L<1 there and
            // Math.Max(1, ...) clamps it). Pick a cost well above the plateau.
            TestAssert.GreaterThan(SquadPowerRegistry.LevelFromCost(5000), 1.0);
        }

        [EmpireTest("Registry")]
        public static void LevelFromCost_FormulaMonotonic()
        {
            double a = SquadPowerRegistry.LevelFromCost(2000);
            double b = SquadPowerRegistry.LevelFromCost(5000);
            double c = SquadPowerRegistry.LevelFromCost(10000);
            TestAssert.GreaterThan(b, a);
            TestAssert.GreaterThan(c, b);
        }

        [EmpireTest("Registry")]
        public static void LevelFromCost_FlooredAtOne()
        {
            // Defensive: pathological negative input should still floor at 1.
            TestAssert.IsTrue(SquadPowerRegistry.LevelFromCost(-1000) >= 1.0);
        }

        // -*- Resolve: null/unassigned defaults -*-

        [EmpireTest("Registry")]
        public static void Resolve_NullSquad_DefaultPower()
        {
            SquadPower p = SquadPowerRegistry.Resolve(null);
            TestAssert.AreEqual(1.0, p.militaryLevel);
            TestAssert.AreEqual(1.0, p.militaryEfficiency);
        }

        [EmpireTest("Registry")]
        public static void Resolve_NullSquad_DoesNotRunProviders()
        {
            // Guard in Resolve: a null squad short-circuits BEFORE the chain.
            // Register a modifier that would bump level if it ran; verify it doesn't.
            var modifier = new AddLevelModifier(100, 50);
            SquadPowerRegistry.Register(modifier);
            try
            {
                SquadPower p = SquadPowerRegistry.Resolve(null);
                TestAssert.AreEqual(1.0, p.militaryLevel,
                    message: "Null squad should bypass the modifier chain (got non-default power)");
            }
            finally { SquadPowerRegistry.Unregister(modifier); }
        }

        [EmpireTest("Registry")]
        public static void Resolve_EmptySquad_DefaultPower()
        {
            // A fresh squad with no loadout (no mercenaries) projects level 1 — its cost is 0
            // so LevelFromCost floors at 1 — regardless of whether it has a home settlement.
            // (Unassigned squads that DO carry a loadout now report their real cost-derived power.)
            var squad = new MercenarySquadFC();
            SquadPower p = SquadPowerRegistry.Resolve(squad);
            TestAssert.AreEqual(1.0, p.militaryLevel);
            TestAssert.AreEqual(1.0, p.militaryEfficiency);
        }

        // -*- Resolve: chained modifiers (game-state-dependent) -*-

        [EmpireTest("Registry")]
        public static void Resolve_SingleProvider_AppliesModification()
        {
            MercenarySquadFC squad = FindStationedSquad();
            if (squad is null) TestAssert.Skip("No stationed squad");

            double basePower = SquadPowerRegistry.Resolve(squad).militaryLevel;
            var modifier = new AddLevelModifier(100, 5);
            SquadPowerRegistry.Register(modifier);
            try
            {
                double withModifier = SquadPowerRegistry.Resolve(squad).militaryLevel;
                TestAssert.AreEqual(basePower + 5, withModifier);
            }
            finally { SquadPowerRegistry.Unregister(modifier); }
        }

        [EmpireTest("Registry")]
        public static void Resolve_MultipleProviders_RunsInDescendingPriority()
        {
            MercenarySquadFC squad = FindStationedSquad();
            if (squad is null) TestAssert.Skip("No stationed squad");

            // High-priority first: double the level. Low-priority second: add 10.
            // Descending order means doubling happens BEFORE the addition.
            //   base level L -> A doubles to 2L -> B adds 10 = 2L + 10
            // Reversed order would yield (L + 10) * 2 = 2L + 20. We assert the former.
            var modA = new MultLevelModifier(100, 2.0);
            var modB = new AddLevelModifier(50, 10);
            double basePower = SquadPowerRegistry.Resolve(squad).militaryLevel;
            SquadPowerRegistry.Register(modA);
            SquadPowerRegistry.Register(modB);
            try
            {
                double result = SquadPowerRegistry.Resolve(squad).militaryLevel;
                double expected = basePower * 2.0 + 10;
                TestAssert.AreEqual(expected, result, tolerance: 0.01);

                // Cross-check: high-priority modifier saw the BASE power, low-priority saw the
                // already-doubled power. Confirms descending-priority dispatch.
                TestAssert.AreEqual(basePower, modA.LastSeenLevel, tolerance: 0.01,
                    message: "Higher-priority modifier should see the unmodified base power");
                TestAssert.AreEqual(basePower * 2.0, modB.LastSeenLevel, tolerance: 0.01,
                    message: "Lower-priority modifier should see the higher-priority modifier's output");
            }
            finally
            {
                SquadPowerRegistry.Unregister(modA);
                SquadPowerRegistry.Unregister(modB);
            }
        }

        [EmpireTest("Registry")]
        public static void Resolve_DuplicateRegister_Ignored()
        {
            MercenarySquadFC squad = FindStationedSquad();
            if (squad is null) TestAssert.Skip("No stationed squad");

            var modifier = new AddLevelModifier(100, 5);
            SquadPowerRegistry.Register(modifier);
            SquadPowerRegistry.Register(modifier); // duplicate

            try
            {
                double basePower;
                // Briefly unregister to get base power...
                SquadPowerRegistry.Unregister(modifier);
                basePower = SquadPowerRegistry.Resolve(squad).militaryLevel;
                // ...then re-register once and try the duplicate path.
                SquadPowerRegistry.Register(modifier);
                SquadPowerRegistry.Register(modifier);

                double result = SquadPowerRegistry.Resolve(squad).militaryLevel;
                TestAssert.AreEqual(basePower + 5, result,
                    message: "Duplicate registration should only apply the modifier once");
            }
            // Unregister twice: the try body registers the modifier twice, and a dedup regression
            // would otherwise leak a copy (a permanent +5 on every squad-power resolution) for the session.
            finally { SquadPowerRegistry.Unregister(modifier); SquadPowerRegistry.Unregister(modifier); }
        }

        [EmpireTest("Registry")]
        public static void Resolve_ExceptionInProvider_DoesNotCrash()
        {
            MercenarySquadFC squad = FindStationedSquad();
            if (squad is null) TestAssert.Skip("No stationed squad");

            var bad = new ThrowingModifier();
            SquadPowerRegistry.Register(bad);
            try
            {
                TestAssert.DoesNotThrow(() => SquadPowerRegistry.Resolve(squad));
            }
            finally { SquadPowerRegistry.Unregister(bad); }
        }

        [EmpireTest("Registry")]
        public static void Resolve_ExceptionInProvider_PreservesRunningPower()
        {
            // A throwing modifier between two well-behaved ones should not break the chain — the
            // running power survives the thrower's exception per the try/catch in Resolve.
            MercenarySquadFC squad = FindStationedSquad();
            if (squad is null) TestAssert.Skip("No stationed squad");

            var first = new AddLevelModifier(200, 5);
            var bad = new ThrowingModifier();
            var last = new AddLevelModifier(50, 7);

            double basePower = SquadPowerRegistry.Resolve(squad).militaryLevel;
            SquadPowerRegistry.Register(first);
            SquadPowerRegistry.Register(bad);
            SquadPowerRegistry.Register(last);
            try
            {
                double result = SquadPowerRegistry.Resolve(squad).militaryLevel;
                TestAssert.AreEqual(basePower + 5 + 7, result, tolerance: 0.01,
                    message: "Throwing modifier should be skipped; remaining chain still runs");
            }
            finally
            {
                SquadPowerRegistry.Unregister(first);
                SquadPowerRegistry.Unregister(bad);
                SquadPowerRegistry.Unregister(last);
            }
        }

        [EmpireTest("Registry")]
        public static void Resolve_Unregister_RemovesProvider()
        {
            MercenarySquadFC squad = FindStationedSquad();
            if (squad is null) TestAssert.Skip("No stationed squad");

            var modifier = new AddLevelModifier(100, 5);
            double basePower = SquadPowerRegistry.Resolve(squad).militaryLevel;
            SquadPowerRegistry.Register(modifier);
            SquadPowerRegistry.Unregister(modifier);
            double result = SquadPowerRegistry.Resolve(squad).militaryLevel;
            TestAssert.AreEqual(basePower, result,
                message: "Unregistered modifier should not contribute");
        }
    }
}
