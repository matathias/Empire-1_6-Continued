namespace FactionColonies;

/* DESTRUCTIVE: temporarily pins the live efficiencyDamping / defenderAdvantage battle settings
   (restored afterwards) so the auto-resolve and forecast math is checked at known values regardless
   of where the player has set the sliders. The forces are standalone and never registered. */
public static class BattleDestructiveTests
{
    [EmpireDestructiveTest("Destructive.Battle")]
    public static void FightRound_HigherEfficiency_CompensatesLowerRoll()
    {
        // DampenEff(2.0) = 1 + efficiencyDamping. At 0.5, A rolls 6 * 1.5 = 9 against B's 8 * 1.0 = 8.
        float saved = FCSettings.efficiencyDamping;
        FCSettings.efficiencyDamping = 0.5f;
        try
        {
            var mfa = BattleTests.CreateForce(5, 2.0, 5); // 2x efficiency
            var mfb = BattleTests.CreateForce(5, 1.0, 5);
            var rand = new BattleTests.AlternatingRandProvider(6, 8);

            SimulateBattleFc.FightRound(mfa, mfb, rand);

            TestAssert.AreEqual(5.0, mfa.forceRemaining, message: "Higher-efficiency attacker should win the round");
            TestAssert.AreEqual(4.0, mfb.forceRemaining, message: "Defender should lose one");
        }
        finally { FCSettings.efficiencyDamping = saved; }
    }

    [EmpireDestructiveTest("Destructive.Battle")]
    public static void AutoResolve_DefenderAdvantage_BoostsDefender()
    {
        // The defender's forceRemaining is multiplied by defenderAdvantage once, before combat.
        const float advantage = 1.5f;
        float saved = FCSettings.defenderAdvantage;
        FCSettings.defenderAdvantage = advantage;
        try
        {
            var mfa = BattleTests.CreateForce(5, 1.0, 5);
            var mfb = BattleTests.CreateForce(5, 1.0, 4);
            var rand = new BattleTests.AlternatingRandProvider(10, 11);

            BattleResult result = SimulateBattleFc.ResolveSynchronously(mfa, mfb, rand);

            TestAssert.AreEqual(Math.Round(4 * advantage), result.defenderInitialForce,
                message: $"Defender initial force should be Round(4 * {advantage}) after the advantage is applied");
        }
        finally { FCSettings.defenderAdvantage = saved; }
    }

    [EmpireDestructiveTest("Destructive.Battle")]
    public static void Forecast_OneForceFairCoin_CorrectValue()
    {
        // Attacker force 1, defender force 1, equal efficiency. A tied roll goes to the defender, so
        // the attacker wins 190 of the 400 d20 pairs: p = 0.475, and the defender's chance is 0.525.
        // defenderHP = Round(1 * defAdv), so pin defAdv to 1 to make the defender exactly 1 HP.
        float originalAdvantage = FCSettings.defenderAdvantage;
        double result;
        try
        {
            FCSettings.defenderAdvantage = 1f;
            // a=1, b=1, n=1: P(atk wins) = C(1,1) * 0.475 = 0.475 → defender chance = 0.525
            var attacker = BattleTests.CreateForce(1, 1.0, 1);
            var defender = BattleTests.CreateForce(1, 1.0, 1);
            result = SimulateBattleFc.CalculateDefenderWinChance(attacker, defender);
        }
        finally
        {
            FCSettings.defenderAdvantage = originalAdvantage;
        }

        TestAssert.AreEqual(0.525, result, 0.001,
            $"1v1 at equal efficiency should give the defender 0.525 (ties go to the defender), got {result}");
    }
}
