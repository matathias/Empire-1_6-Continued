namespace FactionColonies;

/* DESTRUCTIVE: replaces the live core policy set and creates and removes a settlement to check the
 * window logic that depends on them. Not reverted. */
public static class WindowsDestructiveTests
{
    /* Exposes the protected Confirm action of the policy window. */
    private sealed class ProbePolicyWindow : FactionCustomizePoliciesWindowFC
    {
        public ProbePolicyWindow(FactionFC faction) : base(faction) { }

        public void ProbeApply() => ApplySelection();
    }

    /* Up to count core policies that are mutually compatible, in def order. */
    private static List<FCPolicyDef> CompatibleCorePolicies(int count)
    {
        List<FCPolicyDef> picked = [];
        foreach (FCPolicyDef def in FactionCache.GetPoliciesByCategory(FCPolicyCategory.Core))
        {
            if (picked.Count >= count) break;
            bool clash = picked.Any(p => (p.incompatiblePolicies?.Contains(def) ?? false)
                || (def.incompatiblePolicies?.Contains(p) ?? false));
            if (!clash) picked.Add(def);
        }
        return picked;
    }

    /* Replaces the live core policies with fresh ones for defs and rebuilds the behavior cache. */
    private static List<FCPolicy> SetPolicies(List<FCPolicyDef> defs)
    {
        PolicyManager pm = FindFC.PolicyManager;
        pm.RemoveAllPolicies(pm.policies);
        foreach (FCPolicyDef def in defs)
            pm.policies.Add(new FCPolicy(def));
        pm.RebuildBehaviorCache();
        return [.. pm.policies];
    }

    private static void AssertSamePolicies(List<FCPolicy> expected, string context)
    {
        List<FCPolicy> live = FindFC.PolicyManager.policies;
        TestAssert.AreEqual(expected.Count, live.Count, $"{context}: policy count");
        for (int i = 0; i < expected.Count; i++)
            TestAssert.IsTrue(ReferenceEquals(expected[i], live[i]), $"{context}: policy {i} ({expected[i].def?.defName}) should be the same instance");
    }

    [EmpireDestructiveTest("Destructive.Windows")]
    public static void PolicyWindow_PartialSet_OpeningDoesNotRemovePolicies()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        int max = FCSettings.maxPolicyCount;
        if (max < 2) TestAssert.Fail($"Needs room for a partial policy set; maxPolicyCount is {max}");

        List<FCPolicyDef> defs = CompatibleCorePolicies(max - 1);
        if (defs.Count == 0) TestAssert.Fail("No core policy defs found");
        List<FCPolicy> before = SetPolicies(defs);

        ProbePolicyWindow window = null;
        TestAssert.DoesNotThrow(() => window = new ProbePolicyWindow(f), "Constructing the policy window threw");
        AssertSamePolicies(before, "After constructing the window");

        // Confirming the unchanged draft keeps the enacted policies instead of recreating them.
        TestAssert.DoesNotThrow(() => window.ProbeApply(), "Confirming the policy window threw");
        AssertSamePolicies(before, "After confirming an unchanged partial set");
        DestructiveTestUtil.AssertEmpireInvariants(f, "PolicyWindow_PartialSet_OpeningDoesNotRemovePolicies");
    }

    [EmpireDestructiveTest("Destructive.Windows")]
    public static void PolicyWindow_FullSet_ConfirmChangesNothing()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        int max = FCSettings.maxPolicyCount;
        List<FCPolicyDef> defs = CompatibleCorePolicies(max);
        if (defs.Count < max) TestAssert.Fail($"Only {defs.Count} compatible core policies for {max} slots");
        List<FCPolicy> before = SetPolicies(defs);

        ProbePolicyWindow window = new(f);
        AssertSamePolicies(before, "After constructing the window on a locked-in set");
        TestAssert.DoesNotThrow(() => window.ProbeApply(), "Confirming the locked-in policy window threw");
        AssertSamePolicies(before, "After confirming a locked-in set");
        DestructiveTestUtil.AssertEmpireInvariants(f, "PolicyWindow_FullSet_ConfirmChangesNothing");
    }

    [EmpireDestructiveTest("Destructive.Windows")]
    public static void SettlementStillActive_TracksSettlementRemoval()
    {
        WorldSettlementFC settlement = DestructiveTestUtil.RequireTransientSettlement();
        TestAssert.IsTrue(SettlementWindowFc.SettlementStillActive(settlement), "A live faction settlement is active");

        DestructiveTestUtil.SafeRemoveSettlement(settlement);
        TestAssert.IsFalse(SettlementWindowFc.SettlementStillActive(settlement),
            "A removed settlement is no longer active, so its windows close");
        DestructiveTestUtil.AssertEmpireInvariants(DestructiveTestUtil.RequireFaction(), "SettlementStillActive_TracksSettlementRemoval");
    }
}
