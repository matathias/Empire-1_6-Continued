using UnityEngine;

namespace FactionColonies;

/* Pure-logic tests for small shared helpers: FCVersion parsing and ordering (used by patch notes,
   the settings-file version check and the save stamp), the last-seen patch-note version, mod
   version lookup, the gene value cap, weighted race picks, patch-note link icons, the item picker
   view and the same-layer tile distance. Category "UtilHelpers". */
public static class UtilHelperTests
{
    /* FCVersion */

    [EmpireTest("UtilHelpers", GameState = TestGameState.Any)]
    public static void FCVersion_TryParse_ReadsMajorMinorPatch()
    {
        TestAssert.IsTrue(FCVersion.TryParse("1.5.43", out FCVersion version), "A major.minor.patch string should parse");
        TestAssert.AreEqual(new FCVersion(1, 5, 43), version, "Parsed parts");
        TestAssert.AreEqual("1.5.43", version.ToString(), "ToString round-trips the parsed text");
    }

    [EmpireTest("UtilHelpers", GameState = TestGameState.Any)]
    public static void FCVersion_TryParse_MalformedInputGivesZero()
    {
        string[] malformed = [null, "", "1.5", "1.5.43.2", "1.x.3", "a.b.c", "1.2.3x", "1..3"];
        foreach (string text in malformed)
        {
            TestAssert.IsFalse(FCVersion.TryParse(text, out FCVersion version), $"'{text ?? "null"}' should not parse");
            TestAssert.AreEqual(new FCVersion(0, 0, 0), version, $"'{text ?? "null"}' should give 0.0.0");
        }
    }

    [EmpireTest("UtilHelpers", GameState = TestGameState.Any)]
    public static void FCVersion_Compare_IsNumericPerPart()
    {
        FCVersion v1_9_99 = new(1, 9, 99);
        FCVersion v1_10_0 = new(1, 10, 0);
        FCVersion v2_0_0 = new(2, 0, 0);

        TestAssert.IsTrue(v1_10_0.IsNewerThan(v1_9_99), "Minor 10 is newer than minor 9 (numeric, not text order)");
        TestAssert.IsTrue(v1_9_99.IsOlderThan(v1_10_0), "IsOlderThan mirrors IsNewerThan");
        TestAssert.IsTrue(v2_0_0.IsNewerThan(v1_10_0), "Major outranks minor and patch");
        TestAssert.IsTrue(new FCVersion(1, 6, 5).IsNewerThan(new FCVersion(1, 6, 4)), "Patch breaks a major/minor tie");

        FCVersion same = new(1, 9, 99);
        TestAssert.AreEqual(0, v1_9_99.CompareTo(same), "Equal versions compare as 0");
        TestAssert.IsFalse(v1_9_99.IsNewerThan(same), "A version is not newer than itself");
        TestAssert.IsFalse(v1_9_99.IsOlderThan(same), "A version is not older than itself");

        List<FCVersion> sorted = [v2_0_0, v1_9_99, v1_10_0];
        sorted.Sort();
        TestAssert.AreEqual(v1_9_99, sorted[0], "Sort ascending: first");
        TestAssert.AreEqual(v1_10_0, sorted[1], "Sort ascending: second");
        TestAssert.AreEqual(v2_0_0, sorted[2], "Sort ascending: third");
    }

    /* PatchNote versions */

    [EmpireTest("UtilHelpers", GameState = TestGameState.Any)]
    public static void PatchNote_Version_ComparesAgainstFCVersion()
    {
        PatchNote note = new(new PatchNoteModInfo("test.mod")) { major = 1, minor = 6, patch = 4 };

        TestAssert.AreEqual(new FCVersion(1, 6, 4), note.Version, "Version is built from major/minor/patch");
        TestAssert.AreEqual("1.6.4", note.VersionNumber, "VersionNumber formatting");
        TestAssert.IsTrue(note.IsNewerThan(new FCVersion(1, 6, 3)), "Newer than an older patch");
        TestAssert.IsFalse(note.IsNewerThan(new FCVersion(1, 6, 4)), "Not newer than itself");
        TestAssert.IsFalse(note.IsNewerThan(new FCVersion(1, 7, 0)), "Not newer than a later minor");
        TestAssert.IsTrue(note.IsNewerThan(default), "Newer than 0.0.0, the never-seen version");
    }

    [EmpireTest("UtilHelpers", GameState = TestGameState.Any)]
    public static void LastSeenVersion_RoundTripsAndDefaultsToZero()
    {
        const string modId = "empire.test.lastseenversion";
        FCSettings.lastSeenVersions.Remove(modId);
        try
        {
            TestAssert.AreEqual(new FCVersion(0, 0, 0), FCSettings.GetLastSeenVersion(modId), "Unknown mod gives 0.0.0");

            FCSettings.SetLastSeenVersion(modId, new FCVersion(1, 6, 12));
            TestAssert.AreEqual("1.6.12", FCSettings.lastSeenVersions[modId], "Stored as major.minor.patch text");
            TestAssert.AreEqual(new FCVersion(1, 6, 12), FCSettings.GetLastSeenVersion(modId), "Read back the stored version");

            FCSettings.lastSeenVersions[modId] = "garbage";
            TestAssert.AreEqual(new FCVersion(0, 0, 0), FCSettings.GetLastSeenVersion(modId), "Unparseable text gives 0.0.0");
        }
        finally { FCSettings.lastSeenVersions.Remove(modId); }
    }

    /* ModVersionUtil */

    [EmpireTest("UtilHelpers", GameState = TestGameState.Any)]
    public static void ModVersionUtil_NullContent_GivesUnknown()
    {
        TestAssert.AreEqual("Unknown", ModVersionUtil.Get(null), "No content pack gives Unknown");

        string logged = null;
        ModVersionUtil.LogLoaded(null, message => logged = message);
        TestAssert.AreEqual("Did not load a mod version", logged, "LogLoaded reports a missing version");
    }

    [EmpireTest("UtilHelpers", GameState = TestGameState.Any)]
    public static void ModVersionUtil_Empire_MatchesAboutXml()
    {
        ModContentPack content = LoadedModManager.GetMod<FactionColoniesMod>()?.Content;
        TestAssert.IsNotNull(content, "Empire's content pack should be loaded");
        string aboutVersion = content.ModMetaData?.ModVersion;
        string expected = aboutVersion.NullOrEmpty() ? "Unknown" : aboutVersion;

        TestAssert.AreEqual(expected, ModVersionUtil.Get(content), "Get(content) reads About.xml");
        TestAssert.AreEqual(expected, ModVersionUtil.Get<FactionColoniesMod>(), "Get<TMod> resolves the same pack");
        TestAssert.AreEqual(expected, FCSettings.GetModVersion(), "FCSettings.GetModVersion delegates to it");

        string logged = null;
        ModVersionUtil.LogLoaded(content, message => logged = message);
        TestAssert.AreEqual(aboutVersion.NullOrEmpty() ? "Did not load a mod version" : $"v{aboutVersion}", logged,
            "LogLoaded prefixes the version with v");
    }

    /* Gene value cap */

    private static void WithGeneCap(bool unlimited, float cap, Action body)
    {
        TestUtil.WithOverride(() => FCSettings.geneValueFactorUnlimited, v => FCSettings.geneValueFactorUnlimited = v, unlimited,
            () => TestUtil.WithOverride(() => FCSettings.geneValueMaxFactor, v => FCSettings.geneValueMaxFactor = v, cap, body));
    }

    [EmpireTest("UtilHelpers", GameState = TestGameState.Any)]
    public static void GeneValueCap_RoundingNeverExceedsCap() => WithGeneCap(false, 1.9f, () =>
    {
        // 1.95 rounds to 2.0; the cap must still win. Clamping before rounding gave 2.0 here.
        TestAssert.AreEqual(1.9, GeneValuationUtil.ApplyCap(1.95f), 0.0001, "Rounded value above the cap is clamped to it");
        TestAssert.AreEqual(1.9, GeneValuationUtil.ApplyCap(5f), 0.0001, "Far above the cap");
        TestAssert.AreEqual(1.75, GeneValuationUtil.ApplyCap(1.63f), 0.0001, "Below the cap, snaps to the nearest quarter");
        TestAssert.AreEqual(1.5, GeneValuationUtil.ApplyCap(1.6f), 0.0001, "Rounds down to the nearer quarter");
        TestAssert.AreEqual(1.25, GeneValuationUtil.ApplyCap(1.125f), 0.0001, "Midpoints round away from zero");
    });

    [EmpireTest("UtilHelpers", GameState = TestGameState.Any)]
    public static void GeneValueCap_UnlimitedOnlyRounds() => WithGeneCap(true, 1.9f, () =>
    {
        TestAssert.AreEqual(3.0, GeneValuationUtil.ApplyCap(3.1f), 0.0001, "No ceiling when unlimited");
        TestAssert.AreEqual(2.0, GeneValuationUtil.ApplyCap(1.95f), 0.0001, "Rounding still applies");
    });

    [EmpireTest("UtilHelpers", GameState = TestGameState.Any)]
    public static void GeneValueCap_FloorIsAQuarter() => WithGeneCap(false, 0.1f, () =>
    {
        TestAssert.AreEqual(0.25, GeneValuationUtil.ApplyCap(0.05f), 0.0001, "Tiny factors floor at 0.25");
        TestAssert.AreEqual(0.25, GeneValuationUtil.ApplyCap(1f), 0.0001, "The floor wins over a ceiling below it");
    });

    /* Weighted race picks */

    [EmpireTest("UtilHelpers", GameState = TestGameState.Any)]
    public static void GetRandomRace_SkipsZeroWeightAndHandlesEmpty()
    {
        ThingDef zeroWeight = new() { defName = "EmpireTest_ZeroWeightRace" };
        ThingDef weighted = new() { defName = "EmpireTest_WeightedRace" };

        Rand.PushState(12345);
        try
        {
            XenotypeFilter empty = new();
            TestAssert.IsNull(empty.GetRandomRace(), "No races gives null");

            XenotypeFilter allZero = new();
            allZero.AddRaceWithWeight(zeroWeight, 0f);
            TestAssert.IsNull(allZero.GetRandomRace(), "Only zero-weight races gives null");

            XenotypeFilter filter = new();
            filter.AddRaceWithWeight(zeroWeight, 0f);
            filter.AddRaceWithWeight(weighted, 3f);
            for (int i = 0; i < 50; i++)
                TestAssert.AreEqual(weighted, filter.GetRandomRace(), "A zero-weight race is never picked");
        }
        finally { Rand.PopState(); }
    }

    [EmpireTest("UtilHelpers", GameState = TestGameState.Any)]
    public static void GetRandomRace_PicksRoughlyByWeight()
    {
        ThingDef light = new() { defName = "EmpireTest_LightRace" };
        ThingDef heavy = new() { defName = "EmpireTest_HeavyRace" };
        XenotypeFilter filter = new();
        filter.AddRaceWithWeight(light, 1f);
        filter.AddRaceWithWeight(heavy, 3f);

        int heavyCount = 0;
        const int draws = 2000;
        Rand.PushState(424242);
        try
        {
            for (int i = 0; i < draws; i++)
                if (filter.GetRandomRace() == heavy) heavyCount++;
        }
        finally { Rand.PopState(); }

        // Expected 75%; the window is wide enough to be seed-proof yet catches a uniform pick (50%).
        double share = heavyCount / (double)draws;
        TestAssert.GreaterThan(share, 0.68, $"Heavy race share {share:F3} should be near 0.75");
        TestAssert.LessThan(share, 0.82, $"Heavy race share {share:F3} should be near 0.75");
    }

    /* Patch-note link icons */

    [EmpireTest("UtilHelpers", GameState = TestGameState.Any)]
    public static void PatchNoteLink_WithoutIcon_UsesPlaceholderTexture()
    {
        PatchNoteModInfo mod = new("test.mod");
        mod.links.Add(new PatchNoteModInfo.Link { url = "https://example.invalid/a", tooltip = "a", iconPath = "" });
        mod.links.Add(new PatchNoteModInfo.Link { url = "https://example.invalid/b", tooltip = "b", iconPath = null });

        List<Texture2D> icons = mod.LinkButtonImages;
        TestAssert.AreEqual(2, icons.Count, "One icon per link");
        TestAssert.IsTrue(icons[0] == BaseContent.BadTex, "An empty icon path gives the placeholder texture");
        TestAssert.IsTrue(icons[1] == BaseContent.BadTex, "A null icon path gives the placeholder texture");
    }

    /* ThingDefPickerView */

    private static ThingDef Labeled(string defName, string label) => new() { defName = defName, label = label };

    [EmpireTest("UtilHelpers", GameState = TestGameState.Any)]
    public static void ThingDefPickerView_FiltersSortsAndCaches()
    {
        ThingDef apple = Labeled("EmpireTest_Apple", "apple");
        ThingDef banana = Labeled("EmpireTest_Banana", "Banana");
        ThingDef cherry = Labeled("EmpireTest_Cherry", "cherry");
        List<ThingDef> source = [cherry, apple, banana];
        ThingDefPickerView view = new();

        List<ThingDef> az = view.Get(source, null);
        TestAssert.AreEqual(apple, az[0], "A-Z ignores case: first");
        TestAssert.AreEqual(banana, az[1], "A-Z ignores case: second");
        TestAssert.AreEqual(cherry, az[2], "A-Z ignores case: third");
        TestAssert.IsFalse(ReferenceEquals(az, source), "The view never hands back (or reorders) the source list");
        TestAssert.AreEqual(cherry, source[0], "The source list keeps its order");
        TestAssert.IsTrue(ReferenceEquals(az, view.Get(source, null)), "Unchanged inputs reuse the cached view");

        view.SortIndex = 1;
        List<ThingDef> za = view.Get(source, null);
        TestAssert.AreEqual(cherry, za[0], "Changing the sort rebuilds the view (Z-A)");
        TestAssert.AreEqual(apple, za[2], "Z-A last");

        List<ThingDef> filtered = view.Get(source, "AN");
        TestAssert.AreEqual(1, filtered.Count, "Search is a case-insensitive substring match");
        TestAssert.AreEqual(banana, filtered[0], "Only the matching def remains");

        TestAssert.AreEqual(0, view.Get(source, "zzz").Count, "No matches gives an empty view");
    }

    /* PlanetTileExtensions */

    [EmpireTest("UtilHelpers")]
    public static void ApproxDistanceSameLayer_SameLayer_MatchesWorldGrid()
    {
        WorldGrid grid = Find.WorldGrid;
        PlanetLayer surface = TestAssert.NotNull(grid.Surface, "The world should have a surface layer");
        PlanetTile a = new(0, surface);
        PlanetTile b = new(surface.TilesCount / 2, surface);

        TestAssert.AreEqual(0.0, a.ApproxDistanceSameLayer(a), 0.0001, "A tile is 0 from itself");
        TestAssert.AreEqual(grid.ApproxDistanceInTiles(a, b), a.ApproxDistanceSameLayer(b), 0.0001,
            "Same-layer distance is WorldGrid.ApproxDistanceInTiles");
        TestAssert.IsFalse(float.IsInfinity(a.ApproxDistanceSameLayer(b)), "Same-layer distance is finite");
    }

    [EmpireTest("UtilHelpers", RequiresPackageId = ModContentPack.OdysseyModPackageId)]
    public static void ApproxDistanceSameLayer_CrossLayer_IsInfinite()
    {
        WorldGrid grid = Find.WorldGrid;
        PlanetLayer orbit = TestAssert.NotNull(grid.Orbit, "Odyssey is active but the world has no orbit layer");
        PlanetTile surfaceTile = new(0, grid.Surface);
        PlanetTile orbitTile = new(0, orbit);

        float distance = surfaceTile.ApproxDistanceSameLayer(orbitTile);
        TestAssert.IsTrue(float.IsPositiveInfinity(distance), "Tiles on different layers are infinitely far apart");
        TestAssert.IsTrue(float.IsPositiveInfinity(orbitTile.ApproxDistanceSameLayer(surfaceTile)), "In either direction");
        TestAssert.IsFalse(distance <= 1000000f, "A range check rejects a cross-layer pair");
    }
}
