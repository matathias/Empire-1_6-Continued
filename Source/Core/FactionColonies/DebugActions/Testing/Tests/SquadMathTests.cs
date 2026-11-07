namespace FactionColonies;

/* Coverage for SquadEffectivenessUtil.PawnEffectiveness — the per-pawn combat-weight factor.
 * The registry hook (SquadPowerRegistry) is already tested; this covers the underlying math.
 * The null case is pure; the live-pawn case generates a pawn in SquadMathDestructiveTests. */
public static class SquadMathTests
{
    [EmpireTest("Military")]
    public static void PawnEffectiveness_NullPawn_ReturnsZero()
    {
        TestAssert.AreEqual(0.0, SquadEffectivenessUtil.PawnEffectiveness(null));
    }
}
