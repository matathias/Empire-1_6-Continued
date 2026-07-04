using FactionColonies.util;
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;
using Verse.Sound;

namespace FactionColonies
{
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
    /* Manual offensive battles. Offense reuses the flat BattlefieldContext (same serialized type)
     * with the player as the AGGRESSOR: op.aggressor.pawns are the player's squad (attackerPawns),
     * op.defender.pawns are the enemy garrison (defenderPawns). The target is a vanilla enemy
     * Settlement, so ParentSettlement is null here and the map's MapParent is the enemy Settlement
     * itself. This file is physically separate from the untouched defense logic in
     * BattlefieldContext.cs; it shares the role-agnostic primitives (pawn lists, edge-cell finder,
     * the factored SpawnForceMatchedGroup helper) directly.                                      */
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
    public partial class BattlefieldContext
    {
        /// <summary>An active offense battle owns this tile (offense flag set and a live map).</summary>
        public bool HasOffenseAt() => isOffense && map is object;

        /// <summary>The vanilla enemy settlement being assaulted at this tile, or null.</summary>
        private Settlement OffenseTargetSettlement => Find.WorldObjects.SettlementAt(tile);

        /* -*-*-*-*- Start -*-*-*-*- */

        /// <summary>
        /// Offense entry point (mirrors <see cref="StartDefense"/>). Attaches the op, then re-checks
        /// the manual-offense setting and the shared concurrent-map cap; falls back to
        /// <c>op.BeginAutoResolveProgress()</c> when it should auto-resolve. Otherwise generates the
        /// enemy base map, strips the native garrison (keeping buildings + turrets), spawns a
        /// force-matched garrison and the player squad, and zooms in.
        /// </summary>
        public void StartOffense(MilitaryOperation op)
        {
            if (op is null) return;
            op.AttachToBattlefield();

            Settlement target = OffenseTargetSettlement;
            if (target is null || target.Faction is null)
            {
                LogUtil.Warning($"StartOffense: no enemy settlement at tile {tile}; auto-resolving op id={op.id}.");
                op.BeginAutoResolveProgress();
                return;
            }
            if (op.defender?.force is null)
            {
                LogUtil.Warning($"StartOffense: null defender force for op id={op.id}; auto-resolving.");
                op.BeginAutoResolveProgress();
                return;
            }

            // Auto/manual/cap decision (mirrors StartDefense). Manual falls back to auto when the
            // setting is off or the shared concurrent-map cap is reached.
            bool shouldAutoResolve = !FCSettings.manualOffenseBattle;
            if (!shouldAutoResolve && FCSettings.maxConcurrentBattleMaps > 0
                && (FindFC.MilitaryManager?.CountLiveBattleMaps() ?? 0) >= FCSettings.maxConcurrentBattleMaps)
                shouldAutoResolve = true;

            if (shouldAutoResolve)
            {
                op.BeginAutoResolveProgress();
                return;
            }

            LongEventHandler.QueueLongEvent(() =>
            {
                if (map is null)
                {
                    GenerateOffenseMap();
                    if (map is null)
                    {
                        LogUtil.Error($"StartOffense: map generation failed at tile {tile}; auto-resolving op id={op.id}.");
                        op.BeginAutoResolveProgress();
                        return;
                    }
                    StripNativeGarrison();
                }
                // Commit to the manual path only now (a live map exists) so an auto-resolve
                // fallback above never leaves the discriminator set.
                isOffense = true;
                battleMapInitialized = true;
                SpawnEnemyGarrison(op);
                SpawnPlayerSquad(op);
                Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
                ZoomIntoOffense(op, target);
            }, "GeneratingMap", false, GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap);
        }

        /// <summary>
        /// Generates (or fetches) the enemy base map using the target settlement's own
        /// MapGeneratorDef and the world's initial map size, exactly like a caravan attacking an
        /// enemy base. Real buildings and turrets are kept live. Unlike defense, does NOT call
        /// StripLandmarkHostiles / StripUnresearchedTurrets / RecordSettlementLoot /
        /// RecruitMapInhabitants -- those are defense-only and would forbid the enemy's own goods
        /// or spawn civilians.
        /// </summary>
        private void GenerateOffenseMap()
        {
            // A null suggestedMapParentDef makes GetOrGenerateMap resolve the enemy Settlement world
            // object already at this tile as the map parent, generating with that settlement's own
            // MapGeneratorDef and the world's initial map size -- the same call vanilla's
            // SettlementUtility.Attack makes when a caravan attacks an enemy base.
            map = GetOrGenerateMapUtility.GetOrGenerateMap(tile, null);
            if (map is object) map.fogGrid.ClearAllFog();
        }

        /// <summary>Removes the native humanlike garrison spawned by base generation (any faction that
        /// isn't the player or Empire), keeping turrets, buildings, and animals. Mirrors how
        /// ZoomIntoTile strips KCSG/VBGE pawns before spawning controlled forces.</summary>
        private void StripNativeGarrison()
        {
            if (map is null) return;
            List<Pawn> toRemove = new List<Pawn>();
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (!pawn.RaceProps.Humanlike) continue;
                if (pawn.Faction == Faction.OfPlayer) continue;
                if (FindFC.IsEmpireFaction(pawn.Faction)) continue;
                toRemove.Add(pawn);
            }
            foreach (Pawn pawn in toRemove) pawn.Destroy();
            if (toRemove.Count > 0)
                LogUtil.Message($"StripNativeGarrison: removed {toRemove.Count} native pawn(s) from offense map at tile {tile}");
        }

        /* -*-*-*-*- Spawning -*-*-*-*- */

        /// <summary>Spawns the force-matched enemy garrison via the shared point-formula helper
        /// (defender side, LordJob_DefendBase). Turrets already on the map are bonus hazard and are
        /// not counted toward the garrison force -- winning requires clearing this humanlike garrison.</summary>
        private void SpawnEnemyGarrison(MilitaryOperation op)
        {
            if (map is null || op?.defender?.force is null || op.defender.faction is null) return;
            IntVec3 baseCenter = map.Center;
            int initial = op.defender.initialPawnCount;
            SpawnForceMatchedGroup(
                op.defender.force, op.defender.faction, op.defender.pawns, ref initial,
                (pawns, spawnCenter, arriveMode) =>
                    new LordJob_DefendBase(op.defender.faction, baseCenter, 0));
            op.defender.initialPawnCount = initial;

            if (!op.defender.pawns.Any())
                LogUtil.Warning($"SpawnEnemyGarrison: no garrison pawns spawned for op id={op.id}; player wins by walkover.");
        }

        /// <summary>Materializes the player's squad at a map edge under an AI assault lord. Pawns stay
        /// Empire faction (with a draft controller so the player may optionally take direct control)
        /// and assault autonomously via LordJob_AssaultColony, whose breacher/sapper behavior handles
        /// walls. Mounts/companion animals spawn on foot (v1: the assault lord has no rider pairing).</summary>
        private void SpawnPlayerSquad(MilitaryOperation op)
        {
            if (map is null) return;
            MercenarySquadFC squad = op.aggressor?.squad;
            Faction empire = FindFC.EmpireFaction;
            if (squad is null || empire is null)
            {
                LogUtil.Error($"SpawnPlayerSquad: missing squad/empire for op id={op?.id}.");
                return;
            }

            squad.CheckInitialization();
            if (op.aggressor?.homeSettlement is object)
                squad.UpdateSquadStats(op.aggressor.homeSettlement.settlementMilitaryLevel);
            SquadHealthUtil.ResetNeeds(squad);

            double efficiency = op.aggressor?.force?.militaryEfficiency ?? 1.0;
            List<Pawn> mercs = squad.SpawnableMercenaryPawns.ToList();
            foreach (Pawn merc in mercs)
            {
                MilitaryEfficiencyUtil.ShiftPawnGearQuality(merc, efficiency);
                MilitaryEfficiencyUtil.ApplyCombatEfficiencyHediff(merc, efficiency);
            }

            IntVec3 edge = FindNearEdgeCell(map);
            List<Pawn> spawned = new List<Pawn>();
            foreach (Pawn merc in mercs)
            {
                if (merc.IsWildMan()) continue;
                merc.ApplyIdeologyRitualWounds();
                IntVec3 loc = CellFinder.RandomClosewalkCellNear(edge, map, 8);
                try
                {
                    GenSpawn.Spawn(merc, loc, map, new Rot4());
                    // Give a draft controller so the player can optionally draft this attacker; the
                    // pawn stays Empire faction until actually drafted (drafting flips it to OfPlayer).
                    merc.drafter = new Pawn_DraftController(merc);
                    map.mapPawns.RegisterPawn(merc);
                    ReloadableWeaponUtil.LoadMagazinesFromInventory(merc);
                    spawned.Add(merc);
                }
                catch (Exception e)
                {
                    LogUtil.Warning($"SpawnPlayerSquad: failed to spawn {merc.LabelShort} (likely a mod conflict): {e}");
                }
            }

            op.aggressor.pawns.AddRange(spawned);
            op.aggressor.initialPawnCount += spawned.Count;

            if (spawned.Any())
                LordMaker.MakeNewLord(
                    empire,
                    new LordJob_AssaultColony(empire, canKidnap: false, canTimeoutOrFlee: false,
                        sappers: true, useAvoidGridSmart: false, canSteal: false),
                    map, spawned);
            else
                LogUtil.Error($"SpawnPlayerSquad: no player pawns spawned for op id={op.id}.");
        }

        private void ZoomIntoOffense(MilitaryOperation op, Settlement target)
        {
            RimWorld.SoundDefOf.Tick_High.PlayOneShotOnCamera();
            Pawn firstAttacker = attackerPawns.FirstOrDefault();
            GlobalTargetInfo jump = firstAttacker is object
                ? new GlobalTargetInfo(firstAttacker)
                : new GlobalTargetInfo(map.Center, map);
            Find.LetterStack.ReceiveLetter(
                "FCManualOffenseStarted".Translate(target.Name),
                "FCManualOffenseStartedDesc".Translate(FindFC.EmpireName, target.Name),
                LetterDefOf.ThreatBig,
                new LookTargets(jump));
            CameraJumper.TryJumpAndSelect(jump);
        }

        /* -*-*-*-*- Tick -*-*-*-*- */

        /// <summary>Per-tick offense backstop, called by the FactionFC sweep only for isOffense
        /// contexts (defense stays comp-ticked). Detects player win (garrison cleared) / loss
        /// (squad cleared) and prunes stale pawns. During a post-win loot linger it instead waits
        /// for the last mobile player pawn to leave before tearing the map down. Cheap standing-pawn
        /// poll that early-exits instantly when no live battle is running.</summary>
        public void OffenseTick()
        {
            if (!isOffense) return;
            if (map is null) { isOffense = false; return; }

            if (awaitingPlayerExit)
            {
                FinishLingerIfEmpty();
                return;
            }
            if (endingBattle) return;

            PruneStalePawns();

            bool playerWon = !standingDefenderPawns.Any();   // enemy garrison cleared
            bool playerLost = !standingAttackerPawns.Any();  // player squad cleared
            if (!playerWon && !playerLost) return;

            endingBattle = true;
            bool won = playerWon;   // if both empty at once, treat garrison-cleared as the win
            LongEventHandler.QueueLongEvent(() => EndOffense(won),
                "EndingAttack", false, error =>
                {
                    DelayedErrorWindowRequest.Add("FCErrorEndingAttack".Translate(),
                        "FCErrorEndingAttackDescription".Translate());
                    LogUtil.Error(error.Message);
                });
        }

        /* -*-*-*-*- Resolution + teardown -*-*-*-*- */

        /// <summary>Resolves the offense: restores drafted attackers, strips efficiency hediffs,
        /// completes each active op (loot / capture / enslave via ApplyResult), then tears the map
        /// down. A player win with mobile squad pawns still on the map lingers for physical looting
        /// unless the handler opts out (Capture, whose ApplyResult destroys the host settlement).</summary>
        public void EndOffense(bool won)
        {
            Faction empire = FindFC.EmpireFaction;

            // Restore faction on Empire attackers the player drafted, before resolving -- so the
            // squad reconcile in CompleteBattle sees Empire pawns and drafted mercs rejoin Empire.
            foreach (Pawn npc in draftedNPCs)
                if (npc is object && !npc.Dead && !npc.Destroyed && npc.Faction == Faction.OfPlayer)
                    npc.SetFaction(empire);
            draftedNPCs.Clear();

            // Strip combat-efficiency hediffs from every surviving combatant (both sides).
            foreach (Pawn p in attackerPawns.Concat(defenderPawns).ToList())
                if (p is object && !p.Dead && !p.Destroyed)
                    MilitaryEfficiencyUtil.RemoveCombatEfficiencyHediff(p);

            // Resolve each active op through the normal abstract outcome path (ApplyResult runs
            // loot / capture-and-colony / enslave delivery + letter + archive + cooldown).
            bool skipLinger = false;
            List<MilitaryOperation> ops = activeOps is object
                ? activeOps.ToList() : new List<MilitaryOperation>();
            foreach (MilitaryOperation op in ops)
            {
                if (op is null) continue;
                MilitaryJobHandler_Offensive off = op.kind?.Handler as MilitaryJobHandler_Offensive;
                if (won && off is object && off.SkipLootLingerOnWin) skipLinger = true;

                op.CompleteBattle(new BattleResult
                {
                    wasManualBattle = true,
                    winner = won ? BattleWinner.Attacker : BattleWinner.Defender,
                    attackerInitialForce = op.aggressor?.initialPawnCount ?? 0,
                    defenderInitialForce = op.defender?.initialPawnCount ?? 0,
                    attackerForceRemaining = won ? standingAttackerPawns.Count() : 0,
                    defenderForceRemaining = won ? 0 : standingDefenderPawns.Count(),
                    targetTile = tile
                });
            }

            bool lingering = TeardownOffenseMap(won, skipLinger);
            endingBattle = false;
            if (lingering)
            {
                awaitingPlayerExit = true;
            }
            else
            {
                ClearAllOpPawns();
                isOffense = false;
                awaitingPlayerExit = false;
            }
        }

        /// <summary>Teardown recipe reimplemented against <c>tile</c> (never calls DeleteMap, which
        /// dereferences the null ParentSettlement). Returns true when the map was kept alive for a
        /// post-win loot linger. On a win with mobile pawns and no handler opt-out: idle the mobile
        /// pawns under a stripped lord, return only downed pawns home, keep the map. Otherwise
        /// (loss / withdraw / Capture win / all-downed win): return all player pawns home and deinit
        /// the map immediately.</summary>
        private bool TeardownOffenseMap(bool won, bool skipLinger)
        {
            if (map is null) return false;
            Faction empire = FindFC.EmpireFaction;

            List<Pawn> playerPawns = new List<Pawn>();
            bool anyMobile = false;
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.Faction != empire) continue;
                playerPawns.Add(pawn);
                if (!pawn.Downed) anyMobile = true;
            }

            if (won && !skipLinger && anyMobile)
            {
                // LINGER: keep the map alive so the player can physically haul spoils out (pods /
                // reform caravan). Idle the mobile pawns; return only downed pawns home now.
                foreach (Lord lord in map.lordManager.lords.ListFullCopy())
                    map.lordManager.RemoveLord(lord);
                List<Pawn> idlers = playerPawns.Where(p => !p.Dead && !p.Downed).ToList();
                foreach (Pawn p in idlers) p.jobs?.StopAll();
                if (idlers.Any())
                    LordMaker.MakeNewLord(empire, new LordJob_ColonistsIdle(null), map, idlers);
                ReturnOffensePawnsHome(true, playerPawns.Where(p => p.Downed).ToList());
                return true;
            }

            // IMMEDIATE TEARDOWN.
            foreach (Lord lord in map.lordManager.lords.ListFullCopy())
                map.lordManager.RemoveLord(lord);
            ReturnOffensePawnsHome(won, playerPawns);

            CameraJumper.TryJump(tile);
            if (Find.CurrentMap == map) Current.Game.CurrentMap = Find.AnyPlayerHomeMap;
            Current.Game.DeinitAndRemoveMap(map, false);
            map = null;
            return false;
        }

        /// <summary>Finishes a post-win loot linger once the last mobile Empire pawn has left the map
        /// (via vanilla caravan/pod extraction), then returns any remaining downed pawns and deinits
        /// the map. Called from OffenseTick while awaitingPlayerExit.</summary>
        private void FinishLingerIfEmpty()
        {
            if (map is null) { awaitingPlayerExit = false; isOffense = false; return; }
            Faction empire = FindFC.EmpireFaction;

            bool anyMobile = false;
            List<Pawn> remaining = new List<Pawn>();
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.Faction != empire) continue;
                remaining.Add(pawn);
                if (!pawn.Downed) { anyMobile = true; break; }
            }
            if (anyMobile) return;   // player still on-map looting

            foreach (Lord lord in map.lordManager.lords.ListFullCopy())
                map.lordManager.RemoveLord(lord);
            ReturnOffensePawnsHome(true, remaining);

            CameraJumper.TryJump(tile);
            if (Find.CurrentMap == map) Current.Game.CurrentMap = Find.AnyPlayerHomeMap;
            Current.Game.DeinitAndRemoveMap(map, false);
            map = null;
            ClearAllOpPawns();
            awaitingPlayerExit = false;
            isOffense = false;
        }

        /// <summary>Despawns the given Empire pawns, tends the injured, and delivers them back to the
        /// home settlement via a DeliveryEvent (the DeleteMap injured-return recipe, reimplemented
        /// against <c>tile</c> and the op's home settlement so it never dereferences the null
        /// ParentSettlement). A loss adds a one-day return penalty, matching defense.</summary>
        private void ReturnOffensePawnsHome(bool won, List<Pawn> pawns)
        {
            if (pawns is null || pawns.Count == 0) return;
            PlanetTile homeTile = OffenseHomeTile();

            foreach (Pawn pawn in pawns)
                if (pawn.Spawned) pawn.DeSpawn();
            foreach (Pawn pawn in pawns)
                if (!pawn.Dead)
                {
                    int guard = 0;
                    while (pawn.health.HasHediffsNeedingTend())
                    {
                        if (++guard > 10000) { LogUtil.Error("ReturnOffensePawnsHome: too many tend iterations."); break; }
                        TendUtility.DoTend(null, pawn, null);
                    }
                }

            Map home = Find.AnyPlayerHomeMap;
            if (home is null) return;
            int travelTicks = TravelUtil.ReturnTicksToArrive(homeTile, home.Tile);
            if (!won) travelTicks += GenDate.TicksPerDay;

            List<Thing> goods = new List<Thing>(pawns.Count);
            foreach (Pawn pawn in pawns) goods.Add(pawn);

            DeliveryEvent.CreateDeliveryEvent(new FCEvent
            {
                location = home.Tile,
                source = homeTile,
                goods = goods,
                customDescription = won
                    ? DeliveryNotification.ShuttleEventInjuredString
                    : DeliveryNotification.ShuttleEventInjuredLostString,
                timeTillTrigger = Find.TickManager.TicksGame + travelTicks
            });
            string travelDays = ((float)travelTicks / GenDate.TicksPerDay).ToString("0.#");
            Messages.Message("FCInjuredCaravanMembersReturning".Translate(pawns.Count, travelDays),
                MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>Home tile for pawn return: the first active op's aggressor home settlement,
        /// falling back to any player home map tile.</summary>
        private PlanetTile OffenseHomeTile()
        {
            if (activeOps is object)
                foreach (MilitaryOperation op in activeOps)
                    if (op?.aggressor?.homeSettlement is object)
                        return op.aggressor.homeSettlement.Tile;
            Map home = Find.AnyPlayerHomeMap;
            return home is object ? home.Tile : tile;
        }
    }
}
