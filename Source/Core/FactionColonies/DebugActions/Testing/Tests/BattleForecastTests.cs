using System;

namespace FactionColonies
{
    public static class BattleForecastTests
    {
        private static MilitaryForce CreateForce(double level, double efficiency, double remaining)
        {
            return new MilitaryForce
            {
                militaryLevel = level,
                militaryEfficiency = efficiency,
                forceRemaining = remaining
            };
        }

        // ============================
        // Edge Cases
        // ============================

        [EmpireTest("Battle")]
        public static void Forecast_ZeroAttackerForce_ReturnsOne()
        {
            var attacker = CreateForce(0, 1.0, 0);
            var defender = CreateForce(5, 1.0, 5);

            double result = SimulateBattleFc.CalculateDefenderWinChance(attacker, defender);

            TestAssert.AreEqual(1.0, result, message: "Defender should always win vs zero attacker force");
        }

        [EmpireTest("Battle")]
        public static void Forecast_ZeroDefenderForce_ReturnsZero()
        {
            var attacker = CreateForce(5, 1.0, 5);
            var defender = CreateForce(0, 1.0, 0);

            double result = SimulateBattleFc.CalculateDefenderWinChance(attacker, defender);

            TestAssert.AreEqual(0.0, result, message: "Defender should always lose with zero force");
        }

        [EmpireTest("Battle")]
        public static void Forecast_ResultAlwaysInBounds()
        {
            // Test a variety of force/efficiency combos
            double[] forces = { 1, 3, 5, 10, 20 };
            double[] efficiencies = { 0.5, 0.9, 1.0, 1.2, 1.5 };

            foreach (double af in forces)
            foreach (double df in forces)
            foreach (double ae in efficiencies)
            foreach (double de in efficiencies)
            {
                var attacker = CreateForce(af, ae, af);
                var defender = CreateForce(df, de, df);
                double result = SimulateBattleFc.CalculateDefenderWinChance(attacker, defender);
                TestAssert.IsTrue(result >= 0.0 && result <= 1.0,
                    $"Out of bounds: {result} for attacker({af},{ae}) vs defender({df},{de})");
            }
        }

        // ============================
        // Monotonicity
        // ============================

        [EmpireTest("Battle")]
        public static void Forecast_MoreAttackerForce_LowersDefenderChance()
        {
            var defender = CreateForce(5, 1.0, 5);

            var weakAttacker = CreateForce(3, 1.0, 3);
            var strongAttacker = CreateForce(10, 1.0, 10);

            double weakResult = SimulateBattleFc.CalculateDefenderWinChance(weakAttacker, defender);
            double strongResult = SimulateBattleFc.CalculateDefenderWinChance(strongAttacker, defender);

            TestAssert.IsTrue(strongResult < weakResult,
                $"Stronger attacker should lower defender chance: {strongResult} should be < {weakResult}");
        }

        [EmpireTest("Battle")]
        public static void Forecast_MoreDefenderForce_RaisesDefenderChance()
        {
            var attacker = CreateForce(5, 1.0, 5);

            var weakDefender = CreateForce(2, 1.0, 2);
            var strongDefender = CreateForce(10, 1.0, 10);

            double weakResult = SimulateBattleFc.CalculateDefenderWinChance(attacker, weakDefender);
            double strongResult = SimulateBattleFc.CalculateDefenderWinChance(attacker, strongDefender);

            TestAssert.IsTrue(strongResult > weakResult,
                $"Stronger defender should raise defender chance: {strongResult} should be > {weakResult}");
        }

        [EmpireTest("Battle")]
        public static void Forecast_HigherAttackerEfficiency_LowersDefenderChance()
        {
            var defender = CreateForce(5, 1.0, 5);

            var normalAttacker = CreateForce(5, 1.0, 5);
            var efficientAttacker = CreateForce(5, 1.5, 5);

            double normalResult = SimulateBattleFc.CalculateDefenderWinChance(normalAttacker, defender);
            double efficientResult = SimulateBattleFc.CalculateDefenderWinChance(efficientAttacker, defender);

            TestAssert.IsTrue(efficientResult < normalResult,
                $"Higher attacker efficiency should lower defender chance: {efficientResult} should be < {normalResult}");
        }

        [EmpireTest("Battle")]
        public static void Forecast_HigherDefenderEfficiency_RaisesDefenderChance()
        {
            var attacker = CreateForce(5, 1.0, 5);

            var normalDefender = CreateForce(5, 1.0, 5);
            var efficientDefender = CreateForce(5, 1.5, 5);

            double normalResult = SimulateBattleFc.CalculateDefenderWinChance(attacker, normalDefender);
            double efficientResult = SimulateBattleFc.CalculateDefenderWinChance(attacker, efficientDefender);

            TestAssert.IsTrue(efficientResult > normalResult,
                $"Higher defender efficiency should raise defender chance: {efficientResult} should be > {normalResult}");
        }

        // ============================
        // Extreme mismatches
        // ============================

        [EmpireTest("Battle")]
        public static void Forecast_OverwhelmingAttacker_DefenderChanceVeryLow()
        {
            var attacker = CreateForce(20, 1.0, 20);
            var defender = CreateForce(1, 1.0, 1);

            double result = SimulateBattleFc.CalculateDefenderWinChance(attacker, defender);

            TestAssert.LessThan(result, 0.15,
                $"Defender with force 1 vs attacker with force 20 should have very low chance: {result}");
        }

        [EmpireTest("Battle")]
        public static void Forecast_OverwhelmingDefender_DefenderChanceVeryHigh()
        {
            var attacker = CreateForce(1, 1.0, 1);
            var defender = CreateForce(20, 1.0, 20);

            double result = SimulateBattleFc.CalculateDefenderWinChance(attacker, defender);

            TestAssert.GreaterThan(result, 0.85,
                $"Defender with force 20 vs attacker with force 1 should have very high chance: {result}");
        }

        // ============================
        // Known analytical results
        // ============================

        [EmpireTest("Battle")]
        public static void Forecast_EqualEfficiency_MatchesBinomialFormula()
        {
            // With equal efficiency 1.0, DampenEfficiency returns 1.0, so p = 0.5 exactly.
            // P(attacker wins) = Σ(k=b to n) C(n,k) / 2^n where n = a+b-1.
            // We compute expected by hand accounting for defenderAdvantage.
            int rawAttacker = 3;
            int rawDefender = 3;
            int defenderHP = (int)Math.Max(1, Math.Round(rawDefender * FCSettings.defenderAdvantage));
            int n = rawAttacker + defenderHP - 1;

            // Compute expected binomial tail sum at p=0.5
            double expected = BinomialTailHalf(n, defenderHP);

            var attacker = CreateForce(rawAttacker, 1.0, rawAttacker);
            var defender = CreateForce(rawDefender, 1.0, rawDefender);
            double result = SimulateBattleFc.CalculateDefenderWinChance(attacker, defender);

            TestAssert.AreEqual(expected, result, 0.001,
                $"Expected {expected} for a={rawAttacker}, b={defenderHP}, n={n}, got {result}");
        }

        [EmpireTest("Battle")]
        public static void Forecast_AsymmetricForce_MatchesBinomialFormula()
        {
            // Attacker has 2 force, defender has 5 force, equal efficiency → p = 0.5
            int rawAttacker = 2;
            int rawDefender = 5;
            int defenderHP = (int)Math.Max(1, Math.Round(rawDefender * FCSettings.defenderAdvantage));
            int n = rawAttacker + defenderHP - 1;

            double expected = BinomialTailHalf(n, defenderHP);

            var attacker = CreateForce(rawAttacker, 1.0, rawAttacker);
            var defender = CreateForce(rawDefender, 1.0, rawDefender);
            double result = SimulateBattleFc.CalculateDefenderWinChance(attacker, defender);

            TestAssert.AreEqual(expected, result, 0.001,
                $"Expected {expected} for a={rawAttacker}, b={defenderHP}, n={n}, got {result}");
        }

        [EmpireTest("Battle")]
        public static void Forecast_OneForceFairCoin_CorrectValue()
        {
            // Attacker force 1, defender force 1, equal efficiency → p = 0.5
            // defenderHP = Round(1 * defAdv). If defAdv < 1.5, this rounds to 1.
            int defenderHP = (int)Math.Max(1, Math.Round(1.0 * FCSettings.defenderAdvantage));
            if (defenderHP != 1) TestAssert.Skip("defenderAdvantage rounds force 1 to != 1, skip exact check");

            // a=1, b=1, n=1: P(atk wins) = C(1,1) * 0.5 = 0.5 → defender chance = 0.5
            var attacker = CreateForce(1, 1.0, 1);
            var defender = CreateForce(1, 1.0, 1);
            double result = SimulateBattleFc.CalculateDefenderWinChance(attacker, defender);

            TestAssert.AreEqual(0.5, result, 0.001,
                $"1v1 fair coin should be 50/50, got {result}");
        }

        // ============================
        // Consistency with simulation
        // ============================

        [EmpireTest("Battle")]
        public static void Forecast_MatchesSimulationStatistically()
        {
            // Run many simulated battles and verify the analytical forecast is within
            // a reasonable confidence interval of the empirical win rate.
            var baseAttacker = CreateForce(5, 1.1, 5);
            var baseDefender = CreateForce(4, 1.0, 4);

            double forecast = SimulateBattleFc.CalculateDefenderWinChance(baseAttacker, baseDefender);

            int trials = 2000;
            int defenderWins = 0;
            for (int i = 0; i < trials; i++)
            {
                // Create fresh copies each trial (ResolveSynchronously mutates the forces)
                var mfa = CreateForce(baseAttacker.militaryLevel, baseAttacker.militaryEfficiency, baseAttacker.forceRemaining);
                var mfb = CreateForce(baseDefender.militaryLevel, baseDefender.militaryEfficiency, baseDefender.forceRemaining);
                BattleResult result = SimulateBattleFc.ResolveSynchronously(mfa, mfb);
                if (result.DefenderVictory) defenderWins++;
            }

            double empirical = (double)defenderWins / trials;
            // Allow ±5% margin for statistical variance at 2000 trials
            double margin = 0.05;
            TestAssert.IsTrue(Math.Abs(forecast - empirical) < margin,
                $"Forecast {forecast:F3} differs from empirical {empirical:F3} (margin {margin}) over {trials} trials");
        }

        /// <summary>
        /// Computes P(defender wins) = 1 - Σ(k=b to n) C(n,k) / 2^n for the fair-coin case (p=0.5).
        /// </summary>
        private static double BinomialTailHalf(int n, int b)
        {
            double sum = 0;
            double term = 1.0; // C(n, n) * (0.5)^n = (0.5)^n; start with term = (0.5)^n
            for (int i = 0; i < n; i++) term *= 0.5;

            sum = term; // k = n
            for (int k = n - 1; k >= b; k--)
            {
                term *= (k + 1.0) / (n - k); // C(n,k) / C(n,k+1) = (k+1)/(n-k) ; p/q = 1 for p=0.5
                sum += term;
            }
            return Math.Max(0.0, Math.Min(1.0, 1.0 - sum));
        }
    }
}
