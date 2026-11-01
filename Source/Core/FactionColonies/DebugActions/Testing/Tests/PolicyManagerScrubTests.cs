using System.Collections.Generic;
using FactionColonies.util;

namespace FactionColonies
{
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
    /* A policy/edict whose def failed to resolve (removed content) must be pruned on
     * load, mirroring the event manager's null-def scrub. factionTraits uses fixed
     * slots seeded with FCPolicyDefOf.empty and must NOT be pruned. Exercises the pure
     * PolicyManager.ScrubDeadPolicies helper so no live faction state is mutated.
     *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
    public static class PolicyManagerScrubTests
    {
        [EmpireTest("Settlement")]
        public static void ScrubDeadPolicies_RemovesNullDefPolicy_KeepsValid()
        {
            var mgr = new PolicyManager();
            var valid = new FCPolicy { def = FCPolicyDefOf.empty };
            var dead = new FCPolicy { def = null };
            mgr.policies = new List<FCPolicy> { valid, dead };

            mgr.ScrubDeadPolicies();

            TestAssert.AreEqual(1, mgr.policies.Count, "the null-def policy should be removed");
            TestAssert.Contains(mgr.policies, valid, "the resolved policy should survive the scrub");
        }

        [EmpireTest("Settlement")]
        public static void ScrubDeadPolicies_RemovesNullDefEdict()
        {
            var mgr = new PolicyManager();
            mgr.edicts = new Dictionary<FCPolicyCategory, FCPolicy>
            {
                { FCPolicyCategory.Social, new FCPolicy { def = null } },
                { FCPolicyCategory.Tax, new FCPolicy { def = FCPolicyDefOf.empty } }
            };

            mgr.ScrubDeadPolicies();

            TestAssert.IsFalse(mgr.edicts.ContainsKey(FCPolicyCategory.Social),
                "the null-def edict should be removed");
            TestAssert.IsTrue(mgr.edicts.ContainsKey(FCPolicyCategory.Tax),
                "the resolved edict should survive the scrub");
        }

        [EmpireTest("Settlement")]
        public static void ScrubDeadPolicies_LeavesFactionTraitsUntouched()
        {
            // Default traits are five FCPolicyDefOf.empty slots; the scrub must not shrink them.
            var mgr = new PolicyManager();
            int before = mgr.factionTraits.Count;

            mgr.ScrubDeadPolicies();

            TestAssert.AreEqual(before, mgr.factionTraits.Count,
                "empty trait slots must not be pruned");
        }
    }
}
