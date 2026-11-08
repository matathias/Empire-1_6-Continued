using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace FactionColonies;

/* Non-destructive cover for the core settings, world object, comp, Harmony and def helpers:
   stat aggregation, difficulty presets, settings text buffers, caravan/settlement stock math,
   settlement short names and stat breakdowns, relation mirroring, upgrade-tree recursion, the
   PColony def snapshot, and the goodwill patch targets. Everything here works on fresh,
   unregistered objects (defs, factions, an unspawned settlement) or on settings that are put back
   in a finally block, so no live game state is left changed. */
public static class CoreWorldobjectsTests
{
    /*-*-*-*-*/
    /* Helpers */
    /*-*-*-*-*/

    private static FCStatDef MakeStat(FCStatAggregation aggregation) =>
        new() { defName = $"TestStat_{aggregation}", aggregation = aggregation, appliesToSettlements = true };

    // The tax interval has no public setter; tests that change it through a preset write the
    // original back through this accessor.
    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "timeBetweenTaxes_days")]
    private static extern ref int TaxIntervalDays(FCSettings owner);

    private readonly struct PresetSettingsSnapshot
    {
        private readonly int silver, taxDays, tithe, workerCost;
        private readonly float baseBonus, multBonus, upkeepMult;

        public PresetSettingsSnapshot(bool _)
        {
            silver = FCSettings.silverPerResource;
            taxDays = TaxIntervalDays(null);
            tithe = FCSettings.productionTitheMod;
            workerCost = FCSettings.workerCost;
            baseBonus = FCSettings.workerProductionBaseBonus;
            multBonus = FCSettings.workerProductionMultBonus;
            upkeepMult = FCSettings.buildingUpkeepDifficultyMult;
        }

        public void Restore()
        {
            FCSettings.silverPerResource = silver;
            TaxIntervalDays(null) = taxDays;
            FCSettings.productionTitheMod = tithe;
            FCSettings.workerCost = workerCost;
            FCSettings.workerProductionBaseBonus = baseBonus;
            FCSettings.workerProductionMultBonus = multBonus;
            FCSettings.buildingUpkeepDifficultyMult = upkeepMult;
        }
    }

    private static void AssertPresetApplied(EmpireDifficultyLevel level, int silver, int taxDays, int tithe,
        int workerCost, float baseBonus, float multBonus)
    {
        PresetSettingsSnapshot snapshot = new(true);
        try
        {
            // Scramble first so a preset that skips a field can't pass on leftover values.
            FCSettings.silverPerResource = -1;
            TaxIntervalDays(null) = 999;
            FCSettings.productionTitheMod = -1;
            FCSettings.workerCost = -1;
            FCSettings.workerProductionBaseBonus = -1f;
            FCSettings.workerProductionMultBonus = -1f;
            FCSettings.buildingUpkeepDifficultyMult = 7f;

            FCSettings.ApplyDifficultyPreset(level);

            TestAssert.AreEqual(silver, FCSettings.silverPerResource, $"{level}: silverPerResource");
            TestAssert.AreEqual(taxDays, TaxIntervalDays(null), $"{level}: tax interval days");
            TestAssert.AreEqual(tithe, FCSettings.productionTitheMod, $"{level}: productionTitheMod");
            TestAssert.AreEqual(workerCost, FCSettings.workerCost, $"{level}: workerCost");
            TestAssert.AreEqual(baseBonus, FCSettings.workerProductionBaseBonus, 0.0001, $"{level}: workerProductionBaseBonus");
            TestAssert.AreEqual(multBonus, FCSettings.workerProductionMultBonus, 0.0001, $"{level}: workerProductionMultBonus");
            TestAssert.AreEqual(1.0, FCSettings.buildingUpkeepDifficultyMult, 0.0001, $"{level}: buildingUpkeepDifficultyMult resets to 1");
            TestAssert.AreEqual(taxDays, FCSettings.DaysBetweenTaxesByDifficulty(level), $"{level}: DaysBetweenTaxesByDifficulty");
        }
        finally
        {
            snapshot.Restore();
        }
    }

    /*-*-*-*-*/
    /* FCStatDef.Combine */
    /*-*-*-*-*/

    [EmpireTest("Core.Stat", GameState = TestGameState.Any)]
    public static void Combine_Additive_Sums()
    {
        FCStatDef stat = MakeStat(FCStatAggregation.Additive);
        TestAssert.AreEqual(5.5, stat.Combine(2, 3.5));
        TestAssert.AreEqual(-1, stat.Combine(2, -3));
    }

    [EmpireTest("Core.Stat", GameState = TestGameState.Any)]
    public static void Combine_Multiplicative_Multiplies()
    {
        FCStatDef stat = MakeStat(FCStatAggregation.Multiplicative);
        TestAssert.AreEqual(7.0, stat.Combine(2, 3.5));
        TestAssert.AreEqual(0.0, stat.Combine(4, 0));
    }

    [EmpireTest("Core.Stat", GameState = TestGameState.Any)]
    public static void Combine_IdentityValue_IsNeutral()
    {
        foreach (FCStatAggregation aggregation in new[] { FCStatAggregation.Additive, FCStatAggregation.Multiplicative })
        {
            FCStatDef stat = MakeStat(aggregation);
            TestAssert.AreEqual(1.75, stat.Combine(stat.IdentityValue, 1.75), message: $"{aggregation}: identity on the left");
            TestAssert.AreEqual(1.75, stat.Combine(1.75, stat.IdentityValue), message: $"{aggregation}: identity on the right");
        }
    }

    /*-*-*-*-*/
    /* Difficulty presets */
    /*-*-*-*-*/

    [EmpireTest("Core.Settings", GameState = TestGameState.Any)]
    public static void Preset_Peaceful_AppliesPeacefulDefaults() =>
        AssertPresetApplied(EmpireDifficultyLevel.Peaceful,
            FCSettings.DEFAULT_SILVER_PER_RESOURCE_PEACEFUL, FCSettings.DEFAULT_TAX_INTERVAL_DAYS_PEACEFUL,
            FCSettings.DEFAULT_PRODUCTION_TITHE_MOD_PEACEFUL, FCSettings.DEFAULT_WORKER_COST_PEACEFUL,
            FCSettings.DEFAULT_WORKER_PROD_BASE_BONUS_PEACEFUL, FCSettings.DEFAULT_WORKER_PROD_MULT_BONUS_PEACEFUL);

    [EmpireTest("Core.Settings", GameState = TestGameState.Any)]
    public static void Preset_CommunityBuilder_AppliesCommunityBuilderDefaults() =>
        AssertPresetApplied(EmpireDifficultyLevel.CommunityBuilder,
            FCSettings.DEFAULT_SILVER_PER_RESOURCE_COMMUNITYBUILDER, FCSettings.DEFAULT_TAX_INTERVAL_DAYS_COMMUNITYBUILDER,
            FCSettings.DEFAULT_PRODUCTION_TITHE_MOD_COMMUNITYBUILDER, FCSettings.DEFAULT_WORKER_COST_COMMUNITYBUILDER,
            FCSettings.DEFAULT_WORKER_PROD_BASE_BONUS_COMMUNITYBUILDER, FCSettings.DEFAULT_WORKER_PROD_MULT_BONUS_COMMUNITYBUILDER);

    [EmpireTest("Core.Settings", GameState = TestGameState.Any)]
    public static void Preset_AdventureStory_AppliesAdventureStoryDefaults() =>
        AssertPresetApplied(EmpireDifficultyLevel.AdventureStory,
            FCSettings.DEFAULT_SILVER_PER_RESOURCE_ADVENTURESTORY, FCSettings.DEFAULT_TAX_INTERVAL_DAYS_ADVENTURESTORY,
            FCSettings.DEFAULT_PRODUCTION_TITHE_MOD_ADVENTURESTORY, FCSettings.DEFAULT_WORKER_COST_ADVENTURESTORY,
            FCSettings.DEFAULT_WORKER_PROD_BASE_BONUS_ADVENTURESTORY, FCSettings.DEFAULT_WORKER_PROD_MULT_BONUS_ADVENTURESTORY);

    [EmpireTest("Core.Settings", GameState = TestGameState.Any)]
    public static void Preset_StriveToSurvive_AppliesStriveToSurviveDefaults() =>
        AssertPresetApplied(EmpireDifficultyLevel.StriveToSurvive,
            FCSettings.DEFAULT_SILVER_PER_RESOURCE_STRIVETOSURVIVE, FCSettings.DEFAULT_TAX_INTERVAL_DAYS_STRIVETOSURVIVE,
            FCSettings.DEFAULT_PRODUCTION_TITHE_MOD_STRIVETOSURVIVE, FCSettings.DEFAULT_WORKER_COST_STRIVETOSURVIVE,
            FCSettings.DEFAULT_WORKER_PROD_BASE_BONUS_STRIVETOSURVIVE, FCSettings.DEFAULT_WORKER_PROD_MULT_BONUS_STRIVETOSURVIVE);

    [EmpireTest("Core.Settings", GameState = TestGameState.Any)]
    public static void Preset_BloodAndDust_AppliesBloodAndDustDefaults() =>
        AssertPresetApplied(EmpireDifficultyLevel.BloodAndDust,
            FCSettings.DEFAULT_SILVER_PER_RESOURCE_BLOODANDDUST, FCSettings.DEFAULT_TAX_INTERVAL_DAYS_BLOODANDDUST,
            FCSettings.DEFAULT_PRODUCTION_TITHE_MOD_BLOODANDDUST, FCSettings.DEFAULT_WORKER_COST_BLOODANDDUST,
            FCSettings.DEFAULT_WORKER_PROD_BASE_BONUS_BLOODANDDUST, FCSettings.DEFAULT_WORKER_PROD_MULT_BONUS_BLOODANDDUST);

    [EmpireTest("Core.Settings", GameState = TestGameState.Any)]
    public static void Preset_LosingIsFun_AppliesLosingIsFunDefaults() =>
        AssertPresetApplied(EmpireDifficultyLevel.LosingIsFun,
            FCSettings.DEFAULT_SILVER_PER_RESOURCE_LOSINGISFUN, FCSettings.DEFAULT_TAX_INTERVAL_DAYS_LOSINGISFUN,
            FCSettings.DEFAULT_PRODUCTION_TITHE_MOD_LOSINGISFUN, FCSettings.DEFAULT_WORKER_COST_LOSINGISFUN,
            FCSettings.DEFAULT_WORKER_PROD_BASE_BONUS_LOSINGISFUN, FCSettings.DEFAULT_WORKER_PROD_MULT_BONUS_LOSINGISFUN);

    [EmpireTest("Core.Settings", GameState = TestGameState.Any)]
    public static void Preset_Custom_LeavesEverySettingAlone()
    {
        PresetSettingsSnapshot snapshot = new(true);
        try
        {
            FCSettings.silverPerResource = 77;
            TaxIntervalDays(null) = 13;
            FCSettings.productionTitheMod = 7;
            FCSettings.workerCost = 41;
            FCSettings.workerProductionBaseBonus = 0.3f;
            FCSettings.workerProductionMultBonus = 1.7f;
            FCSettings.buildingUpkeepDifficultyMult = 2.5f;

            FCSettings.ApplyDifficultyPreset(EmpireDifficultyLevel.Custom);

            TestAssert.AreEqual(77, FCSettings.silverPerResource, "Custom: silverPerResource unchanged");
            TestAssert.AreEqual(13, TaxIntervalDays(null), "Custom: tax interval unchanged");
            TestAssert.AreEqual(7, FCSettings.productionTitheMod, "Custom: productionTitheMod unchanged");
            TestAssert.AreEqual(41, FCSettings.workerCost, "Custom: workerCost unchanged");
            TestAssert.AreEqual(0.3, FCSettings.workerProductionBaseBonus, 0.0001, "Custom: base bonus unchanged");
            TestAssert.AreEqual(1.7, FCSettings.workerProductionMultBonus, 0.0001, "Custom: mult bonus unchanged");
            TestAssert.AreEqual(2.5, FCSettings.buildingUpkeepDifficultyMult, 0.0001, "Custom: upkeep mult unchanged");
        }
        finally
        {
            snapshot.Restore();
        }
    }

    [EmpireTest("Core.Settings", GameState = TestGameState.Any)]
    public static void DaysBetweenTaxes_CustomFallsBackToDefaultInterval()
    {
        TestAssert.AreEqual(FCSettings.DEFAULT_TAX_INTERVAL_DAYS,
            FCSettings.DaysBetweenTaxesByDifficulty(EmpireDifficultyLevel.Custom));
        TestAssert.AreEqual(FCSettings.DEFAULT_TAX_INTERVAL_DAYS * GenDate.TicksPerDay,
            FCSettings.TicksBetweenTaxesByDifficulty(EmpireDifficultyLevel.Custom));
    }

    /*-*-*-*-*/
    /* Settings text buffers */
    /*-*-*-*-*/

    [EmpireTest("Core.Settings", GameState = TestGameState.Any)]
    public static void SyncIntBuffer_NullBuffer_IsBuiltFromValue()
    {
        int synced = 0;
        string buffer = null;
        FCSettings.SyncIntBuffer(42, ref synced, ref buffer);
        TestAssert.AreEqual("42", buffer);
        TestAssert.AreEqual(42, synced);
    }

    [EmpireTest("Core.Settings", GameState = TestGameState.Any)]
    public static void SyncIntBuffer_UnchangedValue_KeepsHalfTypedEntry()
    {
        int synced = 42;
        string buffer = "";
        FCSettings.SyncIntBuffer(42, ref synced, ref buffer);
        TestAssert.AreEqual("", buffer, "an emptied field must stay empty while its value is unchanged");

        buffer = "4";
        FCSettings.SyncIntBuffer(42, ref synced, ref buffer);
        TestAssert.AreEqual("4", buffer, "a partly typed entry must survive the next frame");
    }

    [EmpireTest("Core.Settings", GameState = TestGameState.Any)]
    public static void SyncIntBuffer_ValueChangedElsewhere_RebuildsBuffer()
    {
        int synced = 42;
        string buffer = "4";
        FCSettings.SyncIntBuffer(10, ref synced, ref buffer);
        TestAssert.AreEqual("10", buffer, "a preset or reset button changing the value must show the new value");
        TestAssert.AreEqual(10, synced);
    }

    /*-*-*-*-*/
    /* Caravan and settlement stock */
    /*-*-*-*-*/

    [EmpireTest("Caravan", GameState = TestGameState.Any)]
    public static void SizeFactor_BelowReference_ShrinksSublinearly()
    {
        TestAssert.AreEqual(Mathf.Sqrt(1f / 3f), CaravanStockMath.SizeFactor(1, 3), 0.0001);
    }

    [EmpireTest("Caravan", GameState = TestGameState.Any)]
    public static void SizeFactor_NoProducers_TreatedAsOne()
    {
        TestAssert.AreEqual(CaravanStockMath.SizeFactor(1, 3), CaravanStockMath.SizeFactor(0, 3), 0.0001);
        TestAssert.AreEqual(CaravanStockMath.SizeFactor(1, 3), CaravanStockMath.SizeFactor(-5, 3), 0.0001);
    }

    [EmpireTest("Caravan", GameState = TestGameState.Any)]
    public static void SizeFactor_NonPositiveReference_IsNeutral()
    {
        TestAssert.AreEqual(1.0, CaravanStockMath.SizeFactor(10, 0), 0.0001);
        TestAssert.AreEqual(1.0, CaravanStockMath.SizeFactor(10, -2), 0.0001);
    }

    [EmpireTest("Caravan", GameState = TestGameState.Any)]
    public static void TargetStacks_SizeFactorStaysInsideClamp()
    {
        // 16 producers over a reference of 1 is a 4x size factor; with 2x tech it would be 8x, so the 4x cap holds.
        TestAssert.AreEqual(Mathf.RoundToInt(30 * CaravanStockMath.MaxStackScale),
            CaravanStockMath.TargetStacks(30, 6f, 3f, CaravanStockMath.SizeFactor(16, 1)));
        // A tiny empire at low tech still carries at least the 0.5x floor.
        TestAssert.AreEqual(Mathf.RoundToInt(30 * CaravanStockMath.MinStackScale),
            CaravanStockMath.TargetStacks(30, 0.75f, 3f, CaravanStockMath.SizeFactor(1, 9)));
    }

    [EmpireTest("Caravan", GameState = TestGameState.Any)]
    public static void TargetStacks_IndustrialAtReference_IsBaseline()
    {
        // TechScaling at Industrial is BaseExtraScale itself; the reference settlement count is 1x.
        TestAssert.AreEqual(30, CaravanStockMath.TargetStacks(30, EmpireStockGenerator.BaseExtraScale,
            EmpireStockGenerator.BaseExtraScale, CaravanStockMath.SizeFactor(3, 3)));
    }

    [EmpireTest("Caravan")]
    public static void TechScaling_NeverIncludesSilverNormalization()
    {
        // ExtraScaling is TechScaling times 100 / silverPerResource; TechScaling alone feeds stack counts.
        TestUtil.WithOverride(() => FCSettings.silverPerResource, v => FCSettings.silverPerResource = v, 5, () =>
        {
            float tech = EmpireStockGenerator.TechScaling();
            TestAssert.AreEqual(tech * 20f, EmpireStockGenerator.ExtraScaling(), 0.001,
                "ExtraScaling should be TechScaling x (100 / 5)");
            TestAssert.LessThanOrEqual(tech, EmpireStockGenerator.BaseExtraScale * 10f,
                "TechScaling is bounded by the Archotech factor, independent of silverPerResource");
        });
    }

    [EmpireTest("Caravan")]
    public static void StockUtil_UnitValue_UsesMarketValueAndFloorsAtOne()
    {
        TestAssert.AreEqual(ThingDefOf.Gold.BaseMarketValue, EmpireStockUtil.UnitValue(ThingDefOf.Gold), 0.0001);

        ThingDef worthless = new() { defName = "TestWorthlessStock" };
        worthless.BaseMarketValue = 0f;
        TestAssert.AreEqual(1.0, EmpireStockUtil.UnitValue(worthless), 0.0001, "a zero market value is floored at 1");
    }

    [EmpireTest("Caravan")]
    public static void StockUtil_RandomizedUnits_StaysInBudgetBand()
    {
        Rand.PushState(12345);
        try
        {
            float unit = EmpireStockUtil.UnitValue(ThingDefOf.Silver);
            for (int i = 0; i < 200; i++)
            {
                int units = EmpireStockUtil.RandomizedUnits(ThingDefOf.Silver, 100f);
                TestAssert.IsTrue(units >= Mathf.RoundToInt(50f / unit) && units <= Mathf.RoundToInt(150f / unit),
                    $"units {units} outside the 0.5x-1.5x band of a 100 budget");
            }
        }
        finally
        {
            Rand.PopState();
        }
    }

    [EmpireTest("Caravan")]
    public static void StockUtil_RandomizedUnits_BonusIsFlat()
    {
        float unit = EmpireStockUtil.UnitValue(ThingDefOf.Silver);
        TestAssert.AreEqual(Mathf.RoundToInt(50f / unit), EmpireStockUtil.RandomizedUnits(ThingDefOf.Silver, 0f, 50f),
            "with no per-item budget only the flat bonus buys units");
    }

    [EmpireTest("Caravan")]
    public static void StockUtil_SellableCandidates_DropsUnsellableDefs()
    {
        ThingDef sellable = new() { defName = "TestSellable", tradeability = Tradeability.All };
        ThingDef buyable = new() { defName = "TestBuyable", tradeability = Tradeability.Buyable };
        ThingDef neither = new() { defName = "TestUntradeable", tradeability = Tradeability.None };
        ThingDef sellOnly = new() { defName = "TestSellOnly", tradeability = Tradeability.Sellable };

        List<ThingDef> result = EmpireStockUtil.SellableCandidates([sellable, buyable, neither, sellOnly]);

        TestAssert.AreEqual(2, result.Count, "only defs a trader can sell remain");
        TestAssert.Contains(result, sellable);
        TestAssert.Contains(result, buyable);
    }

    [EmpireTest("Caravan")]
    public static void StockUtil_GenerateStockPawns_NullKindYieldsNothing()
    {
        TestAssert.IsEmpty(EmpireStockUtil.GenerateStockPawns(null, 5).ToList());
    }

    [EmpireTest("Caravan", GameState = TestGameState.Any)]
    public static void StockGenerator_AnimalCapsHaveSaneDefaults()
    {
        EmpireStockGenerator gen = new();
        TestAssert.GreaterThan(gen.maxAnimalsPerKind, 0, "maxAnimalsPerKind");
        TestAssert.IsTrue(gen.maxAnimalCount >= gen.maxAnimalsPerKind, "the total animal cap is at least the per-kind cap");
    }

    /*-*-*-*-*/
    /* Settlement short name */
    /*-*-*-*-*/

    [EmpireTest("Core.Settlement")]
    public static void ShortName_Default_DerivedFromName()
    {
        WorldSettlementFC s = new() { Name = "Imperial Guard" };
        TestAssert.AreEqual(TextUtil.ToShortName("Imperial Guard"), s.ShortName);
    }

    [EmpireTest("Core.Settlement")]
    public static void ShortName_Derived_FollowsRename()
    {
        WorldSettlementFC s = new() { Name = "Imperial Guard" };
        _ = s.ShortName;
        s.SetName("Crimson Fleet");
        TestAssert.AreEqual(TextUtil.ToShortName("Crimson Fleet"), s.ShortName,
            "an uncustomized short name must follow the new name");
    }

    [EmpireTest("Core.Settlement")]
    public static void ShortName_Custom_SurvivesRename()
    {
        WorldSettlementFC s = new() { Name = "Imperial Guard" };
        s.SetShortName("IGX");
        TestAssert.AreEqual("IGX", s.ShortName);
        s.SetName("Crimson Fleet");
        TestAssert.AreEqual("IGX", s.ShortName, "a custom short name is kept across renames");
    }

    [EmpireTest("Core.Settlement")]
    public static void ShortName_SetToDerivedValue_ClearsCustomization()
    {
        WorldSettlementFC s = new() { Name = "Imperial Guard" };
        s.SetShortName("IGX");
        s.ShortName = TextUtil.ToShortName("Imperial Guard");
        s.SetName("Crimson Fleet");
        TestAssert.AreEqual(TextUtil.ToShortName("Crimson Fleet"), s.ShortName,
            "setting the short name back to the derived one makes it follow renames again");
    }

    [EmpireTest("Core.Settlement")]
    public static void ShortName_SetEmpty_UsesFullName()
    {
        WorldSettlementFC s = new() { Name = "Imperial Guard" };
        s.ShortName = "";
        TestAssert.AreEqual("Imperial Guard", s.ShortName);
    }

    /*-*-*-*-*/
    /* Settlement stat value (listed modifiers) */
    /*-*-*-*-*/

    [EmpireTest("Core.Stat")]
    public static void SettlementStat_Additive_SumsTaggedPermanentAndDecaying()
    {
        FCStatDef stat = MakeStat(FCStatAggregation.Additive);
        WorldSettlementFC s = new() { Name = "Stat Test" };

        s.AddStatModifiers([new FCStatModifier { stat = stat, value = 2 }], "testTagged", "Tagged");
        s.AddPermanentModifiers([new FCStatModifier { stat = stat, value = 3 }], "testPermanent", "Permanent");
        s.AddDecayingPenalty(stat, 10, 5, "testDecaying", "Decaying"); // 2 per day

        TestAssert.AreEqual(2 + 3 + 2, s.GetSettlementStatValue(stat));
    }

    [EmpireTest("Core.Stat")]
    public static void SettlementStat_Multiplicative_MultipliesListedAndAddsDecayingSlice()
    {
        FCStatDef stat = MakeStat(FCStatAggregation.Multiplicative);
        WorldSettlementFC s = new() { Name = "Stat Test" };

        s.AddStatModifiers([new FCStatModifier { stat = stat, value = 2 }], "testTagged", "Tagged");
        s.AddPermanentModifiers([new FCStatModifier { stat = stat, value = 1.5 }], "testPermanent", "Permanent");
        TestAssert.AreEqual(3.0, s.GetSettlementStatValue(stat), message: "tagged x permanent");

        // A decaying penalty is always a flat per-day slice, even on a multiplicative stat.
        s.AddDecayingPenalty(stat, 1, 2, "testDecaying", "Decaying");
        TestAssert.AreEqual(3.5, s.GetSettlementStatValue(stat), message: "tagged x permanent + decaying slice");
    }

    [EmpireTest("Core.Stat")]
    public static void SettlementStat_IgnoresOtherStats_AndCachesUntilInvalidated()
    {
        FCStatDef stat = MakeStat(FCStatAggregation.Additive);
        FCStatDef other = new() { defName = "TestStat_Other", aggregation = FCStatAggregation.Additive };
        WorldSettlementFC s = new() { Name = "Stat Test" };

        s.AddStatModifiers([new FCStatModifier { stat = other, value = 100 }], "testOther", "Other");
        TestAssert.AreEqual(0.0, s.GetSettlementStatValue(stat), message: "modifiers for another stat don't count");

        s.AddStatModifiers([new FCStatModifier { stat = stat, value = 4 }], "testTagged", "Tagged");
        TestAssert.AreEqual(4.0, s.GetSettlementStatValue(stat));
        s.RemoveStatModifiersBySource("testTagged");
        TestAssert.AreEqual(0.0, s.GetSettlementStatValue(stat), message: "removal invalidates the cached value");
    }

    /*-*-*-*-*/
    /* Relation mirroring */
    /*-*-*-*-*/

    [EmpireTest("Core.Harmony")]
    public static void RelationMirror_PlayerFirst_ReturnsOtherSide()
    {
        Faction player = new(), empire = new(), third = new();
        TestAssert.IsTrue(EmpireRelationMirror.TryGetThirdParty(player, third, player, empire, out Faction result));
        TestAssert.IsTrue(result == third, "the non-player side is the third party");
    }

    [EmpireTest("Core.Harmony")]
    public static void RelationMirror_PlayerSecond_ReturnsOtherSide()
    {
        Faction player = new(), empire = new(), third = new();
        TestAssert.IsTrue(EmpireRelationMirror.TryGetThirdParty(third, player, player, empire, out Faction result));
        TestAssert.IsTrue(result == third, "the non-player side is the third party");
    }

    [EmpireTest("Core.Harmony")]
    public static void RelationMirror_PlayerAndEmpire_IsNotMirrored()
    {
        Faction player = new(), empire = new();
        TestAssert.IsFalse(EmpireRelationMirror.TryGetThirdParty(player, empire, player, empire, out Faction a));
        TestAssert.IsNull(a);
        TestAssert.IsFalse(EmpireRelationMirror.TryGetThirdParty(empire, player, player, empire, out Faction b));
        TestAssert.IsNull(b);
    }

    [EmpireTest("Core.Harmony")]
    public static void RelationMirror_NoPlayerSide_IsNotMirrored()
    {
        Faction player = new(), empire = new(), x = new(), y = new();
        TestAssert.IsFalse(EmpireRelationMirror.TryGetThirdParty(x, y, player, empire, out Faction result));
        TestAssert.IsNull(result);
    }

    /*-*-*-*-*/
    /* Goodwill patch targets */
    /*-*-*-*-*/

    [EmpireTest("Core.Harmony")]
    public static void GoodwillSkipEmpire_PatchesAllFiveNotifyMethods()
    {
        string[] names =
        [
            nameof(Faction.Notify_PlayerTraded), nameof(Faction.Notify_MemberTookDamage),
            nameof(Faction.Notify_BuildingTookDamage), nameof(Faction.Notify_MemberStripped),
            nameof(Faction.Notify_BuildingRemoved),
        ];
        foreach (string name in names)
        {
            var method = AccessTools.Method(typeof(Faction), name);
            TestAssert.IsNotNull(method, $"Faction.{name} exists");
            Patches info = Harmony.GetPatchInfo(method);
            bool patched = info?.Prefixes.Any(p => p.PatchMethod.DeclaringType == typeof(GoodwillPatchFunctionsSkipEmpire)) == true;
            TestAssert.IsTrue(patched, $"Faction.{name} carries the skip-Empire goodwill prefix");
        }
    }

    [EmpireTest("Core.Defs")]
    public static void TraderKindDefOf_ResearchTraderResolves()
    {
        TestAssert.IsNotNull(FCTraderKindDefOf.FCResearchTrader, "FCTraderKindDefOf.FCResearchTrader");
        TestAssert.AreEqual("FCResearchTrader", FCTraderKindDefOf.FCResearchTrader.defName);
    }

    /*-*-*-*-*/
    /* Upgrade trees */
    /*-*-*-*-*/

    private static BuildingFCDef MakeBuilding(string name) => new() { defName = $"TestBuilding_{name}" };

    [EmpireTest("Core.Defs", GameState = TestGameState.Any)]
    public static void UpgradeTree_TwoNodeCycle_IsCutInsteadOfRecursing()
    {
        BuildingFCDef a = MakeBuilding("A"), b = MakeBuilding("B");
        a.upgrades = [b];
        b.upgrades = [a];

        List<BuildingUpgradeEntry> tree = [];
        TestAssert.DoesNotThrow(() => FactionCache.CollectUpgradeTree(a, 0, null, tree, []));
        TestAssert.AreEqual(1, tree.Count, "only B is listed under A; the edge back to A is cut");
        TestAssert.IsTrue(tree[0].def == b && tree[0].parent == a && tree[0].depth == 0, "B is a depth-0 upgrade of A");
    }

    [EmpireTest("Core.Defs", GameState = TestGameState.Any)]
    public static void UpgradeTree_SelfCycle_IsEmpty()
    {
        BuildingFCDef a = MakeBuilding("Self");
        a.upgrades = [a];

        List<BuildingUpgradeEntry> tree = [];
        FactionCache.CollectUpgradeTree(a, 0, null, tree, []);
        TestAssert.IsEmpty(tree, "a building can't list itself as its own upgrade");
    }

    [EmpireTest("Core.Defs", GameState = TestGameState.Any)]
    public static void UpgradeTree_Diamond_KeepsSharedUpgradeOnBothBranches()
    {
        // A -> B -> D and A -> C -> D: D is not an ancestor on either path, so it shows up twice.
        BuildingFCDef a = MakeBuilding("DA"), b = MakeBuilding("DB"), c = MakeBuilding("DC"), d = MakeBuilding("DD");
        a.upgrades = [b, c];
        b.upgrades = [d];
        c.upgrades = [d];

        List<BuildingUpgradeEntry> tree = [];
        FactionCache.CollectUpgradeTree(a, 0, null, tree, []);
        TestAssert.AreEqual(4, tree.Count, "B, D, C, D");
        TestAssert.AreEqual(2, tree.Count(e => e.def == d), "D under both B and C");
        TestAssert.IsTrue(tree.Where(e => e.def == d).All(e => e.depth == 1), "D sits one level below A's direct upgrades");
    }

    /*-*-*-*-*/
    /* PColony def snapshot */
    /*-*-*-*-*/

    [EmpireTest("Core.Defs", GameState = TestGameState.Any)]
    public static void CopyFilter_IsIndependentOfSource()
    {
        ThingFilter source = new();
        source.SetAllow(ThingDefOf.Silver, true);

        ThingFilter copy = PColonyDefSnapshot.CopyFilter(source);
        TestAssert.IsTrue(copy is not null && copy != source, "the copy is a new filter");
        TestAssert.IsTrue(copy.Allows(ThingDefOf.Silver), "the copy keeps the source's allowances");

        copy.SetAllow(ThingDefOf.Gold, true);
        TestAssert.IsFalse(source.Allows(ThingDefOf.Gold), "editing the copy must not reach the source");
        TestAssert.IsNull(PColonyDefSnapshot.CopyFilter(null));
    }

    [EmpireTest("Core.Defs")]
    public static void Snapshot_Restore_HandsOutFreshCopies()
    {
        FactionDef pristine = DefDatabase<FactionDef>.GetNamedSilentFail("PColony");
        TestAssert.IsNotNull(pristine, "the PColony def exists");

        FactionDef first = new() { defName = "TestPColonyA" };
        FactionDef second = new() { defName = "TestPColonyB" };
        PColonyDefSnapshot.Restore(first);
        PColonyDefSnapshot.Restore(second);

        if (first.apparelStuffFilter is not null)
            TestAssert.IsTrue(first.apparelStuffFilter != second.apparelStuffFilter,
                "each restore gets its own apparel filter, so one game's SetAllow can't reach the next");
        if (first.caravanTraderKinds is not null)
            TestAssert.IsTrue(first.caravanTraderKinds != second.caravanTraderKinds,
                "each restore gets its own caravan trader list");
        TestAssert.IsTrue(first.techLevel == second.techLevel && first.basicMemberKind == second.basicMemberKind,
            "every restore writes the same startup values");
        TestAssert.DoesNotThrow(() => PColonyDefSnapshot.Restore(null), "a missing def is ignored");
    }

    /*-*-*-*-*/
    /* Expanding icon and comp properties */
    /*-*-*-*-*/

    [EmpireTest("Core.Settlement")]
    public static void SetExpandingIcon_SetsPathAndReplacesCachedTexture()
    {
        const string path = "World/WorldObjects/Expanding/Caravan";
        WorldObjectDef def = new() { defName = "TestExpandingIcon" };
        PrivateFieldUtil.ExpandingIconCache(def) = BaseContent.WhiteTex;

        WorldSettlementFC.SetExpandingIcon(def, path);

        TestAssert.AreEqual(path, def.expandingIconTexture);
        TestAssert.IsTrue(def.ExpandingIconTexture == ContentFinder<Texture2D>.Get(path),
            "the cached texture is replaced, not left on the old icon");
    }

    [EmpireTest("Core.Defs", GameState = TestGameState.Any)]
    public static void MapParentCompProperties_RejectsNonMapParent()
    {
        WorldObjectCompProperties_SettlementBuildings props = new();
        WorldObjectDef notMapParent = new() { defName = "TestNotMapParent", worldObjectClass = typeof(Caravan) };
        WorldObjectDef mapParent = new() { defName = "TestMapParent", worldObjectClass = typeof(WorldSettlementFC) };

        string error = props.ConfigErrors(notMapParent).FirstOrDefault(e => e.Contains("MapParent"));
        TestAssert.IsNotNull(error, "a comp attached to a non-MapParent world object is reported");
        TestAssert.IsTrue(error.Contains(nameof(WorldObjectCompProperties_SettlementBuildings)),
            "the error names the comp properties class");
        TestAssert.IsFalse(props.ConfigErrors(mapParent).Any(e => e.Contains("MapParent")),
            "a MapParent world object is accepted");
    }
}
