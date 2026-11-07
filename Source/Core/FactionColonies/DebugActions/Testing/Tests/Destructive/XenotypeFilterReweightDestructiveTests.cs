namespace FactionColonies;

/*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
/* SetPawnGroupMakers must give each pawnGroupMaker list its OWN PawnGenOption
 * instance. A shared instance across lists lets the per-list reweight
 * (raceWeight / optionsInThatList) clobber the weight for every other list it was
 * added to, over-producing civilians in the Settlement group.
 *
 * DESTRUCTIVE: rebuilds the live empire faction def's pawnGroupMakers through the
 * real refresh path, then checks that no PawnGenOption reference appears in more
 * than one of the lists SetPawnGroupMakers writes to.
 *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
public static class XenotypeFilterReweightDestructiveTests
{
    [EmpireDestructiveTest("Destructive.Xenotype")]
    public static void PawnGroupMakers_NoSharedOptionInstanceAcrossLists()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        XenotypeFilter filter = TestAssert.NotNull(faction.xenotypeFilter, "FactionFC has no XenotypeFilter");
        TestAssert.DoesNotThrow(filter.RefreshPawnGroupMakers, "RefreshPawnGroupMakers threw");

        FactionDef def = TestAssert.NotNull(FindFC.EmpireFactionDef, "No empire FactionDef");
        TestAssert.IsTrue(def.pawnGroupMakers is { Count: >= 4 },
            $"RefreshPawnGroupMakers should build 4 pawnGroupMakers, got {def.pawnGroupMakers?.Count ?? 0}");

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
            if (list is null) continue;
            foreach (PawnGenOption op in list)
            {
                total++;
                seen.Add(op);
            }
        }

        TestAssert.GreaterThan(total, 0, "RefreshPawnGroupMakers produced no pawn group options");
        TestAssert.AreEqual(total, seen.Count,
            "each pawnGroupMaker list must hold its own PawnGenOption instances " +
            "(a shared reference means per-list reweighting cross-contaminates)");
    }
}
