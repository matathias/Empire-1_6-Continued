using FactionColonies.util;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    // Owns the faction's event queue and auxiliary cooldown / fire-count / id-counter
    // bookkeeping. The only code that mutates the queue lives here; everywhere else
    // reads via the IReadOnlyList facade (exposed through FactionFC.Events) or calls
    // one of the methods below.
    //
    // AddEvent is the public entry point for queueing an event. It owns goods
    // consolidation, the TaxDeliveryRegistry interception, the raw queue append,
    // and the FCEventHandlerExtension.OnEventQueued dispatch (whose default impl
    // applies stat modifiers to settlements). Symmetrically, Remove / RemoveWhere
    // dispatch OnEventExpired, which by default removes the stat modifiers and
    // applies prosperityLost.
    //
    // Maintains two in-memory indexes for O(1) event lookups:
    //   defIndex         — groups events by FCEventDef
    //   defLocationIndex — groups events by (FCEventDef, tile) compound key
    // Both are rebuilt on load and maintained automatically by all mutation methods.
    public class FCEventManager : IExposable
    {
        private List<FCEvent> events = new List<FCEvent>();
        private Dictionary<FCEventDef, int> eventCooldowns = new Dictionary<FCEventDef, int>();
        private Dictionary<FCEventDef, int> eventFireCounts = new Dictionary<FCEventDef, int>();
        private int version;
        private int nextEventId = 1;

        /* Indexes (not serialized — rebuilt on load) */
        private Dictionary<FCEventDef, List<FCEvent>> defIndex = new Dictionary<FCEventDef, List<FCEvent>>();
        private Dictionary<DefTileKey, List<FCEvent>> defLocationIndex = new Dictionary<DefTileKey, List<FCEvent>>();

        private static readonly IReadOnlyList<FCEvent> EmptyEventList = new List<FCEvent>();

        public IReadOnlyList<FCEvent> Events => events;
        public int Version => version;
        public int Count => events.Count;

        /* Index maintenance */
        private void IndexAdd(FCEvent evt)
        {
            if (evt?.def is null) return;

            if (!defIndex.TryGetValue(evt.def, out List<FCEvent> defList))
            {
                defList = new List<FCEvent>();
                defIndex[evt.def] = defList;
            }
            defList.Add(evt);

            var key = new DefTileKey(evt.def, evt.location);
            if (!defLocationIndex.TryGetValue(key, out List<FCEvent> locList))
            {
                locList = new List<FCEvent>();
                defLocationIndex[key] = locList;
            }
            locList.Add(evt);
        }

        private void IndexRemove(FCEvent evt)
        {
            if (evt?.def is null) return;

            if (defIndex.TryGetValue(evt.def, out List<FCEvent> defList))
            {
                defList.Remove(evt);
                if (defList.Count == 0) defIndex.Remove(evt.def);
            }

            var key = new DefTileKey(evt.def, evt.location);
            if (defLocationIndex.TryGetValue(key, out List<FCEvent> locList))
            {
                locList.Remove(evt);
                if (locList.Count == 0) defLocationIndex.Remove(key);
            }
        }

        private void IndexClear()
        {
            defIndex.Clear();
            defLocationIndex.Clear();
        }

        private void IndexRebuild()
        {
            IndexClear();
            foreach (FCEvent evt in events)
                IndexAdd(evt);
        }

        /* Indexed query methods */
        /// <summary>All events with the given def. Returns empty list if none.</summary>
        public IReadOnlyList<FCEvent> GetByDef(FCEventDef def)
        {
            if (def is null) return EmptyEventList;
            return defIndex.TryGetValue(def, out List<FCEvent> list) ? list : EmptyEventList;
        }

        /// <summary>True if any event with the given def exists in the queue.</summary>
        public bool AnyWithDef(FCEventDef def)
        {
            return def is object && defIndex.TryGetValue(def, out List<FCEvent> list) && list.Count > 0;
        }

        /// <summary>All events with the given def at the given tile. Returns empty list if none.</summary>
        public IReadOnlyList<FCEvent> GetByDefAndLocation(FCEventDef def, PlanetTile tile)
        {
            if (def is null) return EmptyEventList;
            var key = new DefTileKey(def, tile);
            return defLocationIndex.TryGetValue(key, out List<FCEvent> list) ? list : EmptyEventList;
        }

        /// <summary>First event matching (def, tile), or null if none.</summary>
        public FCEvent FindFirstByDefAndLocation(FCEventDef def, PlanetTile tile)
        {
            if (def is null) return null;
            var key = new DefTileKey(def, tile);
            if (!defLocationIndex.TryGetValue(key, out List<FCEvent> list) || list.Count == 0) return null;
            return list[0];
        }

        /// <summary>True if any event with the given (def, tile) exists.</summary>
        public bool AnyWithDefAndLocation(FCEventDef def, PlanetTile tile)
        {
            if (def is null) return false;
            var key = new DefTileKey(def, tile);
            return defLocationIndex.TryGetValue(key, out List<FCEvent> list) && list.Count > 0;
        }

        /* Mutation methods */

        /// <summary>Public entry point for adding an event to the queue. Owns goods
        /// consolidation, tax-delivery interception, the raw queue append, and the
        /// FCEventHandlerExtension OnEventQueued dispatch (whose default impl applies
        /// stat modifiers to settlements).</summary>
        public void AddEvent(FCEvent evt)
        {
            if (evt is null) return;

            if (evt.goods != null && evt.goods.Count > 0)
                evt.goods = FCEvent.ConsolidateGoods(evt.goods);

            FactionFC faction = FindFC.FactionComp;

            /* Tax delivery interception: registered interceptors may reroute taxColony events. */
            if (evt.def == FCEventDefOf.taxColony && evt.source != PlanetTile.Invalid)
            {
                WorldSettlementFC sourceSettlement = faction.ReturnSettlementByLocation(evt.source);
                TaxDeliveryRegistry.InvokeOnTaxEventCreated(new TaxDeliveryContext(evt, sourceSettlement));
            }

            EnqueueInternal(evt);

            /* Post-enqueue lifecycle hook (default: applies stat + permanent modifiers). */
            FCEventHandlerExtension ext = evt.def?.GetModExtension<FCEventHandlerExtension>();
            if (ext != null) ext.OnEventQueued(evt, faction);
            else EventStatModifierApplier.Apply(evt, faction);
        }

        /* Raw append. Used by AddEvent and SeedFromLegacy; intentionally has no
         * cascade side effects — load-path stat-modifier reapply lives in
         * WorldSettlementFC.PostLoadInit. */
        private void EnqueueInternal(FCEvent evt)
        {
            events.Add(evt);
            IndexAdd(evt);
            version++;
        }

        public bool Remove(FCEvent evt)
        {
            if (evt is null) return false;
            if (!events.Remove(evt)) return false;
            IndexRemove(evt);
            DispatchOnEventExpired(evt);
            evt.phase = FCEventPhase.Completed;
            version++;
            return true;
        }

        public int RemoveWhere(Predicate<FCEvent> match)
        {
            if (match is null) return 0;
            int removed = 0;
            for (int i = events.Count - 1; i >= 0; i--)
            {
                if (!match(events[i])) continue;
                FCEvent evt = events[i];
                IndexRemove(evt);
                events.RemoveAt(i);
                DispatchOnEventExpired(evt);
                evt.phase = FCEventPhase.Completed;
                removed++;
            }
            if (removed > 0) version++;
            return removed;
        }

        private static void DispatchOnEventExpired(FCEvent evt)
        {
            FactionFC faction = FindFC.FactionComp;
            FCEventHandlerExtension ext = evt.def?.GetModExtension<FCEventHandlerExtension>();
            if (ext != null) ext.OnEventExpired(evt, faction);
            else EventStatModifierApplier.Remove(evt, faction);
        }

        /// <summary>Returns the next unique event ID, used by FCEvent's load-id constructor.</summary>
        public int NextEventId() => ++nextEventId;

        /// <summary>Migration entry point used by FactionFC.ExposeData to seed the
        /// counter from a legacy <c>nextEventID</c> scribe key on first load.</summary>
        public void SeedNextEventId(int value)
        {
            if (value > nextEventId) nextEventId = value;
        }

        public void Clear()
        {
            if (events.Count == 0) return;
            events.Clear();
            IndexClear();
            version++;
        }

        /// <summary>Drops events whose def failed to resolve on load (removed/renamed
        /// FCEventDef). Such events carry no label/desc/handler and only crash display
        /// and processing. Raw removal — they were never indexed or stat-applied, so
        /// there is no expiry lifecycle to run. Returns the number purged.</summary>
        public int PruneNullDefEvents(string caller = "")
        {
            int removed = events.RemoveAll(e => e is null || e.def is null);
            if (removed > 0)
            {
                version++;
                LogUtil.Warning($"{caller}: Purged {removed} event(s) with null/unresolved def from save data.");
            }
            return removed;
        }

        // Collects every Queued event whose timeTillTrigger has passed, returning them as a new list.
        // Events stay in the queue; ProcessEvents transitions phase Queued -> Fired (tentative)
        // -> Completed (default at end of body) inside its per-event re-entrancy guard, and a sweep
        // after the loop removes Completed events. Skips events already in Fired or Completed phase.
        public List<FCEvent> CollectDueEvents(int currentTick)
        {
            List<FCEvent> due = null;
            for (int i = events.Count - 1; i >= 0; i--)
            {
                if (!events[i].IsQueued) continue;
                if (events[i].timeTillTrigger > currentTick) continue;
                if (due is null) due = new List<FCEvent>();
                due.Add(events[i]);
            }
            return due;
        }

        // Bulk seed from a legacy list. Used only by FactionFC's save-migration
        // path to move pre-manager events into the manager. Bumps version once.
        // Bypasses the OnEventQueued lifecycle hook — stat modifiers are reapplied
        // separately by WorldSettlementFC.PostLoadInit.
        public void SeedFromLegacy(IEnumerable<FCEvent> legacyEvents)
        {
            if (legacyEvents is null) return;
            bool any = false;
            foreach (FCEvent evt in legacyEvents)
            {
                if (evt is null) continue;
                events.Add(evt);
                IndexAdd(evt);
                any = true;
            }
            if (any) version++;
        }

        public void RecordCooldown(FCEventDef def)
        {
            if (def is null) return;
            eventCooldowns[def] = Find.TickManager.TicksGame;
        }

        public bool IsOnCooldown(FCEventDef def)
        {
            if (def is null || def.cooldownTicks <= 0) return false;
            if (!eventCooldowns.TryGetValue(def, out int lastTick)) return false;
            return Find.TickManager.TicksGame - lastTick < def.cooldownTicks;
        }

        public void RecordFired(FCEventDef def)
        {
            if (def is null) return;
            eventFireCounts.TryGetValue(def, out int count);
            eventFireCounts[def] = count + 1;
        }

        public bool HasReachedMaxFireCount(FCEventDef def)
        {
            if (def is null || def.maxFireCount <= 0) return false;
            if (!eventFireCounts.TryGetValue(def, out int count)) return false;
            return count >= def.maxFireCount;
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref events, "events", LookMode.Deep);
            if (events is null) events = new List<FCEvent>();
            Scribe_Collections.Look(ref eventCooldowns, "eventCooldowns", LookMode.Def, LookMode.Value);
            if (eventCooldowns is null) eventCooldowns = new Dictionary<FCEventDef, int>();
            Scribe_Collections.Look(ref eventFireCounts, "eventFireCounts", LookMode.Def, LookMode.Value);
            if (eventFireCounts is null) eventFireCounts = new Dictionary<FCEventDef, int>();
            Scribe_Values.Look(ref nextEventId, "nextEventId", 1);

            // Indexes are transient — rebuild as soon as the events list is populated.
            // Must run in LoadingVars (not PostLoadInit): WorldSettlementFC.PostLoadInit
            // fires ISettlementPostLoadInit callbacks that query the index, and the
            // PostLoadIniter HashSet can schedule WorldSettlementFC before FCEventManager.
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                IndexRebuild();
        }
    }
}
