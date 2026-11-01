using Verse;

namespace FactionColonies
{
    public static class BuildingDefTests
    {
        // ============================
        // CanBeBuiltForSettlementType
        // ============================

        [EmpireTest("BuildingDef")]
        public static void CanBeBuilt_NoLists_ReturnsTrue()
        {
            // Find a building with empty allow and block lists
            BuildingFCDef building = DefDatabase<BuildingFCDef>.AllDefsListForReading
                .FirstOrDefault(b => (b.settlementTypeAllowList == null || b.settlementTypeAllowList.Count == 0)
                    && (b.settlementTypeBlockList == null || b.settlementTypeBlockList.Count == 0));
            if (building == null) TestAssert.Skip("No building with empty allow/block lists");

            WorldSettlementDef settlementDef = WorldSettlementDefOf.WorldSettlementDef_Surface;
            TestAssert.IsTrue(building.CanBeBuiltForSettlementType(settlementDef),
                $"{building.defName} with no allow/block lists should be buildable everywhere");
        }

        [EmpireTest("BuildingDef")]
        public static void CanBeBuilt_InBlockList_ReturnsFalse()
        {
            WorldSettlementDef settlementDef = WorldSettlementDefOf.WorldSettlementDef_Surface;
            BuildingFCDef building = DefDatabase<BuildingFCDef>.AllDefsListForReading
                .FirstOrDefault(b => b.settlementTypeBlockList != null && b.settlementTypeBlockList.Contains(settlementDef));
            if (building == null) TestAssert.Skip("No building blocks Surface settlement type");

            TestAssert.IsFalse(building.CanBeBuiltForSettlementType(settlementDef),
                $"{building.defName} should be blocked for Surface");
        }

        [EmpireTest("BuildingDef")]
        public static void CanBeBuilt_InAllowList_ReturnsTrue()
        {
            WorldSettlementDef settlementDef = WorldSettlementDefOf.WorldSettlementDef_Surface;
            BuildingFCDef building = DefDatabase<BuildingFCDef>.AllDefsListForReading
                .FirstOrDefault(b => b.settlementTypeAllowList != null && b.settlementTypeAllowList.Contains(settlementDef));
            if (building == null) TestAssert.Skip("No building has Surface in allow list");

            TestAssert.IsTrue(building.CanBeBuiltForSettlementType(settlementDef),
                $"{building.defName} should be allowed for Surface");
        }

        [EmpireTest("BuildingDef")]
        public static void CanBeBuilt_AllowList_ExcludesUnlisted()
        {
            // Find a building with a non-empty allow list, then test with a settlement type NOT in it
            BuildingFCDef building = DefDatabase<BuildingFCDef>.AllDefsListForReading
                .FirstOrDefault(b => b.settlementTypeAllowList != null && b.settlementTypeAllowList.Count > 0);
            if (building == null) TestAssert.Skip("No building has an allow list");

            WorldSettlementDef excluded = DefDatabase<WorldSettlementDef>.AllDefsListForReading
                .FirstOrDefault(sd => !building.settlementTypeAllowList.Contains(sd));
            if (excluded == null) TestAssert.Skip("All settlement types are in the allow list");

            TestAssert.IsFalse(building.CanBeBuiltForSettlementType(excluded),
                $"{building.defName} should NOT be buildable for {excluded.defName} (not in allow list)");
        }

        [EmpireTest("BuildingDef")]
        public static void CanBeBuilt_SubtypeExclusive_ExcludesSiblings()
        {
            foreach (BuildingFCDef building in DefDatabase<BuildingFCDef>.AllDefsListForReading)
            {
                if (building.settlementTypeAllowList == null || building.settlementTypeAllowList.Count == 0) continue;

                foreach (WorldSettlementDef allowed in building.settlementTypeAllowList)
                {
                    if (allowed.baseSettlementType == null) continue;
                    // allowed is a subtype — verify sibling subtypes are excluded
                    foreach (WorldSettlementDef sibling in DefDatabase<WorldSettlementDef>.AllDefsListForReading)
                    {
                        if (sibling == allowed || sibling.baseSettlementType != allowed.baseSettlementType) continue;
                        TestAssert.IsFalse(building.CanBeBuiltForSettlementType(sibling),
                            $"{building.defName} allows {allowed.defName} but should NOT be buildable on sibling {sibling.defName}");
                    }
                }
            }
        }

        // ============================
        // Structural Integrity
        // ============================

        [EmpireTest("BuildingDef")]
        public static void AllBuildings_Upgrades_ResolvedCorrectly()
        {
            foreach (BuildingFCDef def in DefDatabase<BuildingFCDef>.AllDefsListForReading)
            {
                if (def.upgrades == null) continue;
                for (int i = 0; i < def.upgrades.Count; i++)
                {
                    TestAssert.IsNotNull(def.upgrades[i],
                        $"{def.defName}: upgrades[{i}] resolved to null (bad defName in XML?)");
                }
            }
        }

        [EmpireTest("BuildingDef")]
        public static void AllBuildings_RequiredBuildings_ResolvedCorrectly()
        {
            foreach (BuildingFCDef def in DefDatabase<BuildingFCDef>.AllDefsListForReading)
            {
                if (def.requiredBuildings == null) continue;
                for (int i = 0; i < def.requiredBuildings.Count; i++)
                {
                    TestAssert.IsNotNull(def.requiredBuildings[i],
                        $"{def.defName}: requiredBuildings[{i}] resolved to null (bad defName in XML?)");
                }
            }
        }

        [EmpireTest("BuildingDef")]
        public static void AllBuildings_StatModifiers_NoNullStats()
        {
            foreach (BuildingFCDef def in DefDatabase<BuildingFCDef>.AllDefsListForReading)
            {
                for (int i = 0; i < def.statModifiers.Count; i++)
                {
                    TestAssert.IsNotNull(def.statModifiers[i].stat,
                        $"{def.defName}: statModifiers[{i}] has null stat (bad defName in XML?)");
                }
            }
        }

        [EmpireTest("BuildingDef")]
        public static void AllBuildings_Description_NotEmpty()
        {
            foreach (BuildingFCDef def in DefDatabase<BuildingFCDef>.AllDefsListForReading)
            {
                if (def == BuildingFCDefOf.Empty || def == BuildingFCDefOf.Construction) continue;
                TestAssert.IsTrue(!string.IsNullOrEmpty(def.desc),
                    $"{def.defName}: desc should not be null or empty");
            }
        }
    }
}
