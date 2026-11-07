namespace FactionColonies;

/* DESTRUCTIVE: generates a real empire fighter pawn and passes it to the world so
   SquadEffectivenessUtil.PawnEffectiveness runs on live health capacities. Not removed. */
public static class SquadMathDestructiveTests
{
    [EmpireDestructiveTest("Destructive.Military")]
    public static void PawnEffectiveness_LivePawn_InValidRange()
    {
        DestructiveTestUtil.RequireFaction();
        Pawn pawn = DestructiveTestUtil.GenerateWorldPawn();

        double eff = SquadEffectivenessUtil.PawnEffectiveness(pawn);
        TestAssert.IsTrue(eff >= 0.0 && eff <= 1.0, $"Effectiveness must be in [0,1], got {eff}");

        if (pawn.Dead || pawn.Downed)
            TestAssert.AreEqual(0.0, eff, "Dead/downed pawn contributes 0");
        else
            TestAssert.GreaterThan(eff, 0.0, "A conscious, mobile pawn should contribute > 0");
    }
}
