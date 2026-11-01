using FactionColonies.util;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace FactionColonies
{
    public class LordJob_DeployMilitary : LordJob
    {
        /// <summary>Default ticks the lord stays before force-leaving. Mirrored on
        /// <see cref="MilitaryOperation.nextPhaseTick"/> by <c>CreateDeployOp</c> so the busy
        /// timer reflects the actual deploy horizon.</summary>
        public const int DefaultMaxDeploymentTime = 30000;

        public MercenarySquadFC squad;
        private IntVec3 currentOrderPosition;
        private int whenToForceLeave;
        private int timeDeployed = 0;
        private bool readyForCommands = false;

        // Rider -> mount animal for mercs with an assigned mount (Giddy Up 2). Applied once the deployed
        // pawns have spawned (ApplyMountsIfNeeded) and re-applied on order changes (RestartPawnJobs) so a
        // dismount caused by a player move/attack order is corrected. Empty without Giddy Up 2.
        private Dictionary<Pawn, Pawn> mounts;
        private List<Pawn> mountsKeys = new List<Pawn>();
        private List<Pawn> mountsValues = new List<Pawn>();
        private bool mountsApplied;

        private LordToil_DefendPoint lordToil_DefendPoint;
        private LordToil_HuntEnemies lordToil_HuntEnemies;
        private Map currentMap;
        private bool finalized;

        // Tick the current RecoverWoundedAndLeave order was first seen, or -1 when not leaving.
        // Drives the prompt force-finalize in LordJobTick so a stuck sub-pawn (e.g. an orphaned
        // companion animal whose handler merc died) can't hold the lord — and thus the command
        // window — open until the long backstop fires.
        private int leaveOrderStartTick = -1;

        /// <summary>
        /// Default constructor, meant to only be used when creating the job object during loading
        /// </summary>
        public LordJob_DeployMilitary()
        {
        }

        /// <summary>
        /// Creates a new <c>LordJob</c> that controls any pawn during military deployment.
        /// <paramref name="currentOrderPosition"/> is the initial deployment position of the deployed pawns
        /// <paramref name="squad"/> is the squad that contains the pawns deployed
        /// <paramref name="maxDeploymentTime"/> is the time a squad has before being forced into the leave command. It is set at 30000 by default
        /// </summary>
        /// <param name="currentOrderPosition"></param>
        /// <param name="squad"></param>
        /// <param name="maxDeploymentTime"></param>
        public LordJob_DeployMilitary(IntVec3 currentOrderPosition, MercenarySquadFC squad, Dictionary<Pawn, Pawn> mounts = null, int maxDeploymentTime = DefaultMaxDeploymentTime)
        {
            this.currentOrderPosition = currentOrderPosition;
            this.squad = squad;
            this.mounts = mounts;

            whenToForceLeave = maxDeploymentTime + Find.TickManager.TicksGame;
            timeDeployed = Find.TickManager.TicksGame;
            currentMap = squad.Deployment.Map;

            Init();
        }

        /// <summary>
        /// Initializes some variables after loading is complete or when the deployment is first started.
        /// Ensures the deployment command menu window is open; the menu reads/writes
        /// <see cref="SquadDeploymentState.MilitaryOrder"/> / <see cref="SquadDeploymentState.OrderLocation"/>
        /// directly, so this lord doesn't keep a reference to it.
        /// </summary>
        private void Init()
        {
            if (!Find.WindowStack.IsOpen(typeof(DeployedMilitaryCommandMenu)))
                Find.WindowStack.Add(new DeployedMilitaryCommandMenu());

            lordToil_DefendPoint = new LordToil_DefendPoint(currentOrderPosition);
            lordToil_HuntEnemies = new LordToil_HuntEnemies(currentOrderPosition);
        }

        internal bool ReadyForCommands
        {
            get
            {
                if (readyForCommands) return true;

                if (Find.TickManager.TicksGame - timeDeployed > 300)
                {
                    readyForCommands = true;
                    return true;
                }

                if (lord.ownedPawns.All(pawn => pawn.Spawned))
                {
                    readyForCommands = true;
                    return true;
                }

                return false;
            }
        }

        /// <summary>Hard grace period after <c>whenToForceLeave</c> (~4 in-game hours).
        /// If pawns are still in the lord after this, force-finalize.</summary>
        public const int PostLeaveGraceTicks = 10000;

        /// <summary>Short grace after a RecoverWoundedAndLeave order before force-finalizing, so pawns
        /// that CAN path off still leave naturally (~1 in-game hour). Far shorter than the
        /// <see cref="whenToForceLeave"/>+<see cref="PostLeaveGraceTicks"/> backstop.</summary>
        public const int LeaveOrderForceFinalizeTicks = 2500;

        public override void LordJobTick()
        {
            base.LordJobTick();
            ApplyMountsIfNeeded();
            if (finalized) return;

            // Under a leave order, finalize promptly so a single stuck pawn can't wedge the lord
            // (and the command window) open. Finalize once every top-level merc has physically left
            // the map, OR after a short grace (covers the case where a merc itself is the straggler).
            // FinalizeDeployment DeSpawns whatever remains (alive/redeployable) and completes the op.
            bool leaving = squad is object
                           && squad.Deployment.MilitaryOrder == MilitaryOrder.RecoverWoundedAndLeave;
            if (leaving)
            {
                if (leaveOrderStartTick < 0) leaveOrderStartTick = Find.TickManager.TicksGame;

                bool allMercsGone = !squad.DeployedMercenaries.Any();
                bool graceElapsed = Find.TickManager.TicksGame - leaveOrderStartTick > LeaveOrderForceFinalizeTicks;
                if (allMercsGone || graceElapsed)
                {
                    FinalizeDeployment();
                    return;
                }
            }
            else
            {
                // Leave -> Move/Attack resets the grace; it re-seeds on the next leave order.
                leaveOrderStartTick = -1;
            }

            // Long backstop: pawns still in the lord well past the force-leave horizon.
            if (Find.TickManager.TicksGame > whenToForceLeave + PostLeaveGraceTicks
                && lord.ownedPawns.Count > 0)
            {
                FinalizeDeployment();
            }
        }

        /// <summary>Mounts each rider on its mount once the deployed pawns are on the map (drop pods open
        /// a few ticks after the lord forms). Runs once; player move/attack orders that dismount a rider
        /// are corrected in <see cref="RestartPawnJobs"/>. No-op without mounts or Giddy Up 2.</summary>
        private void ApplyMountsIfNeeded()
        {
            if (!FactionCompat.GiddyUp2Active || mountsApplied || mounts is null || mounts.Count == 0)
                return;
            // ReadyForCommands is true once all pawns have spawned (or after the 300-tick grace).
            if (!ReadyForCommands) return;

            foreach (KeyValuePair<Pawn, Pawn> pair in mounts)
            {
                if (pair.Key is object && pair.Value is object && pair.Key.Spawned && pair.Value.Spawned)
                    GiddyUpUtil.Mount(pair.Key, pair.Value);
            }
            mountsApplied = true;
        }

        public override void Notify_AddedToLord()
        {
            base.Notify_AddedToLord();
            if (squad is object)
            {
                squad.Deployment.Lord = lord;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref currentOrderPosition, "currentOrderPosition");
            Scribe_Values.Look(ref timeDeployed, "timeDeployed");
            Scribe_Values.Look(ref whenToForceLeave, "whenToForceLeave");
            // currentOrder used to be persisted here. It moved to SquadDeploymentState.MilitaryOrder.
            // Old-save value is silently ignored on load.
            Scribe_References.Look(ref squad, "squad");
            Scribe_References.Look(ref currentMap, "currentMap");
            Scribe_Values.Look(ref finalized, "finalized");
            Scribe_Values.Look(ref leaveOrderStartTick, "leaveOrderStartTick", -1);
            Scribe_Values.Look(ref mountsApplied, "mountsApplied", false);
            Scribe_Collections.Look(ref mounts, "mounts", LookMode.Reference, LookMode.Reference, ref mountsKeys, ref mountsValues);

            //PostLoadInit is the last loading pass
            if (Scribe.mode == LoadSaveMode.PostLoadInit) Init();
        }

        /// <summary>
        /// Grabs a new currentOrderPosition from <see cref="SquadDeploymentState.OrderLocation"/>
        /// and pushes it into the toils' data. Runs as a preAction so the data is fresh before
        /// <c>GotoToil</c> calls <c>UpdateAllDuties</c> on the target toil.
        /// </summary>
        private void UpdateOrderPositionData()
        {
            if (squad is null) return;
            IntVec3 newPos = squad.Deployment.OrderLocation;
            currentOrderPosition = newPos;

            lordToil_DefendPoint.SetDefendPoint(newPos);
            ((LordToilData_HuntEnemies)lordToil_HuntEnemies.data).fallbackLocation = newPos;
        }

        /// <summary>
        /// Force pawns to drop their current job so the new duty takes effect this tick instead of after the current job finishes.
        /// Runs as a postAction (after <c>GotoToil</c> has installed the target toil's duty) so the pawns' next job is picked
        /// against the new duty, not against the soon-to-be-discarded source duty. This matters for Leave->Move/Attack: the
        /// leave duty's <c>JobGiver_ExitMapBest</c> hands out a Goto with <c>expiryInterval = 500</c> that won't re-evaluate
        /// for ~8 seconds, so any job-end before the duty swap leaves the pawn locked into walking off the map.
        ///
        /// Defend's think tree ends in JobGiver_WanderNearDutyLocation, which alternates GotoWander/Wait_Wander via
        /// <c>nextMoveOrderIsWait</c>. Without resetting it, ~50% of interrupts land on the "wait" half of the toggle and
        /// queue a 125-200 tick Wait_Wander before any movement, visible as a 1-2s freeze before the squad heads to the new point.
        /// </summary>
        private void RestartPawnJobs()
        {
            foreach (Pawn p in lord.ownedPawns)
            {
                if (p is null) continue;
                if (p.mindState is object) p.mindState.nextMoveOrderIsWait = false;
                if (p.jobs?.curJob is object) p.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }

            // Re-mount any rider the order change dismounted (GiddyUpUtil.Mount is idempotent, so
            // already-mounted riders are untouched).
            if (mounts is object)
            {
                foreach (KeyValuePair<Pawn, Pawn> pair in mounts)
                {
                    if (pair.Key is object && pair.Value is object && pair.Key.Spawned && pair.Value.Spawned)
                        GiddyUpUtil.Mount(pair.Key, pair.Value);
                }
            }
        }

        /// <summary>
        /// Creates a list of transitions to allow for leaving from any state when time runs out
        /// </summary>
        /// <param name="stateGraph"></param>
        /// <returns>the Transitions</returns>
        private IEnumerable<Transition> AnyThingToLeavingTransitions(StateGraph stateGraph)
        {
            for (int i = 0; i < stateGraph.lordToils.Count - 1; i++)
            {
                yield return new Transition(stateGraph.lordToils[i], stateGraph.lordToils.Last())
                {
                    triggers = new List<Trigger>(1) { new Trigger_Custom((TriggerSignal _) => Find.TickManager.TicksGame > whenToForceLeave) },
                    preActions = new List<TransitionAction>(1)
                    {
                        new TransitionAction_Custom(delegate()
                        {
                            if (squad is object) squad.Deployment.MilitaryOrder = MilitaryOrder.RecoverWoundedAndLeave;
                            Messages.Message("FCMilitaryPawnsLeavingTimeOut".Translate(), lord.ownedPawns, MessageTypeDefOf.NeutralEvent);
                        })
                    }
                };
            }
        }

        /// <summary>
        /// This generates all transitions needed for the control window to work
        /// </summary>
        /// <param name="stateGraph"></param>
        /// <returns>the Transitions</returns>
        private IEnumerable<Transition> AnyPlayerChoiceTransition(StateGraph stateGraph)
        {
            for (int i = 0; i < stateGraph.lordToils.Count; i++)
            {
                for (int j = 0; j < stateGraph.lordToils.Count; j++)
                {
                    if (i == j) continue;

                    //save j as another variable, otherwise j refers to the same number as the one the loop uses, which in the end is always Count
                    int k = j;
                    yield return new Transition(stateGraph.lordToils[i], stateGraph.lordToils[j])
                    {
                        triggers = new List<Trigger>(1)
                        {
                            new Trigger_Custom((TriggerSignal _) => squad is object && squad.Deployment.MilitaryOrder == (MilitaryOrder)k + 1 && ReadyForCommands)
                        },
                        preActions = new List<TransitionAction>(1)
                        {
                            new TransitionAction_Custom(UpdateOrderPositionData)
                        },
                        postActions = new List<TransitionAction>(1)
                        {
                            new TransitionAction_Custom(RestartPawnJobs)
                        }
                    };
                }
            }
        }

        /// <summary>
        /// If the player selects a new position to move to, the currentOrderPosition must be updated, and the jobs restarted.
        /// This <c>Transition</c> ensures that.
        /// </summary>
        /// <param name="stateGraph"></param>
        /// <returns></returns>
        private Transition RefreshMovementTransition(StateGraph stateGraph)
        {
            return new Transition(stateGraph.lordToils[0], stateGraph.lordToils[0], true)
            {
                triggers = new List<Trigger>(1)
                {
                    new Trigger_Custom((TriggerSignal _) =>
                    {
                        if (squad is null) return false;
                        MilitaryOperation op = squad.Operation;
                        return op is object
                            && op.kind == MilitaryJobDefOf.Deploy
                            && op.phase == MilitaryOperationPhase.Engaged
                            && squad.Deployment.OrderLocation != currentOrderPosition;
                    })
                },
                preActions = new List<TransitionAction>(1)
                {
                    new TransitionAction_Custom(UpdateOrderPositionData)
                },
                postActions = new List<TransitionAction>(1)
                {
                    new TransitionAction_Custom(RestartPawnJobs)
                }
            };
        }

        /// <summary>
        /// The Order of toils in the StateGraph MUST be the same as in the MilitaryOrder enum
        /// </summary>
        /// <returns>a StateGraph</returns>
        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph { StartingToil = lordToil_DefendPoint };

            stateGraph.AddToil(lordToil_HuntEnemies);
            stateGraph.AddToil(new LordToil_RecoverWoundedAndLeave(new LordToilData_ExitMap() { canDig = false, locomotion = LocomotionUrgency.Jog, interruptCurrentJob = true }));

            stateGraph.AddTransition(RefreshMovementTransition(stateGraph));
            stateGraph.AddTransitions(AnyPlayerChoiceTransition(stateGraph));
            stateGraph.AddTransitions(AnyThingToLeavingTransitions(stateGraph));

            return stateGraph;
        }

        /// <summary>
        /// Idempotent finalization: completes the deploy op (which schedules the squad's cooldown
        /// event and fires lifecycle hooks), and despawns any orphaned squad pawns still on the
        /// map (e.g. downed mercs).
        /// </summary>
        private void FinalizeDeployment()
        {
            if (finalized) return;
            finalized = true;

            if (squad is object)
            {
                // Clear the player-issued order so it can't carry into the next deployment.
                // MilitaryOrder is persistent squad state; a stale RecoverWoundedAndLeave would
                // otherwise make the next deploy's lord leave immediately.
                squad.Deployment.MilitaryOrder = MilitaryOrder.Undefined;

                // Find the deploy op and complete it. This schedules the cooldown event linked
                // to the op (which on fire transitions the op to Resolved and unregisters it),
                // and fires LifecycleRegistry.OnBattleResolved -> OnOperationResolved.
                MilitaryOperation op = squad.Operation;
                if (op is object && op.kind == MilitaryJobDefOf.Deploy
                    && op.phase == MilitaryOperationPhase.Engaged)
                {
                    // Deploy isn't a battle — synthesize a result so CompleteBattle's
                    // victory-flag computation has something to read. CompleteBattle now also
                    // registers squad injuries internally, so the explicit call below is gone.
                    op.CompleteBattle(new BattleResult { winner = BattleWinner.Defender });
                }

                // Despawn orphaned downed/stuck mercs still on the map
                if (currentMap is object)
                {
                    foreach (Mercenary merc in squad.mercenaries.Concat(squad.AllSubPawns()))
                    {
                        if (merc?.pawn is object && merc.pawn.Spawned && merc.pawn.Map == currentMap)
                            merc.pawn.DeSpawn();
                    }
                }
            }
        }

        public override void Notify_PawnLost(Pawn pawn, PawnLostCondition condition)
        {
            base.Notify_PawnLost(pawn, condition);
            if (condition == PawnLostCondition.Killed && squad is object)
                squad.dead++;
        }

        public override void Notify_LordDestroyed()
        {
            FinalizeDeployment();
            base.Notify_LordDestroyed();
        }

        public override void Cleanup()
        {
            base.Cleanup();
            FinalizeDeployment();
        }
    }
}
