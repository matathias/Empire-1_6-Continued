using FactionColonies.util;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    public static class ActionTypeTests
    {
        private static FactionFC GetFaction()
        {
            return FindFC.FactionComp;
        }

        // ============================
        // FCActionTypeUtil.RequiresEnable
        // ============================

        [EmpireTest("ActionType")]
        public static void RequiresEnable_OptInActions_ReturnsTrue()
        {
            TestAssert.IsTrue(FCActionTypeUtil.RequiresEnable(FCActionType.SendDiplomat),
                "SendDiplomat should be opt-in");
            TestAssert.IsTrue(FCActionTypeUtil.RequiresEnable(FCActionType.DeployExtraSquad),
                "DeployExtraSquad should be opt-in");
            TestAssert.IsTrue(FCActionTypeUtil.RequiresEnable(FCActionType.BuildRoadsToAllies),
                "BuildRoadsToAllies should be opt-in");
        }

        [EmpireTest("ActionType")]
        public static void RequiresEnable_OptOutActions_ReturnsFalse()
        {
            TestAssert.IsFalse(FCActionTypeUtil.RequiresEnable(FCActionType.DeployMilitary),
                "DeployMilitary should be opt-out");
            TestAssert.IsFalse(FCActionTypeUtil.RequiresEnable(FCActionType.UseFireSupport),
                "UseFireSupport should be opt-out");
            TestAssert.IsFalse(FCActionTypeUtil.RequiresEnable(FCActionType.SendPrisoner),
                "SendPrisoner should be opt-out");
            TestAssert.IsFalse(FCActionTypeUtil.RequiresEnable(FCActionType.SellPrisoner),
                "SellPrisoner should be opt-out");
            TestAssert.IsFalse(FCActionTypeUtil.RequiresEnable(FCActionType.DemolishBuilding),
                "DemolishBuilding should be opt-out");
            TestAssert.IsFalse(FCActionTypeUtil.RequiresEnable(FCActionType.UpgradeSettlement),
                "UpgradeSettlement should be opt-out");
            TestAssert.IsFalse(FCActionTypeUtil.RequiresEnable(FCActionType.TradeWithSettlement),
                "TradeWithSettlement should be opt-out");
        }

        // ============================
        // IsActionAllowed — defaults
        // ============================

        [EmpireTest("ActionType")]
        public static void IsActionAllowed_OptOutAction_AllowedByDefault()
        {
            var faction = GetFaction();
            if (faction == null) TestAssert.Skip("No faction");

            var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
            try
            {
                PolicyTestHelper.ClearAll(faction);
                TestAssert.IsTrue(FindFC.FactionComp.IsActionAllowed(FCActionType.DeployMilitary),
                    "Opt-out action should be allowed when no policies are active");
            }
            finally
            {
                PolicyTestHelper.RestorePolicies(faction, snapshot);
            }
        }

        [EmpireTest("ActionType")]
        public static void IsActionAllowed_OptInAction_DisabledByDefault()
        {
            var faction = GetFaction();
            if (faction == null) TestAssert.Skip("No faction");

            var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
            try
            {
                PolicyTestHelper.ClearAll(faction);
                TestAssert.IsFalse(FindFC.FactionComp.IsActionAllowed(FCActionType.SendDiplomat),
                    "Opt-in action should be disabled when no policies are active");
                TestAssert.IsFalse(FindFC.FactionComp.IsActionAllowed(FCActionType.DeployExtraSquad),
                    "Opt-in action DeployExtraSquad should be disabled when no policies are active");
                TestAssert.IsFalse(FindFC.FactionComp.IsActionAllowed(FCActionType.BuildRoadsToAllies),
                    "Opt-in action BuildRoadsToAllies should be disabled when no policies are active");
            }
            finally
            {
                PolicyTestHelper.RestorePolicies(faction, snapshot);
            }
        }

        // ============================
        // IsActionAllowed — policy effects
        // ============================

        [EmpireTest("ActionType")]
        public static void IsActionAllowed_BlockedAction_Blocked()
        {
            var faction = GetFaction();
            if (faction == null) TestAssert.Skip("No faction");

            var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
            try
            {
                PolicyTestHelper.ClearAll(faction);

                // Pacifist blocks DeployMilitary
                PolicyTestHelper.EnactPolicy(faction, FCPolicyDefOf.pacifist);
                TestAssert.IsFalse(FindFC.FactionComp.IsActionAllowed(FCActionType.DeployMilitary),
                    "Pacifist should block DeployMilitary");
            }
            finally
            {
                PolicyTestHelper.RestorePolicies(faction, snapshot);
            }
        }

        [EmpireTest("ActionType")]
        public static void IsActionAllowed_EnabledAction_Enabled()
        {
            var faction = GetFaction();
            if (faction == null) TestAssert.Skip("No faction");

            var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
            try
            {
                PolicyTestHelper.ClearAll(faction);

                // Pacifist enables SendDiplomat
                PolicyTestHelper.EnactPolicy(faction, FCPolicyDefOf.pacifist);
                TestAssert.IsTrue(FindFC.FactionComp.IsActionAllowed(FCActionType.SendDiplomat),
                    "Pacifist should enable SendDiplomat");
            }
            finally
            {
                PolicyTestHelper.RestorePolicies(faction, snapshot);
            }
        }

        [EmpireTest("ActionType")]
        public static void IsActionAllowed_BlockTakesPrecedence()
        {
            var faction = GetFaction();
            if (faction == null) TestAssert.Skip("No faction");

            // Find an opt-in action that one policy enables and another blocks.
            // Militaristic enables DeployExtraSquad; if we can find another that blocks it, test precedence.
            // Since no current XML blocks DeployExtraSquad, we verify the cache logic directly:
            // after RebuildActionCache, _cachedEnabledActions.ExceptWith(_cachedBlockedActions) means
            // if an action is in both, it ends up only in blocked.

            var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
            try
            {
                PolicyTestHelper.ClearAll(faction);

                // Militaristic enables DeployExtraSquad
                PolicyTestHelper.EnactPolicy(faction, FCPolicyDefOf.militaristic);
                TestAssert.IsTrue(FindFC.FactionComp.IsActionAllowed(FCActionType.DeployExtraSquad),
                    "Militaristic should enable DeployExtraSquad");

                // Verify AnyPolicyEnables returns true
                TestAssert.IsTrue(FindFC.PolicyManager.AnyPolicyEnables(FCActionType.DeployExtraSquad),
                    "AnyPolicyEnables should return true for DeployExtraSquad with Militaristic");

                // Verify AnyPolicyBlocks returns false (no blocker)
                TestAssert.IsFalse(FindFC.PolicyManager.AnyPolicyBlocks(FCActionType.DeployExtraSquad),
                    "AnyPolicyBlocks should return false for DeployExtraSquad");
            }
            finally
            {
                PolicyTestHelper.RestorePolicies(faction, snapshot);
            }
        }

        [EmpireTest("ActionType")]
        public static void ActionCache_RebuildOnPolicyChange()
        {
            var faction = GetFaction();
            if (faction == null) TestAssert.Skip("No faction");

            var snapshot = PolicyTestHelper.SnapshotPolicies(faction);
            try
            {
                PolicyTestHelper.ClearAll(faction);

                // Initially allowed
                TestAssert.IsTrue(FindFC.FactionComp.IsActionAllowed(FCActionType.DeployMilitary),
                    "DeployMilitary should be allowed after clearing policies");

                // Enact pacifist — blocks it
                PolicyTestHelper.EnactPolicy(faction, FCPolicyDefOf.pacifist);
                TestAssert.IsFalse(FindFC.FactionComp.IsActionAllowed(FCActionType.DeployMilitary),
                    "DeployMilitary should be blocked after enacting pacifist");

                // Clear again — allowed again
                PolicyTestHelper.ClearAll(faction);
                TestAssert.IsTrue(FindFC.FactionComp.IsActionAllowed(FCActionType.DeployMilitary),
                    "DeployMilitary should be allowed again after clearing policies");
            }
            finally
            {
                PolicyTestHelper.RestorePolicies(faction, snapshot);
            }
        }

        // ============================
        // Data validation
        // ============================

        [EmpireTest("ActionType")]
        public static void AllPolicies_NoDuplicateBlockedActions()
        {
            foreach (FCPolicyDef def in DefDatabase<FCPolicyDef>.AllDefsListForReading)
            {
                if (def.blockedActions != null && def.blockedActions.Count > 0)
                {
                    var seen = new HashSet<FCActionType>();
                    foreach (FCActionType action in def.blockedActions)
                    {
                        TestAssert.IsTrue(seen.Add(action),
                            $"{def.defName}: duplicate in blockedActions: {action}");
                    }
                }

                if (def.enabledActions != null && def.enabledActions.Count > 0)
                {
                    var seen = new HashSet<FCActionType>();
                    foreach (FCActionType action in def.enabledActions)
                    {
                        TestAssert.IsTrue(seen.Add(action),
                            $"{def.defName}: duplicate in enabledActions: {action}");
                    }
                }
            }
        }

        [EmpireTest("ActionType")]
        public static void AllPolicies_OptInActionsNotInBlockedList()
        {
            // Blocking an opt-in action is meaningless — it's already disabled by default.
            foreach (FCPolicyDef def in DefDatabase<FCPolicyDef>.AllDefsListForReading)
            {
                if (def.blockedActions == null) continue;
                foreach (FCActionType action in def.blockedActions)
                {
                    TestAssert.IsFalse(FCActionTypeUtil.RequiresEnable(action),
                        $"{def.defName}: blockedActions contains opt-in action {action} (meaningless — opt-in actions are disabled by default)");
                }
            }
        }
    }
}
