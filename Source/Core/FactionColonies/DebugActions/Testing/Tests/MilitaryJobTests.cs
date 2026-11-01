using System.Linq;
using Verse;

namespace FactionColonies
{
    public static class MilitaryJobTests
    {
        private static FactionFC GetFaction()
        {
            return FindFC.FactionComp;
        }

        // ============================
        // MilitaryJobDef validation
        // ============================

        [EmpireTest("MilitaryJob")]
        public static void AllActionJobs_HaveHandler()
        {
            // Non-state jobs with a handlerClass should produce a valid Handler instance
            foreach (MilitaryJobDef def in DefDatabase<MilitaryJobDef>.AllDefsListForReading)
            {
                if (def.isState || def.handlerClass == null) continue;
                TestAssert.IsNotNull(def.Handler,
                    $"{def.defName}: has handlerClass {def.handlerClass.Name} but Handler is null");
            }
        }

        [EmpireTest("MilitaryJob")]
        public static void AllStateJobs_NoHandler()
        {
            TestAssert.IsTrue(MilitaryJobDefOf.Undefined.isState,
                "Undefined should be a state job");
            TestAssert.IsNull(MilitaryJobDefOf.Undefined.Handler,
                "Undefined should have no handler");

            TestAssert.IsTrue(MilitaryJobDefOf.Cooldown.isState,
                "Cooldown should be a state job");
            TestAssert.IsNull(MilitaryJobDefOf.Cooldown.Handler,
                "Cooldown should have no handler");

            TestAssert.IsTrue(MilitaryJobDefOf.DefendFriendlySettlement.isState,
                "DefendFriendlySettlement should be a state job");
            TestAssert.IsNull(MilitaryJobDefOf.DefendFriendlySettlement.Handler,
                "DefendFriendlySettlement should have no handler");
        }

        [EmpireTest("MilitaryJob")]
        public static void Handler_IsCached()
        {
            MilitaryJobHandler first = MilitaryJobDefOf.RaidEnemySettlement.Handler;
            MilitaryJobHandler second = MilitaryJobDefOf.RaidEnemySettlement.Handler;
            TestAssert.IsTrue(ReferenceEquals(first, second),
                "Handler should return the same cached instance on repeated access");
        }

        [EmpireTest("MilitaryJob")]
        public static void DefaultEnabled_KnownOptIns_IsFalse()
        {
            // EnslaveEnemySettlement is policy-gated (Authoritarian and Slaver unlock it).
            // RazeEnemySettlement is policy-gated too (militaristic/authoritarian/isolationist unlock it).
            // DefendOwnSettlement is internal: never selectable from the offensive float menu;
            // the manager assigns it directly when it creates a defensive op.
            foreach (MilitaryJobDef def in DefDatabase<MilitaryJobDef>.AllDefsListForReading)
            {
                if (def == MilitaryJobDefOf.EnslaveEnemySettlement
                    || def == MilitaryJobDefOf.RazeEnemySettlement
                    || def == MilitaryJobDefOf.DefendOwnSettlement)
                {
                    TestAssert.IsFalse(def.defaultEnabled,
                        $"{def.defName} should have defaultEnabled=false");
                }
                else
                {
                    TestAssert.IsTrue(def.defaultEnabled,
                        $"{def.defName} should have defaultEnabled=true");
                }
            }
        }

        [EmpireTest("MilitaryJob")]
        public static void OccupiesTarget_CorrectDefaults()
        {
            // These should occupy their target
            TestAssert.IsTrue(MilitaryJobDefOf.RaidEnemySettlement.occupiesTarget,
                "Raid should occupy target");
            TestAssert.IsTrue(MilitaryJobDefOf.EnslaveEnemySettlement.occupiesTarget,
                "Enslave should occupy target");
            TestAssert.IsTrue(MilitaryJobDefOf.CaptureEnemySettlement.occupiesTarget,
                "Capture should occupy target");

            // These should not
            TestAssert.IsFalse(MilitaryJobDefOf.Undefined.occupiesTarget,
                "Undefined should not occupy target");
            TestAssert.IsFalse(MilitaryJobDefOf.Cooldown.occupiesTarget,
                "Cooldown should not occupy target");
            TestAssert.IsFalse(MilitaryJobDefOf.Deploy.occupiesTarget,
                "Deploy should not occupy target");
            TestAssert.IsFalse(MilitaryJobDefOf.DefendFriendlySettlement.occupiesTarget,
                "DefendFriendlySettlement should not occupy target");
        }

        // (cooldownStatDef + deadPawnCooldown tests removed: those fields were dropped when the
        // cooldown system collapsed to travel-only. Travel duration is now computed from
        // TravelUtil.ReturnTicksToArrive in MilitaryOperation.ComputeCooldownTicks instead.)

        // ============================
        // Handler.IsValidTarget
        // ============================

        [EmpireTest("MilitaryJob")]
        public static void RaidHandler_AcceptsNullFaction()
        {
            TestAssert.IsTrue(MilitaryJobDefOf.RaidEnemySettlement.Handler.IsValidTarget(null),
                "Raid handler should accept null faction (base implementation returns true)");
        }

        [EmpireTest("MilitaryJob")]
        public static void CaptureHandler_AcceptsNullFaction()
        {
            TestAssert.IsTrue(MilitaryJobDefOf.CaptureEnemySettlement.Handler.IsValidTarget(null),
                "Capture handler should accept null faction (base implementation returns true)");
        }

        [EmpireTest("MilitaryJob")]
        public static void EnslaveHandler_AcceptsNullFaction()
        {
            // null?.def?.defName is null, which != "Insect", so returns true
            TestAssert.IsTrue(MilitaryJobDefOf.EnslaveEnemySettlement.Handler.IsValidTarget(null),
                "Enslave handler should accept null faction");
        }

        [EmpireTest("MilitaryJob")]
        public static void EnslaveHandler_RejectsInsectFaction()
        {
            // Check all game factions — only Insect should be rejected
            var allFactions = Find.FactionManager.AllFactions.ToList();
            MilitaryJobHandler handler = MilitaryJobDefOf.EnslaveEnemySettlement.Handler;

            foreach (RimWorld.Faction faction in allFactions)
            {
                if (faction.def.defName == "Insect")
                {
                    TestAssert.IsFalse(handler.IsValidTarget(faction),
                        "Enslave handler should reject Insect faction");
                }
                else
                {
                    TestAssert.IsTrue(handler.IsValidTarget(faction),
                        $"Enslave handler should accept faction {faction.def.defName}");
                }
            }
        }

        // ============================
        // IsMilitaryJobAllowed
        // ============================

        [EmpireTest("MilitaryJob")]
        public static void DefaultEnabledJob_AllowedByDefault()
        {
            var faction = GetFaction();
            if (faction == null) TestAssert.Skip("No faction");

            var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
            try
            {
                PolicyTestHelper.ClearAll(faction);
                TestAssert.IsTrue(FindFC.FactionComp.IsMilitaryJobAllowed(MilitaryJobDefOf.RaidEnemySettlement),
                    "Raid should be allowed by default (defaultEnabled=true)");
                TestAssert.IsTrue(FindFC.FactionComp.IsMilitaryJobAllowed(MilitaryJobDefOf.CaptureEnemySettlement),
                    "Capture should be allowed by default (defaultEnabled=true)");
            }
            finally
            {
                PolicyTestHelper.RestorePolicies(faction, snapshot);
            }
        }

        [EmpireTest("MilitaryJob")]
        public static void DefaultDisabledJob_BlockedByDefault()
        {
            var faction = GetFaction();
            if (faction == null) TestAssert.Skip("No faction");

            var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
            try
            {
                PolicyTestHelper.ClearAll(faction);
                TestAssert.IsFalse(FindFC.FactionComp.IsMilitaryJobAllowed(MilitaryJobDefOf.EnslaveEnemySettlement),
                    "Enslave should be blocked by default (defaultEnabled=false)");
            }
            finally
            {
                PolicyTestHelper.RestorePolicies(faction, snapshot);
            }
        }

        [EmpireTest("MilitaryJob")]
        public static void EnabledMilitaryJob_OverridesDefault()
        {
            var faction = GetFaction();
            if (faction == null) TestAssert.Skip("No faction");

            var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
            try
            {
                PolicyTestHelper.ClearAll(faction);

                // Authoritarian enables EnslaveEnemySettlement
                PolicyTestHelper.EnactPolicy(faction, FCPolicyDefOf.authoritarian);
                TestAssert.IsTrue(FindFC.FactionComp.IsMilitaryJobAllowed(MilitaryJobDefOf.EnslaveEnemySettlement),
                    "Authoritarian should enable Enslave via enabledMilitaryJobs");
            }
            finally
            {
                PolicyTestHelper.RestorePolicies(faction, snapshot);
            }
        }

        [EmpireTest("MilitaryJob")]
        public static void BlockedMilitaryJob_Blocks()
        {
            var faction = GetFaction();
            if (faction == null) TestAssert.Skip("No faction");

            var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
            try
            {
                PolicyTestHelper.ClearAll(faction);

                // Isolationist blocks CaptureEnemySettlement
                PolicyTestHelper.EnactPolicy(faction, FCPolicyDefOf.isolationist);
                TestAssert.IsFalse(FindFC.FactionComp.IsMilitaryJobAllowed(MilitaryJobDefOf.CaptureEnemySettlement),
                    "Isolationist should block Capture via blockedMilitaryJobs");
            }
            finally
            {
                PolicyTestHelper.RestorePolicies(faction, snapshot);
            }
        }

        [EmpireTest("MilitaryJob")]
        public static void BlockedOverridesEnabled_ForJobs()
        {
            var faction = GetFaction();
            if (faction == null) TestAssert.Skip("No faction");

            // Isolationist blocks Capture (a default-enabled job). Verify it stays blocked
            // even though Capture's defaultEnabled is true — the blockedMilitaryJobs list
            // takes precedence via _cachedEnabledJobs.ExceptWith(_cachedBlockedJobs).
            var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
            try
            {
                PolicyTestHelper.ClearAll(faction);

                // Without isolationist, Capture is allowed
                TestAssert.IsTrue(FindFC.FactionComp.IsMilitaryJobAllowed(MilitaryJobDefOf.CaptureEnemySettlement),
                    "Capture should be allowed without isolationist");

                // With isolationist, Capture is blocked
                PolicyTestHelper.EnactPolicy(faction, FCPolicyDefOf.isolationist);
                TestAssert.IsFalse(FindFC.FactionComp.IsMilitaryJobAllowed(MilitaryJobDefOf.CaptureEnemySettlement),
                    "Capture should be blocked by isolationist");

                // Clear and verify it's allowed again
                PolicyTestHelper.ClearAll(faction);
                TestAssert.IsTrue(FindFC.FactionComp.IsMilitaryJobAllowed(MilitaryJobDefOf.CaptureEnemySettlement),
                    "Capture should be allowed again after clearing isolationist");
            }
            finally
            {
                PolicyTestHelper.RestorePolicies(faction, snapshot);
            }
        }

        // ============================
        // Integration tests
        // ============================

        [EmpireTest("MilitaryJob")]
        public static void Pacifist_BlocksActionNotJobs()
        {
            var faction = GetFaction();
            if (faction == null) TestAssert.Skip("No faction");

            var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
            try
            {
                PolicyTestHelper.ClearAll(faction);
                PolicyTestHelper.EnactPolicy(faction, FCPolicyDefOf.pacifist);

                // Pacifist blocks the DeployMilitary action
                TestAssert.IsFalse(FindFC.FactionComp.IsActionAllowed(FactionColonies.util.FCActionType.DeployMilitary),
                    "Pacifist should block DeployMilitary action");

                // But individual military jobs are NOT blocked at the job level
                TestAssert.IsTrue(FindFC.FactionComp.IsMilitaryJobAllowed(MilitaryJobDefOf.RaidEnemySettlement),
                    "Pacifist should not block Raid at the job level (blocks at action level instead)");
                TestAssert.IsTrue(FindFC.FactionComp.IsMilitaryJobAllowed(MilitaryJobDefOf.CaptureEnemySettlement),
                    "Pacifist should not block Capture at the job level");
            }
            finally
            {
                PolicyTestHelper.RestorePolicies(faction, snapshot);
            }
        }

        [EmpireTest("MilitaryJob")]
        public static void Authoritarian_EnablesEnslave_JobLevel()
        {
            var faction = GetFaction();
            if (faction == null) TestAssert.Skip("No faction");

            var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
            try
            {
                PolicyTestHelper.ClearAll(faction);
                PolicyTestHelper.EnactPolicy(faction, FCPolicyDefOf.authoritarian);

                // Authoritarian enables Enslave at job level
                TestAssert.IsTrue(FindFC.FactionComp.IsMilitaryJobAllowed(MilitaryJobDefOf.EnslaveEnemySettlement),
                    "Authoritarian should enable Enslave at job level");

                // Does not affect the DeployMilitary action gate (authoritarian doesn't block/enable it)
                TestAssert.IsTrue(FindFC.FactionComp.IsActionAllowed(FactionColonies.util.FCActionType.DeployMilitary),
                    "Authoritarian should not affect the DeployMilitary action gate");
            }
            finally
            {
                PolicyTestHelper.RestorePolicies(faction, snapshot);
            }
        }

        [EmpireTest("MilitaryJob")]
        public static void AllXmlMilitaryJobRefs_AreValid()
        {
            foreach (FCPolicyDef def in DefDatabase<FCPolicyDef>.AllDefsListForReading)
            {
                if (def.blockedMilitaryJobs != null)
                {
                    for (int i = 0; i < def.blockedMilitaryJobs.Count; i++)
                    {
                        TestAssert.IsNotNull(def.blockedMilitaryJobs[i],
                            $"{def.defName}: blockedMilitaryJobs[{i}] resolved to null (bad defName in XML?)");
                    }
                }
                if (def.enabledMilitaryJobs != null)
                {
                    for (int i = 0; i < def.enabledMilitaryJobs.Count; i++)
                    {
                        TestAssert.IsNotNull(def.enabledMilitaryJobs[i],
                            $"{def.defName}: enabledMilitaryJobs[{i}] resolved to null (bad defName in XML?)");
                    }
                }
            }
        }
    }
}
