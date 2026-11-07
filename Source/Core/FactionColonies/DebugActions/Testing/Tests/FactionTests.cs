namespace FactionColonies;

public static class FactionTests
{
    // --- CalculateFactionLevelGoalXP ---

    [EmpireTest("Faction")]
    public static void FactionXP_Level0_Returns100()
    {
        TestAssert.AreEqual(100f, SettlementFormulas.CalculateFactionLevelGoalXP(0), 0.01f);
    }

    [EmpireTest("Faction")]
    public static void FactionXP_Level1_Returns250()
    {
        TestAssert.AreEqual(250f, SettlementFormulas.CalculateFactionLevelGoalXP(1), 0.01f);
    }

    [EmpireTest("Faction")]
    public static void FactionXP_Level5_Returns850()
    {
        TestAssert.AreEqual(850f, SettlementFormulas.CalculateFactionLevelGoalXP(5), 0.01f);
    }

    [EmpireTest("Faction")]
    public static void FactionXP_Level10_Returns1600()
    {
        TestAssert.AreEqual(1600f, SettlementFormulas.CalculateFactionLevelGoalXP(10), 0.01f);
    }
}
