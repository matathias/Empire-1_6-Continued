using System.Linq;
using Verse;

namespace FactionColonies
{
    /* Coverage for SquadEffectivenessUtil.PawnEffectiveness — the per-pawn combat-weight factor.
     * The registry hook (SquadPowerRegistry) is already tested; this covers the underlying math.
     * The null case is pure; the live-pawn case is skip-guarded on having a colonist available. */
    public static class SquadMathTests
    {
        [EmpireTest("Military")]
        public static void PawnEffectiveness_NullPawn_ReturnsZero()
        {
            TestAssert.AreEqual(0.0, SquadEffectivenessUtil.PawnEffectiveness(null));
        }

        [EmpireTest("Military")]
        public static void PawnEffectiveness_LivePawn_InValidRange()
        {
            Map map = Find.CurrentMap;
            if (map == null) TestAssert.Skip("No current map");
            Pawn pawn = map.mapPawns?.FreeColonists?.FirstOrDefault();
            if (pawn == null) TestAssert.Skip("No colonist available");

            double eff = SquadEffectivenessUtil.PawnEffectiveness(pawn);
            TestAssert.IsTrue(eff >= 0.0 && eff <= 1.0, $"Effectiveness must be in [0,1], got {eff}");

            if (pawn.Dead || pawn.Downed)
                TestAssert.AreEqual(0.0, eff, "Dead/downed pawn contributes 0");
            else
                TestAssert.GreaterThan(eff, 0.0, "A conscious, mobile colonist should contribute > 0");
        }
    }
}
