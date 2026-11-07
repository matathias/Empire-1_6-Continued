namespace FactionColonies;

/* DESTRUCTIVE: the settlement comparators are validated by sorting a copy of the live settlement
 * list and asserting the result is monotonic in the comparator's intended direction. Creates
 * transient settlements until at least two exist. Not reverted. */
public static class CompareUtilDestructiveTests
{
    // --- Live settlements: sort-direction contract ---

    [EmpireDestructiveTest("Destructive.Util")]
    public static void CompareSettlementLevel_SortsDescending()
    {
        AssertSortedDescending(CompareUtil.CompareSettlementLevel, s => s.settlementLevel, "Level");
    }

    [EmpireDestructiveTest("Destructive.Util")]
    public static void CompareSettlementLoyalty_SortsDescending()
    {
        AssertSortedDescending(CompareUtil.CompareSettlementLoyalty, s => s.loyalty, "Loyalty");
    }

    [EmpireDestructiveTest("Destructive.Util")]
    public static void CompareSettlementHappiness_SortsDescending()
    {
        AssertSortedDescending(CompareUtil.CompareSettlementHappiness, s => s.happiness, "Happiness");
    }

    [EmpireDestructiveTest("Destructive.Util")]
    public static void CompareSettlementProsperity_SortsDescending()
    {
        AssertSortedDescending(CompareUtil.CompareSettlementProsperity, s => s.prosperity, "Prosperity");
    }

    [EmpireDestructiveTest("Destructive.Util")]
    public static void CompareSettlementUnrest_SortsAscending()
    {
        AssertSortedAscending(CompareUtil.CompareSettlementUnrest, s => s.unrest, "Unrest");
    }

    // --- Helpers ---

    private static void AssertSortedDescending(Comparison<WorldSettlementFC> cmp,
        Func<WorldSettlementFC, double> key, string name)
    {
        var list = RequireSettlements();
        list.Sort(cmp);
        for (int i = 0; i + 1 < list.Count; i++)
            TestAssert.IsTrue(key(list[i]) >= key(list[i + 1]),
                $"{name}: expected descending order at index {i} ({key(list[i])} vs {key(list[i + 1])})");
        DestructiveTestUtil.AssertEmpireInvariants(FindFC.FactionComp, $"{name} sort");
    }

    private static void AssertSortedAscending(Comparison<WorldSettlementFC> cmp,
        Func<WorldSettlementFC, double> key, string name)
    {
        var list = RequireSettlements();
        list.Sort(cmp);
        for (int i = 0; i + 1 < list.Count; i++)
            TestAssert.IsTrue(key(list[i]) <= key(list[i + 1]),
                $"{name}: expected ascending order at index {i} ({key(list[i])} vs {key(list[i + 1])})");
        DestructiveTestUtil.AssertEmpireInvariants(FindFC.FactionComp, $"{name} sort");
    }

    /// <summary>A copy of the live settlement list, creating transient settlements until it holds at least two.</summary>
    private static List<WorldSettlementFC> RequireSettlements()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        DestructiveTestUtil.EnsureSettlements(2);
        return new List<WorldSettlementFC>(faction.settlements);
    }
}
