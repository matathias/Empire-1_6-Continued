using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FactionColonies
{
    /* Transport-pod arrival action that adds the pod's pawns straight into an Empire settlement,
       routing them through whichever WorldObjectComp_SettlementPawnArrival the player chose at launch.

       The action rides on the in-flight pod (TravellingTransporters), which is saved independently of
       the settlement, so on load it must re-find the chosen comp. WorldObjectComp is not
       ILoadReferenceable, so we can't Scribe_References the comp directly; instead we scribe the
       settlement reference (a WorldObject, which IS referenceable) plus the comp's type name, and
       re-resolve the live comp from settlement.AllComps. One arrival comp per type, so the type name
       is a sufficient discriminator. */
    public class TransportersArrivalAction_AddToSettlementFC : TransportersArrivalAction
    {
        private WorldSettlementFC settlement;
        private string arrivalCompType;

        public override bool GeneratesMap => false;

        public TransportersArrivalAction_AddToSettlementFC()
        {
        }

        public TransportersArrivalAction_AddToSettlementFC(WorldSettlementFC settlement, WorldObjectComp_SettlementPawnArrival comp)
        {
            this.settlement = settlement;
            this.arrivalCompType = comp?.GetType().FullName;
        }

        public static WorldObjectComp_SettlementPawnArrival ResolveComp(WorldSettlementFC s, string compType)
        {
            if (s is null || compType.NullOrEmpty()) return null;
            return s.AllComps.OfType<WorldObjectComp_SettlementPawnArrival>()
                .FirstOrDefault(c => c.GetType().FullName == compType);
        }

        public override FloatMenuAcceptanceReport StillValid(IEnumerable<IThingHolder> pods, PlanetTile destinationTile)
        {
            // Neither the battle/raid gate nor the comp's CanReceive gate is re-checked here: both are
            // launch-time gates (see GetFloatMenuOptions). A pod already in flight always completes its
            // arrival, and the comp's ReceivePawns decides what to do given the settlement's current
            // state. We only fail (-> engine caravan fallback) when the target itself is gone
            // (settlement despawned/moved, or the comp type no longer exists).
            FloatMenuAcceptanceReport report = base.StillValid(pods, destinationTile);
            if (!report) return report;
            if (settlement is null || !settlement.Spawned || settlement.Tile != destinationTile)
                return false;
            return ResolveComp(settlement, arrivalCompType) is object;
        }

        public override void Arrived(List<ActiveTransporterInfo> transporters, PlanetTile tile)
        {
            WorldObjectComp_SettlementPawnArrival comp = ResolveComp(settlement, arrivalCompType);

            // Pull the pawns this comp accepts out of the pod containers so it becomes their sole
            // owner (anything left over is handed to the fallback below and never lost).
            List<Pawn> accepted = new List<Pawn>();
            foreach (ActiveTransporterInfo transporter in transporters)
            {
                ThingOwner inner = transporter.innerContainer;
                for (int n = inner.Count - 1; n >= 0; n--)
                {
                    if (inner[n] is Pawn p && comp is object && comp.AcceptsPawn(p))
                    {
                        accepted.Add(p);
                        inner.Remove(p);
                    }
                }
            }

            if (comp is object && accepted.Count > 0)
            {
                comp.ReceivePawns(accepted);
                Messages.Message(comp.ArrivalMessage(accepted), settlement, MessageTypeDefOf.TaskCompletion);
            }

            SettlementPawnArrivalFallback.RouteLeftovers(transporters, tile, settlement);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref settlement, "settlement");
            Scribe_Values.Look(ref arrivalCompType, "arrivalCompType");
        }

        /* Builds one float-menu option per arrival comp that accepts at least one pawn in the
           launched pods. Hidden entirely while the settlement is in a battle or under raid threat. */
        public static IEnumerable<FloatMenuOption> GetFloatMenuOptions(
            IEnumerable<IThingHolder> pods,
            Action<PlanetTile, TransportersArrivalAction> launchAction,
            WorldSettlementFC settlement)
        {
            if (settlement is null || !settlement.Spawned) yield break;
            if (settlement.MilitaryComp?.isUnderAttack == true) yield break;

            List<Pawn> podPawns = ExtractPawns(pods);
            if (podPawns.Count == 0) yield break;

            List<WorldObjectComp> all = settlement.AllComps;
            for (int i = 0; i < all.Count; i++)
            {
                WorldObjectComp_SettlementPawnArrival comp = all[i] as WorldObjectComp_SettlementPawnArrival;
                if (comp is null) continue;
                if (!podPawns.Any(comp.AcceptsPawn)) continue;

                WorldObjectComp_SettlementPawnArrival captured = comp;
                foreach (FloatMenuOption option in TransportersArrivalActionUtility.GetFloatMenuOptions(
                    () => captured.CanReceive,
                    () => new TransportersArrivalAction_AddToSettlementFC(settlement, captured),
                    captured.ArrivalMenuLabel,
                    launchAction,
                    settlement.Tile))
                {
                    yield return option;
                }
            }
        }

        private static List<Pawn> ExtractPawns(IEnumerable<IThingHolder> pods)
        {
            List<Pawn> result = new List<Pawn>();
            foreach (IThingHolder pod in pods)
            {
                ThingOwner held = pod.GetDirectlyHeldThings();
                if (held is null) continue;
                for (int i = 0; i < held.Count; i++)
                {
                    if (held[i] is Pawn p) result.Add(p);
                }
            }
            return result;
        }
    }
}
