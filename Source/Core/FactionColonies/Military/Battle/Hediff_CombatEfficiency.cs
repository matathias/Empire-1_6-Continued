using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FactionColonies
{
    /* Hediff_CombatEfficiency */
    // Custom hediff so the Info Card shows severity-scaled stat values that match what is
    // actually applied to the pawn. The vanilla path
    // (Hediff.SpecialDisplayStats -> HediffDef.SpecialDisplayStats -> HediffStage.SpecialDisplayStats)
    // calls HediffStatsUtility.SpecialDisplayStats(stage, null), which skips the
    // multiplyStatChangesBySeverity scaling and shows raw stage factors (e.g. x200%).
    // Passing the instance applies severity scaling, matching the hover tooltip
    // produced by Hediff.TipStringExtra.
    public class Hediff_CombatEfficiency : Hediff
    {
        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
            foreach (StatDrawEntry item in HediffStatsUtility.SpecialDisplayStats(CurStage, this))
            {
                yield return item;
            }
        }
    }
}
