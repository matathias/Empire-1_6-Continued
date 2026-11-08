namespace FactionColonies;

/* DESTRUCTIVE: calls EmpireRegistry.ClearAll, which empties every facade-managed registry. Live
   participants registered with RegisterPersistent come back; the rest (FactionFC listeners,
   built-in validators, submod hooks registered per game) stay gone until the save is reloaded.
   Also generates and discards one throwaway pawn. */
public static class RegistryPersistenceDestructiveTests
{
    private class Participant : ITaxTickParticipant, IDefenseValidator, IRaidWeightProvider { }

    private static int TaxTickCount(object p) => TaxTickRegistry.Taxers.Count(x => ReferenceEquals(x, p));
    private static int DefenseCount(object p) => DefenseValidatorRegistry.Validators.Count(x => ReferenceEquals(x, p));
    private static int RaidWeightCount(object p) => RaidWeightRegistry.Providers.Count(x => ReferenceEquals(x, p));

    [EmpireDestructiveTest("Destructive.Registry")]
    public static void RegisterPersistent_RepeatedRegistrationAndClears_StayRegisteredOnce()
    {
        Participant p = new();
        try
        {
            EmpireRegistry.RegisterPersistent(p);
            EmpireRegistry.RegisterPersistent(p);
            TestAssert.AreEqual(1, TaxTickCount(p), "Registering persistently twice registers once");

            EmpireRegistry.ClearAll();
            EmpireRegistry.ClearAll();
            TestAssert.AreEqual(1, TaxTickCount(p), "TaxTick: back exactly once after repeated ClearAll");
            TestAssert.AreEqual(1, DefenseCount(p), "DefenseValidator: back exactly once after repeated ClearAll");
            TestAssert.AreEqual(1, RaidWeightCount(p), "RaidWeight: back exactly once after repeated ClearAll");
        }
        finally { EmpireRegistry.Unregister(p); }

        TestAssert.AreEqual(0, TaxTickCount(p), "Unregister removes it now");
        EmpireRegistry.ClearAll();
        TestAssert.AreEqual(0, TaxTickCount(p), "and it does not come back on the next ClearAll");
    }

    [EmpireDestructiveTest("Destructive.Registry")]
    public static void RegisterPersistent_OnlyPersistentParticipantsSurviveClearAll()
    {
        Participant persistent = new();
        Participant perGame = new();
        try
        {
            EmpireRegistry.RegisterPersistent(persistent);
            EmpireRegistry.Register(perGame);
            EmpireRegistry.ClearAll();

            TestAssert.AreEqual(1, TaxTickCount(persistent), "A persistent participant is re-registered");
            TestAssert.AreEqual(0, TaxTickCount(perGame), "A plain registration is cleared");

            // A later plain Register of a persistent participant must not double it up.
            EmpireRegistry.Register(persistent);
            EmpireRegistry.ClearAll();
            TestAssert.AreEqual(1, TaxTickCount(persistent), "Still registered once");
        }
        finally
        {
            EmpireRegistry.Unregister(persistent);
            EmpireRegistry.Unregister(perGame);
        }
    }

    [EmpireDestructiveTest("Destructive.Registry")]
    public static void RegisterPersistent_Null_IsIgnored()
    {
        TestAssert.DoesNotThrow(() =>
        {
            EmpireRegistry.RegisterPersistent(null);
            EmpireRegistry.ClearAll();
        }, "A null persistent participant is ignored, also on ClearAll");
    }

    [EmpireDestructiveTest("Destructive.Registry")]
    public static void DiscardPreviewPawn_DestroysPawnAndGear()
    {
        Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,
            faction: null, forceGenerateNewPawn: true, canGeneratePawnRelations: false));
        TestAssert.NotNull(pawn, "Pawn generation");
        List<Thing> gear = [];
        if (pawn.apparel is not null) gear.AddRange(pawn.apparel.WornApparel);
        if (pawn.equipment is not null) gear.AddRange(pawn.equipment.AllEquipmentListForReading);

        PawnUtil.DiscardPreviewPawn(pawn, "test");

        TestAssert.IsTrue(pawn.Destroyed, "The pawn is destroyed");
        foreach (Thing thing in gear)
            TestAssert.IsTrue(thing.Destroyed, $"Gear {thing.LabelShort} is destroyed with the pawn");
        TestAssert.DoesNotThrow(() => PawnUtil.DiscardPreviewPawn(pawn, "test"), "Discarding twice is harmless");
    }
}
