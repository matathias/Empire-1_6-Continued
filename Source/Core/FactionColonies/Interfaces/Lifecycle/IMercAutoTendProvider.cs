using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Allows submods to influence the auto-tending of off-map mercenary pawns. Each registered
    /// provider can supply a doctor pawn (whose <c>MedicalTendQuality</c> stat is read by vanilla
    /// <c>TendUtility.DoTend</c>) and/or override the medicine ThingDef chosen by the base mod's
    /// tech-level mapping. Register implementations via <see cref="MercAutoTendRegistry"/>.
    /// </summary>
    public interface IMercAutoTendProvider
    {
        /// <summary>
        /// Returns a pawn to act as the tending doctor, or null to defer to other providers /
        /// the base default (no doctor). The doctor does not need to be spawned — vanilla only
        /// reads the doctor's stats. First non-null return wins; later providers do not run.
        /// </summary>
        Pawn ProvideTendingDoctor(Mercenary patient, WorldSettlementFC settlement);

        /// <summary>
        /// Override the medicine ThingDef. Receives the running choice as <paramref name="currentChoice"/>
        /// (initially the base mod's tech-level pick); return that to defer or any other ThingDef
        /// to override. Return null to force no-medicine tending. Chains across all providers.
        /// </summary>
        ThingDef OverrideTendingMedicine(Mercenary patient, WorldSettlementFC settlement, ThingDef currentChoice);
    }
}
