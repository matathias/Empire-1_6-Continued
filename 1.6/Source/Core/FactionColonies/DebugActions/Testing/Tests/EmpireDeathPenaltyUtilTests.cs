using Verse;

namespace FactionColonies
{
    /* Tests for EmpireDeathPenaltyUtil's transient session state:
       - the Kill-prefix dedup marker (now keyed per pawn, so a nested Pawn.Kill can't clear the outer
         pawn's entry), and
       - ResetSessionState clearing the wiped-caravan set (whose Lord loadIDs collide across saves loaded
         in one session, which would otherwise suppress a repeated pack-animal-wipe penalty).
       Pure static-state tests. Non-destructive: each test clears only the fresh keys it adds; the
       ResetSessionState test gates itself on the live set being empty so it never clobbers real entries.
       Category "DeathPenalty". */
    public static class EmpireDeathPenaltyUtilTests
    {
        // -*- Fix 4: nested Kill must not clear the outer pawn's marker -*-

        [EmpireTest("DeathPenalty")]
        public static void KillPrefixMarker_NestedClear_KeepsOuterMarker()
        {
            var outer = new Pawn();
            var inner = new Pawn();
            try
            {
                // Outer Kill's prefix marks the outer pawn.
                EmpireDeathPenaltyUtil.MarkHandledByKillPrefix(outer);
                // A nested Pawn.Kill marks + (in its postfix) clears the inner pawn.
                EmpireDeathPenaltyUtil.MarkHandledByKillPrefix(inner);
                EmpireDeathPenaltyUtil.ClearHandledByKillPrefix(inner);

                TestAssert.IsTrue(EmpireDeathPenaltyUtil.WasHandledByKillPrefix(outer),
                    "Nested Kill clearing the inner pawn must leave the outer pawn's marker intact");
                TestAssert.IsFalse(EmpireDeathPenaltyUtil.WasHandledByKillPrefix(inner),
                    "Inner pawn's marker was cleared by its own postfix");
            }
            finally
            {
                // Clear only the keys this test added — leave the rest of the set untouched.
                EmpireDeathPenaltyUtil.ClearHandledByKillPrefix(outer);
                EmpireDeathPenaltyUtil.ClearHandledByKillPrefix(inner);
            }
        }

        [EmpireTest("DeathPenalty")]
        public static void KillPrefixMarker_ClearRemovesOnlyTarget()
        {
            var a = new Pawn();
            var b = new Pawn();
            try
            {
                EmpireDeathPenaltyUtil.MarkHandledByKillPrefix(a);
                EmpireDeathPenaltyUtil.MarkHandledByKillPrefix(b);
                EmpireDeathPenaltyUtil.ClearHandledByKillPrefix(a);

                TestAssert.IsFalse(EmpireDeathPenaltyUtil.WasHandledByKillPrefix(a), "a was cleared");
                TestAssert.IsTrue(EmpireDeathPenaltyUtil.WasHandledByKillPrefix(b), "b is untouched");
            }
            finally
            {
                EmpireDeathPenaltyUtil.ClearHandledByKillPrefix(a);
                EmpireDeathPenaltyUtil.ClearHandledByKillPrefix(b);
            }
        }

        // -*- Fix 2: ResetSessionState drops wiped-caravan loadIDs so they don't leak across saves -*-

        [EmpireTest("DeathPenalty")]
        public static void ResetSessionState_ClearsWipedCaravanLords()
        {
            // ResetSessionState wipes the whole set, so only run when it's already empty — otherwise we'd
            // clobber genuine mid-session entries and violate the non-destructive contract.
            if (EmpireDeathPenaltyUtil.WipedCaravanCountForTest != 0)
                TestAssert.Skip("Wiped-caravan set is non-empty in the live session; skipping to avoid clobbering it.");

            EmpireDeathPenaltyUtil.MarkCaravanWipedForTest(42);
            TestAssert.IsTrue(EmpireDeathPenaltyUtil.IsCaravanWiped(42), "seeded loadID is recorded");

            EmpireDeathPenaltyUtil.ResetSessionState();

            TestAssert.IsFalse(EmpireDeathPenaltyUtil.IsCaravanWiped(42),
                "ResetSessionState must clear the wiped-caravan set so a colliding loadID in the next save isn't skipped");
            // Set is now empty again — exactly as found.
        }
    }
}
