using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Handler for defensive ops created by <see cref="MilitaryOperationManager.CreateDefensiveOp"/>.
    /// <para>External <see cref="IRaidTarget"/> objects (no settlement map) have
    /// <see cref="ResolvesManually"/> return false, so they auto-resolve through the inherited
    /// <see cref="MilitaryJobHandler.OnAutoResolve"/> (the per-round engine). Settlement targets
    /// return true, and <see cref="OnManualResolve"/> hands off to
    /// <see cref="BattlefieldContext.StartDefense"/>, which decides auto-vs-manual internally based
    /// on <c>FCSettings.battleMode</c> and the settlement's <c>supportsManualBattle</c>.
    /// <see cref="ApplyResult"/> applies the settlement-side outcome (loyalty / happiness / building
    /// destruction). Because it fires per-op, multi-op battle resolutions (multiple concurrent
    /// attackers on one tile) apply the full settlement-side penalty set once per op — each op
    /// is a distinct logical attack with its own consequences.</para>
    /// <para>Result letters are NOT sent per-op: when several concurrent attacks resolve together,
    /// <see cref="WorldObjectComp_SettlementMilitary.EndBattle"/> opens a
    /// <see cref="DefenseLetterAccumulator"/>, each op appends its outcome fragment, and one
    /// condensed letter is flushed at the end (see <see cref="DefensiveBattleEffects"/>).</para>
    /// </summary>
    public class MilitaryJobHandler_Defend : MilitaryJobHandler
    {
        public override void OnOpCreated(MilitaryOperation op)
        {
            // Warning event + "settlement in danger" letter are scheduled by CreateDefensiveOp.
            // Nothing for the handler to do at op creation.
        }

        public override bool ResolvesManually(MilitaryOperation op)
        {
            // Settlement targets go through BattlefieldContext.StartDefense, which decides auto vs
            // manual internally. External IRaidTarget objects have no map, so always auto-resolve.
            return op?.targetObject is WorldSettlementFC s && s.MilitaryComp is object;
        }

        public override void OnManualResolve(MilitaryOperation op)
        {
            if (op is null) return;

            BattlefieldContext bf = FindFC.MilitaryManager?.GetOrCreateBattlefield(op.targetTile);
            if (bf is null)
            {
                LogUtil.Error($"MilitaryJobHandler_Defend.OnManualResolve: no battlefield context for op id={op.id} at tile {op.targetTile}; falling back to auto-resolve.");
                op.BeginAutoResolveProgress();
                return;
            }

            // StartDefense's auto sub-path will eventually call EndBattle → comp.EndBattle →
            // op.CompleteBattle. Manual sub-path drives a real battle that resolves the same way.
            // Either way, op.CompleteBattle gets called.
            bf.StartDefense(op);
        }

        public override void ApplyResult(MilitaryOperation op, BattleResult result)
        {
            // Per-op settlement-side effects. Concurrent multi-op battles get the penalty set
            // applied once per op — each op is a logically distinct attack on the settlement.
            if (op is null || result is null) return;
            if (result.winner == BattleWinner.Error) return;

            if (!(op.targetObject is WorldSettlementFC target)) return;
            // Guard: don't apply Empire-style settlement effects to a settlement that isn't
            // tracked by Empire (no military comp).
            if (target.MilitaryComp is null) return;

            try
            {
                if (result.DefenderVictory) DefensiveBattleEffects.ApplyWin(target, op, result);
                else DefensiveBattleEffects.ApplyLoss(target, op, result);
            }
            catch (Exception e)
            {
                LogUtil.Error($"MilitaryJobHandler_Defend.ApplyResult: settlement-effect application threw on {target.Name}: {e}");
            }
        }
    }

    /// <summary>
    /// Settlement-side effects of a defensive battle outcome (loyalty / happiness / prosperity
    /// changes, building destruction on loss, "settlement leveled down" rolls, result letters).
    /// Invoked from <see cref="MilitaryJobHandler_Defend.ApplyResult"/> so the effects run inside
    /// <see cref="MilitaryOperation.CompleteBattle"/> before lifecycle listeners observe the
    /// resolved op.
    /// <para>Letter emission is split from effect application: when an
    /// <see cref="activeAccumulator"/> is open (set by <see cref="WorldObjectComp_SettlementMilitary.EndBattle"/>
    /// around its per-op dispatch loop), each op's outcome is appended as a fragment and one
    /// condensed letter is flushed afterwards. When no accumulator is open (single-op auto-resolve,
    /// or the <c>OnManualResolve</c> fallback path) the op emits its own letter immediately. Either
    /// way the overwhelming-victory / crushing-defeat flavor is folded into that one letter.</para>
    /// </summary>
    internal static class DefensiveBattleEffects
    {
        /// <summary>Set true once a result letter is sent (immediate path or accumulator flush).
        /// <see cref="WorldObjectComp_SettlementMilitary.EndBattle"/> resets this at entry and
        /// reads it after the per-op dispatch loop (post-flush) to detect a silent skip.</summary>
        internal static bool letterEmitted;

        /// <summary>Open multi-op aggregation context. Non-null only between
        /// <see cref="WorldObjectComp_SettlementMilitary.EndBattle"/> opening it and
        /// <see cref="FlushAccumulator"/> tearing it down. While set, <see cref="ApplyWin"/> /
        /// <see cref="ApplyLoss"/> append fragments to it instead of emitting a letter.</summary>
        internal static DefenseLetterAccumulator activeAccumulator;

        /// <summary>Looks up the per-tile <see cref="BattlefieldContext"/> directly from the manager
        /// so this helper doesn't depend on the comp's private <c>Battlefield</c> backdoor.</summary>
        private static BattlefieldContext BattlefieldFor(WorldSettlementFC settlement)
            => FindFC.MilitaryManager?.GetBattlefield(settlement?.Tile ?? PlanetTile.Invalid);

        public static void ApplyWin(WorldSettlementFC settlement, MilitaryOperation op = null, BattleResult result = null)
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction is null) return;

            faction.AddExperienceToFactionLevel(5f);
            // Threat adaptation runs in MilitaryOperation.CompleteBattle for every Empire battle.

            bool overwhelming = result is object && result.IsOverwhelmingVictory;
            // Overwhelming-victory reward credits the squad's home settlement (matches the old
            // CompleteBattle behavior); falls back to the defended settlement for ghost defenses.
            WorldSettlementFC rewardHome = op?.defender?.homeSettlement ?? settlement;
            int reportId = BattleArchiveUtil.ArchiveAndGetId(op, op?.result, BattleOperationKind.Defense);

            string attackerName = AttackerName(op, result);

            if (activeAccumulator is object)
            {
                double hap = 0.0, loy = 0.0;
                if (overwhelming)
                    (hap, loy) = MilitaryLetterUtil.GainOverwhelmingVictoryReward(rewardHome);
                activeAccumulator.AddWin(op, overwhelming, reportId, attackerName, hap, loy);
                return;
            }

            // No accumulator open — emit this op's letter immediately, folding OV flavor in.
            string body = "FCDefenseSuccessfulFull".Translate(settlement.Name);
            if (overwhelming)
            {
                body += "\n\n" + "FCOverwhelmingVictoryDesc".Translate();
                MilitaryLetterUtil.ApplyOverwhelmingVictoryReward(rewardHome, ref body);
            }
            AppendSharedTail(settlement, ref body);
            EmitLetter(
                overwhelming ? "FCOverwhelmingVictory".Translate() : "FCDefenseSuccessful".Translate(),
                body,
                FCLetterDefOf.FCBattleReportLetterPositive,
                settlement, new List<int> { reportId }, op);
        }

        public static void ApplyLoss(WorldSettlementFC settlement, MilitaryOperation op = null, BattleResult result = null)
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction is null) return;

            double happinessLostMultiplier = settlement.GetStatValue(FCStatDefOf.happinessLostMultiplier);
            double loyaltyLostMultiplier = settlement.GetStatValue(FCStatDefOf.loyaltyLostMultiplier);

            var (prosperityLoss, happinessLoss, loyaltyLoss) = SettlementFormulas.CalculateBattleLossPenalties(
                happinessLostMultiplier, loyaltyLostMultiplier,
                settlement.GetStatValue(FCStatDefOf.battleLossProsperityBase),
                settlement.GetStatValue(FCStatDefOf.battleLossHappinessBase),
                settlement.GetStatValue(FCStatDefOf.battleLossLoyaltyBase));
            prosperityLoss *= faction.GetStatValue(FCStatDefOf.battleProsperityLossMultiplier);
            happinessLoss *= faction.GetStatValue(FCStatDefOf.battleHappinessLossMultiplier);
            loyaltyLoss *= faction.GetStatValue(FCStatDefOf.battleLoyaltyLossMultiplier);
            bool canDestroyBuildings = !FindFC.FactionComp.IsBuildingDestructionPrevented();

            // buildingDestructionChance stat scales the survival threshold:
            //  stat=1.0 -> threshold 7 (36% destruction, default)
            //  stat<1.0 -> higher threshold (less destruction)
            //  stat>1.0 -> lower threshold (more destruction)
            double destructionStat = faction.GetStatValue(FCStatDefOf.buildingDestructionChance);

            // Crushing-defeat amplifier: a defensive loss with zero enemy casualties
            // multiplies the settlement-side penalty set. Mirrors the OV cooldown skip on the
            // winning side. Applied before the destruction-chance threshold is computed so the
            // boosted destruction stat also drives the level-demotion roll below.
            bool isCrushingDefeat = result is object && result.IsCrushingDefeatForDefender;
            if (isCrushingDefeat)
            {
                float pm = FCSettings.crushingDefeatPenaltyMultiplier;
                if (pm > 1f)
                {
                    prosperityLoss *= pm;
                    happinessLoss *= pm;
                    loyaltyLoss *= pm;
                    destructionStat *= pm;
                }
            }

            int deconstructChance = Math.Max(0, Math.Min(11, (int)Math.Round(11 - 4 * destructionStat)));

            settlement.prosperity -= prosperityLoss;
            settlement.happiness -= happinessLoss;
            settlement.loyalty -= loyaltyLoss;

            // Per-op penalty fragment: the bulleted breakdown only. The shared header
            // (FCDefenseFailureFull), the "penalties suffered" lead-in, and the delivery / leave-map
            // tail are added once by the emit path (immediate letter or accumulator flush).
            string fragment = "";

            int displayProsperity = (int)Math.Round(prosperityLoss);
            int displayHappiness = (int)Math.Round(happinessLoss);
            int displayLoyalty = (int)Math.Round(loyaltyLoss);

            if (displayProsperity > 0) fragment += "\n  - " + "FCDefenseFailureProsperityLoss".Translate(displayProsperity);
            if (displayHappiness > 0) fragment += "\n  - " + "FCDefenseFailureHappinessLoss".Translate(displayHappiness);
            if (displayLoyalty > 0) fragment += "\n  - " + "FCDefenseFailureLoyaltyLoss".Translate(displayLoyalty);

            if (canDestroyBuildings && settlement?.BuildingsComp != null)
            {
                List<int> candidates = new List<int>();
                for (int k = 0; k < settlement.BuildingsComp.NumBuildingSlots; k++)
                {
                    int deconstructRoll = new IntRange(0, 10).RandomInRange;
                    if (deconstructRoll < deconstructChance
                        || !settlement.BuildingsComp.BuildingSlotIsBuilding(k)) continue;
                    candidates.Add(k);
                }

                // Sort so buildings that depend on other buildings are demolished first.
                candidates.Sort((a, b) =>
                {
                    BuildingFCDef defA = settlement.BuildingsComp.GetBuildingInSlot(a);
                    BuildingFCDef defB = settlement.BuildingsComp.GetBuildingInSlot(b);
                    // candidates is pre-filtered to building slots, so these are normally
                    // non-null. Guard anyway since the comparator dereferences both.
                    if (defA is null || defB is null) return 0;
                    bool aRequiresB = FactionCache.SatisfiesAnyRequirement(defB, defA.requiredBuildings);
                    bool bRequiresA = FactionCache.SatisfiesAnyRequirement(defA, defB.requiredBuildings);
                    if (aRequiresB) return -1;
                    if (bRequiresA) return 1;
                    int aReqCount = defA.requiredBuildings?.Count ?? 0;
                    int bReqCount = defB.requiredBuildings?.Count ?? 0;
                    return bReqCount.CompareTo(aReqCount);
                });

                foreach (int k in candidates)
                {
                    fragment += "\n  - " + "FCBuildingDestroyedInRaid".Translate(settlement.BuildingsComp.BuildingLabel(k));
                    settlement.DeconstructBuilding(k);
                }
            }

            if (!canDestroyBuildings)
                fragment += "\n  - " + "FCDefenseFailureBuildingsProtected".Translate();

            // Level remover roll — uses the same destruction stat scaling.
            if (settlement?.settlementLevel > 1 && canDestroyBuildings)
            {
                int num = new IntRange(0, 10).RandomInRange;
                if (num >= deconstructChance)
                {
                    fragment += "\n  - " + "FCSettlementDeleveledRaid".Translate();
                    settlement.DelevelSettlement();
                }
            }

            int reportId = BattleArchiveUtil.ArchiveAndGetId(op, op?.result, BattleOperationKind.Defense);
            string attackerName = AttackerName(op, result);

            if (activeAccumulator is object)
            {
                activeAccumulator.AddLoss(op, isCrushingDefeat, reportId, attackerName, fragment);
                return;
            }

            // No accumulator open — emit this op's letter immediately, folding CD flavor in.
            string body = "FCDefenseFailureFull".Translate(settlement.Name);
            if (isCrushingDefeat)
                body += "\n\n" + "FCCrushingDefeatDesc".Translate();
            body += "\n\n" + "FCDefenseFailurePenaltiesHeader".Translate() + fragment;
            AppendSharedTail(settlement, ref body);
            EmitLetter(
                isCrushingDefeat ? "FCCrushingDefeat".Translate() : "FCDefenseFailure".Translate(),
                body,
                FCLetterDefOf.FCBattleReportLetterNegative,
                settlement, new List<int> { reportId }, op);
        }

        /// <summary>
        /// Builds and sends the single condensed letter for every defensive op that resolved under
        /// the current <see cref="activeAccumulator"/>, then clears it. Called by
        /// <see cref="WorldObjectComp_SettlementMilitary.EndBattle"/> after its per-op dispatch loop.
        /// No-op (and leaves <see cref="letterEmitted"/> false) if no op contributed.
        /// </summary>
        public static void FlushAccumulator()
        {
            DefenseLetterAccumulator acc = activeAccumulator;
            activeAccumulator = null;
            if (acc is null || acc.attackCount == 0) return;

            WorldSettlementFC settlement = acc.settlement;
            bool won = acc.won;
            bool multi = acc.attackCount > 1;

            string label;
            LetterDef def;
            if (won)
            {
                label = (acc.overwhelming ? "FCOverwhelmingVictory" : "FCDefenseSuccessful").Translate();
                def = FCLetterDefOf.FCBattleReportLetterPositive;
            }
            else
            {
                label = (acc.overwhelming ? "FCCrushingDefeat" : "FCDefenseFailure").Translate();
                def = FCLetterDefOf.FCBattleReportLetterNegative;
            }

            string body = (won ? "FCDefenseSuccessfulFull" : "FCDefenseFailureFull").Translate(settlement.Name);

            if (acc.overwhelming)
            {
                body += "\n\n" + (won ? "FCOverwhelmingVictoryDesc" : "FCCrushingDefeatDesc").Translate();
                if (won)
                {
                    string rewardLine = MilitaryLetterUtil.FormatOverwhelmingVictoryRewardLine(
                        settlement, acc.totalOvHappiness, acc.totalOvLoyalty);
                    if (!string.IsNullOrEmpty(rewardLine)) body += "\n\n" + rewardLine;
                }
            }

            if (multi)
                body += "\n\n" + "FCDefenseMultiAttackHeader".Translate(settlement.Name, acc.attackCount);

            foreach (DefenseLetterAccumulator.Entry e in acc.entries)
            {
                if (won)
                {
                    // A win has no per-op detail; list each repelled attack only when there
                    // were several. A lone win needs no per-attack line.
                    if (multi)
                        body += "\n\n" + "FCDefenseAttackRepelledEntry".Translate(e.attackerName);
                }
                else
                {
                    body += "\n\n" + (multi
                        ? "FCDefenseAttackPenaltiesEntry".Translate(e.attackerName)
                        : "FCDefenseFailurePenaltiesHeader".Translate());
                    body += e.detail;
                }
            }

            AppendSharedTail(settlement, ref body);
            EmitLetter(label, body, def, settlement, acc.reportIds, acc.representativeOp);
        }

        /* -*-*-*-*- Shared helpers -*-*-*-*- */

        private static void EmitLetter(string label, string body, LetterDef def,
            WorldSettlementFC settlement, List<int> reportIds, MilitaryOperation op)
        {
            MilitaryLetterUtil.SendBattleReportLetter(label, body, def,
                new LookTargets(settlement), reportIds, op);
            letterEmitted = true;
        }

        /// <summary>Appends the pending-delivery message and the "battle over, leave the map"
        /// note — both shared across every op resolved in one battle.</summary>
        private static void AppendSharedTail(WorldSettlementFC settlement, ref string body)
        {
            string deliveryMsg = BattlefieldFor(settlement)?.pendingDeliveryMessage;
            if (!string.IsNullOrEmpty(deliveryMsg))
                body += "\n\n" + deliveryMsg;
            if (settlement.Map != null)
                body += "\n\n" + "FCDefenseBattleOverLeaveMap".Translate();
        }

        private static string AttackerName(MilitaryOperation op, BattleResult result)
            => op?.aggressor?.faction?.Name
               ?? result?.attackerFactionName
               ?? result?.attackerLabel
               ?? "Unknown";
    }

    /// <summary>
    /// Collects the per-op outcome fragments of every defensive op resolving together on one
    /// battlefield, so <see cref="DefensiveBattleEffects.FlushAccumulator"/> can emit a single
    /// condensed letter holding all attacks' results with one "View battle report" button per
    /// attack. All ops in one manual battle share the same win/loss outcome (the settlement
    /// either held or fell), so <see cref="won"/> is fixed at construction.
    /// </summary>
    internal class DefenseLetterAccumulator
    {
        internal struct Entry
        {
            public string attackerName;
            public string detail; // loss: bulleted penalty breakdown; win: unused.
        }

        public readonly WorldSettlementFC settlement;
        public readonly bool won;

        /// <summary>True once any contributing op was an overwhelming victory (win) or crushing
        /// defeat (loss). All ops share defender force counts, so this is normally uniform.</summary>
        public bool overwhelming;

        public int attackCount;
        public readonly List<Entry> entries = new List<Entry>();
        public readonly List<int> reportIds = new List<int>();
        public MilitaryOperation representativeOp;

        public double totalOvHappiness;
        public double totalOvLoyalty;

        public DefenseLetterAccumulator(WorldSettlementFC settlement, bool won)
        {
            this.settlement = settlement;
            this.won = won;
        }

        public void AddWin(MilitaryOperation op, bool overwhelming, int reportId,
            string attackerName, double ovHappiness, double ovLoyalty)
        {
            attackCount++;
            if (representativeOp is null) representativeOp = op;
            if (reportId > 0) reportIds.Add(reportId);
            if (overwhelming) this.overwhelming = true;
            totalOvHappiness += ovHappiness;
            totalOvLoyalty += ovLoyalty;
            entries.Add(new Entry { attackerName = attackerName, detail = "" });
        }

        public void AddLoss(MilitaryOperation op, bool crushing, int reportId,
            string attackerName, string detail)
        {
            attackCount++;
            if (representativeOp is null) representativeOp = op;
            if (reportId > 0) reportIds.Add(reportId);
            if (crushing) overwhelming = true;
            entries.Add(new Entry { attackerName = attackerName, detail = detail });
        }
    }
}
