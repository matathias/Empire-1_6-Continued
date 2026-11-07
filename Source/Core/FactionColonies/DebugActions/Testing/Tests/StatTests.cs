namespace FactionColonies;

public static class StatTests
{
    // ============================
    // Helpers
    // ============================

    private static FactionFC GetFaction()
    {
        return FindFC.FactionComp;
    }

    // ============================
    // Tier 1: Def Validation (pure, no game state needed)
    // ============================

    [EmpireTest("Stat")]
    public static void Def_AllStats_HaveUniqueDefNames()
    {
        var allStats = DefDatabase<FCStatDef>.AllDefsListForReading;
        var names = new HashSet<string>();
        foreach (FCStatDef stat in allStats)
        {
            TestAssert.IsTrue(names.Add(stat.defName),
                $"Duplicate FCStatDef defName: {stat.defName}");
        }
    }

    [EmpireTest("Stat")]
    public static void Def_ResourceTypeDefs_HaveMatchingStatLinks()
    {
        foreach (ResourceTypeDef rtd in DefDatabase<ResourceTypeDef>.AllDefsListForReading)
        {
            TestAssert.IsNotNull(rtd.productionAdditiveStat,
                $"{rtd.defName}: productionAdditiveStat is null");
            TestAssert.IsNotNull(rtd.productionMultiplierStat,
                $"{rtd.defName}: productionMultiplierStat is null");

            if (rtd.productionAdditiveStat is not null)
            {
                TestAssert.AreEqual((object)FCStatAggregation.Additive,
                    (object)rtd.productionAdditiveStat.aggregation,
                    $"{rtd.defName}: productionAdditiveStat should be Additive");
                TestAssert.AreEqual((object)rtd, (object)rtd.productionAdditiveStat.linkedResource,
                    $"{rtd.defName}: productionAdditiveStat.linkedResource should point back");
            }
            if (rtd.productionMultiplierStat is not null)
            {
                TestAssert.AreEqual((object)FCStatAggregation.Multiplicative,
                    (object)rtd.productionMultiplierStat.aggregation,
                    $"{rtd.defName}: productionMultiplierStat should be Multiplicative");
                TestAssert.AreEqual((object)rtd, (object)rtd.productionMultiplierStat.linkedResource,
                    $"{rtd.defName}: productionMultiplierStat.linkedResource should point back");
            }
        }
    }

    [EmpireTest("Stat")]
    public static void Def_ResourceLinkedStats_HaveBidirectionalLinks()
    {
        foreach (FCStatDef stat in DefDatabase<FCStatDef>.AllDefsListForReading)
        {
            if (stat.linkedResource is null) continue;

            ResourceTypeDef rtd = stat.linkedResource;
            bool isAdditive = stat.aggregation == FCStatAggregation.Additive;
            FCStatDef expected = isAdditive ? rtd.productionAdditiveStat : rtd.productionMultiplierStat;
            TestAssert.AreEqual((object)stat, (object)expected,
                $"{stat.defName}: linkedResource {rtd.defName} should reference this stat as its " +
                (isAdditive ? "productionAdditiveStat" : "productionMultiplierStat"));
        }
    }

    // ============================
    // Tier 1: ConfigErrors
    // ============================

    [EmpireTest("Stat")]
    public static void ConfigErrors_NullStat_YieldsError()
    {
        var mods = new List<FCStatModifier> { new() { stat = null, value = 1 } };
        int errorCount = FCStatModifier.ConfigErrors(mods, "TestOwner").Count();
        TestAssert.AreEqual(1, errorCount, "Should yield exactly 1 config error for null stat");
    }

    [EmpireTest("Stat")]
    public static void ConfigErrors_ValidStat_YieldsNoErrors()
    {
        var mods = new List<FCStatModifier>
        {
            new() { stat = FCStatDefOf.militaryBaseLevel, value = 1 }
        };
        int errorCount = FCStatModifier.ConfigErrors(mods, "TestOwner").Count();
        TestAssert.AreEqual(0, errorCount, "Should yield no config errors for valid stat");
    }

    [EmpireTest("Stat")]
    public static void ConfigErrors_NullOrEmptyList_YieldsNoErrors()
    {
        int nullErrors = FCStatModifier.ConfigErrors(null, "TestOwner").Count();
        TestAssert.AreEqual(0, nullErrors, "Null list should yield no errors");

        int emptyErrors = FCStatModifier.ConfigErrors([], "TestOwner").Count();
        TestAssert.AreEqual(0, emptyErrors, "Empty list should yield no errors");
    }

    // ============================
    // Tier 1: FCSituation statModifier validation
    // ============================

    [EmpireTest("Stat")]
    public static void ConfigErrors_FCSituationStageDef_NullStat_YieldsError()
    {
        var stage = new FCSituationStageDef
        {
            defName = "EmpireTest_SituationStage",
            statModifiers = [new() { stat = null, value = 1 }]
        };
        bool hasNullStatError = stage.ConfigErrors().Any(e => e.Contains("null stat"));
        TestAssert.IsTrue(hasNullStatError,
            "FCSituationStageDef with a null-stat modifier should surface a config error");
    }

    [EmpireTest("Stat")]
    public static void ConfigErrors_FCSituationStageDef_ValidStat_NoNullStatError()
    {
        var stage = new FCSituationStageDef
        {
            defName = "EmpireTest_SituationStage",
            statModifiers = [new() { stat = FCStatDefOf.militaryBaseLevel, value = 1 }]
        };
        bool hasNullStatError = stage.ConfigErrors().Any(e => e.Contains("null stat"));
        TestAssert.IsFalse(hasNullStatError,
            "FCSituationStageDef with a valid stat should not surface a null-stat error");
    }

    [EmpireTest("Stat")]
    public static void ConfigErrors_FCSituationApproachDef_NullStat_YieldsError()
    {
        var approach = new FCSituationApproachDef
        {
            defName = "EmpireTest_SituationApproach",
            statModifiers = [new() { stat = null, value = 1 }]
        };
        bool hasNullStatError = approach.ConfigErrors().Any(e => e.Contains("null stat"));
        TestAssert.IsTrue(hasNullStatError,
            "FCSituationApproachDef with a null-stat modifier should surface a config error");
    }

    [EmpireTest("Stat")]
    public static void ConfigErrors_FCSituationApproachDef_ValidStat_NoNullStatError()
    {
        var approach = new FCSituationApproachDef
        {
            defName = "EmpireTest_SituationApproach",
            statModifiers = [new() { stat = FCStatDefOf.militaryBaseLevel, value = 1 }]
        };
        bool hasNullStatError = approach.ConfigErrors().Any(e => e.Contains("null stat"));
        TestAssert.IsFalse(hasNullStatError,
            "FCSituationApproachDef with a valid stat should not surface a null-stat error");
    }

    // ============================
    // Tier 1: GetDescription
    // ============================

    [EmpireTest("Stat")]
    public static void Description_EmptyList_ReturnsEmpty()
    {
        TaggedString desc = FCStatModifier.GetDescription([]);
        TestAssert.IsTrue(desc.RawText.NullOrEmpty(),
            $"Empty list should produce empty description, got '{desc}'");
    }

    [EmpireTest("Stat")]
    public static void Description_NullStatEntry_DoesNotThrow()
    {
        var mods = new List<FCStatModifier> { new() { stat = null, value = 1 } };
        TaggedString desc = FCStatModifier.GetDescription(mods);
        TestAssert.IsTrue(desc.RawText.NullOrEmpty(),
            $"Null stat entry should be skipped, got '{desc}'");
    }

    [EmpireTest("Stat")]
    public static void Description_AdditiveStatWithKey_ProducesOutput()
    {
        // militaryBaseLevel is additive and has a descriptionKey
        FCStatDef stat = FCStatDefOf.militaryBaseLevel;
        TestAssert.IsFalse(stat.descriptionKey.NullOrEmpty(), "militaryBaseLevel should ship a descriptionKey");
        var mods = new List<FCStatModifier> { new() { stat = stat, value = 2 } };
        TaggedString desc = FCStatModifier.GetDescription(mods);
        TestAssert.IsFalse(desc.RawText.NullOrEmpty(),
            "Additive stat with descriptionKey should produce non-empty description");
    }

    [EmpireTest("Stat")]
    public static void Description_MultiplicativeStat_ProducesOutput()
    {
        // happinessGainedMultiplier is multiplicative and has a descriptionKey
        FCStatDef stat = FCStatDefOf.happinessGainedMultiplier;
        TestAssert.IsFalse(stat.descriptionKey.NullOrEmpty(), "happinessGainedMultiplier should ship a descriptionKey");
        var mods = new List<FCStatModifier> { new() { stat = stat, value = 1.5 } };
        TaggedString desc = FCStatModifier.GetDescription(mods);
        TestAssert.IsFalse(desc.RawText.NullOrEmpty(),
            "Multiplicative stat with descriptionKey should produce non-empty description");
    }

    [EmpireTest("Stat")]
    public static void Description_ResourceLinkedStat_ProducesOutput()
    {
        // Find a resource-linked stat (production additive of the first ResourceTypeDef that has one)
        ResourceTypeDef rtd = TestAssert.NotNull(DefDatabase<ResourceTypeDef>.AllDefsListForReading
                .FirstOrDefault(r => r.productionAdditiveStat is not null),
            "No ResourceTypeDef has a productionAdditiveStat");
        var mods = new List<FCStatModifier>
        {
            new() { stat = rtd.productionAdditiveStat, value = 3 }
        };
        TaggedString desc = FCStatModifier.GetDescription(mods);
        TestAssert.IsFalse(desc.RawText.NullOrEmpty(),
            "Resource-linked stat should produce non-empty description");
    }

    // ============================
    // Tier 2: Faction Sanity (settlement-level sanity lives in StatDestructiveTests)
    // ============================

    [EmpireTest("Stat")]
    public static void FactionStat_AllStats_AreFiniteNumbers()
    {
        var faction = TestAssert.NotNull(GetFaction(), "No FactionFC world component in a loaded game");

        foreach (FCStatDef stat in DefDatabase<FCStatDef>.AllDefsListForReading)
        {
            double val = faction.GetFactionStatValue(stat);
            TestAssert.IsFalse(double.IsNaN(val),
                $"GetFactionStatValue({stat.defName}) should not be NaN");
            TestAssert.IsFalse(double.IsInfinity(val),
                $"GetFactionStatValue({stat.defName}) should not be infinite");
        }
    }
}
