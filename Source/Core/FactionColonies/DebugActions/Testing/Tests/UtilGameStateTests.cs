namespace FactionColonies;

/* Util helpers that read the running game but leave it as they found it: crafting gates on
   research, the shared pawn-request builder, and the active tax-spot lookup. Category
   "UtilHelpers". */
public static class UtilGameStateTests
{
    /* CraftUtil.CanCraftItem */

    /* A knowledge-cost project (Anomaly style) has baseCost 0, so comparing progress against
     * baseCost alone used to report it finished before any knowledge was gained. */
    [EmpireTest("UtilHelpers")]
    public static void CanCraftItem_UnfinishedKnowledgeProject_Blocks()
    {
        ResearchProjectDef project = new() { defName = "EmpireTest_KnowledgeProject", baseCost = 0f, knowledgeCost = 50f };
        ThingDef item = new()
        {
            defName = "EmpireTest_KnowledgeGatedItem",
            recipeMaker = new RecipeMakerProperties { researchPrerequisite = project },
        };
        ThingDef itemByList = new()
        {
            defName = "EmpireTest_KnowledgeGatedItemList",
            recipeMaker = new RecipeMakerProperties { researchPrerequisites = [project] },
        };

        Dictionary<ResearchProjectDef, float> knowledge = PrivateFieldUtil.AnomalyKnowledge(Find.ResearchManager);
        try
        {
            TestAssert.IsFalse(project.IsFinished, "The project starts unfinished");
            TestAssert.IsFalse(CraftUtil.CanCraftItem(item), "An unfinished knowledge project blocks crafting (single prerequisite)");
            TestAssert.IsFalse(CraftUtil.CanCraftItem(itemByList), "An unfinished knowledge project blocks crafting (prerequisite list)");

            if (ModsConfig.AnomalyActive)
            {
                knowledge[project] = project.knowledgeCost;
                TestAssert.IsTrue(project.IsFinished, "Full knowledge finishes the project");
                TestAssert.IsTrue(CraftUtil.CanCraftItem(item), "A finished knowledge project allows crafting");
                TestAssert.IsTrue(CraftUtil.CanCraftItem(itemByList), "Also through the prerequisite list");
            }
        }
        finally { knowledge?.Remove(project); }
    }

    [EmpireTest("UtilHelpers")]
    public static void CanCraftItem_ZeroCostProject_CountsAsFinished()
    {
        ResearchProjectDef free = new() { defName = "EmpireTest_FreeProject", baseCost = 0f, knowledgeCost = 0f };
        ThingDef item = new()
        {
            defName = "EmpireTest_FreeGatedItem",
            recipeMaker = new RecipeMakerProperties { researchPrerequisite = free },
        };
        TestAssert.IsTrue(CraftUtil.CanCraftItem(item), "A project with no cost of either kind is finished");
    }

    /* FCPawnGenerator.BaseRequest */

    [EmpireTest("UtilHelpers")]
    public static void BaseRequest_DefaultsAreTheEmpireBaseline()
    {
        Faction faction = FindFC.EmpireFaction;
        PawnGenerationRequest request = FCPawnGenerator.BaseRequest(PawnKindDefOf.Colonist, faction);

        TestAssert.AreEqual(PawnKindDefOf.Colonist, request.KindDef, "Kind");
        TestAssert.AreEqual(faction, request.Faction, "Faction");
        TestAssert.AreEqual(PawnGenerationContext.NonPlayer, request.Context, "Context");
        TestAssert.IsFalse(request.ForceGenerateNewPawn, "World pawns may be reused");
        TestAssert.IsFalse(request.AllowDead, "No dead pawns");
        TestAssert.IsFalse(request.AllowDowned, "No downed pawns");
        TestAssert.IsFalse(request.CanGeneratePawnRelations, "No family relations");
        TestAssert.IsFalse(request.MustBeCapableOfViolence, "Violence not required by default");
        TestAssert.IsTrue(request.AllowGay, "Gay allowed by default");
        TestAssert.IsTrue(request.AllowFood, "Food allowed by default");
        TestAssert.IsFalse(request.AllowAddictions, "No addictions");
        TestAssert.AreEqual(0.0, request.BiocodeWeaponChance, 0.0001, "No biocoded weapons");
        TestAssert.IsNull(request.ForcedXenotype, "No forced xenotype by default");
        TestAssert.IsNull(request.ForcedCustomXenotype, "No forced custom xenotype by default");
        TestAssert.IsFalse(request.FixedBiologicalAge.HasValue, "No fixed biological age by default");
        TestAssert.IsFalse(request.FixedChronologicalAge.HasValue, "No fixed chronological age by default");
        TestAssert.IsFalse(request.FixedGender.HasValue, "No fixed gender by default");
    }

    [EmpireTest("UtilHelpers")]
    public static void BaseRequest_PassesOverridesThrough()
    {
        XenotypeDef xenotype = DefDatabase<XenotypeDef>.AllDefs.FirstOrDefault();
        PawnGenerationRequest request = FCPawnGenerator.BaseRequest(PawnKindDefOf.Colonist, null,
            mustBeViolent: true, allowGay: false, allowFood: false, forcedXenotype: xenotype,
            fixedBiologicalAge: 31f, fixedChronologicalAge: 40f, fixedGender: Gender.Female);

        TestAssert.IsTrue(request.MustBeCapableOfViolence, "mustBeViolent");
        TestAssert.IsFalse(request.AllowGay, "allowGay");
        TestAssert.IsFalse(request.AllowFood, "allowFood");
        TestAssert.AreEqual(xenotype, request.ForcedXenotype, "forcedXenotype");
        TestAssert.AreEqual(31.0, request.FixedBiologicalAge ?? -1f, 0.0001, "fixedBiologicalAge");
        TestAssert.AreEqual(40.0, request.FixedChronologicalAge ?? -1f, 0.0001, "fixedChronologicalAge");
        TestAssert.AreEqual(Gender.Female, request.FixedGender, "fixedGender");
    }

    [EmpireTest("UtilHelpers")]
    public static void AnimalAndMechRequests_BuildOnBaseRequest()
    {
        PawnKindDef animal = PawnKindDefOf.Muffalo;
        PawnGenerationRequest animalRequest = FCPawnGenerator.AnimalRequest(animal);
        TestAssert.AreEqual(FindFC.EmpireFaction, animalRequest.Faction, "Animals belong to the Empire");
        // The age is a random draw, so check it lies in the range GetReasonableMercenaryAge draws from
        // (Rand.Range: min inclusive, max exclusive), not that it equals a second draw.
        float lifeExpectancy = animal.race.race.lifeExpectancy;
        int minAge = (int)Math.Ceiling(lifeExpectancy * 0.2625d);
        int maxAge = (int)Math.Floor(lifeExpectancy * 0.625d);
        TestAssert.IsTrue(animalRequest.FixedBiologicalAge.HasValue, "Animals get a fixed age");
        float age = animalRequest.FixedBiologicalAge.Value;
        TestAssert.IsTrue(age >= minAge && age < Math.Max(minAge + 1, maxAge),
            $"Animals get a reasonable fixed age: {age} should be in [{minAge}, {maxAge})");
        TestAssert.IsFalse(animalRequest.MustBeCapableOfViolence, "Animals need not be violent");
        TestAssert.IsFalse(animalRequest.CanGeneratePawnRelations, "No relations for animals");

        PawnGenerationRequest mechRequest = FCPawnGenerator.MechRequest(PawnKindDefOf.Mech_Scyther);
        TestAssert.AreEqual(FindFC.EmpireFaction, mechRequest.Faction, "Mechs belong to the Empire");
        TestAssert.IsFalse(mechRequest.FixedBiologicalAge.HasValue, "Mechs get no fixed age");
        TestAssert.IsFalse(mechRequest.AllowAddictions, "No addictions for mechs");
    }

    /* PaymentUtil.TryFindActiveTaxSpot */

    private static Building_TaxSpot ScanForActiveSpot(Map map)
    {
        foreach (Building building in map.listerBuildings.allBuildingsColonist)
            if (building is Building_TaxSpot spot && spot.IsActiveTaxDeliverySpot) return spot;
        return null;
    }

    [EmpireTest("UtilHelpers")]
    public static void TryFindActiveTaxSpot_MatchesAScanOfHomeMaps()
    {
        Building_TaxSpot expectedAny = null;
        foreach (Map map in Find.Maps)
        {
            Building_TaxSpot onMap = ScanForActiveSpot(map);
            bool found = PaymentUtil.TryFindActiveTaxSpot(map, out Building_TaxSpot spot);
            TestAssert.AreEqual(onMap is not null, found, $"Per-map lookup on {map}");
            TestAssert.AreEqual(onMap, spot, $"Per-map spot on {map}");

            bool checkFound = PaymentUtil.CheckForTaxSpot(map, out IntVec3 dropSpot);
            TestAssert.AreEqual(found, checkFound, $"CheckForTaxSpot agrees on {map}");
            if (found) TestAssert.AreEqual(spot.Position, dropSpot, $"CheckForTaxSpot drop cell on {map}");

            if (expectedAny is null && map.IsPlayerHome) expectedAny = onMap;
        }

        bool anyFound = PaymentUtil.TryFindActiveTaxSpot(null, out Building_TaxSpot anySpot);
        TestAssert.AreEqual(expectedAny is not null, anyFound, "Any-map lookup finds a spot only on a player home map");
        TestAssert.AreEqual(expectedAny, anySpot, "Any-map lookup returns the first home map's spot");
        if (!anyFound) TestAssert.IsNull(anySpot, "No spot found means a null out value");
    }

    [EmpireTest("UtilHelpers")]
    public static void CheckForTaxSpot_NullMap_ReturnsFalse()
    {
        TestAssert.IsFalse(PaymentUtil.CheckForTaxSpot(null, out _), "A null map has no tax spot");
    }

    /* PawnUtil.DiscardPreviewPawn */

    [EmpireTest("UtilHelpers", GameState = TestGameState.Any)]
    public static void DiscardPreviewPawn_Null_IsANoOp()
    {
        TestAssert.DoesNotThrow(() => PawnUtil.DiscardPreviewPawn(null, "test"), "A null pawn is ignored");
    }
}
