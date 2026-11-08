namespace FactionColonies;

/* Tests for the op phase helpers (IsLive, IsEngagedDeploy), the shared offensive-handler
   configuration of each offensive op kind, and the force/fate helpers the military utilities
   share. Ops are standalone (never registered) and nothing else is mutated. */
public static class MilitaryOpStateTests
{
    private static MilitaryOperation Make(MilitaryJobDef kind, MilitaryOperationPhase phase)
    {
        MilitaryOperation op = MilitaryOperationTests.Make(phase);
        op.kind = kind;
        return op;
    }

    /* IsLive */

    [EmpireTest("Military")]
    public static void IsLive_TrueBeforeCooldown_FalseAfter()
    {
        TestAssert.IsTrue(Make(null, MilitaryOperationPhase.Scheduled).IsLive, "Scheduled is live");
        TestAssert.IsTrue(Make(null, MilitaryOperationPhase.Traveling).IsLive, "Traveling is live");
        TestAssert.IsTrue(Make(null, MilitaryOperationPhase.Engaged).IsLive, "Engaged is live");
        TestAssert.IsFalse(Make(null, MilitaryOperationPhase.CooldownPending).IsLive, "CooldownPending is winding down");
        TestAssert.IsFalse(Make(null, MilitaryOperationPhase.Resolved).IsLive, "Resolved is finished");
    }

    /* IsEngagedDeploy */

    [EmpireTest("Military")]
    public static void IsEngagedDeploy_OnlyEngagedDeployOps()
    {
        TestAssert.IsTrue(Make(MilitaryJobDefOf.Deploy, MilitaryOperationPhase.Engaged).IsEngagedDeploy,
            "An engaged deploy op is an engaged deploy");
        foreach (MilitaryOperationPhase phase in new[] { MilitaryOperationPhase.Scheduled, MilitaryOperationPhase.Traveling,
                     MilitaryOperationPhase.CooldownPending, MilitaryOperationPhase.Resolved })
        {
            TestAssert.IsFalse(Make(MilitaryJobDefOf.Deploy, phase).IsEngagedDeploy, $"A deploy op in {phase} is not engaged");
        }
        TestAssert.IsFalse(Make(MilitaryJobDefOf.RaidEnemySettlement, MilitaryOperationPhase.Engaged).IsEngagedDeploy,
            "An engaged raid is not a deploy");
        TestAssert.IsFalse(Make(null, MilitaryOperationPhase.Engaged).IsEngagedDeploy, "An op without a kind is not a deploy");
    }

    [EmpireTest("Military")]
    public static void IsInDeployOp_NullOrUnregisteredSquad_False()
    {
        TestAssert.IsFalse(MilitaryFC.IsInDeployOp(null), "A null squad is in no op");
        TestAssert.IsFalse(MilitaryFC.IsInDeployOp(SquadLookupTests.NewSquad()), "A squad with no registered op is in no op");
    }

    /* Offensive handler configuration */

    private sealed class RaidProbe : MilitaryJobHandler_Raid
    {
        public bool Reports(BattleResult r) => SendsFailureReport(r);
        public (FCEventDef, string, string, BattleOperationKind, string) Config =>
            (ArrivalEvent, TravelingDescKey, SentLetterKey, ReportKind, FailureLabelKey);
    }

    private sealed class CaptureProbe : MilitaryJobHandler_Capture
    {
        public bool Reports(BattleResult r) => SendsFailureReport(r);
        public (FCEventDef, string, string, BattleOperationKind, string) Config =>
            (ArrivalEvent, TravelingDescKey, SentLetterKey, ReportKind, FailureLabelKey);
    }

    private sealed class RazeProbe : MilitaryJobHandler_Raze
    {
        public bool Reports(BattleResult r) => SendsFailureReport(r);
        public (FCEventDef, string, string, BattleOperationKind, string) Config =>
            (ArrivalEvent, TravelingDescKey, SentLetterKey, ReportKind, FailureLabelKey);
    }

    private sealed class EnslaveProbe : MilitaryJobHandler_Enslave
    {
        public bool Reports(BattleResult r) => SendsFailureReport(r);
        public (FCEventDef, string, string, BattleOperationKind, string) Config =>
            (ArrivalEvent, TravelingDescKey, SentLetterKey, ReportKind, FailureLabelKey);
    }

    private static void AssertConfig((FCEventDef arrival, string travel, string sent, BattleOperationKind kind, string failLabel) actual,
        FCEventDef arrival, string travel, string sent, BattleOperationKind kind, string failLabel, string handler)
    {
        TestAssert.IsNotNull(actual.arrival, $"{handler}: arrival event def did not resolve");
        TestAssert.IsTrue(actual.arrival == arrival, $"{handler}: wrong arrival event {actual.arrival?.defName}");
        TestAssert.AreEqual(travel, actual.travel, $"{handler}: travel description key");
        TestAssert.AreEqual(sent, actual.sent, $"{handler}: forces-sent letter key");
        TestAssert.AreEqual((object)kind, (object)actual.kind, $"{handler}: battle report kind");
        TestAssert.AreEqual(failLabel, actual.failLabel, $"{handler}: failure label key");
        foreach (string key in new[] { actual.travel, actual.sent, actual.failLabel })
            TestAssert.IsTrue(key.CanTranslate(), $"{handler}: key {key} has no translation");
    }

    [EmpireTest("Military", GameState = TestGameState.Any)]
    public static void OffensiveHandlers_KeepTheirEventsAndKeys()
    {
        AssertConfig(new RaidProbe().Config, FCEventDefOf.raidEnemySettlement, "FCSettlementMilitaryForcesRaiding",
            "FCMilitarySentRaid", BattleOperationKind.Raid, "FCRaidFailure", "Raid");
        AssertConfig(new CaptureProbe().Config, FCEventDefOf.captureEnemySettlement, "FCSettlementMilitaryForcesCapturing",
            "FCMilitarySentCapture", BattleOperationKind.Capture, "FCCaptureSettlement", "Capture");
        AssertConfig(new RazeProbe().Config, FCEventDefOf.razeEnemySettlement, "FCSettlementMilitaryForcesRazing",
            "FCMilitarySentRaze", BattleOperationKind.Raze, "FCRazeSettlement", "Raze");
        AssertConfig(new EnslaveProbe().Config, FCEventDefOf.enslaveEnemySettlement, "FCSettlementMilitaryForcesEnslave",
            "FCMilitarySentEnslave", BattleOperationKind.Enslave, "FCRaidFailure", "Enslave");
    }

    [EmpireTest("Military", GameState = TestGameState.Any)]
    public static void OffensiveHandlers_FailureReportGating()
    {
        BattleResult defenderWon = new() { winner = BattleWinner.Defender };
        BattleResult errored = new() { winner = BattleWinner.Error };

        TestAssert.IsTrue(new RaidProbe().Reports(defenderWon), "Raid reports a defender victory");
        TestAssert.IsTrue(new RaidProbe().Reports(errored), "Raid reports any non-victory, including an errored battle");

        TestAssert.IsTrue(new CaptureProbe().Reports(defenderWon), "Capture reports a defender victory");
        TestAssert.IsFalse(new CaptureProbe().Reports(errored), "Capture stays silent on an errored battle");
        TestAssert.IsTrue(new RazeProbe().Reports(defenderWon), "Raze reports a defender victory");
        TestAssert.IsFalse(new RazeProbe().Reports(errored), "Raze stays silent on an errored battle");
        TestAssert.IsTrue(new EnslaveProbe().Reports(defenderWon), "Enslave reports a defender victory");
        TestAssert.IsFalse(new EnslaveProbe().Reports(errored), "Enslave stays silent on an errored battle");
    }

    [EmpireTest("Military")]
    public static void OffensiveApplyResult_NullResultOrNoHome_DoesNothing()
    {
        MilitaryOperation op = Make(MilitaryJobDefOf.RaidEnemySettlement, MilitaryOperationPhase.Engaged);
        RaidProbe raid = new();
        TestAssert.DoesNotThrow(() => raid.ApplyResult(op, null), "ApplyResult threw on a null result");
        TestAssert.DoesNotThrow(() => raid.ApplyResult(op, new BattleResult { winner = BattleWinner.Defender }),
            "ApplyResult threw for an op without a home settlement");
    }

    /* Force and fate helpers */

    [EmpireTest("Military")]
    public static void BuildAttackForce_SetsLevelAndRecomputesRemaining()
    {
        Faction faction = TestAssert.NotNull(Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f =>
                f is not null && !f.IsPlayer && f.def is not null && f.def.humanlikeFaction && !FindFC.IsEmpireFaction(f)),
            "No non-player humanlike faction in a loaded game");

        foreach (double level in new[] { 0.0, 1.0, 7.5 })
        {
            MilitaryForce force = MilitaryOperationsUtil.BuildAttackForce(faction, level);
            TestAssert.NotNull(force, $"BuildAttackForce returned null at level {level}");
            TestAssert.AreEqual(level, force.militaryLevel, tolerance: 1e-9, message: "The level should be the one asked for");
            TestAssert.AreEqual(Math.Max(1, Math.Round(level * force.militaryEfficiency)), force.forceRemaining, tolerance: 1e-9,
                message: $"forceRemaining should be max(1, round(level * efficiency)) at level {level}");
        }
    }

    [EmpireTest("Military", GameState = TestGameState.Any)]
    public static void ReturnPlayerPawnsHome_NoPawns_ReturnsNull()
    {
        TestAssert.IsNull(SquadMapTeardownUtil.ReturnPlayerPawnsHome(null, PlanetTile.Invalid, true), "Null list");
        TestAssert.IsNull(SquadMapTeardownUtil.ReturnPlayerPawnsHome([], PlanetTile.Invalid, false), "Empty list");
    }

    [EmpireTest("Military")]
    public static void MarkDefeatedIfNoSettlementsLeft_FactionWithSettlement_Untouched()
    {
        TestAssert.DoesNotThrow(() => SettlementFateUtil.MarkDefeatedIfNoSettlementsLeft(null), "A null faction should be ignored");

        Settlement owned = Find.WorldObjects.Settlements.FirstOrDefault(s =>
            s.Faction is not null && !s.Faction.IsPlayer && !s.Faction.defeated);
        TestAssert.NotNull(owned, "No undefeated NPC faction owns a settlement in a loaded game");
        SettlementFateUtil.MarkDefeatedIfNoSettlementsLeft(owned.Faction);
        TestAssert.IsFalse(owned.Faction.defeated, "A faction that still owns a settlement must not be marked defeated");
    }
}
