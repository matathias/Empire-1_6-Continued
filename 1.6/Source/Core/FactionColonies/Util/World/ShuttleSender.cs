using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies.util
{
    class ShuttleSender
    {
        protected readonly PlanetTile Tile = PlanetTile.Invalid;
        public static readonly int ShuttleRange = 70;
        public SettlementBuildingComp_Shuttles comp = null;
        public static readonly int cost = 1;

        public ShuttleSender(PlanetTile Tile, SettlementBuildingComp_Shuttles comp)
        {
            this.Tile = Tile;
            this.comp = comp;
        }

        protected virtual bool TargetHasValidWorldObject(GlobalTargetInfo target) => target.HasWorldObject && target.WorldObject is MapParent mapParent && (mapParent.Map?.mapPawns?.AnyFreeColonistSpawned ?? false);

        public virtual bool ChoseWorldTarget(GlobalTargetInfo target) => target.Tile > -1 && Find.WorldGrid.TraversalDistanceBetween(target.Tile, Tile) <= ShuttleRange && TargetHasValidWorldObject(target);

        protected virtual TransportShip SendWaitingShuttle(MapParent target)
        {
            Thing shuttle = ThingMaker.MakeThing(ThingDefOf.Shuttle);
            CompShuttle compShuttle = shuttle.TryGetComp<CompShuttle>();
            if (compShuttle is object)
            {
                compShuttle.permitShuttle = true;
            }
            TransportShip transportShip = TransportShipMaker.MakeTransportShip(TransportShipDefOf.Ship_Shuttle, null, shuttle);

            IntVec3 landingCell = DropCellFinder.GetBestShuttleLandingSpot(target.Map, Faction.OfPlayer);
            transportShip.ArriveAt(landingCell, target.Map.Parent);
            transportShip.AddJobs(new ShipJobDef[]
            {
                ShipJobDefOf.WaitForever,
                ShipJobDefOf.Unload,
                ShipJobDefOf.FlyAway
            });

            if (comp != null)
            {
                comp.shuttleUsesRemaining -= cost;
            }
            CameraJumper.TryJump(landingCell, target.Map);
            return transportShip;
        }

        public virtual bool PerformActionWithTarget(GlobalTargetInfo target)
        {
            if (ChoseWorldTarget(target))
            {
                SendWaitingShuttle(target.WorldObject as MapParent);
                return true;
            }
            return false;
        }

        public string TargetingLabelGetter(GlobalTargetInfo target, PlanetTile tile, int shuttleRange, IEnumerable<IThingHolder> pods, Action<PlanetTile, TransportersArrivalAction> launchAction)
        {
            if (!target.IsValid)
            {
                return null;
            }
            if (!target.IsValid)
            {
                return null;
            }

            if (shuttleRange > 0 && Find.WorldGrid.TraversalDistanceBetween(tile, target.Tile, true, int.MaxValue) > shuttleRange)
            {
                GUI.color = ColorLibrary.RedReadable;
                return "TransportPodDestinationBeyondMaximumRange".Translate();
            }
            List<FloatMenuOption> source = null;

            try
            {
                source = CompLaunchable.GetOptionsForTile(target.Tile, pods, launchAction).ToList();
            }
            catch (Exception ex)
            {
                LogUtil.Warning("Shuttle launch options failed, retrying without animals: " + ex);
                foreach (IThingHolder thingHolder in pods)
                {
                    thingHolder.GetDirectlyHeldThings().RemoveAll(thing => thing.def.race?.Animal ?? false);
                }
                source = CompLaunchable.GetOptionsForTile(target.Tile, pods, launchAction).ToList();
            }

            if (!source.Any())
            {
                return string.Empty;
            }

            if (source.Count() == 1)
            {
                if (source.First().Disabled)
                {
                    GUI.color = ColorLibrary.RedReadable;
                }
                return source.First().Label;
            }
            if (target.WorldObject is MapParent mapParent)
            {
                return "ClickToSeeAvailableOrders_WorldObject".Translate(mapParent.LabelCap);
            }
            return "ClickToSeeAvailableOrders_Empty".Translate();
        }

        public virtual TaggedString DisplayTargetInformation(GlobalTargetInfo target)
        {
            if (!ChoseWorldTarget(target))
            {
                return "FCTargetAnythingWithColonists".Translate();
            }

            if (target.WorldObject is Caravan)
            {
                return "FCRequestShuttleToCaravan".Translate();
            }

            if (target.WorldObject is Settlement)
            {
                return "FCRequestShuttleToColony".Translate();
            }

            if (target.WorldObject is MapParent)
            {
                return "FCRequestShuttleToMap".Translate();
            }

            return "FCTargetAnythingWithColonists".Translate();
        }

        public void DrawWorldRadiusRing() => GenDraw.DrawWorldRadiusRing(Tile, ShuttleRange);
    }
}
