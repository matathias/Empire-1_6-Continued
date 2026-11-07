namespace FactionColonies;

/* Regression cover for the no-home-map guard in WorldObjectComp_SettlementPrisoners.
   ReturnPrisonerToPlayer (and the sibling raid-loot / defense-teardown paths) dereferenced
   Find.AnyPlayerHomeMap.Tile with no null check, so a nomad/caravan-phase player with no home
   colony map hit an NRE. The guard now returns and retains the prisoner instead.

   The bug only manifests when there is NO player home map, so the test briefly detaches every map
   from the game's map list around the call (Find.AnyPlayerHomeMap then returns null; with Odyssey a
   non-home map holding a grav engine would also count, so all maps go) and parks currentMapIndex at
   -1 so Find.CurrentMap returns null instead of indexing past the emptied list. Both are put back
   exactly as they were in the finally block. It is on the destructive tier because it generates a
   pawn, mutates a live settlement's prisoner list, and hides the maps; all three are undone in the
   finally block. */
public static class PrisonerReturnDestructiveTests
{
    [EmpireDestructiveTest("Destructive.Military")]
    public static void ReturnPrisonerToPlayer_NoHomeMap_RetainsPrisonerNoThrow()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        Faction empire = DestructiveTestUtil.EnsureEmpireFaction();

        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();
        WorldObjectComp_SettlementPrisoners comp = TestAssert.NotNull(
            settlement.GetComponent<WorldObjectComp_SettlementPrisoners>(),
            $"Settlement '{settlement.Name}' has no prisoners comp");

        List<Map> maps = Current.Game.Maps;
        List<Map> savedMaps = [.. maps];
        sbyte savedMapIndex = Current.Game.currentMapIndex;
        Pawn pawn = null;
        FCPrisoner prisoner = null;
        try
        {
            pawn = PaymentUtil.GeneratePrisoner(empire);
            prisoner = new FCPrisoner(pawn, settlement);
            comp.prisonerList.Add(prisoner);

            maps.Clear();
            Current.Game.currentMapIndex = -1;
            TestAssert.IsNull(Find.AnyPlayerHomeMap, "Every map should be detached for the test");

            TestAssert.DoesNotThrow(() => comp.ReturnPrisonerToPlayer(prisoner),
                "ReturnPrisonerToPlayer must not throw when there is no player home map");
            TestAssert.Contains(comp.prisonerList, prisoner,
                "the prisoner must be retained (not delivered) when there is no home map");
        }
        finally
        {
            maps.Clear();
            maps.AddRange(savedMaps);
            Current.Game.currentMapIndex = savedMapIndex;
            if (prisoner is not null) comp.prisonerList.Remove(prisoner);
            if (pawn is not null && !pawn.Destroyed) pawn.Destroy();
        }

        DestructiveTestUtil.AssertEmpireInvariants(f, "ReturnPrisonerToPlayer_NoHomeMap_RetainsPrisonerNoThrow");
    }
}
