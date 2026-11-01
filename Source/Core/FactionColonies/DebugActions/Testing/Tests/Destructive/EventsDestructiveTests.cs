using System.Linq;

namespace FactionColonies
{
    /* DESTRUCTIVE: creates and processes real faction events on the live event manager. Added
       events are removed where removal is the code-under-test; ProcessEvents may resolve whatever
       is currently due (not reverted). */
    public static class EventsDestructiveTests
    {
        [EmpireDestructiveTest("Destructive.Events")]
        public static void MakeEvent_AddsToManager()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            FCEvent evt = null;
            TestAssert.DoesNotThrow(() => evt = FCEventMaker.MakeEvent(FCEventDefOf.cooldownMilitary), "MakeEvent threw");
            TestAssert.IsNotNull(evt, "MakeEvent should return an event");

            int before = FindFC.Events.Count;
            TestAssert.DoesNotThrow(() => FindFC.EventManager.AddEvent(evt), "AddEvent threw");
            TestAssert.IsTrue(FindFC.Events.Count > before || FindFC.Events.Contains(evt),
                "Event should be enqueued");

            int removed = 0;
            TestAssert.DoesNotThrow(() => removed = FindFC.EventManager.RemoveWhere(e => ReferenceEquals(e, evt)), "RemoveWhere threw");
            TestAssert.IsFalse(FindFC.Events.Contains(evt), "Event should be gone after RemoveWhere");
            DestructiveTestUtil.AssertEmpireInvariants(f, "MakeEvent_AddsToManager");
        }

        [EmpireDestructiveTest("Destructive.Events")]
        public static void ReturnRandomEvent_NoCrash()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            // Result may legitimately be null when no random event qualifies — we only assert no crash.
            TestAssert.DoesNotThrow(() => FCEventMaker.ReturnRandomEvent(), "ReturnRandomEvent threw");
            DestructiveTestUtil.AssertEmpireInvariants(f, "ReturnRandomEvent_NoCrash");
        }

        [EmpireDestructiveTest("Destructive.Events")]
        public static void ProcessEvents_NoCrash()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            TestAssert.DoesNotThrow(() => FCEventMaker.ProcessEvents(), "ProcessEvents threw");
            DestructiveTestUtil.AssertEmpireInvariants(f, "ProcessEvents_NoCrash");
        }

        [EmpireDestructiveTest("Destructive.Events")]
        public static void RemoveEventsWhere_RemovesMatching()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            FCEvent evt = null;
            TestAssert.DoesNotThrow(() => evt = FCEventMaker.MakeEvent(FCEventDefOf.cooldownMilitary), "MakeEvent threw");
            TestAssert.IsNotNull(evt, "MakeEvent should return an event");
            TestAssert.DoesNotThrow(() => FindFC.EventManager.AddEvent(evt), "AddEvent threw");

            int removed = 0;
            TestAssert.DoesNotThrow(() => removed = FindFC.EventManager.RemoveWhere(e => ReferenceEquals(e, evt)), "RemoveWhere threw");
            TestAssert.AreEqual(1, removed, "Exactly the one added event should be removed by reference");
            DestructiveTestUtil.AssertEmpireInvariants(f, "RemoveEventsWhere_RemovesMatching");
        }
    }
}
