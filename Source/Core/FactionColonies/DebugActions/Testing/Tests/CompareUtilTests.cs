namespace FactionColonies;

/* Coverage for CompareUtil's def comparator, which is pure. The settlement comparators need live
 * settlements and are covered by CompareUtilDestructiveTests. */
public static class CompareUtilTests
{
    // --- Pure: BuildingFCDef by label ---

    [EmpireTest("Util")]
    public static void CompareBuildingDef_OrdersByLabel()
    {
        var a = new BuildingFCDef { label = "alpha" };
        var b = new BuildingFCDef { label = "beta" };
        TestAssert.LessThan(CompareUtil.CompareBuildingDef(a, b), 0, "alpha should sort before beta");
        TestAssert.GreaterThan(CompareUtil.CompareBuildingDef(b, a), 0, "beta should sort after alpha");
        TestAssert.AreEqual(0, CompareUtil.CompareBuildingDef(a, a), "equal labels compare equal");
    }
}
