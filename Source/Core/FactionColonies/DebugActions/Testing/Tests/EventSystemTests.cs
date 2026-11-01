using RimWorld;
using Verse;

namespace FactionColonies
{
    public static class EventSystemTests
    {
        // ============================
        // Def Structural Integrity
        // ============================

        [EmpireTest("EventSystem")]
        public static void AllEventDefs_HaveValidCategory()
        {
            foreach (FCEventDef def in DefDatabase<FCEventDef>.AllDefsListForReading)
            {
                TestAssert.IsNotNull(def.category,
                    $"{def.defName}: category should not be null");
            }
        }

        [EmpireTest("EventSystem")]
        public static void AllRandomEvents_HavePositiveWeight()
        {
            foreach (FCEventDef def in DefDatabase<FCEventDef>.AllDefsListForReading)
            {
                if (!def.isRandomEvent) continue;
                TestAssert.GreaterThan(def.weight, 0,
                    $"{def.defName}: random event should have weight > 0, got {def.weight}");
            }
        }

        [EmpireTest("EventSystem")]
        public static void AllRandomEvents_HaveValidStatRanges()
        {
            foreach (FCEventDef def in DefDatabase<FCEventDef>.AllDefsListForReading)
            {
                if (!def.isRandomEvent) continue;
                TestAssert.IsTrue(def.minimumHappiness <= def.maximumHappiness,
                    $"{def.defName}: minHappiness ({def.minimumHappiness}) > maxHappiness ({def.maximumHappiness})");
                TestAssert.IsTrue(def.minimumLoyalty <= def.maximumLoyalty,
                    $"{def.defName}: minLoyalty ({def.minimumLoyalty}) > maxLoyalty ({def.maximumLoyalty})");
                TestAssert.IsTrue(def.minimumUnrest <= def.maximumUnrest,
                    $"{def.defName}: minUnrest ({def.minimumUnrest}) > maxUnrest ({def.maximumUnrest})");
                TestAssert.IsTrue(def.minimumProsperity <= def.maximumProsperity,
                    $"{def.defName}: minProsperity ({def.minimumProsperity}) > maxProsperity ({def.maximumProsperity})");
            }
        }

        [EmpireTest("EventSystem")]
        public static void AllEventDefs_StatModifiers_NoNullStats()
        {
            foreach (FCEventDef def in DefDatabase<FCEventDef>.AllDefsListForReading)
            {
                if (def.statModifiers == null && def.permanentStatModifiers == null) continue;
                if (def.statModifiers != null)
                {
                    for (int i = 0; i < def.statModifiers.Count; i++)
                    {
                        TestAssert.IsNotNull(def.statModifiers[i].stat,
                            $"{def.defName}: statModifiers[{i}] has null stat");
                    }
                }
                if (def.permanentStatModifiers != null)
                {
                    for (int i = 0; i < def.permanentStatModifiers.Count; i++)
                    {
                        TestAssert.IsNotNull(def.permanentStatModifiers[i].stat,
                            $"{def.defName}: permanentStatModifiers[{i}] has null stat");
                    }
                }
            }
        }

        [EmpireTest("EventSystem")]
        public static void AllEventDefs_Options_ResolvedCorrectly()
        {
            foreach (FCEventDef def in DefDatabase<FCEventDef>.AllDefsListForReading)
            {
                if (def.options == null) continue;
                for (int i = 0; i < def.options.Count; i++)
                {
                    TestAssert.IsNotNull(def.options[i],
                        $"{def.defName}: options[{i}] resolved to null");
                }
            }
        }

        // ============================
        // IsValidRandomEvent
        // ============================

        [EmpireTest("EventSystem")]
        public static void IsValidRandomEvent_NonRandomEvent_ReturnsFalse()
        {
            FCEventDef nonRandom = DefDatabase<FCEventDef>.AllDefsListForReading
                .FirstOrDefault(d => !d.isRandomEvent);
            if (nonRandom == null) TestAssert.Skip("All events are random events");

            TestAssert.IsFalse(FCEventMaker.IsValidRandomEvent(nonRandom),
                $"{nonRandom.defName}: non-random event should be rejected");
        }

        // ============================
        // ReturnRandomEvent (game state)
        // ============================

        [EmpireTest("EventSystem")]
        public static void ReturnRandomEvent_DoesNotThrow()
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction == null) TestAssert.Skip("No faction");

            TestAssert.DoesNotThrow(() => FCEventMaker.ReturnRandomEvent());
        }

        [EmpireTest("EventSystem")]
        public static void ReturnRandomEvent_Result_IsValidOrNull()
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction == null) TestAssert.Skip("No faction");

            FCEventDef result = FCEventMaker.ReturnRandomEvent();
            if (result == null) return; // null is valid (no eligible events)
            TestAssert.IsTrue(result.isRandomEvent,
                $"Returned event {result.defName} should have isRandomEvent=true");
        }

        // ============================
        // MakeEvent (game state)
        // ============================

        [EmpireTest("EventSystem")]
        public static void AllEventDefs_BiomeLists_AreValid()
        {
            foreach (FCEventDef def in DefDatabase<FCEventDef>.AllDefsListForReading)
            {
                foreach (string biome in def.applicableBiomes)
                {
                    TestAssert.IsNotNull(DefDatabase<BiomeDef>.GetNamed(biome, false),
                        $"{def.defName}: applicableBiomes contains unknown biome '{biome}'");
                }
                foreach (string biome in def.restrictedBiomes)
                {
                    TestAssert.IsNotNull(DefDatabase<BiomeDef>.GetNamed(biome, false),
                        $"{def.defName}: restrictedBiomes contains unknown biome '{biome}'");
                }
            }
        }

        [EmpireTest("EventSystem")]
        public static void AllEventOptions_ParentEvent_ResolvedCorrectly()
        {
            foreach (FCOptionDef def in DefDatabase<FCOptionDef>.AllDefsListForReading)
            {
                TestAssert.IsNotNull(def.parentEvent,
                    $"{def.defName}: parentEvent is null (bad defName in XML?)");
            }
        }

        [EmpireTest("EventSystem")]
        public static void MakeEvent_SetsTimeTillTrigger()
        {
            FCEventDef def = DefDatabase<FCEventDef>.AllDefsListForReading
                .FirstOrDefault(d => d.timeTillTrigger > 0);
            if (def == null) TestAssert.Skip("No event def with positive timeTillTrigger");

            FCEvent evt = FCEventMaker.MakeEvent(def);

            TestAssert.IsNotNull(evt, "MakeEvent should return non-null");
            TestAssert.GreaterThan(evt.timeTillTrigger, 0,
                $"timeTillTrigger should be > 0, got {evt.timeTillTrigger}");
        }
    }
}
