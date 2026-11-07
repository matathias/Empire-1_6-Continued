namespace FactionColonies;

public static class SettlementTests
{
    [EmpireTest("Settlement")]
    public static void Settlement_AllDefs_BuildingSlotsNonDecreasingByLevel()
    {
        foreach (WorldSettlementDef def in DefDatabase<WorldSettlementDef>.AllDefsListForReading)
        {
            var ext = def.GetSettlementTypeExtension();
            int prev = 0;
            int maxLevel = Math.Min(10, def.maxSettlementLevel);
            for (int level = 0; level <= maxLevel; level++)
            {
                int slots = ext.GetBuildingSlots(level, def.maxBuildingCount);
                TestAssert.IsTrue(slots >= prev,
                    $"{def.defName}: building slots decreased from {prev} at level {level - 1} to {slots} at level {level}");
                TestAssert.IsTrue(slots <= def.maxBuildingCount,
                    $"{def.defName}: building slots {slots} exceeds maxBuildingCount {def.maxBuildingCount} at level {level}");
                prev = slots;
            }
        }
    }
}
