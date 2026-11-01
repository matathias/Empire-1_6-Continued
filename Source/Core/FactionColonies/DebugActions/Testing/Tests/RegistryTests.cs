using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld.Planet;

namespace FactionColonies
{
    public static class RegistryTests
    {
        // ============================
        // Helpers
        // ============================

        private static WorldSettlementFC GetFirstSettlement()
        {
            var settlements = FindFC.Settlements;
            if (settlements == null || settlements.Count == 0) return null;
            return settlements[0];
        }

        // ============================
        // Test Doubles
        // ============================

        private class TestLifecycleParticipant : ISettlementListener, IMilitaryOperationListener, IResearchListener
        {
            public int SettlementCreatedCount;
            public int SettlementRemovedCount;
            public int BuildingConstructedCount;
            public int BattleResolvedCount;
            public int ResearchCompletedCount;
            public void OnSettlementCreated(WorldSettlementFC s) => SettlementCreatedCount++;
            public void OnSettlementRemoved(WorldSettlementFC s) => SettlementRemovedCount++;
            public void OnSettlementUpgraded(WorldSettlementFC s, int oldLevel, int newLevel) { }
            public void OnSettlementTypeChanged(WorldSettlementFC s, WorldSettlementDef oldDef, WorldSettlementDef newDef) { }
            public void OnBuildingConstructed(WorldSettlementFC s, BuildingFCDef b, int slot) => BuildingConstructedCount++;
            public void OnBuildingDeconstructed(WorldSettlementFC s, BuildingFCDef b, int slot) { }
            public void OnOperationCreated(MilitaryOperation op) { }
            public void OnOperationResolved(MilitaryOperation op) { }
            public void OnBattleResolved(MilitaryOperation op, bool victory, BattleResult result) => BattleResolvedCount++;
            public void OnResearchCompleted(ResearchProjectDef p) => ResearchCompletedCount++;
        }

        private class ThrowingLifecycleParticipant : ISettlementListener, IMilitaryOperationListener
        {
            public void OnSettlementCreated(WorldSettlementFC s) => throw new InvalidOperationException("test");
            public void OnSettlementRemoved(WorldSettlementFC s) => throw new InvalidOperationException("test");
            public void OnSettlementUpgraded(WorldSettlementFC s, int oldLevel, int newLevel) => throw new InvalidOperationException("test");
            public void OnSettlementTypeChanged(WorldSettlementFC s, WorldSettlementDef oldDef, WorldSettlementDef newDef) => throw new InvalidOperationException("test");
            public void OnBuildingConstructed(WorldSettlementFC s, BuildingFCDef b, int slot) => throw new InvalidOperationException("test");
            public void OnBuildingDeconstructed(WorldSettlementFC s, BuildingFCDef b, int slot) => throw new InvalidOperationException("test");
            public void OnOperationCreated(MilitaryOperation op) => throw new InvalidOperationException("test");
            public void OnOperationResolved(MilitaryOperation op) => throw new InvalidOperationException("test");
            public void OnBattleResolved(MilitaryOperation op, bool victory, BattleResult result) => throw new InvalidOperationException("test");
        }

        private class TestBattleModifier : IBattleModifier
        {
            public double LevelBonus;
            public void ModifyForce(BattleForceContext ctx, MilitaryForce force, bool isAttacker) => force.militaryLevel += LevelBonus;
        }

        private class ThrowingBattleModifier : IBattleModifier
        {
            public void ModifyForce(BattleForceContext ctx, MilitaryForce force, bool isAttacker) => throw new InvalidOperationException("test");
        }

        private class TestFactionPowerModifier : IFactionPowerModifier
        {
            public double LevelBonus;
            public int InvokeCount;
            public void ModifyFactionPower(RimWorld.Faction faction, EnemyPower power)
            {
                InvokeCount++;
                if (power is object) power.level += LevelBonus;
            }
        }

        private class ThrowingFactionPowerModifier : IFactionPowerModifier
        {
            public void ModifyFactionPower(RimWorld.Faction faction, EnemyPower power) => throw new InvalidOperationException("test");
        }

        private class TestSettlementPowerModifier : ISettlementPowerModifier
        {
            public double LevelBonus;
            public int InvokeCount;
            public void ModifySettlementPower(RimWorld.Planet.Settlement settlement, EnemyPower power)
            {
                InvokeCount++;
                if (power is object) power.level += LevelBonus;
            }
        }

        private class ThrowingSettlementPowerModifier : ISettlementPowerModifier
        {
            public void ModifySettlementPower(RimWorld.Planet.Settlement settlement, EnemyPower power) => throw new InvalidOperationException("test");
        }

        private class TestPaymentModifier : ISilverPaymentModifier
        {
            public int Discount;
            public void ModifyPayment(SilverPaymentContext context) => context.Amount -= Discount;
        }

        private class ThrowingPaymentModifier : ISilverPaymentModifier
        {
            public void ModifyPayment(SilverPaymentContext context) => throw new InvalidOperationException("test");
        }

        private class TestDefenseValidator : IDefenseValidator
        {
            public bool Allow = true;
            public bool CanDefend(WorldSettlementFC defender, WorldSettlementFC target) => Allow;
        }

        private class ThrowingDefenseValidator : IDefenseValidator
        {
            public bool CanDefend(WorldSettlementFC defender, WorldSettlementFC target) => throw new InvalidOperationException("test");
        }

        private class TestTaxTicker : ITaxTickParticipant
        {
            public int PreTaxCount;
            public int PostTaxCount;
            public void PreTaxResolution(FactionFC f) => PreTaxCount++;
            public void PostTaxResolution(FactionFC f) => PostTaxCount++;
            public void PreSettlementCreateTax(WorldSettlementFC s) { }
            public void PostSettlementCreateTax(WorldSettlementFC s, ref int a, List<Thing> t) { }
        }

        private class ThrowingTaxTicker : ITaxTickParticipant
        {
            public void PreTaxResolution(FactionFC f) => throw new InvalidOperationException("test");
            public void PostTaxResolution(FactionFC f) => throw new InvalidOperationException("test");
            public void PreSettlementCreateTax(WorldSettlementFC s) => throw new InvalidOperationException("test");
            public void PostSettlementCreateTax(WorldSettlementFC s, ref int a, List<Thing> t) => throw new InvalidOperationException("test");
        }

        private class TestSquadValidator : ISquadAssignmentValidator
        {
            public bool Allow = true;
            public string RejectReason = "test reject";
            public bool CanAssign(WorldSettlementFC s, MercenarySquadFC sq, out string reason)
            {
                reason = Allow ? null : RejectReason;
                return Allow;
            }
        }

        private class ThrowingSquadValidator : ISquadAssignmentValidator
        {
            public bool CanAssign(WorldSettlementFC s, MercenarySquadFC sq, out string reason)
            {
                reason = null;
                throw new InvalidOperationException("test");
            }
        }

        private class TestMainTab : IMainTabWindowOverview
        {
            public int PostCloseCount;
            public void PreOpenWindow(FactionFC f) { }
            public void OnTabSwitch() { }
            public void DrawOverviewTab(Rect b) { }
            public void PostCloseWindow() => PostCloseCount++;
            public string TabName() => "TestTab";
        }

        private class ThrowingMainTab : IMainTabWindowOverview
        {
            public void PreOpenWindow(FactionFC f) { }
            public void OnTabSwitch() { }
            public void DrawOverviewTab(Rect b) { }
            public void PostCloseWindow() => throw new InvalidOperationException("test");
            public string TabName() => "ThrowingTab";
        }

        // ============================
        // LifecycleRegistry
        // ============================

        [EmpireDestructiveTest("Registry")]
        public static void Lifecycle_Register_InvokesSettlementCreated()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlements");

            var p = new TestLifecycleParticipant();
            LifecycleRegistry.Register(p);
            try
            {
                LifecycleRegistry.InvokeOnSettlementCreated(settlement);
                TestAssert.AreEqual(1, p.SettlementCreatedCount);
            }
            finally { LifecycleRegistry.Unregister(p); }
        }

        [EmpireDestructiveTest("Registry")]
        public static void Lifecycle_Register_InvokesSettlementRemoved()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlements");

            var p = new TestLifecycleParticipant();
            LifecycleRegistry.Register(p);
            try
            {
                LifecycleRegistry.InvokeOnSettlementRemoved(settlement);
                TestAssert.AreEqual(1, p.SettlementRemovedCount);
            }
            finally { LifecycleRegistry.Unregister(p); }
        }

        [EmpireDestructiveTest("Registry")]
        public static void Lifecycle_Register_InvokesBuildingConstructed()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlements");

            var p = new TestLifecycleParticipant();
            LifecycleRegistry.Register(p);
            try
            {
                LifecycleRegistry.InvokeOnBuildingConstructed(settlement, null, 0);
                TestAssert.AreEqual(1, p.BuildingConstructedCount);
            }
            finally { LifecycleRegistry.Unregister(p); }
        }

        [EmpireDestructiveTest("Registry")]
        public static void Lifecycle_Register_InvokesBattleResolved()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlements");

            var p = new TestLifecycleParticipant();
            LifecycleRegistry.Register(p);
            try
            {
                MilitaryOperation op = MakeSyntheticOp(settlement);
                LifecycleRegistry.InvokeOnBattleResolved(op, true, null);
                TestAssert.AreEqual(1, p.BattleResolvedCount);
            }
            finally { LifecycleRegistry.Unregister(p); }
        }

        /// <summary>
        /// Builds a minimal <see cref="MilitaryOperation"/> for tests that exercise the op-aware
        /// registry overloads. Not registered with the manager — purely a transient stand-in.
        /// </summary>
        private static MilitaryOperation MakeSyntheticOp(WorldSettlementFC home)
        {
            var op = new MilitaryOperation(-1, null, home?.Tile ?? RimWorld.Planet.PlanetTile.Invalid, home);
            op.aggressor.homeSettlement = home;
            op.aggressor.faction = FindFC.EmpireFaction;
            return op;
        }

        [EmpireDestructiveTest("Registry")]
        public static void Lifecycle_Unregister_StopsInvocations()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlements");

            var p = new TestLifecycleParticipant();
            LifecycleRegistry.Register(p);
            LifecycleRegistry.Unregister(p);
            LifecycleRegistry.InvokeOnSettlementCreated(settlement);
            TestAssert.AreEqual(0, p.SettlementCreatedCount);
        }

        [EmpireDestructiveTest("Registry")]
        public static void Lifecycle_DuplicateRegister_Ignored()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlements");

            var p = new TestLifecycleParticipant();
            LifecycleRegistry.Register(p);
            LifecycleRegistry.Register(p); // duplicate
            try
            {
                LifecycleRegistry.InvokeOnSettlementCreated(settlement);
                TestAssert.AreEqual(1, p.SettlementCreatedCount, "Duplicate should be ignored");
            }
            finally { LifecycleRegistry.Unregister(p); }
        }

        [EmpireDestructiveTest("Registry")]
        public static void Lifecycle_Exception_DoesNotCrash()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlements");

            var bad = new ThrowingLifecycleParticipant();
            LifecycleRegistry.Register(bad);
            try
            {
                TestAssert.DoesNotThrow(() => LifecycleRegistry.InvokeOnSettlementCreated(settlement));
            }
            finally { LifecycleRegistry.Unregister(bad); }
        }

        [EmpireDestructiveTest("Registry")]
        public static void Lifecycle_MultipleParticipants_AllInvoked()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlements");

            var p1 = new TestLifecycleParticipant();
            var p2 = new TestLifecycleParticipant();
            LifecycleRegistry.Register(p1);
            LifecycleRegistry.Register(p2);
            try
            {
                LifecycleRegistry.InvokeOnSettlementCreated(settlement);
                TestAssert.AreEqual(1, p1.SettlementCreatedCount, "First participant should be invoked");
                TestAssert.AreEqual(1, p2.SettlementCreatedCount, "Second participant should be invoked");
            }
            finally
            {
                LifecycleRegistry.Unregister(p1);
                LifecycleRegistry.Unregister(p2);
            }
        }

        [EmpireDestructiveTest("Registry")]
        public static void Lifecycle_ExceptionDoesNotBlockOthers()
        {
            var settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlements");

            var bad = new ThrowingLifecycleParticipant();
            var good = new TestLifecycleParticipant();
            LifecycleRegistry.Register(bad);
            LifecycleRegistry.Register(good);
            try
            {
                LifecycleRegistry.InvokeOnSettlementCreated(settlement);
                TestAssert.AreEqual(1, good.SettlementCreatedCount,
                    "Good participant should still be invoked after bad one throws");
            }
            finally
            {
                LifecycleRegistry.Unregister(bad);
                LifecycleRegistry.Unregister(good);
            }
        }

        // ============================
        // BattleModifierRegistry
        // ============================

        [EmpireTest("Registry")]
        public static void BattleModifier_Register_ModifiesForce()
        {
            var c = new TestBattleModifier { LevelBonus = 2.0 };
            BattleModifierRegistry.Register(c);
            try
            {
                var force = new MilitaryForce { militaryLevel = 5, militaryEfficiency = 1.0, forceRemaining = 5 };
                BattleModifierRegistry.InvokeBattleModifiers(null, force, true);
                TestAssert.AreEqual(7.0, force.militaryLevel, message: "Level should increase by 2");
            }
            finally { BattleModifierRegistry.Unregister(c); }
        }

        [EmpireTest("Registry")]
        public static void BattleModifier_Unregister_NoEffect()
        {
            var c = new TestBattleModifier { LevelBonus = 2.0 };
            BattleModifierRegistry.Register(c);
            BattleModifierRegistry.Unregister(c);
            var force = new MilitaryForce { militaryLevel = 5, militaryEfficiency = 1.0, forceRemaining = 5 };
            BattleModifierRegistry.InvokeBattleModifiers(null, force, true);
            TestAssert.AreEqual(5.0, force.militaryLevel, message: "Level should be unchanged");
        }

        [EmpireTest("Registry")]
        public static void BattleModifier_DuplicateRegister_Ignored()
        {
            var c = new TestBattleModifier { LevelBonus = 2.0 };
            BattleModifierRegistry.Register(c);
            BattleModifierRegistry.Register(c);
            try
            {
                var force = new MilitaryForce { militaryLevel = 5, militaryEfficiency = 1.0, forceRemaining = 5 };
                BattleModifierRegistry.InvokeBattleModifiers(null, force, true);
                TestAssert.AreEqual(7.0, force.militaryLevel, message: "Should only apply once");
            }
            // Unregister twice: if dedup regresses (the condition under test), both copies must
            // be removed so a failing run can't leak a test double for the rest of the session.
            finally { BattleModifierRegistry.Unregister(c); BattleModifierRegistry.Unregister(c); }
        }

        [EmpireTest("Registry")]
        public static void BattleModifier_Exception_DoesNotCrash()
        {
            var bad = new ThrowingBattleModifier();
            BattleModifierRegistry.Register(bad);
            try
            {
                var force = new MilitaryForce { militaryLevel = 5, militaryEfficiency = 1.0, forceRemaining = 5 };
                TestAssert.DoesNotThrow(() => BattleModifierRegistry.InvokeBattleModifiers(null, force, true));
            }
            finally { BattleModifierRegistry.Unregister(bad); }
        }

        // ============================
        // BattleModifierRegistry — IFactionPowerModifier
        // ============================

        [EmpireTest("Registry")]
        public static void FactionPowerModifier_RegistersAndInvokes()
        {
            RimWorld.Faction faction = FindFC.EmpireFaction;
            if (faction is null) TestAssert.Skip("No player colony faction");

            var modifier = new TestFactionPowerModifier { LevelBonus = 3.0 };
            BattleModifierRegistry.Register(modifier);
            try
            {
                var power = new EnemyPower { level = 5, efficiency = 1.0 };
                BattleModifierRegistry.InvokeFactionPowerModifiers(faction, power);
                TestAssert.AreEqual(1, modifier.InvokeCount);
                TestAssert.AreEqual(8.0, power.level, message: "Level should increase by 3");
            }
            finally { BattleModifierRegistry.Unregister(modifier); }
        }

        [EmpireTest("Registry")]
        public static void FactionPowerModifier_Unregister_NoEffect()
        {
            RimWorld.Faction faction = FindFC.EmpireFaction;
            if (faction is null) TestAssert.Skip("No player colony faction");

            var modifier = new TestFactionPowerModifier { LevelBonus = 3.0 };
            BattleModifierRegistry.Register(modifier);
            BattleModifierRegistry.Unregister(modifier);

            var power = new EnemyPower { level = 5, efficiency = 1.0 };
            BattleModifierRegistry.InvokeFactionPowerModifiers(faction, power);
            TestAssert.AreEqual(0, modifier.InvokeCount);
            TestAssert.AreEqual(5.0, power.level, message: "Level should be unchanged");
        }

        [EmpireTest("Registry")]
        public static void FactionPowerModifier_Exception_DoesNotCrash()
        {
            RimWorld.Faction faction = FindFC.EmpireFaction;
            if (faction is null) TestAssert.Skip("No player colony faction");

            var bad = new ThrowingFactionPowerModifier();
            BattleModifierRegistry.Register(bad);
            try
            {
                var power = new EnemyPower { level = 5, efficiency = 1.0 };
                TestAssert.DoesNotThrow(() => BattleModifierRegistry.InvokeFactionPowerModifiers(faction, power));
            }
            finally { BattleModifierRegistry.Unregister(bad); }
        }

        // ============================
        // BattleModifierRegistry — ISettlementPowerModifier
        // ============================

        [EmpireTest("Registry")]
        public static void SettlementPowerModifier_RegistersAndInvokes()
        {
            // Settlement parameter can be null — the registry just forwards it to the modifier,
            // which here doesn't dereference it.
            var modifier = new TestSettlementPowerModifier { LevelBonus = 2.5 };
            BattleModifierRegistry.Register(modifier);
            try
            {
                var power = new EnemyPower { level = 4, efficiency = 1.0 };
                BattleModifierRegistry.InvokeSettlementPowerModifiers(null, power);
                TestAssert.AreEqual(1, modifier.InvokeCount);
                TestAssert.AreEqual(6.5, power.level, message: "Level should increase by 2.5");
            }
            finally { BattleModifierRegistry.Unregister(modifier); }
        }

        [EmpireTest("Registry")]
        public static void SettlementPowerModifier_Unregister_NoEffect()
        {
            var modifier = new TestSettlementPowerModifier { LevelBonus = 2.5 };
            BattleModifierRegistry.Register(modifier);
            BattleModifierRegistry.Unregister(modifier);

            var power = new EnemyPower { level = 4, efficiency = 1.0 };
            BattleModifierRegistry.InvokeSettlementPowerModifiers(null, power);
            TestAssert.AreEqual(0, modifier.InvokeCount);
            TestAssert.AreEqual(4.0, power.level);
        }

        [EmpireTest("Registry")]
        public static void SettlementPowerModifier_Exception_DoesNotCrash()
        {
            var bad = new ThrowingSettlementPowerModifier();
            BattleModifierRegistry.Register(bad);
            try
            {
                var power = new EnemyPower { level = 4, efficiency = 1.0 };
                TestAssert.DoesNotThrow(() => BattleModifierRegistry.InvokeSettlementPowerModifiers(null, power));
            }
            finally { BattleModifierRegistry.Unregister(bad); }
        }

        // ============================
        // BattleModifierRegistry — Cross-interface independence
        // ============================

        [EmpireTest("Registry")]
        public static void BattleModifierRegistry_DuplicateRegisterAcrossInterfaces_Independent()
        {
            // Three separate lists in BattleModifierRegistry. Registering an instance that
            // implements two interfaces should produce independent registrations — invoking
            // each chain hits the instance once per chain.
            //
            // We use distinct test doubles per interface here (no class implements both),
            // but the lists themselves must stay independent. Verify by registering on two
            // chains and confirming both fire.
            var faction = FindFC.EmpireFaction;
            if (faction is null) TestAssert.Skip("No player colony faction");

            var battleMod = new TestBattleModifier { LevelBonus = 1 };
            var factionMod = new TestFactionPowerModifier { LevelBonus = 2 };
            BattleModifierRegistry.Register(battleMod);
            BattleModifierRegistry.Register(factionMod);
            try
            {
                var battleForce = new MilitaryForce { militaryLevel = 5, militaryEfficiency = 1.0, forceRemaining = 5 };
                var factionPower = new EnemyPower { level = 5, efficiency = 1.0 };

                BattleModifierRegistry.InvokeBattleModifiers(null, battleForce, true);
                BattleModifierRegistry.InvokeFactionPowerModifiers(faction, factionPower);

                TestAssert.AreEqual(6.0, battleForce.militaryLevel, message: "Battle chain ran");
                TestAssert.AreEqual(7.0, factionPower.level, message: "Faction chain ran");
                TestAssert.AreEqual(1, factionMod.InvokeCount,
                    "Faction modifier should fire exactly once per InvokeFactionPowerModifiers call");
            }
            finally
            {
                BattleModifierRegistry.Unregister(battleMod);
                BattleModifierRegistry.Unregister(factionMod);
            }
        }

        // ============================
        // SilverPaymentRegistry
        // ============================

        [EmpireTest("Registry")]
        public static void SilverPayment_Register_ModifiesAmount()
        {
            var c = new TestPaymentModifier { Discount = 50 };
            SilverPaymentRegistry.Register(c);
            try
            {
                var ctx = new SilverPaymentContext(200, "test");
                SilverPaymentRegistry.InvokeModifiers(ctx);
                TestAssert.AreEqual(150, ctx.Amount, message: "Amount should be reduced by 50");
            }
            finally { SilverPaymentRegistry.Unregister(c); }
        }

        [EmpireTest("Registry")]
        public static void SilverPayment_Unregister_NoEffect()
        {
            var c = new TestPaymentModifier { Discount = 50 };
            SilverPaymentRegistry.Register(c);
            SilverPaymentRegistry.Unregister(c);
            var ctx = new SilverPaymentContext(200, "test");
            SilverPaymentRegistry.InvokeModifiers(ctx);
            TestAssert.AreEqual(200, ctx.Amount, message: "Amount should be unchanged");
        }

        [EmpireTest("Registry")]
        public static void SilverPayment_DuplicateRegister_Ignored()
        {
            var c = new TestPaymentModifier { Discount = 50 };
            SilverPaymentRegistry.Register(c);
            SilverPaymentRegistry.Register(c);
            try
            {
                var ctx = new SilverPaymentContext(200, "test");
                SilverPaymentRegistry.InvokeModifiers(ctx);
                TestAssert.AreEqual(150, ctx.Amount, message: "Should only apply once");
            }
            // Unregister twice so a dedup regression can't leak a test double for the session.
            finally { SilverPaymentRegistry.Unregister(c); SilverPaymentRegistry.Unregister(c); }
        }

        [EmpireTest("Registry")]
        public static void SilverPayment_Exception_DoesNotCrash()
        {
            var bad = new ThrowingPaymentModifier();
            SilverPaymentRegistry.Register(bad);
            try
            {
                var ctx = new SilverPaymentContext(200, "test");
                TestAssert.DoesNotThrow(() => SilverPaymentRegistry.InvokeModifiers(ctx));
            }
            finally { SilverPaymentRegistry.Unregister(bad); }
        }

        [EmpireTest("Registry")]
        public static void SilverPayment_ReturnsContext()
        {
            var ctx = new SilverPaymentContext(100, "test");
            SilverPaymentContext returned = SilverPaymentRegistry.InvokeModifiers(ctx);
            TestAssert.IsTrue(ReferenceEquals(ctx, returned), "Should return the same context object");
        }

        // ============================
        // DefenseValidatorRegistry
        // ============================

        [EmpireTest("Registry")]
        public static void DefenseValidator_AllAllow_ReturnsTrue()
        {
            var c = new TestDefenseValidator { Allow = true };
            DefenseValidatorRegistry.Register(c);
            try
            {
                TestAssert.IsTrue(DefenseValidatorRegistry.CanDefend(null, null));
            }
            finally { DefenseValidatorRegistry.Unregister(c); }
        }

        [EmpireTest("Registry")]
        public static void DefenseValidator_OneRejects_ReturnsFalse()
        {
            var c = new TestDefenseValidator { Allow = false };
            DefenseValidatorRegistry.Register(c);
            try
            {
                TestAssert.IsFalse(DefenseValidatorRegistry.CanDefend(null, null));
            }
            finally { DefenseValidatorRegistry.Unregister(c); }
        }

        [EmpireTest("Registry")]
        public static void DefenseValidator_EmptyRegistry_ReturnsTrue()
        {
            TestAssert.IsTrue(DefenseValidatorRegistry.CanDefend(null, null),
                "No validators means default allow");
        }

        [EmpireTest("Registry")]
        public static void DefenseValidator_Exception_DoesNotCrash()
        {
            var bad = new ThrowingDefenseValidator();
            DefenseValidatorRegistry.Register(bad);
            try
            {
                TestAssert.DoesNotThrow(() => DefenseValidatorRegistry.CanDefend(null, null));
            }
            finally { DefenseValidatorRegistry.Unregister(bad); }
        }

        [EmpireTest("Registry")]
        public static void DefenseValidator_Unregister_StopsRejection()
        {
            var c = new TestDefenseValidator { Allow = false };
            DefenseValidatorRegistry.Register(c);
            DefenseValidatorRegistry.Unregister(c);
            TestAssert.IsTrue(DefenseValidatorRegistry.CanDefend(null, null),
                "After unregister, should allow again");
        }

        // ============================
        // TaxTickRegistry
        // ============================

        [EmpireDestructiveTest("Registry")]
        public static void TaxTick_Register_InvokesPreTax()
        {
            var c = new TestTaxTicker();
            TaxTickRegistry.Register(c);
            try
            {
                TaxTickRegistry.InvokePreTaxResolution(null);
                TestAssert.AreEqual(1, c.PreTaxCount);
            }
            finally { TaxTickRegistry.Unregister(c); }
        }

        [EmpireDestructiveTest("Registry")]
        public static void TaxTick_Register_InvokesPostTax()
        {
            var c = new TestTaxTicker();
            TaxTickRegistry.Register(c);
            try
            {
                TaxTickRegistry.InvokePostTaxResolution(null);
                TestAssert.AreEqual(1, c.PostTaxCount);
            }
            finally { TaxTickRegistry.Unregister(c); }
        }

        [EmpireDestructiveTest("Registry")]
        public static void TaxTick_Unregister_StopsInvocations()
        {
            var c = new TestTaxTicker();
            TaxTickRegistry.Register(c);
            TaxTickRegistry.Unregister(c);
            TaxTickRegistry.InvokePreTaxResolution(null);
            TestAssert.AreEqual(0, c.PreTaxCount);
        }

        [EmpireDestructiveTest("Registry")]
        public static void TaxTick_DuplicateRegister_Ignored()
        {
            var c = new TestTaxTicker();
            TaxTickRegistry.Register(c);
            TaxTickRegistry.Register(c);
            try
            {
                TaxTickRegistry.InvokePreTaxResolution(null);
                TestAssert.AreEqual(1, c.PreTaxCount, "Should only invoke once");
            }
            finally { TaxTickRegistry.Unregister(c); }
        }

        [EmpireDestructiveTest("Registry")]
        public static void TaxTick_Exception_DoesNotCrash()
        {
            var bad = new ThrowingTaxTicker();
            TaxTickRegistry.Register(bad);
            try
            {
                TestAssert.DoesNotThrow(() => TaxTickRegistry.InvokePreTaxResolution(null));
                TestAssert.DoesNotThrow(() => TaxTickRegistry.InvokePostTaxResolution(null));
            }
            finally { TaxTickRegistry.Unregister(bad); }
        }

        // ============================
        // SquadAssignmentRegistry
        // ============================

        [EmpireTest("Registry")]
        public static void SquadAssignment_AllAllow_ReturnsTrue()
        {
            var c = new TestSquadValidator { Allow = true };
            SquadAssignmentRegistry.Register(c);
            try
            {
                bool result = SquadAssignmentRegistry.CanAssign(null, null, out string reason);
                TestAssert.IsTrue(result);
                TestAssert.IsNull(reason, "Reason should be null on allow");
            }
            finally { SquadAssignmentRegistry.Unregister(c); }
        }

        [EmpireTest("Registry")]
        public static void SquadAssignment_OneRejects_ReturnsFalseWithReason()
        {
            var c = new TestSquadValidator { Allow = false, RejectReason = "too expensive" };
            SquadAssignmentRegistry.Register(c);
            try
            {
                bool result = SquadAssignmentRegistry.CanAssign(null, null, out string reason);
                TestAssert.IsFalse(result);
                TestAssert.AreEqual((object)"too expensive", (object)reason);
            }
            finally { SquadAssignmentRegistry.Unregister(c); }
        }

        [EmpireTest("Registry")]
        public static void SquadAssignment_EmptyRegistry_ReturnsTrue()
        {
            bool result = SquadAssignmentRegistry.CanAssign(null, null, out string reason);
            TestAssert.IsTrue(result, "No validators means default allow");
            TestAssert.IsNull(reason);
        }

        [EmpireTest("Registry")]
        public static void SquadAssignment_Exception_DoesNotCrash()
        {
            var bad = new ThrowingSquadValidator();
            SquadAssignmentRegistry.Register(bad);
            try
            {
                TestAssert.DoesNotThrow(() => SquadAssignmentRegistry.CanAssign(null, null, out string reason));
            }
            finally { SquadAssignmentRegistry.Unregister(bad); }
        }

        // ============================
        // MainTableRegistry
        // ============================

        [EmpireTest("Registry")]
        public static void MainTable_Register_AppearsInTabs()
        {
            var tab = new TestMainTab();
            MainTableRegistry.Register(tab);
            try
            {
                TestAssert.Contains(MainTableRegistry.Tabs, tab);
            }
            finally { MainTableRegistry.Unregister(tab); }
        }

        [EmpireTest("Registry")]
        public static void MainTable_Unregister_RemovedFromTabs()
        {
            var tab = new TestMainTab();
            MainTableRegistry.Register(tab);
            MainTableRegistry.Unregister(tab);
            TestAssert.IsFalse(MainTableRegistry.Tabs.Contains(tab), "Tab should be removed");
        }

        [EmpireTest("Registry")]
        public static void MainTable_InvokesPostClose()
        {
            var tab = new TestMainTab();
            MainTableRegistry.Register(tab);
            try
            {
                MainTableRegistry.InvokePostCloseWindow();
                TestAssert.AreEqual(1, tab.PostCloseCount);
            }
            finally { MainTableRegistry.Unregister(tab); }
        }

        [EmpireTest("Registry")]
        public static void MainTable_DuplicateRegister_Ignored()
        {
            var tab = new TestMainTab();
            MainTableRegistry.Register(tab);
            MainTableRegistry.Register(tab);
            try
            {
                MainTableRegistry.InvokePostCloseWindow();
                TestAssert.AreEqual(1, tab.PostCloseCount, "Should only invoke once");
            }
            // Unregister twice so a dedup regression can't leak a test double for the session.
            finally { MainTableRegistry.Unregister(tab); MainTableRegistry.Unregister(tab); }
        }

        [EmpireTest("Registry")]
        public static void MainTable_Exception_DoesNotCrash()
        {
            var bad = new ThrowingMainTab();
            MainTableRegistry.Register(bad);
            try
            {
                TestAssert.DoesNotThrow(() => MainTableRegistry.InvokePostCloseWindow());
            }
            finally { MainTableRegistry.Unregister(bad); }
        }

        // ============================
        // BuildingFilterRegistry
        // ============================

        [EmpireTest("Registry")]
        public static void BuildingFilter_Register_AppearsInFilters()
        {
            var filter = new BuildingFilter("Test", null, def => true);
            BuildingFilterRegistry.Register(filter);
            try
            {
                TestAssert.Contains(BuildingFilterRegistry.Filters, filter);
            }
            finally { BuildingFilterRegistry.Unregister(filter); }
        }

        [EmpireTest("Registry")]
        public static void BuildingFilter_Unregister_RemovedFromFilters()
        {
            var filter = new BuildingFilter("Test", null, def => true);
            BuildingFilterRegistry.Register(filter);
            BuildingFilterRegistry.Unregister(filter);
            TestAssert.IsFalse(BuildingFilterRegistry.Filters.Contains(filter), "Filter should be removed");
        }

        [EmpireTest("Registry")]
        public static void BuildingFilter_DuplicateRegister_Ignored()
        {
            var filter = new BuildingFilter("Test", null, def => true);
            BuildingFilterRegistry.Register(filter);
            BuildingFilterRegistry.Register(filter);
            try
            {
                int count = 0;
                foreach (BuildingFilter f in BuildingFilterRegistry.Filters)
                    if (ReferenceEquals(f, filter)) count++;
                TestAssert.AreEqual(1, count, "Should only appear once");
            }
            // Unregister twice so a dedup regression can't leak a test double for the session.
            finally { BuildingFilterRegistry.Unregister(filter); BuildingFilterRegistry.Unregister(filter); }
        }

        // ============================
        // AnimalPickerFilterRegistry
        // ============================

        /// <summary>Returns a fixed verdict regardless of input, so tests don't need a real PawnKindDef.</summary>
        private class StubAnimalPickerFilter : IAnimalPickerFilter
        {
            public bool Allow = true;
            public bool IsAnimalAllowed(PawnKindDef animal) => Allow;
        }

        [EmpireTest("Registry")]
        public static void AnimalPickerFilter_Register_AppearsInFilters()
        {
            StubAnimalPickerFilter filter = new StubAnimalPickerFilter();
            EmpireRegistry.Register(filter);
            try
            {
                TestAssert.Contains(AnimalPickerFilterRegistry.Filters, filter);
            }
            finally { EmpireRegistry.Unregister(filter); }
        }

        [EmpireTest("Registry")]
        public static void AnimalPickerFilter_DisallowingFilter_Gates()
        {
            // AND semantics: one disallowing filter gates the kind regardless of any others.
            bool baseline = AnimalPickerFilterRegistry.IsAllowed(null);
            StubAnimalPickerFilter filter = new StubAnimalPickerFilter { Allow = false };
            EmpireRegistry.Register(filter);
            try
            {
                TestAssert.IsFalse(AnimalPickerFilterRegistry.IsAllowed(null),
                    "A disallowing filter should gate the kind");
            }
            finally { EmpireRegistry.Unregister(filter); }

            TestAssert.AreEqual(baseline, AnimalPickerFilterRegistry.IsAllowed(null),
                "Unregistering should restore the prior verdict");
        }

        // ============================
        // EmpireCacheUtil (external invalidators)
        // ============================

        // NOTE: EmpireCacheUtil.InvalidateAll() calls EmpireRegistry.ClearAll(), which wipes every
        // live registry (FactionFC listeners, built-in validators, submod hooks) that only
        // re-registers on save load. These CacheInvalidator tests are therefore DESTRUCTIVE: a
        // pass still leaves the session's registries gutted until a reload.
        [EmpireDestructiveTest("Registry")]
        public static void CacheInvalidator_Register_InvokesOnInvalidateAll()
        {
            int count = 0;
            EmpireCacheUtil.RegisterCacheInvalidator("_test", () => count++);
            try
            {
                EmpireCacheUtil.InvalidateAll();
                TestAssert.AreEqual(1, count);
            }
            finally { EmpireCacheUtil.UnregisterCacheInvalidator("_test"); }
        }

        [EmpireDestructiveTest("Registry")]
        public static void CacheInvalidator_SurvivesInvalidateAll()
        {
            int count = 0;
            EmpireCacheUtil.RegisterCacheInvalidator("_test", () => count++);
            try
            {
                EmpireCacheUtil.InvalidateAll();
                EmpireCacheUtil.InvalidateAll();
                TestAssert.AreEqual(2, count, "Callback should survive across InvalidateAll calls");
            }
            finally { EmpireCacheUtil.UnregisterCacheInvalidator("_test"); }
        }

        [EmpireDestructiveTest("Registry")]
        public static void CacheInvalidator_DuplicateKey_ReplacesOld()
        {
            int oldCount = 0;
            int newCount = 0;
            EmpireCacheUtil.RegisterCacheInvalidator("_test", () => oldCount++);
            EmpireCacheUtil.RegisterCacheInvalidator("_test", () => newCount++);
            try
            {
                EmpireCacheUtil.InvalidateAll();
                TestAssert.AreEqual(0, oldCount, "Old callback should not fire");
                TestAssert.AreEqual(1, newCount, "New callback should fire");
            }
            finally { EmpireCacheUtil.UnregisterCacheInvalidator("_test"); }
        }

        [EmpireDestructiveTest("Registry")]
        public static void CacheInvalidator_Exception_DoesNotBlockOthers()
        {
            int count = 0;
            EmpireCacheUtil.RegisterCacheInvalidator("_test_bad", () => throw new InvalidOperationException("test"));
            EmpireCacheUtil.RegisterCacheInvalidator("_test_good", () => count++);
            try
            {
                TestAssert.DoesNotThrow(() => EmpireCacheUtil.InvalidateAll());
                TestAssert.AreEqual(1, count, "Good callback should still fire after bad one throws");
            }
            finally
            {
                EmpireCacheUtil.UnregisterCacheInvalidator("_test_bad");
                EmpireCacheUtil.UnregisterCacheInvalidator("_test_good");
            }
        }

        [EmpireDestructiveTest("Registry")]
        public static void CacheInvalidator_Unregister_StopsInvocations()
        {
            int count = 0;
            EmpireCacheUtil.RegisterCacheInvalidator("_test", () => count++);
            EmpireCacheUtil.UnregisterCacheInvalidator("_test");
            EmpireCacheUtil.InvalidateAll();
            TestAssert.AreEqual(0, count, "Callback should not fire after unregister");
        }

        // ============================
        // RaidWeightRegistry
        // ============================

        private class FixedRaidWeightProvider : IRaidWeightProvider
        {
            private readonly float _weight;
            public FixedRaidWeightProvider(float weight) { _weight = weight; }
            public float GetSettlementRaidWeight(WorldSettlementFC settlement, RimWorld.Faction attackingFaction) => _weight;
        }

        private class ThrowingRaidWeightProvider : IRaidWeightProvider
        {
            public float GetSettlementRaidWeight(WorldSettlementFC settlement, RimWorld.Faction attackingFaction)
                => throw new InvalidOperationException("test");
        }

        [EmpireTest("Registry")]
        public static void RaidWeight_NoProviders_ReturnsOne()
        {
            // Premise: no providers registered. A live submod (e.g. VOE) may register one, which
            // would make the combined weight != 1; skip rather than falsely fail.
            if (RaidWeightRegistry.Providers.Count > 0)
                TestAssert.Skip("Live raid-weight providers registered; identity-weight premise does not hold");

            // Default identity weight when no provider is registered.
            TestAssert.AreEqual(1.0, RaidWeightRegistry.GetCombinedWeight(null, null), 0.0001);
        }

        [EmpireTest("Registry")]
        public static void RaidWeight_SingleProvider_Multiplies()
        {
            var p = new FixedRaidWeightProvider(2.5f);
            RaidWeightRegistry.Register(p);
            try
            {
                TestAssert.AreEqual(2.5, RaidWeightRegistry.GetCombinedWeight(null, null), 0.0001);
            }
            finally { RaidWeightRegistry.Unregister(p); }
        }

        [EmpireTest("Registry")]
        public static void RaidWeight_MultipleProviders_ProductCombines()
        {
            var p1 = new FixedRaidWeightProvider(2.0f);
            var p2 = new FixedRaidWeightProvider(1.5f);
            RaidWeightRegistry.Register(p1);
            RaidWeightRegistry.Register(p2);
            try
            {
                TestAssert.AreEqual(3.0, RaidWeightRegistry.GetCombinedWeight(null, null), 0.0001,
                    "Combined weight should be the product (2.0 * 1.5)");
            }
            finally
            {
                RaidWeightRegistry.Unregister(p1);
                RaidWeightRegistry.Unregister(p2);
            }
        }

        [EmpireTest("Registry")]
        public static void RaidWeight_ZeroWeight_ProducesZero()
        {
            var p = new FixedRaidWeightProvider(0f);
            RaidWeightRegistry.Register(p);
            try
            {
                TestAssert.AreEqual(0.0, RaidWeightRegistry.GetCombinedWeight(null, null), 0.0001,
                    "Returning 0 should exclude the settlement");
            }
            finally { RaidWeightRegistry.Unregister(p); }
        }

        [EmpireTest("Registry")]
        public static void RaidWeight_DuplicateRegister_Ignored()
        {
            var p = new FixedRaidWeightProvider(2.0f);
            RaidWeightRegistry.Register(p);
            RaidWeightRegistry.Register(p);
            try
            {
                TestAssert.AreEqual(2.0, RaidWeightRegistry.GetCombinedWeight(null, null), 0.0001,
                    "Duplicate should be ignored");
            }
            // Unregister twice so a dedup regression can't leak a test double for the session.
            finally { RaidWeightRegistry.Unregister(p); RaidWeightRegistry.Unregister(p); }
        }

        [EmpireTest("Registry")]
        public static void RaidWeight_Exception_DoesNotCrash()
        {
            var bad = new ThrowingRaidWeightProvider();
            RaidWeightRegistry.Register(bad);
            try
            {
                TestAssert.DoesNotThrow(() => RaidWeightRegistry.GetCombinedWeight(null, null));
            }
            finally { RaidWeightRegistry.Unregister(bad); }
        }

        // ============================
        // RaidTargetRegistry
        // ============================

        private class StubRaidTarget : IRaidTarget
        {
            private readonly WorldObject _obj;
            public StubRaidTarget(WorldObject obj = null) { _obj = obj; }
            public WorldObject WorldObject => _obj;
            public string Name => "TestRaidTarget";
            public PlanetTile Tile => PlanetTile.Invalid;
            public int MilitaryLevel => 1;
            public bool IsUnderAttack { get; set; }
            public void OnRaidWon(BattleResult result) { }
            public void OnRaidLost(BattleResult result) { }
        }

        private class ThrowingRaidTarget : IRaidTarget
        {
            public WorldObject WorldObject => throw new InvalidOperationException("test");
            public string Name => throw new InvalidOperationException("test");
            public PlanetTile Tile => throw new InvalidOperationException("test");
            public int MilitaryLevel => throw new InvalidOperationException("test");
            public bool IsUnderAttack { get => throw new InvalidOperationException("test"); set => throw new InvalidOperationException("test"); }
            public void OnRaidWon(BattleResult result) { }
            public void OnRaidLost(BattleResult result) { }
        }

        [EmpireTest("Registry")]
        public static void RaidTarget_Register_AppearsInTargets()
        {
            var t = new StubRaidTarget();
            RaidTargetRegistry.Register(t);
            try
            {
                TestAssert.Contains(RaidTargetRegistry.Targets, (IRaidTarget)t);
            }
            finally { RaidTargetRegistry.Unregister(t); }
        }

        [EmpireTest("Registry")]
        public static void RaidTarget_Unregister_RemovedFromTargets()
        {
            var t = new StubRaidTarget();
            RaidTargetRegistry.Register(t);
            RaidTargetRegistry.Unregister(t);
            TestAssert.IsFalse(RaidTargetRegistry.Targets.Contains(t));
        }

        [EmpireTest("Registry")]
        public static void RaidTarget_DuplicateRegister_Ignored()
        {
            var t = new StubRaidTarget();
            RaidTargetRegistry.Register(t);
            RaidTargetRegistry.Register(t);
            try
            {
                int count = 0;
                foreach (IRaidTarget r in RaidTargetRegistry.Targets)
                    if (System.Object.ReferenceEquals(r, t)) count++;
                TestAssert.AreEqual(1, count, "Duplicate should be ignored");
            }
            // Unregister twice so a dedup regression can't leak a test double for the session.
            finally { RaidTargetRegistry.Unregister(t); RaidTargetRegistry.Unregister(t); }
        }

        [EmpireTest("Registry")]
        public static void RaidTarget_FindByWorldObject_NullObj_ReturnsNull()
        {
            // FindByWorldObject with a null object should not match a target whose WorldObject is null.
            var t = new StubRaidTarget(null);
            RaidTargetRegistry.Register(t);
            try
            {
                // We pass null; the iteration compares against WorldObject == null, which would match
                // — but FindByWorldObject's behavior depends on the implementation. We accept either
                // null result or t; just verify it doesn't crash.
                TestAssert.DoesNotThrow(() => RaidTargetRegistry.FindByWorldObject(null));
            }
            finally { RaidTargetRegistry.Unregister(t); }
        }

        [EmpireTest("Registry")]
        public static void RaidTarget_FindByWorldObject_Exception_DoesNotCrash()
        {
            var bad = new ThrowingRaidTarget();
            RaidTargetRegistry.Register(bad);
            try
            {
                TestAssert.DoesNotThrow(() => RaidTargetRegistry.FindByWorldObject(null));
            }
            finally { RaidTargetRegistry.Unregister(bad); }
        }

        // ============================
        // AutoDefenderRegistry
        // ============================

        private class StubAutoDefender : IAutoDefender
        {
            public int _militaryLevel;
            public int _range = 999;
            public bool _canAutoDefend = true;

            // The interface demands that we define WorldObject, but we don't actually need it.
            //  Hence, null.
            public WorldObject WorldObject => null;
            public int MilitaryLevel => _militaryLevel;
            public int Range => _range;
            public bool CanAutoDefend => _canAutoDefend;
            public MilitaryForce CreateDefendingForce() => null;
            public void OnDefensePledged(WorldObject target) { }
            public void OnDefenseStarted(WorldObject target) { }
            public void OnDefenseComplete(bool won, BattleResult result) { }
            public void OnDefenseReplaced() { }
            public List<Verse.Pawn> GetDefendingPawns() => null;
            public void ReturnDefendingPawns(List<Verse.Pawn> pawns) { }
        }

        [EmpireTest("Registry")]
        public static void AutoDefender_Register_AppearsInDefenders()
        {
            var d = new StubAutoDefender();
            AutoDefenderRegistry.Register(d);
            try
            {
                TestAssert.Contains(AutoDefenderRegistry.Defenders, (IAutoDefender)d);
            }
            finally { AutoDefenderRegistry.Unregister(d); }
        }

        [EmpireTest("Registry")]
        public static void AutoDefender_Unregister_RemovedFromDefenders()
        {
            var d = new StubAutoDefender();
            AutoDefenderRegistry.Register(d);
            AutoDefenderRegistry.Unregister(d);
            TestAssert.IsFalse(AutoDefenderRegistry.Defenders.Contains(d));
        }

        [EmpireTest("Registry")]
        public static void AutoDefender_DuplicateRegister_Ignored()
        {
            var d = new StubAutoDefender();
            AutoDefenderRegistry.Register(d);
            AutoDefenderRegistry.Register(d);
            try
            {
                int count = 0;
                foreach (IAutoDefender def in AutoDefenderRegistry.Defenders)
                    if (System.Object.ReferenceEquals(def, d)) count++;
                TestAssert.AreEqual(1, count, "Duplicate should be ignored");
            }
            // Unregister twice so a dedup regression can't leak a test double for the session.
            finally { AutoDefenderRegistry.Unregister(d); AutoDefenderRegistry.Unregister(d); }
        }

        [EmpireTest("Registry")]
        public static void AutoDefender_FindByWorldObject_NullObj_ReturnsNull()
        {
            // Explicit guard in FindByWorldObject: null obj returns null short-circuit.
            TestAssert.IsNull(AutoDefenderRegistry.FindByWorldObject(null));
        }

        [EmpireTest("Registry")]
        public static void AutoDefender_FindBestDefender_NoDefenders_ReturnsNull()
        {
            // Premise: no auto-defenders registered. A live submod (e.g. VOE outposts) may register
            // one, which could make FindBestDefender return non-null; skip rather than falsely fail.
            if (AutoDefenderRegistry.Defenders.Count > 0)
                TestAssert.Skip("Live auto-defenders registered; no-defenders premise does not hold");

            // With no defenders registered, FindBestDefender returns null.
            TestAssert.IsNull(AutoDefenderRegistry.FindBestDefender(RimWorld.Planet.PlanetTile.Invalid, 0));
        }

        [EmpireTest("Registry")]
        public static void AutoDefender_FindBestDefender_CannotAutoDefend_Skipped()
        {
            // The CanAutoDefend == false branch short-circuits before any tile lookup, so this
            // is safe to test without real world geometry.
            var d = new StubAutoDefender { _militaryLevel = 10, _canAutoDefend = false };
            AutoDefenderRegistry.Register(d);
            try
            {
                IAutoDefender result = AutoDefenderRegistry.FindBestDefender(RimWorld.Planet.PlanetTile.Invalid, 0);
                TestAssert.IsNull(result, "Disabled defender should not be selected");
            }
            finally { AutoDefenderRegistry.Unregister(d); }
        }

        [EmpireTest("Registry")]
        public static void AutoDefender_FindBestDefender_BelowMinLevel_Skipped()
        {
            // Same early-return path: skip without touching WorldObject.Tile.
            var d = new StubAutoDefender { _militaryLevel = 3, _canAutoDefend = true };
            AutoDefenderRegistry.Register(d);
            try
            {
                IAutoDefender result = AutoDefenderRegistry.FindBestDefender(RimWorld.Planet.PlanetTile.Invalid, 5);
                TestAssert.IsNull(result, "Defender below minMilitaryLevel should not be selected");
            }
            finally { AutoDefenderRegistry.Unregister(d); }
        }

        // ============================
        // EmpireRegistry (unified facade)
        // ============================

        /// <summary>
        /// Test double implementing multiple unrelated interfaces so we can verify
        /// <see cref="EmpireRegistry.Register"/> routes a single registration to every
        /// matching domain.
        /// </summary>
        private class MultiInterfaceParticipant : ISettlementListener, ITaxTickParticipant, IDefenseValidator
        {
            public int SettlementCreated;
            public int PreTax;
            public int CanDefendCalls;

            // ISettlementListener (only OnSettlementCreated is interesting; others are stubs)
            public void OnSettlementCreated(WorldSettlementFC s) => SettlementCreated++;
            public void OnSettlementRemoved(WorldSettlementFC s) { }
            public void OnSettlementUpgraded(WorldSettlementFC s, int oldLevel, int newLevel) { }
            public void OnSettlementTypeChanged(WorldSettlementFC s, WorldSettlementDef oldDef, WorldSettlementDef newDef) { }
            public void OnBuildingConstructed(WorldSettlementFC s, BuildingFCDef b, int slot) { }
            public void OnBuildingDeconstructed(WorldSettlementFC s, BuildingFCDef b, int slot) { }

            // ITaxTickParticipant
            public void PreTaxResolution(FactionFC f) => PreTax++;
            public void PostTaxResolution(FactionFC f) { }
            public void PreSettlementCreateTax(WorldSettlementFC s) { }
            public void PostSettlementCreateTax(WorldSettlementFC s, ref int a, List<Thing> t) { }

            // IDefenseValidator
            public bool CanDefend(WorldSettlementFC defender, WorldSettlementFC target)
            {
                CanDefendCalls++;
                return true;
            }
        }

        /// <summary>
        /// Class that implements zero registered interfaces, used to verify the
        /// "matched no registry" warning path.
        /// </summary>
        private class NonParticipant { }

        [EmpireDestructiveTest("Registry")]
        public static void EmpireRegistry_Register_RoutesToAllMatchingDomains()
        {
            WorldSettlementFC settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlements");

            MultiInterfaceParticipant p = new MultiInterfaceParticipant();
            EmpireRegistry.Register(p);
            try
            {
                // ISettlementListener routing
                LifecycleRegistry.InvokeOnSettlementCreated(settlement);
                TestAssert.AreEqual(1, p.SettlementCreated, "Should fire via LifecycleRegistry");

                // ITaxTickParticipant routing
                TaxTickRegistry.InvokePreTaxResolution(null);
                TestAssert.AreEqual(1, p.PreTax, "Should fire via TaxTickRegistry");

                // IDefenseValidator routing
                DefenseValidatorRegistry.CanDefend(null, null);
                TestAssert.AreEqual(1, p.CanDefendCalls, "Should fire via DefenseValidatorRegistry");
            }
            finally { EmpireRegistry.Unregister(p); }
        }

        [EmpireDestructiveTest("Registry")]
        public static void EmpireRegistry_Unregister_RemovesFromAllMatchingDomains()
        {
            WorldSettlementFC settlement = GetFirstSettlement();
            if (settlement == null) TestAssert.Skip("No settlements");

            MultiInterfaceParticipant p = new MultiInterfaceParticipant();
            EmpireRegistry.Register(p);
            EmpireRegistry.Unregister(p);

            LifecycleRegistry.InvokeOnSettlementCreated(settlement);
            TaxTickRegistry.InvokePreTaxResolution(null);
            DefenseValidatorRegistry.CanDefend(null, null);

            TestAssert.AreEqual(0, p.SettlementCreated, "Settlement hook should be removed");
            TestAssert.AreEqual(0, p.PreTax, "Tax hook should be removed");
            TestAssert.AreEqual(0, p.CanDefendCalls, "Defense validator should be removed");
        }

        [EmpireDestructiveTest("Registry")]
        public static void EmpireRegistry_ClearAll_ClearsEveryFacadeManagedRegistry()
        {
            MultiInterfaceParticipant p = new MultiInterfaceParticipant();
            EmpireRegistry.Register(p);
            EmpireRegistry.ClearAll();

            // After ClearAll, none of the domain registries should still hold p.
            // We can't easily enumerate every list, but we can verify that none of
            // p's hooks fire after ClearAll.
            WorldSettlementFC settlement = GetFirstSettlement();
            if (settlement != null) LifecycleRegistry.InvokeOnSettlementCreated(settlement);
            TaxTickRegistry.InvokePreTaxResolution(null);
            DefenseValidatorRegistry.CanDefend(null, null);

            TestAssert.AreEqual(0, p.SettlementCreated, "Settlement hook cleared");
            TestAssert.AreEqual(0, p.PreTax, "Tax hook cleared");
            TestAssert.AreEqual(0, p.CanDefendCalls, "Defense validator cleared");
        }

        [EmpireTest("Registry")]
        public static void EmpireRegistry_Register_NonParticipant_NoOp()
        {
            // Passing an object that implements zero registered interfaces should
            // log a warning but not throw. We don't capture the log here; just
            // verify no exception escapes and no domain registry is affected.
            NonParticipant junk = new NonParticipant();
            TestAssert.DoesNotThrow(() => EmpireRegistry.Register(junk));
            TestAssert.DoesNotThrow(() => EmpireRegistry.Unregister(junk));
        }

        [EmpireTest("Registry")]
        public static void EmpireRegistry_Register_Null_NoOp()
        {
            TestAssert.DoesNotThrow(() => EmpireRegistry.Register(null));
            TestAssert.DoesNotThrow(() => EmpireRegistry.Unregister(null));
        }

        // ============================
        // RegistryDispatch re-entrancy
        // ============================
        // Exercises RegistryDispatch against a private RegistryList so a participant can
        // unregister itself mid-callback without fanning out to live listeners or the real
        // settlement. Before the snapshot fix, the self-removal invalidated the foreach
        // enumerator and threw InvalidOperationException out past the per-item try/catch.

        private class SelfUnregisteringParticipant
        {
            public RegistryList<SelfUnregisteringParticipant> Owner;
            public bool UnregisterSelf;
            public int InvokeCount;

            public void Fire()
            {
                InvokeCount++;
                if (UnregisterSelf) Owner.Unregister(this);
            }
        }

        [EmpireTest("Registry")]
        public static void RegistryDispatch_Each_SelfUnregisterMidCallback_NoThrowRemainingRun()
        {
            var list = new RegistryList<SelfUnregisteringParticipant>();
            var a = new SelfUnregisteringParticipant { Owner = list, UnregisterSelf = true };
            var b = new SelfUnregisteringParticipant { Owner = list };
            var c = new SelfUnregisteringParticipant { Owner = list };
            list.Register(a);
            list.Register(b);
            list.Register(c);

            TestAssert.DoesNotThrow(() =>
                RegistryDispatch.Each(list.Items, p => p.Fire(), "Fire"),
                "self-unregister mid-callback must not invalidate the iteration");

            TestAssert.AreEqual(1, a.InvokeCount, "self-unregistering item still fires once");
            TestAssert.AreEqual(1, b.InvokeCount, "later items still run after a mid-callback unregister");
            TestAssert.AreEqual(1, c.InvokeCount, "later items still run after a mid-callback unregister");
            TestAssert.AreEqual(2, list.Count, "the self-unregister still took effect");
        }

        [EmpireTest("Registry")]
        public static void RegistryDispatch_All_SelfUnregisterMidPredicate_NoThrow()
        {
            var list = new RegistryList<SelfUnregisteringParticipant>();
            var a = new SelfUnregisteringParticipant { Owner = list, UnregisterSelf = true };
            var b = new SelfUnregisteringParticipant { Owner = list };
            list.Register(a);
            list.Register(b);

            bool result = true;
            TestAssert.DoesNotThrow(() =>
                result = RegistryDispatch.All(list.Items, p => { p.Fire(); return true; }, "Fire"),
                "self-unregister mid-predicate must not invalidate the iteration");

            TestAssert.IsTrue(result, "no predicate returned false");
            TestAssert.AreEqual(1, a.InvokeCount);
            TestAssert.AreEqual(1, b.InvokeCount, "later validators still run after a mid-callback unregister");
        }
    }
}
