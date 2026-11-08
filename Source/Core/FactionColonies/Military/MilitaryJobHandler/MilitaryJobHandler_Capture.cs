namespace FactionColonies;

public class MilitaryJobHandler_Capture : MilitaryJobHandler_Offensive
{
    protected override FCEventDef ArrivalEvent => FCEventDefOf.captureEnemySettlement;
    protected override string TravelingDescKey => "FCSettlementMilitaryForcesCapturing";
    protected override string SentLetterKey => "FCMilitarySentCapture";
    protected override string TargetLogLabel => "capture";
    protected override BattleOperationKind ReportKind => BattleOperationKind.Capture;
    protected override string FailureLabelKey => "FCCaptureSettlement";

    protected override string FailureBody(MilitaryOperation op, Settlement target) =>
        "FCCaptureEnemySettlementFailure".Translate(op.aggressor.homeSettlement.Name, target.Name);

    protected override void ApplyVictory(MilitaryOperation op, BattleResult result, Settlement target)
    {
        // A manual battle defers the settlement->colony swap to map teardown so the live
        // battle map is never destroyed under the player. The abstract (auto-resolve) path
        // has no map and swaps immediately.
        if (result.wasManualBattle)
            DeferManualCapture(FindFC.FactionComp, op.aggressor.homeSettlement, op.targetTile, target, op, result);
        else
            ApplyCaptureSuccess(FindFC.FactionComp, op.aggressor.homeSettlement, op.targetTile, target, op, result);
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
        if (!SettlementFateUtil.TryDestroySettlement(target, capturedTile))
        {
            LogUtil.Warning($"Capture: target settlement at {capturedTile} survived Destroy(); " +
                            "likely destruction-protected. Falling back to raid rewards.");
            FallBackToRaidVictory(faction, home, tempFactionLink, target, op, result,
                "FCCaptureSettlement", "FCCaptureBlockedFallbackToRaid");
            return;
        }

        // XP grant moved past the destruction check so the fallback path doesn't double up
        // (ApplyVictoryToTarget grants its own +5f).
        faction.AddExperienceToFactionLevel(5f);

        WorldSettlementFC worldsettlement = ColonyUtil.SetupCapturedSettlement(capturedTile, tmpName, tech);

        SettlementFateUtil.MarkDefeatedIfNoSettlementsLeft(tempFactionLink);

        string body = "FCCaptureEnemySettlementSuccess".Translate(home.Name, worldsettlement.Name, worldsettlement.settlementLevel);

        AppendOverwhelmingVictory(result, home, ref body);

        int reportId = BattleArchiveUtil.ArchiveAndGetId(op, op?.result, BattleOperationKind.Capture);
        MilitaryLetterUtil.SendBattleReportLetter("FCCaptureSettlement".Translate(),
            body, FCLetterDefOf.FCBattleReportLetterPositive,
            new LookTargets(worldsettlement), reportId, op);
    }

    /* Deferred capture for a MANUAL offensive battle: the squad won on the enemy base's real
     * map. Grant XP and send the battle report now, but record the settlement->colony swap on
     * the tile's BattlefieldContext so it runs at map teardown (once the player's forces leave),
     * never destroying a live map under the player. SetupCapturedSettlement sends its own
     * "settlement formed" letter when the colony is stood up. */
    private static void DeferManualCapture(FactionFC faction, WorldSettlementFC home,
        PlanetTile capturedTile, Settlement target, MilitaryOperation op, BattleResult result)
    {
        string tmpName = target.LabelCap;
        TechLevel tech = target.Faction.def.techLevel;

        faction.AddExperienceToFactionLevel(5f);

        BattlefieldContext bf = FindFC.MilitaryManager?.GetBattlefield(capturedTile);
        if (bf is not null)
            bf.RegisterPendingCapture(tmpName, tech);
        else
            LogUtil.Warning($"DeferManualCapture: no battlefield at {capturedTile}; deferred capture will not complete.");

        string body = "FCCaptureEnemySettlementSuccessManual".Translate(home.Name, tmpName);
        AppendOverwhelmingVictory(result, home, ref body);
        int reportId = BattleArchiveUtil.ArchiveAndGetId(op, op?.result, BattleOperationKind.Capture);
        MilitaryLetterUtil.SendBattleReportLetter("FCCaptureSettlement".Translate(),
            body, FCLetterDefOf.FCBattleReportLetterPositive,
            new LookTargets(new GlobalTargetInfo(capturedTile)), reportId, op);
    }
}
