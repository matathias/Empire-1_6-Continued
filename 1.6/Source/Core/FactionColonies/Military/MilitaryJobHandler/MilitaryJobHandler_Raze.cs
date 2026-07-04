using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using System.Linq;
using Verse;

namespace FactionColonies
{
    public class MilitaryJobHandler_Raze : MilitaryJobHandler_Offensive
    {
        // Raze's ApplyResult destroys the host Settlement outright (no replacement colony), so a
        // win tears the live offense map down under the player just like Capture. Force the
        // immediate-teardown branch on a Raze win.
        public override bool SkipLootLingerOnWin => true;

        public override void OnOpCreated(MilitaryOperation op)
        {
            WorldSettlementFC home = op.aggressor?.homeSettlement;
            if (home is null) return;

            int travelTicks = System.Math.Max(0, op.nextPhaseTick - Find.TickManager.TicksGame);
            string desc = "FCSettlementMilitaryForcesRazing".Translate(home.Name, op.targetObject?.Label ?? "").ToString();

            op.ScheduleEvent(FCEventDefOf.razeEnemySettlement, home.Tile, travelTicks, desc);

            Settlement targetSettlement = op.targetObject as Settlement
                ?? Find.WorldObjects.SettlementAt(op.targetTile);
            Find.LetterStack.ReceiveLetter("FCMilitaryAction".Translate(),
                "FCMilitarySentRaze".Translate(home.Name, targetSettlement?.LabelCap ?? (TaggedString)""),
                LetterDefOf.NeutralEvent);
        }

        public override void ApplyResult(MilitaryOperation op, BattleResult result)
        {
            if (result is null || op.aggressor?.homeSettlement is null) return;

            Settlement target = op.targetObject as Settlement
                ?? Find.WorldObjects.SettlementAt(op.targetTile);
            if (target is null)
            {
                LogUtil.Warning("Military raze target at tile " + op.targetTile + " no longer exists");
                return;
            }

            if (result.AttackerVictory)
            {
                ApplyRazeSuccess(FindFC.FactionComp, op.aggressor.homeSettlement, op.targetTile, target, op, result);
            }
            else if (result.DefenderVictory)
            {
                string body = "FCRazeEnemySettlementFailure".Translate(target.Name);
                // Fold the crushing-defeat flavor into this single failure letter rather than
                // sending a separate Crushing Defeat letter.
                if (MilitaryLetterUtil.IsCrushingLoss(result, playerWon: false))
                    body += "\n\n" + "FCCrushingDefeatDesc".Translate();
                int reportId = BattleArchiveUtil.ArchiveAndGetId(op, result, BattleOperationKind.Raze);
                MilitaryLetterUtil.SendBattleReportLetter("FCRazeSettlement".Translate(),
                    body, FCLetterDefOf.FCBattleReportLetterNegative,
                    new LookTargets(target), reportId, op);
            }
        }

        /* Shared raze-victory side effects: destroy the target settlement outright (no replacement),
         * defeat the former owner if it was their last settlement, and grant faction XP. Scorched
         * earth: no loot. The overwhelming-victory flavor + reward is folded into the success letter. */
        private static void ApplyRazeSuccess(FactionFC faction, WorldSettlementFC home,
            PlanetTile razedTile, Settlement target, MilitaryOperation op = null, BattleResult result = null)
        {
            string tmpName = target.LabelCap;
            Faction tempFactionLink = target.Faction;
            target.Destroy();

            // Mod-protected settlements (Empire's own WorldSettlementFC, or third-party
            // protected settlements) might Harmony-patch Destroy to no-op. Detect both common shapes:
            // a prefix-return-false leaves Destroyed=false; a postfix that re-adds the object
            // leaves Destroyed=true but the same instance still resolves at the tile.
            bool destructionSucceeded = target.Destroyed
                && Find.WorldObjects.SettlementAt(razedTile) != target;
            if (!destructionSucceeded)
            {
                LogUtil.Warning($"Raze: target settlement at {razedTile} survived Destroy(); " +
                                "likely destruction-protected. Falling back to raid rewards.");
                ApplyRazeFallbackToRaid(faction, home, tempFactionLink, target, op, result);
                return;
            }

            // XP grant moved past the destruction check so the fallback path doesn't double up
            // (ApplyVictoryToTarget grants its own +5f).
            faction.AddExperienceToFactionLevel(5f);

            bool defeated = !Find.WorldObjects.Settlements.Any(settlement => settlement.Faction != null
                && settlement.Faction == tempFactionLink);

            if (defeated)
            {
                tempFactionLink.defeated = true;
            }

            string body = "FCRazeEnemySettlementSuccess".Translate(home.Name, tmpName);

            // Fold the overwhelming-victory flavor + happiness/loyalty reward into this single
            // letter rather than sending a separate Overwhelming Victory letter.
            if (MilitaryLetterUtil.IsOverwhelmingWin(result, playerWon: true))
            {
                body += "\n\n" + "FCOverwhelmingVictoryDesc".Translate();
                MilitaryLetterUtil.ApplyOverwhelmingVictoryReward(home, ref body);
            }

            int reportId = BattleArchiveUtil.ArchiveAndGetId(op, op?.result, BattleOperationKind.Raze);
            // The target object is gone; anchor the letter to its former tile.
            MilitaryLetterUtil.SendBattleReportLetter("FCRazeSettlement".Translate(),
                body, FCLetterDefOf.FCBattleReportLetterPositive,
                new LookTargets(razedTile), reportId, op);
        }

        /* Failed-raze fallback: target's Destroy() was blocked by another mod, but the squad won
         * the battle. Send a "couldn't burn it down, plundered supplies instead" letter and route
         * through Raid's victory side effects (loot + optional prisoner + delivery). */
        private static void ApplyRazeFallbackToRaid(FactionFC faction, WorldSettlementFC home,
            Faction enemyFaction, Settlement target, MilitaryOperation op = null, BattleResult result = null)
        {
            // The fallback letter doesn't archive — it's a one-line "fallback" notice. The
            // raid victory below archives via Raid.ApplyVictoryToTarget's letter, which
            // is the one with the meaningful battle report context (and folds in OV flavor).
            Find.LetterStack.ReceiveLetter(
                "FCRazeSettlement".Translate(),
                "FCRazeBlockedFallbackToRaid".Translate(home.Name, target.LabelCap),
                LetterDefOf.NeutralEvent, new LookTargets(target));
            MilitaryJobHandler_Raid.ApplyVictoryToTarget(faction, home, enemyFaction, target, op, result);
        }
    }
}
