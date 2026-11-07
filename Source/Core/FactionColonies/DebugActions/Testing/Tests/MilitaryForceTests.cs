namespace FactionColonies;

public static class MilitaryForceTests
{
    // ============================
    // Constructor / Pure Math
    // ============================

    [EmpireTest("MilitaryForce")]
    public static void Constructor_ForceRemaining_EqualsRoundedLevelTimesEfficiency()
    {
        var force = new MilitaryForce(7.0, 1.3, null, null);
        double expected = Math.Round(7.0 * 1.3); // 9.1 → 9
        TestAssert.AreEqual(expected, force.forceRemaining,
            $"forceRemaining should be Round(level * efficiency) = {expected}");
    }

    [EmpireTest("MilitaryForce")]
    public static void Constructor_FractionalResult_RoundsCorrectly()
    {
        var force = new MilitaryForce(5.0, 1.5, null, null);
        double expected = Math.Round(5.0 * 1.5); // 7.5 → 8 (banker's rounding)
        TestAssert.AreEqual(expected, force.forceRemaining);
    }

    [EmpireTest("MilitaryForce")]
    public static void DefensivePower_AppliesDefenderAdvantage()
    {
        var force = new MilitaryForce(10.0, 1.0, null, null);
        double expected = Math.Round(force.forceRemaining * FCSettings.defenderAdvantage);
        TestAssert.AreEqual(expected, force.DefensivePower,
            $"DefensivePower should be Round(forceRemaining * {FCSettings.defenderAdvantage})");
    }

    // ============================
    // Tech Level Mapping
    // ============================

    [EmpireTest("MilitaryForce")]
    public static void TechLevelMapping_Neolithic_Level2_Eff1()
    {
        MilitaryDeploymentUtil.GetTechLevelBaseline(
            TechLevel.Neolithic, out double level, out double eff);
        TestAssert.AreEqual(2.0, level, message: "Neolithic level");
        TestAssert.AreEqual(0.9, eff, message: "Neolithic efficiency");
    }

    [EmpireTest("MilitaryForce")]
    public static void TechLevelMapping_Spacer_Level6_Eff1Point1()
    {
        MilitaryDeploymentUtil.GetTechLevelBaseline(
            TechLevel.Spacer, out double level, out double eff);
        TestAssert.AreEqual(6.0, level, message: "Spacer level");
        TestAssert.AreEqual(1.1, eff, message: "Spacer efficiency");
    }

    [EmpireTest("MilitaryForce")]
    public static void TechLevelMapping_Archotech_HighestValues()
    {
        MilitaryDeploymentUtil.GetTechLevelBaseline(
            TechLevel.Archotech, out double level, out double eff);
        TestAssert.AreEqual(9.0, level, message: "Archotech level");
        TestAssert.AreEqual(1.3, eff, message: "Archotech efficiency");
    }

    [EmpireTest("MilitaryForce")]
    public static void TechLevelMapping_AllLevels_PositiveValues()
    {
        foreach (TechLevel tech in Enum.GetValues(typeof(TechLevel)))
        {
            MilitaryDeploymentUtil.GetTechLevelBaseline(
                tech, out double level, out double eff);
            TestAssert.GreaterThan(level, 0, $"TechLevel {tech}: level should be > 0");
            TestAssert.GreaterThan(eff, 0, $"TechLevel {tech}: efficiency should be > 0");
        }
    }

    // ============================
    // EnemyPower
    // ============================

    [EmpireTest("MilitaryForce")]
    public static void EnemyPower_NoEntryForPlayerOrEmpire()
    {
        WorldComponent_EnemyPower registry = TestAssert.NotNull(FindFC.EnemyPower, "No EnemyPower world component in a loaded game");

        // The player and the allied empire faction must never get an enemy estimate; the
        // world-map inspect line keys off this null to stay off player/empire settlements.
        TestAssert.IsNull(registry.GetOrCompute(Faction.OfPlayer),
            "Player faction must not have an EnemyPower entry");

        if (FindFC.EmpireFaction is not null)
        {
            TestAssert.IsNull(registry.GetOrCompute(FindFC.EmpireFaction),
                "Empire faction must not have an EnemyPower entry");
        }

        Settlement playerColony = Find.WorldObjects.Settlements
            .FirstOrDefault(s => s is not WorldSettlementFC && s.Faction == Faction.OfPlayer);
        if (playerColony is not null)
        {
            TestAssert.IsNull(registry.GetOrCompute(playerColony),
                "Player colony must not have an EnemyPower entry");
        }
    }
}
