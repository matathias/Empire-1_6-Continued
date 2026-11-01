using FactionColonies.util;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System.Linq;
using Verse;
using Verse.AI;

namespace FactionColonies
{
    /// <summary>
    /// Prevents battle participants from walking off the edge of an Empire settlement's battle
    /// map mid-fight. Allows the player's own caravan / shuttle colonists to leave at will,
    /// since they're volunteers — but Empire NPCs (defenders, squad mercenaries, settlement
    /// inhabitants) and the NPCs the player has drafted into combat are committed to the battle.
    ///
    /// Hostile attackers are *not* blocked: when their lord enters its exit toil they're
    /// supposed to retreat off the map, which is how the battle ends.
    /// </summary>
    [HarmonyPatch(typeof(JobDriver_Goto), "TryExitMap")]
    public class Patch
    {
        static bool Prefix(ref JobDriver_Goto __instance)
        {
            Pawn pawn = __instance.pawn;
            if (!(pawn.Map?.Parent is WorldSettlementFC settlement))
            {
                // Manual offensive battles run on a vanilla enemy Settlement. Undrafted Empire
                // attackers may freely walk off the edge to extract, but a pawn the player DRAFTED
                // (now Faction.OfPlayer) must be trapped until the battle resolves -- otherwise the
                // player could march it off the map and keep it as a free colonist. EndOffense
                // restores drafted pawns to Empire on teardown, lifting this block.
                Settlement enemyBase = pawn.Map?.Parent as Settlement;
                if (enemyBase is object)
                {
                    BattlefieldContext offBf = FindFC.MilitaryManager?.GetBattlefield(enemyBase.Tile);
                    if (offBf is object && offBf.HasOffenseAt() && offBf.draftedNPCs.Contains(pawn))
                        return false;
                }
                return true;
            }

            var military = settlement.MilitaryComp;
            if (military == null || !military.isUnderAttack) return true;

            // Player's own colonists who weren't drafted by us are volunteers (caravan / shuttle
            // joiners) and may leave at will — even though they're aggregated into
            // military.defenders for combat purposes.
            if (pawn.Faction == Faction.OfPlayer && !military.draftedNPCs.Contains(pawn))
                return true;

            // Empire NPCs / squad mercenaries spawned for this battle are tracked on each op's
            // defender.pawns; the comp's `defenders` enumerable aggregates across ops at this tile.
            if (military.defenders.Contains(pawn)) return false;

            // Belt-and-suspenders for pawns that should have been registered as defenders but
            // weren't (e.g., a submod path that bypasses our spawn helpers): block any squad
            // mercenary or any drafted pawn left over after the player-faction check above.
            if (pawn.IsMercenary()) return false;
            if (pawn.Drafted) return false;

            return true;
        }
    }

    /// <summary>
    /// Keeps a mercenary's avian companions from being destroyed when a deployed squad leaves the map.
    /// <see cref="JobGiver_ExitMap"/> routes fliers of a non-player faction through its flying-exit branch
    /// (<see cref="JobDefOf.ExitMapFlying"/>), which wraps the pawn in a <c>FlyerLeaving</c> skyfaller that
    /// DESTROYS its contents on leave (<c>Skyfaller.LeaveMap -&gt; Destroy -&gt; ClearAndDestroyContents</c>).
    /// Empire mercs are a non-player faction, so a merc's bird would be destroyed rather than preserved —
    /// counting as a Missing sub-pawn the player must pay to replace. Redirect merc fliers to a normal ground
    /// exit (a Goto with <c>exitMapOnArrival</c>), which routes through <c>Pawn.ExitMap -&gt; PassToWorld</c>
    /// and is preserved by <see cref="MercenaryPassToWorld"/>. If no edge is reachable on foot we clear the
    /// job (the pawn idles and is despawned/preserved when the deployment finalizes) — better a brief idle
    /// than a destroyed companion. Patches the abstract base's TryGiveJob, so every JobGiver_ExitMap subclass
    /// (ExitMapBest, etc.) is covered — none of them override it.
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_ExitMap), "TryGiveJob")]
    class MercAnimalNoFlyExit
    {
        static void Postfix(Pawn pawn, ref Job __result)
        {
            if (__result?.def != JobDefOf.ExitMapFlying) return;
            if (FindFC.Military?.IsMercenaryPawn(pawn) != true) return;

            if (RCellFinder.TryFindBestExitSpot(pawn, out IntVec3 dest, TraverseMode.ByPawn))
            {
                Job job = JobMaker.MakeJob(JobDefOf.Goto, dest);
                job.exitMapOnArrival = true;
                job.locomotionUrgency = LocomotionUrgency.Jog;
                __result = job;
            }
            else
            {
                __result = null;
            }
        }
    }
}
