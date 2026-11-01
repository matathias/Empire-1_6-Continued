using FactionColonies.util;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FactionColonies
{
    public static class FCEventMaker
    {
        public static void CalculateSuccess(FCOptionDef option, FCEvent parentEvent)
        {
            if (option.successEvent is null) return;

            float baseChance = option.baseChanceOfSuccess;
            // Max-inclusive: 1..100 so chance is exactly chance/100 (0 = never, 100 = always).
            int roll = Rand.RangeInclusive(1, 100);
            FCEventDef chosen = roll <= baseChance ? option.successEvent : option.failEvent;
            if (chosen is null) return;

            /* Tainted chain -> silent drop. The current event (with options) already ran its course.
             * Filter must precede MakeRandomEvent: that call would open a window itself for
             * activateAtStart && options.Count > 0 defs. */
            if (FCSettings.disableEventsWithOptions) return;

            List<WorldSettlementFC> settlements = option.parentEvent.settlementsCarryOver
                ? parentEvent.settlementTraitLocations
                : null;
            FCEvent tempEvent = MakeRandomEvent(chosen, settlements);

            if (tempEvent != null)
            {
                FindFC.EventManager.AddEvent(tempEvent);

                Find.LetterStack.ReceiveLetter(tempEvent.def.label, BuildEventLetterBody(tempEvent), LetterDefOf.NeutralEvent);
            }
        }

        /// <summary>
        /// True if a multi-step random event chain is currently unresolved for the faction.
        /// Used by the <c>blockEventsDuringChain</c> setting to bar new random roots while a chain
        /// is in progress (the check supersedes the frequency timer).
        /// </summary>
        public static bool IsRandomChainInProgress(FactionFC faction)
        {
            if (faction is null) return false;
            HashSet<FCEventDef> members = FactionCache.RandomChainMemberDefs;
            if (members.Count == 0) return false;

            // Any queued / in-flight chain event. Completed events are awaiting sweep — ignore.
            foreach (FCEvent e in faction.eventManager.Events)
            {
                if (e?.def != null && !e.IsCompleted && members.Contains(e.def)) return true;
            }

            return false;
        }

        public static string BuildEventLetterBody(FCEvent evt)
        {
            string desc = evt.hasCustomDescription && !evt.customDescription.NullOrEmpty()
                ? evt.customDescription
                : evt.def.desc ?? "";

            string body = desc.Format();

            // Stat modifiers
            TaggedString statDesc = FCStatModifier.GetDescription(evt.def.statModifiers);
            if (!statDesc.NullOrEmpty())
            {
                body += "\n\n" + statDesc;
            }

            // Permanent stat modifiers
            TaggedString permDesc = FCStatModifier.GetDescription(evt.def.permanentStatModifiers);
            if (!permDesc.NullOrEmpty())
            {
                body += "\n\n" + permDesc;
            }

            // Affected settlements
            if (evt.settlementTraitLocations != null && evt.settlementTraitLocations.Count > 0)
            {
                string settlementString = evt.settlementTraitLocations
                    .Where(s => s != null)
                    .Join(s => " " + s.Name, "\n");
                if (!settlementString.NullOrEmpty())
                {
                    body += "\n\n" + "FCEventAffectingSettlements".Translate() + "\n" + settlementString;
                }
            }

            return body;
        }

        public static bool IsValidRandomEvent(FCEventDef cEvent)
        {
            FactionFC tmp = FindFC.FactionComp;

            if (!cEvent.isRandomEvent) return false;
            if (FCSettings.IsEventDisabled(cEvent.defName)) return false;
            if (Find.World.PlayerWealthForStoryteller < cEvent.requiredWealth) return false;

            // Stat range checks
            if (cEvent.minimumHappiness > tmp.averageHappiness || tmp.averageHappiness > cEvent.maximumHappiness) return false;
            if (cEvent.minimumLoyalty > tmp.averageLoyalty || tmp.averageLoyalty > cEvent.maximumLoyalty) return false;
            if (cEvent.minimumUnrest > tmp.averageUnrest || tmp.averageUnrest > cEvent.maximumUnrest) return false;
            if (cEvent.minimumProsperity > tmp.averageProsperity || tmp.averageProsperity > cEvent.maximumProsperity) return false;

            // Settlement count check
            bool noSettlementRequirement = cEvent.rangeSettlementsAffected.min == 0
                                           && cEvent.rangeSettlementsAffected.max == 0
                                           && !cEvent.targetAllSettlements;
            if (!noSettlementRequirement && FindFC.Settlements.Count < cEvent.rangeSettlementsAffected.min) return false;
            if (cEvent.targetAllSettlements && FindFC.Settlements.Count == 0) return false;

            // Biome check — for settlement-targeting events, at least one settlement must qualify
            if (!noSettlementRequirement && (cEvent.applicableBiomes.Count > 0 || cEvent.restrictedBiomes.Count > 0))
            {
                bool anyMatch = false;
                foreach (WorldSettlementFC s in FindFC.Settlements)
                {
                    if (cEvent.BiomeAllowed(s.biome)) { anyMatch = true; break; }
                }
                if (!anyMatch) return false;
            }

            // Required resource check
            if (cEvent.requiredResource != null)
            {
                bool hasResource = FindFC.FactionComp.ReturnResource(cEvent.requiredResource).amount > 0;
                if (!hasResource) return false;
            }

            // Incompatible/duplicate event check
            // Faction-wide events are blocked globally if already active.
            // Settlement-specific events are allowed through — MakeRandomEvent handles per-settlement filtering.
            foreach (FCEvent evt in FindFC.Events)
            {
                if (evt.def == null) continue;
                if (cEvent == evt.def && noSettlementRequirement) return false;

                foreach (FCEventDef inEvt in evt.def.incompatibleEvents)
                {
                    if (cEvent == inEvt) return false;
                }
            }

            // Tech level and research requirements
            if (!cEvent.SatisfiesTechRequirements(tmp.techLevel)) return false;

            // Cooldown check
            if (tmp.IsEventOnCooldown(cEvent)) return false;

            // Max fire count check
            if (tmp.HasReachedMaxFireCount(cEvent)) return false;

            // Minimum settlements prerequisite (independent of rangeSettlementsAffected targeting)
            if (cEvent.minSettlements > 0 && tmp.settlements.Count < cEvent.minSettlements) return false;

            // Required policy/trait/edict
            if (cEvent.requiredPolicy != null
                && !FindFC.PolicyManager.HasPolicy(cEvent.requiredPolicy)
                && !FindFC.PolicyManager.HasTrait(cEvent.requiredPolicy)
                && !FindFC.PolicyManager.HasEdict(cEvent.requiredPolicy)) return false;

            // Minimum faction age
            if (cEvent.minDaysSinceFounded > 0
                && (Find.TickManager.TicksGame - tmp.FoundingTick) < cEvent.minDaysSinceFounded * GenDate.TicksPerDay) return false;

            return true;
        }

        public static FCEventDef ReturnRandomEvent()
        {
            List<FCEventDef> tmpEventList = new List<FCEventDef>();

            foreach (FCEventDef eventDef in FactionCache.AllRandomEventDefs)
            {
                if (FCSettings.disableEventsWithOptions
                    && FactionCache.EventDefNamesWithOptionsInChain.Contains(eventDef.defName))
                    continue;
                if (IsValidRandomEvent(eventDef))
                {
                    for (int i = 0; i < eventDef.weight; i++)
                    {
                        tmpEventList.Add(eventDef);
                    }
                }
            }

            if (tmpEventList.Count() == 0)
                return null;

            FCEventDef selected = tmpEventList.RandomElement();

            // Allow active behaviors to request a single re-roll
            FactionFC faction = FindFC.FactionComp;
            if (faction != null)
            {
                bool reroll = false;
                FindFC.PolicyManager.ForEachBehavior(b =>
                {
                    if (!reroll && b.ShouldRerollEvent(selected))
                        reroll = true;
                });
                if (reroll)
                    selected = tmpEventList.RandomElement();
            }

            return selected;
        }

        public static FCEvent MakeEvent(FCEventDef def)
        {
            if (def == null)
            {
                return null;
            }

            FCEvent tempEvent = new FCEvent(true);
            tempEvent.def = def;
            tempEvent.tickStarted = Find.TickManager.TicksGame;
            int duration = def.timeTillTrigger;
            if (def.HasVariableDuration)
            {
                duration = Rand.Range(def.timeTillTrigger, def.timeTillTriggerMax);
                tempEvent.timeMinTrigger = Find.TickManager.TicksGame + def.timeTillTrigger;
                tempEvent.timeMaxTrigger = Find.TickManager.TicksGame + def.timeTillTriggerMax;
                LogUtil.Message($"Making event {def.defName} with variable duration. Min: {def.timeTillTrigger}, Max: {def.timeTillTriggerMax}, Duration: {duration}");
            }
            else
            {
                LogUtil.Message($"Making event {def.defName} with duration {duration}");
            }
            tempEvent.timeTillTrigger = Find.TickManager.TicksGame + duration;
            return tempEvent;
        }

        public static FCEvent MakeRandomEvent(FCEventDef def, List<WorldSettlementFC> SettlementTraitLocations)
        {
            if (def is null) return null;

            FactionFC worldcomp = FindFC.FactionComp;

            int now = Find.TickManager.TicksGame;
            int duration = def.timeTillTrigger;
            FCEvent tempEvent = new FCEvent(true)
            {
                def = def,
                tickStarted = now,
                settlementTraitLocations = new List<WorldSettlementFC>()
            };
            if (def.HasVariableDuration)
            {
                duration = Rand.Range(def.timeTillTrigger, def.timeTillTriggerMax);
                tempEvent.timeMinTrigger = now + def.timeTillTrigger;
                tempEvent.timeMaxTrigger = now + def.timeTillTriggerMax;
            }
            tempEvent.timeTillTrigger = now + duration;

            try
            {
                // Carry over settlement locations from parent event, or pick new ones
                if (SettlementTraitLocations != null && SettlementTraitLocations.Count > 0)
                {
                    tempEvent.settlementTraitLocations.AddRange(SettlementTraitLocations);
                }
                else if (tempEvent.def.targetAllSettlements)
                {
                    // Deterministically target every qualifying settlement
                    HashSet<WorldSettlementFC> excludedSettlements = new HashSet<WorldSettlementFC>();
                    foreach (FCEvent activeEvt in worldcomp.Events)
                    {
                        if (activeEvt.def == null) continue;
                        bool isSameDef = activeEvt.def == def;
                        bool isIncompatible = false;
                        if (!isSameDef)
                        {
                            foreach (FCEventDef inEvt in activeEvt.def.incompatibleEvents)
                            {
                                if (inEvt == def) { isIncompatible = true; break; }
                            }
                        }
                        if (isSameDef || isIncompatible)
                        {
                            foreach (WorldSettlementFC s in activeEvt.settlementTraitLocations)
                            {
                                if (s != null) excludedSettlements.Add(s);
                            }
                        }
                    }

                    foreach (WorldSettlementFC settlement in worldcomp.settlements)
                    {
                        if (excludedSettlements.Contains(settlement)) continue;
                        if (!tempEvent.def.BiomeAllowed(settlement.biome)) continue;
                        if (!tempEvent.def.SettlementTypeAllowed(settlement.settlementDef)) continue;
                        if (tempEvent.def.requiredResource != null)
                        {
                            ResourceFC res = settlement.GetResource(tempEvent.def.requiredResource);
                            // null always fails theta comparisons, so this check is safe
                            if (res?.InstantaneousProduction <= 0) continue;
                        }
                        tempEvent.settlementTraitLocations.Add(settlement);
                    }

                    if (tempEvent.settlementTraitLocations.Count == 0)
                    {
                        LogUtil.Warning($"targetAllSettlements event '{def.defName}' found no qualifying settlements");
                        return null;
                    }
                }
                else if (tempEvent.def.rangeSettlementsAffected.max != 0)
                {
                    int numSettlements = tempEvent.def.rangeSettlementsAffected.RandomInRange;

                    //if random number of settlements more than total settlements, reset number settlements.
                    if (numSettlements > worldcomp.settlements.Count())
                    {
                        numSettlements = worldcomp.settlements.Count();
                    }

                    //List of map locations
                    List<WorldSettlementFC> settlements = new List<WorldSettlementFC>();
                    //temporary list of settlemnts.
                    List<WorldSettlementFC> tmp = new List<WorldSettlementFC>();

                    // Exclude settlements already affected by the same or an incompatible event
                    HashSet<WorldSettlementFC> excludedSettlements = new HashSet<WorldSettlementFC>();
                    foreach (FCEvent activeEvt in worldcomp.Events)
                    {
                        if (activeEvt.def == null) continue;
                        bool isSameDef = activeEvt.def == def;
                        bool isIncompatible = false;
                        if (!isSameDef)
                        {
                            foreach (FCEventDef inEvt in activeEvt.def.incompatibleEvents)
                            {
                                if (inEvt == def) { isIncompatible = true; break; }
                            }
                        }
                        if (isSameDef || isIncompatible)
                        {
                            foreach (WorldSettlementFC s in activeEvt.settlementTraitLocations)
                            {
                                if (s != null) excludedSettlements.Add(s);
                            }
                        }
                    }

                    foreach (WorldSettlementFC settlement in worldcomp.settlements.InRandomOrder())
                    {
                        if (excludedSettlements.Contains(settlement)) continue;
                        if (!tempEvent.def.BiomeAllowed(settlement.biome)) continue;
                        if (!tempEvent.def.SettlementTypeAllowed(settlement.settlementDef)) continue;
                        if (tempEvent.def.requiredResource != null)
                        {
                            ResourceFC res = settlement.GetResource(tempEvent.def.requiredResource);
                            if (res != null && res.InstantaneousProduction > 0)
                            {
                                // Settlements that produce more of a resource should have a higher weight
                                for (int i = 0; i < Math.Max(0, res.InstantaneousProduction); i++)
                                {
                                    tmp.Add(settlement);
                                }
                            }
                        }
                        else
                        {
                            tmp.Add(settlement);
                        }
                    }

                    // Pick first settlement randomly
                    if (tmp.Count > 0)
                    {
                        WorldSettlementFC first = tmp.RandomElement();
                        settlements.Add(first);
                        tmp.Remove(first);
                    }

                    // Pick remaining settlements, weighted by proximity to the first
                    while (tmp.Count > 0 && settlements.Count < numSettlements)
                    {
                        WorldSettlementFC next;
                        if (tempEvent.def.useProximity && settlements.Count > 0)
                        {
                            next = SelectByProximity(tmp, settlements[0], tempEvent.def.proximityFalloff);
                        }
                        else
                        {
                            next = tmp.RandomElement();
                        }

                        if (!settlements.Contains(next))
                        {
                            settlements.Add(next);
                        }
                        tmp.Remove(next);
                    }

                    tempEvent.settlementTraitLocations.AddRange(settlements);

                    // If no valid settlements were chosen, then return early instead of firing the event
                    if (tempEvent.settlementTraitLocations.Count == 0)
                    {
                        LogUtil.Warning($"Random event '{def.defName}' found no valid settlements"
                                        + (def.requiredResource != null ? $" (requires {def.requiredResource.defName} production)" : "")
                                        + (def.applicableBiomes.Count > 0 ? $" (biomes: {string.Join(", ", def.applicableBiomes)})" : "")
                                        + (def.restrictedBiomes.Count > 0 ? $" (excluded biomes: {string.Join(", ", def.restrictedBiomes)})" : ""));
                        return null;
                    }
                }

                //if event has options
                //open event option window
                if (tempEvent.def.options.Count > 0 && tempEvent.def.activateAtStart)
                {
                    // This path bypasses the event queue (returns null), so ProcessEvents' fire-count
                    // recording never runs for these roots. Record it here to keep maxFireCount honest.
                    if (tempEvent.def.maxFireCount > 0)
                    {
                        FindFC.FactionComp.RecordEventFired(tempEvent.def);
                    }
                    Find.WindowStack.Add(new FCOptionWindow(tempEvent.def, tempEvent));
                    return null;
                }
            }
            catch (Exception e)
            {
                LogUtil.Error($"Couldn't create Random Event with def: {def?.defName ?? "NULL"} and list of size: {SettlementTraitLocations?.Count ?? 0}: {e.Message}");
            }

            return tempEvent;
        }


        private static WorldSettlementFC SelectByProximity(
            List<WorldSettlementFC> candidates, WorldSettlementFC anchor, float falloff)
        {
            float totalWeight = 0f;
            float[] weights = new float[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                float dist = Find.WorldGrid.ApproxDistanceInTiles(anchor.Tile, candidates[i].Tile);
                weights[i] = 1f / (1f + dist / falloff);
                totalWeight += weights[i];
            }

            float roll = Rand.Range(0f, totalWeight);
            float cumulative = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                cumulative += weights[i];
                if (roll <= cumulative)
                    return candidates[i];
            }
            return candidates[candidates.Count - 1];
        }

        public static void ProcessEvents()
        {
            FactionFC faction = FindFC.FactionComp;
            int currentTick = Find.TickManager.TicksGame;

            // Phase 1: collect every Queued event past its timer (events stay in the queue;
            // their phase transitions during processing and a sweep at the end removes Completed).
            List<FCEvent> due = faction.eventManager.CollectDueEvents(currentTick);
            if (due is null) return;

            // Phase 2: process collected events
            foreach (var evt in due)
            {
                try
                {
                    // Guard against accidental re-fires
                    if (!evt.IsQueued)
                    {
                        LogUtil.Error($"ProcessEvents: event '{evt.def?.defName ?? "NULL"}' (loadID={evt.loadID}) not in Queued phase ({evt.phase}). Skipping.");
                        continue;
                    }
                    evt.phase = FCEventPhase.Fired;     // tentative; mid-processing only. Handlers wanting persistence transition to Resolving during their run.

                    // Defer the cooldown to the last chain step: re-stamp the chain root's cooldown each
                    // time any member of its chain fires.
                    FCEventDef cooldownRoot = FactionCache.ChainCooldownRootOf(evt.def);
                    if (cooldownRoot != null)
                    {
                        faction.RecordEventCooldown(cooldownRoot);
                    }
                    else if (evt.def != null && evt.def.cooldownTicks > 0)
                    {
                        faction.RecordEventCooldown(evt.def);
                    }

                    // Track fire count for events with a max
                    if (evt.def?.maxFireCount > 0)
                    {
                        faction.RecordEventFired(evt.def);
                    }

                    if (evt.def == null)
                    {
                        LogUtil.Warning($"Purging event with null def (loadID={evt.loadID}). Likely corrupted save data.");
                        evt.phase = FCEventPhase.Completed; // let the end-of-pass sweep remove it
                        continue;
                    }

                    WorldSettlementFC settlement;

                    LogUtil.Message($"Processing event {evt.def.defName}");

                    FCEventHandlerExtension handler = evt.def.GetModExtension<FCEventHandlerExtension>();
                    bool handled = handler != null && handler.ResolveEvent(evt, faction);

                    if (!handled)
                    {
                        // Op-aware dispatch: military events scheduled by MilitaryOperationManager
                        // carry a linkedOperation back-reference. Route them through the op's phase
                        // machine. An orphan op-linked event (linkedOperation null because the op was
                        // unregistered while events still pointed at it) is a silent no-op — the
                        // correct behavior on the dispatch side.
                        if (evt.HasLinkedOperation)
                        {
                            try { evt.linkedOperation.OnEventFired(evt); }
                            catch (Exception e)
                            {
                                LogUtil.Error($"FCEventMaker: op id={evt.linkedOperation.id} threw in OnEventFired for '{evt.def.defName}': {e}");
                            }
                        }
                        else
                        {
                            switch (evt.def.defName)
                            {
                                case "settleNewColony":
                                    {
                                        try
                                        {
                                            //Settle new colony event
                                            faction.AddExperienceToFactionLevel(10f);

                                            ColonyUtil.CreatePlayerColonySettlement(evt.location, evt.settlementToCreate);

                                            faction.settlementCaravansList.Remove(evt.location);
                                        }
                                        catch (Exception e)
                                        {
                                            LogUtil.Error($"Exception processing event '{evt.def?.defName ?? "NULL"}' (loadID={evt.loadID}): {e}");

                                            faction.settlementCaravansList.Remove(evt.location);
                                        }
                                        break;
                                    }
                                case "taxColony":
                                    {
                                        settlement = faction.ReturnSettlementByLocation(evt.source);
                                        if (settlement is null)
                                        {
                                            LogUtil.Warning($"taxColony event references missing settlement at tile {evt.source}. Skipping delivery.");
                                            break;
                                        }

                                        // Let registered interceptors try to handle delivery first
                                        TaxDeliveryContext deliveryCtx = new TaxDeliveryContext(evt, settlement);
                                        if (!TaxDeliveryRegistry.InvokeTryDeliverGoods(deliveryCtx))
                                        {
                                            // No interceptor handled it — default delivery
                                            string str = "FCTaxesFrom".Translate() + " " + settlement.Name + " " + "FCHaveBeenDelivered".Translate() + "!";
                                            Message msg = new Message(str, MessageTypeDefOf.PositiveEvent);
                                            PaymentUtil.DeliverThings(evt, LetterMaker.MakeLetter("FCTaxesHaveArrived".Translate(), str + "\n" + evt.goods.ToLetterString(), LetterDefOf.PositiveEvent), msg);
                                        }
                                        break;
                                    }
                                case "constructBuilding":
                                    //Create building
                                    settlement = faction.ReturnSettlementByLocation(evt.source);
                                    if (settlement != null)
                                    {
                                        settlement.ConstructBuilding(evt.building, evt.buildingSlot);
                                        Messages.Message("FCBuildingEventCompletedMsg".Translate(evt.building.LabelCap, settlement.Name), MessageTypeDefOf.PositiveEvent);
                                    }
                                    else
                                    {
                                        LogUtil.Error($"Attempted to resolve a constructBuilding event for an invalid settlement");
                                    }
                                    break;
                                case "upgradeSettlement":
                                    {
                                        if (faction.ReturnSettlementByLocation(evt.location) != null)
                                        {
                                            //if settlement is not null
                                            settlement = faction.ReturnSettlementByLocation(evt.location);
                                            settlement.UpgradeSettlement(setFlags: true);
                                            Find.LetterStack.ReceiveLetter("FCUpgradeSettlement".Translate(),
                                                "FCUpgradeEventCompletedDesc".Translate(settlement.Name, settlement.settlementLevel, "FCUpgradeColonyDesc".Translate()),
                                                LetterDefOf.PositiveEvent);
                                        }

                                        break;
                                    }
                                default:
                                    {
                                        // Undefined event: optionally awards a random thing reward.
                                        if (evt.def.randomThingValue > 0 && evt.def.randomThingRewardDef != null)
                                        {
                                            /* Scale reward value by the number of affected settlements so multi-target events
                                             * deliver reward magnitude proportional to their scope. Matches FCOptionCostUtil's
                                             * cost scaling — same Max(1, liveTargets) rule via the shared helper. */
                                            int scaledValue = evt.def.randomThingValue * FCEventScalingUtil.CountAffectedSettlements(evt);
                                            List<Thing> list = PaymentUtil.GenerateRewardThings(scaledValue, evt.def.randomThingRewardDef);

                                            string str = "FCGoodsReceivedFollowing".Translate(evt.def.label);

                                            str = list.Aggregate(str, (before, after) => before + "\n" + after.LabelCap);

                                            evt.goods.AddRange(list);

                                            evt.let = LetterMaker.MakeLetter("FCGoodsReceived".Translate(), str, LetterDefOf.PositiveEvent);
                                            if (list.Count > 0)
                                            {
                                                if (!evt.source.IsValidTile())
                                                {
                                                    if (evt.settlementTraitLocations.Any())
                                                    {
                                                        evt.source = evt.settlementTraitLocations.First().Tile;
                                                    }
                                                    else
                                                    {
                                                        evt.source = FindFC.CapitalLocation;
                                                    }
                                                }
                                                DeliveryEvent.CreateDeliveryEvent(evt);
                                            }
                                        }
                                        break;
                                    }
                            }
                        } // end of unlinked-event else
                    }

                    //If has loot to give
                    if (evt.def.loot.Any())
                    {
                        List<Thing> list = evt.def.loot.Select(thing => ThingMaker.MakeThing(thing)).ToList();
                        PaymentUtil.DeliverThings(list, evt.source);
                    }


                    /* Stat-modifier removal + prosperityLost subtraction + InvalidateFactionStatCache
                     * now fire from FCEventHandlerExtension.OnEventExpired (dispatched by
                     * FCEventManager.Remove / RemoveWhere when the event leaves the queue). */

                    //if have options
                    if (evt.def != null && evt.def.options.Count > 0 && evt.def.activateAtStart == false)
                    {
                        Find.WindowStack.Add(new FCOptionWindow(evt.def, evt));
                    }

                    //if has following event
                    if (evt.def.HasFollowUp)
                    {
                        FCEventDef target;
                        if (evt.def.splitEventFollows)
                        {
                            // Max-inclusive: 1..100 so splitEventChance is exact (100 = always).
                            int roll = Rand.RangeInclusive(1, 100);
                            target = roll <= evt.def.splitEventChance
                                ? evt.def.followingEvent
                                : evt.def.followingEvent2;
                        }
                        else
                        {
                            target = evt.def.followingEvent;
                        }

                        /* Silent-drop guard: if the next link in the chain leads to options and the
                         * setting is on, the chain ends here. Must precede MakeRandomEvent: that call
                         * would open a window itself for activateAtStart && options.Count > 0 defs. */
                        bool drop = target is null
                            || (FCSettings.disableEventsWithOptions
                                && FactionCache.EventDefNamesWithOptionsInChain.Contains(target.defName));

                        if (!drop)
                        {
                            List<WorldSettlementFC> settlements = evt.def.settlementsCarryOver
                                ? evt.settlementTraitLocations
                                : null;
                            FCEvent tempEvent = MakeRandomEvent(target, settlements);

                            if (tempEvent != null)
                            {
                                faction.eventManager.AddEvent(tempEvent);

                                Find.LetterStack.ReceiveLetter(tempEvent.def.label, BuildEventLetterBody(tempEvent), LetterDefOf.NeutralEvent);
                            }
                        }
                    }

                    evt.RunAction();
                }
                catch (Exception ex)
                {
                    LogUtil.Error($"ProcessEvents: exception processing event '{evt.def?.defName ?? "NULL"}' " +
                        $"(loadID={evt.loadID}): {ex}");
                    TryRecoverFailedEvent(evt, faction);
                }

                // Force out of Fired phase. Handlers that wanted persistence transitioned to
                // Resolving during their run; everything still in Fired here is fire-and-forget
                // and unconditionally completed. After this point, no event in the queue should
                // be in Fired phase.
                if (evt.IsFired)
                {
                    evt.phase = FCEventPhase.Completed;
                }
            }

            // Sweep events that completed during this tick (or earlier).
            faction.eventManager.RemoveWhere(e => e.IsCompleted);
        }

        private static void TryRecoverFailedEvent(FCEvent evt, FactionFC faction)
        {
            if (evt?.def is null) return;
            try
            {
                if (evt.def == FCEventDefOf.constructBuilding)
                {
                    WorldSettlementFC settlement = faction.ReturnSettlementByLocation(evt.source);
                    if (settlement is object && evt.building is object && evt.buildingSlot >= 0)
                    {
                        settlement.ConstructBuilding(evt.building, evt.buildingSlot);
                        LogUtil.Warning($"Recovered orphaned construction: {evt.building.defName} at {settlement.Name} slot {evt.buildingSlot}");
                    }
                }
                else if (evt.def == FCEventDefOf.upgradeSettlement)
                {
                    WorldSettlementFC settlement = faction.ReturnSettlementByLocation(evt.location);
                    if (settlement is object)
                    {
                        settlement.UpgradeSettlement(setFlags: true);
                        LogUtil.Warning($"Recovered orphaned upgrade at {settlement.Name}");
                    }
                }
            }
            catch (Exception recoveryEx)
            {
                LogUtil.Error($"ProcessEvents: recovery also failed for '{evt.def.defName}': {recoveryEx}");
            }
        }

        public static void CreateTaxEvent(BillFC bill)
        {
            FactionFC faction = FindFC.FactionComp;

            FCEvent tmp = MakeEvent(FCEventDefOf.taxColony);
            tmp.tickStarted = Find.TickManager.TicksGame;

            if (bill.settlement != null && faction.settlements.Contains(bill.settlement))
            {
                tmp.source = bill.settlement.Tile; //source location
                tmp.customDescription = "FCTaxesFromSettlementAreBeingDelivered".Translate(bill.settlement.Name);
            }
            else
            {
                // FIX: Instead of using -1, use the capital location as both source and destination
                // This represents taxes being collected locally at the capital
                PlanetTile fallbackTile = Find.AnyPlayerHomeMap?.Tile ?? PlanetTile.Invalid;
                tmp.source = faction.capitalLocation != PlanetTile.Invalid ? faction.capitalLocation : fallbackTile;
                tmp.customDescription = "FCTaxesFromSettlementAreBeingDelivered".Translate("FCCapital".Translate());

                LogUtil.Message($"Tax Event Debug: faction.capitalLocation={faction.capitalLocation}, fallbackTile={fallbackTile}, tmp.source={tmp.source}");
            }

            tmp.location = faction.capitalLocation;

            // Lock in delivery mode at creation time so changing settings mid-transit doesn't alter delivery
            bool canUseShuttle = faction.settlements.FirstOrFallback(s => s.Tile == tmp.source)
                ?.BuildingsComp?.HasBuilding(BuildingFCDefOf.shuttlePort) ?? false;
            tmp.deliveryMode = DeliveryLogistics.TaxDeliveryModeForSettlement(canUseShuttle, tmp.source);

            // FIX: Handle case where source equals destination (local delivery)
            if (tmp.source == tmp.location)
            {
                // Local delivery - very short time
                tmp.timeTillTrigger = Find.TickManager.TicksGame + GenDate.TicksPerHour; // 1 hour
            }
            else
            {
                int travelTime = TravelUtil.ReturnTicksToArrive(tmp.source, tmp.location);
                tmp.timeTillTrigger = Find.TickManager.TicksGame + travelTime;
                LogUtil.Message($"Tax Event Travel Debug: source={tmp.source}, destination={tmp.location}, travelTime={travelTime} ticks ({travelTime / GenDate.TicksPerDay:F1} days)");
            }

            tmp.hasCustomDescription = true;
            //add tithe
            tmp.goods = bill.taxes.itemTithes;

            if (bill.taxes.silverAmount > 0) //if getting paid, add silver to tithe
            {
                //add to tithe
                //tmp.goods.Add()
                int silverTotal = (int)bill.taxes.silverAmount;
                while (silverTotal > 0)
                {
                    Thing thing = ThingMaker.MakeThing(ThingDefOf.Silver);

                    if (silverTotal > thing.def.stackLimit)
                    {
                        thing.stackCount = thing.def.stackLimit;
                        silverTotal -= thing.def.stackLimit;
                    }
                    else
                    {
                        //if not above stack limit
                        thing.stackCount = silverTotal;
                        silverTotal -= silverTotal;
                    }

                    tmp.goods.Add(thing);
                }
            }
            else if (bill.taxes.silverAmount < 0) //if paying money
            {
                //remove money from colony. BillFC.AttemptResolve already verified the balance, so
                //this atomic pay should always succeed; log if a payment modifier somehow inflated it.
                if (!PaymentUtil.TryPaySilver((int)(-1 * (bill.taxes.silverAmount)), PaymentUtil.Reason_TaxPayment, bill.settlement))
                {
                    LogUtil.Warning($"CreateTaxEvent: could not collect {-bill.taxes.silverAmount} silver for {bill.settlement?.Name}; balance shifted after bill resolution.");
                }
            }


            // add event to queue and remove bill
            try
            {
                if (tmp.goods.Count > 0) //if any silver or tithe in bill create event. else, well, don't
                {
                    faction.eventManager.AddEvent(tmp);
                }
            }
            catch (Exception e)
            {
                LogUtil.Error($"Error in CreateTaxEvent: {e}");
            }
            finally
            {
                faction.taxLedger.RemoveBill(bill);
            }
        }
    }
}