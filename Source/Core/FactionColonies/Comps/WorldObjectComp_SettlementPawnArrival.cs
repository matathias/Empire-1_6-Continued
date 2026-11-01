using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FactionColonies
{
    /* Base for a settlement WorldObjectComp that can absorb pawns delivered straight to the
       settlement by transport pod (see TransportersArrivalAction_AddToSettlementFC), skipping the
       tedious land -> form caravan -> transfer flow.

       One arrival type per subclass (the arrival action identifies the chosen type by its class
       name, so don't register two of the same type on one settlement). WorldSettlementFC enumerates
       AllComps.OfType<WorldObjectComp_SettlementPawnArrival>() and offers one float-menu option per
       comp that accepts at least one pawn in the launched pods. */
    public abstract class WorldObjectComp_SettlementPawnArrival : WorldObjectComp
    {
        public WorldSettlementFC Settlement => parent as WorldSettlementFC;

        public abstract bool AcceptsPawn(Pawn pawn);

        /* Launch-time gate (permission / capacity / state) controlling whether this comp's
           float-menu option is offered. It is NOT re-checked on arrival: a pod already in flight
           always completes, so ReceivePawns must place every pawn it accepts -- handle a slot that
           filled mid-flight there (e.g. overflow to a resident), don't rely on this to block it. */
        public virtual FloatMenuAcceptanceReport CanReceive => true;

        /* Float-menu label, e.g. "Add prisoners to <settlement>". */
        public abstract string ArrivalMenuLabel { get; }

        /* Message shown after this comp absorbs the pod's pawns. Override to describe the role;
           the default is deliberately generic so it is never wrong for a new arrival type. */
        public virtual string ArrivalMessage(List<Pawn> pawns)
            => "FCPawnArrivalDefault".Translate(pawns.Count, Settlement.Label);

        /* Absorb the already-filtered pawns. By the time this runs they have already been removed
           from the pod containers, so this comp becomes their sole owner. */
        public abstract void ReceivePawns(List<Pawn> pawns);
    }
}
