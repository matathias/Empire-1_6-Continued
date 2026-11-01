using System.Linq;
using Verse;

namespace FactionColonies
{
    /* DESTRUCTIVE: enacts and revokes real edicts on the live policy manager, and exercises the
       tax-cycle edict-upkeep deduction (which can RevokeAllEdicts when upkeep goes unpaid). Not reverted. */
    public static class PoliciesDestructiveTests
    {
        /// <summary>Enacts the first edict def that actually sticks (respecting locks, level, and
        /// incompatibilities). Returns the enacted def, or null if none could be enacted.</summary>
        private static FCPolicyDef EnactFirstAvailableEdict()
        {
            foreach (FCPolicyDef d in DefDatabase<FCPolicyDef>.AllDefsListForReading.Where(p => p.IsEdict))
            {
                if (FindFC.PolicyManager.HasEdict(d)) continue;
                FindFC.PolicyManager.EnactEdict(d);
                if (FindFC.PolicyManager.HasEdict(d)) return d;
            }
            return null;
        }

        [EmpireDestructiveTest("Destructive.Policy")]
        public static void EnactThenRevokeEdict_RoundTrips()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            FCPolicyDef enacted = null;
            TestAssert.DoesNotThrow(() => enacted = EnactFirstAvailableEdict(), "EnactEdict threw");
            if (enacted is null) TestAssert.Skip("No edict could be enacted (all locked/incompatible)");

            TestAssert.IsTrue(FindFC.PolicyManager.HasEdict(enacted), "Edict should be active after enacting");
            TestAssert.DoesNotThrow(() => FindFC.PolicyManager.RevokeEdict(enacted.category), "RevokeEdict threw");
            TestAssert.IsFalse(FindFC.PolicyManager.HasEdict(enacted), "Edict should be gone after revoking");
            DestructiveTestUtil.AssertEmpireInvariants(f, "EnactThenRevokeEdict_RoundTrips");
        }

        [EmpireDestructiveTest("Destructive.Policy")]
        public static void RevokeAllEdicts_ClearsUpkeep()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            // Best-effort: enact whatever edicts will stick across the available categories.
            TestAssert.DoesNotThrow(() =>
            {
                foreach (FCPolicyDef d in DefDatabase<FCPolicyDef>.AllDefsListForReading.Where(p => p.IsEdict).Take(8))
                {
                    if (!FindFC.PolicyManager.HasEdict(d)) FindFC.PolicyManager.EnactEdict(d);
                }
            }, "EnactEdict loop threw");

            TestAssert.DoesNotThrow(() => FindFC.PolicyManager.RevokeAllEdicts(), "RevokeAllEdicts threw");
            TestAssert.AreEqual(0, FindFC.PolicyManager.GetEdictUpkeep(), "Edict upkeep should be 0 after RevokeAllEdicts");
            TestAssert.DoesNotThrow(() => FindFC.PolicyManager.RebuildBehaviorCache(), "RebuildBehaviorCache threw");
            DestructiveTestUtil.AssertEmpireInvariants(f, "RevokeAllEdicts_ClearsUpkeep");
        }

        [EmpireDestructiveTest("Destructive.Policy")]
        public static void AddTax_WithEdictUpkeep_NoCrash()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            if (FindFC.EmpireFaction is null) TestAssert.Skip("No Empire faction");

            // Enacting an edict means AddTax exercises the upkeep-deduction branch, which on a
            // shortfall calls RevokeAllEdicts. We assert the cycle survives, not the specific outcome.
            TestAssert.DoesNotThrow(() => EnactFirstAvailableEdict(), "EnactEdict threw");
            TestAssert.DoesNotThrow(() => FindFC.TaxLedger.AddTax(f), "AddTax (with edict upkeep) threw");
            TestAssert.DoesNotThrow(() => FindFC.PolicyManager.RebuildBehaviorCache(), "RebuildBehaviorCache threw");
            DestructiveTestUtil.AssertEmpireInvariants(f, "AddTax_WithEdictUpkeep_NoCrash");
        }
    }
}
