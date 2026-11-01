using System.Linq;
using Verse;

namespace FactionColonies
{
    /* DESTRUCTIVE: creates and destroys real player settlements on the live world. No cleanup —
       created settlements are removed only where removal is itself the code-under-test. See
       EmpireDestructiveTestAttribute. */
    public static class SettlementLifecycleDestructiveTests
    {
        [EmpireDestructiveTest("Destructive.Settlement")]
        public static void Create_TransientSettlement_AddsToFactionAndWorld()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile found for a new settlement");

            TestAssert.IsTrue(f.settlements.Contains(s), "New settlement should be in faction.settlements");
            TestAssert.IsTrue(Find.WorldObjects.Contains(s), "New settlement should be a registered world object");
            DestructiveTestUtil.AssertEmpireInvariants(f, "Create_TransientSettlement");
        }

        [EmpireDestructiveTest("Destructive.Settlement")]
        public static void Remove_TransientSettlement_FullTeardown()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile found for a new settlement");

            DestructiveTestUtil.SafeRemoveSettlement(s);

            TestAssert.IsFalse(f.settlements.Contains(s), "Removed settlement should be gone from faction.settlements");
            TestAssert.IsFalse(Find.WorldObjects.Contains(s), "Removed settlement should be gone from world objects");
            if (FindFC.TaxLedger?.Bills != null)
                TestAssert.IsFalse(FindFC.TaxLedger.Bills.Any(b => b?.settlement == s),
                    "No tax bill should reference the removed settlement");
            if (FindFC.MilitaryManager?.Active != null)
                TestAssert.IsFalse(FindFC.MilitaryManager.Active.Any(op =>
                        op != null && (op.aggressor?.homeSettlement == s || op.defender?.homeSettlement == s || op.targetObject == s)),
                    "No active op should reference the removed settlement");
            DestructiveTestUtil.AssertEmpireInvariants(f, "Remove_TransientSettlement");
        }

        [EmpireDestructiveTest("Destructive.Settlement")]
        public static void CreateRemove_Repeated_x5()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            int baseline = f.settlements.Count;

            for (int i = 0; i < 5; i++)
            {
                WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
                if (s is null) TestAssert.Skip($"No valid tile found on cycle {i}");
                TestAssert.AreEqual(baseline + 1, f.settlements.Count, $"Cycle {i}: count should rise by 1");
                DestructiveTestUtil.SafeRemoveSettlement(s);
                TestAssert.AreEqual(baseline, f.settlements.Count, $"Cycle {i}: count should return to baseline");
                DestructiveTestUtil.AssertEmpireInvariants(f, $"CreateRemove cycle {i}");
            }
        }

        [EmpireDestructiveTest("Destructive.Settlement")]
        public static void RemoveSettlement_SetCapitalSurvives()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            if (f.settlements.Count == 0) TestAssert.Skip("No settlements to remove");

            // NOTE: the faction "capital" (capitalLocation) is a player-colony tile set by the Capital
            // Spot building — it is never an Empire settlement tile, so there is no capital-settlement to
            // single out here. This exercises removing an ordinary settlement and confirms the capital
            // re-selection path (SetCapital) stays resilient afterward.
            WorldSettlementFC target = f.settlements.First();

            // Create a throwaway spare first so removal doesn't necessarily drop us to zero settlements
            // (reduces crash surface while still exercising the teardown path).
            DestructiveTestUtil.CreateTransientSettlement();

            DestructiveTestUtil.SafeRemoveSettlement(target);
            TestAssert.IsFalse(f.settlements.Contains(target), "Settlement should be gone after removal");
            TestAssert.DoesNotThrow(() => f.SetCapital(), "SetCapital should not throw after a settlement removal");
            DestructiveTestUtil.AssertEmpireInvariants(f, "RemoveSettlement_SetCapitalSurvives");
        }
    }
}
