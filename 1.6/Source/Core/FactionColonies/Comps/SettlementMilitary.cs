using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;

// Save-load buffers (declared below) reference [Obsolete] DefenseWave; they are drained on
// PostLoadInit by MilitaryMigrationUtil. The runtime surface is computed properties backed by
// the manager. File-level pragma scopes the obsolete-warning silence to this file.
#pragma warning disable 0618

namespace FactionColonies
{
    public class WorldObjectCompProperties_SettlementMilitary : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_SettlementMilitary()
        {
            compClass = typeof(WorldObjectComp_SettlementMilitary);
        }
        public override IEnumerable<string> ConfigErrors(WorldObjectDef parentDef)
        {
            foreach (string item in base.ConfigErrors(parentDef))
            {
                yield return item;
            }
            if (!typeof(MapParent).IsAssignableFrom(parentDef.worldObjectClass))
            {
                yield return parentDef.defName + " has WorldObjectCompProperties_SettlementMilitary but it's not MapParent.";
            }
        }
    }

    public class WorldObjectComp_SettlementMilitary : WorldObjectComp, ISettlementPostLoadInit
    {
        private WorldSettlementFC cachedWorldSettlementParent = null;
        public WorldSettlementFC WorldSettlement
        {
            get
            {
                if (cachedWorldSettlementParent != null)
                {
                    return cachedWorldSettlementParent;
                }
                if (parent is WorldSettlementFC ws)
                {
                    cachedWorldSettlementParent = ws;
                }
                else
                {
                    cachedWorldSettlementParent = null;
                    LogUtil.ErrorOnce($"WorldObjectComp_SettlementMilitary has a non-WorldSettlementFC parent: {parent.Label}", 93512108);
                }
                return cachedWorldSettlementParent;
            }
        }
        public Map Map => WorldSettlement.Map;

        public int artilleryTimer = 0;
        public int settlementMilitaryLevel;

        /// <summary>Compatibility shim: 1:1 settlement-to-squad accessor. Squads live on the
        /// faction-wide pool and reference their billet via <see cref="MercenarySquadFC.settlement"/>.
        /// New code should iterate <see cref="WorldSettlementFC.StationedSquads"/>; this shim
        /// returns the first stationed squad for cross-mod source compatibility.</summary>
        [System.Obsolete("Use WorldSettlementFC.StationedSquads. This shim returns the primary stationed squad.")]
        public MercenarySquadFC militarySquad
        {
            get
            {
                List<MercenarySquadFC> stationed = WorldSettlement?.StationedSquads;
                if (stationed is null || stationed.Count == 0) return null;
                return stationed[0];
            }
            set
            {
                // Setter semantics: "this is THE squad now". Detach any other stationed squads,
                // then attach the new one to this settlement.
                if (WorldSettlement is null) return;
                if (value is null)
                {
                    // Snapshot the list — assigning settlement = null mutates StationedSquads.
                    List<MercenarySquadFC> stationed = new List<MercenarySquadFC>(WorldSettlement.StationedSquads);
                    foreach (MercenarySquadFC s in stationed)
                    {
                        if (s is object) s.settlement = null;
                    }
                    return;
                }
                if (value.settlement == WorldSettlement) return;
                List<MercenarySquadFC> existing = new List<MercenarySquadFC>(WorldSettlement.StationedSquads);
                foreach (MercenarySquadFC s in existing)
                {
                    if (s is object && s != value) s.settlement = null;
                }
                value.settlement = WorldSettlement;
            }
        }

        /// <summary>Compatibility shim: per-settlement auto-defend flag. Auto-defend lives on
        /// the squad (<see cref="MercenarySquadFC.autoDefend"/>) so a settlement with multiple
        /// squads can opt some in and some out. The shim returns true when any stationed squad
        /// has <c>autoDefend</c> set; the setter applies the flag to all stationed squads.</summary>
        [System.Obsolete("Use MercenarySquadFC.autoDefend. This shim aggregates across stationed squads.")]
        public bool autoDefend
        {
            get
            {
                List<MercenarySquadFC> stationed = WorldSettlement?.StationedSquads;
                if (stationed is null) return false;
                for (int i = 0; i < stationed.Count; i++)
                {
                    if (stationed[i] != null && stationed[i].autoDefend) return true;
                }
                return false;
            }
            set
            {
                List<MercenarySquadFC> stationed = WorldSettlement?.StationedSquads;
                if (stationed is null) return;
                for (int i = 0; i < stationed.Count; i++)
                {
                    if (stationed[i] != null) stationed[i].autoDefend = value;
                }
            }
        }

        // -*-*-*-*- Squad-first comp-side load buffers -*-*-*-*-
        // The canonical home for these values is MercenarySquadFC (squad.settlement /
        // squad.autoDefend). When a save XML carries them on the comp, ExposeData captures
        // them into these [Unsaved] buffers; MilitaryMigrationUtil drains them in PostLoadInit.
        // Never written on save.
        [Unsaved] public MercenarySquadFC _legacyMilitarySquad;
        [Unsaved] public bool _legacyAutoDefend;

        /* -*-*-*-*- Operation-state comp-side load buffers -*-*-*-*-
         * The canonical home for op state is MilitaryOperation on the manager; the comp's
         * militaryBusy / militaryJob / militaryLocation / militaryEnemy / isUnderAttack are
         * computed properties that read from the manager (defined below). Battle infrastructure
         * (attackers / defenders / draftedNPCs) lives on BattlefieldContext. When a save XML
         * carries these values on the comp, ExposeData reads them into the buffers below
         * during LoadingVars; <see cref="MilitaryMigrationUtil"/> drains them in PostLoadInit.
         * They are never written on save.
         */
        public bool _legacyMilitaryBusy;
        public MilitaryJobDef _legacyMilitaryJob;
        public PlanetTile _legacyMilitaryLocation = PlanetTile.Invalid;
        public Faction _legacyMilitaryEnemy;
        public bool _legacyIsUnderAttack;
        public List<Pawn> _legacyAttackers;
        public List<Pawn> _legacyDefenders;
        public List<Pawn> _legacyDraftedNPCs;
        public List<DefenseWave> _legacyActiveWaves;
        public bool _legacyBattleMapInitialized;
        public int _legacyInitialDefenderCount;

        /* -*-*-*-*- Computed battle state (derived from BattlefieldContext) -*-*-*-*-
         * Read-only proxies onto the per-tile BattlefieldContext owned by the manager.
         * External code that read these fields (UI / VEF compat / WorldSettlementFC / debug)
         * continues to compile and read correctly. Internal mutations live on BattlefieldContext.
         */

        private static readonly List<Pawn> _emptyPawnList = new List<Pawn>();

        private BattlefieldContext Battlefield
            => FindFC.MilitaryManager?.GetBattlefield(WorldSettlement?.Tile ?? PlanetTile.Invalid);

        public IEnumerable<Pawn> attackers => Battlefield?.attackerPawns ?? Enumerable.Empty<Pawn>();
        public IEnumerable<Pawn> defenders => Battlefield?.defenderPawns ?? Enumerable.Empty<Pawn>();
        public List<Pawn> draftedNPCs => Battlefield?.draftedNPCs ?? _emptyPawnList;

        /* -*-*-*-*- Computed properties (derived from manager state) -*-*-*-*- */

        /// <summary>True when this settlement has any active op in which it's the squad-bearer
        /// (offensive aggressor, deploy aggressor, or foreign defender of another settlement's
        /// defensive battle).</summary>
        public bool militaryBusy
        {
            get
            {
                MilitaryOperationManager manager = FindFC.MilitaryManager;
                if (manager is null) return false;
                IReadOnlyList<MilitaryOperation> ops = manager.GetOpsForSettlement(WorldSettlement);
                for (int i = 0; i < ops.Count; i++)
                {
                    MilitaryOperation op = ops[i];
                    if (op.aggressor?.homeSettlement == WorldSettlement) return true;
                    if (op.defender?.homeSettlement == WorldSettlement
                        && (op.targetObject as WorldSettlementFC) != WorldSettlement) return true;
                }
                return false;
            }
        }

        /// <summary>Current op kind for this settlement: the aggressor op's kind (Raid/Capture/
        /// Enslave/Deploy/Cooldown) or <c>DefendFriendlySettlement</c> if foreign-defending,
        /// else <c>Undefined</c>.</summary>
        public MilitaryJobDef militaryJob
        {
            get
            {
                MilitaryOperation op = FindOwnOp();
                if (op is null) return MilitaryJobDefOf.Undefined;
                if (op.phase == MilitaryOperationPhase.CooldownPending) return MilitaryJobDefOf.Cooldown;
                if (op.aggressor?.homeSettlement == WorldSettlement) return op.kind ?? MilitaryJobDefOf.Undefined;
                // Foreign-defender case
                return MilitaryJobDefOf.DefendFriendlySettlement;
            }
        }

        /// <summary>Target tile of the active op (raid target, deploy map tile, or defended settlement).</summary>
        public PlanetTile militaryLocation
        {
            get
            {
                MilitaryOperation op = FindOwnOp();
                return op?.targetTile ?? PlanetTile.Invalid;
            }
        }

        /// <summary>Enemy faction in the current op (defender's faction for offensive ops,
        /// aggressor's faction for foreign-defender ops).</summary>
        public Faction militaryEnemy
        {
            get
            {
                MilitaryOperation op = FindOwnOp();
                if (op is null) return null;
                if (op.aggressor?.homeSettlement == WorldSettlement) return op.defender?.faction;
                // Foreign-defender: enemy is the attacker
                return op.aggressor?.faction;
            }
        }

        /// <summary>True when this settlement is the target of any active defensive op.</summary>
        public bool isUnderAttack => FindFC.MilitaryManager?.HasDefenseAt(WorldSettlement) ?? false;

        /// <summary>True while a defensive op targets this settlement but its battle has NOT yet
        /// begun (24h warning window still open). Once the op engages, the manual Defend entry
        /// points hide so re-firing StartDefence can't spawn a duplicate attacker wave. An
        /// in-flight caravan that arrives post-engagement joins via CaravanDefend instead (see
        /// StartDefence).</summary>
        public bool canStartDefense
        {
            get
            {
                MilitaryOperation op = FindFC.MilitaryManager?.GetDefensiveOpAt(WorldSettlement);
                return op is object && op.phase != MilitaryOperationPhase.Engaged;
            }
        }

        /// <summary>Aggressor's force in the active defensive battle on this tile, or null.</summary>
        public MilitaryForce attackerForce => FindIncomingDefensiveOp()?.aggressor?.force;

        /// <summary>Defender's force in the active defensive battle on this tile, or null.</summary>
        public MilitaryForce defenderForce => FindIncomingDefensiveOp()?.defender?.force;

        /// <summary>Returns the first op where this settlement is "the actor" — aggressor of any
        /// op, or foreign defender of someone else's defensive op. Used by the computed
        /// militaryJob / militaryLocation / militaryEnemy properties.</summary>
        private MilitaryOperation FindOwnOp()
        {
            MilitaryOperationManager manager = FindFC.MilitaryManager;
            if (manager is null) return null;
            IReadOnlyList<MilitaryOperation> ops = manager.GetOpsForSettlement(WorldSettlement);
            for (int i = 0; i < ops.Count; i++)
            {
                MilitaryOperation op = ops[i];
                if (op.aggressor?.homeSettlement == WorldSettlement) return op;
                if (op.defender?.homeSettlement == WorldSettlement
                    && (op.targetObject as WorldSettlementFC) != WorldSettlement) return op;
            }
            return null;
        }

        /// <summary>Returns the defensive op targeting THIS settlement, or null.</summary>
        private MilitaryOperation FindIncomingDefensiveOp()
        {
            MilitaryOperationManager manager = FindFC.MilitaryManager;
            if (manager is null) return null;
            IReadOnlyList<MilitaryOperation> ops = manager.GetOpsForSettlement(WorldSettlement);
            for (int i = 0; i < ops.Count; i++)
            {
                MilitaryOperation op = ops[i];
                if (op.IsDefensive && (op.targetObject as WorldSettlementFC) == WorldSettlement) return op;
            }
            return null;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref artilleryTimer, "artilleryTimer");
            Scribe_Values.Look(ref settlementMilitaryLevel, "settlementMilitaryLevel");

            /* On load, fill the comp-side migration buffers from XML so MilitaryMigrationUtil
             * can drain them in PostLoadInit. Canonical state lives on MilitaryOperationManager
             * (ops) and BattlefieldContext (battle pawns); save writes use the manager-owned
             * layout, so nothing here is written on Saving (old-format comp XML only resolves
             * when loading a pre-refactor save).
             *
             * The DIRECT comp-level reference reads (Scribe_References + LookMode.Reference
             * lists) MUST run in BOTH LoadingVars and ResolvingCrossRefs: RimWorld registers
             * each loadID in LoadingVars and only consumes it in ResolvingCrossRefs (see
             * Scribe_References.Look / TakeResolvedRef). Gating them to LoadingVars alone leaves
             * every loadID unconsumed -- "Not all loadIDs which were read were consumed" -- and
             * leaves the buffers null, so the squad/battle migration silently drops its data.
             * Deep reads (activeWaves, the forces) self-resolve via the global cross-ref pass
             * (ScribeExtractor.SaveableFromNode registers them), so they stay LoadingVars-only. */
            if (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.ResolvingCrossRefs)
            {
                // Squad-first split: capture militarySquad from the XML into an [Unsaved] buffer.
                // MigrateLegacyComp_MilitarySquad consumes it in PostLoadInit and writes
                // squad.settlement = this + squad.autoDefend = buffer.
                Scribe_References.Look(ref _legacyMilitarySquad, "militarySquad");
                Scribe_References.Look(ref _legacyMilitaryEnemy, "militaryEnemy");

                Scribe_Collections.Look(ref _legacyAttackers, "attackers", LookMode.Reference);
                Scribe_Collections.Look(ref _legacyDefenders, "defenders", LookMode.Reference);
                Scribe_Collections.Look(ref _legacyDraftedNPCs, "draftedNPCs", LookMode.Reference);
            }

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Values.Look(ref _legacyAutoDefend, "autoDefend", false);

                Scribe_Values.Look(ref _legacyMilitaryBusy, "militaryBusy", false);
                Scribe_Defs.Look(ref _legacyMilitaryJob, "militaryJob");
                Scribe_Values.Look(ref _legacyMilitaryLocation, "militaryLocation", PlanetTile.Invalid);
                Scribe_Values.Look(ref _legacyIsUnderAttack, "isUnderAttack", false);
                Scribe_Values.Look(ref _legacyBattleMapInitialized, "battleMapInitialized", false);
                Scribe_Values.Look(ref _legacyInitialDefenderCount, "initialDefenderCount", 0);

                Scribe_Collections.Look(ref _legacyActiveWaves, "activeWaves", LookMode.Deep);

                MilitaryForce legacyAttackerForce = null;
                MilitaryForce legacyDefenderForce = null;
                Scribe_Deep.Look(ref legacyAttackerForce, "attackerForce");
                Scribe_Deep.Look(ref legacyDefenderForce, "defenderForce");

                if (_legacyActiveWaves is null) _legacyActiveWaves = new List<DefenseWave>();
                if (_legacyActiveWaves.Count == 0 && (legacyAttackerForce is object || legacyDefenderForce is object))
                {
                    _legacyActiveWaves.Add(new DefenseWave
                    {
                        attackerForce = legacyAttackerForce,
                        defenderForce = legacyDefenderForce,
                        attackerFaction = legacyAttackerForce?.homeFaction
                    });
                }
            }
        }

        public override void Initialize(WorldObjectCompProperties props_l)
        {
            base.Initialize(props_l);
        }

        public override void CompTick()
        {
            // base.CompTick() is empty in Rimworld 1.6.
            Battlefield?.Tick();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            if (canStartDefense)
            {
                yield return DefendColonyAction();
            }
            if (isUnderAttack)
            {
                // Show change-defender gizmos for each pending (not yet fired) attack event
                IReadOnlyList<FCEvent> pendingEvents = MilitaryOperationsUtil.ReturnMilitaryEventsByLocation(WorldSettlement.Tile);
                for (int i = 0; i < pendingEvents.Count; i++)
                {
                    yield return ChangeDefenderAction(pendingEvents[i]);
                }
            }

            // "Watch battle" gizmo: visible while an auto-resolved battle on this settlement
            // is in flight. Opens the live BattleProgressWindow for the linked op so the
            // player can watch rolls land hour by hour.
            MilitaryOperation activeBattle = FindActiveBattleProgressOp();
            if (activeBattle is object)
            {
                yield return WatchBattleAction(activeBattle);
            }
        }

        private MilitaryOperation FindActiveBattleProgressOp()
        {
            MilitaryOperationManager mgr = FindFC.MilitaryManager;
            if (mgr is null) return null;
            IReadOnlyList<MilitaryOperation> active = mgr.active;
            foreach (MilitaryOperation op in active)
            {
                if (op?.battleResult is null) continue;
                if (op.phase != MilitaryOperationPhase.Engaged) continue;
                if (op.defender?.homeSettlement == WorldSettlement) return op;
            }
            return null;
        }

        private Command WatchBattleAction(MilitaryOperation op)
        {
            return new Command_Action
            {
                defaultLabel = "FCBattleProgressGizmoLabel".Translate(),
                defaultDesc = "FCBattleProgressGizmoDesc".Translate(),
                icon = TexLoad.iconMilitary,
                action = delegate { Find.WindowStack.Add(new BattleProgressWindow(op)); }
            };
        }

        private Command DefendColonyAction()
        {
            Command_Action defendColony = new Command_Action
            {
                defaultLabel = "FCDefendColony".Translate(),
                defaultDesc = "FCDefendColonyDesc".Translate(),
                icon = TexLoad.iconMilitary,
                action = delegate
                {
                    StartDefence(MilitaryOperationsUtil.ReturnMilitaryEventByLocation(WorldSettlement.Tile), () => { });
                }
            };
            /* If auto-battle is enabled, then disable the button. We leave it visible, though, so that the player knows that this is an option if
             * they change their settings. */
            AcceptanceReport canUse = CanDoManualFight();
            if (!canUse.Accepted)
            {
                defendColony.Disable(canUse.Reason);
            }

            return defendColony;
        }

        private Command ChangeDefenderAction(FCEvent evt)
        {
            return new Command_Action
            {
                defaultLabel = "FCDefendSettlement".Translate(),
                defaultDesc = "",
                icon = TexLoad.iconCustomize,
                action = delegate { Find.WindowStack.Add(new Dialog_DefendSettlement(evt)); }
            };
        }

        public override IEnumerable<Gizmo> GetCaravanGizmos(Caravan caravan)
        {
            foreach (Gizmo gizmo in base.GetCaravanGizmos(caravan))
            {
                yield return gizmo;
            }
            if (isUnderAttack)
            {
                yield return DefendColonyCaravan(caravan);
            }
        }

        private Command DefendColonyCaravan(Caravan caravan)
        {
            Command_Action defendColonyCaravan = new Command_Action
            {
                defaultLabel = "FCDefendColony".Translate(),
                defaultDesc = "FCDefendColonyDesc".Translate(),
                icon = TexLoad.iconMilitary,
                action = () =>
                {
                    StartDefence(MilitaryOperationsUtil.ReturnMilitaryEventByLocation(WorldSettlement.Tile), () => CaravanDefend(caravan));
                }
            };
            /* If auto-battle is enabled, then disable the button. We leave it visible, though, so that the player knows that this is an option if
             * they change their settings. (Once manual fighting becomes an option the player can use, at least) */
            AcceptanceReport canUse = CanDoManualFight();
            if (!canUse.Accepted)
            {
                defendColonyCaravan.Disable(canUse.Reason);
            }

            return defendColonyCaravan;
        }

        private AcceptanceReport CanDoManualFight()
        {
            if (!WorldSettlement.settlementDef.supportsManualBattle)
            {
                return new AcceptanceReport("FCSettlementTypeNoManualBattle".Translate());
            }
            if (FCSettings.battleMode == BattleMode.Auto)
            {
                return new AcceptanceReport("FCAutoBattleEnabledNoManualFight".Translate());
            }
            if (FCSettings.battleMode == BattleMode.Hybrid && !PlayerCaravanOnSettlementTile())
            {
                return new AcceptanceReport("FCHybridBattleEnabledNoManualFight".Translate());
            }
            return AcceptanceReport.WasAccepted;
        }

        // CaravanDefend / AddToDefenceFromList are thin wrappers around BattlefieldContext;
        // external callers (VEF Harmony patch, WorldSettlementDefendAction,
        // TransportPodArrivalActionPatch) target them by name on the comp.

        private bool PlayerCaravanOnSettlementTile()
        {
            return Find.WorldObjects.Caravans.Any(c =>
                c.Tile == WorldSettlement.Tile && c.Faction == Faction.OfPlayer);
        }

        public void CaravanDefend(Caravan caravan)
        {
            BattlefieldContext bf = FindFC.MilitaryManager?.GetOrCreateBattlefield(WorldSettlement.Tile);
            if (bf is null)
            {
                LogUtil.Error($"CaravanDefend: no battlefield for {WorldSettlement?.Name}.");
                return;
            }
            bf.CaravanDefend(caravan);
        }

        public void AddToDefenceFromList(List<Pawn> pawns, PlanetTile destinationTile)
        {
            AddToDefenceFromList(pawns, destinationTile, assignToLord: true);
        }

        public void AddToDefenceFromList(List<Pawn> pawns, PlanetTile destinationTile, bool assignToLord)
        {
            BattlefieldContext bf = FindFC.MilitaryManager?.GetOrCreateBattlefield(destinationTile);
            if (bf is null)
            {
                LogUtil.Error($"AddToDefenceFromList: no battlefield for tile {destinationTile}.");
                return;
            }
            bf.AddToDefenceFromList(pawns, destinationTile, assignToLord);
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan)
        {
            if (isUnderAttack)
                foreach (var option in WorldSettlementDefendAction.GetFloatMenuOptions(caravan, WorldSettlement))
                    yield return option;
        }

        /// <summary>Thin delegation to <see cref="BattlefieldContext.StartDefense"/>. Looks up the op
        /// linked to <paramref name="evt"/> and routes the start-defense flow through it.
        /// External callers (manual Defend gizmo, CaravanDefend, debug actions) keep using this
        /// entry point for source compatibility.</summary>
        public void StartDefence(FCEvent evt, Action after)
        {
            MilitaryOperationManager manager = FindFC.MilitaryManager;
            if (manager is null)
            {
                LogUtil.Error($"StartDefence: no MilitaryManager available for {WorldSettlement?.Name}.");
                FindFC.FactionComp?.RemoveEvent(evt);
                return;
            }

            // The op — not the warning event — is the source of truth. StartDefense strips the
            // warning event from the queue the instant a battle begins, so by the time the player
            // clicks Defend (or a caravan arrives) the op can be live with no event in the queue,
            // making evt null. Resolve via the manager by tile in that case; only when no live op
            // exists is this a genuine orphaned isUnderAttack flag worth clearing.
            MilitaryOperation op = evt?.linkedOperation ?? manager.GetDefensiveOpAt(WorldSettlement);
            if (op is null)
            {
                LogUtil.Warning($"StartDefence: no live defensive op for {WorldSettlement?.Name}; clearing stuck attack state.");
                FindFC.FactionComp?.RemoveEvent(evt);
                ClearAttackState();
                return;
            }

            // Battle already underway (op engaged, or past it): re-running StartDefense would spawn
            // a duplicate attacker wave via its "add to existing battle" path. Run the caller's
            // follow-up (e.g. an arriving caravan's reinforcement join) and bail instead of
            // restarting.
            if (op.phase != MilitaryOperationPhase.Scheduled
                && op.phase != MilitaryOperationPhase.Traveling)
            {
                after?.Invoke();
                return;
            }

            // Player-initiated defense (Defend gizmo / caravan arrival) jumps the warning-event
            // timer. Without this, the op stays in Scheduled phase and CompleteBattle silently
            // bails at battle end, with no result letter. OnEventFired's auto-trigger path runs the
            // same transition before calling StartDefense.
            op.BeginEngagement();

            BattlefieldContext bf = manager.GetOrCreateBattlefield(WorldSettlement.Tile);
            bf.StartDefense(op, after);
        }


        public void EndBattle(bool won, int remaining, BattleResult battleResult = null)
        {
            var faction = FindFC.FactionComp;

            // Reset before per-op dispatch so the post-flush check below can detect whether the
            // accumulator flush (or an immediate-emit fallback) successfully sent a result letter.
            DefensiveBattleEffects.letterEmitted = false;

            LogUtil.Message("WorldSettlementFC.EndBattle: Handling combat resolution...");

            // Op completion runs first so manager state catches up before any side effect
            // queries it. Each op fires its own LifecycleRegistry.OnBattleResolved and schedules
            // its own cooldown event linked back to itself.
            MilitaryOperationManager manager = FindFC.MilitaryManager;
            if (manager is object)
            {
                var opsAtTile = manager.GetOpsAt(WorldSettlement.Tile);
                if (opsAtTile.Count == 0)
                {
                    LogUtil.Warning($"WorldSettlementFC.EndBattle: no manager ops at tile {WorldSettlement.Tile}; battle resolution dropped.");
                }
                else
                {
                    int defenderInitial = Battlefield?.initialDefenderCount ?? remaining;

                    // Open the condensed-letter accumulator: each op's CompleteBattle ->
                    // MilitaryJobHandler_Defend.ApplyResult appends its outcome fragment instead
                    // of emitting a letter; FlushAccumulator below sends one condensed letter
                    // holding every concurrent attack's result, with one report button per attack.
                    DefensiveBattleEffects.activeAccumulator =
                        new DefenseLetterAccumulator(WorldSettlement, won);
                    try
                    {
                        // Snapshot to avoid enumeration mutation if CompleteBattle unregisters.
                        var snapshot = new List<MilitaryOperation>(opsAtTile);
                        foreach (MilitaryOperation op in snapshot)
                        {
                            if (op is null) continue;
                            if (!op.IsDefensive) continue;
                            if (op.phase == MilitaryOperationPhase.CooldownPending
                                || op.phase == MilitaryOperationPhase.Resolved) continue;

                            // Manual-battle path: each concurrent attack gets its OWN BattleResult
                            // so it archives independently (WorldComponent_Archive.RecordBattleReport
                            // stamps reportId in place — a shared object can't carry N distinct ids)
                            // and its battle report shows that attack's own attacker context
                            // (filled from the op by BattleArchiveUtil). Defender-side / force-count
                            // fields mirror the original synthetic stub, so overwhelming-victory /
                            // crushing-defeat detection is unchanged. The auto-resolve path arrives
                            // with battleResult already populated by the per-round auto-resolve engine.
                            BattleResult resultForOp = battleResult ?? new BattleResult
                            {
                                winner = won ? BattleWinner.Defender : BattleWinner.Attacker,
                                defenderInitialForce = defenderInitial,
                                defenderForceRemaining = remaining,
                                wasManualBattle = true
                            };
                            try { op.CompleteBattle(resultForOp); }
                            catch (Exception innerEx)
                            {
                                LogUtil.Error($"EndBattle: op id={op.id} threw in CompleteBattle: {innerEx}");
                            }
                        }
                    }
                    finally
                    {
                        // Always flush — FlushAccumulator clears activeAccumulator itself, so the
                        // context never leaks into the next battle even if the loop threw.
                        DefensiveBattleEffects.FlushAccumulator();
                    }
                }
            }

            // Settlement-side effects (building destruction, stat changes) run inside
            // op.CompleteBattle via MilitaryJobHandler_Defend.ApplyResult — once per op. Multi-op
            // battles apply one full penalty set per concurrent attacker, treating each op as a
            // logically distinct attack on the settlement. The result LETTER, however, is
            // condensed: every op appends a fragment to DefensiveBattleEffects.activeAccumulator
            // and FlushAccumulator (above) sends a single letter for the whole battle.
            // isUnderAttack is computed from manager state; the op completing already drove it.
            // BattlefieldContext.EndBattle resets battleMapInitialized after this call returns.

            // Every battle resolution should produce a result letter via the accumulator flush
            // (or an immediate-emit fallback). If none did, then the upstream pipeline has a
            // silent-skip bug; log as an error. Earlier log lines ("ignoring re-entry on op id=N
            // in phase X", "no manager ops at tile", etc.) identify which skip point fired.
            if (!DefensiveBattleEffects.letterEmitted)
            {
                LogUtil.Error($"EndBattle: no per-op handler sent a result letter at tile {WorldSettlement?.Tile} (won={won}). ");
            }

            _ = remaining; // parameter retained for source compatibility with external callers.
            _ = faction;
        }

        public void ClearAttackState()
        {
            // Foreign-defender squad-injury bookkeeping already ran inside op.CompleteBattle
            // (which registers both aggressor and defender squads). No need to re-fire it here.
            // isUnderAttack is computed from manager state — battle pawn lists and flags live on
            // BattlefieldContext, so clear them through it.
            BattlefieldContext bf = Battlefield;
            if (bf is object)
            {
                bf.endingBattle = false;
                bf.battleMapInitialized = false;
                bf.shuttleLandingPending = false;
                bf.draftedNPCs?.Clear();
                bf.ClearAllOpPawns();
            }
        }

        public void PostSettlementLoadInit(WorldSettlementFC settlement)
        {
            if (isUnderAttack
                && MilitaryOperationsUtil.ReturnMilitaryEventByLocation(settlement.Tile) is null
                && !attackers.Any() && !defenders.Any())
            {
                // Save taken mid-battle: event was removed from the queue but combatants are still
                // scribed. Leave the battle state alone (EndBattle will clear naturally on resolve).
                LogUtil.Warning($"Repairing stuck isUnderAttack flag on {settlement.Name} during load " +
                    $"(no matching settlementBeingAttacked event).");
                ClearAttackState();
            }

            // Orphan-DefendFriendlySettlement repair: catches deploys whose target was cleaned up
            // without notifying us (any code path that bypasses ClearAttackState's notify hook).
            if (militaryBusy
                && militaryJob == MilitaryJobDefOf.DefendFriendlySettlement
                && IsStaleDeploy())
            {
                LogUtil.Warning($"Clearing orphaned DefendFriendlySettlement on {settlement.Name} during load.");
                ReturnMilitary(false);
            }
        }

        // Shared by load-time and debug-force paths. Caller has already verified
        // militaryJob == DefendFriendlySettlement.
        public bool IsStaleDeploy()
        {
            // Tile-less deploy: SendMilitary sets job + location together, so this is broken state.
            if (militaryLocation == PlanetTile.Invalid) return true;

            // Active warning event for the target; defense is actually in progress.
            if (MilitaryOperationsUtil.ReturnMilitaryEventByLocation(militaryLocation) is object) return false;

            // Target world object is gone (settlement destroyed, outpost despawned, etc.): stale.
            var targetComp = Find.WorldObjects.WorldObjectAt<WorldSettlementFC>(militaryLocation)?.MilitaryComp;
            if (targetComp is null) return true;

            // Target exists, no event, not under attack: stale.
            return !targetComp.isUnderAttack;
        }

        // Battle pawn-list mutations live on BattlefieldContext. These shims exist because lord
        // jobs (LordJob_HuntColonists / LordJob_DefendColony / LordJob_ColonistsIdle) call them
        // by name on the settlement comp; treat them as load-bearing public API.
        public void EndAttack() => Battlefield?.EndAttack();
        public void RemoveAttacker(Pawn downed) => Battlefield?.RemoveAttacker(downed);
        public void RemoveDefender(Pawn defender) => Battlefield?.RemoveDefender(defender);

        public override void PostCaravanFormed(Caravan caravan)
        {
            BattlefieldContext bf = Battlefield;
            foreach (var pawn in caravan.pawns)
            {
                var lord = pawn.GetLord();
                if (lord != null)
                    lord.Notify_PawnLost(pawn, PawnLostCondition.LeftVoluntarily);
                // Routes through bf.RemoveDefender so per-op pawn lists stay in sync.
                bf?.RemoveDefender(pawn);
            }

            if (Map is object)
                foreach (var pawn in caravan.pawns)
                    Map.reservationManager.ReleaseAllClaimedBy(pawn);

            base.PostCaravanFormed(caravan);
        }

        /// <summary>Squad-first entry point. Routes <paramref name="squad"/> through the manager
        /// to launch a handler-driven offensive op.</summary>
        public void SendMilitary(MercenarySquadFC squad, PlanetTile location, MilitaryJobDef job, int timeToFinish, Faction enemy)
        {
            if (squad is null)
            {
                LogUtil.Warning("SendMilitary: null squad parameter; aborting.");
                return;
            }
            /* Morale lockout: refuse the raid before any deployment-cost bill is created. */
            if (squad.settlement is object && squad.settlement.TryGetSquadDeploymentBlock(out string lockReason))
            {
                Messages.Message(lockReason, MessageTypeDefOf.RejectInput);
                return;
            }
            if (IsTargetOccupied(location)) return;

            if (job?.Handler is object)
            {
                MilitaryOperationManager manager = FindFC.MilitaryManager;
                if (manager is null)
                {
                    LogUtil.Error("SendMilitary: MilitaryManager unavailable; aborting offensive op.");
                    return;
                }
                WorldObject target = ResolveTargetWorldObject(location);
                if (target is null)
                {
                    LogUtil.Warning($"SendMilitary: no world object found at tile {location}; aborting.");
                    return;
                }
                FindFC.TaxLedger.CreateDeploymentCostBill(squad);
                manager.CreateOffensiveOp(squad, target, job, enemy, timeToFinish);
                return;
            }
        }

        /// <summary>Compatibility shim that resolves the settlement's primary stationed squad
        /// (via the obsolete <see cref="militarySquad"/> accessor) and forwards. Callers should
        /// pick a specific squad via <see cref="WorldSettlementFC.StationedSquads"/> or the
        /// source-picker dialog.</summary>
        [Obsolete("Pass an explicit MercenarySquadFC squad. Resolves to the primary stationed squad as a fallback.")]
        public void SendMilitary(PlanetTile location, MilitaryJobDef job, int timeToFinish, Faction enemy)
        {
            MercenarySquadFC squad = WorldSettlement?.StationedSquads.FirstOrDefault();
            if (squad is null)
            {
                Messages.Message("FCNoSquadAssigned".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }
            SendMilitary(squad, location, job, timeToFinish, enemy);
        }

        /// <summary>
        /// Look up the WorldObject at <paramref name="tile"/> in priority order: Empire settlement,
        /// any other Settlement (raid target), or any registered <see cref="IRaidTarget"/>'s
        /// world object. Returns null if nothing matches.
        /// </summary>
        private static WorldObject ResolveTargetWorldObject(PlanetTile tile)
        {
            WorldObject target = Find.WorldObjects.WorldObjectAt<WorldSettlementFC>(tile);
            if (target is object) return target;
            target = Find.WorldObjects.SettlementAt(tile);
            if (target is object) return target;
            foreach (IRaidTarget rt in RaidTargetRegistry.Targets)
            {
                if (rt?.WorldObject is object && rt.Tile == tile) return rt.WorldObject;
            }
            return null;
        }

        public Settlement ReturnMilitaryTarget()
        {
            return !militaryLocation.Valid ? null : Find.WorldObjects.SettlementAt(militaryLocation);
        }

        /// <summary>
        /// Registers squad injuries and optionally shows the player a "military cooldown" letter.
        /// Called by debug actions, settlement-removal sweep, and load-time stale-deploy repair.
        /// Most release / cooldown work now lives on <see cref="MilitaryOperation"/> (Resolve
        /// fires lifecycle hooks and unregisters); this method only handles the residual squad-
        /// injury bookkeeping.
        /// </summary>
        public void ReturnMilitary(bool alert)
        {
            if (!militaryBusy) return; // No active op — nothing to do

            // Register injuries across every stationed squad so injured pawns get healed.
            MilitaryFC mfc = FindFC.Military;
            if (mfc != null)
            {
                List<MercenarySquadFC> stationed = WorldSettlement?.StationedSquads;
                if (stationed != null)
                {
                    for (int i = 0; i < stationed.Count; i++)
                    {
                        if (stationed[i] != null) mfc.RegisterSquadInjuries(stationed[i]);
                    }
                }
            }

            if (alert)
            {
                Find.LetterStack.ReceiveLetter("Military Cooldown", "FCMilitaryCooldown".Translate(WorldSettlement.Name),
                    LetterDefOf.PositiveEvent);
            }
        }

        public bool IsMilitaryBusy(bool silent = false)
        {
            if (militaryBusy && !silent)
            {
                Messages.Message("FCMilitaryAlreadyAssigned".Translate(), MessageTypeDefOf.RejectInput);
            }

            return militaryBusy;
        }

        public bool IsMilitarySquadValid()
        {
            List<MercenarySquadFC> stationed = WorldSettlement?.StationedSquads;
            if (stationed is null || stationed.Count == 0)
            {
                Messages.Message("FCNoSquadAssigned".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }

            // Valid if any stationed squad has an outfit and equipped mercs ready to deploy.
            bool anyHasOutfit = false;
            for (int i = 0; i < stationed.Count; i++)
            {
                MercenarySquadFC s = stationed[i];
                if (s is null) continue;
                s.CheckInitialization();
                if (s.outfit is null) continue;
                anyHasOutfit = true;
                if (s.EquippedMercenaries.Any()) return true;
            }

            Messages.Message((anyHasOutfit ? "FCNoSquadEquipped" : "FCNoSquadLoadoutAssigned").Translate(),
                MessageTypeDefOf.RejectInput);
            return false;
        }

        public bool IsMilitaryValid()
        {
            return settlementMilitaryLevel > 0;
        }

        public bool IsTargetOccupied(PlanetTile location)
        {
            // Single source of truth for launch eligibility: an existing op or a live map at the
            // tile (player there in person) both block a launch. CanLaunchOffensiveAt supplies the
            // keyed reject reason.
            MilitaryOperationManager manager = FindFC.MilitaryManager;
            if (manager is null) return false;
            if (manager.CanLaunchOffensiveAt(location, out string rejectReason)) return false;
            Messages.Message(rejectReason, MessageTypeDefOf.RejectInput);
            return true;
        }
    }
}
