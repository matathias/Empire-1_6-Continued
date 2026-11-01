using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

// Migration utility — sole legitimate consumer of [Obsolete] legacy military state on
// WorldObjectComp_SettlementMilitary, FCEvent, and DefenseWave. Drained on PostLoadInit.
#pragma warning disable 0618

namespace FactionColonies
{
    /// <summary>
    /// One-shot migration: drains pre-refactor military state into the new
    /// <see cref="MilitaryOperationManager"/> model.
    /// <para>Pre-refactor saves carried military operation state on three locations:</para>
    /// <list type="bullet">
    ///  <item><description><see cref="WorldObjectComp_SettlementMilitary"/>: <c>militaryJob</c>,
    ///   <c>militaryLocation</c>, <c>militaryEnemy</c>, <c>militaryBusy</c>,
    ///   <c>isUnderAttack</c>, <c>activeWaves</c>.</description></item>
    ///  <item><description><see cref="FCEvent"/>: <c>militaryForceAttacking</c>, <c>militaryForceDefending</c>,
    ///   <c>settlementFCDefending</c>, <c>externalDefenderSource</c>.</description></item>
    ///  <item><description><see cref="MercenarySquadFC"/>: <c>isDeployed</c>, <c>timeDeployed</c>.</description></item>
    /// </list>
    /// <para>The migration creates <see cref="MilitaryOperation"/>s in the manager and links
    /// pending FCEvents to them via <see cref="FCEvent.linkedOperation"/> so the next time
    /// those events fire they dispatch through the op flow. Legacy fields stay populated for
    /// the round-trip but are never read by runtime code.</para>
    /// </summary>
    public static class MilitaryMigrationUtil
    {
        /// <summary>
        /// Squad-first refactor migration: pre-refactor saves carried <c>militarySquad</c> and
        /// <c>autoDefend</c> on the comp itself. The values are loaded into <c>_legacyMilitarySquad</c>
        /// and <c>_legacyAutoDefend</c> buffers in <c>PostExposeData</c>'s LoadingVars branch.
        /// This method drains them onto the squads (settlement billet + per-squad autoDefend flag)
        /// during PostLoadInit. Idempotent across reloads because the legacy fields are not
        /// re-serialized after migration.
        /// </summary>
        public static void MigrateLegacyComp_MilitarySquad(FactionFC faction)
        {
            if (faction is null || faction.settlements is null) return;
            int migrated = 0;
            foreach (WorldSettlementFC settlement in faction.settlements)
            {
                var comp = settlement?.MilitaryComp;
                if (comp is null) continue;
                MercenarySquadFC legacy = comp._legacyMilitarySquad;
                if (legacy is null) continue;
                if (legacy.settlement is null) legacy.settlement = settlement;
                if (comp._legacyAutoDefend && !legacy.autoDefend) legacy.autoDefend = true;
                comp._legacyMilitarySquad = null;
                comp._legacyAutoDefend = false;
                migrated++;
            }
            if (migrated > 0)
            {
                LogUtil.MessageForce($"MilitaryMigrationUtil.MigrateLegacyComp_MilitarySquad: bound {migrated} legacy squad(s) to their settlements.");
            }
        }

        /// <summary>
        /// Returns true if any pre-refactor military state was loaded that has not yet been
        /// drained into the manager. Cheap detection: checks for any settlement with active op
        /// flags or pending military events.
        /// </summary>
        public static bool AnyLegacyStatePresent(FactionFC faction)
        {
            if (faction is null) return false;
            if (faction.settlements is null) return false;

            foreach (WorldSettlementFC settlement in faction.settlements)
            {
                if (settlement is null) continue;
                var comp = settlement.MilitaryComp;
                if (comp is null) continue;
                if (comp._legacyMilitaryBusy) return true;
                if (comp._legacyIsUnderAttack) return true;
                if (comp._legacyActiveWaves is object && comp._legacyActiveWaves.Count > 0) return true;
                if (comp._legacyAttackers is object && comp._legacyAttackers.Count > 0) return true;
                if (comp._legacyDefenders is object && comp._legacyDefenders.Count > 0) return true;
            }

            // Also check for any pending military events. If a pre-refactor save has events but
            // no comp flags (rare, but possible on a corrupted save), we still want to link them.
            if (faction.eventManager is object)
            {
                foreach (FCEvent evt in faction.Events)
                {
                    if (evt is null) continue;
                    if (evt.HasLinkedOperation) continue; // already linked
                    if (evt.def == FCEventDefOf.raidEnemySettlement
                        || evt.def == FCEventDefOf.captureEnemySettlement
                        || evt.def == FCEventDefOf.enslaveEnemySettlement
                        || evt.def == FCEventDefOf.cooldownMilitary
                        || evt.def == FCEventDefOf.settlementBeingAttacked) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Walks loaded legacy state and reconstructs <see cref="MilitaryOperation"/>s in the
        /// manager. Safe across save/reload because the legacy fields on the comp are loaded
        /// only in <see cref="LoadSaveMode.LoadingVars"/> and never re-serialized — once
        /// drained they don't survive the next save, so <see cref="AnyLegacyStatePresent"/>
        /// returns false on subsequent loads. Not idempotent within a single load cycle: the
        /// reconstruct functions don't check the manager for existing ops covering the same
        /// legacy event, so calling this twice in one cycle would create duplicates. The sole
        /// call site (<c>FactionFC.PostLoadInit</c>) only invokes once per load.
        /// </summary>
        public static void Migrate(FactionFC faction)
        {
            if (faction is null) return;
            MilitaryOperationManager manager = faction.militaryOperationManager;
            if (manager is null) return;

            // Squad-first refactor: drain comp._legacyMilitarySquad / _legacyAutoDefend onto the
            // squads themselves before any other migration runs (downstream calls read squad.settlement).
            MigrateLegacyComp_MilitarySquad(faction);

            int migratedCount = 0;

            foreach (WorldSettlementFC settlement in faction.settlements ?? new List<WorldSettlementFC>())
            {
                if (settlement is null) continue;
                var comp = settlement.MilitaryComp;
                if (comp is null) continue;

                // Offensive op in flight (Traveling phase): comp has militaryJob set to a
                // handler-driven job (Raid / Capture / Enslave) and a pending arrival event.
                if (comp._legacyMilitaryBusy && comp._legacyMilitaryJob is object && comp._legacyMilitaryJob.Handler is object)
                {
                    FCEvent arrival = FindPendingArrivalEvent(faction, settlement.Tile, comp._legacyMilitaryJob);
                    if (arrival is object)
                    {
                        MilitaryOperation op = ReconstructOffensiveOp(manager, settlement, comp, arrival);
                        if (op is object)
                        {
                            arrival.linkedOperation = op;
                            op.sourceEvents.Add(arrival);
                            migratedCount++;
                        }
                    }
                }
                // Offensive op in cooldown: comp._legacyMilitaryJob = Cooldown + cooldownMilitary event.
                else if (comp._legacyMilitaryBusy && comp._legacyMilitaryJob == MilitaryJobDefOf.Cooldown)
                {
                    FCEvent cooldown = faction.FindEventByDefAndLocation(FCEventDefOf.cooldownMilitary, settlement.Tile);
                    if (cooldown is object)
                    {
                        MilitaryOperation op = ReconstructCooldownOp(manager, settlement, comp, cooldown);
                        if (op is object)
                        {
                            cooldown.linkedOperation = op;
                            op.sourceEvents.Add(cooldown);
                            migratedCount++;
                        }
                    }
                }
                // Deploy op: handler-less, no FCEvent. Squad is physically on a player map.
                // Older saves used the squad's now-removed isDeployed flag for this; we now
                // infer the deploy state from comp._legacyMilitaryJob == Deploy.
                else if (comp._legacyMilitaryBusy && comp._legacyMilitaryJob == MilitaryJobDefOf.Deploy)
                {
                    MilitaryOperation op = ReconstructDeployOp(manager, settlement, comp);
                    if (op is object) migratedCount++;
                }

                // Defensive warning pending: comp._legacyIsUnderAttack + settlementBeingAttacked event.
                if (comp._legacyIsUnderAttack)
                {
                    FCEvent warning = faction.FindEventByDefAndLocation(FCEventDefOf.settlementBeingAttacked, settlement.Tile);
                    if (warning is object && warning.linkedOperation is null)
                    {
                        MilitaryOperation op = ReconstructDefensiveOp(manager, settlement, comp, warning);
                        if (op is object)
                        {
                            warning.linkedOperation = op;
                            op.sourceEvents.Add(warning);
                            migratedCount++;
                        }
                    }
                    else if (comp._legacyActiveWaves is object && comp._legacyActiveWaves.Count > 0)
                    {
                        // Mid-battle save: warning event already consumed, battle in progress
                        // on the comp. Reconstruct an op so EndBattle can fire CompleteBattle on it.
                        MilitaryOperation op = ReconstructEngagedDefensiveOp(manager, settlement, comp);
                        if (op is object) migratedCount++;
                    }
                }

                // Drain the comp's legacy battle pawn buffers into the BattlefieldContext for this
                // settlement's tile. The flat attacker/defender pawn lists are now aggregations
                // across the per-op pawn lists, so legacy pawns route onto the primary defensive
                // op's aggressor/defender lists (multi-wave saves lose per-wave grouping; only the
                // aggregated total survives).
                if (HasLegacyBattleState(comp))
                {
                    BattlefieldContext bf = manager.GetOrCreateBattlefield(settlement.Tile);
                    if (bf is object)
                    {
                        if (comp._legacyDraftedNPCs is object) bf.draftedNPCs.AddRange(comp._legacyDraftedNPCs);
                        bf.battleMapInitialized = comp._legacyBattleMapInitialized;

                        MilitaryOperation defensiveOp = bf.FirstDefensiveOp();
                        if (defensiveOp is object)
                        {
                            // Legacy flat lists fold into the primary op's per-side pawn lists.
                            if (comp._legacyAttackers is object)
                            {
                                defensiveOp.aggressor.pawns.AddRange(comp._legacyAttackers);
                                defensiveOp.aggressor.initialPawnCount += comp._legacyAttackers.Count;
                            }
                            if (comp._legacyDefenders is object)
                            {
                                defensiveOp.defender.pawns.AddRange(comp._legacyDefenders);
                            }
                            // initialDefenderCount goes onto the primary op's defender side
                            // (matches the comp.EndBattle path that used the flat field for
                            // overwhelming-victory detection).
                            if (comp._legacyInitialDefenderCount > 0)
                            {
                                defensiveOp.defender.initialPawnCount += comp._legacyInitialDefenderCount;
                            }

                            // EndAttack walks ops to return external defender pawns; without this,
                            // an outpost-defended migrated battle would fail to return its survivors.
                            if (comp._legacyActiveWaves is object)
                            {
                                foreach (DefenseWave wave in comp._legacyActiveWaves)
                                {
                                    if (wave is null) continue;
                                    if (wave.waveAttackers is object) defensiveOp.aggressor.pawns.AddRange(wave.waveAttackers);
                                    if (wave.waveDefenders is object) defensiveOp.defender.pawns.AddRange(wave.waveDefenders);
                                }
                            }
                        }
                    }
                }
            }

            manager.RebuildIndices();

            if (migratedCount > 0)
            {
                LogUtil.MessageForce($"MilitaryMigrationUtil: drained {migratedCount} legacy military operation(s) into the manager.");
            }
        }

        private static FCEvent FindPendingArrivalEvent(FactionFC faction, PlanetTile tile, MilitaryJobDef job)
        {
            if (faction is null || job is null) return null;
            FCEventDef arrivalDef = ArrivalDefForJob(job);
            if (arrivalDef is null) return null;
            return faction.FindEventByDefAndLocation(arrivalDef, tile);
        }

        private static FCEventDef ArrivalDefForJob(MilitaryJobDef job)
        {
            if (job == MilitaryJobDefOf.RaidEnemySettlement) return FCEventDefOf.raidEnemySettlement;
            if (job == MilitaryJobDefOf.CaptureEnemySettlement) return FCEventDefOf.captureEnemySettlement;
            if (job == MilitaryJobDefOf.EnslaveEnemySettlement) return FCEventDefOf.enslaveEnemySettlement;
            return null;
        }

        /* Reconstruct* helpers are called from Migrate(), which runs in PostLoadInit AFTER
         * MigrateLegacyComp_MilitarySquad has bound legacy squads to their home settlement
         * via squad.settlement. By the time these methods read home.PrimaryStationedSquad,
         * exactly one stationed squad exists per settlement (the migrated legacy one). */
        private static MilitaryOperation ReconstructOffensiveOp(MilitaryOperationManager manager,
            WorldSettlementFC home, WorldObjectComp_SettlementMilitary comp, FCEvent arrival)
        {
            // Resolve target world object from the comp's recorded location.
            WorldObject target = Find.WorldObjects.WorldObjectAt<WorldSettlementFC>(comp._legacyMilitaryLocation);
            if (target is null) target = Find.WorldObjects.SettlementAt(comp._legacyMilitaryLocation);
            if (target is null) return null;

            int newId = manager.nextOperationId++;
            var op = new MilitaryOperation(newId, comp._legacyMilitaryJob, comp._legacyMilitaryLocation, target);
            op.phase = MilitaryOperationPhase.Traveling;
            op.nextPhaseTick = arrival.timeTillTrigger;
            op.aggressor.faction = FindFC.EmpireFaction;
            op.aggressor.homeSettlement = home;
            op.aggressor.squad = home.PrimaryStationedSquad;
            op.aggressor.force = MilitaryForce.CreateMilitaryForceFromSettlement(home, isAttacking: true);
            op.defender.faction = comp._legacyMilitaryEnemy;
            manager.Register(op);
            return op;
        }

        private static MilitaryOperation ReconstructCooldownOp(MilitaryOperationManager manager,
            WorldSettlementFC home, WorldObjectComp_SettlementMilitary comp, FCEvent cooldown)
        {
            int newId = manager.nextOperationId++;
            var op = new MilitaryOperation(newId, MilitaryJobDefOf.Cooldown, home.Tile, home);
            op.phase = MilitaryOperationPhase.CooldownPending;
            op.nextPhaseTick = cooldown.timeTillTrigger;
            op.aggressor.faction = FindFC.EmpireFaction;
            op.aggressor.homeSettlement = home;
            op.aggressor.squad = home.PrimaryStationedSquad;
            manager.Register(op);
            return op;
        }

        private static MilitaryOperation ReconstructDeployOp(MilitaryOperationManager manager,
            WorldSettlementFC home, WorldObjectComp_SettlementMilitary comp)
        {
            int newId = manager.nextOperationId++;
            // Old saves stored comp._legacyMilitaryLocation as currentMap.Index (a buggy int cast that
            // didn't match any real tile). Use the home settlement's tile instead — the squad's
            // physical map presence is tracked by the spawned pawns, not by the op's targetTile.
            var op = new MilitaryOperation(newId, MilitaryJobDefOf.Deploy, home.Tile, home);
            op.phase = MilitaryOperationPhase.Engaged;
            op.aggressor.faction = FindFC.EmpireFaction;
            op.aggressor.homeSettlement = home;
            op.aggressor.squad = home.PrimaryStationedSquad;
            manager.Register(op);
            return op;
        }

        private static MilitaryOperation ReconstructDefensiveOp(MilitaryOperationManager manager,
            WorldSettlementFC settlement, WorldObjectComp_SettlementMilitary comp, FCEvent warning)
        {
            int newId = manager.nextOperationId++;
            WorldObject target = warning.settlementFCDefending ?? settlement;
            var op = new MilitaryOperation(newId, MilitaryJobDefOf.DefendOwnSettlement, target.Tile, target);
            op.phase = MilitaryOperationPhase.Scheduled;
            op.nextPhaseTick = warning.timeTillTrigger;
            op.aggressor.faction = warning.militaryForceAttackingFaction
                                ?? warning.militaryForceAttacking?.homeFaction;
            op.aggressor.force = warning.militaryForceAttacking;
            op.defender.faction = warning.militaryForceDefendingFaction ?? FindFC.EmpireFaction;
            op.defender.homeSettlement = warning.militaryForceDefending?.homeSettlement;
            op.defender.force = warning.militaryForceDefending;
            op.externalDefenderSource = warning.externalDefenderSource;

            /* Wire the defender squad the same way CreateDefensiveOp does. Without this,
             * GenerateFriendlies sees op.defender.squad == null and falls into the random-pawn
             * branch instead of using the player's stationed mercs; SpawnReinforcementsForOp
             * for foreign-defender saves bails because squad is null. Skip when an external
             * auto-defender owns the defense (no Empire squad on that path). */
            if (op.externalDefenderSource is null && op.defender.homeSettlement is object)
                op.defender.squad = MilitaryOperationManager.PickPrimaryDefendingSquad(op.defender.homeSettlement);

            manager.Register(op);
            return op;
        }

        private static bool HasLegacyBattleState(WorldObjectComp_SettlementMilitary comp)
        {
            return (comp._legacyAttackers is object && comp._legacyAttackers.Count > 0)
                || (comp._legacyDefenders is object && comp._legacyDefenders.Count > 0)
                || (comp._legacyDraftedNPCs is object && comp._legacyDraftedNPCs.Count > 0)
                || (comp._legacyActiveWaves is object && comp._legacyActiveWaves.Count > 0)
                || comp._legacyBattleMapInitialized
                || comp._legacyInitialDefenderCount > 0;
        }

        private static MilitaryOperation ReconstructEngagedDefensiveOp(MilitaryOperationManager manager,
            WorldSettlementFC settlement, WorldObjectComp_SettlementMilitary comp)
        {
            // Mid-battle: pull force info from the first active wave. EndBattle will fire
            // CompleteBattle on this op when the battle resolves naturally.
            DefenseWave wave = comp._legacyActiveWaves != null && comp._legacyActiveWaves.Count > 0
                ? comp._legacyActiveWaves[0]
                : null;
            if (wave is null) return null;

            int newId = manager.nextOperationId++;
            var op = new MilitaryOperation(newId, MilitaryJobDefOf.DefendOwnSettlement, settlement.Tile, settlement);
            op.phase = MilitaryOperationPhase.Engaged;
            op.nextPhaseTick = -1;
            op.aggressor.faction = wave.attackerFaction ?? wave.attackerForce?.homeFaction;
            op.aggressor.force = wave.attackerForce;
            op.defender.faction = FindFC.EmpireFaction;
            op.defender.homeSettlement = wave.defenderForce?.homeSettlement ?? settlement;
            op.defender.force = wave.defenderForce;
            op.externalDefenderSource = wave.externalDefenderSource;

            // Mirror ReconstructDefensiveOp: wire the defender squad so post-battle reinforcement
            // paths and op-lifecycle code see the same shape as a fresh CreateDefensiveOp.
            if (op.externalDefenderSource is null && op.defender.homeSettlement is object)
                op.defender.squad = MilitaryOperationManager.PickPrimaryDefendingSquad(op.defender.homeSettlement);

            manager.Register(op);
            return op;
        }
    }
}
