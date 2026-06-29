using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Single source of truth for all active <see cref="MilitaryOperation"/>s on a faction.
    /// Lives as a field on <see cref="FactionFC"/> and is reachable via
    /// <c>FactionCache.MilitaryManager</c>.
    /// <para>Owns the canonical <c>active</c> list, indexed lookups (by tile, by squad, by
    /// settlement), and the per-tile <see cref="BattlefieldContext"/> dictionary. Indices are
    /// <c>[Unsaved]</c> and rebuilt from <c>active</c> at load (in <see cref="RebuildIndices"/>).</para>
    /// </summary>
    public class MilitaryOperationManager : IExposable
    {
        /* Saved state */
        public List<MilitaryOperation> active = new List<MilitaryOperation>();
        public Dictionary<PlanetTile, BattlefieldContext> battlefields = new Dictionary<PlanetTile, BattlefieldContext>();
        public int nextOperationId = 1;

        /* Indices — rebuilt on load, not scribed. _bySquad is list-valued because a squad can
         * legitimately appear on more than one op (e.g. defending while a separate cooldown op
         * for the same squad is still draining; or a defender squad shared across waves). */
        [Unsaved] private Dictionary<PlanetTile, List<MilitaryOperation>> _byTile;
        [Unsaved] private Dictionary<MercenarySquadFC, List<MilitaryOperation>> _bySquad;
        [Unsaved] private Dictionary<WorldSettlementFC, List<MilitaryOperation>> _bySettlement;

        /* Scribe scratch buffers for the battlefields dict. RimWorld's
         * Scribe_Collections.Look(ref dict) requires working lists during load. */
        [Unsaved] private List<PlanetTile> _battlefieldKeyScratch;
        [Unsaved] private List<BattlefieldContext> _battlefieldValueScratch;

        public MilitaryOperationManager()
        {
            _byTile = new Dictionary<PlanetTile, List<MilitaryOperation>>();
            _bySquad = new Dictionary<MercenarySquadFC, List<MilitaryOperation>>();
            _bySettlement = new Dictionary<WorldSettlementFC, List<MilitaryOperation>>();
        }

        public bool IsEmpty => (active is null || active.Count == 0)
                            && (battlefields is null || battlefields.Count == 0);

        public IReadOnlyList<MilitaryOperation> Active => active;
        public IReadOnlyDictionary<PlanetTile, BattlefieldContext> Battlefields => battlefields;

        public void ExposeData()
        {
            Scribe_Collections.Look(ref active, "active", LookMode.Deep);
            Scribe_Collections.Look(ref battlefields, "battlefields",
                LookMode.Value, LookMode.Deep,
                ref _battlefieldKeyScratch, ref _battlefieldValueScratch);
            Scribe_Values.Look(ref nextOperationId, "nextOperationId", 1);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (active is null) active = new List<MilitaryOperation>();
                if (battlefields is null) battlefields = new Dictionary<PlanetTile, BattlefieldContext>();
                EnsureIndicesAllocated();
            }
        }

        private void EnsureIndicesAllocated()
        {
            if (_byTile is null) _byTile = new Dictionary<PlanetTile, List<MilitaryOperation>>();
            if (_bySquad is null) _bySquad = new Dictionary<MercenarySquadFC, List<MilitaryOperation>>();
            if (_bySettlement is null) _bySettlement = new Dictionary<WorldSettlementFC, List<MilitaryOperation>>();
        }

        /* -*-*-*-*- Mutation -*-*-*-*- */

        /// <summary>Adds <paramref name="op"/> to the active list and updates indices.
        /// Idempotent: registering the same op twice is a no-op.</summary>
        public void Register(MilitaryOperation op)
        {
            if (op is null) return;
            if (active is null) active = new List<MilitaryOperation>();
            if (active.Contains(op)) return;
            active.Add(op);
            IndexAdd(op);
        }

        /// <summary>Removes <paramref name="op"/> from the active list, drops it from indices,
        /// and detaches from any <see cref="BattlefieldContext"/>. Safe to call after <c>op.Resolve</c>
        /// has already removed map state.</summary>
        public void Unregister(MilitaryOperation op)
        {
            if (op is null) return;
            // Detach first so the context can clean up before the op's tile reference is wiped.
            if (op.battlefieldRef.Valid)
            {
                BattlefieldContext ctx = GetBattlefield(op.battlefieldRef);
                ctx?.Detach(op);
            }
            IndexRemove(op);
            if (active is object) active.Remove(op);
        }

        /// <summary>
        /// Creates an offensive operation: <paramref name="source"/> squad sets out on a job
        /// (raid / capture / enslave / defend-friendly) against <paramref name="target"/>
        /// belonging to <paramref name="enemy"/>. Returns the registered op. The handler's
        /// <see cref="MilitaryJobHandler.OnOpCreated"/> is called after registration so it can
        /// schedule the arrival event and send any letters.
        /// <para><paramref name="source"/> must be assigned to a settlement. The op's home
        /// settlement is the squad's billet.</para>
        /// </summary>
        public MilitaryOperation CreateOffensiveOp(MercenarySquadFC source, WorldObject target,
            MilitaryJobDef jobDef, Faction enemy, int timeToFinish)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (source.settlement is null) throw new ArgumentException("Source squad must be assigned to a settlement to launch an offensive op.", nameof(source));
            if (target is null) throw new ArgumentNullException(nameof(target));
            if (jobDef is null) throw new ArgumentNullException(nameof(jobDef));

            /* Morale lockout backstop: an unhappy/disloyal/restive settlement cannot launch offensive
             * ops. Entry points (SendMilitary, Dialog_AttackSettlement) gate first so no bill is
             * charged; this guards any path that reaches the manager directly. */
            if (source.settlement.SquadDeploymentLocked)
            {
                LogUtil.Warning($"CreateOffensiveOp: settlement {source.settlement.Name} is morale-locked; rejecting offensive op.");
                return null;
            }

            int newId = nextOperationId++;
            var op = new MilitaryOperation(newId, jobDef, target.Tile, target);
            op.phase = MilitaryOperationPhase.Traveling;
            op.nextPhaseTick = Find.TickManager.TicksGame + Math.Max(0, timeToFinish);

            op.aggressor.faction = FindFC.EmpireFaction;
            op.aggressor.homeSettlement = source.settlement;
            op.aggressor.squad = source;
            // Squad-derived force: SquadPowerRegistry maps loadout cost -> military level so
            // two squads at the same billet project distinct forces. Falls through to the
            // unstaffed-billet half-power path only if the squad somehow has no settlement
            // (defensive coding — CreateOffensiveOp's null-settlement check above already
            // rejects this case).
            op.aggressor.force = MilitaryForce.CreateMilitaryForceFromSquad(source, isAttacking: true)
                              ?? MilitaryForce.CreateMilitaryForceFromUnstaffedBillet(source.settlement, isAttacking: true);

            op.defender.faction = enemy;
            // op.defender.force is computed lazily in BeginEngagement via FactionCache.EnemyPower.ResolveDefenderForceForOp.

            Register(op);

            try
            {
                jobDef.Handler?.OnOpCreated(op);
            }
            catch (Exception e)
            {
                LogUtil.Error($"MilitaryOperationManager.CreateOffensiveOp: handler {jobDef.Handler?.GetType().Name} threw in OnOpCreated: {e}");
            }

            LifecycleRegistry.InvokeOnOperationCreated(op);
            return op;
        }

        /// <summary>
        /// Creates a defensive operation: <paramref name="attackerFaction"/> launches
        /// <paramref name="attackerForce"/> at <paramref name="target"/> (an Empire settlement
        /// or external <see cref="IRaidTarget"/>). Returns the registered op (or <c>null</c> if
        /// rejected — already-active defense, missing target comp, etc).
        /// <para>Runs auto-defender selection: scans Empire settlements with <c>autoDefend</c>
        /// for the strongest non-busy non-attacked one that beats the target's level, plus the
        /// best <see cref="IAutoDefender"/> registry entry in range. Whichever is stronger wins.</para>
        /// <para>Schedules the 24-hour <c>settlementBeingAttacked</c> warning event linked back
        /// to the op via <see cref="FCEvent.linkedOperation"/>. The op carries the
        /// force / faction / target data <see cref="BattlefieldContext.StartDefense"/> needs
        /// when the warning fires.</para>
        /// </summary>
        public MilitaryOperation CreateDefensiveOp(WorldObject target, MilitaryForce attackerForce,
            Faction attackerFaction)
        {
            if (target is null) throw new ArgumentNullException(nameof(target));
            if (attackerForce is null) throw new ArgumentNullException(nameof(attackerForce));

            FactionFC factionFC = FindFC.FactionComp;
            if (factionFC is null) return null;

            // Resolve the target settlement (when target is a WorldSettlementFC) so we can run
            // the auto-defender selection. External raid targets get a force generated from
            // their virtual military level via the IAutoDefender path or stay as-is.
            WorldSettlementFC targetSettlement = target as WorldSettlementFC;

            int newId = nextOperationId++;
            // Defensive ops use MilitaryJobHandler_Defend. The handler routes through
            // BattlefieldContext.StartDefense for settlement targets (which decides auto vs manual
            // internally) and through the per-round auto-resolve engine for external IRaidTarget.
            // ApplyResult applies settlement-side effects (loyalty / happiness / building destruction)
            // per-op — multiple concurrent defensive ops on the same tile each apply their own
            // penalty set, treating each attacker as a logically distinct battle.
            var op = new MilitaryOperation(newId, MilitaryJobDefOf.DefendOwnSettlement, target.Tile, target);
            op.phase = MilitaryOperationPhase.Scheduled;
            op.nextPhaseTick = Find.TickManager.TicksGame + GenDate.TicksPerDay;

            op.aggressor.faction = attackerFaction;
            op.aggressor.force = attackerForce;

            op.defender.faction = FindFC.EmpireFaction;
            if (targetSettlement is object)
            {
                // Squad-first defense: pick the strongest available squad billeted at the target,
                // and project its squad-derived force. Empty billets still defend at half-power so
                // the settlement isn't defenseless; cap-0 settlements (structurally non-military)
                // produce null and rely entirely on auto-defender selection / external defenders.
                // The force computed here is a forecast value used by the warning letter; the actual
                // battle-time force is re-sampled in MilitaryOperation.BeginEngagement so post-warning
                // changes (new buildings, policy shifts, event stat modifiers) apply to the fight.
                op.defender.homeSettlement = targetSettlement;
                op.defender.squad = PickPrimaryDefendingSquad(targetSettlement);
                op.defender.force = op.defender.squad is object
                    ? MilitaryForce.CreateMilitaryForceFromSquad(op.defender.squad)
                    : MilitaryForce.CreateMilitaryForceFromUnstaffedBillet(targetSettlement);
            }
            // For external raid targets, defender.force is set below by the auto-defender path.

            // Auto-defender selection. Picks the strongest replacement defender (Empire foreign
            // settlement or external IAutoDefender) if it beats whatever the op currently uses.
            // Runs before Register so the index reflects the final defender.homeSettlement /
            // defender.squad assignment without needing a re-index pass.
            ApplyAutoDefenderSelection(op, target, targetSettlement, factionFC);

            // External IRaidTarget with no eligible auto-defender: synthesize a force from the
            // target's virtual military level so engagement isn't fed a null defender.force.
            if (op.defender.force is null && targetSettlement is null)
            {
                int targetMilLevel = 1;
                foreach (IRaidTarget rt in RaidTargetRegistry.Targets)
                {
                    if (rt?.WorldObject == target) { targetMilLevel = Math.Max(1, rt.MilitaryLevel); break; }
                }
                op.defender.force = new MilitaryForce(targetMilLevel, 1.0, null, op.defender.faction);
            }

            Register(op);

            // Schedule the warning event. The op carries all the force / faction / target data
            // BattlefieldContext.StartDefense needs — no need to mirror anything onto the event.
            FCEvent warningEvent = op.ScheduleEvent(
                FCEventDefOf.settlementBeingAttacked, target.Tile, GenDate.TicksPerDay);
            if (warningEvent is object)
            {
                warningEvent.hasDestination = true;

                // Description + win-chance forecast (mirrors old AttackPlayerSettlement letter).
                string desc = "FCSettlementAboutToBeAttacked".Translate(target.Label, attackerFaction?.Name ?? "").ToString();
                if (op.aggressor.force is object && op.defender.force is object)
                {
                    double winChance = SimulateBattleFc.CalculateDefenderWinChance(op.aggressor.force, op.defender.force);
                    desc += "\n\n" + "FCBattleForecast".Translate(
                        op.aggressor.force.forceRemaining,
                        op.aggressor.force.militaryEfficiency.ToString("0.##"),
                        op.defender.force.DefensivePower,
                        op.defender.force.militaryEfficiency.ToString("0.##"),
                        (winChance * 100).ToString("F0"));
                }
                if (op.externalDefenderSource is object)
                {
                    desc += "\n\n" + "FCExternalDefenderAutoAssigned".Translate(op.externalDefenderSource.LabelCap);
                }
                if (FCSettings.battleMode == BattleMode.Hybrid)
                    desc += "\n\n" + "FCSettlementAttackHybridHint".Translate();
                warningEvent.hasCustomDescription = true;
                warningEvent.customDescription = desc;
            }

            LifecycleRegistry.InvokeOnOperationCreated(op);

            // "Settlement in danger" letter, mirrors old AttackPlayerSettlement.
            try
            {
                Find.LetterStack.ReceiveLetter(
                    "FCSettlementInDanger".Translate(),
                    warningEvent?.customDescription ?? "",
                    LetterDefOf.ThreatBig,
                    new LookTargets(target));
            }
            catch (Exception e)
            {
                LogUtil.Error($"CreateDefensiveOp: failed sending FCSettlementInDanger letter: {e}");
            }

            return op;
        }

        /// <summary>
        /// Runs auto-defender selection for a freshly-created defensive op.
        /// Mutates <paramref name="op"/>'s <c>defender</c> participant if a stronger foreign
        /// squad or external <see cref="IAutoDefender"/> is selected. Rankings use real
        /// squad power (<see cref="SquadPowerRegistry"/>), so a strong squad at a low-level
        /// settlement can outrank a weak squad at a high-level settlement.
        /// </summary>
        private static void ApplyAutoDefenderSelection(MilitaryOperation op, WorldObject target,
            WorldSettlementFC targetSettlement, FactionFC factionFC)
        {
            // Find strongest eligible Empire foreign-defender squad, ranked by squad power.
            MercenarySquadFC bestForeignSquad = null;
            WorldSettlementFC bestForeignBillet = null;
            double bestForeignLevel = -1;
            foreach (WorldSettlementFC candidate in factionFC.settlements)
            {
                if (candidate == targetSettlement) continue;
                if (candidate.MilitaryComp is null) continue;
                if (candidate.MilitaryComp.isUnderAttack) continue;
                if (targetSettlement is object && !DefenseValidatorRegistry.CanDefend(candidate, targetSettlement)) continue;
                foreach (MercenarySquadFC squad in candidate.StationedSquads)
                {
                    if (squad is null || !squad.autoDefend) continue;
                    if (!squad.IsAvailable) continue;
                    double squadLevel = SquadPowerRegistry.Resolve(squad).militaryLevel;
                    if (squadLevel > bestForeignLevel)
                    {
                        bestForeignSquad = squad;
                        bestForeignBillet = candidate;
                        bestForeignLevel = squadLevel;
                    }
                }
            }

            // Find best external auto-defender in range.
            IAutoDefender bestExternal = AutoDefenderRegistry.FindBestDefender(target.Tile, 0);

            // Project the foreign squad's defending force the same way the target's force was
            // projected. Comparing the foreign squad's raw loadout level against the target's
            // already-bonused level was an apples-to-oranges asymmetry that made small foreign
            // squads lose to empty-billet synthetic forces of equal level.
            MilitaryForce foreignProjected = bestForeignSquad is object
                ? MilitaryForce.CreateMilitaryForceFromSquad(bestForeignSquad, isAttacking: false)
                : null;

            double targetLevel = op.defender.force?.militaryLevel ?? 0;
            double foreignLevel = foreignProjected?.militaryLevel ?? 0;
            double externalLevel = bestExternal?.MilitaryLevel ?? 0;

            // When the target has no real stationed squad, op.defender.force is the half-power
            // unstaffed-billet synthetic — it shouldn't gate a real foreign squad the user opted
            // into via autoDefend. Override unconditionally in that case; otherwise compare power.
            bool targetHasOwnSquad = op.defender.squad is object;

            // Foreign squad wins if either (a) target has no own squad to project — any real
            // foreign squad beats a synthetic billet — or (b) it beats the target's level outright.
            // In both branches it must also be at least as strong as the external option.
            if (bestForeignSquad is object
                && (!targetHasOwnSquad || foreignLevel > targetLevel)
                && foreignLevel >= externalLevel)
            {
                op.defender.homeSettlement = bestForeignBillet;
                op.defender.squad = bestForeignSquad;
                op.defender.force = MilitaryForce.CreateMilitaryForceFromSquad(bestForeignSquad, isAttacking: false);
                op.externalDefenderSource = null;
                return;
            }

            // External wins if it beats the target's level (and the foreign was not stronger).
            // Same "no own squad" override applies — synthetic billet shouldn't gate a real
            // external defender either.
            if (bestExternal is object && (!targetHasOwnSquad || externalLevel > targetLevel))
            {
                op.defender.homeSettlement = null;
                op.defender.squad = null;
                op.defender.force = bestExternal.CreateDefendingForce();
                op.externalDefenderSource = bestExternal.WorldObject;
                // Pledge now (during the warning window), not at engagement, so the defender reads as
                // committed in its UI and can't be double-booked by a second concurrent attack.
                bestExternal.OnDefensePledged(target);
                return;
            }

            // No replacement defender — op.defender keeps its target-settlement default.
        }

        /// <summary>Picks the strongest <see cref="MercenarySquadFC.IsAvailable"/> squad stationed
        /// at <paramref name="settlement"/> as the primary defender, ranked by
        /// <see cref="SquadPowerRegistry"/> projected power. Returns null if no squad qualifies
        /// (all busy / cooldown / no squads stationed).
        /// <para>Internal so <see cref="MilitaryMigrationUtil"/> can wire the defender squad onto
        /// reconstructed defensive ops the same way <see cref="CreateDefensiveOp"/> does on fresh
        /// ops.</para></summary>
        internal static MercenarySquadFC PickPrimaryDefendingSquad(WorldSettlementFC settlement)
        {
            if (settlement is null) return null;
            MercenarySquadFC best = null;
            double bestPower = -1;
            foreach (MercenarySquadFC squad in settlement.StationedSquads)
            {
                if (squad is null) continue;
                if (!squad.IsAvailable) continue;
                double power = SquadPowerRegistry.Resolve(squad).militaryLevel;
                if (power > bestPower)
                {
                    best = squad;
                    bestPower = power;
                }
            }
            return best;
        }

        /// <summary>
        /// Creates a "deploy" operation: the empire's squad is spawned on a player map (typically
        /// the home colony) for direct combat support. Unlike offensive ops, there's no arrival
        /// event — the squad is already physical when this fires. The op stays in <c>Engaged</c>
        /// until the squad's lord finalizes (via <see cref="MilitaryOperation.CompleteBattle"/>),
        /// then transitions through cooldown like any other op.
        /// </summary>
        public MilitaryOperation CreateDeployOp(MercenarySquadFC source, PlanetTile deployTile)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));

            /* Morale lockout backstop (see CreateOffensiveOp). Deploys are gated too; the deploy
             * entry (CallinAlliedForces) checks first so no bill is charged. */
            if (source.settlement is object && source.settlement.SquadDeploymentLocked)
            {
                LogUtil.Warning($"CreateDeployOp: settlement {source.settlement.Name} is morale-locked; rejecting deploy op.");
                return null;
            }

            int newId = nextOperationId++;
            // Use the current map's WorldObject as the targetObject if present, otherwise null.
            WorldObject targetObject = Find.WorldObjects.WorldObjectAt<WorldObject>(deployTile);
            var op = new MilitaryOperation(newId, MilitaryJobDefOf.Deploy, deployTile, targetObject);
            op.phase = MilitaryOperationPhase.Engaged;
            op.phaseStartedTick = Find.TickManager.TicksGame;
            // Mirror the LordJob's force-leave horizon onto nextPhaseTick so the busy display
            // (Math.Max(0, op.nextPhaseTick - now) / TicksPerDay) shows real time remaining
            // rather than 0.0 d. Deploy has no scheduled phase event, so this value is purely
            // informational — the actual transition out of Engaged happens via FinalizeDeployment.
            op.nextPhaseTick = Find.TickManager.TicksGame
                + LordJob_DeployMilitary.DefaultMaxDeploymentTime
                + LordJob_DeployMilitary.PostLeaveGraceTicks;
            op.aggressor.faction = FindFC.EmpireFaction;
            op.aggressor.homeSettlement = source.settlement;
            op.aggressor.squad = source;
            // No defender — Deploy isn't an attack operation, just squad presence.

            Register(op);
            LifecycleRegistry.InvokeOnOperationCreated(op);
            return op;
        }

        /// <summary>Get-or-create a <see cref="BattlefieldContext"/> for <paramref name="tile"/>.</summary>
        public BattlefieldContext GetOrCreateBattlefield(PlanetTile tile)
        {
            if (battlefields is null) battlefields = new Dictionary<PlanetTile, BattlefieldContext>();
            if (!battlefields.TryGetValue(tile, out BattlefieldContext ctx))
            {
                ctx = new BattlefieldContext(tile);
                battlefields[tile] = ctx;
            }
            return ctx;
        }

        /// <summary>Removes the battlefield at <paramref name="tile"/> from the manager.
        /// Does not clean up its map / pawns — that's the context's job during <c>Detach</c>.</summary>
        internal void RemoveBattlefield(PlanetTile tile)
        {
            if (battlefields is object) battlefields.Remove(tile);
        }

        /* -*-*-*-*- Queries -*-*-*-*- */

        public IReadOnlyList<MilitaryOperation> GetOpsAt(PlanetTile tile)
        {
            EnsureIndicesAllocated();
            if (_byTile.TryGetValue(tile, out List<MilitaryOperation> list)) return list;
            return Array.Empty<MilitaryOperation>();
        }

        /// <summary>Returns the first registered op the squad is participating in, or <c>null</c>.
        /// Convenience wrapper over <see cref="GetOpsForSquad"/> for callers that just want
        /// "the active op" (most do — squads usually appear on at most one op at a time).</summary>
        public MilitaryOperation GetOpForSquad(MercenarySquadFC squad)
        {
            IReadOnlyList<MilitaryOperation> ops = GetOpsForSquad(squad);
            return ops.Count > 0 ? ops[0] : null;
        }

        /// <summary>Returns every registered op the squad is participating in. A squad can legitimately
        /// appear on more than one op (e.g. a foreign-defender squad still in cooldown when a fresh
        /// defensive op references it as an inactive home-settlement-squad).</summary>
        public IReadOnlyList<MilitaryOperation> GetOpsForSquad(MercenarySquadFC squad)
        {
            if (squad is null) return Array.Empty<MilitaryOperation>();
            EnsureIndicesAllocated();
            if (_bySquad.TryGetValue(squad, out List<MilitaryOperation> list)) return list;
            return Array.Empty<MilitaryOperation>();
        }

        public IReadOnlyList<MilitaryOperation> GetOpsForSettlement(WorldSettlementFC settlement)
        {
            if (settlement is null) return Array.Empty<MilitaryOperation>();
            EnsureIndicesAllocated();
            if (_bySettlement.TryGetValue(settlement, out List<MilitaryOperation> list)) return list;
            return Array.Empty<MilitaryOperation>();
        }

        public bool HasDefenseAt(WorldSettlementFC settlement) => GetDefensiveOpAt(settlement) is object;

        /// <summary>Returns the live defensive op targeting <paramref name="settlement"/>, or null.
        /// This is the source of truth for "under attack" — the warning event may already be gone
        /// (StartDefense strips it once the battle begins), but the op lives on through Engaged.</summary>
        public MilitaryOperation GetDefensiveOpAt(WorldSettlementFC settlement)
        {
            if (settlement is null) return null;
            // "Under attack" is target-based, not defender-based: when a foreign auto-defender
            // is selected, op.defender.homeSettlement points at the foreign billet (the squad's
            // home), not the settlement actually being attacked. Look up by target tile and
            // match on op.targetObject so the right settlement gets the under-attack flag.
            // Filter out ops in CooldownPending/Resolved — the battle is already over and the
            // squad is recovering; settlement is no longer "under attack" semantically.
            IReadOnlyList<MilitaryOperation> ops = GetOpsAt(settlement.Tile);
            for (int i = 0; i < ops.Count; i++)
            {
                MilitaryOperation op = ops[i];
                if (op.targetObject != settlement) continue;
                if (op.phase == MilitaryOperationPhase.CooldownPending) continue;
                if (op.phase == MilitaryOperationPhase.Resolved) continue;
                return op;
            }
            return null;
        }

        public bool HasOffensiveOpFrom(WorldSettlementFC settlement)
        {
            if (settlement is null) return false;
            IReadOnlyList<MilitaryOperation> ops = GetOpsForSettlement(settlement);
            for (int i = 0; i < ops.Count; i++)
            {
                MilitaryOperation op = ops[i];
                if (op.aggressor.homeSettlement == settlement) return true;
            }
            return false;
        }

        public bool IsSquadBusy(MercenarySquadFC squad) => GetOpsForSquad(squad).Count > 0;

        public bool IsTileOccupiedBy(PlanetTile tile, MilitaryJobDef kind)
        {
            IReadOnlyList<MilitaryOperation> ops = GetOpsAt(tile);
            for (int i = 0; i < ops.Count; i++)
            {
                if (ops[i].kind == kind) return true;
            }
            return false;
        }

        public BattlefieldContext GetBattlefield(PlanetTile tile)
        {
            if (battlefields is null) return null;
            battlefields.TryGetValue(tile, out BattlefieldContext ctx);
            return ctx;
        }

        /// <summary>Rebuilds the per-tile / per-squad / per-settlement indices from <see cref="active"/>.
        /// Called from <c>FactionFC.FinalizeInit</c> and after migration.</summary>
        public void RebuildIndices()
        {
            EnsureIndicesAllocated();
            _byTile.Clear();
            _bySquad.Clear();
            _bySettlement.Clear();

            if (active is null) return;
            for (int i = 0; i < active.Count; i++)
            {
                MilitaryOperation op = active[i];
                if (op is null) continue;
                IndexAdd(op);
            }
        }

        internal void IndexAdd(MilitaryOperation op)
        {
            EnsureIndicesAllocated();
            if (op.targetTile.Valid)
            {
                if (!_byTile.TryGetValue(op.targetTile, out List<MilitaryOperation> tileList))
                {
                    tileList = new List<MilitaryOperation>();
                    _byTile[op.targetTile] = tileList;
                }
                if (!tileList.Contains(op)) tileList.Add(op);
            }
            IndexSquad(op, op.aggressor?.squad);
            IndexSquad(op, op.defender?.squad);
            IndexSettlement(op, op.aggressor?.homeSettlement);
            IndexSettlement(op, op.defender?.homeSettlement);
        }

        internal void IndexRemove(MilitaryOperation op)
        {
            EnsureIndicesAllocated();
            if (op.targetTile.Valid && _byTile.TryGetValue(op.targetTile, out List<MilitaryOperation> tileList))
            {
                tileList.Remove(op);
                if (tileList.Count == 0) _byTile.Remove(op.targetTile);
            }
            UnindexSquad(op, op.aggressor?.squad);
            UnindexSquad(op, op.defender?.squad);
            UnindexSettlement(op, op.aggressor?.homeSettlement);
            UnindexSettlement(op, op.defender?.homeSettlement);
        }

        private void IndexSquad(MilitaryOperation op, MercenarySquadFC squad)
        {
            if (squad is null) return;
            if (!_bySquad.TryGetValue(squad, out List<MilitaryOperation> list))
            {
                list = new List<MilitaryOperation>();
                _bySquad[squad] = list;
            }
            if (!list.Contains(op)) list.Add(op);
        }

        private void UnindexSquad(MilitaryOperation op, MercenarySquadFC squad)
        {
            if (squad is null) return;
            if (_bySquad.TryGetValue(squad, out List<MilitaryOperation> list))
            {
                list.Remove(op);
                if (list.Count == 0) _bySquad.Remove(squad);
            }
        }

        private void IndexSettlement(MilitaryOperation op, WorldSettlementFC settlement)
        {
            if (settlement is null) return;
            if (!_bySettlement.TryGetValue(settlement, out List<MilitaryOperation> list))
            {
                list = new List<MilitaryOperation>();
                _bySettlement[settlement] = list;
            }
            if (!list.Contains(op)) list.Add(op);
        }

        private void UnindexSettlement(MilitaryOperation op, WorldSettlementFC settlement)
        {
            if (settlement is null) return;
            if (_bySettlement.TryGetValue(settlement, out List<MilitaryOperation> list))
            {
                list.Remove(op);
                if (list.Count == 0) _bySettlement.Remove(settlement);
            }
        }
    }
}
