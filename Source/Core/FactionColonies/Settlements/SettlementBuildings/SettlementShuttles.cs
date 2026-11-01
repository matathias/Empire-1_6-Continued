using FactionColonies.util;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    public class SettlementBuildingComp_Shuttles : SettlementBuildingComp
    {
        public int shuttleUsesRemaining = 0;
        public int totalShuttleUses = 0;
        public int lastShuttleUsesRefreshTick = 0;
        public const int shuttleRefreshInterval = GenDate.TicksPerDay * 5;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref shuttleUsesRemaining, "shuttleUsesRemaining", 0);
            Scribe_Values.Look(ref totalShuttleUses, "totalShuttleUses", 0);
            Scribe_Values.Look(ref lastShuttleUsesRefreshTick, "lastShuttleUsesRefreshTick", 0);
        }

        private void RefreshTotalShuttleUses(int buildingSlotToSkip = -1)
        {
            totalShuttleUses = 0;
            for (int i = 0; i < buildingSlots.Count; i++)
            {
                int slot = buildingSlots[i];
                if (slot != buildingSlotToSkip)
                {
                    BuildingFCExtension_Shuttles ext = parentComp?.GetBuildingInSlot(slot)?.GetModExtension<BuildingFCExtension_Shuttles>();
                    if (ext != null)
                    {
                        totalShuttleUses += ext.shuttleUses;
                    }
                }
            }
        }

        public override void OnConstruct(int buildingSlot)
        {
            LogUtil.Message("Start of SettlementBuildingComp_Shuttles.OnConstruct");
            base.OnConstruct(buildingSlot);

            int oldTotalUses = totalShuttleUses;
            RefreshTotalShuttleUses();
            shuttleUsesRemaining += (totalShuttleUses - oldTotalUses);
            if (shuttleUsesRemaining < 0)
            {
                shuttleUsesRemaining = 0;
            }
            else if (shuttleUsesRemaining > totalShuttleUses)
            {
                shuttleUsesRemaining = totalShuttleUses;
            }
        }
        public override void OnDeconstruct(int buildingSlot)
        {
            LogUtil.Message("Start of SettlementBuildingComp_Shuttles.OnDeconstruct");
            RefreshTotalShuttleUses(buildingSlot);
            if (shuttleUsesRemaining > totalShuttleUses)
            {
                shuttleUsesRemaining = totalShuttleUses;
            }

            base.OnDeconstruct(buildingSlot);
        }

        public override void Tick()
        {
            // base.Tick() is empty (SettlementBuildingComp) as of Empire Refactored 1.5.x.

            if (lastShuttleUsesRefreshTick + shuttleRefreshInterval <= Find.TickManager.TicksGame)
            {
                RefreshTotalShuttleUses();
                shuttleUsesRemaining = totalShuttleUses;
                lastShuttleUsesRefreshTick = Find.TickManager.TicksGame;
            }
        }
        public override IEnumerable<Gizmo> GetGizmos()
        {
            IEnumerable<Gizmo> gizmos = base.GetGizmos();
            if (gizmos != null)
            {
                foreach (Gizmo gizmo in gizmos)
                {
                    yield return gizmo;
                }
            }
            yield return RequestShuttleAction(settlement);
            yield return RequestShuttleForCaravanAction(settlement);
        }

        private Command RequestShuttleAction(WorldSettlementFC worldsettlement)
        {
            Command_Action requestShuttle = new Command_Action
            {
                defaultLabel = "FCShuttlePortCallShuttleLabel".Translate(),
                defaultDesc = "FCShuttlePortCallShuttleDesc".Translate(shuttleUsesRemaining, ShuttleSender.cost),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/CallShuttle"),
                action = delegate
                {
                    Find.WorldSelector.ClearSelection();
                    var sender = new ShuttleSender(worldsettlement.Tile, this);
                    Find.WorldTargeter.BeginTargeting(sender.PerformActionWithTarget, true,
                        CompLaunchable.TargeterMouseAttachment, false, sender.DrawWorldRadiusRing,
                        sender.DisplayTargetInformation, sender.ChoseWorldTarget);
                }
            };
            if (shuttleUsesRemaining < ShuttleSender.cost)
            {
                requestShuttle.Disable("FCNotEnoughShuttleUsesRemaining".Translate());
            }

            return requestShuttle;
        }

        private Command RequestShuttleForCaravanAction(WorldSettlementFC worldsettlement)
        {
            Command_Action requestShuttleForCaravan = new Command_Action
            {
                defaultLabel = "FCShuttlePortCallShuttleForCaravanLabel".Translate(),
                defaultDesc = "FCShuttlePortCallShuttleDesc".Translate(shuttleUsesRemaining, ShuttleSender.cost),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/CallShuttle"),

                action = delegate
                {
                    var caravans = Find.World.worldObjects.Caravans.Where(caravan => caravan.Faction == Faction.OfPlayer).ToList();
                    var options = new List<FloatMenuOption>();

                    caravans.ForEach(caravan => options.Add(new FloatMenuOption(caravan.Label, delegate
                    {
                        var sender = new ShuttleSenderCaravan(caravan.Tile, caravan, this);

                        CameraJumper.TryJump(caravan);
                        Find.WorldSelector.ClearSelection();
                        var tile = caravan.Tile;
                        Find.WorldTargeter.BeginTargeting(sender.ChoseWorldTarget, true,
                            CompLaunchable.TargeterMouseAttachment, false,
                            sender.DrawWorldRadiusRing,
                            target => sender.TargetingLabelGetter(target, tile, ShuttleSender.ShuttleRange,
                                Gen.YieldSingle(caravan), sender.Launch));
                    })));

                    if (options.Count == 0) options.Add(new FloatMenuOption("FCNoCaravansToSendShuttleTo".Translate(), null));

                    Find.WindowStack.Add(new FloatMenu(options));
                }
            };
            if (shuttleUsesRemaining < ShuttleSender.cost)
            {
                requestShuttleForCaravan.Disable("FCNoShuttleUsesRemaining".Translate());
            }

            return requestShuttleForCaravan;
        }
    }
}
