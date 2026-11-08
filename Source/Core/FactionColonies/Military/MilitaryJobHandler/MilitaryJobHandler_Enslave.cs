namespace FactionColonies;

public class MilitaryJobHandler_Enslave : MilitaryJobHandler_Offensive
{
    public override bool IsValidTarget(Faction targetFaction) => targetFaction?.def?.defName != "Insect";

    protected override FCEventDef ArrivalEvent => FCEventDefOf.enslaveEnemySettlement;
    protected override string TravelingDescKey => "FCSettlementMilitaryForcesEnslave";
    protected override string SentLetterKey => "FCMilitarySentEnslave";
    protected override string TargetLogLabel => "enslave";
    protected override BattleOperationKind ReportKind => BattleOperationKind.Enslave;
    protected override string FailureLabelKey => "FCRaidFailure";

    protected override string FailureBody(MilitaryOperation op, Settlement target) =>
        "FCRaidEnemySettlementFailure".Translate(target.LabelCap);

    protected override void ApplyVictory(MilitaryOperation op, BattleResult result, Settlement target) =>
        ApplyEnslaveSuccess(FindFC.FactionComp, op.aggressor.homeSettlement,
            op.defender?.faction, target, op, result);

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
        AppendOverwhelmingVictory(result, home, ref body);

        int reportId = BattleArchiveUtil.ArchiveAndGetId(op, op?.result, BattleOperationKind.Enslave);
        MilitaryLetterUtil.SendBattleReportLetter("FCRaidLoot".Translate(),
            body, FCLetterDefOf.FCBattleReportLetterPositive,
            new LookTargets(target), reportId, op);
    }
}
