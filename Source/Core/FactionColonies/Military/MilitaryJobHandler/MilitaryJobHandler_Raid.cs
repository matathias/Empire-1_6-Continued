namespace FactionColonies;

public class MilitaryJobHandler_Raid : MilitaryJobHandler_Offensive
{
    protected override FCEventDef ArrivalEvent => FCEventDefOf.raidEnemySettlement;
    protected override string TravelingDescKey => "FCSettlementMilitaryForcesRaiding";
    protected override string SentLetterKey => "FCMilitarySentRaid";
    protected override string TargetLogLabel => "raid";
    protected override BattleOperationKind ReportKind => BattleOperationKind.Raid;
    protected override string FailureLabelKey => "FCRaidFailure";

    protected override string FailureBody(MilitaryOperation op, Settlement target) =>
        "FCRaidEnemySettlementFailure".Translate(target.LabelCap);

    // Any result other than an attacker victory (including an errored battle) reports a failure.
    protected override bool SendsFailureReport(BattleResult result) => true;

    protected override void ApplyVictory(MilitaryOperation op, BattleResult result, Settlement target) =>
        ApplyVictoryToTarget(FindFC.FactionComp, op.aggressor.homeSettlement,
            op.defender?.faction, target, op, result);

    private static void LootByTech(TechLevel tech, ref int lootLevel, ref bool getSlaves)
    {
        lootLevel = tech switch
        {
            TechLevel.Archotech or TechLevel.Ultra or TechLevel.Spacer => 4,
            TechLevel.Industrial => 3,
            TechLevel.Medieval or TechLevel.Neolithic => 2,
            _ => 1,
        };
    }
    private static void LootByFactionDef(FactionDef def, ref int lootLevel, ref bool getSlaves)
    {
        if (def.defName == "Insect")
        {
            lootLevel = 3;
            getSlaves = false;
        }
    }

    /* Shared victory side effects: loot, prisoners, XP, delivery event. Called from ApplyVictory,
     * and from the capture/raze failed-destruction fallback. The overwhelming-victory flavor +
     * reward is folded into this letter rather than sent separately. */
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
        if (num <= 4 && getSlaves && enemyFaction is not null)
        {
            Pawn prisoner = PaymentUtil.GeneratePrisoner(enemyFaction);
            text += "FCPrisonerCaptureInfo".Translate(prisoner.Name.ToString(), home.Name);
            home.PrisonerComp?.AddPrisoner(prisoner);
        }

        string body = "FCRaidEnemySettlementSuccess".Translate(target.LabelCap) + "\n" + text;
        AppendOverwhelmingVictory(result, home, ref body);

        int reportId = BattleArchiveUtil.ArchiveAndGetId(op, op?.result, BattleOperationKind.Raid);
        MilitaryLetterUtil.SendBattleReportLetter("FCRaidLoot".Translate(),
            body, FCLetterDefOf.FCBattleReportLetterPositive,
            new LookTargets(target), reportId, op);

        // Nomad/caravan-phase players have no home colony map to deliver loot to; skip the
        // delivery (the victory letter above already fired) rather than NRE on the null map.
        Map playerHome = Find.AnyPlayerHomeMap;
        if (playerHome is not null)
        {
            FCEvent eventParams = new()
            {
                location = playerHome.Tile,
                source = home.Tile,
                goods = loot,
                customDescription = text,
                timeTillTrigger = Find.TickManager.TicksGame + TravelUtil.ReturnTicksToArrive(home.Tile, playerHome.Tile)
            };
            DeliveryEvent.CreateDeliveryEvent(eventParams);
        }
    }
}
