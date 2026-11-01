using FactionColonies.util;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /* Surface-only invariant: see FCRoadQueue. Tile IDs stored as int here
       are safe because the road system never handles non-surface tiles. */
    public class FCRoadBuilder : IExposable
    {
        public FCRoadQueue roadQueue;
        public RoadDef roadDef;
        public bool shouldDrawPaths = false;

        public int daysBetweenTicks = 3;
        public bool roadBuildingEnabled = true;
        public bool wasRoadBuildingDisabled = true;
        bool hasRoadBuildersBoost;
        bool pathsFullyProcessed;

        public FCRoadBuilder()
        {
        }

        //DirtPath (priority: 10) - This is the lowest priority road, likely the basic dirt path
        //DirtRoad (priority: 20) - This is the traditional dirt road
        //StoneRoad (priority: 30)
        //AncientAsphaltRoad (priority: 40)
        //AncientAsphaltHighway (priority: 50)
        // We could define our own and provide a roaddef for this, such as spacer / glitterworld tech level roads...

        public void ExposeData()
        {
            Scribe_Defs.Look(ref roadDef, "roadDef");
            Scribe_Values.Look(ref daysBetweenTicks, "daysBetweenTicks");
            Scribe_Values.Look(ref roadBuildingEnabled, "roadBuildingEnabled");
            Scribe_Values.Look(ref wasRoadBuildingDisabled, "wasRoadBuildingDisabled");
            Scribe_Deep.Look(ref roadQueue, "roadQueue", new object[] { this.roadDef, this.daysBetweenTicks });
        }

        /* Resolves the days-between-road-builds interval. Normally the configured
           mod setting; the Road Builders policy boost builds at a third of that
           interval (integer round-to-nearest via (cfg + 1) / 3), floored at 1 day
           so building never happens more often than the daily boundary. */
        static int EffectiveInterval(bool boosted)
        {
            int cfg = FCSettings.roadBuildIntervalDays;
            return boosted ? Math.Max(1, (cfg + 1) / 3) : cfg;
        }

        public void FirstTick()
        {
            CheckForTechChanges();
            CreateRoadQueue(false);
            FlagUpdateRoadQueues();

            if (roadQueue.nextRoadTick == 0)
                roadQueue.nextRoadTick = Find.TickManager.TicksGame;

            if (daysBetweenTicks == 0)
            {
                LogUtil.Message("FCRoadBuilder - Resetting daysBetweenTicks");
                int days = EffectiveInterval(hasRoadBuildersBoost);
                daysBetweenTicks = days;
                roadQueue.daysBetweenTicks = days;
            }
        }

        public void RoadTick()
        {
            if (roadDef == null)
            {
                wasRoadBuildingDisabled = true;
                return;
            }

            if (!roadBuildingEnabled)
            {
                wasRoadBuildingDisabled = true;
                return;
            }

            FactionFC faction = FindFC.FactionComp;

            if (roadQueue == null)
            {
                LogUtil.Message("RoadTick: No road queue found");
                return;
            }

            if (!hasRoadBuildersBoost && FindFC.FactionComp.IsActionAllowed(FCActionType.BuildRoadsToAllies))
            {
                roadQueue.shouldUpdateSettlementsToProcess = true;
                hasRoadBuildersBoost = true;
            }

            // Interval is driven by the global mod setting; re-sync every tick so
            // changes to the setting take effect immediately.
            int interval = EffectiveInterval(hasRoadBuildersBoost);
            daysBetweenTicks = interval;
            roadQueue.daysBetweenTicks = interval;

            // If road building was disabled, then set the next tick to make a road to the correct time
            if (wasRoadBuildingDisabled)
            {
                wasRoadBuildingDisabled = false;
                roadQueue.nextRoadTick = Find.TickManager.TicksGame + GenDate.TicksPerDay * roadQueue.daysBetweenTicks;
            }

            // Ensure settlement lists are populated before processing paths.
            if (roadQueue.shouldUpdateSettlementsToProcess)
            {
                roadQueue.UpdateSettlementsToProcess();
                roadQueue.shouldUpdateSettlementsToProcess = false;
            }

            // Advance incremental MST computation (non-threaded only)
            if (!FCSettings.useThreadedRoadComputation && FCSettings.edgesPerRoadTick > 0)
            {
                roadQueue.AdvanceMSTIncremental(FCSettings.edgesPerRoadTick);
            }

            if (!pathsFullyProcessed && roadQueue.IsMSTReady)
            {
                for (int i = 0; i < 5; i++)
                {
                    if (!roadQueue.ProcessOnePath())
                    {
                        pathsFullyProcessed = true;
                        break;
                    }
                }
            }

            roadQueue.BuildRoadSegments();
        }

        /// <summary>
        /// Checks if a settlement is a valid road target. When empireTileIds is
        /// provided, uses O(1) lookup instead of iterating all empire settlements.
        /// </summary>
        public static bool IsValidRoadTarget(Settlement settlement, HashSet<int> empireTileIds = null)
        {
            if (!settlement.Tile.Layer.IsRootSurface)
                return false;

            FactionFC fC = FindFC.FactionComp;

            // If faction exists and is either player or player has roadBuilders trait and the faction is an ally
            if (settlement.Faction != null)
                if (settlement.Faction.IsPlayer || (FindFC.FactionComp.IsActionAllowed(FCActionType.BuildRoadsToAllies) && settlement.Faction.PlayerRelationKind == FactionRelationKind.Ally))
                    return true;

            if (empireTileIds is object)
                return empireTileIds.Contains(settlement.Tile.tileId);

            foreach (WorldSettlementFC settlementFC in fC.settlements)
            {
                if (settlementFC.Tile == settlement.Tile)
                    return true;
            }

            return false;
        }

        public FCRoadQueue CreateRoadQueue(bool logFailure = true)
        {
            if (roadQueue != null)
            {
                if (logFailure)
                {
                    LogUtil.Message($"Road queue already exists.");
                }

                return roadQueue;
            }
            roadQueue = new FCRoadQueue(roadDef, daysBetweenTicks);
            return roadQueue;
        }

        public void CheckForTechChanges()
        {
            RoadDef def = this.roadDef;

            if (DefDatabase<ResearchProjectDef>.GetNamed("FCRoadBuildingHighway", false).IsFinished)
            {
                def = RoadDefOf.AncientAsphaltHighway;
            }
            else if (DefDatabase<ResearchProjectDef>.GetNamed("FCRoadBuildingRoad", false).IsFinished)
            {
                def = RoadDefOf.AncientAsphaltRoad;
            }
            else if (DefDatabase<ResearchProjectDef>.GetNamed("FCRoadBuildingStone", false).IsFinished)
            {
                def = DefDatabase<RoadDef>.GetNamed("StoneRoad", false);
            }
            else if (DefDatabase<ResearchProjectDef>.GetNamed("FCRoadBuildingDirt", false).IsFinished)
            {
                // Use DirtPath (priority 10) to match existing world-generated dirt paths
                def = DefDatabase<RoadDef>.GetNamed("DirtPath", false);
            }

            if (this.roadDef != def)
            {
                LogUtil.Message($"Road type changed from {this.roadDef?.defName ?? "null"} to {def?.defName ?? "null"}");
                this.roadDef = def;

                if (roadQueue is object)
                {
                    roadQueue.RoadDef = def;
                    FlagUpdateRoadQueues();
                }
            }
        }

        public void DrawPaths()
        {
            if (roadQueue is null) return;
            roadQueue.DrawPaths();
        }

        /// <summary>
        /// Flags all road queues to update whenever they are able.
        /// </summary>
        public void FlagUpdateRoadQueues()
        {
            if (roadQueue != null)
            {
                roadQueue.shouldUpdateSettlementsToProcess = true;
                pathsFullyProcessed = false;
            }
        }
    }
}
