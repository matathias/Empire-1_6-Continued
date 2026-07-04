using FactionColonies.util;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace FactionColonies
{
    public static class DebugUtil
    {

        [DebugAction("Empire", "Force auto-resolve round now", allowedGameStates = AllowedGameStates.Playing)]
        private static void ForceAutoResolveRoundNow()
        {
            MilitaryOperationManager mgr = FindFC.MilitaryManager;
            if (mgr is null)
            {
                LogUtil.MessageForce("No MilitaryOperationManager available.");
                return;
            }
            int bumped = 0;
            foreach (MilitaryOperation op in mgr.active)
            {
                if (op is null) continue;
                if (op.phase != MilitaryOperationPhase.Engaged) continue;
                FCEvent evt = op.sourceEvents.FirstOrDefault(e => e is object && e.def == FCEventDefOf.autoResolveBattleRound);
                if (evt is object)
                {
                    evt.timeTillTrigger = Find.TickManager.TicksGame + 1;
                    bumped++;
                }
            }
            LogUtil.MessageForce($"Bumped {bumped} autoResolveBattleRound event(s) to fire next tick.");
        }

        [DebugAction("Empire", "View Events and ticks till", allowedGameStates = AllowedGameStates.Playing)]
        private static void ViewEventsAndLog()
        {
            foreach (FCEvent e in FindFC.Events)
            {
                LogUtil.MessageForce(e.def.defName + " with cooldown: " + (e.timeTillTrigger - Find.TickManager.TicksGame));
            }
        }

        [DebugAction("Empire", "Increment Time 5 Days", allowedGameStates = AllowedGameStates.Playing)]
        private static void IncrementTimeFiveDays()
        {
            LogUtil.MessageForce("Debug - Increment Time 5 Days");
            Find.TickManager.DebugSetTicksGame(Find.TickManager.TicksGame + 300000);
        }

        [DebugAction("Empire", "Increment Time 1 Year", allowedGameStates = AllowedGameStates.Playing)]
        private static void IncrementTimeOneYear()
        {
            LogUtil.MessageForce("Debug - Increment Time 1 Year");
            Find.TickManager.DebugSetTicksGame(Find.TickManager.TicksGame + GenDate.TicksPerYear);
        }

        [DebugAction("Empire", "Kill & Regen Leader", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugKillAndRegenLeader()
        {
            Faction faction = FindFC.EmpireFaction;
            if (faction == null)
            {
                LogUtil.MessageForce("No Empire faction found.");
                return;
            }
            Pawn oldLeader = faction.leader;
            if (oldLeader != null)
            {
                LogUtil.MessageForce($"Killing leader: {oldLeader.Name} ({oldLeader.ThingID}), " +
                                     $"title: {faction.LeaderTitle}, " +
                                     $"ideo: {oldLeader.Ideo?.name ?? "none"}");
                oldLeader.Kill(null);
            }
            else
            {
                LogUtil.MessageForce("No current leader. Generating new one.");
            }
            ColonyUtil.CreatePlayerFactionLeader(faction);
            if (faction.leader != null)
            {
                LogUtil.MessageForce($"New leader: {faction.leader.Name} ({faction.leader.ThingID}), " +
                                     $"title: {faction.LeaderTitle}, " +
                                     $"pawnKind: {faction.leader.kindDef?.defName ?? "null"}, " +
                                     $"ideo: {faction.leader.Ideo?.name ?? "none"}");
            }
        }

        [DebugAction("Empire", "Print Races", allowedGameStates = AllowedGameStates.Playing)]
        private static void PrintRaces()
        {
            FindFC.EmpireFaction.def.pawnGroupMakers.ForEach(maker =>
            {
                LogUtil.MessageForce("Traders: " + maker.traders.Count);
                foreach (PawnGenOption option in maker.options)
                {
                    LogUtil.MessageForce("Race: " + option.kind.race.defName + ", " + option.kind.defName + ", " +
                                option.kind.isFighter + ", " + option.kind.trader + " for " + maker.kindDef);
                }
            });
        }

        [DebugAction("Empire", "Send Pawn To Settlement", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SendPawnToSettlement()
        {
            List<Pawn> selected = Find.Selector.SelectedPawns;
            if (!selected.Any())
            {
                Messages.Message("No prisoner selected!", MessageTypeDefOf.RejectInput);
                return;
            }
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (WorldSettlementFC settlement in FindFC.Settlements)
            {
                list.Add(new DebugMenuOption(
                    $"{settlement.Name} - Level: {settlement.settlementLevel} - Prisoners: {settlement.PrisonerComp?.prisonerList?.Count ?? 0}",
                    DebugMenuOptionMode.Action, delegate
                    {
                        foreach (Pawn pawn in selected)
                        {
                            settlement.PrisonerComp?.AddPrisoner(pawn);
                            if (pawn.Spawned) pawn.DeSpawn();

                            foreach (var bed in Find.Maps.Where(map => map.IsPlayerHome).SelectMany(map =>
                                map.listerBuildings.allBuildingsColonist).OfType<Building_Bed>())
                            {
                                if (!Enumerable.Any(bed.OwnersForReading, found => found == pawn)) continue;
                                bed.ForPrisoners = false;
                                bed.ForPrisoners = true;
                            }
                        }
                    }));
            }

            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }

        [DebugAction("Empire", "Clear faction traits and policies", allowedGameStates = AllowedGameStates.Playing)]
        private static void ClearFactionTraitsAndPolicies()
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction == null) return;

            for (int i = 0; i < FindFC.PolicyManager.factionTraits.Count; i++)
            {
                if (FindFC.PolicyManager.factionTraits[i]?.behavior != null)
                {
                    try { FindFC.PolicyManager.factionTraits[i].behavior.OnRemoved(faction); }
                    catch (Exception e) { LogUtil.Error($"FCPolicyBehavior.OnRemoved error: {e}"); }
                }
                FindFC.PolicyManager.factionTraits[i] = new FCPolicy(FCPolicyDefOf.empty);
            }

            FindFC.PolicyManager.RemoveAllPolicies(FindFC.PolicyManager.policies);
            FindFC.PolicyManager.RebuildBehaviorCache();

            LogUtil.Message("Cleared faction traits and policies.");
        }

        [DebugAction("Empire", "Reset All Military Squad Assignments", allowedGameStates = AllowedGameStates.Playing)]
        private static void ResetAllMilitarySquads()
        {
            LogUtil.MessageForce("Debug - Reset All Military Squad Assignments");
            MilitaryFC mfc = FindFC.Military;
            var allMercs = mfc.AllMercenaries.ToList();
            for (int i = allMercs.Count - 1; i >= 0; i--)
            {
                if (allMercs[i].squad.Deployment.HasLord)
                {
                    allMercs[i].squad.Deployment.Map.lordManager.RemoveLord(allMercs[i].squad.Deployment.Lord);
                }

                allMercs[i].pawn.Destroy();
                allMercs[i].squad.mercenaries.Remove(allMercs[i]);
            }

            for (int k = mfc.mercenarySquads.Count() - 1; k >= 0; k--)
            {
                MercenarySquadFC squad = mfc.mercenarySquads[k];
                if (squad?.settlement != null) squad.settlement = null;
                mfc.mercenarySquads.RemoveAt(k);
            }


            mfc.CheckMilitaryUtilForErrors();
        }


        [DebugAction("Empire", "Make Random Event", allowedGameStates = AllowedGameStates.Playing)]
        private static void MakeRandomEvent()
        {
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (FCEventDef evtDef in DefDatabase<FCEventDef>.AllDefsListForReading)
            {
                if (evtDef.isRandomEvent)
                    list.Add(new DebugMenuOption(evtDef.label, DebugMenuOptionMode.Action, delegate
                    {
                        LogUtil.MessageForce("Debug - Make Random Event - " + evtDef.label);
                        FCEvent evt = FCEventMaker.MakeRandomEvent(evtDef, null);
                        if (evt == null)
                        {
                            if (!evtDef.activateAtStart)
                                LogUtil.Warning("Debug - Event returned null: " + evtDef.defName);
                            return;
                        }

                        if (!evtDef.activateAtStart)
                        {
                            FindFC.EventManager.AddEvent(evt);
                        }

                        string settlementString = evt.settlementTraitLocations.Join((settlement) => $" {settlement.Name}", "\n");
                        string eventDesc = evt.def.FormattedDesc;
                        if (!settlementString.NullOrEmpty())
                            Find.LetterStack.ReceiveLetter("Random Event", $"{eventDesc}\n{"FCEventAffectingSettlements".Translate()}\n{settlementString}", LetterDefOf.NeutralEvent);
                        else
                            Find.LetterStack.ReceiveLetter("Random Event", eventDesc, LetterDefOf.NeutralEvent);
                    }
                    ));
            }

            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }

        [DebugAction("Empire", "Start situation", allowedGameStates = AllowedGameStates.Playing)]
        private static void StartSituation()
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction is null)
            {
                LogUtil.MessageForce("Debug - Start situation: no faction.");
                return;
            }

            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (FCSituationDef sitDef in DefDatabase<FCSituationDef>.AllDefsListForReading)
            {
                FCSituationDef localDef = sitDef;
                string menuLabel = $"{localDef.label ?? localDef.defName} ({localDef.scope})";
                list.Add(new DebugMenuOption(menuLabel, DebugMenuOptionMode.Action, delegate
                {
                    // Settlement-scoped defs need a target; open a second menu to pick one. Faction-scoped
                    // start immediately. Debug bypasses spawn eligibility (calls StartSituation directly).
                    if (localDef.scope == FCSituationScope.Settlement)
                    {
                        List<DebugMenuOption> targets = new List<DebugMenuOption>();
                        foreach (WorldSettlementFC settlement in FindFC.Settlements)
                        {
                            WorldSettlementFC localSettlement = settlement;
                            targets.Add(new DebugMenuOption(localSettlement.Name, DebugMenuOptionMode.Action, delegate
                            {
                                FCSituation sit = faction.situationManager.StartSituation(localDef, localSettlement);
                                LogUtil.MessageForce(sit is object
                                    ? $"Debug - Started situation '{localDef.defName}' on {localSettlement.Name}."
                                    : $"Debug - Failed to start situation '{localDef.defName}' on {localSettlement.Name}.");
                            }));
                        }
                        Find.WindowStack.Add(new Dialog_DebugOptionListLister(targets));
                    }
                    else
                    {
                        FCSituation sit = faction.situationManager.StartSituation(localDef, null);
                        LogUtil.MessageForce(sit is object
                            ? $"Debug - Started faction situation '{localDef.defName}'."
                            : $"Debug - Failed to start situation '{localDef.defName}'.");
                    }
                }));
            }

            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }

        [DebugAction("Empire", "Remove all situations", allowedGameStates = AllowedGameStates.Playing)]
        private static void RemoveAllSituations()
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction is null)
            {
                LogUtil.MessageForce("Debug - Remove all situations: no faction.");
                return;
            }

            int removed = 0;
            foreach (FCSituation sit in new List<FCSituation>(faction.situationManager.Situations))
            {
                if (faction.situationManager.Remove(sit)) removed++;
            }
            LogUtil.MessageForce($"Debug - Removed {removed} situation(s).");
        }

        [DebugAction("Empire", "Adjust situation progress", allowedGameStates = AllowedGameStates.Playing)]
        private static void AdjustSituationProgress()
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction is null)
            {
                LogUtil.MessageForce("Debug - Adjust situation progress: no faction.");
                return;
            }

            FCSituationManager manager = faction.situationManager;
            List<FCSituation> sits = new List<FCSituation>(manager.Situations);
            if (sits.Count == 0)
            {
                Messages.Message("No active situations.", MessageTypeDefOf.RejectInput);
                return;
            }

            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (FCSituation sit in sits)
            {
                FCSituation localSit = sit;
                string target = localSit.targetSettlement is object ? localSit.targetSettlement.Name : "faction-wide";
                string menuLabel = $"{localSit.def.label ?? localSit.def.defName} [{target}] @ {localSit.progress:0}/{localSit.def.maxProgress:0}";
                list.Add(new DebugMenuOption(menuLabel, DebugMenuOptionMode.Action, delegate
                {
                    List<DebugMenuOption> deltas = new List<DebugMenuOption>();
                    AddSituationDeltaOption(deltas, manager, localSit, "+10", 10f);
                    AddSituationDeltaOption(deltas, manager, localSit, "+25", 25f);
                    AddSituationDeltaOption(deltas, manager, localSit, "-10", -10f);
                    AddSituationDeltaOption(deltas, manager, localSit, "-25", -25f);
                    AddSituationDeltaOption(deltas, manager, localSit, "to top", localSit.def.maxProgress - localSit.progress);
                    AddSituationDeltaOption(deltas, manager, localSit, "to bottom", -localSit.progress);
                    Find.WindowStack.Add(new Dialog_DebugOptionListLister(deltas));
                }));
            }

            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }

        private static void AddSituationDeltaOption(List<DebugMenuOption> options, FCSituationManager manager,
            FCSituation sit, string label, float delta)
        {
            options.Add(new DebugMenuOption(label, DebugMenuOptionMode.Action, delegate
            {
                if (!manager.Situations.Contains(sit)) return; // may have been removed by a Terminal endpoint
                manager.AddProgress(sit, delta);
                string stage = sit.currentStage is object ? sit.currentStage.label : "none";
                LogUtil.MessageForce($"Debug - {sit.def.defName} progress -> {sit.progress:0} (stage: {stage}).");
            }));
        }

        [DebugAction("Empire", "Proc MilitaryTimeDue", allowedGameStates = AllowedGameStates.Playing)]
        private static void ProcMilitaryTimeDue()
        {
            LogUtil.MessageForce("Debug - Proc MilitaryTimeDue");
            FindFC.FactionComp.militaryTimeDue = Find.TickManager.TicksGame + 1;
        }

        [DebugAction("Empire", "Attack Player Settlement", allowedGameStates = AllowedGameStates.Playing)]
        private static void AttackPlayerSettlement()
        {
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (WorldSettlementFC settlement in FindFC.Settlements)
            {
                list.Add(new DebugMenuOption(settlement.Name, DebugMenuOptionMode.Action, delegate
                {
                    Faction enemyFaction = Find.FactionManager.RandomEnemyFaction();
                    if (enemyFaction == null)
                    {
                        Messages.Message("No enemy faction found.", MessageTypeDefOf.RejectInput);
                        return;
                    }

                    List<DebugMenuOption> levelList = new List<DebugMenuOption>();
                    for (int level = 1; level <= 10; level++)
                    {
                        int chosenLevel = level;
                        levelList.Add(new DebugMenuOption($"Level {chosenLevel}", DebugMenuOptionMode.Action, delegate
                        {
                            MilitaryDeploymentUtil.GetTechLevelBaseline(enemyFaction.def.techLevel, out double _, out double efficiency);
                            MilitaryForce attackingForce = new MilitaryForce(chosenLevel, efficiency, null, enemyFaction);
                            LogUtil.MessageForce($"Debug - Attack Player Settlement - {settlement.Name} (level {chosenLevel}, efficiency {efficiency})");
                            if (!MilitaryOperationsUtil.AttackPlayerSettlement(attackingForce, settlement, enemyFaction))
                            {
                                Messages.Message($"Debug attack on {settlement.Name} failed (no MilitaryComp or MilitaryManager).", MessageTypeDefOf.RejectInput);
                            }
                        }));
                    }
                    Find.WindowStack.Add(new Dialog_DebugOptionListLister(levelList));
                }
                ));
            }

            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }

        [DebugAction("Empire", "Instant Attack Player Settlement", allowedGameStates = AllowedGameStates.Playing)]
        private static void InstantAttackPlayerSettlement()
        {
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (WorldSettlementFC settlement in FindFC.Settlements)
            {
                list.Add(new DebugMenuOption(settlement.Name, DebugMenuOptionMode.Action, delegate
                {
                    Faction enemyFaction = Find.FactionManager.RandomEnemyFaction();
                    if (enemyFaction == null)
                    {
                        Messages.Message("No enemy faction found.", MessageTypeDefOf.RejectInput);
                        return;
                    }

                    List<DebugMenuOption> levelList = new List<DebugMenuOption>();
                    for (int level = 1; level <= 10; level++)
                    {
                        int chosenLevel = level;
                        levelList.Add(new DebugMenuOption($"Level {chosenLevel}", DebugMenuOptionMode.Action, delegate
                        {
                            MilitaryDeploymentUtil.GetTechLevelBaseline(enemyFaction.def.techLevel, out double _, out double efficiency);
                            MilitaryForce attackingForce = new MilitaryForce(chosenLevel, efficiency, null, enemyFaction);
                            LogUtil.MessageForce($"Debug - Instant Attack Player Settlement - {settlement.Name} (level {chosenLevel}, efficiency {efficiency})");
                            if (settlement.MilitaryComp is null || FindFC.MilitaryManager is null)
                            {
                                Messages.Message($"Debug attack on {settlement.Name} failed (no MilitaryComp or MilitaryManager).", MessageTypeDefOf.RejectInput);
                                return;
                            }

                            // Call the manager directly so we get the freshly-created op handle. Going via
                            // MilitaryUtilFC.AttackPlayerSettlement would force a tile-wide event lookup, which
                            // returns the FIRST settlementBeingAttacked at this tile — that can be an older,
                            // already-stacked attack rather than the one we just queued.
                            MilitaryOperation op = FindFC.MilitaryManager.CreateDefensiveOp(settlement, attackingForce, enemyFaction);
                            if (op is null) return;

                            FCEvent attackEvt = null;
                            for (int i = op.sourceEvents.Count - 1; i >= 0; i--)
                            {
                                if (op.sourceEvents[i]?.def == FCEventDefOf.settlementBeingAttacked)
                                {
                                    attackEvt = op.sourceEvents[i];
                                    break;
                                }
                            }
                            if (attackEvt != null)
                            {
                                attackEvt.timeTillTrigger = Find.TickManager.TicksGame + 1;
                            }
                        }));
                    }
                    Find.WindowStack.Add(new Dialog_DebugOptionListLister(levelList));
                }
                ));
            }

            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }

        [DebugAction("Empire", "Force Attack + Event Same Tick", allowedGameStates = AllowedGameStates.Playing)]
        private static void ForceAttackAndEventSameTick()
        {
            FactionFC faction = FindFC.FactionComp;
            IReadOnlyList<FCEvent> attackEvents = faction.GetEventsByDef(FCEventDefOf.settlementBeingAttacked);
            FCEvent attackEvt = attackEvents.Count > 0 ? attackEvents[0] : null;
            if (attackEvt == null)
            {
                LogUtil.MessageForce("Debug - No pending settlementBeingAttacked event. Use 'Attack Player Settlement' first.");
                return;
            }

            if (FCSettings.disableRandomEvents)
            {
                LogUtil.MessageForce("Debug - Warning: random events are disabled in settings. Random event will not fire.");
            }

            int nextDayBoundary = ((Find.TickManager.TicksGame / GenDate.TicksPerDay) + 1) * GenDate.TicksPerDay;
            attackEvt.timeTillTrigger = nextDayBoundary;
            faction.randomEventLastAdded = FCSettings.maxDaysTillRandomEvent + 1;
            Find.TickManager.DebugSetTicksGame(nextDayBoundary - 1);
            LogUtil.MessageForce($"Debug - Attack timer and random event aligned to tick {nextDayBoundary}. Unpause to trigger both on the same tick.");
        }

        [DebugAction("Empire", "Change Settlement Defending Force", allowedGameStates = AllowedGameStates.Playing)]
        private static void ChangeAttackPlayerSettlementMilitaryForce()
        {
            FactionFC worldcomp = FindFC.FactionComp;
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (FCEvent evt in worldcomp.Events)
            {
                if (evt.def == FCEventDefOf.settlementBeingAttacked)
                {
                    list.Add(new DebugMenuOption(
                        worldcomp.ReturnSettlementByLocation(evt.location)?.Name ?? "Unknown",
                        DebugMenuOptionMode.Action, delegate
                        {
                            //when event is selected, select defending force to replace it with

                            List<DebugMenuOption> list2 = new List<DebugMenuOption>();
                            MilitaryOperation defOp = evt.linkedOperation;
                            WorldObject currentDefenderTarget = defOp?.targetObject;
                            WorldSettlementFC currentDefenderHome = defOp?.defender?.homeSettlement;
                            foreach (WorldSettlementFC settlement in worldcomp.settlements)
                            {
                                if (settlement.MilitaryComp == null || !settlement.MilitaryComp.IsMilitaryValid()) continue;
                                if (currentDefenderTarget is object && settlement.Name == currentDefenderTarget.Label) continue;
                                list2.Add(new DebugMenuOption(
                                    settlement.Name + " - " + settlement.settlementMilitaryLevel + " - Busy: " +
                                    settlement.MilitaryComp.militaryBusy, DebugMenuOptionMode.Action, delegate
                                    {
                                        if (settlement.MilitaryComp.IsMilitaryBusy() == false)
                                        {
                                            LogUtil.MessageForce($"Debug - Change Player Settlement - {currentDefenderHome?.Name ?? "Unknown"} to {settlement.Name}");
                                            MilitaryOperationsUtil.ChangeDefendingMilitaryForce(evt, settlement);
                                        }
                                    }
                                ));
                            }

                            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list2));
                        }
                    ));
                }
            }

            if (list.Any())
            {
                Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
            }
        }

        [DebugAction("Empire", "Upgrade Player Settlement", allowedGameStates = AllowedGameStates.Playing)]
        private static void UpgradePlayerSettlementx1() => UpgradePlayerSettlement();

        [DebugAction("Empire", "Upgrade Player Settlement x5", allowedGameStates = AllowedGameStates.Playing)]
        private static void UpgradePlayerSettlementx5() => UpgradePlayerSettlement(5);

        private static void UpgradePlayerSettlement(int times = 1)
        {
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (WorldSettlementFC settlement in FindFC.Settlements)
            {
                list.Add(new DebugMenuOption(settlement.Name, DebugMenuOptionMode.Action, delegate
                {
                    if (times > 0)
                    {
                        LogUtil.MessageForce("Debug - Upgrade Player Settlement x" + times + "- " + settlement.Name);
                    }
                    else
                    {
                        LogUtil.MessageForce("Debug - Downgrade Player Settlement x" + times + "- " + settlement.Name);
                    }
                    settlement.UpgradeSettlement(times);
                }
                ));
            }

            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }

        [DebugAction("Empire", "Flag Road Queue Update", allowedGameStates = AllowedGameStates.Playing)]
        private static void FlagRoadQueueUpdate()
        {
            LogUtil.MessageForce("Debug - Flag Road Queue Update");
            FindFC.RoadBuilder.FlagUpdateRoadQueues();
        }

        [DebugAction("Empire", "De-Level Player Settlement", allowedGameStates = AllowedGameStates.Playing)]
        private static void DelevelPlayerSettlement() => UpgradePlayerSettlement(-1);

        [DebugAction("Empire", "Dump settlement ticking comps", allowedGameStates = AllowedGameStates.Playing)]
        private static void DumpTickingComps()
            => WithSettlementChoice(s => s.DebugLogTickingComps());

        [DebugAction("Empire", "Reset All Military Squads", allowedGameStates = AllowedGameStates.Playing)]
        private static void ResetMilitarySquads()
        {
            LogUtil.MessageForce("Debug - Reset All Military Squads");
            MilitaryFC util = FindFC.Military;

            for (int i = util.mercenarySquads.Count - 1; i >= 0; i--)
            {
                MercenarySquadFC squad = util.mercenarySquads[i];
                if (squad.Deployment.HasLord)
                {
                    squad.Deployment.Map?.lordManager.RemoveLord(squad.Deployment.Lord);
                }

                foreach (Mercenary merc in squad.mercenaries.Concat(squad.AllSubPawns()).ToList())
                {
                    if (merc?.pawn != null && !merc.pawn.Destroyed)
                        merc.pawn.Destroy();
                }

                squad.settlement = null;

                util.mercenarySquads.RemoveAt(i);
            }

            foreach (WorldSettlementFC settlement in FindFC.Settlements)
            {
                settlement.MilitaryComp?.ReturnMilitary(false);
            }

            util.CheckMilitaryUtilForErrors();
        }

        [DebugAction("Empire", "Reset All Squad Cooldowns", allowedGameStates = AllowedGameStates.Playing)]
        private static void ResetAllSquadCooldowns()
        {
            LogUtil.MessageForce("Debug - Reset All Squad Cooldowns");
            FactionFC faction = FindFC.FactionComp;

            /* Squad-first model: a squad's cooldown is a cooldownMilitary ("traveling") FCEvent
             * linked to a MilitaryOperation in CooldownPending phase, plus the per-squad
             * nextAvailableTick gate. Clear all three layers. */

            // 1. Resolve every op stuck in a return-trip cooldown. Snapshot first — Resolve()
            //    unregisters the op, which mutates the manager's active list.
            int opsResolved = 0;
            MilitaryOperationManager mgr = FindFC.MilitaryManager;
            if (mgr is object)
            {
                List<MilitaryOperation> snapshot = new List<MilitaryOperation>(mgr.active);
                foreach (MilitaryOperation op in snapshot)
                {
                    if (op is null || op.phase != MilitaryOperationPhase.CooldownPending) continue;
                    op.Resolve();
                    opsResolved++;
                }
            }

            // 2. Sweep all cooldownMilitary ("traveling") events from the faction queue,
            //    including any orphans whose linked op is already gone.
            int eventsCleared = faction.RemoveEventsWhere(e => e.def == FCEventDefOf.cooldownMilitary);

            // 3. Clear the per-squad cooldown gate so squads are immediately available.
            int squadsCleared = 0;
            List<MercenarySquadFC> pool = faction.military?.mercenarySquads;
            if (pool is object)
            {
                for (int i = 0; i < pool.Count; i++)
                {
                    MercenarySquadFC squad = pool[i];
                    if (squad is null || squad.nextAvailableTick <= Find.TickManager.TicksGame) continue;
                    squad.nextAvailableTick = Find.TickManager.TicksGame;
                    squadsCleared++;
                }
            }

            LogUtil.MessageForce($"Debug - Resolved {opsResolved} cooldown op(s), cleared " +
                $"{eventsCleared} traveling event(s), reset {squadsCleared} squad cooldown(s)");
        }

        [DebugAction("Empire", "Reset Hire Laborers Cooldown", allowedGameStates = AllowedGameStates.Playing)]
        private static void ResetLaborerCooldown()
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction?.laborerCooldown is null) return;
            faction.laborerCooldown.tickLastUsed = -1;
            LogUtil.MessageForce("Debug - Reset Hire Laborers cooldown");
        }

        [DebugAction("Empire", "Clear Old Bills", allowedGameStates = AllowedGameStates.Playing)]
        private static void ClearOldBills()
        {
            FindFC.TaxLedger.ClearOldBills();
        }

        [DebugAction("Empire", "Clear All Events", allowedGameStates = AllowedGameStates.Playing)]
        private static void ClearAllEvents()
        {
            FindFC.EventManager?.Clear();
        }

        [DebugAction("Empire", "Clear All Bills", allowedGameStates = AllowedGameStates.Playing)]
        private static void ClearAllBills()
        {
            FindFC.TaxLedger.ClearAllBills();
        }

        [DebugAction("Empire", "Place 500 Silver", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void PlaceSilverFC() => SilverPlacer(500);

        [DebugAction("Empire", "Place 50000 Silver", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void PlaceALotOfSilverFC() => SilverPlacer(50000);

        private static void SilverPlacer(int amount)
        {
            Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = amount;
            GenPlace.TryPlaceThing(silver, UI.MouseCell(), Find.CurrentMap, ThingPlaceMode.Near);
        }

        private static void CallInAlliedForcesSelect()
        {
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (WorldSettlementFC settlement in FindFC.Settlements)
            {
                MercenarySquadFC squad = settlement.PrimaryStationedSquad;
                if (squad != null)
                {
                    list.Add(new DebugMenuOption(settlement.Name, DebugMenuOptionMode.Action, delegate
                    {
                        IncidentParms parms = new IncidentParms();
                        parms.target = Find.CurrentMap;
                        parms.faction = FindFC.EmpireFaction;
                        parms.podOpenDelay = 140;
                        parms.points = 999;
                        parms.raidArrivalModeForQuickMilitaryAid = true;
                        parms.raidNeverFleeIndividual = true;
                        parms.raidArrivalMode = PawnsArrivalModeDefOf.CenterDrop;
                        parms.raidStrategy = RaidStrategyDefOf.ImmediateAttackFriendly;

                        squad.CheckInitialization();
                        squad.UpdateSquadStats(settlement.settlementMilitaryLevel);

                        DebugTools.curTool = new DebugTool("Select Drop Position", delegate
                        {
                            IntVec3 dropPosition = UI.MouseCell();
                            parms.spawnCenter = dropPosition;

                            squad.Deployment.OrderLocation = dropPosition;

                            var debugEquippedPawns = squad.AllEquippedMercenaryPawns.ToList();
                            PawnsArrivalModeWorkerUtility.DropInDropPodsNearSpawnCenter(parms, debugEquippedPawns);
                            debugEquippedPawns.ForEach(pawn => pawn.ApplyIdeologyRitualWounds());
                            FindFC.MilitaryManager?.CreateDeployOp(squad, Find.CurrentMap.Tile);
                            DebugTools.curTool = null;
                        });
                    }));
                }
            }

            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }


        [DebugAction("Empire", "Call In Allied Forces", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void CallInAlliedForcesDebug() => CallInAlliedForcesSelect();


        [DebugAction("Empire", "Level Up Faction", allowedGameStates = AllowedGameStates.Playing)]
        private static void LevelUpFaction()
        {
            FactionFC faction = FindFC.FactionComp;
            faction.AddExperienceToFactionLevel(faction.factionXPGoal);
        }

        // ============================
        // Helper
        // ============================

        private static void WithSettlementChoice(Action<WorldSettlementFC> callback)
        {
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (WorldSettlementFC settlement in FindFC.Settlements)
            {
                WorldSettlementFC local = settlement;
                list.Add(new DebugMenuOption(
                    $"{local.Name} (Lv{local.settlementLevel})",
                    DebugMenuOptionMode.Action, () => callback(local)));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }

        private static void WithSquadChoice(Action<MercenarySquadFC> callback)
        {
            List<MercenarySquadFC> pool = FindFC.Military?.mercenarySquads;
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            if (pool is object)
            {
                for (int i = 0; i < pool.Count; i++)
                {
                    MercenarySquadFC squad = pool[i];
                    if (squad is null) continue;
                    MercenarySquadFC local = squad;
                    string where = local.settlement?.Name ?? "(unassigned)";
                    list.Add(new DebugMenuOption(
                        $"{local.DisplayName} @ {where} - {SquadHealthUtil.CountInjuredMercs(local)} injured merc(s)",
                        DebugMenuOptionMode.Action, () => callback(local)));
                }
            }
            if (list.Count == 0)
            {
                LogUtil.MessageForce("Debug - No squads available");
                return;
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }

        // ============================
        // Settlement Debug Actions
        // ============================

        [DebugAction("Empire", "Instant Build Building", allowedGameStates = AllowedGameStates.Playing)]
        private static void InstantBuildBuilding()
        {
            WithSettlementChoice(settlement =>
            {
                var comp = settlement.BuildingsComp;
                int slotCount = comp.NumBuildingSlots;
                List<DebugMenuOption> slots = new List<DebugMenuOption>();
                for (int i = 0; i < slotCount; i++)
                {
                    int localSlot = i;
                    BuildingFCDef current = comp.Buildings[i].def;
                    string slotLabel = $"Slot {i}: {current.label ?? current.defName}";
                    slots.Add(new DebugMenuOption(slotLabel, DebugMenuOptionMode.Action, () =>
                    {
                        List<DebugMenuOption> buildingOptions = new List<DebugMenuOption>();
                        foreach (BuildingFCDef bDef in DefDatabase<BuildingFCDef>.AllDefsListForReading)
                        {
                            if (bDef == BuildingFCDefOf.Empty || bDef == BuildingFCDefOf.Construction) continue;
                            BuildingFCDef localDef = bDef;
                            buildingOptions.Add(new DebugMenuOption(localDef.label ?? localDef.defName, DebugMenuOptionMode.Action, () =>
                            {
                                comp.ConstructBuilding(localDef, localSlot);
                                LogUtil.MessageForce($"Debug - Instant built {localDef.defName} in slot {localSlot} at {settlement.Name}");
                                Messages.Message($"Debug: Built {localDef.label ?? localDef.defName} in {settlement.Name}", MessageTypeDefOf.PositiveEvent, false);
                            }));
                        }
                        Find.WindowStack.Add(new Dialog_DebugOptionListLister(buildingOptions));
                    }));
                }
                Find.WindowStack.Add(new Dialog_DebugOptionListLister(slots));
            });
        }

        [DebugAction("Empire", "Log Settlement Stats", allowedGameStates = AllowedGameStates.Playing)]
        private static void LogSettlementStats()
        {
            foreach (WorldSettlementFC s in FindFC.Settlements)
            {
                LogUtil.MessageForce($"[{s.Name}] Lv{s.settlementLevel} | Happy:{s.happiness:F0} Loyal:{s.loyalty:F0} Unrest:{s.unrest:F0} Prosper:{s.prosperity:F0} | Workers:{s.workers}/{s.workersMax} Prisoners:{s.PrisonerComp?.prisonerList?.Count ?? 0}");
            }
        }

        [DebugAction("Empire", "Set Settlement Stat", allowedGameStates = AllowedGameStates.Playing)]
        private static void SetSettlementStat()
        {
            WithSettlementChoice(settlement =>
            {
                List<DebugMenuOption> stats = new List<DebugMenuOption>();
                string[] statNames = { "happiness", "loyalty", "unrest", "prosperity" };
                foreach (string stat in statNames)
                {
                    string localStat = stat;
                    stats.Add(new DebugMenuOption(localStat, DebugMenuOptionMode.Action, () =>
                    {
                        List<DebugMenuOption> values = new List<DebugMenuOption>();
                        foreach (int val in new[] { 0, 25, 50, 75, 100 })
                        {
                            int localVal = val;
                            values.Add(new DebugMenuOption(localVal.ToString(), DebugMenuOptionMode.Action, () =>
                            {
                                switch (localStat)
                                {
                                    case "happiness": settlement.happiness = localVal; break;
                                    case "loyalty": settlement.loyalty = localVal; break;
                                    case "unrest": settlement.unrest = localVal; break;
                                    case "prosperity": settlement.prosperity = localVal; break;
                                }
                                LogUtil.MessageForce($"Debug - Set {settlement.Name} {localStat} = {localVal}");
                            }));
                        }
                        Find.WindowStack.Add(new Dialog_DebugOptionListLister(values));
                    }));
                }
                Find.WindowStack.Add(new Dialog_DebugOptionListLister(stats));
            });
        }

        [DebugAction("Empire", "Set Settlement Stat (All Settlements)", allowedGameStates = AllowedGameStates.Playing)]
        private static void SetSettlementStatAll()
        {
            if (FindFC.Settlements.NullOrEmpty())
            {
                LogUtil.MessageForce("Debug - Set Settlement Stat (All Settlements): no settlements.");
                return;
            }

            List<DebugMenuOption> stats = new List<DebugMenuOption>();
            string[] statNames = { "happiness", "loyalty", "unrest", "prosperity" };
            foreach (string stat in statNames)
            {
                string localStat = stat;
                stats.Add(new DebugMenuOption(localStat, DebugMenuOptionMode.Action, () =>
                {
                    List<DebugMenuOption> values = new List<DebugMenuOption>();
                    foreach (int val in new[] { 0, 25, 50, 75, 100 })
                    {
                        int localVal = val;
                        values.Add(new DebugMenuOption(localVal.ToString(), DebugMenuOptionMode.Action, () =>
                        {
                            int count = 0;
                            foreach (WorldSettlementFC settlement in FindFC.Settlements)
                            {
                                switch (localStat)
                                {
                                    case "happiness": settlement.happiness = localVal; break;
                                    case "loyalty": settlement.loyalty = localVal; break;
                                    case "unrest": settlement.unrest = localVal; break;
                                    case "prosperity": settlement.prosperity = localVal; break;
                                }
                                count++;
                            }
                            LogUtil.MessageForce($"Debug - Set {localStat} = {localVal} for {count} settlement(s)");
                        }));
                    }
                    Find.WindowStack.Add(new Dialog_DebugOptionListLister(values));
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(stats));
        }

        [DebugAction("Empire", "Create Settlement (Instant)", allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        private static void CreateSettlementInstant()
        {
            List<WorldSettlementDef> defs = DefDatabase<WorldSettlementDef>.AllDefsListForReading;
            if (defs.Count == 1)
            {
                StartTilePickerForSettlement(defs[0]);
                return;
            }

            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (WorldSettlementDef def in defs)
            {
                WorldSettlementDef localDef = def;
                string layerLabel = localDef.planetLayers.Count > 0
                    ? localDef.planetLayers[0].defName
                    : "Surface";
                list.Add(new DebugMenuOption($"{localDef.LabelCap} [{layerLabel}]", DebugMenuOptionMode.Action,
                    () => StartTilePickerForSettlement(localDef)));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }

        private static void StartTilePickerForSettlement(WorldSettlementDef def)
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction is null)
            {
                LogUtil.MessageForce("Debug - FactionFC WorldComponent is null, cannot create settlement.");
                return;
            }

            faction.layersForTilePicker = def.planetLayers;

            Find.TilePicker.StartTargeting_NewTemp(
                delegate (PlanetTile tile)
                {
                    StringBuilder reason = new StringBuilder();
                    if (!WorldTileChecker.IsValidTileForNewSettlement(tile, def, reason))
                    {
                        Messages.Message($"Cannot settle here: {reason}", MessageTypeDefOf.RejectInput);
                        return false;
                    }
                    if (faction.CheckSettlementCaravansList(tile))
                    {
                        Messages.Message("A settlement caravan is already heading to this tile.", MessageTypeDefOf.RejectInput);
                        return false;
                    }
                    return true;
                },
                delegate (PlanetTile tile)
                {
                    PlanetTile settlementTile = def.GetTileForSettlement(tile);
                    LogUtil.MessageForce($"Debug - Create Settlement (Instant) at tile {settlementTile.Tile} with type {def.defName}");
                    ColonyUtil.CreatePlayerColonySettlement(settlementTile, def);
                    faction.layersForTilePicker = null;
                },
                allowEscape: true,
                noTileChosen: delegate
                {
                    faction.layersForTilePicker = null;
                },
                title: $"Select tile for {def.LabelCap}",
                showRandomButton: false,
                showNextButton: true,
                canCancel: true
            );
        }

        [DebugAction("Empire", "Create 10 Random Settlements", allowedGameStates = AllowedGameStates.Playing)]
        private static void CreateTenRandomSettlements()
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction is null)
            {
                LogUtil.MessageForce("Debug - FactionFC WorldComponent is null, cannot create settlements.");
                return;
            }

            WorldSettlementDef def = WorldSettlementDefOf.WorldSettlementDef_Surface;
            int created = 0;
            int maxAttempts = 500;

            for (int attempts = 0; attempts < maxAttempts && created < 10; attempts++)
            {
                PlanetTile tile = TileFinder.RandomSettlementTileFor(Find.WorldGrid.Surface, FindFC.EmpireFaction);
                if (tile == -1) continue;
                if (!WorldTileChecker.IsValidTileForNewSettlement(tile, def)) continue;

                ColonyUtil.CreatePlayerColonySettlement(tile, def);
                created++;
            }

            LogUtil.MessageForce($"Debug - Created {created}/10 random settlements.");
        }

        [DebugAction("Empire", "Create Settlement Per Resource (L5, workers maxed)", allowedGameStates = AllowedGameStates.Playing)]
        private static void CreateSettlementPerResource()
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction is null)
            {
                LogUtil.MessageForce("Debug - FactionFC WorldComponent is null, cannot create settlements.");
                return;
            }

            WorldSettlementDef def = WorldSettlementDefOf.WorldSettlementDef_Surface;
            const int maxAttempts = 300;

            int created = 0;
            int specialtyCount = 0;
            int skipped = 0;
            StringBuilder summary = new StringBuilder();

            foreach (ResourceTypeDef rtd in FactionCache.AllResourceTypeDefs)
            {
                /* Find a settleable surface tile, preferring a biome that boosts this resource (additive > 1) */
                PlanetTile bestTile = PlanetTile.Invalid;
                PlanetTile fallbackTile = PlanetTile.Invalid;
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    PlanetTile tile = TileFinder.RandomSettlementTileFor(Find.WorldGrid.Surface, FindFC.EmpireFaction);
                    if (!tile.Valid) continue;
                    if (!WorldTileChecker.IsValidTileForNewSettlement(tile, def)) continue;

                    BiomeResourceDef bres = DefDatabase<BiomeResourceDef>.GetNamed(tile.Tile.PrimaryBiome.defName, false)
                        ?? BiomeResourceDefOf.defaultBiome;
                    ResourceAvailability avail = bres.GetBiomeResource(rtd);
                    if (avail is null) continue; /* biome blocks this resource entirely */

                    if (!fallbackTile.Valid) fallbackTile = tile;
                    if (avail.additive > 1)
                    {
                        bestTile = tile;
                        break;
                    }
                }

                bool usedSpecialty = bestTile.Valid;
                PlanetTile chosen = usedSpecialty ? bestTile : fallbackTile;
                if (!chosen.Valid)
                {
                    LogUtil.Warning($"Create Settlement Per Resource - no placeable tile found for {rtd.defName}, skipping.");
                    summary.AppendLine($"  {rtd.defName}: SKIPPED (no tile)");
                    skipped++;
                    continue;
                }

                WorldSettlementFC s = ColonyUtil.CreatePlayerColonySettlement(chosen, def);
                created++;
                if (usedSpecialty) specialtyCount++;

                /* Level to 5 (clamped to FCSettings.settlementMaxLevel / def.maxSettlementLevel) */
                s.UpgradeSettlement(5 - s.settlementLevel);

                /* Load the whole worker pool onto this settlement's matching resource */
                ResourceFC target = s.GetResource(rtd);
                int cap = (int)s.workersUltraMax;
                string biomeNote = usedSpecialty ? "specialty biome" : "base biome";
                if (target is null)
                {
                    LogUtil.Warning($"Create Settlement Per Resource - {s.Name} has no {rtd.defName} resource (biome/tech restricted), skipping worker assignment.");
                    summary.AppendLine($"  {rtd.defName}: {s.Name} L{s.settlementLevel} ({biomeNote}), no workers (resource absent)");
                }
                else
                {
                    /* Drain any pre-assigned workers, then fill the target resource to the cap */
                    foreach (ResourceFC r in s.Resources) s.IncreaseWorkers(r, -(cap + 1));
                    s.IncreaseWorkers(target, cap);
                    summary.AppendLine($"  {rtd.defName}: {s.Name} L{s.settlementLevel} ({biomeNote}), {target.assignedWorkers} workers");
                }
            }

            LogUtil.MessageForce($"Debug - Create Settlement Per Resource: created {created} settlement(s) ({specialtyCount} on specialty biomes, {skipped} skipped).\n{summary}");
        }

        [DebugAction("Empire", "Remove Player Settlement", allowedGameStates = AllowedGameStates.Playing)]
        private static void RemovePlayerSettlement()
        {
            WithSettlementChoice(settlement =>
            {
                LogUtil.MessageForce($"Debug - Remove Player Settlement - {settlement.Name}");
                ColonyUtil.RemovePlayerSettlement(settlement);
            });
        }

        [DebugAction("Empire", "Add Stat Modifier", allowedGameStates = AllowedGameStates.Playing)]
        private static void AddStatModifier()
        {
            WithSettlementChoice(settlement =>
            {
                List<DebugMenuOption> list = new List<DebugMenuOption>();
                foreach (FCStatDef stat in DefDatabase<FCStatDef>.AllDefsListForReading)
                {
                    FCStatDef localStat = stat;
                    list.Add(new DebugMenuOption(localStat.defName, DebugMenuOptionMode.Action, () =>
                    {
                        List<DebugMenuOption> values = new List<DebugMenuOption>();
                        double[] options = localStat.aggregation == FCStatAggregation.Additive
                            ? new double[] { -10, -5, -1, 1, 5, 10 }
                            : new double[] { 0.5, 0.75, 1.25, 1.5, 2.0 };
                        foreach (double val in options)
                        {
                            double localVal = val;
                            string label = localStat.aggregation == FCStatAggregation.Additive
                                ? (localVal > 0 ? $"+{localVal}" : $"{localVal}")
                                : $"x{localVal}";
                            values.Add(new DebugMenuOption(label, DebugMenuOptionMode.Action, () =>
                            {
                                settlement.AddStatModifiers(
                                    new List<FCStatModifier> { new FCStatModifier { stat = localStat, value = localVal } },
                                    "debug", "Debug: " + localStat.defName);
                                LogUtil.MessageForce($"Debug - Added stat {localStat.defName} = {localVal} to {settlement.Name}");
                            }));
                        }
                        Find.WindowStack.Add(new Dialog_DebugOptionListLister(values));
                    }));
                }
                Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
            });
        }

        [DebugAction("Empire", "Clear Debug Stat Modifiers", allowedGameStates = AllowedGameStates.Playing)]
        private static void ClearDebugStatModifiers()
        {
            WithSettlementChoice(settlement =>
            {
                settlement.RemoveStatModifiersBySource("debug");
                LogUtil.MessageForce($"Debug - Cleared debug stat modifiers from {settlement.Name}");
            });
        }

        [DebugAction("Empire", "Log All Stat Values", allowedGameStates = AllowedGameStates.Playing)]
        private static void LogAllStatValues()
        {
            WithSettlementChoice(settlement =>
            {
                FactionFC faction = FindFC.FactionComp;
                LogUtil.MessageForce($"--- Stat Values for {settlement.Name} ---");
                int defaultCount = 0;
                foreach (FCStatDef stat in DefDatabase<FCStatDef>.AllDefsListForReading)
                {
                    if (stat.appliesToSettlements)
                    {
                        double final = faction.GetStatValue(stat, settlement);
                        double settlementPart = settlement.GetSettlementStatValue(stat);
                        double factionPart = faction.GetFactionStatValue(stat);
                        if (Math.Abs(final - stat.IdentityValue) < 0.001
                            && Math.Abs(settlementPart - stat.IdentityValue) < 0.001
                            && Math.Abs(factionPart - stat.IdentityValue) < 0.001)
                        {
                            defaultCount++;
                            continue;
                        }
                        string agg = stat.aggregation == FCStatAggregation.Additive ? "Add" : "Mult";
                        LogUtil.MessageForce($"  {stat.defName}: Final={final:F2} | Settlement={settlementPart:F2} | Faction={factionPart:F2} ({agg})");
                    }
                    else
                    {
                        double val = faction.GetFactionStatValue(stat);
                        if (Math.Abs(val - stat.IdentityValue) < 0.001)
                        {
                            defaultCount++;
                            continue;
                        }
                        LogUtil.MessageForce($"  {stat.defName}: {val:F2} (faction-only)");
                    }
                }
                LogUtil.MessageForce($"  ({defaultCount} stats at default value)");
            });
        }

        [DebugAction("Empire", "Log Stat Breakdown", allowedGameStates = AllowedGameStates.Playing)]
        private static void LogStatBreakdown()
        {
            WithSettlementChoice(settlement =>
            {
                List<DebugMenuOption> list = new List<DebugMenuOption>();
                foreach (FCStatDef stat in DefDatabase<FCStatDef>.AllDefsListForReading)
                {
                    FCStatDef localStat = stat;
                    list.Add(new DebugMenuOption(localStat.defName, DebugMenuOptionMode.Action, () =>
                    {
                        FactionFC faction = FindFC.FactionComp;
                        string agg = localStat.aggregation == FCStatAggregation.Additive ? "Additive" : "Multiplicative";
                        LogUtil.MessageForce($"--- Stat Breakdown: {localStat.defName} ({agg}, default={localStat.IdentityValue}) ---");

                        // Settlement-level modifiers
                        foreach (FCStatModifier mod in settlement.StatModifiers)
                        {
                            if (mod.stat == localStat)
                                LogUtil.MessageForce($"  Settlement modifier: {mod.value:F2}");
                        }

                        // IStatModifierProvider comps
                        foreach (WorldObjectComp comp in settlement.AllComps)
                        {
                            if (comp is IStatModifierProvider provider)
                            {
                                double compVal = provider.GetStatModifier(localStat);
                                if (Math.Abs(compVal - (localStat.aggregation == FCStatAggregation.Additive ? 0 : 1)) > 0.001)
                                    LogUtil.MessageForce($"  Comp ({comp.GetType().Name}): {compVal:F2}");
                            }
                        }
                        LogUtil.MessageForce($"  Settlement partial = {settlement.GetSettlementStatValue(localStat):F2}");

                        // Faction-level (policies + traits)
                        foreach (FCPolicy p in FindFC.PolicyManager.policies)
                        {
                            if (p?.def == null) continue;
                            foreach (FCStatModifier mod in p.def.statModifiers)
                            {
                                if (mod.stat == localStat)
                                    LogUtil.MessageForce($"  Policy ({p.def.defName}): {mod.value:F2}");
                            }
                        }
                        foreach (FCPolicy p in FindFC.PolicyManager.factionTraits)
                        {
                            if (p?.def == null || p.def == FCPolicyDefOf.empty) continue;
                            foreach (FCStatModifier mod in p.def.statModifiers)
                            {
                                if (mod.stat == localStat)
                                    LogUtil.MessageForce($"  Trait ({p.def.defName}): {mod.value:F2}");
                            }
                        }
                        LogUtil.MessageForce($"  Faction partial = {faction.GetFactionStatValue(localStat):F2}");

                        // Behavior contributions
                        foreach (FCPolicyBehavior b in FindFC.PolicyManager.CachedBehaviors)
                        {
                            string desc = b.GetStatDescription(localStat, settlement);
                            if (!desc.NullOrEmpty())
                                LogUtil.MessageForce($"  Behavior ({b.GetType().Name}): {desc.TrimEnd()}");
                        }

                        double final = faction.GetStatValue(localStat, settlement);
                        LogUtil.MessageForce($"  FINAL = {final:F2}");
                    }));
                }
                Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
            });
        }

        [DebugAction("Empire", "Log Faction Stats", allowedGameStates = AllowedGameStates.Playing)]
        private static void LogFactionStatValues()
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction == null) return;

            LogUtil.MessageForce("--- Faction-Level Stat Values ---");
            int defaultCount = 0;
            foreach (FCStatDef stat in DefDatabase<FCStatDef>.AllDefsListForReading)
            {
                double val = faction.GetFactionStatValue(stat);
                if (Math.Abs(val - stat.IdentityValue) < 0.001)
                {
                    defaultCount++;
                    continue;
                }
                string agg = stat.aggregation == FCStatAggregation.Additive ? "Add" : "Mult";
                LogUtil.MessageForce($"  {stat.defName} = {val:F2} ({agg}, default={stat.IdentityValue})");
            }
            LogUtil.MessageForce($"  ({defaultCount} stats at default value)");
        }

        // ============================
        // Military Debug Actions
        // ============================

        [DebugAction("Empire", "Log Military Status", allowedGameStates = AllowedGameStates.Playing)]
        private static void LogMilitaryStatus()
        {
            foreach (WorldSettlementFC s in FindFC.Settlements)
            {
                var comp = s.MilitaryComp;
                if (comp == null)
                {
                    LogUtil.MessageForce($"[{s.Name}] MilitaryComp: null");
                    continue;
                }
                List<MercenarySquadFC> stationed = s.StationedSquads;
                string squadInfo;
                if (stationed.Count == 0)
                {
                    squadInfo = "No squad";
                }
                else
                {
                    int deployedCount = 0;
                    for (int i = 0; i < stationed.Count; i++)
                    {
                        if (stationed[i] != null && stationed[i].Deployment.IsPhysicallyDeployed()) deployedCount++;
                    }
                    squadInfo = $"Squads:{stationed.Count} Deployed:{deployedCount} Job:{comp.militaryJob}";
                }
                LogUtil.MessageForce($"[{s.Name}] MilLv:{s.settlementMilitaryLevel} Busy:{comp.militaryBusy} | {squadInfo}");
            }
        }

        [DebugAction("Empire", "Force Return Settlement Military", allowedGameStates = AllowedGameStates.Playing)]
        private static void ForceReturnSettlementMilitary()
        {
            WithSettlementChoice(settlement =>
            {
                if (settlement.MilitaryComp != null)
                {
                    settlement.MilitaryComp.ReturnMilitary(true);
                    LogUtil.MessageForce($"Debug - Force returned military for {settlement.Name}");
                }
                else
                {
                    LogUtil.MessageForce($"Debug - {settlement.Name} has no MilitaryComp");
                }
            });
        }

        [DebugAction("Empire", "Clear Orphaned Deployments", allowedGameStates = AllowedGameStates.Playing)]
        private static void ForceCheckOrphanedDeploys()
        {
            int cleared = 0;
            foreach (WorldSettlementFC settlement in FindFC.Settlements)
            {
                var comp = settlement.MilitaryComp;
                if (comp is null) continue;
                if (!comp.militaryBusy || comp.militaryJob != MilitaryJobDefOf.DefendFriendlySettlement) continue;
                if (comp.IsStaleDeploy())
                {
                    comp.ReturnMilitary(false);
                    cleared++;
                    LogUtil.MessageForce($"  Cleared stale DefendFriendlySettlement on {settlement.Name}");
                }
            }
            LogUtil.MessageForce($"Clear Orphaned Deployments: cleared {cleared}.");
        }

        [DebugAction("Empire", "Run Military Error Check", allowedGameStates = AllowedGameStates.Playing)]
        private static void RunMilitaryErrorCheck()
        {
            LogUtil.MessageForce("Debug - Running military error check");
            FindFC.Military.CheckMilitaryUtilForErrors();
            LogUtil.MessageForce("Debug - Military error check complete");
        }

        [DebugAction("Empire", "Heal Squad to Full", allowedGameStates = AllowedGameStates.Playing)]
        private static void HealSquadToFull()
        {
            WithSquadChoice(squad =>
            {
                int healed = 0;
                IEnumerable<Mercenary> all = (squad.mercenaries ?? Enumerable.Empty<Mercenary>())
                    .Concat(squad.AllSubPawns());
                foreach (Mercenary merc in all)
                {
                    // Skip empty slots and dead/destroyed pawns — there is no revival system,
                    // dead mercs are already empty slots. HealPawn does merc.pawn.health.Reset().
                    if (merc?.pawn is null || merc.pawn.Dead || merc.pawn.Destroyed) continue;
                    SquadHealthUtil.HealPawn(merc);
                    healed++;
                }
                LogUtil.MessageForce($"Debug - Healed {healed} pawn(s) in squad {squad.DisplayName}");
            });
        }

        // ============================
        // Faction / Economy Debug Actions
        // ============================

        [DebugAction("Empire", "Log Faction Status", allowedGameStates = AllowedGameStates.Playing)]
        private static void LogFactionStatus()
        {
            FactionFC f = FindFC.FactionComp;
            LogUtil.MessageForce($"Faction Lv{f.factionLevel} | XP:{f.factionXPCurrent:F0}/{f.factionXPGoal:F0}");
            LogUtil.MessageForce($"Settlements:{f.settlements.Count} | Income:{f.income:F0} Upkeep:{f.upkeep:F0} Profit:{f.profit:F0}");
            LogUtil.MessageForce($"TaxDue:{f.taxLedger.nextTaxDueTick - Find.TickManager.TicksGame} ticks | MilDue:{f.militaryTimeDue - Find.TickManager.TicksGame} ticks");
            LogUtil.MessageForce($"AvgHappy:{f.averageHappiness:F0} AvgLoyal:{f.averageLoyalty:F0} AvgUnrest:{f.averageUnrest:F0} AvgProsper:{f.averageProsperity:F0}");
            LogUtil.MessageForce($"Policies:{FindFC.PolicyManager.policies.Count} | Traits:{FindFC.PolicyManager.factionTraits.Count} | ResearchPool:{f.GetResourcePoolValue(ResourceTypeDefOf.RTD_Research):F0}");
            if (FindFC.PolicyManager.factionTraits.Any())
            {
                LogUtil.MessageForce($"Trait list: {FindFC.PolicyManager.factionTraits.Select(t => t.def?.defName).ToCommaList()}");
            }
            if (FindFC.PolicyManager.policies.Any())
            {
                LogUtil.MessageForce($"Policy list: {FindFC.PolicyManager.policies.Select(t => t.def?.defName).ToCommaList()}");
            }
        }

        [DebugAction("Empire", "Add Faction XP", allowedGameStates = AllowedGameStates.Playing)]
        private static void AddFactionXP()
        {
            FactionFC faction = FindFC.FactionComp;
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (int amount in new[] { 100, 500, 1000 })
            {
                int localAmount = amount;
                list.Add(new DebugMenuOption($"+{localAmount} XP", DebugMenuOptionMode.Action, () =>
                {
                    faction.AddExperienceToFactionLevel(localAmount);
                    LogUtil.MessageForce($"Debug - Added {localAmount} XP (now {faction.factionXPCurrent:F0}/{faction.factionXPGoal:F0})");
                }));
            }
            float remaining = faction.factionXPGoal - faction.factionXPCurrent;
            list.Add(new DebugMenuOption($"+{remaining:F0} XP (to next level)", DebugMenuOptionMode.Action, () =>
            {
                faction.AddExperienceToFactionLevel(remaining);
                LogUtil.MessageForce($"Debug - Added {remaining:F0} XP to reach next level");
            }));
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }

        [DebugAction("Empire", "Proc Tax Due", allowedGameStates = AllowedGameStates.Playing)]
        private static void ProcTaxDue()
        {
            LogUtil.MessageForce("Debug - Proc TaxTimeDue");
            FindFC.TaxLedger.nextTaxDueTick = Find.TickManager.TicksGame + 1;
        }

        [DebugAction("Empire", "Snapshot Tax Production Now", allowedGameStates = AllowedGameStates.Playing)]
        private static void SnapshotTaxProductionNow()
        {
            LogUtil.MessageForce("Debug - Snapshot Tax Production (1 day)");
            foreach (WorldSettlementFC s in FindFC.Settlements)
                s.AccumulateDailyProduction();
        }

        [DebugAction("Empire", "Snapshot Tax Production x10", allowedGameStates = AllowedGameStates.Playing)]
        private static void SnapshotTaxProductionTenTimes()
        {
            LogUtil.MessageForce("Debug - Snapshot Tax Production (10 days)");
            foreach (WorldSettlementFC s in FindFC.Settlements)
                for (int i = 0; i < 10; i++)
                    s.AccumulateDailyProduction();
        }

        [DebugAction("Empire", "Log Resource Production", allowedGameStates = AllowedGameStates.Playing)]
        private static void LogResourceProduction()
        {
            WithSettlementChoice(settlement =>
            {
                LogUtil.MessageForce($"--- Resources for {settlement.Name} ---");
                foreach (ResourceFC r in settlement.Resources)
                {
                    LogUtil.MessageForce($"  {r.def.defName}: Workers:{r.assignedWorkers} Base:{r.productionBase:F2} Mult:{r.productionMult:F2} Production:{r.production:F2} Income:{r.actualIncome:F2}");
                }
            });
        }

        // ============================
        // Events Debug Actions
        // ============================

        [DebugAction("Empire", "Force Trigger Event", allowedGameStates = AllowedGameStates.Playing)]
        private static void ForceTriggerEvent()
        {
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (FCEvent evt in FindFC.Events)
            {
                FCEvent localEvt = evt;
                int ticksLeft = localEvt.timeTillTrigger - Find.TickManager.TicksGame;
                list.Add(new DebugMenuOption(
                    $"{localEvt.def.defName} (in {ticksLeft} ticks)",
                    DebugMenuOptionMode.Action, () =>
                    {
                        localEvt.timeTillTrigger = Find.TickManager.TicksGame + 1;
                        LogUtil.MessageForce($"Debug - Force triggering {localEvt.def.defName}");
                    }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }

        /* Mirror of "Force Trigger Event" that lets you pick which follow-up of a queued
         * chain event to spawn, bypassing the random roll in FCEventMaker.ProcessEvents. */
        [DebugAction("Empire", "Force Trigger Followup", allowedGameStates = AllowedGameStates.Playing)]
        private static void ForceTriggerFollowup()
        {
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (FCEvent evt in FindFC.Events)
            {
                if (!evt.def.HasFollowUp) continue;
                FCEvent localEvt = evt;
                int ticksLeft = localEvt.timeTillTrigger - Find.TickManager.TicksGame;
                list.Add(new DebugMenuOption(
                    $"{localEvt.def.defName} (in {ticksLeft} ticks)",
                    DebugMenuOptionMode.Action, () => Find.WindowStack.Add(
                        new Dialog_DebugOptionListLister(BuildFollowupOptions(localEvt)))));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }

        // Builds the second-level menu of follow-up branches for a queued chain event,
        // mirroring the branch semantics of FCEventMaker.ProcessEvents.
        private static List<DebugMenuOption> BuildFollowupOptions(FCEvent parent)
        {
            List<DebugMenuOption> options = new List<DebugMenuOption>();
            FCEventDef def = parent.def;
            if (def.splitEventFollows)
            {
                if (def.followingEvent is object)
                    options.Add(MakeFollowupOption(parent, def.followingEvent,
                        $"{def.followingEvent.defName} (split {def.splitEventChance}%)"));
                if (def.followingEvent2 is object)
                    options.Add(MakeFollowupOption(parent, def.followingEvent2,
                        $"{def.followingEvent2.defName} (split {100 - def.splitEventChance}%)"));
            }
            else if (def.followingEvent is object)
            {
                options.Add(MakeFollowupOption(parent, def.followingEvent,
                    $"{def.followingEvent.defName} (guaranteed)"));
            }
            return options;
        }

        // Spawns the chosen follow-up immediately, reusing the spawn logic from
        // FCEventMaker.ProcessEvents (MakeRandomEvent + AddEvent + letter).
        private static DebugMenuOption MakeFollowupOption(FCEvent parent, FCEventDef target, string label)
        {
            return new DebugMenuOption(label, DebugMenuOptionMode.Action, () =>
            {
                List<WorldSettlementFC> settlements = parent.def.settlementsCarryOver
                    ? parent.settlementTraitLocations
                    : null;
                FCEvent tempEvent = FCEventMaker.MakeRandomEvent(target, settlements);
                if (tempEvent != null)
                {
                    FindFC.EventManager.AddEvent(tempEvent);
                    Find.LetterStack.ReceiveLetter(tempEvent.def.label,
                        FCEventMaker.BuildEventLetterBody(tempEvent), LetterDefOf.NeutralEvent);
                }
                LogUtil.MessageForce($"Debug - Force triggering followup {target.defName} of {parent.def.defName}");
            });
        }

        // ============================
        // Road Debug Actions
        // ============================

        [DebugAction("Empire", "Build Road Segment Now", allowedGameStates = AllowedGameStates.Playing)]
        private static void BuildRoadSegmentNow()
        {
            var rb = FindFC.RoadBuilder;
            if (rb.roadDef == null)
            {
                LogUtil.MessageForce("Debug - No road research completed yet");
                return;
            }
            if (rb.roadQueue == null)
            {
                LogUtil.MessageForce("Debug - No road queue exists");
                return;
            }
            rb.roadQueue.nextRoadTick = Find.TickManager.TicksGame;
            rb.roadQueue.ProcessOnePath();
            bool built = rb.roadQueue.BuildRoadSegments();
            LogUtil.MessageForce($"Debug - Build Road Segment Now: {(built ? "segment built" : "no segment to build")}");
        }

        [DebugAction("Empire", "Build 10 Road Segments", allowedGameStates = AllowedGameStates.Playing)]
        private static void BuildTenRoadSegments()
        {
            var rb = FindFC.RoadBuilder;
            if (rb.roadDef is null)
            {
                LogUtil.MessageForce("Debug - No road research completed yet");
                return;
            }
            if (rb.roadQueue is null)
            {
                LogUtil.MessageForce("Debug - No road queue exists");
                return;
            }

            int built = 0;
            for (int i = 0; i < 10; i++)
            {
                rb.roadQueue.nextRoadTick = Find.TickManager.TicksGame;
                rb.roadQueue.ProcessOnePath();
                if (!rb.roadQueue.BuildRoadSegments()) break;
                built++;
            }
            LogUtil.MessageForce($"Debug - Built {built}/10 road segments.");
        }

        // ============================
        // Policy Debug Actions
        // ============================

        [DebugAction("Empire", "Enact Policy (Debug)", allowedGameStates = AllowedGameStates.Playing)]
        private static void EnactPolicyDebug()
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction == null) return;

            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (FCPolicyDef def in DefDatabase<FCPolicyDef>.AllDefsListForReading)
            {
                if (def == FCPolicyDefOf.empty) continue;
                if (def.category != FCPolicyCategory.Core) continue;
                FCPolicyDef local = def;
                string status = FindFC.PolicyManager.policies.Any(p => p.def == local) ? " [ACTIVE]" : "";
                list.Add(new DebugMenuOption($"{local.defName}{status}", DebugMenuOptionMode.Action, () =>
                {
                    var policy = new FCPolicy(local);
                    FindFC.PolicyManager.policies.Add(policy);
                    FindFC.PolicyManager.RebuildBehaviorCache();
                    LogUtil.MessageForce($"Debug - Enacted policy: {local.defName} (behavior: {(policy.behavior != null ? policy.behavior.GetType().Name : "none")})");
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }

        [DebugAction("Empire", "Enact Trait (Debug)", allowedGameStates = AllowedGameStates.Playing)]
        private static void EnactTraitDebug()
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction == null) return;

            List<DebugMenuOption> traitList = new List<DebugMenuOption>();
            foreach (FCPolicyDef def in DefDatabase<FCPolicyDef>.AllDefsListForReading)
            {
                if (def == FCPolicyDefOf.empty) continue;
                if (def.category != FCPolicyCategory.Trait) continue;
                FCPolicyDef local = def;
                traitList.Add(new DebugMenuOption(local.defName, DebugMenuOptionMode.Action, () =>
                {
                    List<DebugMenuOption> slotList = new List<DebugMenuOption>();
                    for (int i = 0; i < FindFC.PolicyManager.factionTraits.Count; i++)
                    {
                        int slot = i;
                        string current = FindFC.PolicyManager.factionTraits[slot]?.def?.defName ?? "empty";
                        slotList.Add(new DebugMenuOption($"Slot {slot} [{current}]", DebugMenuOptionMode.Action, () =>
                        {
                            if (FindFC.PolicyManager.factionTraits[slot]?.behavior != null)
                            {
                                try { FindFC.PolicyManager.factionTraits[slot].behavior.OnRemoved(faction); }
                                catch (Exception e) { LogUtil.Error($"OnRemoved error: {e}"); }
                            }
                            var trait = new FCPolicy(local);
                            FindFC.PolicyManager.factionTraits[slot] = trait;
                            FindFC.PolicyManager.RebuildBehaviorCache();
                            LogUtil.MessageForce($"Debug - Set trait slot {slot} to: {local.defName}");
                        }));
                    }
                    Find.WindowStack.Add(new Dialog_DebugOptionListLister(slotList));
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(traitList));
        }

        [DebugAction("Empire", "Instantly Enact Edict", allowedGameStates = AllowedGameStates.Playing)]
        private static void InstantlyEnactEdict()
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction == null) return;

            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (FCPolicyDef def in DefDatabase<FCPolicyDef>.AllDefsListForReading)
            {
                if (!def.IsEdict) continue;
                FCPolicyDef local = def;
                FCPolicy existing;
                FindFC.PolicyManager.edicts.TryGetValue(local.category, out existing);
                string status = (existing != null && existing.def == local) ? " [ACTIVE]" : "";
                list.Add(new DebugMenuOption($"[{local.category}] {local.LabelCap}{status}", DebugMenuOptionMode.Action, () =>
                {
                    FindFC.PolicyManager.EnactEdict(local);
                    FCPolicy edict;
                    if (FindFC.PolicyManager.edicts.TryGetValue(local.category, out edict))
                    {
                        edict.timeEnacted = Find.TickManager.TicksGame - local.enactDuration;
                        faction.InvalidateFactionStatCache();
                        faction.DirtyFactionProfitCache();
                    }
                    LogUtil.MessageForce($"Debug - Instantly enacted edict: {local.defName}");
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }

        [DebugAction("Empire", "Log Policy Behavior State", allowedGameStates = AllowedGameStates.Playing)]
        private static void LogPolicyBehaviorState()
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction == null) return;

            LogUtil.MessageForce("=== Policy Behavior State ===");

            foreach (FCPolicy p in FindFC.PolicyManager.policies)
            {
                if (p?.behavior == null)
                {
                    LogUtil.MessageForce($"[Policy] {p?.def?.defName ?? "null"}: no behavior");
                    continue;
                }
                LogBehaviorState("Policy", p);
            }

            for (int i = 0; i < FindFC.PolicyManager.factionTraits.Count; i++)
            {
                FCPolicy p = FindFC.PolicyManager.factionTraits[i];
                if (p?.def == null || p.def == FCPolicyDefOf.empty) continue;
                if (p.behavior == null)
                {
                    LogUtil.MessageForce($"[Trait {i}] {p.def.defName}: no behavior");
                    continue;
                }
                LogBehaviorState($"Trait {i}", p);
            }
        }

        private static void LogBehaviorState(string prefix, FCPolicy p)
        {
            string behaviorType = p.behavior.GetType().Name;

            if (p.behavior is FCPolicyBehavior_Militaristic mil)
            {
                LogUtil.MessageForce($"[{prefix}] {p.def.defName} ({behaviorType}): extraSquadCooldown Ready={mil.DebugCooldownReady()} Days={mil.DebugCooldownDays():F1}");
            }
            else if (p.behavior is FCPolicyBehavior_Pacifist pac)
            {
                LogUtil.MessageForce($"[{prefix}] {p.def.defName} ({behaviorType}): diplomatCooldown Ready={pac.DebugCooldownReady()} Days={pac.DebugCooldownDays():F1}");
            }
            else if (p.behavior is FCPolicyBehavior_Feudal feu)
            {
                LogUtil.MessageForce($"[{prefix}] {p.def.defName} ({behaviorType}): mercenaryCooldown Ready={feu.DebugCooldownReady()} Days={feu.DebugCooldownDays():F1}");
            }
            else if (p.behavior is FCPolicyBehavior_Egalitarian egal)
            {
                LogUtil.MessageForce($"[{prefix}] {p.def.defName} ({behaviorType}): taxBreaks={egal.DebugTaxBreakCount()} active={egal.DebugActiveTaxBreakCount()}");
            }
            else if (p.behavior is FCPolicyBehavior_Mercantile merc)
            {
                int ticksUntil = merc.DebugNextCaravanTick() - Find.TickManager.TicksGame;
                float daysUntil = ticksUntil / (float)GenDate.TicksPerDay;
                LogUtil.MessageForce($"[{prefix}] {p.def.defName} ({behaviorType}): nextCaravan in {daysUntil:F1} days ({ticksUntil} ticks)");
            }
            else
            {
                LogUtil.MessageForce($"[{prefix}] {p.def.defName} ({behaviorType}): (no inspectable state)");
            }
        }

        [DebugAction("Empire", "Force Policy Cooldowns Ready", allowedGameStates = AllowedGameStates.Playing)]
        private static void ForcePolicyCooldownsReady()
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction == null) return;

            int count = 0;
            foreach (FCPolicyBehavior b in FindFC.PolicyManager.CachedBehaviors)
            {
                if (b is FCPolicyBehavior_Militaristic mil) { mil.DebugResetCooldown(); count++; }
                else if (b is FCPolicyBehavior_Pacifist pac) { pac.DebugResetCooldown(); count++; }
                else if (b is FCPolicyBehavior_Feudal feu) { feu.DebugResetCooldown(); count++; }
                else if (b is FCPolicyBehavior_Mercantile merc) { merc.DebugResetNextCaravan(); count++; }
            }
            LogUtil.MessageForce($"Debug - Reset {count} policy cooldowns to ready");
        }

        [DebugAction("Empire", "Trigger Policy Hook", allowedGameStates = AllowedGameStates.Playing)]
        private static void TriggerPolicyHook()
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction == null) return;

            List<DebugMenuOption> hookList = new List<DebugMenuOption>();

            hookList.Add(new DebugMenuOption("OnSettlementCreated", DebugMenuOptionMode.Action, () =>
            {
                WithSettlementChoice(settlement =>
                {
                    FindFC.PolicyManager.ForEachBehavior(b => b.OnSettlementCreated(faction, settlement));
                    LogUtil.MessageForce($"Debug - Triggered OnSettlementCreated on {settlement.Name}");
                });
            }));

            hookList.Add(new DebugMenuOption("OnSettlementRemoved", DebugMenuOptionMode.Action, () =>
            {
                WithSettlementChoice(settlement =>
                {
                    FindFC.PolicyManager.ForEachBehavior(b => b.OnSettlementRemoved(faction, settlement));
                    LogUtil.MessageForce($"Debug - Triggered OnSettlementRemoved on {settlement.Name}");
                });
            }));

            hookList.Add(new DebugMenuOption("OnSquadDeployed", DebugMenuOptionMode.Action, () =>
            {
                WithSettlementChoice(settlement =>
                {
                    // Debug trigger has no real op — behaviors that inspect op must null-check.
                    FindFC.PolicyManager.ForEachBehavior(b => b.OnSquadDeployed(faction, null, settlement, false));
                    LogUtil.MessageForce($"Debug - Triggered OnSquadDeployed on {settlement.Name}");
                });
            }));

            hookList.Add(new DebugMenuOption("OnSquadRecalled", DebugMenuOptionMode.Action, () =>
            {
                WithSettlementChoice(settlement =>
                {
                    FindFC.PolicyManager.ForEachBehavior(b => b.OnSquadRecalled(faction, null, settlement));
                    LogUtil.MessageForce($"Debug - Triggered OnSquadRecalled on {settlement.Name}");
                });
            }));

            hookList.Add(new DebugMenuOption("OnTaxCollected", DebugMenuOptionMode.Action, () =>
            {
                WithSettlementChoice(settlement =>
                {
                    FindFC.PolicyManager.ForEachBehavior(b => b.OnTaxCollected(faction, settlement));
                    LogUtil.MessageForce($"Debug - Triggered OnTaxCollected on {settlement.Name}");
                });
            }));

            hookList.Add(new DebugMenuOption("OnSettlementCostPaid", DebugMenuOptionMode.Action, () =>
            {
                FindFC.PolicyManager.ForEachBehavior(b => b.OnSettlementCostPaid(faction));
                LogUtil.MessageForce("Debug - Triggered OnSettlementCostPaid");
            }));

            Find.WindowStack.Add(new Dialog_DebugOptionListLister(hookList));
        }

        // ============================
        // Road Debug Actions
        // ============================

        [DebugAction("Empire", "Log Road Builder Status", allowedGameStates = AllowedGameStates.Playing)]
        private static void LogRoadBuilderStatus()
        {
            var rb = FindFC.RoadBuilder;
            LogUtil.MessageForce($"Road Builder: Enabled:{rb.roadBuildingEnabled} RoadDef:{rb.roadDef?.defName ?? "null"} DaysBetweenTicks:{rb.daysBetweenTicks}");
            if (rb.roadQueue != null)
            {
                var rq = rb.roadQueue;
                LogUtil.MessageForce($"Road Queue: NextTick:{rq.nextRoadTick - Find.TickManager.TicksGame} ticks | FromTiles:{rq.lastFromTileCount} ToTiles:{rq.lastToTileCount} Paths:{rq.roadPaths.Count} NeedsUpdate:{rq.shouldUpdateSettlementsToProcess}");
            }
            else
            {
                LogUtil.MessageForce("Road Queue: null");
            }
        }

        [DebugAction("Empire", "Validate Settlement Caravan List", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugValidateSettlementCaravanList()
        {
            LogUtil.MessageForce($"Validating settlement caravan list...");
            FindFC.FactionComp?.ValidateSettlementCaravansList();
        }

        [DebugAction("Empire", "Fire Support (Pick Source)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void FireSupportPickSource()
        {
            MilitaryFC util = FindFC.Military;
            if (util.fireSupportDefs == null || !util.fireSupportDefs.Any())
            {
                Messages.Message("No fire support definitions configured.", MessageTypeDefOf.RejectInput);
                return;
            }

            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (MilitaryFireSupport support in util.fireSupportDefs)
            {
                if (support.projectiles == null || !support.projectiles.Any()) continue;
                MilitaryFireSupport localSupport = support;
                list.Add(new DebugMenuOption(
                    $"{localSupport.name} ({localSupport.projectiles.Count} shells, acc {localSupport.accuracy})",
                    DebugMenuOptionMode.Action, () =>
                    {
                        DebugTools.curTool = new DebugTool("Select target location", () =>
                        {
                            IntVec3 targetLocation = UI.MouseCell();
                            DebugTools.curTool = new DebugTool("Select source (edge) location", () =>
                            {
                                IntVec3 sourceLocation = UI.MouseCell();
                                Map map = Find.CurrentMap;

                                List<ThingDef> projectiles = new List<ThingDef>();
                                projectiles.AddRange(localSupport.projectiles);

                                MilitaryFireSupport fireSupport = new MilitaryFireSupport(
                                    "fireSupport", map, targetLocation,
                                    projectiles.Count * 15, 600, localSupport.accuracy, projectiles);
                                fireSupport.sourceLocation = sourceLocation;
                                util.fireSupport.Add(fireSupport);

                                float dist = sourceLocation.DistanceTo(targetLocation);
                                LogUtil.MessageForce($"Debug - Fire Support '{localSupport.name}' " +
                                    $"from ({sourceLocation.x},{sourceLocation.z}) " +
                                    $"to ({targetLocation.x},{targetLocation.z}) " +
                                    $"distance: {dist:F1} cells");

                                DebugTools.curTool = null;
                            });
                        });
                    }));
            }

            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }

        [DebugAction("Empire", "Force Restock Settlement Trader", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugForceRestockSettlementTrader()
        {
            List<DebugMenuOption> options = new List<DebugMenuOption>();
            foreach (WorldSettlementFC settlement in FindFC.Settlements)
            {
                options.Add(new DebugMenuOption(settlement.Name, DebugMenuOptionMode.Action, () =>
                {
                    settlement.trader?.TryDestroyStock();
                    LogUtil.MessageForce($"Destroyed trader stock for {settlement.Name}. Will regenerate on next trade access.");
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
        }

        /* Diagnostic dumps for the gene valuator. Use these to spot which genes are producing
         * outsized contributions to the xenotype cost factor. */

        [DebugAction("Empire", "Dump Gene Valuation Cache", allowedGameStates = AllowedGameStates.Playing)]
        private static void DumpGeneValuationCache()
        {
            List<GeneDef> genes = DefDatabase<GeneDef>.AllDefsListForReading
                .OrderBy(g => g.defName).ToList();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Gene raw contributions ({genes.Count} genes)");
            sb.AppendLine("Aggregate-then-score: per-gene raw inputs to the xenotype profile. " +
                          "Stat offsets sum, stat factors multiply, capMods accumulate per capacity, " +
                          "aptitudes sum, biostatMet/Arc sum, marketValueFactor and painFactor multiply.");
            sb.AppendLine("");

            foreach (GeneDef gene in genes)
            {
                GeneValuationUtil.XenotypeProfile p = GeneValuationUtil.GetGeneProfile(gene);
                if (IsProfileTrivial(p)) continue;
                sb.Append(FormatGeneRawContribution(gene, p));
            }

            LogUtil.MessageForce(sb.ToString());
        }

        [DebugAction("Empire", "Dump Xenotype Valuation Cache", allowedGameStates = AllowedGameStates.Playing)]
        private static void DumpXenotypeValuationCache()
        {
            StringBuilder sb = new StringBuilder();

            List<XenotypeDef> xenoDefs = DefDatabase<XenotypeDef>.AllDefsListForReading
                .OrderBy(x => x.defName).ToList();
            List<CustomXenotype> customs = FactionCache.CustomXenotypes ?? new List<CustomXenotype>();

            sb.AppendLine($"Xenotype valuation cache ({xenoDefs.Count} defs + {customs.Count} custom)");
            sb.AppendLine(
                $"weights: mvf={FCSettings.geneValueWeightMvf:F2} " +
                $"met={FCSettings.geneValueWeightMet:F2} " +
                $"arc={FCSettings.geneValueWeightArc:F2} " +
                $"eff={FCSettings.geneValueWeightEffects:F2} " +
                $"pain={FCSettings.geneValueWeightPain:F2} " +
                $"dmgR={FCSettings.geneValueWeightDmgResist:F2}");
            sb.AppendLine("");

            foreach (XenotypeDef xeno in xenoDefs)
                AppendXenotypeSection(sb, xeno.defName,
                    GeneValuationUtil.GetXenotypeProfile(xeno),
                    GeneValuationUtil.GetXenotypeComponents(xeno),
                    GeneValuationUtil.RawXenotypeFactor(xeno),
                    GeneValuationUtil.XenotypeFactor(xeno));

            foreach (CustomXenotype custom in customs)
            {
                if (custom is null) continue;
                AppendXenotypeSection(sb, (custom.name ?? "???") + " [custom]",
                    GeneValuationUtil.GetXenotypeProfile(custom),
                    GeneValuationUtil.GetXenotypeComponents(custom),
                    GeneValuationUtil.RawXenotypeFactor(custom),
                    GeneValuationUtil.XenotypeFactor(custom));
            }

            LogUtil.MessageForce(sb.ToString());
        }

        private static void AppendXenotypeSection(StringBuilder sb, string label,
            GeneValuationUtil.XenotypeProfile profile,
            GeneValuationUtil.GeneValueComponents components,
            float rawFactor, float finalFactor)
        {
            string flag = "";
            if (Math.Abs(rawFactor - finalFactor) > 0.005f)
                flag = rawFactor < finalFactor ? " [FLOORED]" : " [CAPPED]";

            sb.AppendLine($"--- {label} (xenoFactor={finalFactor:F2}x; raw={rawFactor:F2}{flag}) ---");

            sb.AppendLine(
                $"  buckets: shoot={Signed(components.EffectShooting)} " +
                $"melee={Signed(components.EffectMelee)} " +
                $"shared={Signed(components.EffectShared)} " +
                $"nonCombat={Signed(components.EffectNonCombat)} " +
                $"(branch: {components.DescribeCombatBranch()}, combat={Signed(components.FlattenEffect())})");

            sb.AppendLine(
                $"  scalars: mvf={Signed(components.MvfBonus)} " +
                $"met={Signed(components.MetBonus)} " +
                $"arc={Signed(components.ArcBonus)} " +
                $"pain={Signed(components.PainBonus)} " +
                $"dmgR={Signed(components.DmgResistBonus)}");

            sb.AppendLine($"  weighted: {Signed(components.ApplyWeights())}");

            if (profile is null || IsProfileTrivial(profile))
            {
                sb.AppendLine("  (no aggregated stat effects)");
                return;
            }

            AppendProfileBody(sb, profile, "  ");
        }

        /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
        /* Gene-valuation diagnostic formatting helpers               */
        /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/

        private static bool IsProfileTrivial(GeneValuationUtil.XenotypeProfile p)
        {
            if (p is null) return true;
            if (p.StatOffsetSum.Count > 0) return false;
            if (p.StatFactorProduct.Count > 0) return false;
            if (p.CapOffsetSum.Count > 0) return false;
            if (p.CapFactorProduct.Count > 0) return false;
            if (p.AptitudeSum.Count > 0) return false;
            if (p.DamageFactorProduct.Count > 0) return false;
            if (Math.Abs(p.MvfProduct - 1f) > 0.0001f) return false;
            if (Math.Abs(p.MetSum) > 0.0001f) return false;
            if (Math.Abs(p.ArcSum) > 0.0001f) return false;
            if (Math.Abs(p.PainFactorProduct - 1f) > 0.0001f) return false;
            return true;
        }

        private static string FormatGeneRawContribution(GeneDef gene, GeneValuationUtil.XenotypeProfile p)
        {
            StringBuilder sb = new StringBuilder();
            string scalarLine = FormatScalars(p);
            sb.AppendLine(scalarLine is null
                ? $"[{gene.defName}]"
                : $"[{gene.defName}] {scalarLine}");
            AppendProfileBody(sb, p, "  ");
            return sb.ToString();
        }

        private static void AppendProfileBody(StringBuilder sb, GeneValuationUtil.XenotypeProfile p, string indent)
        {
            if (p.StatOffsetSum.Count > 0)
            {
                sb.Append(indent).Append("offsets: ");
                bool first = true;
                foreach (KeyValuePair<StatDef, float> kvp in p.StatOffsetSum.OrderBy(k => k.Key.defName))
                {
                    if (!first) sb.Append("  ");
                    sb.Append(kvp.Key.defName).Append(":").Append(Signed(kvp.Value));
                    first = false;
                }
                sb.AppendLine();
            }
            if (p.StatFactorProduct.Count > 0)
            {
                sb.Append(indent).Append("factors: ");
                bool first = true;
                foreach (KeyValuePair<StatDef, float> kvp in p.StatFactorProduct.OrderBy(k => k.Key.defName))
                {
                    if (!first) sb.Append("  ");
                    sb.Append(kvp.Key.defName).Append(":x").Append(kvp.Value.ToString("F2"));
                    first = false;
                }
                sb.AppendLine();
            }
            if (p.CapOffsetSum.Count > 0 || p.CapFactorProduct.Count > 0)
            {
                HashSet<PawnCapacityDef> seen = new HashSet<PawnCapacityDef>();
                foreach (PawnCapacityDef k in p.CapOffsetSum.Keys) seen.Add(k);
                foreach (PawnCapacityDef k in p.CapFactorProduct.Keys) seen.Add(k);
                sb.Append(indent).Append("caps: ");
                bool first = true;
                foreach (PawnCapacityDef cap in seen.OrderBy(c => c.defName))
                {
                    float o; p.CapOffsetSum.TryGetValue(cap, out o);
                    float f; if (!p.CapFactorProduct.TryGetValue(cap, out f)) f = 1f;
                    if (!first) sb.Append("  ");
                    sb.Append(cap.defName).Append(" o:").Append(Signed(o)).Append(" f:x").Append(f.ToString("F2"));
                    first = false;
                }
                sb.AppendLine();
            }
            if (p.AptitudeSum.Count > 0)
            {
                sb.Append(indent).Append("apts: ");
                bool first = true;
                foreach (KeyValuePair<SkillDef, int> kvp in p.AptitudeSum.OrderBy(k => k.Key.defName))
                {
                    if (!first) sb.Append("  ");
                    sb.Append(kvp.Key.defName).Append(":").Append(kvp.Value >= 0 ? "+" : "").Append(kvp.Value);
                    first = false;
                }
                sb.AppendLine();
            }
            if (p.DamageFactorProduct.Count > 0)
            {
                sb.Append(indent).Append("dmgF: ");
                bool first = true;
                foreach (KeyValuePair<DamageDef, float> kvp in p.DamageFactorProduct.OrderBy(k => k.Key.defName))
                {
                    if (!first) sb.Append("  ");
                    sb.Append(kvp.Key.defName).Append(":x").Append(kvp.Value.ToString("F2"));
                    first = false;
                }
                sb.AppendLine();
            }
        }

        private static string FormatScalars(GeneValuationUtil.XenotypeProfile p)
        {
            List<string> parts = new List<string>(4);
            if (Math.Abs(p.MvfProduct - 1f) > 0.0001f) parts.Add($"mvf=x{p.MvfProduct:F2}");
            if (Math.Abs(p.MetSum) > 0.0001f)         parts.Add($"met={p.MetSum:+0;-0;0}");
            if (Math.Abs(p.ArcSum) > 0.0001f)         parts.Add($"arc={p.ArcSum:+0;-0;0}");
            if (Math.Abs(p.PainFactorProduct - 1f) > 0.0001f) parts.Add($"pain=x{p.PainFactorProduct:F2}");
            return parts.Count == 0 ? null : string.Join(" ", parts.ToArray());
        }

        private static string Signed(float v)
        {
            return v.ToString("+0.00;-0.00;0.00");
        }
    }
}
