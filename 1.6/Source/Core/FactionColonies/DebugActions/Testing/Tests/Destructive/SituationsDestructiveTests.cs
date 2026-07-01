using System.Linq;
using Verse;

namespace FactionColonies
{
    /* DESTRUCTIVE: starts, advances, switches, and removes real situations on the live faction
       situation manager, and charges upkeep against real player silver. The upkeep cases spend or
       drain silver and are NOT restored (the unaffordable case drains down to upkeep-1). Backed by
       the dormant EmpireTestSituation_* demo defs; each test skips cleanly if those defs are absent
       or there is no active game. */
    public static class SituationsDestructiveTests
    {
        private const string FactionDefName = "EmpireTestSituation_Faction";
        private const string SettlementDefName = "EmpireTestSituation_Settlement";
        private const string CrackdownDefName = "EmpireTest_Approach_Crackdown";

        private static FCSituationDef RequireFactionDef()
        {
            FCSituationDef def = DefDatabase<FCSituationDef>.GetNamedSilentFail(FactionDefName);
            if (def is null) TestAssert.Skip($"Demo def '{FactionDefName}' not found");
            return def;
        }

        private static FCSituationApproachDef ApproachOf(FCSituationDef def, string defName)
            => def.approaches.FirstOrDefault(a => a.defName == defName);

        [EmpireDestructiveTest("Destructive.Situations")]
        public static void StartSituation_AddsToManager()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            FCSituationDef def = RequireFactionDef();
            FCSituationManager mgr = f.situationManager;

            int before = mgr.Count;
            FCSituation sit = null;
            TestAssert.DoesNotThrow(() => sit = mgr.StartSituation(def, null), "StartSituation threw");
            TestAssert.IsNotNull(sit, "StartSituation should return an instance");
            TestAssert.AreEqual(before + 1, mgr.Count, "Count should increment by one");
            TestAssert.IsTrue(mgr.Situations.Contains(sit), "Situation should be in the manager");
            TestAssert.IsTrue(ReferenceEquals(sit.activeApproach, def.defaultApproach),
                "New situation should start on the default approach");
            TestAssert.IsTrue(ReferenceEquals(sit.currentStage, def.StageAt(def.startProgress)),
                "New situation stage should match StageAt(startProgress)");

            mgr.Remove(sit);
            DestructiveTestUtil.AssertEmpireInvariants(f, "StartSituation_AddsToManager");
        }

        [EmpireDestructiveTest("Destructive.Situations")]
        public static void AddProgress_MovesBarAndStage()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            FCSituationDef def = RequireFactionDef();
            FCSituationManager mgr = f.situationManager;

            FCSituation sit = mgr.StartSituation(def, null);
            TestAssert.IsNotNull(sit, "StartSituation should return an instance");
            try
            {
                float startProgress = sit.progress;
                FCSituationStageDef startStage = sit.currentStage;
                // Push above the first stage threshold (25) so both the bar and the stage move.
                TestAssert.DoesNotThrow(() => mgr.AddProgress(sit, 30f), "AddProgress threw");
                TestAssert.GreaterThan(sit.progress, startProgress, "Progress should have increased");
                TestAssert.IsFalse(ReferenceEquals(sit.currentStage, startStage),
                    "Crossing a threshold should change the current stage");
                TestAssert.IsTrue(ReferenceEquals(sit.currentStage, def.StageAt(sit.progress)),
                    "currentStage should equal StageAt(progress)");
            }
            finally { mgr.Remove(sit); }
            DestructiveTestUtil.AssertEmpireInvariants(f, "AddProgress_MovesBarAndStage");
        }

        [EmpireDestructiveTest("Destructive.Situations")]
        public static void SwitchApproach_ChangesApproach()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            FCSituationDef def = RequireFactionDef();
            FCSituationManager mgr = f.situationManager;
            FCSituationApproachDef crackdown = ApproachOf(def, CrackdownDefName);
            if (crackdown is null) TestAssert.Skip($"Approach '{CrackdownDefName}' not found");

            FCSituation sit = mgr.StartSituation(def, null);
            TestAssert.IsNotNull(sit, "StartSituation should return an instance");
            try
            {
                TestAssert.DoesNotThrow(() => mgr.SwitchApproach(sit, crackdown), "SwitchApproach threw");
                TestAssert.IsTrue(ReferenceEquals(sit.activeApproach, crackdown),
                    "activeApproach should be the switched-to approach");
                TestAssert.DoesNotThrow(() => mgr.SwitchApproach(sit, def.defaultApproach),
                    "SwitchApproach back threw");
                TestAssert.IsTrue(ReferenceEquals(sit.activeApproach, def.defaultApproach),
                    "activeApproach should revert to default after switching back");
            }
            finally { mgr.Remove(sit); }
            DestructiveTestUtil.AssertEmpireInvariants(f, "SwitchApproach_ChangesApproach");
        }

        [EmpireDestructiveTest("Destructive.Situations")]
        public static void Remove_ClearsSituation()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            FCSituationDef def = RequireFactionDef();
            FCSituationManager mgr = f.situationManager;

            FCSituation sit = mgr.StartSituation(def, null);
            TestAssert.IsNotNull(sit, "StartSituation should return an instance");

            int before = mgr.Count;
            bool removed = false;
            TestAssert.DoesNotThrow(() => removed = mgr.Remove(sit), "Remove threw");
            TestAssert.IsTrue(removed, "Remove should return true for a live situation");
            TestAssert.IsFalse(mgr.Situations.Contains(sit), "Situation should be gone after Remove");
            TestAssert.AreEqual(before - 1, mgr.Count, "Count should decrement by one");
            TestAssert.IsFalse(mgr.Remove(sit), "Second Remove should return false (idempotent)");
            DestructiveTestUtil.AssertEmpireInvariants(f, "Remove_ClearsSituation");
        }

        [EmpireDestructiveTest("Destructive.Situations")]
        public static void DailyUpkeep_AffordableDoesNotRevert()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            FCSituationDef def = RequireFactionDef();
            FCSituationManager mgr = f.situationManager;
            FCSituationApproachDef crackdown = ApproachOf(def, CrackdownDefName);
            if (crackdown is null) TestAssert.Skip($"Approach '{CrackdownDefName}' not found");
            if (crackdown.upkeepSilver <= 0) TestAssert.Skip("Crackdown approach has no upkeep to charge");
            if (PaymentUtil.GetSilver() < crackdown.upkeepSilver)
                TestAssert.Skip("Insufficient silver to test the affordable-upkeep path");

            FCSituation sit = mgr.StartSituation(def, null);
            TestAssert.IsNotNull(sit, "StartSituation should return an instance");
            try
            {
                mgr.SwitchApproach(sit, crackdown);
                int before = PaymentUtil.GetSilver();
                TestAssert.DoesNotThrow(() => new SituationUpkeepDailyCharger().PostDailyAccrual(f),
                    "PostDailyAccrual threw");
                TestAssert.IsTrue(ReferenceEquals(sit.activeApproach, crackdown),
                    "Affordable upkeep should not revert the approach");
                TestAssert.IsTrue(PaymentUtil.GetSilver() < before,
                    "Affordable upkeep should have spent silver");
            }
            finally { mgr.Remove(sit); }
            DestructiveTestUtil.AssertEmpireInvariants(f, "DailyUpkeep_AffordableDoesNotRevert");
        }

        [EmpireDestructiveTest("Destructive.Situations")]
        public static void DailyUpkeep_UnaffordableReverts()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            FCSituationDef def = RequireFactionDef();
            FCSituationManager mgr = f.situationManager;
            FCSituationApproachDef crackdown = ApproachOf(def, CrackdownDefName);
            if (crackdown is null) TestAssert.Skip($"Approach '{CrackdownDefName}' not found");
            if (crackdown.upkeepSilver <= 0) TestAssert.Skip("Crackdown approach has no upkeep to charge");

            // Drain silver to just below the upkeep so the charge is guaranteed to fail. DESTRUCTIVE:
            // removes player silver down to (upkeep - 1) and does not restore it.
            int excess = PaymentUtil.GetSilver() - (crackdown.upkeepSilver - 1);
            if (excess > 0) PaymentUtil.PaySilver(excess, "SituationTest_DrainSilver");
            TestAssert.IsTrue(PaymentUtil.GetSilver() < crackdown.upkeepSilver,
                "Silver should be below upkeep before the charge");

            FCSituation sit = mgr.StartSituation(def, null);
            TestAssert.IsNotNull(sit, "StartSituation should return an instance");
            try
            {
                mgr.SwitchApproach(sit, crackdown);
                TestAssert.DoesNotThrow(() => new SituationUpkeepDailyCharger().PostDailyAccrual(f),
                    "PostDailyAccrual threw");
                TestAssert.IsTrue(ReferenceEquals(sit.activeApproach, def.defaultApproach),
                    "Unaffordable upkeep should revert the approach to the default");
            }
            finally { mgr.Remove(sit); }
            DestructiveTestUtil.AssertEmpireInvariants(f, "DailyUpkeep_UnaffordableReverts");
        }

        [EmpireDestructiveTest("Destructive.Situations")]
        public static void SettlementScoped_LifecycleAndCleanup()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            FCSituationDef def = DefDatabase<FCSituationDef>.GetNamedSilentFail(SettlementDefName);
            if (def is null) TestAssert.Skip($"Demo def '{SettlementDefName}' not found");
            FCSituationManager mgr = f.situationManager;

            WorldSettlementFC settlement = DestructiveTestUtil.CreateTransientSettlement();
            if (settlement is null) TestAssert.Skip("Could not create a transient settlement");

            FCSituation sit = null;
            try
            {
                sit = mgr.StartSituation(def, settlement);
                TestAssert.IsNotNull(sit, "StartSituation should return an instance for a settlement-scoped def");
                TestAssert.IsTrue(mgr.AnyForSettlement(settlement),
                    "Manager should report a situation for the target settlement");
            }
            finally
            {
                DestructiveTestUtil.SafeRemoveSettlement(settlement);
            }

            TestAssert.IsFalse(mgr.AnyForSettlement(settlement),
                "Removing the settlement should clean up its situations");
            if (sit is object)
                TestAssert.IsFalse(mgr.Situations.Contains(sit),
                    "The settlement's situation should be gone after removal");
            DestructiveTestUtil.AssertEmpireInvariants(f, "SettlementScoped_LifecycleAndCleanup");
        }
    }
}
