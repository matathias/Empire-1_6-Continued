using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FactionColonies
{
    public class MilitaryJobHandler_Raid : MilitaryJobHandler_Offensive
    {
        public override void OnOpCreated(MilitaryOperation op)
        {
            WorldSettlementFC home = op.aggressor?.homeSettlement;
            if (home is null) return;

            int travelTicks = System.Math.Max(0, op.nextPhaseTick - Find.TickManager.TicksGame);
            string desc = "FCSettlementMilitaryForcesRaiding".Translate(home.Name, op.targetObject?.Label ?? "").ToString();

            op.ScheduleEvent(FCEventDefOf.raidEnemySettlement, home.Tile, travelTicks, desc);

            Settlement targetSettlement = op.targetObject as Settlement
                ?? Find.WorldObjects.SettlementAt(op.targetTile);
            Find.LetterStack.ReceiveLetter("FCMilitaryAction".Translate(),
                "FCMilitarySentRaid".Translate(home.Name, targetSettlement?.LabelCap ?? (TaggedString)""),
                LetterDefOf.NeutralEvent);
        }

        public override void ApplyResult(MilitaryOperation op, BattleResult result)
        {
            if (result is null || op.aggressor?.homeSettlement is null) return;

            Settlement target = op.targetObject as Settlement
                ?? Find.WorldObjects.SettlementAt(op.targetTile);
            if (target is null)
            {
                LogUtil.Warning("Military raid target at tile " + op.targetTile + " no longer exists");
                return;
            }

            if (result.AttackerVictory)
            {
                ApplyVictoryToTarget(FindFC.FactionComp, op.aggressor.homeSettlement,
                    op.defender?.faction, target, op, result);
            }
            else
            {
                string body = "FCRaidEnemySettlementFailure".Translate(target.LabelCap);
                // Fold the crushing-defeat flavor into this single failure letter rather than
                // sending a separate Crushing Defeat letter.
                if (MilitaryLetterUtil.IsCrushingLoss(result, playerWon: false))
                    body += "\n\n" + "FCCrushingDefeatDesc".Translate();
                int reportId = BattleArchiveUtil.ArchiveAndGetId(op, result, BattleOperationKind.Raid);
                MilitaryLetterUtil.SendBattleReportLetter("FCRaidFailure".Translate(),
                    body, FCLetterDefOf.FCBattleReportLetterNegative,
                    new LookTargets(target), reportId, op);
            }
        }

        private static void LootByTech(TechLevel tech, ref int lootLevel, ref bool getSlaves)
        {
            switch (tech)
            {
                case TechLevel.Archotech:
                case TechLevel.Ultra:
                case TechLevel.Spacer:
                    lootLevel = 4;
                    break;
                case TechLevel.Industrial:
                    lootLevel = 3;
                    break;
                case TechLevel.Medieval:
                case TechLevel.Neolithic:
                    lootLevel = 2;
                    break;
                default:
                    lootLevel = 1;
                    break;
            }
        }
        private static void LootByFactionDef(FactionDef def, ref int lootLevel, ref bool getSlaves)
        {
            if (def.defName == "Insect")
            {
                lootLevel = 3;
                getSlaves = false;
            }
        }

        /* Shared victory side effects: loot, prisoners, XP, delivery event. Called from ApplyResult,
         * and from MilitaryJobHandler_Capture's failed-destruction fallback. The overwhelming-
         * victory flavor + reward is folded into this letter rather than sent separately. */
        internal static void ApplyVictoryToTarget(FactionFC faction, WorldSettlementFC home, Faction enemyFaction, Settlement target, MilitaryOperation op = null, BattleResult result = null)
        {
            faction.AddExperienceToFactionLevel(5f);

            TechLevel tech = target.Faction.def.techLevel;
            int lootLevel = 1;
            bool getSlaves = true;

            LootByTech(tech, ref lootLevel, ref getSlaves);
            LootByFactionDef(target.Faction.def, ref lootLevel, ref getSlaves);

            List<Thing> loot = PaymentUtil.GenerateRaidLoot(lootLevel, tech);

            string text = "FCSettlementDeliveringLoot".Translate();
            text = loot.Aggregate(text, (current, thing) => current + thing.LabelCap + " " + thing.stackCount + "x\n ");

            int num = new IntRange(0, 10).RandomInRange;
            if (num <= 4 && getSlaves && enemyFaction is object)
            {
                Pawn prisoner = PaymentUtil.GeneratePrisoner(enemyFaction);
                text += "FCPrisonerCaptureInfo".Translate(prisoner.Name.ToString(), home.Name);
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

            int reportId = BattleArchiveUtil.ArchiveAndGetId(op, op?.result, BattleOperationKind.Raid);
            MilitaryLetterUtil.SendBattleReportLetter("FCRaidLoot".Translate(),
                body, FCLetterDefOf.FCBattleReportLetterPositive,
                new LookTargets(target), reportId, op);

            FCEvent eventParams = new FCEvent()
            {
                location = Find.AnyPlayerHomeMap.Tile,
                source = home.Tile,
                goods = loot,
                customDescription = text,
                timeTillTrigger = Find.TickManager.TicksGame + TravelUtil.ReturnTicksToArrive(home.Tile, Find.AnyPlayerHomeMap.Tile)
            };
            DeliveryEvent.CreateDeliveryEvent(eventParams);
        }
    }
}
