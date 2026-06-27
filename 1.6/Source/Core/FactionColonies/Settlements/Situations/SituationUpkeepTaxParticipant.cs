using System.Collections.Generic;
using RimWorld;
using Verse;
using FactionColonies.util;

namespace FactionColonies
{
    /// <summary>
    /// Charges each active situation's approach upkeep once per tax cycle, from the faction-level
    /// <see cref="PostTaxResolution"/> callback (not the per-settlement callbacks, which fire once per
    /// settlement). The draw is empire-wide — <c>TryPaySilver</c>'s settlement arg only feeds payment
    /// modifiers; the silver comes from the shared bank. If an approach's upkeep cannot be paid, the
    /// situation auto-reverts to its default approach and the player is notified.
    /// </summary>
    public class SituationUpkeepTaxParticipant : ITaxTickParticipant
    {
        private const string UpkeepReason = "SituationUpkeep";

        public void PreTaxResolution(FactionFC faction) { }
        public void PreSettlementCreateTax(WorldSettlementFC settlement) { }
        public void PostSettlementCreateTax(WorldSettlementFC settlement, ref int silverAmount, List<Thing> titheThings) { }

        public void PostTaxResolution(FactionFC faction)
        {
            FCSituationManager manager = faction?.situationManager;
            if (manager == null) return;

            // Snapshot — an auto-revert switches approach but does not mutate the list.
            foreach (FCSituation sit in new List<FCSituation>(manager.Situations))
            {
                FCSituationApproachDef approach = sit.activeApproach;
                if (approach == null || approach.upkeepSilver <= 0) continue;

                bool paid = PaymentUtil.TryPaySilver(approach.upkeepSilver, UpkeepReason, sit.targetSettlement);
                if (!paid && sit.def.defaultApproach != null && approach != sit.def.defaultApproach)
                {
                    manager.SwitchApproach(sit, sit.def.defaultApproach);
                    Messages.Message(
                        "FCSituationUpkeepUnpaid".Translate(sit.def.LabelCap, sit.def.defaultApproach.LabelCap),
                        MessageTypeDefOf.NegativeEvent);
                }
            }
        }
    }
}
