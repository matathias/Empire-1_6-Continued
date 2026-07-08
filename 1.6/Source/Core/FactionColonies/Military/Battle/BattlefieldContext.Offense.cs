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
        /* Poll cadences for the offense sweep. Offense has no lord-notification path (defense's
         * RemoveAttacker/RemoveDefender), so polling is the only win/loss detection -- 60 ticks
         * keeps the end-of-battle latency imperceptible while cutting the per-tick LINQ work.
         * The linger check merely waits for the player to leave; 250 matches defense's gate. */
        private const int OffenseWinCheckInterval = 60;
        private const int OffenseLingerCheckInterval = 250;

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

            // A map already exists at this tile but isn't ours (the player attacked in person
            // after launch, a quest spawned a site map, ...): never hijack it -- resolve
            // abstractly instead. The launch gate makes this rare; this is the backstop.
            if (map is null && Current.Game.FindMap(tile) is object)
            {
                LogUtil.Warning($"StartOffense: foreign map already present at tile {tile}; auto-resolving op id={op.id}.");
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
                    new LordJob_DefendBase(op.defender.faction, baseCenter, 0),
                spawnInBaseInterior: true);
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

        /// <summary>Drops a player caravan's pawns into an ongoing assault as player-controlled
        /// combatants (mirrors joining a manual defense with a caravan). They spawn at a map edge,
        /// are tracked as attackers so win/loss detection doesn't count the squad as wiped while the
        /// player's colonists still fight, and count as player pawns for the linger teardown. The
        /// caravan is consumed. Hostility to the garrison is already guaranteed by the op-launch
        /// AttackFaction call, so the colonists can engage immediately once the player drafts them.</summary>
        public void CaravanJoinAttack(Caravan caravan)
        {
            if (caravan is null) return;
            if (map is null || !isOffense)
            {
                Messages.Message("FCJoinAttackNoBattle".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            MilitaryOperation op = activeOps?.FirstOrDefault(o => o?.aggressor is object);
            List<Pawn> pawns = caravan.pawns.InnerListForReading.ListFullCopy();

            // Release the pawns from the caravan container before spawning them (mirrors the defense
            // CaravanDefend order: copy list -> destroy caravan -> spawn on map).
            if (!caravan.Destroyed) caravan.Destroy();

            IntVec3 edge = FindNearEdgeCell(map);
            foreach (Pawn pawn in pawns)
            {
                if (pawn.Spawned) continue;
                IntVec3 loc = CellFinder.RandomClosewalkCellNear(edge, map, 8);
                GenSpawn.Spawn(pawn, loc, map, Rot4.Random);
                map.mapPawns.RegisterPawn(pawn);
            }

            if (op?.aggressor?.pawns is object)
            {
                // Track only fighting colonists as attackers: a surviving pack animal or a
                // caravan prisoner must not hold the squad-wiped loss condition open.
                foreach (Pawn pawn in pawns)
                {
                    if (pawn.RaceProps?.Humanlike != true) continue;
                    if (pawn.IsPrisonerOfColony) continue;
                    op.aggressor.pawns.Add(pawn);
                    op.aggressor.initialPawnCount++;
                }
            }

            Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
            CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(map.Center, map));
        }

        /* -*-*-*-*- Tick -*-*-*-*- */

        /// <summary>Offense backstop, called by the FactionFC sweep only for isOffense contexts
        /// (defense stays comp-ticked). Detects player win (garrison cleared) / loss (squad cleared)
        /// and prunes stale pawns on a 60-tick cadence -- offense has no lord-notification path, so
        /// polling is the only win/loss detection. During a post-win loot linger it instead waits
        /// (on a 250-tick cadence) for the last mobile player pawn to leave before tearing the map
        /// down. Early-exits instantly when no live battle is running.</summary>
        public void OffenseTick()
        {
            if (!isOffense) return;
            if (map is null) { isOffense = false; return; }

            if (awaitingPlayerExit)
            {
                if (Find.TickManager.TicksGame % OffenseLingerCheckInterval != 0) return;
                FinishLingerIfEmpty();
                return;
            }
            if (endingBattle) return;
            if (Find.TickManager.TicksGame % OffenseWinCheckInterval != 0) return;

            PruneStalePawns();
            CaptureUntrackedAttackers();

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

        /// <summary>Registers player pawns that arrived on the offense map outside
        /// CaravanJoinAttack (drop pods, shuttles) as attackers, so win/loss accounting sees them
        /// -- the offense mirror of the defense Tick's untracked-pawn capture. Humanlike,
        /// non-prisoner colonists only: animals and prisoners must not gate the loss condition.
        /// No lord is assigned; these are the player's own colonists.</summary>
        private void CaptureUntrackedAttackers()
        {
            MilitaryOperation op = activeOps?.FirstOrDefault(o => o?.aggressor?.pawns is object);
            if (op is null) return;
            HashSet<Pawn> tracked = new HashSet<Pawn>(attackerPawns);
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.Faction != Faction.OfPlayer) continue;
                if (pawn.Dead || pawn.Downed) continue;
                if (pawn.RaceProps?.Humanlike != true) continue;
                if (pawn.IsPrisonerOfColony) continue;
                if (tracked.Contains(pawn)) continue;
                LogUtil.Message($"Registering untracked player pawn {pawn.LabelShort} with offense at tile {tile}");
                op.aggressor.pawns.Add(pawn);
                op.aggressor.initialPawnCount++;
            }
        }

        /* -*-*-*-*- Resolution + teardown -*-*-*-*- */

        /// <summary>Resolves the offense: restores drafted attackers, strips efficiency hediffs,
        /// completes each active op (loot / capture / enslave via ApplyResult), then tears the map
        /// down following the defense scheme exactly -- close immediately when no player colonists
        /// are on the map, otherwise linger until they leave. <paramref name="withdrawn"/> marks a
        /// voluntary retreat, which is a failed op but never a crushing defeat.</summary>
        public void EndOffense(bool won, bool withdrawn = false)
        {
            Faction empire = FindFC.EmpireFaction;
            offenseWon = won;

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
            // loot / enslave delivery + letter + archive + cooldown). A manual Capture win defers
            // its settlement->colony swap to teardown via RegisterPendingCapture.
            List<MilitaryOperation> ops = activeOps is object
                ? activeOps.ToList() : new List<MilitaryOperation>();
            foreach (MilitaryOperation op in ops)
            {
                if (op is null) continue;
                op.CompleteBattle(new BattleResult
                {
                    wasManualBattle = true,
                    wasWithdrawal = withdrawn,
                    winner = won ? BattleWinner.Attacker : BattleWinner.Defender,
                    attackerInitialForce = op.aggressor?.initialPawnCount ?? 0,
                    defenderInitialForce = op.defender?.initialPawnCount ?? 0,
                    // A withdrawal extracts the surviving squad rather than a wipe, so its attacker
                    // remaining is the standing count (not 0).
                    attackerForceRemaining = (won || withdrawn) ? standingAttackerPawns.Count() : 0,
                    defenderForceRemaining = won ? 0 : standingDefenderPawns.Count(),
                    targetTile = tile
                });
            }

            bool lingering = TeardownOffenseMap(won);
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
        /// dereferences the null ParentSettlement). Mirrors defense DeleteMap: the linger-vs-close
        /// decision is gated on genuine mobile player colonists (Faction.OfPlayer, e.g. a Join
        /// Attack caravan) -- NOT the Empire squad mercs, and NOT player animals (an animal cannot
        /// reform a caravan on its own, so it must never hold the map open). Returns true when the
        /// map is kept alive for the player's colonists to leave; the Empire squad is preserved
        /// off-map on close.</summary>
        private bool TeardownOffenseMap(bool won)
        {
            if (map is null) return false;

            List<Pawn> playerColonists = new List<Pawn>();
            bool anyMobile = false;
            // Snapshot the spawned-pawn list: SetFaction on a spawned pawn de/re-registers it in
            // mapPawns' internal list -- the very list AllPawnsSpawned returns -- and mutating it
            // mid-enumeration throws.
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (pawn.Faction != Faction.OfPlayer)
                {
                    // The player's caravan prisoners spawn with their own faction; ship them home
                    // with the colonists instead of letting map removal destroy them.
                    if (pawn.IsPrisonerOfColony) playerColonists.Add(pawn);
                    continue;
                }
                // A squad merc still in OfPlayer (drafted and not yet restored) is NOT a player
                // colonist: return it to Empire (which also undrafts it) so the preserve step holds
                // it off-map for redeployment instead of ReturnPlayerColonistsHome shipping it home
                // under the player's control. It must also not count toward anyMobile, or a stray
                // drafted merc would keep the map lingering indefinitely.
                if (pawn.IsMercenary())
                {
                    pawn.SetFaction(FindFC.EmpireFaction);
                    continue;
                }
                playerColonists.Add(pawn);
                // Animals never gate the linger: a lone surviving pack animal cannot reform a
                // caravan, so counting it mobile would hold the map open forever. Vehicles are
                // pawns but not RaceProps.Animal, so they still count.
                if (!pawn.Downed && !pawn.RaceProps.Animal) anyMobile = true;
            }

            if (anyMobile)
            {
                // LINGER: keep the map alive until the player's colonists leave. Strip only OUR
                // side's lords -- on a withdrawal the enemy garrison is still alive and must keep
                // its defend-base lord, or it degrades into uncoordinated lordless individuals.
                Faction empire = FindFC.EmpireFaction;
                foreach (Lord lord in map.lordManager.lords.ListFullCopy())
                    if (lord.faction == empire || lord.faction == Faction.OfPlayer)
                        map.lordManager.RemoveLord(lord);

                if (won)
                {
                    // Won: the garrison is cleared; idle the surviving Empire mercs as loot-phase
                    // guards until the player leaves.
                    List<Pawn> idlers = new List<Pawn>();
                    foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                        if (pawn.Faction == empire && !pawn.Dead && !pawn.Downed) idlers.Add(pawn);
                    foreach (Pawn p in idlers) p.jobs?.StopAll();
                    if (idlers.Any())
                        LordMaker.MakeNewLord(empire, new LordJob_ColonistsIdle(null), map, idlers);
                }
                else
                {
                    // Withdrawn (a loss never lingers -- mobile colonists count as standing
                    // attackers): the garrison is still alive, so idling mercs among live hostiles
                    // just gets them shot. Extract the squad off-map now.
                    SquadMapTeardownUtil.PreserveEmpirePawns(map);
                    FindFC.Military?.TryAutoReplaceAllSquads();
                }
                ReturnPlayerColonistsHome(won, playerColonists.Where(p => p.Downed).ToList());
                return true;
            }

            CloseOffenseMap(won, playerColonists);
            return false;
        }

        /// <summary>Finishes a loot linger once the last mobile player colonist has left the map
        /// (via vanilla caravan/pod extraction), then closes the map and releases this context if
        /// its ops already detached. Called from OffenseTick while awaitingPlayerExit.</summary>
        private void FinishLingerIfEmpty()
        {
            if (map is null)
            {
                awaitingPlayerExit = false;
                isOffense = false;
                ReleaseBattlefieldIfOrphaned();
                return;
            }

            List<Pawn> playerColonists = new List<Pawn>();
            bool anyMobile = false;
            // Snapshot: the merc-reclaim SetFaction below mutates the spawned-pawn list
            // (see TeardownOffenseMap).
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (pawn.Faction != Faction.OfPlayer)
                {
                    if (pawn.IsPrisonerOfColony) playerColonists.Add(pawn);
                    continue;
                }
                // Reclaim any squad merc found in OfPlayer back to Empire (undrafts it) so it is
                // preserved off-map on close rather than delivered home, and so it never keeps the
                // linger alive as a false "player colonist still looting". Drafting is blocked once
                // the linger starts, so this is a safety net for older saves / other-mod faction flips.
                if (pawn.IsMercenary())
                {
                    pawn.SetFaction(FindFC.EmpireFaction);
                    continue;
                }
                playerColonists.Add(pawn);
                if (!pawn.Downed && !pawn.RaceProps.Animal) { anyMobile = true; break; }
            }
            if (anyMobile) return;   // player colonists still on-map looting

            CloseOffenseMap(offenseWon, playerColonists);
            ClearAllOpPawns();
            awaitingPlayerExit = false;
            isOffense = false;
            ReleaseBattlefieldIfOrphaned();
        }

        /// <summary>Drops this context from the manager once its last op has already detached.
        /// Normally Detach removes the context, but an op that resolves mid-linger detaches while
        /// the player is still on the map, so no later Detach fires -- without this, the context
        /// (with stale battleMapInitialized/offenseWon flags) leaks in the battlefields dict.</summary>
        private void ReleaseBattlefieldIfOrphaned()
        {
            if (activeOps is object && activeOps.Count > 0) return;
            FindFC.MilitaryManager?.RemoveBattlefield(tile);
        }

        /// <summary>Common close path (immediate or linger-end): remove lords, return the player's
        /// downed colonists home, preserve the Empire squad off-map (the defense-map hook that does
        /// this never fires on an enemy-Settlement map, so it is invoked explicitly here), deinit the
        /// map, and complete any deferred Capture swap.</summary>
        private void CloseOffenseMap(bool won, List<Pawn> playerColonists)
        {
            if (map is null) return;

            foreach (Lord lord in map.lordManager.lords.ListFullCopy())
                map.lordManager.RemoveLord(lord);

            // Player colonists (Join Attack) return home injured; Empire squad mercs are NOT routed
            // through the delivery event -- the squad holds them off-map via the preserve helper.
            ReturnPlayerColonistsHome(won, playerColonists);
            SquadMapTeardownUtil.PreserveEmpirePawns(map);
            FindFC.Military?.TryAutoReplaceAllSquads();

            Map homeMap = Find.AnyPlayerHomeMap;
            CameraJumper.TryJump(tile);
            if (Find.CurrentMap == map && homeMap is object) Current.Game.CurrentMap = homeMap;
            Current.Game.DeinitAndRemoveMap(map, false);
            map = null;
            battleMapInitialized = false;

            CompletePendingSettlementFate();
        }

        /// <summary>Records a deferred Capture: a manual Capture win completes the settlement->colony
        /// swap on map teardown (see <see cref="CompletePendingSettlementFate"/>) rather than during
        /// CompleteBattle, so the live battle map is never destroyed under the player.</summary>
        public void RegisterPendingCapture(string name, TechLevel tech)
        {
            pendingCapture = true;
            pendingCaptureName = name;
            pendingCaptureTech = tech;
        }

        /// <summary>Records a deferred Raze: a manual Raze win destroys the enemy settlement on map
        /// teardown rather than during CompleteBattle, so the live battle map is never destroyed
        /// under the player.</summary>
        public void RegisterPendingRaze()
        {
            pendingRaze = true;
        }

        /// <summary>Completes a deferred Capture/Raze once the map is gone: destroys the enemy
        /// settlement world object at this tile and (for Capture) stands up the Empire colony in its
        /// place via the same ColonyUtil.SetupCapturedSettlement used by the abstract path.</summary>
        private void CompletePendingSettlementFate()
        {
            if (!pendingCapture && !pendingRaze) return;
            bool capture = pendingCapture;
            pendingCapture = false;
            pendingRaze = false;

            Settlement enemy = Find.WorldObjects.SettlementAt(tile);
            Faction enemyFaction = enemy?.Faction;
            if (enemy is object && !enemy.Destroyed)
                enemy.Destroy();

            // Mark the faction defeated if it has no settlements left (mirrors ApplyCaptureSuccess /
            // ApplyRazeSuccess).
            if (enemyFaction is object &&
                !Find.WorldObjects.Settlements.Any(s => s.Faction != null && s.Faction == enemyFaction))
                enemyFaction.defeated = true;

            if (capture)
                ColonyUtil.SetupCapturedSettlement(tile, pendingCaptureName, pendingCaptureTech);
            pendingCaptureName = null;
            pendingCaptureTech = TechLevel.Undefined;
        }

        /// <summary>Despawns the given player colonists, tends the injured, and delivers them back to
        /// the home settlement via a DeliveryEvent (the DeleteMap injured-return recipe, reimplemented
        /// against <c>tile</c> and the op's home settlement). A loss adds a one-day return penalty,
        /// matching defense. Empire squad mercs are handled separately by the preserve helper.</summary>
        private void ReturnPlayerColonistsHome(bool won, List<Pawn> pawns)
        {
            if (pawns is null || pawns.Count == 0) return;

            foreach (Pawn pawn in pawns)
                if (pawn.Spawned) pawn.DeSpawn();
            foreach (Pawn pawn in pawns)
                if (!pawn.Dead)
                {
                    int guard = 0;
                    while (pawn.health.HasHediffsNeedingTend())
                    {
                        if (++guard > 10000) { LogUtil.Error("ReturnPlayerColonistsHome: too many tend iterations."); break; }
                        TendUtility.DoTend(null, pawn, null);
                    }
                }

            Map home = Find.AnyPlayerHomeMap;
            if (home is null) return;
            // Travel is from the battle site (this tile) -- the pawns are physically at the enemy
            // settlement, not at the squad's home billet.
            int travelTicks = TravelUtil.ReturnTicksToArrive(tile, home.Tile);
            if (!won) travelTicks += GenDate.TicksPerDay;

            List<Thing> goods = new List<Thing>(pawns.Count);
            foreach (Pawn pawn in pawns) goods.Add(pawn);

            DeliveryEvent.CreateDeliveryEvent(new FCEvent
            {
                location = home.Tile,
                source = tile,
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
    }
}
