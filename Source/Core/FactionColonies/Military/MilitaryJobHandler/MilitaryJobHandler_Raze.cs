namespace FactionColonies;

public class MilitaryJobHandler_Raze : MilitaryJobHandler_Offensive
{
    protected override FCEventDef ArrivalEvent => FCEventDefOf.razeEnemySettlement;
    protected override string TravelingDescKey => "FCSettlementMilitaryForcesRazing";
    protected override string SentLetterKey => "FCMilitarySentRaze";
    protected override string TargetLogLabel => "raze";
    protected override BattleOperationKind ReportKind => BattleOperationKind.Raze;
    protected override string FailureLabelKey => "FCRazeSettlement";

    protected override string FailureBody(MilitaryOperation op, Settlement target) =>
        "FCRazeEnemySettlementFailure".Translate(target.Name);

    protected override void ApplyVictory(MilitaryOperation op, BattleResult result, Settlement target)
    {
        // A manual battle defers destroying the settlement to map teardown so the live
        // battle map is never destroyed under the player. The abstract path has no map and
        // razes immediately.
        if (result.wasManualBattle)
            DeferManualRaze(FindFC.FactionComp, op.aggressor.homeSettlement, op.targetTile, target, op, result);
        else
            ApplyRazeSuccess(FindFC.FactionComp, op.aggressor.homeSettlement, op.targetTile, target, op, result);
    }

    /* Shared raze-victory side effects: destroy the target settlement outright (no replacement),
     * defeat the former owner if it was their last settlement, and grant faction XP. Scorched
     * earth: no loot. The overwhelming-victory flavor + reward is folded into the success letter. */
    private static void ApplyRazeSuccess(FactionFC faction, WorldSettlementFC home,
        PlanetTile razedTile, Settlement target, MilitaryOperation op = null, BattleResult result = null)
    {
        string tmpName = target.LabelCap;
        Faction tempFactionLink = target.Faction;
        if (!SettlementFateUtil.TryDestroySettlement(target, razedTile))
        {
            LogUtil.Warning($"Raze: target settlement at {razedTile} survived Destroy(); " +
                            "likely destruction-protected. Falling back to raid rewards.");
            FallBackToRaidVictory(faction, home, tempFactionLink, target, op, result,
                "FCRazeSettlement", "FCRazeBlockedFallbackToRaid");
            return;
        }

        // XP grant moved past the destruction check so the fallback path doesn't double up
        // (ApplyVictoryToTarget grants its own +5f).
        faction.AddExperienceToFactionLevel(5f);

        SettlementFateUtil.MarkDefeatedIfNoSettlementsLeft(tempFactionLink);

        string body = "FCRazeEnemySettlementSuccess".Translate(home.Name, tmpName);

        AppendOverwhelmingVictory(result, home, ref body);

        int reportId = BattleArchiveUtil.ArchiveAndGetId(op, op?.result, BattleOperationKind.Raze);
        // The target object is gone; anchor the letter to its former tile.
        MilitaryLetterUtil.SendBattleReportLetter("FCRazeSettlement".Translate(),
            body, FCLetterDefOf.FCBattleReportLetterPositive,
            new LookTargets(razedTile), reportId, op);
    }

    /* Deferred raze for a MANUAL offensive battle: the squad won on the enemy base's real map.
     * Grant XP and send the battle report now, but record the settlement destruction on the
     * tile's BattlefieldContext so it runs at map teardown (once the player's forces leave),
     * never destroying a live map under the player. */
    private static void DeferManualRaze(FactionFC faction, WorldSettlementFC home,
        PlanetTile razedTile, Settlement target, MilitaryOperation op, BattleResult result)
    {
        string tmpName = target.LabelCap;

        faction.AddExperienceToFactionLevel(5f);

        BattlefieldContext bf = FindFC.MilitaryManager?.GetBattlefield(razedTile);
        if (bf is not null)
            bf.RegisterPendingRaze();
        else
            LogUtil.Warning($"DeferManualRaze: no battlefield at {razedTile}; deferred raze will not complete.");

        string body = "FCRazeEnemySettlementSuccessManual".Translate(home.Name, tmpName);
        AppendOverwhelmingVictory(result, home, ref body);
        int reportId = BattleArchiveUtil.ArchiveAndGetId(op, op?.result, BattleOperationKind.Raze);
        MilitaryLetterUtil.SendBattleReportLetter("FCRazeSettlement".Translate(),
            body, FCLetterDefOf.FCBattleReportLetterPositive,
            new LookTargets(new GlobalTargetInfo(razedTile)), reportId, op);
    }
}
