namespace FactionColonies;

public static class BuildingUpkeepTests
{
    // ============================
    // Special Buildings (pure)
    // ============================

    [EmpireTest("BuildingUpkeep")]
    public static void EmptyBuilding_ZeroUpkeep()
    {
        TestAssert.AreEqual(0.0, BuildingFCDefOf.Empty.Upkeep,
            message: "Empty building should have 0 upkeep");
    }

    [EmpireTest("BuildingUpkeep")]
    public static void ConstructionBuilding_ZeroUpkeep()
    {
        TestAssert.AreEqual(0.0, BuildingFCDefOf.Construction.Upkeep,
            message: "Construction placeholder should have 0 upkeep");
    }
}
