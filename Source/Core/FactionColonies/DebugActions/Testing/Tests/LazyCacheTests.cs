namespace FactionColonies;

public static class LazyCacheTests
{
    [EmpireTest("LazyCache")]
    public static void Faction_TechLevel_IsNotUndefined()
    {
        var fc = DestructiveTestUtil.RequireFaction();

        TestAssert.IsTrue(fc.techLevel != TechLevel.Undefined, $"techLevel should not be Undefined, got {fc.techLevel}");
    }
}
