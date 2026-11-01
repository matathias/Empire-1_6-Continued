using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /* Empire-flavored FormCaravanComp.
     * Vanilla treats a map as "safe to reform" once GenHostility.AnyHostileActiveThreatToPlayer
     * returns false, which happens as soon as attackers go into flee/down state. Empire keeps
     * the battle officially active until the attackers list empties (i.e. the last attacker
     * despawns or dies). The gap between the two leaves the reform-caravan gizmo enabled while
     * Empire's ShouldRemoveMapNow still blocks map removal, producing awkward (and potentially
     * bug/exploit-prone) behavior.
     *
     * This subclass disables the gizmo until Empire's battle bookkeeping is done. */
    public class WorldObjectCompProperties_FormCaravanFC : WorldObjectCompProperties_FormCaravan
    {
        public WorldObjectCompProperties_FormCaravanFC()
        {
            compClass = typeof(FormCaravanCompFC);
        }
    }

    public class FormCaravanCompFC : FormCaravanComp
    {
        public override IEnumerable<Gizmo> GetGizmos()
        {
            WorldObjectComp_SettlementMilitary mil = parent.GetComponent<WorldObjectComp_SettlementMilitary>();
            bool battleStillActive = mil is object && mil.isUnderAttack;

            foreach (Gizmo gizmo in base.GetGizmos())
            {
                if (battleStillActive && gizmo is Command_Action cmd && cmd.tutorTag == "ReformCaravan")
                    cmd.Disable("FCReformCaravanBattleStillActive".Translate());
                yield return gizmo;
            }
        }
    }
}
