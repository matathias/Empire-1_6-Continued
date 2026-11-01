using System.Linq;
using Verse;

namespace FactionColonies
{
    /* Regression cover for the no-home-map guard in WorldObjectComp_SettlementPrisoners.
       ReturnPrisonerToPlayer (and the sibling raid-loot / defense-teardown paths) dereferenced
       Find.AnyPlayerHomeMap.Tile with no null check, so a nomad/caravan-phase player with no home
       colony map hit an NRE. The guard now returns and retains the prisoner instead.

       The bug only manifests when there is NO player home map, so this test skips (instantly, before
       generating anything) whenever one exists — i.e. for essentially every normal-colony tester. It
       is on the destructive tier because it generates a pawn and mutates a live settlement's prisoner
       list; both are undone in the finally block. */
    public static class PrisonerReturnDestructiveTests
    {
        [EmpireDestructiveTest("Destructive.Military")]
        public static void ReturnPrisonerToPlayer_NoHomeMap_RetainsPrisonerNoThrow()
        {
            if (Find.AnyPlayerHomeMap is object)
                TestAssert.Skip("Player has a home map; the no-home-map guard is unreachable");

            FactionFC f = DestructiveTestUtil.RequireFaction();
            if (FindFC.EmpireFaction is null) TestAssert.Skip("No Empire faction");

            WorldSettlementFC settlement = FindFC.Settlements?.FirstOrDefault();
            if (settlement is null) TestAssert.Skip("No Empire settlement available");
            WorldObjectComp_SettlementPrisoners comp = settlement.GetComponent<WorldObjectComp_SettlementPrisoners>();
            if (comp is null) TestAssert.Skip("Settlement has no prisoners comp");

            Pawn pawn = null;
            FCPrisoner prisoner = null;
            try
            {
                pawn = PaymentUtil.GeneratePrisoner(FindFC.EmpireFaction);
                prisoner = new FCPrisoner(pawn, settlement);
                comp.prisonerList.Add(prisoner);

                TestAssert.DoesNotThrow(() => comp.ReturnPrisonerToPlayer(prisoner),
                    "ReturnPrisonerToPlayer must not throw when there is no player home map");
                TestAssert.Contains(comp.prisonerList, prisoner,
                    "the prisoner must be retained (not delivered) when there is no home map");
            }
            finally
            {
                if (prisoner != null) comp.prisonerList.Remove(prisoner);
                if (pawn != null && !pawn.Destroyed) pawn.Destroy();
            }

            DestructiveTestUtil.AssertEmpireInvariants(f, "ReturnPrisonerToPlayer_NoHomeMap_RetainsPrisonerNoThrow");
        }
    }
}
