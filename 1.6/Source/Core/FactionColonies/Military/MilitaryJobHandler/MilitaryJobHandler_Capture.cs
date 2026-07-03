using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using System.Linq;
using Verse;

namespace FactionColonies
{
    public class MilitaryJobHandler_Capture : MilitaryJobHandler
    {
        public override void OnOpCreated(MilitaryOperation op)
        {
            WorldSettlementFC home = op.aggressor?.homeSettlement;
            if (home is null) return;

            int travelTicks = System.Math.Max(0, op.nextPhaseTick - Find.TickManager.TicksGame);
            string desc = "FCSettlementMilitaryForcesCapturing".Translate(home.Name, op.targetObject?.Label ?? "").ToString();

            op.ScheduleEvent(FCEventDefOf.captureEnemySettlement, home.Tile, travelTicks, desc);

            Settlement targetSettlement = op.targetObject as Settlement
                ?? Find.WorldObjects.SettlementAt(op.targetTile);
            Find.LetterStack.ReceiveLetter("FCMilitaryAction".Translate(),
                "FCMilitarySentCapture".Translate(home.Name, targetSettlement?.LabelCap ?? (TaggedString)""),
                LetterDefOf.NeutralEvent);
        }

        public override void ApplyResult(MilitaryOperation op, BattleResult result)
        {
            if (result is null || op.aggressor?.homeSettlement is null) return;

            Settlement target = op.targetObject as Settlement
                ?? Find.WorldObjects.SettlementAt(op.targetTile);
            if (target is null)
            {
                LogUtil.Warning("Military capture target at tile " + op.targetTile + " no longer exists");
                return;
            }

            if (result.AttackerVictory)
            {
                ApplyCaptureSuccess(FindFC.FactionComp, op.aggressor.homeSettlement, op.targetTile, target, op, result);
            }
            else if (result.DefenderVictory)
            {
                string body = "FCCaptureEnemySettlementFailure".Translate(op.aggressor.homeSettlement.Name, target.Name);
                // Fold the crushing-defeat flavor into this single failure letter rather than
                // sending a separate Crushing Defeat letter.
                if (MilitaryLetterUtil.IsCrushingLoss(result, playerWon: false))
                    body += "\n\n" + "FCCrushingDefeatDesc".Translate();
                int reportId = BattleArchiveUtil.ArchiveAndGetId(op, result, BattleOperationKind.Capture);
                MilitaryLetterUtil.SendBattleReportLetter("FCCaptureSettlement".Translate(),
                    body, FCLetterDefOf.FCBattleReportLetterNegative,
                    new LookTargets(target), reportId, op);
            }
        }

        /* Shared capture-victory side effects: destroy the target settlement, replace it with a
         * player-owned WorldSettlementFC, and configure starting prosperity/loyalty. The
         * overwhelming-victory flavor + reward is folded into the success letter. */
        private static void ApplyCaptureSuccess(FactionFC faction, WorldSettlementFC home,
            PlanetTile capturedTile, Settlement target, MilitaryOperation op = null, BattleResult result = null)
        {
            string tmpName = target.LabelCap;
            TechLevel tech = target.Faction.def.techLevel;
            Faction tempFactionLink = target.Faction;
            target.Destroy();

            // Mod-protected settlements (Empire's own WorldSettlementFC, or third-party
            // protected settlements) might Harmony-patch Destroy to no-op. Detect both common shapes:
            // a prefix-return-false leaves Destroyed=false; a postfix that re-adds the object
            // leaves Destroyed=true but the same instance still resolves at the tile.
            bool destructionSucceeded = target.Destroyed
                && Find.WorldObjects.SettlementAt(capturedTile) != target;
            if (!destructionSucceeded)
            {
                LogUtil.Warning($"Capture: target settlement at {capturedTile} survived Destroy(); " +
                                "likely destruction-protected. Falling back to raid rewards.");
                ApplyCaptureFallbackToRaid(faction, home, tempFactionLink, target, op, result);
                return;
            }

            // XP grant moved past the destruction check so the fallback path doesn't double up
            // (ApplyVictoryToTarget grants its own +5f).
            faction.AddExperienceToFactionLevel(5f);

            WorldSettlementFC worldsettlement = ColonyUtil.SetupCapturedSettlement(capturedTile, tmpName, tech);

            bool defeated = !Find.WorldObjects.Settlements.Any(settlement => settlement.Faction != null
                && settlement.Faction == tempFactionLink);

            if (defeated)
            {
                tempFactionLink.defeated = true;
            }

            string body = "FCCaptureEnemySettlementSuccess".Translate(home.Name, worldsettlement.Name, worldsettlement.settlementLevel);

            // Fold the overwhelming-victory flavor + happiness/loyalty reward into this single
            // letter rather than sending a separate Overwhelming Victory letter.
            if (MilitaryLetterUtil.IsOverwhelmingWin(result, playerWon: true))
            {
                body += "\n\n" + "FCOverwhelmingVictoryDesc".Translate();
                MilitaryLetterUtil.ApplyOverwhelmingVictoryReward(home, ref body);
            }

            int reportId = BattleArchiveUtil.ArchiveAndGetId(op, op?.result, BattleOperationKind.Capture);
            MilitaryLetterUtil.SendBattleReportLetter("FCCaptureSettlement".Translate(),
                body, FCLetterDefOf.FCBattleReportLetterPositive,
                new LookTargets(worldsettlement), reportId, op);
        }

        /* Failed-capture fallback: target's Destroy() was blocked by another mod, but the squad
         * won the battle. Send a "couldn't permanently neutralize, raided supplies instead" letter
         * and route through Raid's victory side effects (loot + optional prisoner + delivery). */
        private static void ApplyCaptureFallbackToRaid(FactionFC faction, WorldSettlementFC home,
            Faction enemyFaction, Settlement target, MilitaryOperation op = null, BattleResult result = null)
        {
            // The fallback letter doesn't archive — it's a one-line "fallback" notice. The
            // raid victory below archives via Raid.ApplyVictoryToTarget's letter, which
            // is the one with the meaningful battle report context (and folds in OV flavor).
            Find.LetterStack.ReceiveLetter(
                "FCCaptureSettlement".Translate(),
                "FCCaptureBlockedFallbackToRaid".Translate(home.Name, target.LabelCap),
                LetterDefOf.NeutralEvent, new LookTargets(target));
            MilitaryJobHandler_Raid.ApplyVictoryToTarget(faction, home, enemyFaction, target, op, result);
        }
    }
}
