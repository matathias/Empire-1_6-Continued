namespace FactionColonies;

/* DESTRUCTIVE: ResetSessionState wipes the live session's whole wiped-caravan set, including any
   genuine entries recorded earlier in the session. Not restored. */
public static class EmpireDeathPenaltyUtilDestructiveTests
{
    // ResetSessionState drops wiped-caravan loadIDs so they don't leak across saves.
    [EmpireDestructiveTest("Destructive.DeathPenalty")]
    public static void ResetSessionState_ClearsWipedCaravanLords()
    {
        EmpireDeathPenaltyUtil.MarkCaravanWipedForTest(42);
        TestAssert.IsTrue(EmpireDeathPenaltyUtil.IsCaravanWiped(42), "seeded loadID is recorded");

        EmpireDeathPenaltyUtil.ResetSessionState();

        TestAssert.IsFalse(EmpireDeathPenaltyUtil.IsCaravanWiped(42),
            "ResetSessionState must clear the wiped-caravan set so a colliding loadID in the next save isn't skipped");
        TestAssert.AreEqual(0, EmpireDeathPenaltyUtil.WipedCaravanCountForTest,
            "ResetSessionState must leave the wiped-caravan set empty");
    }
}
