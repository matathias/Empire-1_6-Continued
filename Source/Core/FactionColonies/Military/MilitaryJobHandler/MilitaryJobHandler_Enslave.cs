using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FactionColonies
{
    public class MilitaryJobHandler_Enslave : MilitaryJobHandler_Offensive
    {
        public override bool IsValidTarget(Faction targetFaction) => targetFaction?.def?.defName != "Insect";

        public override void OnOpCreated(MilitaryOperation op)
        {
            WorldSettlementFC home = op.aggressor?.homeSettlement;
            if (home is null) return;

            int travelTicks = System.Math.Max(0, op.nextPhaseTick - Find.TickManager.TicksGame);
            string desc = "FCSettlementMilitaryForcesEnslave".Translate(home.Name, op.targetObject?.Label ?? "").ToString();

            op.ScheduleEvent(FCEventDefOf.enslaveEnemySettlement, home.Tile, travelTicks, desc);

            Settlement targetSettlement = op.targetObject as Settlement
                ?? Find.WorldObjects.SettlementAt(op.targetTile);
            Find.LetterStack.ReceiveLetter("FCMilitaryAction".Translate(),
                "FCMilitarySentEnslave".Translate(home.Name, targetSettlement?.LabelCap ?? (TaggedString)""),
                LetterDefOf.NeutralEvent);
        }

        public override void ApplyResult(MilitaryOperation op, BattleResult result)
        {
            if (result is null || op.aggressor?.homeSettlement is null) return;

            Settlement target = op.targetObject as Settlement
                ?? Find.WorldObjects.SettlementAt(op.targetTile);
            if (target is null)
            {
                LogUtil.Warning("Military enslave target at tile " + op.targetTile + " no longer exists");
                return;
            }

            if (result.AttackerVictory)
            {
                ApplyEnslaveSuccess(FindFC.FactionComp, op.aggressor.homeSettlement,
                    op.defender?.faction, target, op, result);
            }
            else if (result.DefenderVictory)
            {
                string body = "FCRaidEnemySettlementFailure".Translate(target.LabelCap);
                // Fold the crushing-defeat flavor into this single failure letter rather than
                // sending a separate Crushing Defeat letter.
                if (MilitaryLetterUtil.IsCrushingLoss(result, playerWon: false))
                    body += "\n\n" + "FCCrushingDefeatDesc".Translate();
                int reportId = BattleArchiveUtil.ArchiveAndGetId(op, result, BattleOperationKind.Enslave);
                MilitaryLetterUtil.SendBattleReportLetter("FCRaidFailure".Translate(),
                    body, FCLetterDefOf.FCBattleReportLetterNegative,
                    new LookTargets(target), reportId, op);
            }
        }

        /* Shared enslave-victory side effects: 1-3 prisoners. The overwhelming-victory flavor +
         * reward is folded into the success letter. */
        private static void ApplyEnslaveSuccess(FactionFC faction, WorldSettlementFC home, Faction enemyFaction, Settlement target, MilitaryOperation op = null, BattleResult result = null)
        {
            faction.AddExperienceToFactionLevel(5f);

            string text = "";

            int num = new IntRange(1, 3).RandomInRange;
            for (int i = 0; i < num; i++)
            {
                Pawn prisoner = PaymentUtil.GeneratePrisoner(enemyFaction);
                text += "FCPrisonerCaptureInfo".Translate(prisoner.Name.ToString(), home.Name) + "\n";
                home.PrisonerComp?.AddPrisoner(prisoner);
            }

            string body = "FCRaidEnemySettlementSuccess".Translate(target.LabelCap) + "\n" + text;

            // Fold the overwhelming-victory flavor + happiness/loyalty reward into this single
            // letter rather than sending a separate Overwhelming Victory letter.
            if (MilitaryLetterUtil.IsOverwhelmingWin(result, playerWon: true))
            {
                body += "\n\n" + "FCOverwhelmingVictoryDesc".Translate();
                MilitaryLetterUtil.ApplyOverwhelmingVictoryReward(home, ref body);
            }

            int reportId = BattleArchiveUtil.ArchiveAndGetId(op, op?.result, BattleOperationKind.Enslave);
            MilitaryLetterUtil.SendBattleReportLetter("FCRaidLoot".Translate(),
                body, FCLetterDefOf.FCBattleReportLetterPositive,
                new LookTargets(target), reportId, op);
        }
    }
}
