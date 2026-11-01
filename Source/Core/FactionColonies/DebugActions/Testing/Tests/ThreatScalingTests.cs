using System;
using System.Linq;
using RimWorld;
using Verse;

namespace FactionColonies
{
    public static class ThreatScalingTests
    {
        // ============================
        // Test Double
        // ============================

        private class TestContributor : IThreatScalingContributor
        {
            public double Additive;
            public double Multiplicative = 1.0;
            public double GetAdditiveContribution(FactionFC f) => Additive;
            public double GetMultiplicativeContribution(FactionFC f) => Multiplicative;
        }

        private class ThrowingContributor : IThreatScalingContributor
        {
            public double GetAdditiveContribution(FactionFC f) => throw new InvalidOperationException("test");
            public double GetMultiplicativeContribution(FactionFC f) => throw new InvalidOperationException("test");
        }

        private static FactionFC GetFaction()
        {
            return FindFC.FactionComp;
        }

        // ============================
        // Tier 1: Registry (no game state needed)
        // ============================

        [EmpireTest("ThreatScaling")]
        public static void Registry_RegisterAndInvokeAdditive()
        {
            var c = new TestContributor { Additive = 0.5 };
            ThreatScalingRegistry.Register(c);
            try
            {
                double result = ThreatScalingRegistry.InvokeGetAdditiveContributions(null);
                TestAssert.AreEqual(0.5, result, message: "Additive contribution should be 0.5");
            }
            finally
            {
                ThreatScalingRegistry.Unregister(c);
            }
        }

        [EmpireTest("ThreatScaling")]
        public static void Registry_RegisterAndInvokeMultiplicative()
        {
            var c = new TestContributor { Multiplicative = 1.5 };
            ThreatScalingRegistry.Register(c);
            try
            {
                double result = ThreatScalingRegistry.InvokeGetMultiplierContributions(null);
                TestAssert.AreEqual(1.5, result, message: "Multiplicative contribution should be 1.5");
            }
            finally
            {
                ThreatScalingRegistry.Unregister(c);
            }
        }

        [EmpireTest("ThreatScaling")]
        public static void Registry_MultipleContributors_Additive()
        {
            var c1 = new TestContributor { Additive = 0.3 };
            var c2 = new TestContributor { Additive = 0.2 };
            ThreatScalingRegistry.Register(c1);
            ThreatScalingRegistry.Register(c2);
            try
            {
                double result = ThreatScalingRegistry.InvokeGetAdditiveContributions(null);
                TestAssert.AreEqual(0.5, result, message: "Sum of additive contributions");
            }
            finally
            {
                ThreatScalingRegistry.Unregister(c1);
                ThreatScalingRegistry.Unregister(c2);
            }
        }

        [EmpireTest("ThreatScaling")]
        public static void Registry_MultipleContributors_Multiplicative()
        {
            var c1 = new TestContributor { Multiplicative = 1.5 };
            var c2 = new TestContributor { Multiplicative = 2.0 };
            ThreatScalingRegistry.Register(c1);
            ThreatScalingRegistry.Register(c2);
            try
            {
                double result = ThreatScalingRegistry.InvokeGetMultiplierContributions(null);
                TestAssert.AreEqual(3.0, result, message: "Product of multiplicative contributions");
            }
            finally
            {
                ThreatScalingRegistry.Unregister(c1);
                ThreatScalingRegistry.Unregister(c2);
            }
        }

        [EmpireTest("ThreatScaling")]
        public static void Registry_Unregister_RemovesContributor()
        {
            var c = new TestContributor { Additive = 0.5, Multiplicative = 2.0 };
            ThreatScalingRegistry.Register(c);
            ThreatScalingRegistry.Unregister(c);

            double additive = ThreatScalingRegistry.InvokeGetAdditiveContributions(null);
            double multiplicative = ThreatScalingRegistry.InvokeGetMultiplierContributions(null);

            TestAssert.AreEqual(0.0, additive, message: "Additive should be 0 after unregister");
            TestAssert.AreEqual(1.0, multiplicative, message: "Multiplicative should be 1.0 after unregister");
        }

        [EmpireTest("ThreatScaling")]
        public static void Registry_DuplicateRegister_IgnoresDuplicate()
        {
            var c = new TestContributor { Additive = 0.5 };
            ThreatScalingRegistry.Register(c);
            ThreatScalingRegistry.Register(c); // duplicate
            try
            {
                double result = ThreatScalingRegistry.InvokeGetAdditiveContributions(null);
                TestAssert.AreEqual(0.5, result, message: "Duplicate should be ignored, total still 0.5");
            }
            finally
            {
                // Unregister twice: if dedup regresses (the condition under test), both copies must
                // be removed so a failing run can't leak a test double for the rest of the session.
                ThreatScalingRegistry.Unregister(c);
                ThreatScalingRegistry.Unregister(c);
            }
        }

        [EmpireTest("ThreatScaling")]
        public static void Registry_ExceptionInContributor_DoesNotCrash()
        {
            var bad = new ThrowingContributor();
            ThreatScalingRegistry.Register(bad);
            try
            {
                // Should not throw — exceptions are caught internally
                double additive = ThreatScalingRegistry.InvokeGetAdditiveContributions(null);
                double multiplicative = ThreatScalingRegistry.InvokeGetMultiplierContributions(null);

                TestAssert.AreEqual(0.0, additive, message: "Additive should return identity on exception");
                TestAssert.AreEqual(1.0, multiplicative, message: "Multiplicative should return identity on exception");
            }
            finally
            {
                ThreatScalingRegistry.Unregister(bad);
            }
        }

        // ============================
        // Tier 2: FCStatDef Validation
        // ============================

        [EmpireTest("ThreatScaling")]
        public static void Def_ThreatScalingBase_Exists()
        {
            TestAssert.IsNotNull(FCStatDefOf.threatScalingBase,
                "FCStatDefOf.threatScalingBase should be resolved");
        }

        [EmpireTest("ThreatScaling")]
        public static void Def_ThreatScalingBase_IsAdditive()
        {
            TestAssert.AreEqual((object)FCStatAggregation.Additive,
                (object)FCStatDefOf.threatScalingBase.aggregation,
                "threatScalingBase should be Additive");
        }

        [EmpireTest("ThreatScaling")]
        public static void Def_ThreatScalingBase_IsFactionOnly()
        {
            TestAssert.IsFalse(FCStatDefOf.threatScalingBase.appliesToSettlements,
                "threatScalingBase should not apply to settlements");
        }

        [EmpireTest("ThreatScaling")]
        public static void Def_ThreatScalingMultiplier_Exists()
        {
            TestAssert.IsNotNull(FCStatDefOf.threatScalingMultiplier,
                "FCStatDefOf.threatScalingMultiplier should be resolved");
        }

        [EmpireTest("ThreatScaling")]
        public static void Def_ThreatScalingMultiplier_IsMultiplicative()
        {
            TestAssert.AreEqual((object)FCStatAggregation.Multiplicative,
                (object)FCStatDefOf.threatScalingMultiplier.aggregation,
                "threatScalingMultiplier should be Multiplicative");
        }

        [EmpireTest("ThreatScaling")]
        public static void Def_ThreatScalingMultiplier_IsFactionOnly()
        {
            TestAssert.IsFalse(FCStatDefOf.threatScalingMultiplier.appliesToSettlements,
                "threatScalingMultiplier should not apply to settlements");
        }

        // ============================
        // Tier 3: ETL Computation (requires active faction)
        // DORMANT — ComputeEmpireThreatLevel is no longer in the live raid path (removed in the
        // 2026-06-14 threat-scaling simplification); retained for a future threat-scaling submod.
        // The live raid path is covered by Tier 7 / Tier 8 below.
        // ============================

        [EmpireTest("ThreatScaling")]
        public static void ETL_WithSettlements_IsAtLeastOne()
        {
            var faction = GetFaction();
            if (faction == null || faction.settlements.Count == 0)
                TestAssert.Skip("No faction/settlements");

            double etl = ThreatScalingUtil.ComputeEmpireThreatLevel(faction);
            TestAssert.IsTrue(etl >= 1.0,
                $"ETL should be >= 1.0, got {etl}");
        }

        [EmpireTest("ThreatScaling")]
        public static void ETL_WithSettlements_IsCappedByMaxThreatMultiplier()
        {
            var faction = GetFaction();
            if (faction == null || faction.settlements.Count == 0)
                TestAssert.Skip("No faction/settlements");

            double etl = ThreatScalingUtil.ComputeEmpireThreatLevel(faction);
            TestAssert.LessThanOrEqual(etl, FCSettings.maxThreatMultiplier,
                $"ETL should be <= maxThreatMultiplier ({FCSettings.maxThreatMultiplier}), got {etl}");
        }

        [EmpireTest("ThreatScaling")]
        public static void ETL_IsFiniteNumber()
        {
            var faction = GetFaction();
            if (faction == null || faction.settlements.Count == 0)
                TestAssert.Skip("No faction/settlements");

            double etl = ThreatScalingUtil.ComputeEmpireThreatLevel(faction);
            TestAssert.IsFalse(double.IsNaN(etl), "ETL should not be NaN");
            TestAssert.IsFalse(double.IsInfinity(etl), "ETL should not be infinite");
        }

        [EmpireTest("ThreatScaling")]
        public static void ETL_LoweredCap_ClampsProperly()
        {
            var faction = GetFaction();
            if (faction == null || faction.settlements.Count == 0)
                TestAssert.Skip("No faction/settlements");

            float original = FCSettings.maxThreatMultiplier;
            try
            {
                FCSettings.maxThreatMultiplier = 1.0f;
                double etl = ThreatScalingUtil.ComputeEmpireThreatLevel(faction);
                TestAssert.AreEqual(1.0, etl,
                    message: $"ETL should be clamped to 1.0 when cap is 1.0, got {etl}");
            }
            finally
            {
                FCSettings.maxThreatMultiplier = original;
            }
        }

        [EmpireTest("ThreatScaling")]
        public static void ETL_RegistryAdditive_IncreasesETL()
        {
            var faction = GetFaction();
            if (faction == null || faction.settlements.Count == 0)
                TestAssert.Skip("No faction/settlements");

            double baseline = ThreatScalingUtil.ComputeEmpireThreatLevel(faction);
            var c = new TestContributor { Additive = 0.5 };
            ThreatScalingRegistry.Register(c);
            try
            {
                double boosted = ThreatScalingUtil.ComputeEmpireThreatLevel(faction);
                TestAssert.IsTrue(boosted >= baseline,
                    $"ETL with additive contributor ({boosted}) should be >= baseline ({baseline})");
            }
            finally
            {
                ThreatScalingRegistry.Unregister(c);
            }
        }

        [EmpireTest("ThreatScaling")]
        public static void ETL_RegistryMultiplicative_IncreasesETL()
        {
            var faction = GetFaction();
            if (faction == null || faction.settlements.Count == 0)
                TestAssert.Skip("No faction/settlements");

            double baseline = ThreatScalingUtil.ComputeEmpireThreatLevel(faction);
            var c = new TestContributor { Multiplicative = 1.5 };
            ThreatScalingRegistry.Register(c);
            try
            {
                double boosted = ThreatScalingUtil.ComputeEmpireThreatLevel(faction);
                TestAssert.IsTrue(boosted >= baseline,
                    $"ETL with multiplicative contributor ({boosted}) should be >= baseline ({baseline})");
            }
            finally
            {
                ThreatScalingRegistry.Unregister(c);
            }
        }

        // ============================
        // Tier 4: Handicap Cap (requires active faction)
        // DORMANT — ComputeHandicapCap is no longer in the live raid path (incoming raids use the
        // early-game raid cap, covered in Tier 8); retained for a future threat-scaling submod.
        // ============================

        [EmpireTest("ThreatScaling")]
        public static void HandicapCap_IsAtLeastTwo()
        {
            var faction = GetFaction();
            if (faction == null || faction.settlements.Count == 0)
                TestAssert.Skip("No faction/settlements");

            double cap = ThreatScalingUtil.ComputeHandicapCap(faction);
            TestAssert.IsTrue(cap >= 2.0,
                $"Handicap cap should be >= 2.0, got {cap}");
        }

        [EmpireTest("ThreatScaling")]
        public static void HandicapCap_IsFiniteNumber()
        {
            var faction = GetFaction();
            if (faction == null || faction.settlements.Count == 0)
                TestAssert.Skip("No faction/settlements");

            double cap = ThreatScalingUtil.ComputeHandicapCap(faction);
            TestAssert.IsFalse(double.IsNaN(cap), "Handicap cap should not be NaN");
            TestAssert.IsFalse(double.IsInfinity(cap), "Handicap cap should not be infinite");
        }

        // ============================
        // Tier 5: Frequency Scaling (requires active faction)
        // ============================

        [EmpireTest("ThreatScaling")]
        public static void AttackInterval_RespectsMinBound()
        {
            var faction = GetFaction();
            if (faction == null || faction.settlements.Count == 0)
                TestAssert.Skip("No faction/settlements");

            int interval = ThreatScalingUtil.ComputeScaledAttackInterval(faction);
            TestAssert.IsTrue(interval >= FCSettings.minMaxDaysTillMilitaryAction.min,
                $"Interval ({interval}) should be >= min ({FCSettings.minMaxDaysTillMilitaryAction.min})");
        }

        [EmpireTest("ThreatScaling")]
        public static void AttackInterval_RespectsMaxBound()
        {
            var faction = GetFaction();
            if (faction == null || faction.settlements.Count == 0)
                TestAssert.Skip("No faction/settlements");

            int interval = ThreatScalingUtil.ComputeScaledAttackInterval(faction);
            TestAssert.IsTrue(interval <= FCSettings.minMaxDaysTillMilitaryAction.max,
                $"Interval ({interval}) should be <= max ({FCSettings.minMaxDaysTillMilitaryAction.max})");
        }

        [EmpireTest("ThreatScaling")]
        public static void AttackInterval_RunMultiple_AllWithinBounds()
        {
            var faction = GetFaction();
            if (faction == null || faction.settlements.Count == 0)
                TestAssert.Skip("No faction/settlements");

            int min = FCSettings.minMaxDaysTillMilitaryAction.min;
            int max = FCSettings.minMaxDaysTillMilitaryAction.max;

            for (int i = 0; i < 20; i++)
            {
                int interval = ThreatScalingUtil.ComputeScaledAttackInterval(faction);
                TestAssert.IsTrue(interval >= min,
                    $"Iteration {i}: interval ({interval}) below min ({min})");
                TestAssert.IsTrue(interval <= max,
                    $"Iteration {i}: interval ({interval}) above max ({max})");
            }
        }

        // ============================
        // Tier 6: Adaptation (requires storyteller)
        // ============================

        [EmpireTest("ThreatScaling")]
        public static void Adaptation_ThreatFactor_IsFinite()
        {
            var faction = GetFaction();
            if (faction == null)
                TestAssert.Skip("No faction");

            double factor = faction.threatAdaptation.ThreatFactor;
            TestAssert.IsFalse(double.IsNaN(factor), "ThreatFactor should not be NaN");
            TestAssert.IsFalse(double.IsInfinity(factor), "ThreatFactor should not be infinite");
        }

        [EmpireTest("ThreatScaling")]
        public static void Adaptation_ThreatFactor_IsPositive()
        {
            var faction = GetFaction();
            if (faction == null)
                TestAssert.Skip("No faction");

            double factor = faction.threatAdaptation.ThreatFactor;
            TestAssert.GreaterThan(factor, 0.0,
                $"ThreatFactor should be > 0, got {factor}");
        }

        [EmpireTest("ThreatScaling")]
        public static void Adaptation_NotNull_OnFaction()
        {
            var faction = GetFaction();
            if (faction == null)
                TestAssert.Skip("No faction");

            TestAssert.IsNotNull(faction.threatAdaptation,
                "threatAdaptation field should never be null");
        }

        // ============================
        // Tier 7: Faction Selection Weight (live raid path; no game state)
        // Pure coverage for ThreatScalingUtil.ComputeFactionSelectionWeight, the closeness-based
        // weighting that drives live enemy-faction selection (replaced ETL on 2026-06-14).
        // ============================

        [EmpireTest("ThreatScaling")]
        public static void SelectionWeight_EvenLevel_IsFull()
        {
            TestAssert.AreEqual(1.0, ThreatScalingUtil.ComputeFactionSelectionWeight(5, 5),
                message: "Even level should weight 1.0");
        }

        [EmpireTest("ThreatScaling")]
        public static void SelectionWeight_OneApart_IsThreeQuarters()
        {
            TestAssert.AreEqual(0.75, ThreatScalingUtil.ComputeFactionSelectionWeight(6, 5),
                message: "+/-1 level should weight 0.75");
        }

        [EmpireTest("ThreatScaling")]
        public static void SelectionWeight_TwoApart_IsHalf()
        {
            TestAssert.AreEqual(0.5, ThreatScalingUtil.ComputeFactionSelectionWeight(3, 5),
                message: "+/-2 level should weight 0.5");
        }

        [EmpireTest("ThreatScaling")]
        public static void SelectionWeight_ThreeApart_IsQuarter()
        {
            TestAssert.AreEqual(0.25, ThreatScalingUtil.ComputeFactionSelectionWeight(2, 5),
                message: "+/-3 level should weight 0.25");
        }

        [EmpireTest("ThreatScaling")]
        public static void SelectionWeight_FourApart_FlooredAt5Percent()
        {
            // Raw 1 - 4*0.25 = 0.0, floored to 0.05
            TestAssert.AreEqual(0.05, ThreatScalingUtil.ComputeFactionSelectionWeight(9, 5),
                message: "+/-4 level should floor to 0.05");
        }

        [EmpireTest("ThreatScaling")]
        public static void SelectionWeight_FarApart_FlooredAt5Percent()
        {
            // Raw 1 - 10*0.25 = -1.5, floored to 0.05 (distant tiers rare but never excluded)
            TestAssert.AreEqual(0.05, ThreatScalingUtil.ComputeFactionSelectionWeight(15, 5),
                message: "Distant levels should never drop below the 0.05 floor");
        }

        [EmpireTest("ThreatScaling")]
        public static void SelectionWeight_IsSymmetric()
        {
            TestAssert.AreEqual(
                ThreatScalingUtil.ComputeFactionSelectionWeight(3, 7),
                ThreatScalingUtil.ComputeFactionSelectionWeight(7, 3),
                message: "Weight should depend only on the absolute level distance");
        }

        // ============================
        // Tier 8: Live raid path (requires active faction)
        // ComputeEarlyGameRaidCap + PickWeightedEnemyFaction — the live incoming-raid cap and
        // enemy-faction selection that replaced the dormant ETL/handicap tiers.
        // ============================

        [EmpireTest("ThreatScaling")]
        public static void EarlyGameRaidCap_RespectsFloor()
        {
            var faction = GetFaction();
            if (faction == null)
                TestAssert.Skip("No faction");

            double cap = ThreatScalingUtil.ComputeEarlyGameRaidCap(faction);
            TestAssert.IsTrue(cap >= 2.0,
                $"Early-game raid cap should be >= 2.0 floor, got {cap}");
        }

        [EmpireTest("ThreatScaling")]
        public static void EarlyGameRaidCap_IsFiniteNumber()
        {
            var faction = GetFaction();
            if (faction == null)
                TestAssert.Skip("No faction");

            double cap = ThreatScalingUtil.ComputeEarlyGameRaidCap(faction);
            TestAssert.IsFalse(double.IsNaN(cap), "Early-game raid cap should not be NaN");
            TestAssert.IsFalse(double.IsInfinity(cap), "Early-game raid cap should not be infinite");
        }

        [EmpireTest("ThreatScaling")]
        public static void PickWeightedEnemyFaction_DoesNotThrow()
        {
            var faction = GetFaction();
            if (faction == null)
                TestAssert.Skip("No faction");

            TestAssert.DoesNotThrow(() => ThreatScalingUtil.PickWeightedEnemyFaction(faction));
        }

        [EmpireTest("ThreatScaling")]
        public static void PickWeightedEnemyFaction_ReturnsHostileOrNull()
        {
            var faction = GetFaction();
            if (faction == null)
                TestAssert.Skip("No faction");

            Faction picked = ThreatScalingUtil.PickWeightedEnemyFaction(faction);
            bool anyEligible = Find.FactionManager.AllFactionsVisible
                .Any(f => f.HostileTo(Faction.OfPlayer) && !f.defeated && !f.Hidden);

            if (picked == null)
            {
                TestAssert.IsFalse(anyEligible,
                    "Returned null but an eligible hostile faction exists");
                return;
            }

            TestAssert.IsTrue(picked.HostileTo(Faction.OfPlayer),
                $"Picked faction {picked.Name} should be hostile to the player");
            TestAssert.IsFalse(picked.defeated, $"Picked faction {picked.Name} should not be defeated");
            TestAssert.IsFalse(picked.Hidden, $"Picked faction {picked.Name} should not be hidden");
        }
    }
}
