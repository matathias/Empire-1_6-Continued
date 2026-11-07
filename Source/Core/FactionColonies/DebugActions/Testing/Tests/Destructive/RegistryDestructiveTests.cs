namespace FactionColonies;

/* DESTRUCTIVE: temporarily unregisters live registry participants (re-registered afterwards) and
   may create a real player settlement to pass to registry hooks. Settlements are not removed. */
public static class RegistryDestructiveTests
{
    [EmpireDestructiveTest("Destructive.Registry")]
    public static void RaidWeight_NoProviders_ReturnsOne()
    {
        // Live submods (e.g. VOE) may register providers; take them out so the identity weight is visible.
        List<IRaidWeightProvider> live = RaidWeightRegistry.Providers.ToList();
        foreach (IRaidWeightProvider p in live) RaidWeightRegistry.Unregister(p);
        try
        {
            TestAssert.AreEqual(0, RaidWeightRegistry.Providers.Count, "All providers should be unregistered");
            TestAssert.AreEqual(1.0, RaidWeightRegistry.GetCombinedWeight(null, null), 0.0001,
                "Combined weight with no providers should be the identity 1.0");
        }
        finally
        {
            foreach (IRaidWeightProvider p in live) RaidWeightRegistry.Register(p);
        }
    }

    [EmpireDestructiveTest("Destructive.Registry")]
    public static void AutoDefender_FindBestDefender_NoDefenders_ReturnsNull()
    {
        // Live submods (e.g. VOE outposts) may register defenders; take them out for the empty case.
        List<IAutoDefender> live = AutoDefenderRegistry.Defenders.ToList();
        foreach (IAutoDefender d in live) AutoDefenderRegistry.Unregister(d);
        try
        {
            TestAssert.AreEqual(0, AutoDefenderRegistry.Defenders.Count, "All defenders should be unregistered");
            TestAssert.IsNull(AutoDefenderRegistry.FindBestDefender(RimWorld.Planet.PlanetTile.Invalid, 0),
                "FindBestDefender should return null with no defenders registered");
        }
        finally
        {
            foreach (IAutoDefender d in live) AutoDefenderRegistry.Register(d);
        }
    }

    [EmpireDestructiveTest("Destructive.Registry")]
    public static void DefaultMembers_BareParticipant_RegisteredChangesNothing()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();

        PawnKindDef animal = DefDatabase<PawnKindDef>.AllDefs.FirstOrDefault(k => k.RaceProps?.Animal ?? false);

        double additiveBefore = ThreatScalingRegistry.InvokeGetAdditiveContributions(faction);
        double multBefore = ThreatScalingRegistry.InvokeGetMultiplierContributions(faction);
        bool canDefendBefore = DefenseValidatorRegistry.CanDefend(settlement, settlement);
        bool animalBefore = AnimalPickerFilterRegistry.IsAllowed(animal);
        int costLinesBefore = FoundingValidatorRegistry.GetCostDescriptions(settlement.Tile, settlement.settlementDef).Count;
        HashSet<int> roadTilesBefore = [];
        RoadNodeProviderRegistry.CollectInto(roadTilesBefore);

        var bare = new RegistryTests.BareParticipant();
        EmpireRegistry.Register(bare);
        PsycastSystemRegistry.Register(bare);
        try
        {
            // Identity-filtered views skip an implementer that left WorldObject/Tile/Key at their defaults.
            TestAssert.IsFalse(RaidTargetRegistry.Targets.Contains(bare), "RaidTargetRegistry skips a target with no WorldObject");
            TestAssert.IsFalse(AutoDefenderRegistry.Defenders.Contains(bare), "AutoDefenderRegistry skips a defender with no WorldObject");
            TestAssert.IsFalse(MilitaryTabRegistry.Entries.Contains(bare), "MilitaryTabRegistry skips an entry with no WorldObject");
            TestAssert.IsFalse(PsycastSystemRegistry.Providers.Contains(bare), "PsycastSystemRegistry rejects a provider with no Key");
            TestAssert.IsNull(RaidTargetRegistry.FindByWorldObject(null), "FindByWorldObject(null)");

            // Aggregating registries produce the same result with the bare implementer present.
            TestAssert.AreEqual(additiveBefore, ThreatScalingRegistry.InvokeGetAdditiveContributions(faction), message: "ETL additive");
            TestAssert.AreEqual(multBefore, ThreatScalingRegistry.InvokeGetMultiplierContributions(faction), message: "ETL multiplier");
            TestAssert.AreEqual(canDefendBefore, DefenseValidatorRegistry.CanDefend(settlement, settlement), "CanDefend");
            TestAssert.AreEqual(animalBefore, AnimalPickerFilterRegistry.IsAllowed(animal), "IsAllowed");
            TestAssert.AreEqual(costLinesBefore, FoundingValidatorRegistry.GetCostDescriptions(settlement.Tile, settlement.settlementDef).Count, "founding cost lines");
            HashSet<int> roadTilesAfter = [];
            RoadNodeProviderRegistry.CollectInto(roadTilesAfter);
            TestAssert.IsTrue(roadTilesBefore.SetEquals(roadTilesAfter), "road node tiles");

            TestAssert.DoesNotThrow(MainTableRegistry.InvokePostCloseWindow, "InvokePostCloseWindow");
        }
        finally
        {
            EmpireRegistry.Unregister(bare);
            PsycastSystemRegistry.Unregister(bare);
        }
        DestructiveTestUtil.AssertEmpireInvariants(faction, "DefaultMembers_BareParticipant_RegisteredChangesNothing");
    }
}
