using VanillaPsycastsExpanded;
using AbilityDef = VEF.Abilities.AbilityDef;

namespace FactionColonies.VPE;

/// <summary>
/// Point math for the VPE unit designer: the psylink budget, the points a saved selection list
/// spends (each psycast, plus one for the first psycast of each path, each focus and each stat
/// point), and trimming a list to a budget. Uses VPE's real psycast defs, which are loaded on the
/// main menu as well, so these tests touch no game state.
/// </summary>
public static class VPEPointMathTests
{
    private const string Category = "VPE Point Math";

    /* Fixture: two psycasts on one path, one on a second path, and a meditation focus. */
    private sealed class Fixture
    {
        public AbilityDef pathA1;
        public AbilityDef pathA2;
        public AbilityDef pathB1;
        public MeditationFocusDef focus;
    }

    private static Fixture RequireFixture()
    {
        List<IGrouping<PsycasterPathDef, AbilityDef>> byPath = DefDatabase<AbilityDef>.AllDefsListForReading
            .Where(d => d.Psycast()?.path is not null)
            .GroupBy(d => d.Psycast().path)
            .ToList();
        IGrouping<PsycasterPathDef, AbilityDef> pathA = byPath.FirstOrDefault(g => g.Count() >= 2);
        TestAssert.NotNull(pathA, "VPE should define a psycaster path with at least two psycasts");
        IGrouping<PsycasterPathDef, AbilityDef> pathB = byPath.FirstOrDefault(g => g.Key != pathA.Key);
        TestAssert.NotNull(pathB, "VPE should define at least two psycaster paths");
        MeditationFocusDef focus = DefDatabase<MeditationFocusDef>.AllDefsListForReading.FirstOrDefault();
        TestAssert.NotNull(focus, "At least one meditation focus def should exist");

        List<AbilityDef> a = pathA.ToList();
        return new Fixture { pathA1 = a[0], pathA2 = a[1], pathB1 = pathB.First(), focus = focus };
    }

    private static SavedPsycast Ability(AbilityDef def) => new(VPEPsycastProvider.ProviderKey, def.defName);

    private static SavedPsycast Stat(int count) =>
        new(VPEPsycastProvider.ProviderKey, "", VPEPsycastProvider.KindStatUpgrade, count);

    private static SavedPsycast Focus(MeditationFocusDef def) =>
        new(VPEPsycastProvider.ProviderKey, def.defName, VPEPsycastProvider.KindMeditationFocus, 1);

    private static SavedPsycast Foreign(string defName) => new("SomeOtherSystem", defName);

    // ============================
    // Budget
    // ============================

    [EmpireTest(Category, GameState = TestGameState.Any)]
    public static void Budget_IsLevelPlusOne_ZeroWithoutPsylink()
    {
        TestAssert.AreEqual(0, VPEPointMath.Budget(0), "No psylink means no points");
        TestAssert.AreEqual(0, VPEPointMath.Budget(-3), "A negative level means no points");
        TestAssert.AreEqual(2, VPEPointMath.Budget(1), "Level 1 grants 2 points");
        TestAssert.AreEqual(6, VPEPointMath.Budget(5), "Each level past 1 adds one point");
    }

    // ============================
    // SpentPoints
    // ============================

    [EmpireTest(Category, GameState = TestGameState.Any)]
    public static void SpentPoints_NullOrEmpty_IsZero()
    {
        TestAssert.AreEqual(0, VPEPointMath.SpentPoints(null));
        TestAssert.AreEqual(0, VPEPointMath.SpentPoints([]));
    }

    [EmpireTest(Category, GameState = TestGameState.Any)]
    public static void SpentPoints_FirstPsycastOfAPathAlsoPaysForThePath()
    {
        Fixture f = RequireFixture();
        TestAssert.AreEqual(2, VPEPointMath.SpentPoints([Ability(f.pathA1)]), "Path + psycast");
        TestAssert.AreEqual(3, VPEPointMath.SpentPoints([Ability(f.pathA1), Ability(f.pathA2)]),
            "A second psycast on an unlocked path costs only itself");
        TestAssert.AreEqual(4, VPEPointMath.SpentPoints([Ability(f.pathA1), Ability(f.pathB1)]),
            "Psycasts on two paths pay for both paths");
    }

    [EmpireTest(Category, GameState = TestGameState.Any)]
    public static void SpentPoints_StatsAndFoci()
    {
        Fixture f = RequireFixture();
        TestAssert.AreEqual(3, VPEPointMath.SpentPoints([Stat(3)]), "A stat entry costs its count");
        TestAssert.AreEqual(1, VPEPointMath.SpentPoints([Stat(0)]), "A stat entry without a count costs one point");
        TestAssert.AreEqual(1, VPEPointMath.SpentPoints([Focus(f.focus)]), "A meditation focus costs one point");
        TestAssert.AreEqual(1 + 2 + 2, VPEPointMath.SpentPoints([Focus(f.focus), Stat(2), Ability(f.pathB1)]),
            "Kinds add up");
    }

    [EmpireTest(Category, GameState = TestGameState.Any)]
    public static void SpentPoints_ForeignEntriesAreFree_MissingDefCostsOne()
    {
        Fixture f = RequireFixture();
        TestAssert.AreEqual(0, VPEPointMath.SpentPoints([Foreign(f.pathA1.defName)]),
            "Entries of another psycast system cost no VPE points");
        TestAssert.AreEqual(1, VPEPointMath.SpentPoints([new(VPEPsycastProvider.ProviderKey, "EmpireTest_NoSuchPsycast")]),
            "A psycast whose def is missing still costs its own point, with no path point");
    }

    // ============================
    // Trim
    // ============================

    [EmpireTest(Category, GameState = TestGameState.Any)]
    public static void Trim_KeepsTheLongestPrefixThatFits()
    {
        Fixture f = RequireFixture();
        // Costs in order: 2 (path A + psycast), 1, 2 (path B + psycast). Level 2 = 3 points.
        List<SavedPsycast> picks = [Ability(f.pathA1), Ability(f.pathA2), Ability(f.pathB1)];
        List<SavedPsycast> trimmed = VPEPointMath.Trim(picks, 2);
        TestAssert.AreEqual(2, trimmed.Count, "The path-B psycast no longer fits");
        TestAssert.AreEqual((object)f.pathA2.defName, (object)trimmed[1].psycastDef);
        TestAssert.LessThanOrEqual(VPEPointMath.SpentPoints(trimmed), VPEPointMath.Budget(2), "The kept prefix fits the budget");

        TestAssert.AreEqual(2, VPEPointMath.Trim(picks, 3).Count, "4 points still cannot pay for path B");
        TestAssert.AreEqual(3, VPEPointMath.Trim(picks, 4).Count, "5 points pay for everything");
    }

    [EmpireTest(Category, GameState = TestGameState.Any)]
    public static void Trim_DropsEverythingAfterTheFirstMiss()
    {
        Fixture f = RequireFixture();
        // Level 1 = 2 points. The path-B psycast (2) fits; the focus after a miss must not be kept,
        // even though it alone would fit.
        List<SavedPsycast> picks = [Ability(f.pathB1), Ability(f.pathA1), Focus(f.focus)];
        List<SavedPsycast> trimmed = VPEPointMath.Trim(picks, 1);
        TestAssert.AreEqual(1, trimmed.Count, "Only the first pick fits; the walk stops at the first miss");
        TestAssert.AreEqual((object)f.pathB1.defName, (object)trimmed[0].psycastDef);
    }

    [EmpireTest(Category, GameState = TestGameState.Any)]
    public static void Trim_PartiallyKeepsAStatEntry()
    {
        Fixture f = RequireFixture();
        // Level 2 = 3 points: focus (1) + 2 of the 5 stat points.
        List<SavedPsycast> trimmed = VPEPointMath.Trim([Focus(f.focus), Stat(5), Ability(f.pathA1)], 2);
        TestAssert.AreEqual(2, trimmed.Count, "Nothing after a partially kept stat entry is kept");
        TestAssert.AreEqual((object)VPEPsycastProvider.KindStatUpgrade, (object)trimmed[1].kind);
        TestAssert.AreEqual(2, trimmed[1].count, "The stat entry is cut to the remaining points");
        TestAssert.AreEqual(3, VPEPointMath.SpentPoints(trimmed), "The trimmed list spends exactly the budget");
    }

    [EmpireTest(Category, GameState = TestGameState.Any)]
    public static void Trim_ForeignEntriesPassThrough_NullIsEmpty()
    {
        Fixture f = RequireFixture();
        TestAssert.IsEmpty(VPEPointMath.Trim(null, 5), "A null list trims to an empty one");

        List<SavedPsycast> trimmed = VPEPointMath.Trim([Foreign("x"), Ability(f.pathA1), Foreign("y")], 0);
        TestAssert.AreEqual(1, trimmed.Count, "With no budget only the foreign entries before the first miss stay");
        TestAssert.AreEqual((object)"x", (object)trimmed[0].psycastDef);
    }

    [EmpireTest(Category, GameState = TestGameState.Any)]
    public static void Trim_ResultAlwaysFitsTheBudget()
    {
        Fixture f = RequireFixture();
        List<SavedPsycast> picks = [Stat(2), Ability(f.pathA1), Focus(f.focus), Ability(f.pathB1), Ability(f.pathA2), Stat(4)];
        for (int level = 0; level <= 12; level++)
        {
            int spent = VPEPointMath.SpentPoints(VPEPointMath.Trim(picks, level));
            TestAssert.LessThanOrEqual(spent, VPEPointMath.Budget(level), $"Trimmed spend fits at psylink level {level}");
        }
        TestAssert.AreEqual(VPEPointMath.SpentPoints(picks), VPEPointMath.SpentPoints(VPEPointMath.Trim(picks, 30)),
            "A budget that covers everything keeps everything");
    }

    // ============================
    // PathOf
    // ============================

    [EmpireTest(Category, GameState = TestGameState.Any)]
    public static void PathOf_ResolvesThePsycastPath()
    {
        Fixture f = RequireFixture();
        TestAssert.IsTrue(VPEPointMath.PathOf(f.pathA1.defName) == f.pathA1.Psycast().path, "Path of a known psycast");
        TestAssert.IsNull(VPEPointMath.PathOf(null));
        TestAssert.IsNull(VPEPointMath.PathOf(""));
        TestAssert.IsNull(VPEPointMath.PathOf("EmpireTest_NoSuchPsycast"), "An unknown def has no path");
    }
}
