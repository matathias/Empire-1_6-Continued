using RimWorld;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /* Auto-tending helper for off-map mercenary pawns. Mirrors what WorldPawns.WorldPawnsTick
     * does to alive world pawns: when a wound needs tending, apply a tend without consuming
     * inventory medicine. We replicate the core of TendUtility.DoTend (quality calculation +
     * Hediff.Tended calls) rather than calling DoTend directly, because DoTend insists on
     * destroying the medicine Thing it receives — and we're abstracting medicine, not
     * actually consuming player stockpile.
     *
     * Doctor + medicine are sourced through MercAutoTendRegistry, so submods can supply a
     * skilled doctor or override the medicine the base mod chose from tech level. */
    public static class MercTendingUtil
    {
        private static readonly List<Hediff> _tmpHediffs = new List<Hediff>();

        /// <summary>
        /// Applies one tend pass to <paramref name="merc"/>'s pawn using the doctor + medicine
        /// resolved through <see cref="MercAutoTendRegistry"/>. No-op if the pawn has no
        /// hediffs needing tending. Caller is responsible for confirming the pawn is alive
        /// and off-map — this method does not re-check.
        /// </summary>
        public static void TendOnce(Mercenary merc, WorldSettlementFC settlement)
        {
            if (merc?.pawn is null) return;
            Pawn patient = merc.pawn;
            if (!patient.health.HasHediffsNeedingTend()) return;

            Pawn doctor = MercAutoTendRegistry.PickDoctor(merc, settlement);
            ThingDef medicine = MercAutoTendRegistry.PickMedicine(merc, settlement);

            float quality = TendUtility.CalculateBaseTendQuality(doctor, patient, medicine);
            float maxQuality = medicine?.GetStatValueAbstract(StatDefOf.MedicalQualityMax) ?? 0.7f;

            _tmpHediffs.Clear();
            TendUtility.GetOptimalHediffsToTendWithSingleTreatment(patient, medicine != null, _tmpHediffs);
            for (int i = 0; i < _tmpHediffs.Count; i++)
            {
                _tmpHediffs[i].Tended(quality, maxQuality, i);
            }
            _tmpHediffs.Clear();

            patient.records?.Increment(RecordDefOf.TimesTendedTo);
            doctor?.records?.Increment(RecordDefOf.TimesTendedOther);
        }
    }
}
