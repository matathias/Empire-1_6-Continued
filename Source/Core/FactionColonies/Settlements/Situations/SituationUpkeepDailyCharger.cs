using System.Collections.Generic;
using RimWorld;
using Verse;
using FactionColonies.util;

namespace FactionColonies
{
    /// <summary>
    /// Charges each active situation's approach upkeep once per day, from the
    /// <see cref="PostDailyAccrual"/> callback that fires after the faction's daily accrual loop
    /// completes. The draw is empire-wide — <c>TryPaySilver</c>'s settlement arg only feeds payment
    /// modifiers; the silver comes diretly from the player's coffers. If an approach's upkeep cannot
    /// be paid, the situation auto-reverts to its default approach and the player is notified.
    /// </summary>
    public class SituationUpkeepDailyCharger : IDailyAccrualParticipant
    {
        private const string UpkeepReason = "SituationUpkeep";

        public void PostDailyAccrual(FactionFC faction)
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
