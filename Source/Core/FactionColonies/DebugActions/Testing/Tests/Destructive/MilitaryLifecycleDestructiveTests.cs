using System.IO;

namespace FactionColonies;

/* DESTRUCTIVE: hires real squads (spends silver), kills real mercs off-map, generates and spawns an
   animal on the home map, creates Militaristic extra squads and deploy ops, swaps the live
   storyteller's adaptation curves (restored afterwards), founds and destroys an NPC settlement,
   writes temp/template files (deleted afterwards) and drives Scribe directly. Not reverted. */
public static class MilitaryLifecycleDestructiveTests
{
    private static MilitaryFC RequireMilitary()
    {
        DestructiveTestUtil.RequireFaction();
        return TestAssert.NotNull(FindFC.Military, "No MilitaryFC on the faction");
    }

    private static Mercenary RequireLiveOffMapMerc(MercenarySquadFC squad)
    {
        Mercenary merc = TestAssert.NotNull(squad.mercenaries.FirstOrDefault(m => m?.pawn is not null && !m.pawn.Dead),
            $"Hired squad {squad.DisplayName} has no live merc");
        TestAssert.IsFalse(merc.pawn.Spawned, "A freshly hired merc should be off-map");
        return merc;
    }

    /// <summary>Generates a companion animal for <paramref name="owner"/> and wires it into the squad.</summary>
    private static Mercenary AddLiveAnimal(MercenarySquadFC squad, Mercenary owner, Mercenary.SubPawnType type)
    {
        Mercenary sub = new() { handler = owner };
        MercenaryPawnFactory.CreateNewAnimal(squad, ref sub, PawnKindDefOf.Muffalo);
        TestAssert.NotNull(sub.pawn, "Could not generate a muffalo sub-pawn");
        sub.subPawnType = type;
        owner.animals.Add(sub);
        FindFC.Military.RebuildMercenaryPawnSet();
        return sub;
    }

    /* Merc death off-map */

    [EmpireDestructiveTest("Destructive.Military")]
    public static void OffMapMercDeath_EmptiesTheSlot()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        MilitaryFC mil = RequireMilitary();
        MercenarySquadFC squad = DestructiveTestUtil.EnsureHiredSquad();
        Mercenary merc = RequireLiveOffMapMerc(squad);
        Pawn pawn = merc.pawn;

        TestAssert.IsTrue(mil.ReturnSquadFromUnit(pawn) == squad, "The squad should be found for an off-map merc");
        TestAssert.IsTrue(mil.ReturnMercenaryFromUnit(pawn, squad) == merc, "The slot should be found for an off-map merc");

        int emptyBefore = squad.mercenaries.Count(m => m is not null && m.IsEmptySlot);
        TestAssert.DoesNotThrow(() => BattleCasualtyApplicator.KillCasualty(pawn), "Killing an off-map merc threw");
        TestAssert.IsTrue(pawn.Dead, "The merc should be dead");

        TestAssert.IsNull(merc.pawn, "An off-map death must empty the slot so Fill Empty Slots can refill it");
        TestAssert.IsTrue(squad.mercenaries.Contains(merc), "The slot itself stays, keeping its loadout");
        TestAssert.AreEqual(emptyBefore + 1, squad.mercenaries.Count(m => m is not null && m.IsEmptySlot),
            "Exactly one more slot should be empty");
        TestAssert.IsFalse(mil.IsMercenaryPawn(pawn), "A dead merc must not stay registered as a merc pawn");
        DestructiveTestUtil.AssertEmpireInvariants(f, "OffMapMercDeath_EmptiesTheSlot");
    }

    [EmpireDestructiveTest("Destructive.Military")]
    public static void OffMapSubPawnDeath_LeavesAMissingPlaceholder()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        MilitaryFC mil = RequireMilitary();
        MercenarySquadFC squad = DestructiveTestUtil.EnsureHiredSquad();
        Mercenary owner = RequireLiveOffMapMerc(squad);
        Mercenary animal = AddLiveAnimal(squad, owner, Mercenary.SubPawnType.Animal);
        Pawn pawn = animal.pawn;

        TestAssert.IsTrue(mil.ReturnSquadFromUnit(pawn) == squad, "The squad should be found for an off-map sub-pawn");
        TestAssert.IsTrue(mil.FindSubPawnWrapper(pawn) == animal, "The wrapper should be found for an off-map sub-pawn");
        TestAssert.IsNull(mil.ReturnMercenaryFromUnit(pawn, squad), "A sub-pawn is not a slot merc");

        TestAssert.DoesNotThrow(() => BattleCasualtyApplicator.KillCasualty(pawn), "Killing an off-map animal threw");
        TestAssert.IsNull(animal.pawn, "The dead animal's wrapper should be emptied");
        TestAssert.IsTrue(owner.animals.Contains(animal), "The wrapper stays as a placeholder for a paid replacement");
        TestAssert.IsTrue(animal.IsMissingSubPawn, "The wrapper should read as missing");
        TestAssert.Contains(squad.MissingSubPawns(), animal, "The missing animal should be offered for refill");
        TestAssert.IsTrue(owner.pawn is not null, "The owning merc is untouched");
        DestructiveTestUtil.AssertEmpireInvariants(f, "OffMapSubPawnDeath_LeavesAMissingPlaceholder");
    }

    /* Deployed check and availability */

    [EmpireDestructiveTest("Destructive.Military")]
    public static void SpawnedSubPawn_MakesTheSquadDeployedAndUnavailable()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        MilitaryFC mil = RequireMilitary();
        Map map = TestAssert.NotNull(Find.AnyPlayerHomeMap, "No player home map to spawn the sub-pawn on");
        MercenarySquadFC squad = DestructiveTestUtil.EnsureHiredSquad();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement();
        mil.Assign(squad, settlement, true);
        Mercenary owner = RequireLiveOffMapMerc(squad);
        Mercenary mount = AddLiveAnimal(squad, owner, Mercenary.SubPawnType.Mount);

        TestAssert.IsFalse(squad.Deployment.IsPhysicallyDeployed(), "Nothing is on a map yet");
        bool availableBefore = squad.IsAvailable;

        try
        {
            GenSpawn.Spawn(mount.pawn, CellFinder.RandomClosewalkCellNear(map.Center, map, 10), map);
            TestAssert.IsTrue(squad.Deployment.IsPhysicallyDeployed(), "A sub-pawn on a map means the squad is deployed");
            TestAssert.IsFalse(squad.IsAvailable, "A squad with a pawn still on a map must not read as available");
        }
        finally
        {
            if (mount.pawn is { Spawned: true }) mount.pawn.DeSpawn();
        }

        TestAssert.IsFalse(squad.Deployment.IsPhysicallyDeployed(), "Despawned again, the squad is home");
        TestAssert.AreEqual((object)availableBefore, (object)squad.IsAvailable, "Availability should return to what it was");

        owner.animals.Remove(mount);
        if (mount.pawn is { Destroyed: false }) mount.pawn.Destroy();
        mil.RebuildMercenaryPawnSet();
        DestructiveTestUtil.AssertEmpireInvariants(f, "SpawnedSubPawn_MakesTheSquadDeployedAndUnavailable");
    }

    /* Militaristic extra squad */

    [EmpireDestructiveTest("Destructive.Military")]
    public static void ExtraSquad_IsOutfittedAndNotStationed()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        MilitaryFC mil = RequireMilitary();
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement(s => !s.SquadDeploymentLocked);
        MilSquadFC outfit = DestructiveTestUtil.EnsureSquadTemplate();

        MercenarySquadFC extra = TestAssert.NotNull(mil.CreateMercenarySquad(settlement, true, outfit),
            "CreateMercenarySquad returned null");
        TestAssert.IsTrue(extra.isExtraSquad, "The squad should be flagged as an extra squad");

        int designed = outfit.Units.Take(MilSquadFC.MaxSquadSize).Count(u => u is not null && !u.isBlank);
        TestAssert.GreaterThan(designed, 0, "The template should have at least one real unit");
        TestAssert.AreEqual(designed, extra.mercenaries.Count(m => m?.pawn is not null),
            "Every designed unit should get a pawn, or the deploy spawns an empty lord");
        TestAssert.IsFalse(settlement.StationedSquads.Contains(extra), "An extra squad must not take a billet");

        mil.RemoveIdleExtraSquads();
        TestAssert.IsFalse(mil.mercenarySquads.Contains(extra), "An extra squad with no op (cancelled placement) should be dropped");
        DestructiveTestUtil.AssertEmpireInvariants(f, "ExtraSquad_IsOutfittedAndNotStationed");
    }

    [EmpireDestructiveTest("Destructive.Military")]
    public static void ExtraSquad_LivesUntilItsDeployOpResolves()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        MilitaryFC mil = RequireMilitary();
        MilitaryOperationManager mgr = TestAssert.NotNull(FindFC.MilitaryManager, "No military manager");
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement(s => !s.SquadDeploymentLocked);
        MilSquadFC outfit = DestructiveTestUtil.EnsureSquadTemplate();
        MercenarySquadFC regular = DestructiveTestUtil.EnsureHiredSquad();

        MercenarySquadFC extra = TestAssert.NotNull(mil.CreateMercenarySquad(settlement, true, outfit),
            "CreateMercenarySquad returned null");
        MilitaryOperation op = TestAssert.NotNull(mgr.CreateDeployOp(extra, settlement.Tile),
            "CreateDeployOp rejected a deploy from a settlement that is not morale-locked");
        TestAssert.IsTrue(extra.IsBusy, "The extra squad should be busy with its deploy op");

        mil.RemoveIdleExtraSquads();
        TestAssert.IsTrue(mil.mercenarySquads.Contains(extra), "A busy extra squad must survive the idle sweep");

        DestructiveTestUtil.ResolveAndSweepOp(op);
        TestAssert.IsFalse(mil.mercenarySquads.Contains(extra), "The extra squad should be dropped once its op resolves");
        TestAssert.IsNull(extra.settlement, "A dropped extra squad keeps no billet");

        mil.RemoveExtraSquad(regular);
        TestAssert.IsTrue(mil.mercenarySquads.Contains(regular), "RemoveExtraSquad must ignore a regular squad");
        DestructiveTestUtil.AssertEmpireInvariants(f, "ExtraSquad_LivesUntilItsDeployOpResolves");
    }

    /* Threat adaptation */

    /// <summary>Installs adaptation curves that make adaptDays readable through ThreatFactor
    /// (ThreatFactor = adaptDays + 1, no clamping in range) and a flat 3-day loss per lost battle.</summary>
    private static void WithReadableAdaptation(Action body)
    {
        StorytellerDef def = TestAssert.NotNull(Find.Storyteller?.def, "No storyteller def in a loaded game");
        Difficulty difficulty = TestAssert.NotNull(Find.Storyteller.difficulty, "No storyteller difficulty in a loaded game");
        SimpleCurve savedPoints = def.pointsFactorFromAdaptDays;
        SimpleCurve savedLoss = def.adaptDaysLossFromColonistLostByPostPopulation;
        float savedMin = def.adaptDaysMin;
        float savedMax = def.adaptDaysMax;
        float savedEffect = difficulty.adaptationEffectFactor;
        def.pointsFactorFromAdaptDays = [new CurvePoint(-10000f, -9999f), new CurvePoint(10000f, 10001f)];
        def.adaptDaysLossFromColonistLostByPostPopulation = [new CurvePoint(0f, 3f), new CurvePoint(10000f, 3f)];
        def.adaptDaysMin = -10000f;
        def.adaptDaysMax = 10000f;
        difficulty.adaptationEffectFactor = 1f;
        try { body(); }
        finally
        {
            def.pointsFactorFromAdaptDays = savedPoints;
            def.adaptDaysLossFromColonistLostByPostPopulation = savedLoss;
            def.adaptDaysMin = savedMin;
            def.adaptDaysMax = savedMax;
            difficulty.adaptationEffectFactor = savedEffect;
        }
    }

    private static MilitaryOperation EngagedEmpireOp(MilitaryJobDef kind, Faction empire)
    {
        MilitaryOperation op = new(-1, kind, PlanetTile.Invalid, null) { phase = MilitaryOperationPhase.Engaged };
        op.aggressor.faction = empire;
        return op;
    }

    [EmpireDestructiveTest("Destructive.Military")]
    public static void DeployOp_DoesNotCountAsALostBattle()
    {
        Faction empire = DestructiveTestUtil.EnsureEmpireFaction();
        EmpireThreatAdaptation adapt = TestAssert.NotNull(FindFC.ThreatAdaptation, "No threat adaptation on the faction");

        WithReadableAdaptation(() =>
        {
            double before = adapt.ThreatFactor;
            MilitaryOperation deploy = EngagedEmpireOp(MilitaryJobDefOf.Deploy, empire);
            TestAssert.DoesNotThrow(() => deploy.CompleteBattle(new BattleResult { winner = BattleWinner.Defender }),
                "Completing a deploy op threw");
            TestAssert.AreEqual(before, adapt.ThreatFactor, tolerance: 1e-6,
                message: "Ending a deployment must not move the threat adaptation");
            DestructiveTestUtil.ResolveAndSweepOp(deploy);

            // Control: the same placeholder result on a real offensive does count as a loss.
            MilitaryOperation raid = EngagedEmpireOp(MilitaryJobDefOf.RaidEnemySettlement, empire);
            TestAssert.DoesNotThrow(() => raid.CompleteBattle(new BattleResult { winner = BattleWinner.Defender }),
                "Completing a raid op threw");
            TestAssert.AreEqual(before - 3.0, adapt.ThreatFactor, tolerance: 1e-4,
                message: "A lost raid should lower adaptDays by the curve's 3 days");
            DestructiveTestUtil.ResolveAndSweepOp(raid);
        });
    }

    /* MilitaryOperation save/load of its result */

    private static string TempOpPath() => Path.Combine(GenFilePaths.TempFolderPath, "EmpireTest_MilitaryOperation.xml");

    private static string SaveOp(MilitaryOperation op, string path)
    {
        try
        {
            Scribe.saver.InitSaving(path, "militaryOperation");
            op.ExposeData();
        }
        finally
        {
            Scribe.saver.FinalizeSaving();
        }
        return File.ReadAllText(path);
    }

    private static MilitaryOperation LoadOp(string path)
    {
        MilitaryOperation op = new();
        Scribe.loader.InitLoading(path);
        try
        {
            // Registered like a deep-loaded object, so the cross-reference and post-load passes
            // run on it too.
            Scribe.loader.crossRefs.RegisterForCrossRefResolve(op);
            Scribe.loader.initer.RegisterForPostLoadInit(op);
            op.ExposeData();
        }
        finally
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars) Scribe.loader.FinalizeLoading();
            else Scribe.ForceStop();
        }
        return op;
    }

    private static MilitaryOperation RoundTrip(BattleResult result, BattleResult battleResult, out string xml)
    {
        MilitaryOperation op = MilitaryOperationTests.Make(MilitaryOperationPhase.CooldownPending);
        op.kind = MilitaryJobDefOf.RaidEnemySettlement;
        op.result = result;
        op.battleResult = battleResult;
        string path = TempOpPath();
        try
        {
            xml = SaveOp(op, path);
            return LoadOp(path);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [EmpireDestructiveTest("Destructive.Military")]
    public static void OpRoundTrip_SharedResult_SavedOnceAndStillShared()
    {
        DestructiveTestUtil.RequireFaction();
        BattleResult shared = new() { winner = BattleWinner.Attacker, totalRounds = 7 };
        MilitaryOperation loaded = RoundTrip(shared, shared, out string xml);

        TestAssert.IsFalse(xml.Contains("<battleResult>") || xml.Contains("<battleResult "),
            "A result shared by both fields should be written only once");
        TestAssert.NotNull(loaded.result, "The result should load");
        TestAssert.IsTrue(ReferenceEquals(loaded.result, loaded.battleResult), "Both fields should point to one object after load");
        TestAssert.AreEqual((object)BattleWinner.Attacker, (object)loaded.result.winner);
        TestAssert.AreEqual(7, loaded.result.totalRounds);
    }

    [EmpireDestructiveTest("Destructive.Military")]
    public static void OpRoundTrip_DistinctResults_BothSavedAndStayDistinct()
    {
        DestructiveTestUtil.RequireFaction();
        BattleResult result = new() { winner = BattleWinner.Attacker };
        BattleResult battleResult = new() { winner = BattleWinner.Defender };
        MilitaryOperation loaded = RoundTrip(result, battleResult, out string xml);

        TestAssert.IsTrue(xml.Contains("<battleResult>") || xml.Contains("<battleResult "),
            "A separate battleResult must still be written (and older saves always have one)");
        TestAssert.NotNull(loaded.result, "result should load");
        TestAssert.NotNull(loaded.battleResult, "battleResult should load");
        TestAssert.IsFalse(ReferenceEquals(loaded.result, loaded.battleResult), "Distinct results must stay distinct");
        TestAssert.AreEqual((object)BattleWinner.Attacker, (object)loaded.result.winner);
        TestAssert.AreEqual((object)BattleWinner.Defender, (object)loaded.battleResult.winner);
    }

    [EmpireDestructiveTest("Destructive.Military")]
    public static void OpRoundTrip_NoResult_BothStayNull()
    {
        DestructiveTestUtil.RequireFaction();
        MilitaryOperation loaded = RoundTrip(null, null, out _);
        TestAssert.IsNull(loaded.result);
        TestAssert.IsNull(loaded.battleResult);

        BattleResult onlyBattle = new() { winner = BattleWinner.Defender };
        MilitaryOperation loadedBattleOnly = RoundTrip(null, onlyBattle, out _);
        TestAssert.IsNull(loadedBattleOnly.result, "No result was saved");
        TestAssert.NotNull(loadedBattleOnly.battleResult, "A battleResult without a result must survive");
    }

    /* Template files */

    [EmpireDestructiveTest("Destructive.Military")]
    public static void FireSupportTemplate_UnsafeName_SavesInsideItsFolderAndReloads()
    {
        const string name = "EmpireTest/Strike:1";
        string path = FactionColoniesMilitary.GetFireSupportPath(name);
        SavedFireSupportFC template = new() { name = name, accuracy = 7f, projectiles = [], fireSupportType = "fireSupport" };
        try
        {
            TestAssert.DoesNotThrow(() => FactionColoniesMilitary.SaveFireSupport(template), "SaveFireSupport threw");
            TestAssert.IsTrue(File.Exists(path), $"The template should be written to {path}");
            TestAssert.IsTrue(FactionColoniesMilitary.GetFireSupport(name) == template, "The saved template should be cached");

            TestAssert.DoesNotThrow(FactionColoniesMilitary.Read, "Reloading the templates threw");
            SavedFireSupportFC reloaded = TestAssert.NotNull(FactionColoniesMilitary.GetFireSupport(name),
                "The template should reload from its file under its original name");
            TestAssert.AreEqual(7.0, reloaded.accuracy, tolerance: 1e-6, message: "The reloaded template keeps its data");
        }
        finally
        {
            FactionColoniesMilitary.RemoveFireSupport(name);
        }
        TestAssert.IsFalse(File.Exists(path), "Removing the template should delete its file");
        TestAssert.IsNull(FactionColoniesMilitary.GetFireSupport(name), "Removing the template should drop it from the cache");
    }

    /* Settlement fate */

    [EmpireDestructiveTest("Destructive.Military")]
    public static void TryDestroySettlement_RemovesIt_FactionWithOthersStaysUndefeated()
    {
        Faction empire = DestructiveTestUtil.EnsureEmpireFaction();
        Faction enemy = TestAssert.NotNull(Find.FactionManager.AllFactionsListForReading.FirstOrDefault(fac =>
                fac is not null && !fac.IsPlayer && fac != empire && !fac.Hidden && !fac.defeated && fac.def.humanlikeFaction
                && Find.WorldObjects.Settlements.Any(s => s.Faction == fac)),
            "No visible NPC faction owns a settlement");

        PlanetTile tile = TileFinder.RandomSettlementTileFor(Find.WorldGrid.Surface, enemy);
        TestAssert.IsTrue(tile.Valid, $"No valid tile to found a settlement for {enemy.Name}");
        Settlement doomed = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
        doomed.SetFaction(enemy);
        doomed.Tile = tile;
        doomed.Name = SettlementNameGenerator.GenerateSettlementName(doomed);
        Find.WorldObjects.Add(doomed);

        TestAssert.IsTrue(SettlementFateUtil.TryDestroySettlement(doomed, tile), "An unprotected settlement should be destroyed");
        TestAssert.IsTrue(doomed.Destroyed);
        TestAssert.IsNull(Find.WorldObjects.SettlementAt(tile), "Nothing should be left at the tile");

        SettlementFateUtil.MarkDefeatedIfNoSettlementsLeft(enemy);
        TestAssert.IsFalse(enemy.defeated, "A faction with other settlements is not defeated");
    }

    /* Returning player pawns with no home map */

    [EmpireDestructiveTest("Destructive.Military")]
    public static void SendToNearestCaravan_PutsLivePawnsInAPlayerCaravan()
    {
        DestructiveTestUtil.RequireFaction();
        PlanetTile tile = Find.AnyPlayerHomeMap?.Tile
                          ?? Find.WorldObjects.Caravans.FirstOrDefault(c => c.IsPlayerControlled)?.Tile
                          ?? PlanetTile.Invalid;
        TestAssert.IsTrue(tile.Valid, "The player has neither a home map nor a caravan");
        Pawn pawn = TestAssert.NotNull(PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,
            Faction.OfPlayer, forceGenerateNewPawn: true)), "Could not generate a colonist");

        Caravan caravan = null;
        try
        {
            TestAssert.DoesNotThrow(() => caravan = SquadMapTeardownUtil.SendToNearestCaravan([pawn], tile),
                "SendToNearestCaravan threw");
            TestAssert.NotNull(caravan, "A live pawn should always end up in a caravan");
            TestAssert.IsTrue(caravan.IsPlayerControlled, "The caravan should be the player's");
            TestAssert.IsTrue(caravan.ContainsPawn(pawn), "The pawn should be in the caravan");
            TestAssert.IsTrue(pawn.IsWorldPawn(), "Caravan members are world pawns");
        }
        finally
        {
            if (caravan is not null && caravan.ContainsPawn(pawn)) caravan.RemovePawn(pawn);
            if (caravan is { Destroyed: false } && caravan.PawnsListForReading.Count == 0) caravan.Destroy();
            if (pawn.IsWorldPawn()) Find.WorldPawns.RemovePawn(pawn);
            if (!pawn.Destroyed) pawn.Destroy();
        }
    }

    /* Offensive handler flow */

    private sealed class CountingHandler : MilitaryJobHandler_Offensive
    {
        public int victories;
        public Settlement lastTarget;
        protected override FCEventDef ArrivalEvent => FCEventDefOf.raidEnemySettlement;
        protected override string TravelingDescKey => "FCSettlementMilitaryForcesRaiding";
        protected override string SentLetterKey => "FCMilitarySentRaid";
        protected override string TargetLogLabel => "test";
        protected override BattleOperationKind ReportKind => BattleOperationKind.Raid;
        protected override string FailureLabelKey => "FCRaidFailure";
        protected override string FailureBody(MilitaryOperation op, Settlement target) => "";
        // No failure letter, so the flow can run without sending or archiving anything.
        protected override bool SendsFailureReport(BattleResult result) => false;
        protected override void ApplyVictory(MilitaryOperation op, BattleResult result, Settlement target)
        {
            victories++;
            lastTarget = target;
        }
    }

    [EmpireDestructiveTest("Destructive.Military")]
    public static void OffensiveApplyResult_VictoryOnlyForAnExistingTarget()
    {
        WorldSettlementFC home = DestructiveTestUtil.EnsureSettlement();
        Settlement target = TestAssert.NotNull(Find.WorldObjects.Settlements.FirstOrDefault(s =>
            s.Faction is not null && !s.Faction.IsPlayer && !FindFC.IsEmpireFaction(s.Faction)), "No NPC settlement in the world");

        MilitaryOperation op = MilitaryOperationTests.Make(MilitaryOperationPhase.Engaged);
        op.aggressor.homeSettlement = home;
        op.targetObject = target;
        op.targetTile = target.Tile;
        CountingHandler handler = new();

        handler.ApplyResult(op, new BattleResult { winner = BattleWinner.Defender });
        TestAssert.AreEqual(0, handler.victories, "A defender victory is not a victory");

        handler.ApplyResult(op, new BattleResult { winner = BattleWinner.Attacker });
        TestAssert.AreEqual(1, handler.victories, "An attacker victory should apply the victory once");
        TestAssert.IsTrue(handler.lastTarget == target, "The victory should be applied to the op's target");

        MilitaryOperation lost = MilitaryOperationTests.Make(MilitaryOperationPhase.Engaged);
        lost.aggressor.homeSettlement = home;
        handler.ApplyResult(lost, new BattleResult { winner = BattleWinner.Attacker });
        TestAssert.AreEqual(1, handler.victories, "A target that no longer exists gets no victory");
    }
}
