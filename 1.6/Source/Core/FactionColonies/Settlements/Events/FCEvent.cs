using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    public class FCEvent : IExposable, ILoadReferenceable
    {
        public FCEventDef def = new FCEventDef();
        public PlanetTile location = -1;
        public int timeTillTrigger = -1;
        public int timeMinTrigger = -1;
        public int timeMaxTrigger = -1;
        public int tickStarted = -1;
        public int loadID = -1;
        public PlanetTile source = -1;
        public bool hasDestination;
        public int buildingSlot = -1;
        public BuildingFCDef building;
        public List<WorldSettlementFC> settlementTraitLocations = new List<WorldSettlementFC>();
        /* Frozen cohort of settlements this event actually applied its stat/permanent
         * modifiers to (recorded at Apply time). Removal reverses exactly this set so a
         * settlement founded mid-event is neither buffed nor penalized. */
        public List<WorldSettlementFC> appliedStatSettlements;
        /* Per-option scaled silver cost, keyed by FCOptionDef.defName. Null until the options
         * window first opens; populated by FCOptionWindow so the price shown is the price paid
         * and survives save/reload. Recomputes lazily for legacy events that never snapshotted. */
        public Dictionary<string, int> optionCostSnapshots;
        public List<Thing> goods = new List<Thing>();
        public bool hasCustomDescription;
        public string customDescription = "";
        public bool hasCustomLabel;
        public string customLabel = "";

        /* User-facing event label. Falls back to the def's label when no per-event
         * override was set. Centralized so display call sites stay in lockstep. */
        public string Label =>
            hasCustomLabel && !customLabel.NullOrEmpty() ? customLabel : def?.label;

        //Delivery things
        public Message msg = null;
        public Letter let = null;
        public bool isDelayed = false;
        public TaxDeliveryMode deliveryMode;

        //Military Force stuff — drained into MilitaryOperation by MilitaryMigrationUtil on
        // PostLoadInit. Kept declared (and scribed) so pre-refactor saves load cleanly; new
        // code reads/writes the corresponding MilitaryOperationParticipant fields instead.
        [Obsolete("Drained into MilitaryOperation by MilitaryMigrationUtil. Will be removed in a future version.")]
        public MilitaryForce militaryForceAttacking;
        [Obsolete("Drained into MilitaryOperation by MilitaryMigrationUtil. Will be removed in a future version.")]
        public Faction militaryForceAttackingFaction;
        [Obsolete("Drained into MilitaryOperation by MilitaryMigrationUtil. Will be removed in a future version.")]
        public MilitaryForce militaryForceDefending;
        [Obsolete("Drained into MilitaryOperation by MilitaryMigrationUtil. Will be removed in a future version.")]
        public Faction militaryForceDefendingFaction;
        [Obsolete("Drained into MilitaryOperation by MilitaryMigrationUtil. Will be removed in a future version.")]
        public WorldObject settlementFCDefending;
        /// <summary>
        /// If the defending force was provided by an external <see cref="IAutoDefender"/> (not an Empire settlement),
        /// this references the defender's world object so it can be notified on battle completion.
        /// </summary>
        [Obsolete("Drained into MilitaryOperation.externalDefenderSource by MilitaryMigrationUtil. Will be removed in a future version.")]
        public WorldObject externalDefenderSource;

        public WorldSettlementDef settlementToCreate = null;

        /// <summary>
        /// Back-reference to a <see cref="MilitaryOperation"/> in the
        /// <see cref="FactionFC.militaryOperationManager"/> when this event is a wakeup primitive
        /// for that op (arrival, cooldown, warning, etc.). Null when unlinked. <see cref="FCEventMaker"/>
        /// dispatches op-linked events through <see cref="MilitaryOperation.OnEventFired"/>.
        /// </summary>
        public MilitaryOperation linkedOperation;

        public bool HasLinkedOperation => linkedOperation is object;

        /// <summary>Lifecycle phase.
        /// <para> - Queued: in queue, awaiting timer.</para>
        /// <para> - Fired: tentative, mid-processing only. Should never persist past a single ProcessEvents pass.</para>
        /// <para> - Resolving: handler-driven persistence. Set explicitly to keep the event alive past its fire.</para>
        /// <para> - Completed: done, awaiting sweep from the queue.</para>
        /// Default is Queued.</summary>
        public FCEventPhase phase = FCEventPhase.Queued;

        public bool IsQueued => phase == FCEventPhase.Queued;
        public bool IsFired => phase == FCEventPhase.Fired;
        public bool IsResolving => phase == FCEventPhase.Resolving;
        public bool IsCompleted => phase == FCEventPhase.Completed;

        public bool HasVariableDuration => timeMinTrigger > 0 && timeMaxTrigger > 0;

        public float Progress
        {
            get
            {
                if (tickStarted < 0) return 1f;
                int endpoint = HasVariableDuration ? timeMinTrigger : timeTillTrigger;
                if (endpoint <= tickStarted) return 1f;
                int now = Find.TickManager.TicksGame;
                if (now >= endpoint) return 1f;
                return (float)(now - tickStarted) / (endpoint - tickStarted);
            }
        }

        public FCEvent()
        {
            //Constructor
        }

        public FCEvent(bool New)
        {
            loadID = FindFC.EventManager.NextEventId();
        }

        /// <summary>
        /// Defines parameters of event with custom description
        /// </summary>
        /// <param name="f">FactionFC object</param>
        /// <param name="mapLocation">Location of the event object</param>
        /// <param name="timeToFinish">Time of event's completion</param>
        public void DefineEvent(FactionFC f, PlanetTile mapLocation, int timeToFinish)
        {
            this.hasCustomDescription = true;
            this.tickStarted = Find.TickManager.TicksGame;
            this.timeTillTrigger = Find.TickManager.TicksGame + timeToFinish;
            this.location = mapLocation;
            f.eventManager.AddEvent(this);
        }

        public void ExposeData()
        {
            //Ref
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref location, "location");
            Scribe_Values.Look(ref timeTillTrigger, "timeTillTrigger");
            Scribe_Values.Look(ref tickStarted, "tickStarted", -1);
            Scribe_Values.Look(ref timeMinTrigger, "timeMinTrigger", -1);
            Scribe_Values.Look(ref timeMaxTrigger, "timeMaxTrigger", -1);
            Scribe_Values.Look(ref source, "source");
            Scribe_Values.Look(ref hasDestination, "hasDestination");
            Scribe_Collections.Look(ref settlementTraitLocations, "settlementTraitLocations", LookMode.Reference);
            Scribe_Collections.Look(ref appliedStatSettlements, "appliedStatSettlements", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                appliedStatSettlements?.RemoveAll(s => s == null);
            Scribe_Collections.Look(ref optionCostSnapshots, "optionCostSnapshots", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref goods, "goods", LookMode.Deep);
            Scribe_Values.Look(ref loadID, "loadID");

            Scribe_Values.Look(ref buildingSlot, "buildingSlot");

            Scribe_Defs.Look(ref building, "building");


            Scribe_Values.Look(ref hasCustomDescription, "hasCustomDescription");
            Scribe_Values.Look(ref customDescription, "customDescription");
            Scribe_Values.Look(ref hasCustomLabel, "hasCustomLabel");
            Scribe_Values.Look(ref customLabel, "customLabel");

            Scribe_Deep.Look(ref msg, "msg");
            Scribe_Deep.Look(ref let, "let");
            Scribe_Values.Look(ref isDelayed, "isDelayed", false);
            Scribe_Values.Look(ref deliveryMode, "deliveryMode");
            Scribe_Values.Look(ref phase, "phase", FCEventPhase.Queued);

            //Military stuff — legacy save-format fields drained by MilitaryMigrationUtil.
#pragma warning disable 0618
            Scribe_Deep.Look(ref militaryForceAttacking, "militaryForceAttacking");
            Scribe_References.Look(ref militaryForceAttackingFaction, "militaryForceAttackingFaction");
            Scribe_Deep.Look(ref militaryForceDefending, "militaryForceDefending");
            Scribe_References.Look(ref militaryForceDefendingFaction, "militaryForceDefendingFaction");
            Scribe_References.Look(ref settlementFCDefending, "SettlementFCDefending");
            Scribe_References.Look(ref externalDefenderSource, "externalDefenderSource");
#pragma warning restore 0618

            Scribe_Defs.Look(ref settlementToCreate, "settlementToCreate");

            Scribe_References.Look(ref linkedOperation, "linkedOperation");
        }

        public string GetUniqueLoadID()
        {
            return "FCEvent_" + loadID;
        }

        /// <summary>
        /// Runs the event's mod-extension handler. Called by ProcessEvents after re-entrancy
        /// guards. Handlers that want the event to persist past this fire (e.g. ongoing battle,
        /// multi-step delivery) set <c>phase = FCEventPhase.Resolving</c> during their run;
        /// otherwise the event auto-transitions to Completed after ProcessEvents finishes.
        /// </summary>
        public void RunAction()
        {
            try
            {
                def?.GetModExtension<FCEventHandlerExtension>()?.OnEventTriggered(this);
            }
            catch (Exception e)
            {
                LogUtil.Error($"FCEvent.RunAction: OnEventTriggered threw for '{def?.defName ?? "NULL"}': {e}");
            }
        }

        /// <summary>
        /// Merges goods into properly-sized stacks respecting each ThingDef's stackLimit.
        /// </summary>
        public static List<Thing> ConsolidateGoods(List<Thing> goods)
        {
            List<Thing> consolidated = new List<Thing>();

            foreach (Thing thing in goods)
            {
                if (thing.stackCount <= 0) continue;

                bool merged = false;
                for (int i = 0; i < consolidated.Count; i++)
                {
                    Thing existing = consolidated[i];
                    if (existing.CanStackWith(thing) && existing.stackCount < existing.def.stackLimit)
                    {
                        existing.TryAbsorbStack(thing, true);
                        if (thing.stackCount <= 0 || thing.Destroyed)
                        {
                            merged = true;
                            break;
                        }
                    }
                }

                if (!merged && thing.stackCount > 0 && !thing.Destroyed)
                {
                    consolidated.Add(thing);
                }
            }

            // Split any over-limit stacks that resulted from absorption
            List<Thing> result = new List<Thing>();
            foreach (Thing thing in consolidated)
            {
                // This while *shouldn't* loop infinitely, but just in case, we'll add a break-out case
                int i = 0;
                const int LOOP_LIMIT = 1000;
                while (thing.stackCount > thing.def.stackLimit && i < LOOP_LIMIT)
                {
                    result.Add(thing.SplitOff(thing.def.stackLimit));
                    i++;
                }
                if (i == LOOP_LIMIT)
                {
                    LogUtil.Error($"ConsolidateGoods: reached LOOP_LIMIT iterations when splitting stack of {thing.Label}");
                }

                if (thing.stackCount > 0)
                {
                    result.Add(thing);
                }
            }

            return result;
        }
    }
}