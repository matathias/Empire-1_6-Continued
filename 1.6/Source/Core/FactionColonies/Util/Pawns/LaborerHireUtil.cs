using RimWorld;
using RimWorld.QuestGen;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies.util
{
    /// <summary>
    /// DefOf handles for the two "Hire Laborers" quest scripts. Both are Royalty-gated, so both are
    /// MayRequire-gated here (null when Royalty is absent).
    /// </summary>
    [DefOf]
    public static class EmpireQuestScriptDefOf
    {
        [MayRequire("Ludeon.RimWorld.Royalty")]
        public static QuestScriptDef Empire_HireLaborers_Ground;

        [MayRequire("Ludeon.RimWorld.Royalty")]
        public static QuestScriptDef Empire_HireLaborers_Shuttle;

        static EmpireQuestScriptDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(EmpireQuestScriptDefOf));
        }
    }

    /// <summary>
    /// The player-facing "Hire Laborers" faction action: pay silver to have the Empire send a small
    /// group of temporary, general-labor-only colonists. The laborer lifecycle (generation,
    /// work-disable, join-player, leave timer, goodwill, save/load) is owned entirely by the fired
    /// quest; this util only gates (silver + cooldown), picks transport, and fires the right quest.
    /// Transport mode mirrors tax delivery so laborers arrive/depart the same way taxes do.
    /// </summary>
    public static class LaborerHireUtil
    {
        /// <summary>Laborers scale with the empire's footprint: a base count plus a per-settlement
        /// increment, clamped to the configured maximum. All three terms are mod settings.</summary>
        public static int CalculateCount()
        {
            int settlements = FindFC.FactionComp?.settlements?.Count ?? 0;
            int raw = FCSettings.laborerBaseCount + settlements * FCSettings.laborerPerSettlement;
            return Mathf.Clamp(raw, FCSettings.laborerBaseCount, FCSettings.laborerMaxCount);
        }

        public static int CalculateCost(int count, int days)
        {
            return count * FCSettings.laborerCostPerDay * days;
        }

        /// <summary>Cost of the offer as it currently stands (used for the live button label).</summary>
        public static int CurrentCost()
        {
            return CalculateCost(CalculateCount(), FCSettings.laborerDurationDays);
        }

        /// <summary>
        /// True when a hire can be started right now. On false, <paramref name="reason"/> holds a
        /// player-facing explanation suitable for a disabled-button tooltip.
        /// </summary>
        public static bool CanHire(out string reason)
        {
            reason = null;
            if (!ModsConfig.RoyaltyActive)
            {
                reason = "FCHireLaborersNoRoyalty".Translate();
                return false;
            }
            FactionFC faction = FindFC.FactionComp;
            if (faction is null)
            {
                reason = "FCHireLaborersNoMap".Translate();
                return false;
            }
            if (FindFC.TaxMap is null)
            {
                reason = "FCHireLaborersNoMap".Translate();
                return false;
            }
            if (!faction.laborerCooldown.IsReady)
            {
                // The deployed state is handled by the button swapping to "Dismiss Laborers", so a
                // disabled Hire button here only ever means the post-contract rest period.
                reason = "FCHireLaborersCooldown".Translate(faction.laborerCooldown.DaysRemaining.ToString("0.#"));
                return false;
            }
            if (!DebugSettings.godMode && PaymentUtil.GetSilver() < CurrentCost())
            {
                reason = "FCHireLaborersNoSilver".Translate(CurrentCost());
                return false;
            }
            return true;
        }

        /// <summary>
        /// Gate (cooldown -> map -> pay) then fire the transport-appropriate quest. Silver is only
        /// consumed and the cooldown is only started once the quest actually fires; any earlier bail
        /// consumes nothing.
        /// </summary>
        public static void HireLaborers()
        {
            if (!ModsConfig.RoyaltyActive) return;

            FactionFC faction = FindFC.FactionComp;
            if (faction is null) return;

            // Keep the cooldown length in sync with the (live-editable) setting before gating on it.
            faction.laborerCooldown.SetCooldown(FCSettings.laborerCooldownDays * GenDate.TicksPerDay);

            // Cooldown gate (shows its own RejectInput message when still cooling down).
            if (!faction.laborerCooldown.TryUseOrShowCooldown()) return;

            Map map = FindFC.TaxMap;
            if (map is null)
            {
                Messages.Message("FCHireLaborersNoMap".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            int count = CalculateCount();
            int days = FCSettings.laborerDurationDays;
            int cost = CalculateCost(count, days);

            // Payment (god mode hires for free, matching fire support).
            if (!DebugSettings.godMode && !PaymentUtil.TryPaySilver(cost, PaymentUtil.Reason_HireLaborers))
            {
                Messages.Message("FCHireLaborersNoSilver".Translate(cost), MessageTypeDefOf.RejectInput, false);
                return;
            }

            PawnKindDef laborerKind = ResolveLaborerPawnKind();

            // Transport mode mirrors tax delivery (walk-in / drop-pod / shuttle), honoring the forced-mode setting.
            bool anyShuttlePort = FindFC.Settlements.Any(s => s.BuildingsComp?.HasBuilding(BuildingFCDefOf.shuttlePort) ?? false);
            TaxDeliveryMode mode = DeliveryLogistics.TaxDeliveryModeForSettlement(anyShuttlePort, faction.capitalLocation);

            Slate slate = new Slate();
            slate.Set("map", map);
            slate.Set("laborersCount", count);
            slate.Set("permitFaction", FindFC.EmpireFaction);
            slate.Set("laborersPawnKind", laborerKind);
            slate.Set("laborersDurationDays", days);
            slate.Set("laborerSkillBonus", SkillBonus());

            QuestScriptDef script;
            if (mode == TaxDeliveryMode.Shuttle && EmpireQuestScriptDefOf.Empire_HireLaborers_Shuttle is object)
            {
                script = EmpireQuestScriptDefOf.Empire_HireLaborers_Shuttle;
                slate.Set("landingCell", DropCellFinder.GetBestShuttleLandingSpot(map, Faction.OfPlayer));
            }
            else
            {
                script = EmpireQuestScriptDefOf.Empire_HireLaborers_Ground;
                slate.Set("arrivalMode", mode == TaxDeliveryMode.DropPod
                    ? PawnsArrivalModeDefOf.CenterDrop
                    : PawnsArrivalModeDefOf.EdgeWalkIn);
            }

            if (script is null) return;

            QuestUtility.GenerateQuestAndMakeAvailable(script, slate);

            // Anchor the cooldown to the laborers' scheduled departure (hire + contract length), so the
            // rest period only begins once they leave and re-hiring stays blocked while they're deployed.
            faction.laborerCooldown.Use(Find.TickManager.TicksGame + days * GenDate.TicksPerDay);
        }

        /// <summary>
        /// Finds the currently-deployed laborer batch's quest, if any (used to swap the main-tab button
        /// to "Dismiss Laborers" and to end the contract early). Both quest scripts are checked.
        /// </summary>
        public static bool TryGetActiveLaborerQuest(out Quest quest)
        {
            quest = null;
            QuestScriptDef ground = EmpireQuestScriptDefOf.Empire_HireLaborers_Ground;
            QuestScriptDef shuttle = EmpireQuestScriptDefOf.Empire_HireLaborers_Shuttle;
            var quests = Find.QuestManager?.QuestsListForReading;
            if (quests is null) return false;
            for (int i = 0; i < quests.Count; i++)
            {
                Quest q = quests[i];
                if (q is null || q.State != QuestState.Ongoing) continue;
                if (q.root == ground || (shuttle is object && q.root == shuttle))
                {
                    quest = q;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Sends the currently-deployed laborers home early (after a confirmation). The contract's
        /// remaining days are forfeited with no refund, and the cooldown restarts from this moment.
        /// </summary>
        public static void DismissLaborers()
        {
            if (!TryGetActiveLaborerQuest(out Quest quest)) return;
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "FCDismissLaborersConfirm".Translate(FCSettings.laborerCooldownDays),
                () => DoDismiss(quest)));
        }

        private static void DoDismiss(Quest quest)
        {
            // The dialog resolves a frame or more later; bail if the contract already ended on its own
            // (so we don't re-anchor a cooldown that already started at the scheduled departure).
            if (quest is null || quest.State != QuestState.Ongoing) return;

            // Ending the quest runs QuestPart_Leave (leaveOnCleanup): laborers revert to the Empire and
            // walk off the map.
            quest.End(QuestEndOutcome.Success, sendLetter: false);

            FactionFC faction = FindFC.FactionComp;
            if (faction?.laborerCooldown is object)
            {
                faction.laborerCooldown.SetCooldown(FCSettings.laborerCooldownDays * GenDate.TicksPerDay);
                faction.laborerCooldown.SetEndTick(Find.TickManager.TicksGame);
            }
            Messages.Message("FCDismissLaborersDone".Translate(), MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>
        /// Per-skill bonus applied to hired laborers, scaling with the empire's average settlement
        /// level (level 1 = no bonus). Both the per-level rate and the cap are mod settings.
        /// </summary>
        private static int SkillBonus()
        {
            var settlements = FindFC.Settlements;
            double avgLevel = (settlements != null && settlements.Any())
                ? settlements.Average(s => s.settlementLevel)
                : 1.0;
            int raw = Mathf.RoundToInt((float)(avgLevel - 1.0) * FCSettings.laborerSkillBonusPerLevel);
            return Mathf.Clamp(raw, 0, FCSettings.laborerSkillBonusCap);
        }

        /// <summary>
        /// A villager-template pawnkind matching the faction's race (same resolution
        /// <see cref="FCPawnGenerator.CivilianRequest"/> uses). Never returns null.
        /// </summary>
        private static PawnKindDef ResolveLaborerPawnKind()
        {
            ThingDef race = FindFC.FactionComp?.xenotypeFilter?.GetRandomRace() ?? ThingDefOf.Human;
            return PawnKindTemplateUtil.GetVillagerForRace(race);
        }
    }
}
