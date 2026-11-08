namespace FactionColonies;

/* The auto-resolve injury path must not destroy a part whose loss kills the pawn: the core part, a
   part with a vital tag, or a part holding one. Checked on the human body with a bare,
   never-generated pawn, which only supplies the race's body. */
public static class CasualtyVitalPartTests
{
    private static Pawn HumanBody() => new() { def = ThingDefOf.Human };

    private static BodyPartRecord Part(BodyPartDef def) =>
        TestAssert.NotNull(BodyDefOf.Human.AllParts.FirstOrDefault(p => p.def == def), $"The human body has no {def?.defName}");

    [EmpireTest("Battle", GameState = TestGameState.Any)]
    public static void VitalParts_AreProtected()
    {
        Pawn pawn = HumanBody();
        TestAssert.IsTrue(BattleCasualtyApplicator.ShouldAvoidDestroyingPart(pawn.RaceProps.body.corePart, pawn), "The core part (torso)");
        TestAssert.IsTrue(BattleCasualtyApplicator.ShouldAvoidDestroyingPart(Part(BodyPartDefOf.Heart), pawn), "The heart pumps blood");
        TestAssert.IsTrue(BattleCasualtyApplicator.ShouldAvoidDestroyingPart(Part(BodyPartDefOf.Lung), pawn), "A lung breathes");
        TestAssert.IsTrue(BattleCasualtyApplicator.ShouldAvoidDestroyingPart(Part(BodyPartDefOf.Neck), pawn), "The neck holds the head");
        TestAssert.IsTrue(BattleCasualtyApplicator.ShouldAvoidDestroyingPart(Part(BodyPartDefOf.Head), pawn), "The head holds the brain");
    }

    [EmpireTest("Battle", GameState = TestGameState.Any)]
    public static void Limbs_AreNotProtected()
    {
        Pawn pawn = HumanBody();
        foreach (BodyPartDef def in new[] { BodyPartDefOf.Leg, BodyPartDefOf.Arm, BodyPartDefOf.Hand, BodyPartDefOf.Shoulder, BodyPartDefOf.Eye })
        {
            TestAssert.IsFalse(BattleCasualtyApplicator.ShouldAvoidDestroyingPart(Part(def), pawn),
                $"Losing a {def.defName} is an injury, not a death");
        }
    }

    [EmpireTest("Battle", GameState = TestGameState.Any)]
    public static void EveryPartWithAVitalTag_IsProtected()
    {
        Pawn pawn = HumanBody();
        foreach (BodyPartRecord part in BodyDefOf.Human.AllParts)
        {
            if (part.def?.tags is null || !part.def.tags.Any(t => t.vital)) continue;
            TestAssert.IsTrue(BattleCasualtyApplicator.ShouldAvoidDestroyingPart(part, pawn),
                $"{part.Label} carries a vital tag and must be protected");
            for (BodyPartRecord parent = part.parent; parent is not null; parent = parent.parent)
            {
                TestAssert.IsTrue(BattleCasualtyApplicator.ShouldAvoidDestroyingPart(parent, pawn),
                    $"{parent.Label} holds the vital {part.Label} and must be protected");
            }
        }
    }
}
