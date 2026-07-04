using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;
using Verse.Sound;

namespace FactionColonies
{
    /// <summary>
    /// Per-tile shared object that owns the battle map, pawn lists, lords, and wave state for
    /// every <see cref="MilitaryOperation"/> targeting a single tile.
    /// <para>One <c>BattlefieldContext</c> exists per active battle tile, regardless of how many
    /// concurrent ops it serves. When two ops target the same tile (e.g. two attackers on one
    /// settlement) they share the map: the second op's pawns spawn into the existing map and
    /// either join the existing attacker lord or get a fresh one if the previous lord was
    /// destroyed.</para>
    /// <para>Lifecycle: a context outlives any single op. After the last op detaches, if the
    /// player is still on the map the context enters <see cref="awaitingPlayerExit"/>; a new
    /// op arriving in that state re-engages by calling <see cref="TryReengage"/>. The context
    /// is destroyed once <see cref="activeOps"/> is empty AND the player has left.</para>
    /// </summary>
    public partial class BattlefieldContext : IExposable
    {
        /// <summary>Tick interval for the orphan-flag clearing sweep in <see cref="Tick"/>.</summary>
        private const int OrphanCheckTickInterval = 2500;

        public PlanetTile tile = PlanetTile.Invalid;

        /// <summary>The battle map. Lazy: created when the first op transitions to Engaged on this tile.</summary>
        public Map map;

        /// <summary>Ops currently using this battlefield. The context cannot be destroyed while non-empty.</summary>
        public List<MilitaryOperation> activeOps = new List<MilitaryOperation>();

        /// <summary>True when the last op has resolved but the player is still on-map.
        /// A new op arriving in this state triggers <see cref="TryReengage"/>.</summary>
        public bool awaitingPlayerExit;

        /* -*-*-*-*- Battle state -*-*-*-*-
         * Per-side pawn ownership lives on op.aggressor.pawns / op.defender.pawns. The flat
         * <see cref="attackerPawns"/> / <see cref="defenderPawns"/> properties below are derived
         * aggregations across all active ops at this tile, used by Tick / RemoveAttacker /
         * RemoveDefender / DeleteMap and any external readers (UI, debug).
         *
         * <see cref="draftedNPCs"/> stays as a flat list — it tracks NPCs the player drafted
         * during this tile's battles (faction-restored on map cleanup), with no natural per-op
         * owner. Only <c>DeleteMap</c> consumes it.
         */

        public List<Pawn> draftedNPCs = new List<Pawn>();

        /* Loose items present on the defense map at generation time -- the settlement's own
         * "stuff". Snapshotted by RecordSettlementLoot() before any pawns spawn, so anything
         * dropped later in the fight (attacker spoils, fallen reinforcements' gear) is absent
         * and remains takeable. Consumed by the caravan-reform restriction patch via
         * IsSettlementLoot(). */
        public HashSet<Thing> settlementLoot = new HashSet<Thing>();

        /* Non-combatant settlement inhabitants recruited onto this battle map (see
         * RecruitMapInhabitants). Used only to draw their on-map name labels as a dimmed
         * version of the defenders' label color, so the player can tell the fighting squad
         * from civilian filler at a glance. */
        public HashSet<Pawn> civilianPawns = new HashSet<Pawn>();

        public bool battleMapInitialized;
        // True once an offensive op has attached to this tile. Distinguishes offense contexts
        // (no Empire ParentSettlement, ticked by the FactionFC sweep, torn down by EndOffense)
        // from defense contexts (comp-ticked).
        public bool isOffense;
        public bool endingBattle;
        public bool shuttleLandingPending;
        public string pendingDeliveryMessage;

        /// <summary>All attacker pawns across every active op at this tile (derived).</summary>
        public IEnumerable<Pawn> attackerPawns =>
            activeOps == null
                ? Enumerable.Empty<Pawn>()
                : activeOps.SelectMany(o => o?.aggressor?.pawns ?? Enumerable.Empty<Pawn>());

        /// <summary>All defender pawns across every active op at this tile (derived).</summary>
        public IEnumerable<Pawn> defenderPawns =>
            activeOps == null
                ? Enumerable.Empty<Pawn>()
                : activeOps.SelectMany(o => o?.defender?.pawns ?? Enumerable.Empty<Pawn>());

        /* Attackers/defenders still able to fight: excludes downed, dead, and truly-gone pawns.
         * Battle-end decisions use these (not the raw attacker/defenderPawns) so an incapacitated
         * combatant ends the fight even when no Lord notification fired to remove it. RimWorld's
         * MakeDowned only notifies the pawn's OWN Lord; a lordless combatant (e.g. a drafted-then-
         * downed Empire merc, or a pawn added via RegisterPawnsAsDefenders(assignToLord:false)) is
         * otherwise never pruned on downing and would keep the battle alive until it actually dies. */
        public IEnumerable<Pawn> standingAttackerPawns =>
            attackerPawns.Where(p => p != null && !p.Dead && !p.Downed && !IsPawnTrulyGone(p));

        public IEnumerable<Pawn> standingDefenderPawns =>
            defenderPawns.Where(p => p != null && !p.Dead && !p.Downed && !IsPawnTrulyGone(p));

        /// <summary>Sum of every active op's defender <c>initialPawnCount</c>. Used by manual
        /// battle resolution to build the <see cref="BattleResult"/> (defender initial vs
        /// remaining drives overwhelming-victory detection in <c>op.CompleteBattle</c>).</summary>
        public int initialDefenderCount =>
            activeOps == null ? 0 : activeOps.Sum(o => o?.defender?.initialPawnCount ?? 0);

        public BattlefieldContext() { }

        public BattlefieldContext(PlanetTile tile)
        {
            this.tile = tile;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref tile, "tile", PlanetTile.Invalid);
            Scribe_References.Look(ref map, "map");
            Scribe_Collections.Look(ref activeOps, "activeOps", LookMode.Reference);
            Scribe_Values.Look(ref awaitingPlayerExit, "awaitingPlayerExit", false);

            Scribe_Collections.Look(ref draftedNPCs, "draftedNPCs", LookMode.Reference);
            Scribe_Collections.Look(ref settlementLoot, "settlementLoot", LookMode.Reference);
            Scribe_Collections.Look(ref civilianPawns, "civilianPawns", LookMode.Reference);
            Scribe_Values.Look(ref battleMapInitialized, "battleMapInitialized", false);
            Scribe_Values.Look(ref isOffense, "isOffense", false);
            Scribe_Values.Look(ref endingBattle, "endingBattle", false);
            Scribe_Values.Look(ref shuttleLandingPending, "shuttleLandingPending", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (activeOps is null) activeOps = new List<MilitaryOperation>();
                if (draftedNPCs is null) draftedNPCs = new List<Pawn>();
                if (settlementLoot is null) settlementLoot = new HashSet<Thing>();
                if (civilianPawns is null) civilianPawns = new HashSet<Pawn>();
            }
        }

        /* -*-*-*-*- Per-op pawn helpers -*-*-*-*- */

        /// <summary>Removes <paramref name="pawn"/> from whichever op's defender pawn list owns it,
        /// if any. Use after the pawn becomes unavailable (downed, dead, drafted into caravan, ...).</summary>
        public void RemoveDefenderPawn(Pawn pawn)
        {
            if (activeOps is null) return;
            foreach (MilitaryOperation op in activeOps)
            {
                if (op?.defender?.pawns is null) continue;
                if (op.defender.pawns.Remove(pawn)) return;
            }
        }

        /// <summary>Removes <paramref name="pawn"/> from whichever op's aggressor pawn list owns it.</summary>
        public void RemoveAttackerPawn(Pawn pawn)
        {
            if (activeOps is null) return;
            foreach (MilitaryOperation op in activeOps)
            {
                if (op?.aggressor?.pawns is null) continue;
                if (op.aggressor.pawns.Remove(pawn)) return;
            }
        }

        /// <summary>Drops null / destroyed / orphan-despawned pawns from every op's pawn lists.</summary>
        public void PruneStalePawns()
        {
            if (activeOps is null) return;
            foreach (MilitaryOperation op in activeOps)
            {
                op?.aggressor?.pawns?.RemoveAll(IsPawnTrulyGone);
                op?.defender?.pawns?.RemoveAll(IsPawnTrulyGone);
            }
        }

        /// <summary>Clears every op's pawn lists. Called after battle resolution to release
        /// pawn references before the ops detach.</summary>
        public void ClearAllOpPawns()
        {
            if (activeOps is null) return;
            foreach (MilitaryOperation op in activeOps)
            {
                op?.aggressor?.pawns?.Clear();
                op?.defender?.pawns?.Clear();
            }
        }

        /* -*-*-*-*- Lookups -*-*-*-*- */

        /// <summary>The Empire settlement at this battlefield's tile, or null for non-Empire raid targets.</summary>
        public WorldSettlementFC ParentSettlement => Find.WorldObjects.WorldObjectAt<WorldSettlementFC>(tile);

        /// <summary>The Empire settlement's military comp, or null.</summary>
        public WorldObjectComp_SettlementMilitary ParentMilitaryComp => ParentSettlement?.MilitaryComp;

        /* -*-*-*-*- Tick -*-*-*-*-
         * Per-tick battlefield housekeeping: orphan flag clearing, stale pawn cleanup, untracked
         * player pawn capture, stuck-battle detection. Called once per game tick by the comp.
         */

        public void Tick()
        {
            WorldSettlementFC settlement = ParentSettlement;
            if (settlement is null) return;

            bool isUnderAttack = FindFC.MilitaryManager?.HasDefenseAt(settlement) ?? false;
            if (!isUnderAttack) return;
            if (endingBattle) return;

            int ticks = Find.TickManager.TicksGame;

            // Periodic orphan flag clearing: isUnderAttack is true but no map / no combatants
            // and no warning event in queue.
            if (ticks % OrphanCheckTickInterval == 0 && map is null && !attackerPawns.Any() && !defenderPawns.Any())
            {
                FCEvent evt = MilitaryOperationsUtil.ReturnMilitaryEventByLocation(settlement.Tile);
                if (evt is null)
                {
                    LogUtil.Warning($"Clearing orphaned isUnderAttack flag on {settlement.Name} " +
                        $"(no matching settlementBeingAttacked event in queue).");
                    ParentMilitaryComp?.ClearAttackState();
                    return;
                }
            }

            if (ticks % 250 != 0) return;
            if (map == null) return;

            // Clean stale references: null (save/load), destroyed, or despawned-without-holder.
            // Pod-bound pawns in a descending Skyfaller are !Spawned but ParentHolder != null;
            // they stay tracked until the pod opens.
            PruneStalePawns();

            // Detect untracked player pawns on the battle map (e.g. shuttle-delivered pawns
            // that spawned via the Unload job after the ArrivePatch fired). Attribute them to
            // the primary defensive op (no natural per-op owner; the first defensive op is the
            // canonical bench).
            MilitaryOperation primaryDef = FirstDefensiveOp();
            var defenderSet = new HashSet<Pawn>(defenderPawns);
            Lord battleLord = null;
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.Faction != Faction.OfPlayer) continue;
                if (pawn.Dead || pawn.Downed) continue;
                if (!defenderSet.Contains(pawn))
                {
                    LogUtil.Warning($"Registering untracked player pawn {pawn.LabelShort} with defense at {settlement.Name}");
                    if (primaryDef?.defender?.pawns is object)
                    {
                        primaryDef.defender.pawns.Add(pawn);
                        primaryDef.defender.initialPawnCount++;
                    }
                }
                // Skip lord assignment for non-humanlike pawns. VehiclePawns conflict with
                // Lord duties (see VehicleFrameworkCompat.cs); animals aren't given duties.
                if (pawn.RaceProps?.intelligence != Intelligence.Humanlike) continue;
                if (pawn.GetLord() is null)
                {
                    if (battleLord is null)
                        battleLord = defenderPawns.FirstOrDefault(d => d.GetLord() != null)?.GetLord();
                    if (battleLord != null && !battleLord.ownedPawns.Contains(pawn))
                        battleLord.AddPawn(pawn);
                }
            }

            // Don't declare stuck if attackers are still inbound in drop pods. "Standing" pawns,
            // not the raw lists, so an incapacitated-but-lordless combatant (never pruned via
            // Notify_PawnLost) still resolves the battle instead of dragging it out until it dies.
            bool attackersGone = !standingAttackerPawns.Any() && !HasPendingPodAttackers();
            if (attackersGone || !standingDefenderPawns.Any())
            {
                LogUtil.Warning($"Stuck battle detected at {settlement.Name}, forcing resolution.");
                endingBattle = true;
                LongEventHandler.QueueLongEvent(EndAttack,
                    "EndingAttack", false, error =>
                    {
                        DelayedErrorWindowRequest.Add("FCErrorEndingAttack".Translate(),
                            "FCErrorEndingAttackDescription".Translate());
                        LogUtil.Error(error.Message);
                    });
            }
        }

        /* -*-*-*-*- Static helpers -*-*-*-*- */

        /// <summary>Pod-bound pawns during Skyfaller descent are <c>!Spawned</c> but
        /// <c>ParentHolder != null</c>. A pure <c>!Spawned</c> check treats them as lost and
        /// triggers false victory.</summary>
        public static bool IsPawnTrulyGone(Pawn p)
        {
            if (p is null) return true;
            if (p.Destroyed) return true;
            if (p.Spawned) return false;
            if (p.ParentHolder is object) return false;
            return true;
        }

        /// <summary>Shared "is this a valid edge spawn cell" test: standable, unfogged, and
        /// reachable to the host faction base (or the biggest map-edge district if there is no
        /// host faction). Used by both <see cref="FindNearEdgeCell"/> and
        /// <see cref="FindEdgeCellAwayFromEnemies"/> so the two stay in lockstep.</summary>
        private static bool IsValidEdgeSpawnCell(IntVec3 x, Map map, Faction hostFaction)
        {
            if (!x.Standable(map) || x.Fogged(map)) return false;
            if (hostFaction is object && map.reachability.CanReachFactionBase(x, hostFaction)) return true;
            return hostFaction is null && map.reachability.CanReachBiggestMapEdgeDistrict(x);
        }

        public static IntVec3 FindNearEdgeCell(Map map)
        {
            var hostFaction = map.ParentFaction;
            if (CellFinder.TryFindRandomEdgeCellWith(x => IsValidEdgeSpawnCell(x, map, hostFaction),
                    map, CellFinder.EdgeRoadChance_Neutral, out var result))
                return CellFinder.RandomClosewalkCellNear(result, map, 5);
            if (CellFinder.TryFindRandomEdgeCellWith(x => x.Standable(map) && !x.Fogged(map),
                    map, CellFinder.EdgeRoadChance_Neutral, out result))
                return CellFinder.RandomClosewalkCellNear(result, map, 5);
            LogUtil.Warning("Could not find any valid edge cell.");
            return CellFinder.RandomCell(map);
        }

        private static PawnsArrivalModeDef ResolveRaidArriveMode(IncidentParms parms)
        {
            return parms.raidStrategy.arriveModes
                .Where(mode => mode.Worker.CanUseWith(parms))
                .TryRandomElementByWeight(mode => mode.Worker.GetSelectionWeight(parms), out PawnsArrivalModeDef output)
                ? output
                : PawnsArrivalModeDefOf.EdgeWalkIn;
        }

        public bool HasPendingPodAttackers()
        {
            if (activeOps is null) return false;
            foreach (MilitaryOperation op in activeOps)
            {
                List<Pawn> list = op?.aggressor?.pawns;
                if (list is null) continue;
                foreach (Pawn p in list)
                {
                    if (p is null || p.Destroyed || p.Dead) continue;
                    if (!p.Spawned && p.ParentHolder is object) return true;
                }
            }
            return false;
        }

        /* -*-*-*-*- Op lifecycle (Join/Detach) -*-*-*-*- */

        /// <summary>Register <paramref name="op"/> on this battlefield. Idempotent. Clears
        /// <see cref="awaitingPlayerExit"/>. Pawn / lord spawning is the caller's responsibility
        /// (typically via <see cref="StartDefense"/> or <see cref="SpawnParticipantOnMap"/>).</summary>
        public void Join(MilitaryOperation op)
        {
            if (op is null) return;
            if (activeOps is null) activeOps = new List<MilitaryOperation>();
            if (!activeOps.Contains(op)) activeOps.Add(op);
            awaitingPlayerExit = false;
        }

        /// <summary>Remove <paramref name="op"/> from this battlefield. If no ops remain and the
        /// player is still on the map, transitions into <see cref="awaitingPlayerExit"/>;
        /// otherwise the context is removed from the manager.</summary>
        public void Detach(MilitaryOperation op)
        {
            if (op is null) return;
            activeOps?.Remove(op);

            if (activeOps == null || activeOps.Count == 0)
            {
                // Use AllPawnsSpawned + faction filter so VehiclePawns count as "player on map".
                // FreeColonistsSpawnedCount would miss a parked vehicle and prematurely
                // tear down the battlefield context while the player is still around.
                bool playerOnMap = false;
                if (map is object && map.mapPawns is object)
                {
                    foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                    {
                        if (pawn.Faction == Faction.OfPlayer) { playerOnMap = true; break; }
                    }
                }
                if (playerOnMap)
                {
                    awaitingPlayerExit = true;
                }
                else
                {
                    awaitingPlayerExit = false;
                    FindFC.MilitaryManager?.RemoveBattlefield(tile);
                }
            }
        }

        /// <summary>Spawns an op's aggressor or defender pawns onto this battlefield and attaches
        /// them to the appropriate lord. Public op-aware entry point for handlers / submods that
        /// want to inject reinforcements mid-battle.</summary>
        public void SpawnParticipantOnMap(MilitaryOperation op, ParticipantSide side)
        {
            if (op is null || map is null) return;

            if (side == ParticipantSide.Defender)
            {
                // Squad-based defender (foreign settlement reinforcement).
                if (op.defender?.squad is object)
                {
                    SpawnReinforcementsForOp(op);
                    return;
                }
                // No squad — generate fallback pawns from force points using the same path used
                // for the initial defending settlement's spawn.
                if (op.defender?.force is null) return;
                GenerateFriendlies(op);
                return;
            }

            // Aggressor side: hostile faction raid spawn.
            SpawnAttackersForOp(op);
        }

        /// <summary>Called when a new op joins while <see cref="awaitingPlayerExit"/> is true.
        /// Spawns fresh attacker pawns/lord on the existing map; if defender pawns survived,
        /// re-recruits them into a fresh defender lord.</summary>
        public void TryReengage(MilitaryOperation op)
        {
            if (op is null || map is null) return;

            awaitingPlayerExit = false;
            battleMapInitialized = true;
            endingBattle = false;

            // Re-recruit any surviving Empire defenders from idle lords back into a defense lord
            RecruitIdleDefenders(op);

            // Spawn fresh attackers for the new op
            SpawnAttackersForOp(op);
        }

        /* -*-*-*-*- Map generation -*-*-*-*- */

        /// <summary>Get-or-create the battle map at this tile. Idempotent.</summary>
        public Map GenerateMap()
        {
            if (map is object) return map;
            WorldSettlementFC settlement = ParentSettlement;
            if (settlement is null)
            {
                LogUtil.Error($"BattlefieldContext.GenerateMap: no Empire settlement at tile {tile}; cannot generate map.");
                return null;
            }
            int size = settlement.DefenseMapSize;
            map = MapGenerator.GenerateMap(
                new IntVec3(size, 1, size),
                settlement, settlement.MapGeneratorDef, settlement.ExtraGenStepDefs);
            return map;
        }

        /* Removes hostile-faction Things spawned by vanilla landmark / tile-mutator map
           generation (ancient turrets, drone-trap dispensers, dormant mech/insect clusters,
           hives, insect lair entrances, etc.) so they don't attack the Empire's defenders
           and the incoming raid alike. Defense-only -- manual offensive battles on enemy
           settlements legitimately want this content active, so this is intentionally
           NOT called from GenerateMap() itself. */
        private void StripLandmarkHostiles()
        {
            if (map is null) return;

            Faction player = Faction.OfPlayer;
            Faction empireFaction = FindFC.EmpireFaction;
            if (player is null) return;

            List<Thing> toDestroy = new List<Thing>();
            foreach (Thing thing in map.listerThings.AllThings)
            {
                if (thing.Faction is null) continue;
                if (thing.Faction == empireFaction) continue;
                if (!thing.Faction.HostileTo(player)) continue;
                toDestroy.Add(thing);
            }

            foreach (Thing thing in toDestroy)
            {
                if (!thing.Destroyed) thing.Destroy(DestroyMode.Vanish);
            }

            if (toDestroy.Count > 0)
                LogUtil.Message($"StripLandmarkHostiles: removed {toDestroy.Count} hostile pre-existing thing(s) from defense map at tile {map.Tile}");
        }

        /* Removes the settlement's own defensive turrets that the player hasn't unlocked. The
           defense map is generated by whatever base-generation system is installed -- VBGE
           (which produces a full outlander/empire base) or Empire's own edgeDefense path -- and
           neither gates turret placement on player research, so an early-game empire could field
           auto-turrets the player has no way to build. This strips any Empire-owned turret whose
           research prerequisites the player hasn't finished (using the same "all prerequisites
           met" test the game applies before letting the player place a building). Turrets with no
           prerequisites, and non-Empire turrets (already removed by StripLandmarkHostiles), are
           left alone. Defense-only, for the same reason StripLandmarkHostiles is. */
        private void StripUnresearchedTurrets()
        {
            if (map is null) return;

            Faction empireFaction = FindFC.EmpireFaction;
            if (empireFaction is null) return;

            List<Thing> toDestroy = new List<Thing>();
            foreach (Thing thing in map.listerThings.AllThings)
            {
                if (thing.Faction != empireFaction) continue;
                if (!(thing.def.building?.IsTurret ?? false)) continue;

                List<ResearchProjectDef> prereqs = thing.def.researchPrerequisites;
                if (prereqs is null) continue;
                if (prereqs.All(r => r.IsFinished)) continue;

                toDestroy.Add(thing);
            }

            foreach (Thing thing in toDestroy)
            {
                if (!thing.Destroyed) thing.Destroy(DestroyMode.Vanish);
            }

            if (toDestroy.Count > 0)
                LogUtil.Message($"StripUnresearchedTurrets: removed {toDestroy.Count} unresearched turret(s) from defense map at tile {map.Tile}");
        }

        /* Snapshots the loose items that exist on the freshly generated defense map -- the
           settlement's own resources/loot. Runs before defenders and attackers spawn (i.e.
           before ZoomIntoTile/SetupAttack), so battle spoils dropped later are NOT recorded and
           remain takeable. Used by the caravan-reform restriction patch to withhold the Empire's
           own stuff from the reform/transporter item list.

           When the setting is on, each item is also force-forbidden so the player can't have
           pawns manually equip/wear/haul it into inventory and carry it off (bypassing the reform
           filter). The forbidden state is locked by the CompForbiddable.Forbidden setter patch,
           which vetoes any un-forbid attempt on these items while the battle map exists. */
        private void RecordSettlementLoot()
        {
            if (settlementLoot is null) settlementLoot = new HashSet<Thing>();
            settlementLoot.Clear();
            if (map is null) return;

            bool forbid = FCSettings.restrictDefenseMapLoot;
            foreach (Thing thing in map.listerThings.AllThings)
            {
                if (thing.def.category != ThingCategory.Item) continue;
                settlementLoot.Add(thing);
                if (forbid) thing.SetForbidden(true, false);
            }

            if (settlementLoot.Count > 0)
                LogUtil.Message($"RecordSettlementLoot: marked {settlementLoot.Count} settlement item(s) as non-takeable on defense map at tile {map.Tile}");
        }

        /// <summary>True if the thing was part of the settlement's loot at map generation
        /// (i.e. the Empire's own property), as opposed to battle spoils dropped during the fight.</summary>
        public bool IsSettlementLoot(Thing t) => settlementLoot != null && settlementLoot.Contains(t);

        /* -*-*-*-*- Battle entry: StartDefense -*-*-*-*-
         * Called from op.OnEventFired's defensive branch (or via comp.StartDefence's facade) once
         * the warning event fires. Drives the three paths: add to existing battle, auto-resolve,
         * or fresh battle. Reads forces/factions from the op rather than the FCEvent.
         */

        public void StartDefense(MilitaryOperation op, Action after = null)
        {
            if (op is null) return;

            // Attach the op so attackerPawns / defenderPawns aggregations (derived from
            // activeOps) include this op's per-side pawn lists. Without this the stuck-battle
            // guard in Tick() trips on the next 250-tick boundary because aggregations read empty.
            op.AttachToBattlefield();

            // Drop the op's warning event from the queue if it's still there. Op.OnEventFired
            // already strips it from sourceEvents; this removes it from the FactionFC event list.
            FactionFC factionFC = FindFC.FactionComp;
            if (factionFC is object)
            {
                foreach (FCEvent srcEvt in op.sourceEvents)
                {
                    if (srcEvt is null) continue;
                    if (srcEvt.def == FCEventDefOf.settlementBeingAttacked)
                        factionFC.RemoveEvent(srcEvt);
                }
            }

            WorldSettlementFC settlement = ParentSettlement;
            if (settlement is null)
            {
                LogUtil.Error($"StartDefense: no Empire settlement at tile {tile}.");
                return;
            }

            bool isUnderAttack = FindFC.MilitaryManager?.HasDefenseAt(settlement) ?? false;

            // Path 1: existing battle map active — add a new attacker group.
            if (battleMapInitialized && map is object && isUnderAttack)
            {
                LongEventHandler.QueueLongEvent(() =>
                {
                    SpawnAttackersForOp(op);
                    SpawnReinforcementsForOp(op);

                    string enemyName = op.aggressor?.faction?.Name ?? "Unknown";
                    Find.LetterStack.ReceiveLetter(
                        "FCNewWaveArrived".Translate(settlement.Name),
                        "FCNewWaveArrivedDesc".Translate(settlement.Name, enemyName),
                        LetterDefOf.ThreatBig,
                        new LookTargets(settlement));
                    after?.Invoke();
                }, "GeneratingMap", false, null);
                return;
            }

            // Auto-resolve check
            bool shouldAutoResolve = false;
            if (FCSettings.battleMode == BattleMode.Auto || !settlement.settlementDef.supportsManualBattle)
            {
                shouldAutoResolve = true;
            }
            else if (FCSettings.battleMode == BattleMode.Hybrid)
            {
                shouldAutoResolve = !battleMapInitialized && !IsPlayerCaravanOnTile();
            }
            if (!shouldAutoResolve && FCSettings.maxConcurrentBattleMaps > 0
                && (FindFC.MilitaryManager?.CountLiveBattleMaps() ?? 0) >= FCSettings.maxConcurrentBattleMaps)
                shouldAutoResolve = true;

            if (shouldAutoResolve)
            {
                MilitaryForce defForce = op.defender?.force;
                MilitaryForce atkForce = op.aggressor?.force;
                if (defForce is null || atkForce is null)
                {
                    LogUtil.Warning($"StartDefense: missing force(s) for {settlement.Name} on auto-resolve path.");
                    EndBattle(false, 0, null);
                    return;
                }
                // Per-round auto-resolve: seed BattleResult and let the per-hour event clock
                // roll one round per hour until completion. comp.isUnderAttack /
                // comp.militaryBusy / squad.IsBusy all read manager state, so they continue
                // reflecting "engaged" through the duration. CompleteBattle (with its
                // settlement-side effects via MilitaryJobHandler_Defend.ApplyResult) fires
                // when the final round resolves.
                op.BeginAutoResolveProgress();

                // "Battle commenced" letter: the 24h warning announced the imminent attack;
                // this confirms it has now begun. Outcome letter still fires from the handler
                // at the end of the duration window.
                string enemyName = op.aggressor?.faction?.Name ?? "Unknown";
                Find.LetterStack.ReceiveLetter(
                    "FCAutoBattleCommenced".Translate(settlement.Name),
                    "FCAutoBattleCommencedDesc".Translate(settlement.Name, enemyName),
                    LetterDefOf.ThreatBig,
                    new LookTargets(settlement));
                return;
            }

            // Path 2: post-battle map exists, player still on it — re-engage.
            if (map is object && !isUnderAttack)
            {
                battleMapInitialized = true;

                LongEventHandler.QueueLongEvent(() =>
                {
                    RecruitIdleDefenders(op);
                    SpawnAttackersForOp(op);
                    SpawnReinforcementsForOp(op);
                    Find.TickManager.Notify_GeneratedPotentiallyHostileMap();

                    string enemyName = op.aggressor?.faction?.Name ?? "Unknown";
                    Find.LetterStack.ReceiveLetter(
                        "FCManualBattleStarted".Translate(settlement.Name),
                        "FCManualBattleStartedDesc".Translate(settlement.Name, enemyName),
                        LetterDefOf.ThreatBig,
                        new LookTargets(settlement));
                    after?.Invoke();
                }, "GeneratingMap", false, null);
                return;
            }

            // Path 3: fresh battle — generate map.
            if (op.defender?.force is null)
            {
                LogUtil.Warning($"StartDefense: defender force is null for {settlement.Name}, settlement loses by default.");
                EndBattle(false, 0, null);
                return;
            }

            LongEventHandler.QueueLongEvent(() =>
            {
                if (map == null)
                {
                    GenerateMap();
                    StripLandmarkHostiles();
                    StripUnresearchedTurrets();
                    RecordSettlementLoot();
                }
                ZoomIntoTile(op);
                SetupAttack(op);
                after?.Invoke();
            }, "GeneratingMap", false, GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap);
        }

        private void SetupAttack(MilitaryOperation op)
        {
            // ZoomIntoTile may have aborted via EndBattle (null force); don't spawn attackers
            // into a cleaned-up state.
            WorldSettlementFC settlement = ParentSettlement;
            if (settlement is null) return;
            bool isUnderAttack = FindFC.MilitaryManager?.HasDefenseAt(settlement) ?? false;
            if (!isUnderAttack) return;

            if (map is null)
            {
                LogUtil.Error($"SetupAttack: {settlement.Name} has no map. Resetting battle state.");
                EndBattle(false, 0, null);
                return;
            }

            if (op?.aggressor?.force is null || op.aggressor.faction is null)
            {
                LogUtil.Error($"SetupAttack: Missing attacking force or faction for {settlement.Name}. Resetting battle state.");
                EndBattle(false, 0, null);
                return;
            }

            SpawnAttackersForOp(op);
        }

        /* -*-*-*-*- Spawning -*-*-*-*- */

        /// <summary>Spawns attackers for a single op onto the current map. Adds them to the op's
        /// <c>aggressor.pawns</c> list (the flat <see cref="attackerPawns"/> accessor aggregates
        /// across ops). Creates a fresh <see cref="LordJob_HuntColonists"/> for the spawned group;
        /// the LordManager owns it from there (read it back via <c>pawn.GetLord()</c>).</summary>
        private void SpawnAttackersForOp(MilitaryOperation op)
        {
            if (map is null || op?.aggressor?.force is null) return;

            WorldSettlementFC settlement = ParentSettlement;
            int initial = op.aggressor.initialPawnCount;
            List<Pawn> newAttackers = SpawnForceMatchedGroup(
                op.aggressor.force, op.aggressor.faction, op.aggressor.pawns, ref initial,
                (pawns, spawnCenter, arriveMode) =>
                    new LordJob_HuntColonists(settlement, arriveMode != PawnsArrivalModeDefOf.CenterDrop));
            op.aggressor.initialPawnCount = initial;

            if (!newAttackers.Any())
            {
                LogUtil.Error("Got no pawns spawning attackers for op id=" + op.id);
                if (!attackerPawns.Any())
                    LongEventHandler.QueueLongEvent(EndAttack, "EndingAttack", false, null);
            }
        }

        /// <summary>
        /// Shared force-matched hostile/garrison spawner. Builds raid <see cref="IncidentParms"/>
        /// sized from <paramref name="force"/> via the same AdjustedRaidPoints(forceRemaining * 175,
        /// ...) formula (clamped to a 300-point minimum), generates + arrives the pawns, applies
        /// combat-efficiency hediffs from <c>force.militaryEfficiency</c>, appends them to
        /// <paramref name="targetPawnList"/>, bumps <paramref name="initialPawnCount"/>, and attaches
        /// the lord the caller supplies via <paramref name="lordFactory"/>. Returns the spawned
        /// pawns (empty on failure). Role-agnostic: <see cref="SpawnAttackersForOp"/> is one caller
        /// (aggressor side, LordJob_HuntColonists), the offense garrison is the other (defender side,
        /// LordJob_DefendBase).
        /// </summary>
        public List<Pawn> SpawnForceMatchedGroup(
            MilitaryForce force, Faction faction, List<Pawn> targetPawnList,
            ref int initialPawnCount,
            Func<List<Pawn>, IntVec3, PawnsArrivalModeDef, LordJob> lordFactory)
        {
            List<Pawn> spawned = new List<Pawn>();
            if (map is null || force is null || faction is null || targetPawnList is null) return spawned;

            IncidentParms parms = new IncidentParms
            {
                target = map,
                faction = faction,
                generateFightersOnly = true,
                raidStrategy = RaidStrategyDefOf.ImmediateAttack,
                raidNeverFleeIndividual = true
            };
            parms.points = Math.Max(
                IncidentWorker_Raid.AdjustedRaidPoints(
                    (float)force.forceRemaining * 175,
                    PawnsArrivalModeDefOf.EdgeWalkIn, parms.raidStrategy,
                    parms.faction, PawnGroupKindDefOf.Combat,
                    parms.target),
                300f);
            parms.raidArrivalMode = ResolveRaidArriveMode(parms);
            // TryResolveRaidSpawnCenter can fail (e.g. EdgeWalkIn when no pawn can path to an edge
            // cell). A failed resolve leaves parms.spawnCenter invalid, degenerating Arrive() into a
            // raw mid-map GenSpawn. Fall back to a guaranteed edge walk-in on a valid edge cell.
            if (!parms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(parms))
            {
                parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
                parms.spawnCenter = FindNearEdgeCell(map);
                parms.spawnRotation = Rot4.FromAngleFlat((map.Center - parms.spawnCenter).AngleFlat);
            }

            spawned = PawnGroupMakerUtility.GeneratePawns(
                IncidentParmsUtility.GetDefaultPawnGroupMakerParms(
                    PawnGroupKindDefOf.Combat, parms, true)).ToList();
            if (!spawned.Any())
            {
                LogUtil.Error($"SpawnForceMatchedGroup: got no pawns for faction {faction.Name} from parms {parms}");
                return spawned;
            }

            double efficiency = force.militaryEfficiency;
            foreach (Pawn p in spawned)
                MilitaryEfficiencyUtil.ApplyCombatEfficiencyHediff(p, efficiency);

            parms.raidArrivalMode.Worker.Arrive(spawned, parms);

            targetPawnList.AddRange(spawned);
            initialPawnCount += spawned.Count;

            LordJob lordJob = lordFactory(spawned, parms.spawnCenter, parms.raidArrivalMode);
            if (lordJob is object)
                LordMaker.MakeNewLord(faction, lordJob, map, spawned);
            return spawned;
        }

        /// <summary>Spawns reinforcement defenders for an op if a foreign defending settlement's
        /// squad is available. Does NOT generate random pawns if the squad is deployed. Adds the
        /// spawned pawns to the op's <c>defender.pawns</c> (the flat <see cref="defenderPawns"/>
        /// accessor aggregates across ops); rolls them into the existing defender lord if present,
        /// otherwise creates one.</summary>
        private void SpawnReinforcementsForOp(MilitaryOperation op)
        {
            if (map is null || op?.defender?.force is null) return;

            WorldSettlementFC ourSettlement = ParentSettlement;

            WorldSettlementFC home = op.defender.force.homeSettlement;
            if (home?.MilitaryComp is null) return;
            // Don't spawn reinforcements from the home settlement defending itself — already handled
            if (home == ourSettlement) return;

            // Use the squad recorded on the op (set at op creation by ApplyAutoDefenderSelection)
            MercenarySquadFC squad = op.defender.squad;
            if (squad is null
                || squad.outfit is null
                || !squad.mercenaries.Any()
                || squad.Deployment.IsPhysicallyDeployed()) return;

            squad.CheckInitialization();
            squad.UpdateSquadStats(op.defender.force.homeSettlement.settlementMilitaryLevel);
            SquadHealthUtil.ResetNeeds(squad);

            // SpawnableMercenaryPawns filters downed mercs out of the reinforcement wave —
            // they stay at base to recover from the previous engagement.
            List<Pawn> reinforcements = squad.SpawnableMercenaryPawns.ToList();

            double efficiency = op.defender.force.militaryEfficiency;
            foreach (Pawn merc in reinforcements)
            {
                MilitaryEfficiencyUtil.ShiftPawnGearQuality(merc, efficiency);
                MilitaryEfficiencyUtil.ApplyCombatEfficiencyHediff(merc, efficiency);
            }
            Lord defenseLord = defenderPawns.FirstOrDefault()?.GetLord();
            var spawnedReinforcements = new List<Pawn>();

            foreach (Pawn friendly in reinforcements)
            {
                if (friendly.IsWildMan()) continue;
                friendly.ApplyIdeologyRitualWounds();

                IntVec3 loc;
                if (!CellFinder.TryFindRandomCellNear(map.Center, map, 20, c => c.Standable(map), out loc))
                    loc = map.Center;

                try
                {
                    GenSpawn.Spawn(friendly, loc, map, new Rot4());
                    friendly.drafter = new Pawn_DraftController(friendly);
                    map.mapPawns.RegisterPawn(friendly);
                    // Load reloadable-weapon magazines from carried ammo (e.g. Yayo's Combat); no-op otherwise.
                    ReloadableWeaponUtil.LoadMagazinesFromInventory(friendly);
                    spawnedReinforcements.Add(friendly);
                }
                catch (Exception e)
                {
                    LogUtil.Warning($"Failed to spawn reinforcement {friendly.LabelShort}: {e}");
                }
            }

            if (spawnedReinforcements.Any())
            {
                if (defenseLord is object)
                {
                    foreach (Pawn pawn in spawnedReinforcements)
                        defenseLord.AddPawn(pawn);
                }
                else
                {
                    LordMaker.MakeNewLord(FindFC.EmpireFaction,
                        new LordJob_DefendColony(ourSettlement, new Dictionary<Pawn, Pawn>()),
                        map, spawnedReinforcements);
                }

                op.defender.pawns.AddRange(spawnedReinforcements);
                op.defender.initialPawnCount += spawnedReinforcements.Count;
            }
        }

        /// <summary>Re-recruits surviving Empire NPC defenders from their idle lord into a new
        /// defense lord. Used when reusing a post-battle map for a new attack (Path 2 + reengage).
        /// Recruited pawns are attributed to the joining op's defender participant.</summary>
        private void RecruitIdleDefenders(MilitaryOperation op)
        {
            if (map is null) return;
            WorldSettlementFC settlement = ParentSettlement;
            if (settlement is null) return;
            Faction empireFaction = FindFC.EmpireFaction;
            var idleDefenders = new List<Pawn>();

            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.Dead || pawn.Downed) continue;
                if (pawn.Faction != empireFaction && pawn.Faction != Faction.OfPlayer) continue;
                if (pawn.IsPrisonerOfColony) continue;
                idleDefenders.Add(pawn);
            }

            foreach (Pawn pawn in idleDefenders)
            {
                Lord lord = pawn.GetLord();
                if (lord != null)
                    lord.Notify_PawnLost(pawn, PawnLostCondition.LeftVoluntarily);
            }

            if (idleDefenders.Any())
            {
                LordMaker.MakeNewLord(empireFaction,
                    new LordJob_DefendColony(settlement, new Dictionary<Pawn, Pawn>()),
                    map, idleDefenders);
                if (op?.defender?.pawns is object)
                {
                    op.defender.pawns.AddRange(idleDefenders);
                    op.defender.initialPawnCount += idleDefenders.Count;
                }
            }
        }

        private void ZoomIntoTile(MilitaryOperation op)
        {
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
            if (battleMapInitialized) return;

            WorldSettlementFC settlement = ParentSettlement;
            if (settlement is null || map is null) return;

            if (op?.defender?.force is null)
            {
                LogUtil.Warning($"Aborting defense for {settlement.Name}, null defending force. Resetting battle state.");
                EndBattle(false, 0, null);
                return;
            }

            battleMapInitialized = true;

            map.fogGrid.ClearAllFog();

            // Remove pawns spawned by KCSG/VBGE that don't belong to Empire or the player.
            Faction empireFaction = FindFC.EmpireFaction;
            List<Pawn> toRemove = new List<Pawn>();
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (!pawn.RaceProps.Humanlike) continue;
                if (pawn.Faction == empireFaction) continue;
                if (pawn.Faction == Faction.OfPlayer) continue;
                toRemove.Add(pawn);
            }
            foreach (Pawn pawn in toRemove) pawn.Destroy();
            if (toRemove.Count > 0)
                LogUtil.Message($"Cleaned up {toRemove.Count} unrelated pawns from {settlement.Name}");

            GenerateFriendlies(op);
            RecruitMapInhabitants(op);
            Find.TickManager.Notify_GeneratedPotentiallyHostileMap();

            string enemyName = op.aggressor?.force?.homeFaction?.Name ?? op.aggressor?.faction?.Name ?? "Unknown";
            Pawn firstDefender = defenderPawns.FirstOrDefault();
            GlobalTargetInfo jumpTarget = firstDefender is object
                ? new GlobalTargetInfo(firstDefender)
                : new GlobalTargetInfo(new IntVec3(map.Size.x / 2, 0, map.Size.z / 2), map);
            Find.LetterStack.ReceiveLetter(
                "FCManualBattleStarted".Translate(settlement.Name),
                "FCManualBattleStartedDesc".Translate(settlement.Name, enemyName),
                LetterDefOf.ThreatBig,
                new LookTargets(jumpTarget));
        }

        private void GenerateFriendlies(MilitaryOperation op)
        {
            WorldSettlementFC settlement = ParentSettlement;
            if (settlement is null || map is null) return;
            MilitaryForce force = op?.defender?.force;
            if (force is null) return;

            var points = Math.Max((float)(force.forceRemaining * 100), 50f);
            List<Pawn> friendlies = null;
            var riders = new Dictionary<Pawn, Pawn>();

            // Try external defender pawns (VOE outposts, etc.)
            if (force.homeSettlement == null && op.externalDefenderSource != null)
            {
                IAutoDefender extDefender = AutoDefenderRegistry.FindByWorldObject(op.externalDefenderSource);
                friendlies = extDefender?.GetDefendingPawns();
            }

            if (friendlies == null || friendlies.Count == 0)
            {
                WorldSettlementFC homeSettlement = force.homeSettlement;
                // Use the squad recorded on the op, set at op creation by PickPrimaryDefendingSquad
                // (target's own squad) or ApplyAutoDefenderSelection (foreign auto-defender)
                MercenarySquadFC squad = op?.defender?.squad;
                bool hasSquad = squad != null
                    && squad.outfit != null
                    && squad.mercenaries.Any();

                // No stationed squad: field a militia from the player's own unit designs instead of
                // wholly-random pawns. The ephemeral squad flows through the same squadAvailable path
                // below (CheckInitialization generates and equips its designed pawns). Returns null —
                // and we keep the random-raid fallback — only when the player has designed no units.
                if (!hasSquad)
                {
                    MercenarySquadFC militia = TryBuildDesignMilitia(settlement, force);
                    if (militia != null) { squad = militia; hasSquad = true; }
                }

                bool squadDeployed = hasSquad && squad.Deployment.IsPhysicallyDeployed();
                bool squadAvailable = hasSquad && !squadDeployed;

                if (squadAvailable)
                {
                    squad.CheckInitialization();
                    squad.UpdateSquadStats(homeSettlement.settlementMilitaryLevel);
                    SquadHealthUtil.ResetNeeds(squad);

                    // SpawnableMercenaryPawns filters downed mercs out of the initial defender
                    // wave — they stay at base instead of being dropped into a fight they can't
                    // participate in.
                    friendlies = squad.SpawnableMercenaryPawns.ToList();

                    double efficiency = force.militaryEfficiency;
                    foreach (Pawn merc in friendlies)
                    {
                        MilitaryEfficiencyUtil.ShiftPawnGearQuality(merc, efficiency);
                        MilitaryEfficiencyUtil.ApplyCombatEfficiencyHediff(merc, efficiency);
                    }

                    // Only mounts are ridden (LordToil_DefendSelfAndMount mounts each rider). Companion
                    // animals (subPawnType == Animal) are NOT added here, so they spawn and fight on foot.
                    foreach (var sub in squad.AllSubPawns())
                    {
                        if (sub.subPawnType != Mercenary.SubPawnType.Mount || sub.pawn is null) continue;
                        if (sub.handler?.pawn is object)
                            riders.Add(sub.handler.pawn, sub.pawn);
                    }
                }
                else if (!hasSquad)
                {
                    var parms = new IncidentParms
                    {
                        target = map,
                        faction = FindFC.EmpireFaction,
                        generateFightersOnly = true,
                        raidStrategy = RaidStrategyDefOf.ImmediateAttackFriendly
                    };
                    parms.points = IncidentWorker_Raid.AdjustedRaidPoints(points,
                        PawnsArrivalModeDefOf.EdgeWalkIn, parms.raidStrategy,
                        parms.faction, PawnGroupKindDefOf.Combat,
                        parms.target);
                    friendlies = PawnGroupMakerUtility.GeneratePawns(
                        IncidentParmsUtility.GetDefaultPawnGroupMakerParms(
                            PawnGroupKindDefOf.Combat, parms, true)).ToList();
                    if (!friendlies.Any()) LogUtil.Error("Got no pawns spawning raid from parms " + parms);

                    double efficiency = force.militaryEfficiency;
                    foreach (Pawn defender in friendlies)
                    {
                        MilitaryEfficiencyUtil.ApplyCombatEfficiencyHediff(defender, efficiency);
                    }
                }
                // else: squad exists but is physically deployed — settlement fights with inhabitants only.
            }

            void tryFindLoc(out IntVec3 loc, Pawn friendly)
            {
                // Source the spawn zone from the already-generated map's actual size so it
                // can never drift from DefenseMapSize. A centered square (~half the map),
                // clipped on-map, preserves the original "spawn near the middle" intent.
                int mapSize = map.Size.x;
                int zone = mapSize / 2;
                if (zone < 10) zone = 10;
                if (zone > mapSize - 2) zone = mapSize - 2;
                int min = (mapSize - zone) / 2;
                CellRect rect = new CellRect(min, min, zone, zone).ClipInsideMap(map);
                CellFinder.TryFindRandomCellInsideWith(rect,
                    testing => testing.Standable(map) && map.reachability.CanReachMapEdge(testing,
                        TraverseParms.For(TraverseMode.PassDoors)), out loc);
                if (loc.x == -1000)
                {
                    LogUtil.Message("Failed with " + friendly + ", " + loc);
                    CellFinder.TryFindRandomCellNear(map.Center, map, 75,
                        testing => testing.Standable(map), out loc);
                }
            }

            if (friendlies is null) friendlies = new List<Pawn>();

            var spawnedFriendlies = new List<Pawn>();
            foreach (var friendly in friendlies)
            {
                if (friendly.IsWildMan()) continue;
                friendly.ApplyIdeologyRitualWounds();

                IntVec3 loc;
                if (friendly.AnimalOrWildMan())
                {
                    Pawn rider = null;
                    if (riders.Count > 0)
                    {
                        var pair = riders.FirstOrDefault(p => p.Value.thingIDNumber == friendly.thingIDNumber);
                        rider = pair.Key;
                    }

                    if (rider is object)
                    {
                        // A mount: spawn it next to its rider so GU2 can mount them together.
                        CellFinder.TryFindRandomCellInsideWith(new CellRect((int)rider.DrawPos.x - 5,
                                (int)rider.DrawPos.z - 5, 10, 10),
                            testing => testing.Standable(map) && map.reachability.CanReachMapEdge(testing,
                                TraverseParms.For(TraverseMode.PassDoors)), out loc);
                    }
                    else
                    {
                        // A companion animal (not a mount): spawns standalone and fights on foot.
                        tryFindLoc(out loc, friendly);
                    }
                }
                else
                {
                    tryFindLoc(out loc, friendly);
                }

                try
                {
                    GenSpawn.Spawn(friendly, loc, map, new Rot4());
                    friendly.drafter = new Pawn_DraftController(friendly);
                    map.mapPawns.RegisterPawn(friendly);
                    // Load reloadable-weapon magazines from carried ammo (e.g. Yayo's Combat); no-op otherwise.
                    ReloadableWeaponUtil.LoadMagazinesFromInventory(friendly);
                    spawnedFriendlies.Add(friendly);
                }
                catch (Exception e)
                {
                    LogUtil.Warning($"Failed to spawn defender {friendly.LabelShort} (likely a mod conflict): {e}");
                }
            }

            LordMaker.MakeNewLord(FindFC.EmpireFaction, new LordJob_DefendColony(settlement, riders), map, spawnedFriendlies);

            // Per-op pawn tracking: every defender belongs to the op that summoned it. EndAttack
            // uses op.defender.pawns to return external auto-defender pawns; the BattlefieldContext
            // flat accessors aggregate across ops; overwhelming-victory detection compares
            // op.defender.initialPawnCount vs op.defender.pawns.Count at battle end.
            op.defender.pawns.AddRange(spawnedFriendlies);
            op.defender.initialPawnCount += spawnedFriendlies.Count;
        }

        /// <summary>Builds a transient militia squad from the player's individual unit designs to
        /// defend a settlement that has no stationed squad. Draws designed units at random (with
        /// replacement) from those that fit within the budget implied by the defending
        /// <paramref name="force"/>'s military level, until their combined loadout cost meets that
        /// budget, hard-capped at <see cref="MilSquadFC.MaxSquadSize"/>. The returned squad is never
        /// registered in <see cref="MilitaryFC.mercenarySquads"/> — its pawns are disposable and are
        /// torn down with the battle map, like the random-raid defenders it replaces. Returns null
        /// when the player has designed no unit that fits the budget, so the caller falls back to
        /// random generation rather than forcing an unaffordable design through.</summary>
        private MercenarySquadFC TryBuildDesignMilitia(WorldSettlementFC settlement, MilitaryForce force)
        {
            MilitaryFC mil = FindFC.Military;
            if (mil?.units is null) return null;

            double budget = SquadPowerRegistry.CostFromLevel(force?.militaryLevel ?? 1.0);

            // Only designs the settlement can actually afford are eligible
            List<MilUnitFC> pool = mil.units
                .Where(u => u is object && !u.isBlank && u.getTotalCost <= budget)
                .ToList();
            if (pool.Count == 0) return null;

            int cap = MilSquadFC.MaxSquadSize;

            // Draw affordable designs (with replacement) until the loadout budget is met, capped at MaxSquadSize
            MilSquadFC outfit = new MilSquadFC(false);   // not registered in Military.squads; transient
            double spent = 0;
            int count = 0;
            while (count < cap && (count == 0 || spent < budget))
            {
                MilUnitFC pick = pool.RandomElement();
                outfit.AddUnit(pick);
                spent += Math.Max(1.0, pick.getTotalCost);
                count++;
            }

            MercenarySquadFC militia = new MercenarySquadFC();
            militia.settlement = settlement;   // faction colors + per-pawn settlement back-ref
            militia.outfit = outfit;
            // Generate + equip the pawns now. CheckInitialization (called later in the reused squad
            // path) only fires when mercenaries is null, but a fresh squad field-inits it to an empty
            // non-null list — so without this explicit call no pawns are ever created and zero
            // defenders spawn. Real squads hit InitiateSquad via MilitaryFC.HireSquad at hire time.
            militia.InitiateSquad();
            return militia;
        }

        private void RecruitMapInhabitants(MilitaryOperation op)
        {
            if (map == null || !defenderPawns.Any()) return;
            WorldSettlementFC settlement = ParentSettlement;
            if (settlement is null) return;

            Lord defenseLord = defenderPawns.FirstOrDefault()?.GetLord();
            if (defenseLord == null) return;

            Faction empireFaction = FindFC.EmpireFaction;
            var defenderSet = new HashSet<Pawn>(defenderPawns);
            var inhabitants = new List<Pawn>();

            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (defenderSet.Contains(pawn)) continue;
                if (!pawn.RaceProps.Humanlike) continue;
                if (pawn.Downed || pawn.Dead) continue;
                if (pawn.Faction != empireFaction) continue;
                if (pawn.IsPrisonerOfColony) continue;
                inhabitants.Add(pawn);
            }

            int targetCount = (int)settlement.workers;

            while (inhabitants.Count > targetCount)
            {
                Pawn excess = inhabitants[inhabitants.Count - 1];
                inhabitants.RemoveAt(inhabitants.Count - 1);
                if (excess.Spawned) excess.Destroy();
            }

            int spawnAttempts = 0;
            while (inhabitants.Count < targetCount && spawnAttempts < targetCount * 2)
            {
                spawnAttempts++;
                try
                {
                    Pawn civilian = PawnGenerator.GeneratePawn(FCPawnGenerator.CivilianRequest());
                    IntVec3 loc;
                    if (!CellFinder.TryFindRandomCellNear(map.Center, map, 15, c => c.Standable(map), out loc))
                        loc = map.Center;
                    GenSpawn.Spawn(civilian, loc, map);
                    inhabitants.Add(civilian);
                }
                catch (Exception e)
                {
                    LogUtil.Warning($"Failed to generate/spawn civilian (likely a mod conflict): {e}");
                }
            }

            for (int i = 0; i < inhabitants.Count; i++)
            {
                if (i % 8 != 0)
                    inhabitants[i].equipment.DestroyAllEquipment();
            }

            foreach (Pawn inhabitant in inhabitants)
            {
                Lord existingLord = inhabitant.GetLord();
                if (existingLord != null)
                    existingLord.Notify_PawnLost(inhabitant, PawnLostCondition.LeftVoluntarily);

                defenseLord.AddPawn(inhabitant);
                civilianPawns.Add(inhabitant);
                if (op?.defender?.pawns is object)
                {
                    op.defender.pawns.Add(inhabitant);
                    op.defender.initialPawnCount++;
                }
            }

            if (inhabitants.Count > 0)
            {
                LogUtil.Message($"Added {inhabitants.Count} settlement inhabitants to defenders at {settlement.Name}");
            }
        }

        /* -*-*-*-*- Battle resolution -*-*-*-*- */

        public void EndBattle(bool won, int remaining, BattleResult battleResult = null)
        {
            // Settlement-side effects (happiness/loyalty/buildings) and op.CompleteBattle
            // dispatch are still owned by comp.EndBattle since they're settlement-specific.
            // BattlefieldContext just clears its own state after the comp finishes.
            WorldObjectComp_SettlementMilitary comp = ParentMilitaryComp;
            if (comp is object)
            {
                comp.EndBattle(won, remaining, battleResult);
            }
            battleMapInitialized = false;
        }

        public void EndAttack()
        {
            // Snapshot defenderPawns (full list — the cleanup loops below intentionally include
            // downed-but-alive survivors when returning external defenders and stripping hediffs).
            // The win/remaining calc, however, counts only STANDING defenders so a battle that ended
            // because every defender was downed reports a loss, not a victory.
            List<Pawn> defendersSnapshot = defenderPawns.ToList();
            int remaining = standingDefenderPawns.Count();
            bool attackersGone = !standingAttackerPawns.Any() && !HasPendingPodAttackers();
            bool won = remaining > 0 || attackersGone;

            // Return external defender pawns per op before map cleanup destroys them.
            if (activeOps is object)
            {
                foreach (MilitaryOperation op in activeOps)
                {
                    if (op?.externalDefenderSource is null) continue;
                    IAutoDefender extDefender = AutoDefenderRegistry.FindByWorldObject(op.externalDefenderSource);
                    if (extDefender is null) continue;

                    List<Pawn> survivingPawns = new List<Pawn>();
                    foreach (Pawn pawn in op.defender.pawns)
                    {
                        if (pawn != null && !pawn.Dead && !pawn.Destroyed)
                        {
                            if (pawn.Spawned) pawn.DeSpawn();
                            survivingPawns.Add(pawn);
                        }
                    }
                    extDefender.ReturnDefendingPawns(survivingPawns);
                }
            }

            // Strip combat efficiency hediffs from surviving defenders
            foreach (Pawn defender in defendersSnapshot)
            {
                if (defender != null && !defender.Dead && !defender.Destroyed)
                    MilitaryEfficiencyUtil.RemoveCombatEfficiencyHediff(defender);
            }

            DeleteMap(won);
            EndBattle(won, remaining);

            ClearAllOpPawns();
            endingBattle = false;
            pendingDeliveryMessage = null;
        }

        public void RemoveAttacker(Pawn downed)
        {
            RemoveAttackerPawn(downed);
            PruneStalePawns();

            bool anyUnderAttack = FindFC.MilitaryManager?.HasDefenseAt(ParentSettlement) ?? false;
            if (standingAttackerPawns.Any() || HasPendingPodAttackers() || endingBattle || !anyUnderAttack) return;

            endingBattle = true;
            LongEventHandler.QueueLongEvent(EndAttack, "EndingAttack", false, error =>
            {
                DelayedErrorWindowRequest.Add("FCErrorEndingAttack".Translate(),
                    "FCErrorEndingAttackDescription".Translate());
                LogUtil.Error(error.Message);
            });
        }

        public void RemoveDefender(Pawn defender)
        {
            RemoveDefenderPawn(defender);
            PruneStalePawns();

            bool anyUnderAttack = FindFC.MilitaryManager?.HasDefenseAt(ParentSettlement) ?? false;
            if (standingDefenderPawns.Any() || endingBattle || !anyUnderAttack) return;

            endingBattle = true;
            LongEventHandler.QueueLongEvent(EndAttack, "EndingAttack", false, error =>
            {
                DelayedErrorWindowRequest.Add("FCErrorEndingAttack".Translate(),
                    "FCErrorEndingAttackDescription".Translate());
                LogUtil.Error(error.Message);
            });
        }

        public void DeleteMap(bool won = true)
        {
            if (map is null) return;
            WorldSettlementFC settlement = ParentSettlement;
            if (settlement is null) return;

            // Restore faction on Empire defenders the player drafted during battle.
            Faction empireFaction = FindFC.EmpireFaction;
            foreach (Pawn npc in draftedNPCs)
            {
                if (npc is null || npc.Dead || npc.Destroyed) continue;
                if (npc.Faction == Faction.OfPlayer)
                    npc.SetFaction(empireFaction);
            }
            draftedNPCs.Clear();

            // Check for player units on the map. Use AllPawnsSpawned + faction filter
            // instead of FreeColonistsSpawned so VehiclePawns (and player animals) count.
            // Pawns aboard a VehiclePawn are not directly spawned, but the vehicle itself
            // is a player-faction Pawn — detecting the vehicle is enough to keep the map
            // alive so the player can drive off and reform the caravan.
            List<Pawn> playerPawns = new List<Pawn>();
            bool anyMobile = false;
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.Faction != Faction.OfPlayer) continue;
                playerPawns.Add(pawn);
                if (!pawn.Downed) anyMobile = true;
            }

            if (anyMobile || shuttleLandingPending)
            {
                // Mobile player pawns (or shuttles) exist — keep the map alive.
                foreach (var lord in map.lordManager.lords.ListFullCopy())
                    map.lordManager.RemoveLord(lord);

                List<Pawn> empireDefenders = new List<Pawn>();
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                    if (pawn.Faction == empireFaction && !pawn.Dead && !pawn.Downed)
                        empireDefenders.Add(pawn);

                foreach (Pawn pawn in empireDefenders)
                    pawn.jobs.StopAll();

                if (empireDefenders.Any())
                    LordMaker.MakeNewLord(empireFaction, new LordJob_ColonistsIdle(settlement), map, empireDefenders);

                return;
            }

            // Immediate path — remove all lords before cleanup
            foreach (var lord in map.lordManager.lords.ListFullCopy())
                map.lordManager.RemoveLord(lord);

            if (playerPawns.Count > 0)
            {
                foreach (Pawn pawn in playerPawns)
                    if (pawn.Spawned) pawn.DeSpawn();

                foreach (Pawn pawn in playerPawns)
                    if (!pawn.Dead)
                    {
                        int iterations = 0;
                        while (pawn.health.HasHediffsNeedingTend())
                        {
                            iterations++;
                            if (iterations > 10000)
                            {
                                LogUtil.Error("BattlefieldContext.DeleteMap: Too many tend iterations.");
                                break;
                            }
                            TendUtility.DoTend(null, pawn, null);
                        }
                    }

                string eventText = won
                    ? DeliveryNotification.ShuttleEventInjuredString
                    : DeliveryNotification.ShuttleEventInjuredLostString;
                int travelTicks = TravelUtil.ReturnTicksToArrive(settlement.Tile, Find.AnyPlayerHomeMap.Tile);
                if (!won) travelTicks += GenDate.TicksPerDay;

                var goods = new List<Thing>(playerPawns.Count);
                foreach (Pawn pawn in playerPawns) goods.Add(pawn);

                var eventParams = new FCEvent
                {
                    location = Find.AnyPlayerHomeMap.Tile,
                    source = settlement.Tile,
                    goods = goods,
                    customDescription = eventText,
                    timeTillTrigger = Find.TickManager.TicksGame + travelTicks
                };
                DeliveryEvent.CreateDeliveryEvent(eventParams);
                string travelDays = ((float)travelTicks / GenDate.TicksPerDay).ToString("0.#");
                pendingDeliveryMessage = "FCInjuredCaravanMembersReturning".Translate(playerPawns.Count, travelDays);
            }

            CameraJumper.TryJump(settlement.Tile);
            Current.Game.CurrentMap = Find.AnyPlayerHomeMap;
            Current.Game.DeinitAndRemoveMap(map, false);
            map = null;
        }

        /* -*-*-*-*- Caravan defend / external pawn injection -*-*-*-*- */

        public void AddToDefenceFromList(List<Pawn> pawns, int destinationTile)
        {
            AddToDefenceFromList(pawns, destinationTile, assignToLord: true);
        }

        /// <summary>Registers pawns with the defense system. When <paramref name="assignToLord"/>
        /// is false, pawns are added to defenders but not to the battle lord (used for VEF
        /// vehicles that have their own job systems).</summary>
        public void AddToDefenceFromList(List<Pawn> pawns, int destinationTile, bool assignToLord)
        {
            if (pawns.NullOrEmpty())
            {
                LogUtil.Error("Tried to add an empty list of pawns to a battlefield");
                return;
            }

            // If battle is already in progress, register directly.
            bool isUnderAttack = FindFC.MilitaryManager?.HasDefenseAt(ParentSettlement) ?? false;
            if (isUnderAttack && map != null)
            {
                RegisterPawnsAsDefenders(pawns, assignToLord);
                return;
            }

            // Battle hasn't started yet — start it via the op linked to the warning event.
            FCEvent warning = MilitaryOperationsUtil.ReturnMilitaryEventByLocation(new PlanetTile(destinationTile));
            if (warning is null)
            {
                LogUtil.Warning("AddToDefenceFromList: no settlementBeingAttacked event at destination tile.");
                return;
            }

            MilitaryOperation op = warning.linkedOperation;
            if (op is null)
            {
                LogUtil.Warning($"AddToDefenceFromList: warning event at tile {destinationTile} has no linked op.");
                return;
            }

            StartDefense(op, () => RegisterPawnsAsDefenders(pawns, assignToLord));
        }

        public void RegisterPawnsAsDefenders(List<Pawn> pawns, bool assignToLord)
        {
            WorldSettlementFC settlement = ParentSettlement;
            HashSet<Pawn> defenderSet = new HashSet<Pawn>(defenderPawns);

            if (assignToLord)
            {
                Lord existingLord = defenderPawns.FirstOrDefault()?.GetLord();
                if (existingLord != null)
                {
                    foreach (var pawn in pawns)
                    {
                        if (!defenderSet.Contains(pawn) && !existingLord.ownedPawns.Contains(pawn))
                            existingLord.AddPawn(pawn);
                    }
                }
                else if (map != null && settlement is object)
                {
                    var lordless = new List<Pawn>();
                    foreach (var pawn in pawns)
                    {
                        if (!defenderSet.Contains(pawn) && pawn.GetLord() is null)
                            lordless.Add(pawn);
                    }
                    if (lordless.Any())
                        LordMaker.MakeNewLord(FindFC.EmpireFaction,
                            new LordJob_ColonistsIdle(settlement), map, lordless);
                }
            }

            // Attribute new defender pawns to the primary defensive op at this tile (the one that
            // started the battle). Caravan defends and other "loose" defender additions don't have
            // a natural per-op owner; the first defensive op is the canonical bench.
            MilitaryOperation primaryDef = FirstDefensiveOp();
            if (primaryDef?.defender?.pawns is null) return;

            foreach (var pawn in pawns)
            {
                if (!defenderSet.Contains(pawn))
                {
                    primaryDef.defender.pawns.Add(pawn);
                    primaryDef.defender.initialPawnCount++;
                }
            }
        }

        /// <summary>The first defensive op attached to this battlefield, or null if none. Used to
        /// attribute "loose" defender pawns (caravan defends, untracked map pawns) to a canonical
        /// op so per-op pawn lists stay aligned with the flat <see cref="defenderPawns"/>.</summary>
        public MilitaryOperation FirstDefensiveOp()
        {
            if (activeOps is null) return null;
            for (int i = 0; i < activeOps.Count; i++)
            {
                MilitaryOperation op = activeOps[i];
                if (op is object && op.IsDefensive) return op;
            }
            return null;
        }

        public void CaravanDefend(Caravan caravan)
        {
            var pawns = caravan.pawns.InnerListForReading.ListFullCopy();

            // Check for shuttle in caravan (Odyssey DLC passenger shuttle)
            var shuttle = caravan.Shuttle;
            if (shuttle != null && map != null)
            {
                ShuttleCaravanDefend(caravan, pawns, shuttle);
                return;
            }

            // Standard flow — spawn pawns at map edge
            RegisterPawnsAsDefenders(pawns, assignToLord: true);
            if (!caravan.Destroyed) caravan.Destroy();
            SpawnPawnsAtEdge(pawns);
        }

        private void ShuttleCaravanDefend(Caravan caravan, List<Pawn> pawns, Building_PassengerShuttle shuttle)
        {
            Pawn owner = CaravanInventoryUtility.GetOwnerOf(caravan, shuttle);
            owner?.inventory.innerContainer.Remove(shuttle);

            RegisterPawnsAsDefenders(pawns, assignToLord: false);
            if (!caravan.Destroyed) caravan.Destroy();

            CompShuttle compShuttle = shuttle.TryGetComp<CompShuttle>();
            TransportShipDef shipDef = compShuttle?.Props?.shipDef ?? TransportShipDefOf.Ship_Shuttle;
            TransportShip transportShip = TransportShipMaker.MakeTransportShip(shipDef, pawns, shuttle);

            shuttleLandingPending = true;

            var battleMap = map;
            var settlement = ParentSettlement;
            var shuttleDef = shuttle.def;
            Rot4 shuttleRotation = shuttleDef.defaultPlacingRot;
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                Current.Game.CurrentMap = battleMap;
                CameraJumper.TryJump(new IntVec3(battleMap.Size.x / 2, 0, battleMap.Size.z / 2), battleMap);

                var targetParams = new TargetingParameters
                {
                    canTargetLocations = true,
                    canTargetSelf = false,
                    canTargetPawns = false,
                    canTargetFires = false,
                    canTargetBuildings = false,
                    canTargetItems = false
                };

                bool landed = false;
                Find.Targeter.BeginTargeting(targetParams,
                    delegate (LocalTargetInfo target)
                    {
                        landed = true;
                        shuttleLandingPending = false;
                        shuttle.Rotation = shuttleRotation;
                        transportShip.ArriveAt(target.Cell, settlement);
                        transportShip.AddJobs(ShipJobDefOf.Unload, ShipJobDefOf.WaitForever);
                    },
                    delegate (LocalTargetInfo target)
                    {
                        RoyalTitlePermitWorker_CallShuttle.DrawShuttleGhost(target, battleMap, shuttleDef, shuttleRotation);
                    },
                    delegate (LocalTargetInfo target)
                    {
                        return RoyalTitlePermitWorker_CallShuttle.ShuttleCanLandHere(target, battleMap, shuttleDef, shuttleRotation);
                    },
                    null,
                    delegate
                    {
                        if (landed) return;
                        shuttleLandingPending = false;
                        if (!Find.Maps.Contains(battleMap)) return;
                        IntVec3 fallback = DropCellFinder.GetBestShuttleLandingSpot(battleMap, Faction.OfPlayer);
                        transportShip.ArriveAt(fallback, settlement);
                        transportShip.AddJobs(ShipJobDefOf.Unload, ShipJobDefOf.WaitForever);
                    },
                    null, true, null,
                    delegate (LocalTargetInfo target)
                    {
                        if (!shuttleDef.rotatable) return;
                        if (KeyBindingDefOf.Designator_RotateRight.KeyDownEvent)
                            shuttleRotation = shuttleRotation.Rotated(RotationDirection.Clockwise);
                        if (KeyBindingDefOf.Designator_RotateLeft.KeyDownEvent)
                            shuttleRotation = shuttleRotation.Rotated(RotationDirection.Counterclockwise);
                    });
            });
        }

        /// <summary>Picks a valid edge spawn cell biased AWAY from currently-spawned enemy
        /// attackers, so a defending caravan doesn't walk straight into the raiders. Samples
        /// candidate edge cells (same validity rules as <see cref="FindNearEdgeCell"/>) and keeps
        /// the one whose nearest enemy is farthest away (max-min distance). Falls back to plain
        /// <see cref="FindNearEdgeCell"/> when there are no spawned enemies (e.g. all attackers
        /// still in drop pods) or no valid candidate is sampled.</summary>
        private IntVec3 FindEdgeCellAwayFromEnemies(Map map)
        {
            // Only spawned pawns have a meaningful Position; pod-bound attackers (!Spawned,
            // ParentHolder is object) have no on-map position yet, so exclude them. If every
            // attacker is still in a pod, there's nothing to avoid — fall back.
            var enemyCells = new List<IntVec3>();
            foreach (var enemy in attackerPawns)
            {
                if (enemy is object && enemy.Spawned)
                    enemyCells.Add(enemy.Position);
            }
            if (enemyCells.Count == 0)
                return FindNearEdgeCell(map);

            const int SampleCount = 40;
            var hostFaction = map.ParentFaction;

            IntVec3 best = IntVec3.Invalid;
            int bestScore = int.MinValue; // score = squared distance to the NEAREST enemy
            for (int i = 0; i < SampleCount; i++)
            {
                IntVec3 candidate;
                if (!CellFinder.TryFindRandomEdgeCellWith(x => IsValidEdgeSpawnCell(x, map, hostFaction),
                        map, CellFinder.EdgeRoadChance_Neutral, out candidate))
                    continue;

                int nearest = int.MaxValue;
                for (int e = 0; e < enemyCells.Count; e++)
                {
                    int d = candidate.DistanceToSquared(enemyCells[e]);
                    if (d < nearest) nearest = d;
                }

                if (nearest > bestScore)
                {
                    bestScore = nearest;
                    best = candidate;
                }
            }

            if (!best.IsValid)
                return FindNearEdgeCell(map);

            return CellFinder.RandomClosewalkCellNear(best, map, 5);
        }

        private void SpawnPawnsAtEdge(List<Pawn> pawns)
        {
            if (map is null) return;
            var enterCell = FindEdgeCellAwayFromEnemies(map);
            foreach (var pawn in pawns)
            {
                var loc = CellFinder.RandomSpawnCellForPawnNear(enterCell, map);
                GenSpawn.Spawn(pawn, loc, map, Rot4.Random);
            }
        }

        /* -*-*-*-*- Helpers -*-*-*-*- */

        private bool IsPlayerCaravanOnTile()
        {
            return Find.WorldObjects.Caravans.Any(c =>
                c.Tile == tile && c.Faction == Faction.OfPlayer);
        }
    }
}
