using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FactionColonies
{
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
    /* SetPawnGroupMakers must give each pawnGroupMaker list its OWN PawnGenOption
     * instance. A shared instance across lists lets the per-list reweight
     * (raceWeight / optionsInThatList) clobber the weight for every other list it was
     * added to, over-producing civilians in the Settlement group.
     *
     * Read-only invariant check over the live faction's populated pawnGroupMakers:
     * no PawnGenOption reference may appear in more than one of the lists that
     * SetPawnGroupMakers writes to. Non-destructive; mutates nothing.
     *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
    public static class XenotypeFilterReweightTests
    {
        [EmpireTest("Xenotype")]
        public static void PawnGroupMakers_NoSharedOptionInstanceAcrossLists()
        {
            FactionDef def = FindFC.EmpireFactionDef;
            if (def?.pawnGroupMakers == null || def.pawnGroupMakers.Count < 4)
                TestAssert.Skip("Empire faction pawnGroupMakers not populated");

            // The lists SetPawnGroupMakers populates and later reweights per-list.
            var lists = new List<List<PawnGenOption>>
            {
                def.pawnGroupMakers[0].options, // Combat
                def.pawnGroupMakers[1].options, // Trader
                def.pawnGroupMakers[1].guards,  // Trader guards
                def.pawnGroupMakers[1].traders, // Traders
                def.pawnGroupMakers[2].options, // Settlement
                def.pawnGroupMakers[3].options, // Peaceful
            };

            // Reference-identity set (PawnGenOption has no value Equals override).
            var seen = new HashSet<PawnGenOption>();
            int total = 0;
            foreach (List<PawnGenOption> list in lists)
            {
                if (list == null) continue;
                foreach (PawnGenOption op in list)
                {
                    total++;
                    seen.Add(op);
                }
            }

            if (total == 0) TestAssert.Skip("No pawn group options populated yet");

            TestAssert.AreEqual(total, seen.Count,
                "each pawnGroupMaker list must hold its own PawnGenOption instances " +
                "(a shared reference means per-list reweighting cross-contaminates)");
        }
    }
}
