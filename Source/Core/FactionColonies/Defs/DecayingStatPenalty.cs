using System;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// A temporary, self-decaying additive contribution to one of a settlement's "loss" stats
    /// (happinessLostBase / loyaltyLostBase / unrestGainedBase). Used to deliver a pawn/caravan-loss
    /// penalty gradually — "-X over Y days" — instead of one instant hit, while staying visible in the
    /// settlement's happiness/loyalty/unrest tooltip via the normal stat-modifier breakdown.
    ///
    /// <para>Each day, <see cref="CurrentValue"/> is the amount contributed to the stat (and shown in
    /// the tooltip). The daily bookkeeping tick (<c>WorldSettlementFC.TickDecayingPenalties</c>) drains
    /// <see cref="remaining"/> by that slice and advances <see cref="daysElapsed"/>; the entry is pruned
    /// once fully delivered. Linear amortization keeps the slice ~constant at <c>total / daysTotal</c>.</para>
    ///
    /// Serialized directly on <see cref="WorldSettlementFC"/> alongside permanent modifiers.
    /// </summary>
    public class DecayingStatPenalty : IExposable
    {
        // The loss stat this penalty feeds (Additive; positive value == "more loss"/"more unrest").
        public FCStatDef stat;
        // Penalty severity not yet delivered. Starts at the full amount, drains to ~0.
        public double remaining;
        public int daysTotal;
        public int daysElapsed;
        public string sourceId;
        public string sourceLabel;

        /// <summary>Whole days of penalty still pending (for the tooltip suffix).</summary>
        public int DaysLeft => daysTotal > daysElapsed ? daysTotal - daysElapsed : 0;

        /// <summary>
        /// The amount contributed to <see cref="stat"/> this day. Self-correcting linear amortization:
        /// remaining / days-left. Returns 0 once the penalty has been fully delivered.
        /// </summary>
        public double CurrentValue => daysTotal > daysElapsed ? remaining / (daysTotal - daysElapsed) : 0;

        /// <summary>True once the penalty has been fully delivered and should be pruned.</summary>
        public bool Finished => daysElapsed >= daysTotal || remaining <= 0;

        public void DecrementDay()
        {
            double toRemove = CurrentValue;
            remaining -= toRemove;
            daysElapsed++;
        }
        public void MergePenalty(double rem, int days)
        {
            remaining += rem;
            daysTotal = Math.Max(daysTotal, days);
            daysElapsed = 0;
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref stat, "stat");
            Scribe_Values.Look(ref remaining, "remaining");
            Scribe_Values.Look(ref daysTotal, "daysTotal");
            Scribe_Values.Look(ref daysElapsed, "daysElapsed");
            Scribe_Values.Look(ref sourceId, "sourceId");
            Scribe_Values.Look(ref sourceLabel, "sourceLabel");
        }
    }
}
