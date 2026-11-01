using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FactionColonies
{
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
    /* ResourceEventRewardDef.BuildParams must assign param.techLevel BEFORE running
     * its ResourceFilterExtensions, so tech-gating filters see the real tech level
     * instead of TechLevel.Undefined.
     *-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
    public static class ResourceEventRewardDefTests
    {
        /// <summary>Records the tech level handed to it by BuildParams' filter-extension loop.</summary>
        private class RecordingFilterExtension : ResourceFilterExtension
        {
            public TechLevel? received;
            public override void SetFilter(ThingFilter filter, TechLevel techlevel, ResourceFC resource = null)
            {
                received = techlevel;
            }
        }

        [EmpireTest("Resource")]
        public static void BuildParams_AssignsTechLevelBeforeFilterExtensions()
        {
            var recorder = new RecordingFilterExtension();
            var def = new ResourceEventRewardDef
            {
                overrideTechLevel = true,
                techLevel = TechLevel.Spacer,
                modExtensions = new List<DefModExtension> { recorder }
            };

            ThingSetMakerParams param = def.BuildParams(1000.0, out _);

            TestAssert.AreEqual(TechLevel.Spacer, param.techLevel,
                "param.techLevel should equal the override level");
            TestAssert.IsNotNull(recorder.received, "filter extension should have been invoked");
            TestAssert.AreEqual(TechLevel.Spacer, recorder.received.Value,
                "filter extension should receive the override tech level, not Undefined");
        }
    }
}
