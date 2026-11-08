namespace FactionColonies;

/* The battle forecast's per-round win chance must be the exact chance the battle engine's d20
   round produces. With one HP per side and no defender advantage the forecast reduces to a single
   round, so 1 - CalculateDefenderWinChance is that round's attacker win chance, and it can be
   checked against SimulateRound driven through all 400 roll pairs. Settings are overridden for the
   duration of each test and restored afterwards. */
public static class BattleRoundChanceTests
{
    /// <summary>Hands out a fixed sequence of rolls, one per Range call.</summary>
    private sealed class ScriptedRand(int first, int second) : IRandProvider
    {
        private int calls;
        public int Range(int min, int maxExclusive) => calls++ == 0 ? first : second;
    }

    private static MilitaryForce OneHp(double efficiency) =>
        new() { militaryLevel = 1, militaryEfficiency = efficiency, forceRemaining = 1 };

    /// <summary>Attacker's single-round win chance as the forecast models it.</summary>
    private static double ForecastRoundChance(double attackerEff, double defenderEff) =>
        1.0 - SimulateBattleFc.CalculateDefenderWinChance(OneHp(attackerEff), OneHp(defenderEff));

    /// <summary>Attacker's single-round win chance counted from SimulateRound over every roll pair.</summary>
    private static double EngineRoundChance(double attackerEff, double defenderEff)
    {
        int wins = 0;
        for (int a = 1; a <= 20; a++)
            for (int b = 1; b <= 20; b++)
                if (SimulateBattleFc.SimulateRound(OneHp(attackerEff), OneHp(defenderEff), new ScriptedRand(a, b)).attackerWonRound)
                    wins++;
        return wins / 400.0;
    }

    private static void WithForecastSettings(float damping, Action body) =>
        TestUtil.WithOverride(() => FCSettings.defenderAdvantage, v => FCSettings.defenderAdvantage = v, 1f, () =>
            TestUtil.WithOverride(() => FCSettings.efficiencyDamping, v => FCSettings.efficiencyDamping = v, damping, body));

    [EmpireTest("Battle", GameState = TestGameState.Any)]
    public static void RoundChance_EqualEfficiency_TiesGoToDefender()
    {
        // 190 of the 400 pairs have the attacker strictly higher; the 20 ties go to the defender.
        WithForecastSettings(1f, () =>
            TestAssert.AreEqual(0.475, ForecastRoundChance(1.0, 1.0), tolerance: 1e-9,
                message: "Equal efficiency should give the attacker 190/400 per round"));
    }

    [EmpireTest("Battle", GameState = TestGameState.Any)]
    public static void RoundChance_KnownRatios()
    {
        WithForecastSettings(1f, () =>
        {
            // 2a > b: a = 1..10 wins 2a - 1 pairs (100 total), a = 11..20 wins all 20 (200): 300/400.
            TestAssert.AreEqual(0.75, ForecastRoundChance(2.0, 1.0), tolerance: 1e-9, message: "Double efficiency");
            // a/2 > b: a = 2k-1 and a = 2k each win k - 1 pairs, k = 1..10: 90/400.
            TestAssert.AreEqual(0.225, ForecastRoundChance(0.5, 1.0), tolerance: 1e-9, message: "Half efficiency");
        });
    }

    [EmpireTest("Battle", GameState = TestGameState.Any)]
    public static void RoundChance_MatchesEngineAcrossEfficiencies()
    {
        double[] efficiencies = [0.5, 0.8, 1.0, 1.05, 1.25, 1.5, 2.0];
        foreach (float damping in new[] { 1f, 0.5f })
        {
            WithForecastSettings(damping, () =>
            {
                foreach (double ae in efficiencies)
                    foreach (double de in efficiencies)
                    {
                        TestAssert.AreEqual(EngineRoundChance(ae, de), ForecastRoundChance(ae, de), tolerance: 1e-9,
                            message: $"Forecast round chance differs from the engine at attacker {ae}, defender {de}, damping {damping}");
                    }
            });
        }
    }
}
