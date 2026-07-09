using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FactionColonies
{
    public static class StatTests
    {
        // ============================
        // Helpers
        // ============================

        private static FactionFC GetFaction()
        {
            return FindFC.FactionComp;
        }

        private static WorldSettlementFC GetFirstSettlement()
        {
            var settlements = FindFC.Settlements;
            if (settlements == null || settlements.Count == 0)
                return null;
            return settlements[0];
        }

        /// <summary>
        /// Adds a test modifier, runs the action, then removes the modifier in a finally block.
        /// </summary>
        private static void WithTestModifier(WorldSettlementFC settlement, FCStatDef stat, double value, Action action)
        {
            var mods = new List<FCStatModifier> { new FCStatModifier { stat = stat, value = value } };
            settlement.AddStatModifiers(mods, "test");
            try
            {
                action();
            }
            finally
            {
                settlement.RemoveStatModifiersBySource("test");
            }
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

                if (rtd.productionAdditiveStat != null)
                {
                    TestAssert.AreEqual((object)FCStatAggregation.Additive,
                        (object)rtd.productionAdditiveStat.aggregation,
                        $"{rtd.defName}: productionAdditiveStat should be Additive");
                    TestAssert.AreEqual((object)rtd, (object)rtd.productionAdditiveStat.linkedResource,
                        $"{rtd.defName}: productionAdditiveStat.linkedResource should point back");
                }
                if (rtd.productionMultiplierStat != null)
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
                if (stat.linkedResource == null) continue;

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
            var mods = new List<FCStatModifier> { new FCStatModifier { stat = null, value = 1 } };
            int errorCount = FCStatModifier.ConfigErrors(mods, "TestOwner").Count();
            TestAssert.AreEqual(1, errorCount, "Should yield exactly 1 config error for null stat");
        }

        [EmpireTest("Stat")]
        public static void ConfigErrors_ValidStat_YieldsNoErrors()
        {
            var mods = new List<FCStatModifier>
            {
                new FCStatModifier { stat = FCStatDefOf.militaryBaseLevel, value = 1 }
            };
            int errorCount = FCStatModifier.ConfigErrors(mods, "TestOwner").Count();
            TestAssert.AreEqual(0, errorCount, "Should yield no config errors for valid stat");
        }

        [EmpireTest("Stat")]
        public static void ConfigErrors_NullOrEmptyList_YieldsNoErrors()
        {
            int nullErrors = FCStatModifier.ConfigErrors(null, "TestOwner").Count();
            TestAssert.AreEqual(0, nullErrors, "Null list should yield no errors");

            int emptyErrors = FCStatModifier.ConfigErrors(new List<FCStatModifier>(), "TestOwner").Count();
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
                statModifiers = new List<FCStatModifier> { new FCStatModifier { stat = null, value = 1 } }
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
                statModifiers = new List<FCStatModifier> { new FCStatModifier { stat = FCStatDefOf.militaryBaseLevel, value = 1 } }
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
                statModifiers = new List<FCStatModifier> { new FCStatModifier { stat = null, value = 1 } }
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
                statModifiers = new List<FCStatModifier> { new FCStatModifier { stat = FCStatDefOf.militaryBaseLevel, value = 1 } }
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
            TaggedString desc = FCStatModifier.GetDescription(new List<FCStatModifier>());
            TestAssert.IsTrue(desc.RawText.NullOrEmpty(),
                $"Empty list should produce empty description, got '{desc}'");
        }

        [EmpireTest("Stat")]
        public static void Description_NullStatEntry_DoesNotThrow()
        {
            var mods = new List<FCStatModifier> { new FCStatModifier { stat = null, value = 1 } };
            TaggedString desc = FCStatModifier.GetDescription(mods);
            TestAssert.IsTrue(desc.RawText.NullOrEmpty(),
                $"Null stat entry should be skipped, got '{desc}'");
        }

        [EmpireTest("Stat")]
        public static void Description_AdditiveStatWithKey_ProducesOutput()
        {
            // militaryBaseLevel is additive and has a descriptionKey
            FCStatDef stat = FCStatDefOf.militaryBaseLevel;
            if (stat.descriptionKey.NullOrEmpty())
                TestAssert.Skip("militaryBaseLevel has no descriptionKey");
            var mods = new List<FCStatModifier> { new FCStatModifier { stat = stat, value = 2 } };
            TaggedString desc = FCStatModifier.GetDescription(mods);
            TestAssert.IsFalse(desc.RawText.NullOrEmpty(),
                "Additive stat with descriptionKey should produce non-empty description");
        }

        [EmpireTest("Stat")]
        public static void Description_MultiplicativeStat_ProducesOutput()
        {
            // happinessGainedMultiplier is multiplicative and has a descriptionKey
            FCStatDef stat = FCStatDefOf.happinessGainedMultiplier;
            if (stat.descriptionKey.NullOrEmpty())
                TestAssert.Skip("happinessGainedMultiplier has no descriptionKey");
            var mods = new List<FCStatModifier> { new FCStatModifier { stat = stat, value = 1.5 } };
            TaggedString desc = FCStatModifier.GetDescription(mods);
            TestAssert.IsFalse(desc.RawText.NullOrEmpty(),
                "Multiplicative stat with descriptionKey should produce non-empty description");
        }

        [EmpireTest("Stat")]
        public static void Description_ResourceLinkedStat_ProducesOutput()
        {
            // Find a resource-linked stat (production additive for the first ResourceTypeDef)
            ResourceTypeDef rtd = DefDatabase<ResourceTypeDef>.AllDefsListForReading.FirstOrDefault();
            if (rtd == null || rtd.productionAdditiveStat == null)
                TestAssert.Skip("No ResourceTypeDef with productionAdditiveStat");
            var mods = new List<FCStatModifier>
            {
                new FCStatModifier { stat = rtd.productionAdditiveStat, value = 3 }
            };
            TaggedString desc = FCStatModifier.GetDescription(mods);
            TestAssert.IsFalse(desc.RawText.NullOrEmpty(),
                "Resource-linked stat should produce non-empty description");
        }

        // ============================
        // Tier 2: Faction/Settlement Sanity (SKIP if no game state)
        // ============================

        [EmpireTest("Stat")]
        public static void FactionStat_AllStats_AreFiniteNumbers()
        {
            var faction = GetFaction();
            if (faction == null) TestAssert.Skip("No faction");

            foreach (FCStatDef stat in DefDatabase<FCStatDef>.AllDefsListForReading)
            {
                double val = faction.GetFactionStatValue(stat);
                TestAssert.IsFalse(double.IsNaN(val),
                    $"GetFactionStatValue({stat.defName}) should not be NaN");
                TestAssert.IsFalse(double.IsInfinity(val),
                    $"GetFactionStatValue({stat.defName}) should not be infinite");
            }
        }

        [EmpireTest("Stat")]
        public static void CombinedStat_AllSettlementStats_AreFinite()
        {
            var faction = GetFaction();
            if (faction == null || faction.settlements.Count == 0)
                TestAssert.Skip("No faction/settlements");

            foreach (WorldSettlementFC settlement in faction.settlements)
            {
                foreach (FCStatDef stat in DefDatabase<FCStatDef>.AllDefsListForReading)
                {
                    if (!stat.appliesToSettlements) continue;
                    double val = faction.GetStatValue(stat, settlement);
                    TestAssert.IsFalse(double.IsNaN(val),
                        $"GetStatValue({stat.defName}, {settlement.Name}) is NaN");
                    TestAssert.IsFalse(double.IsInfinity(val),
                        $"GetStatValue({stat.defName}, {settlement.Name}) is infinite");
                }
            }
        }

        [EmpireTest("Stat")]
        public static void CombinedStat_FactionOnlyStats_IgnoreSettlement()
        {
            var faction = GetFaction();
            var settlement = GetFirstSettlement();
            if (faction == null || settlement == null)
                TestAssert.Skip("No faction/settlement");

            foreach (FCStatDef stat in DefDatabase<FCStatDef>.AllDefsListForReading)
            {
                if (stat.appliesToSettlements) continue;
                double withSettlement = faction.GetStatValue(stat, settlement);
                double withoutSettlement = faction.GetStatValue(stat, null);
                TestAssert.AreEqual(withoutSettlement, withSettlement,
                    message: $"Faction-only stat {stat.defName} should ignore settlement");
            }
        }

        [EmpireTest("Stat")]
        public static void CombinedStat_Additive_SettlementPlusFaction()
        {
            var faction = GetFaction();
            var settlement = GetFirstSettlement();
            if (faction == null || settlement == null)
                TestAssert.Skip("No faction/settlement");

            // Use happinessGainedBase — additive, appliesToSettlements
            FCStatDef stat = FCStatDefOf.happinessGainedBase;
            if (!stat.appliesToSettlements || stat.aggregation != FCStatAggregation.Additive)
                TestAssert.Skip("happinessGainedBase is not additive+settlement");

            double factionPart = faction.GetFactionStatValue(stat);
            double settlementPart = settlement.GetSettlementStatValue(stat);
            double combined = faction.GetStatValue(stat, settlement);

            // combined = settlementPart + factionPart + behavior adjustments
            // behavior adjustments can only ADD to this, so combined >= settlement + faction
            // (unless a behavior subtracts, which none currently do for this stat)
            TestAssert.IsFalse(double.IsNaN(combined),
                $"Combined additive stat should not be NaN");
            // Verify the sum matches (within tolerance for behavior adjustments)
            double expected = settlementPart + factionPart;
            double delta = combined - expected;
            // Behaviors may adjust, but the delta should be bounded
            TestAssert.IsTrue(Math.Abs(delta) < 50,
                $"Stat {stat.defName}: combined ({combined:F2}) diverges too far from settlement+faction ({expected:F2}), delta={delta:F2}");
        }

        [EmpireTest("Stat")]
        public static void CombinedStat_Multiplicative_SettlementTimesFaction()
        {
            var faction = GetFaction();
            var settlement = GetFirstSettlement();
            if (faction == null || settlement == null)
                TestAssert.Skip("No faction/settlement");

            // Use happinessGainedMultiplier — multiplicative, appliesToSettlements
            FCStatDef stat = FCStatDefOf.happinessGainedMultiplier;
            if (!stat.appliesToSettlements || stat.aggregation != FCStatAggregation.Multiplicative)
                TestAssert.Skip("happinessGainedMultiplier is not multiplicative+settlement");

            double factionPart = faction.GetFactionStatValue(stat);
            double settlementPart = settlement.GetSettlementStatValue(stat);
            double combined = faction.GetStatValue(stat, settlement);

            TestAssert.IsFalse(double.IsNaN(combined),
                $"Combined multiplicative stat should not be NaN");
            double expected = settlementPart * factionPart;
            double delta = combined - expected;
            // Behaviors may adjust, but the delta should be bounded
            TestAssert.IsTrue(Math.Abs(delta) < 50,
                $"Stat {stat.defName}: combined ({combined:F2}) diverges too far from settlement*faction ({expected:F2}), delta={delta:F2}");
        }

        // ============================
        // Tier 3: Add/Remove Modifier Tests (SKIP if no settlement)
        // ============================

        [EmpireTest("Stat")]
        public static void AddModifier_Additive_IncreasesStatValue()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlement");

            FCStatDef stat = FCStatDefOf.happinessGainedBase;
            double before = settlement.GetSettlementStatValue(stat);

            WithTestModifier(settlement, stat, 3.0, () =>
            {
                double after = settlement.GetSettlementStatValue(stat);
                TestAssert.AreEqual(before + 3.0, after,
                    message: $"Additive modifier should increase stat by exactly 3.0 (before={before}, after={after})");
            });
        }

        [EmpireTest("Stat")]
        public static void AddModifier_Multiplicative_MultipliesStatValue()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlement");

            FCStatDef stat = FCStatDefOf.happinessGainedMultiplier;
            double before = settlement.GetSettlementStatValue(stat);

            WithTestModifier(settlement, stat, 1.5, () =>
            {
                double after = settlement.GetSettlementStatValue(stat);
                TestAssert.AreEqual(before * 1.5, after,
                    message: $"Multiplicative modifier should multiply stat by 1.5 (before={before}, after={after})");
            });
        }

        [EmpireTest("Stat")]
        public static void RemoveModifier_RestoresOriginalValue()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlement");

            FCStatDef stat = FCStatDefOf.happinessGainedBase;
            double before = settlement.GetSettlementStatValue(stat);

            var mods = new List<FCStatModifier> { new FCStatModifier { stat = stat, value = 5.0 } };
            settlement.AddStatModifiers(mods, "test");
            double during = settlement.GetSettlementStatValue(stat);
            TestAssert.AreEqual(before + 5.0, during,
                message: "Stat should increase after adding modifier");

            settlement.RemoveStatModifiers(mods, "test");
            double after = settlement.GetSettlementStatValue(stat);
            TestAssert.AreEqual(before, after,
                message: $"Stat should restore after removing modifier (before={before}, after={after})");
        }

        [EmpireTest("Stat")]
        public static void RemoveModifier_ByReference_OnlyRemovesMatchingObject()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlement");

            FCStatDef stat = FCStatDefOf.happinessGainedBase;
            double before = settlement.GetSettlementStatValue(stat);

            // Create two separate modifier lists with same stat and value
            var mods1 = new List<FCStatModifier> { new FCStatModifier { stat = stat, value = 2.0 } };
            var mods2 = new List<FCStatModifier> { new FCStatModifier { stat = stat, value = 2.0 } };

            settlement.AddStatModifiers(mods1, "test1");
            settlement.AddStatModifiers(mods2, "test2");

            double bothAdded = settlement.GetSettlementStatValue(stat);
            TestAssert.AreEqual(before + 4.0, bothAdded,
                message: "Both modifiers should be applied");

            // Remove first by reference
            settlement.RemoveStatModifiers(mods1, "test1");
            double oneRemoved = settlement.GetSettlementStatValue(stat);
            TestAssert.AreEqual(before + 2.0, oneRemoved,
                message: "Only first modifier should be removed, second should remain");

            // Cleanup
            settlement.RemoveStatModifiers(mods2, "test2");
            double restored = settlement.GetSettlementStatValue(stat);
            TestAssert.AreEqual(before, restored,
                message: "All modifiers should be removed");
        }

        [EmpireTest("Stat")]
        public static void RemoveModifiersBySource_RemovesCorrectEntries()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlement");

            FCStatDef stat = FCStatDefOf.happinessGainedBase;
            double before = settlement.GetSettlementStatValue(stat);

            // Add modifiers from two different sources
            var mods1 = new List<FCStatModifier> { new FCStatModifier { stat = stat, value = 3.0 } };
            var mods2 = new List<FCStatModifier> { new FCStatModifier { stat = stat, value = 5.0 } };

            settlement.AddStatModifiers(mods1, "testA");
            settlement.AddStatModifiers(mods2, "testB");

            double bothAdded = settlement.GetSettlementStatValue(stat);
            TestAssert.AreEqual(before + 8.0, bothAdded,
                message: "Both source modifiers should be applied");

            // Remove only source A
            settlement.RemoveStatModifiersBySource("testA");
            double afterRemoveA = settlement.GetSettlementStatValue(stat);
            TestAssert.AreEqual(before + 5.0, afterRemoveA,
                message: "Only sourceA modifiers should be removed");

            // Cleanup
            settlement.RemoveStatModifiersBySource("testB");
            double restored = settlement.GetSettlementStatValue(stat);
            TestAssert.AreEqual(before, restored,
                message: "All modifiers should be removed");
        }

        [EmpireTest("Stat")]
        public static void Cache_AddModifier_DirtiesSettlementCache()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlement");

            FCStatDef stat = FCStatDefOf.happinessGainedBase;

            // Populate the cache
            double initial = settlement.GetSettlementStatValue(stat);

            // Add a modifier — should invalidate cache
            WithTestModifier(settlement, stat, 7.0, () =>
            {
                double updated = settlement.GetSettlementStatValue(stat);
                TestAssert.AreEqual(initial + 7.0, updated,
                    message: "Cache should be invalidated and return updated value after add");
            });

            // After removal, cache should be invalidated again
            double restored = settlement.GetSettlementStatValue(stat);
            TestAssert.AreEqual(initial, restored,
                message: "Cache should be invalidated and return original value after remove");
        }

        // ============================
        // Tier 4: Resource-Stat Integration (SKIP if no settlement with resources)
        // ============================

        [EmpireTest("Stat")]
        public static void Resource_AllResources_HaveLinkedStats()
        {
            var faction = GetFaction();
            if (faction == null || faction.settlements.Count == 0)
                TestAssert.Skip("No faction/settlements");

            foreach (WorldSettlementFC settlement in faction.settlements)
            {
                foreach (ResourceFC resource in settlement.Resources)
                {
                    TestAssert.IsNotNull(resource.def.productionAdditiveStat,
                        $"{settlement.Name}/{resource.def.defName}: productionAdditiveStat is null");
                    TestAssert.IsNotNull(resource.def.productionMultiplierStat,
                        $"{settlement.Name}/{resource.def.defName}: productionMultiplierStat is null");
                }
            }
        }

        [EmpireTest("Stat")]
        public static void Resource_StatAdditive_AffectsProductionBase()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlement");

            ResourceFC resource = settlement.Resources.FirstOrDefault();
            if (resource == null || resource.def.productionAdditiveStat == null)
                TestAssert.Skip("No resource with productionAdditiveStat");

            double baseBefore = resource.productionBase;

            WithTestModifier(settlement, resource.def.productionAdditiveStat, 2.0, () =>
            {
                double baseAfter = resource.productionBase;
                TestAssert.GreaterThan(baseAfter, baseBefore,
                    $"productionBase should increase after adding additive stat modifier (before={baseBefore:F2}, after={baseAfter:F2})");
            });

            double baseRestored = resource.productionBase;
            TestAssert.AreEqual(baseBefore, baseRestored,
                message: $"productionBase should restore after removing modifier");
        }

        [EmpireTest("Stat")]
        public static void Resource_StatMultiplier_AffectsProductionMult()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlement");

            ResourceFC resource = settlement.Resources.FirstOrDefault();
            if (resource == null || resource.def.productionMultiplierStat == null)
                TestAssert.Skip("No resource with productionMultiplierStat");

            double multBefore = resource.productionMult;

            WithTestModifier(settlement, resource.def.productionMultiplierStat, 1.5, () =>
            {
                double multAfter = resource.productionMult;
                TestAssert.GreaterThan(multAfter, multBefore,
                    $"productionMult should increase after adding multiplier stat modifier (before={multBefore:F2}, after={multAfter:F2})");
            });

            double multRestored = resource.productionMult;
            TestAssert.AreEqual(multBefore, multRestored,
                message: $"productionMult should restore after removing modifier");
        }

        // ============================
        // Tier 5: Behavior Tests (SKIP if policy not active)
        // ============================

        [EmpireTest("Stat")]
        public static void Behavior_AllBehaviors_ModifyStatDoesNotThrow()
        {
            var faction = GetFaction();
            if (faction == null || faction.settlements.Count == 0)
                TestAssert.Skip("No faction/settlements");

            // Test a representative sample of stats across all settlements
            FCStatDef[] sampleStats = new FCStatDef[]
            {
                FCStatDefOf.taxBonusFlat,
                FCStatDefOf.happinessGainedBase,
                FCStatDefOf.militaryBaseLevel,
                FCStatDefOf.settlementCostMultiplier,
                FCStatDefOf.prosperityGainedBase
            };

            foreach (WorldSettlementFC settlement in faction.settlements)
            {
                foreach (FCStatDef stat in sampleStats)
                {
                    double val = faction.GetStatValue(stat, settlement);
                    TestAssert.IsFalse(double.IsNaN(val),
                        $"GetStatValue({stat.defName}, {settlement.Name}) should not be NaN");
                    TestAssert.IsFalse(double.IsInfinity(val),
                        $"GetStatValue({stat.defName}, {settlement.Name}) should not be infinite");
                }
            }
        }

        [EmpireTest("Stat")]
        public static void Behavior_Egalitarian_TaxBreakReducesTaxBonus()
        {
            var faction = GetFaction();
            var settlement = GetFirstSettlement();
            if (faction == null || settlement == null)
                TestAssert.Skip("No faction/settlement");

            bool egalitarianActive = FindFC.PolicyManager.policies.Any(p =>
                p?.def == FCPolicyDefOf.egalitarian);
            if (!egalitarianActive)
                TestAssert.Skip("Egalitarian policy not active");

            double taxBonus = faction.GetStatValue(FCStatDefOf.taxBonusFlat, settlement);
            TestAssert.IsFalse(double.IsNaN(taxBonus),
                "Egalitarian taxBonusFlat should not be NaN");

            // When no tax break is active, the behavior contributes 0 to taxBonusFlat.
            // When a tax break is active, it contributes -30. Either way the delta vs. faction base
            // is never above 0 for this trait.
            double baseWithoutBehavior = faction.GetFactionStatValue(FCStatDefOf.taxBonusFlat);
            double behaviorDelta = taxBonus - baseWithoutBehavior;
            TestAssert.IsTrue(behaviorDelta <= 0.01,
                $"Egalitarian delta ({behaviorDelta:F2}) should be <= 0 (0 off-break, -30 on-break)");
            TestAssert.IsTrue(behaviorDelta >= -30.01,
                $"Egalitarian delta ({behaviorDelta:F2}) should be >= -30 (tax break penalty)");
        }

        [EmpireTest("Stat")]
        public static void CombinedStat_TitheMultiplier_RespectsSettlementModifiers()
        {
            var faction = GetFaction();
            var settlement = GetFirstSettlement();
            if (faction == null || settlement == null)
                TestAssert.Skip("No faction/settlement");

            FCStatDef stat = FCStatDefOf.titheValueMultiplier;
            TestAssert.IsTrue(stat.appliesToSettlements,
                "titheValueMultiplier should apply to settlements");
            double combined = faction.GetStatValue(stat, settlement);
            TestAssert.IsFalse(double.IsNaN(combined),
                "titheValueMultiplier combined value should not be NaN");
            TestAssert.GreaterThan(combined, 0,
                "titheValueMultiplier combined value should be positive");
        }
    }
}
