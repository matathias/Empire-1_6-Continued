namespace FactionColonies;

public static class BuildingDefTests
{
    // ============================
    // CanBeBuiltForSettlementType
    // ============================

    // The four list-semantics tests use standalone defs (never registered) so they do not depend on
    // which shipped buildings happen to carry allow/block lists.

    [EmpireTest("BuildingDef")]
    public static void CanBeBuilt_NoLists_ReturnsTrue()
    {
        var building = new BuildingFCDef { defName = "EmpireTest_NoLists" };

        WorldSettlementDef settlementDef = WorldSettlementDefOf.WorldSettlementDef_Surface;
        TestAssert.IsTrue(building.CanBeBuiltForSettlementType(settlementDef),
            $"{building.defName} with no allow/block lists should be buildable everywhere");
    }

    [EmpireTest("BuildingDef")]
    public static void CanBeBuilt_InBlockList_ReturnsFalse()
    {
        WorldSettlementDef settlementDef = WorldSettlementDefOf.WorldSettlementDef_Surface;
        var building = new BuildingFCDef { defName = "EmpireTest_BlocksSurface", settlementTypeBlockList = [settlementDef] };

        TestAssert.IsFalse(building.CanBeBuiltForSettlementType(settlementDef),
            $"{building.defName} should be blocked for Surface");
    }

    [EmpireTest("BuildingDef")]
    public static void CanBeBuilt_InAllowList_ReturnsTrue()
    {
        WorldSettlementDef settlementDef = WorldSettlementDefOf.WorldSettlementDef_Surface;
        var building = new BuildingFCDef { defName = "EmpireTest_AllowsSurface", settlementTypeAllowList = [settlementDef] };

        TestAssert.IsTrue(building.CanBeBuiltForSettlementType(settlementDef),
            $"{building.defName} should be allowed for Surface");
    }

    [EmpireTest("BuildingDef")]
    public static void CanBeBuilt_AllowList_ExcludesUnlisted()
    {
        // The allow list names only a standalone settlement type, so Surface (which neither is
        // nor derives from it) is unlisted.
        var listedOnly = new WorldSettlementDef { defName = "EmpireTest_ListedSettlementType" };
        var building = new BuildingFCDef { defName = "EmpireTest_AllowsOther", settlementTypeAllowList = [listedOnly] };

        WorldSettlementDef excluded = WorldSettlementDefOf.WorldSettlementDef_Surface;
        TestAssert.IsFalse(building.CanBeBuiltForSettlementType(excluded),
            $"{building.defName} should NOT be buildable for {excluded.defName} (not in allow list)");
    }

    [EmpireTest("BuildingDef")]
    public static void CanBeBuilt_SubtypeExclusive_ExcludesSiblings()
    {
        foreach (BuildingFCDef building in DefDatabase<BuildingFCDef>.AllDefsListForReading)
        {
            if (building.settlementTypeAllowList is null || building.settlementTypeAllowList.Count == 0) continue;

            foreach (WorldSettlementDef allowed in building.settlementTypeAllowList)
            {
                if (allowed.baseSettlementType is null) continue;
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
            if (def.upgrades is null) continue;
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
            if (def.requiredBuildings is null) continue;
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
