using RimWorld;
using Verse;

namespace FactionColonies
{
    /* DESTRUCTIVE: mutates faction-wide state — pumps XP/level, spikes settlement morale, resets
       the capital. Not reverted. */
    public static class FactionStateDestructiveTests
    {
        [EmpireDestructiveTest("Destructive.Faction")]
        public static void AddExperience_CapsAtMax()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            TestAssert.DoesNotThrow(() => f.AddExperienceToFactionLevel(1000000f), "AddExperienceToFactionLevel threw");
            TestAssert.LessThanOrEqual(f.factionLevel, FactionFC.MaxFactionLevel,
                $"factionLevel should never exceed MaxFactionLevel ({FactionFC.MaxFactionLevel}), got {f.factionLevel}");
            DestructiveTestUtil.AssertEmpireInvariants(f, "AddExperience_CapsAtMax");
        }

        [EmpireDestructiveTest("Destructive.Faction")]
        public static void GainHappiness_StaysClamped()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            if (f.settlements.Count == 0) TestAssert.Skip("No settlements");

            TestAssert.DoesNotThrow(() => f.GainHappiness(100000), "GainHappiness(+) threw");
            TestAssert.DoesNotThrow(() => f.GainHappiness(-100000), "GainHappiness(-) threw");
            foreach (WorldSettlementFC s in f.settlements)
            {
                TestAssert.IsTrue(s.happiness >= 0 && s.happiness <= 100,
                    $"{s.Name}: happiness should stay in [0,100], got {s.happiness}");
            }
            DestructiveTestUtil.AssertEmpireInvariants(f, "GainHappiness_StaysClamped");
        }

        [EmpireDestructiveTest("Destructive.Faction")]
        public static void GainUnrest_StaysClamped()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            if (f.settlements.Count == 0) TestAssert.Skip("No settlements");

            var msg = new Message("Empire destructive test", MessageTypeDefOf.NeutralEvent);
            TestAssert.DoesNotThrow(() => f.GainUnrestForReason(msg, 100000), "GainUnrestForReason threw");
            foreach (WorldSettlementFC s in f.settlements)
            {
                TestAssert.IsTrue(s.unrest >= 0 && s.unrest <= 100,
                    $"{s.Name}: unrest should stay in [0,100], got {s.unrest}");
            }
            DestructiveTestUtil.AssertEmpireInvariants(f, "GainUnrest_StaysClamped");
        }

        [EmpireDestructiveTest("Destructive.Faction")]
        public static void SetCapital_NoCrash()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            TestAssert.DoesNotThrow(() => f.SetCapital(), "SetCapital threw");
            DestructiveTestUtil.AssertEmpireInvariants(f, "SetCapital_NoCrash");
        }
    }
}
