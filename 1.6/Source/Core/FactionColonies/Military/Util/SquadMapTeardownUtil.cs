using System.Collections.Generic;
using System.Linq;
using RimWorld;
using FactionColonies.util;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Shared battle-map teardown handling for Empire-faction pawns. Used by both manual defense
    /// (<see cref="WorldSettlementFC.Notify_MyMapAboutToBeRemoved"/>) and manual offense (whose map
    /// parent is a vanilla enemy Settlement and so never fires that hook), so both preserve the
    /// squad identically.
    /// </summary>
    public static class SquadMapTeardownUtil
    {
        /// <summary>
        /// Cleans up Empire-faction pawns on a battle map about to be removed. Squad pawns
        /// (mercenaries and their bonded sub-pawns) are despawned and faction-restored so the squad
        /// holds them off-map (not the world pawn pool) for redeployment; genuine generated Empire
        /// defenders are destroyed as disposable. Non-Empire pawns are left alone.
        /// </summary>
        public static void PreserveEmpirePawns(Map map)
        {
            Faction empireFaction = FindFC.EmpireFaction;
            if (empireFaction is null || map is null) return;

            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.ToList())
            {
                // Squad pawns — mercenaries AND their bonded sub-pawns (mechs / companion animals) —
                // persist between battles. IsMercenary() self-heals against a lagging cache, so it
                // reliably recognizes a freshly-bonded mech. Despawn so the squad holds them off-map,
                // and restore the Empire faction as a safety net so they redeploy correctly next
                // battle. Only genuine generated defenders get destroyed.
                if (pawn.IsMercenary())
                {
                    if (pawn.Spawned) pawn.DeSpawn();
                    if (pawn.Faction != empireFaction) pawn.SetFaction(empireFaction);
                    continue;
                }

                // Generated (non-squad) Empire defenders are disposable — destroy them so they don't
                // ghost into the world pawn pool.
                if (pawn.Faction != empireFaction) continue;
                pawn.DeSpawn();
                if (!pawn.Destroyed)
                    pawn.Destroy();
            }
        }

        /// <summary>
        /// Last-resort safety net: a battle map is about to be removed but still has live player pawns
        /// spawned on it. The keep-map-open mechanism (<see cref="BattlefieldContext.DeleteMap"/>'s
        /// container-aware check plus <c>WorldSettlementFC.ShouldRemoveMapNow</c>) is supposed to keep
        /// the map alive whenever a live player pawn is present, so any pawn caught here means an
        /// upstream gap slipped through. Logs a warning with enough detail to trace that gap, then
        /// delivers the pawns home instead of letting the map removal destroy them.
        /// </summary>
        public static void EvacuatePlayerPawns(Map map)
        {
            Faction player = Faction.OfPlayer;
            if (map is null || player is null) return;

            List<Pawn> toEvacuate = new List<Pawn>();
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (pawn.Faction != player || pawn.Dead) continue;
                toEvacuate.Add(pawn);
            }
            if (toEvacuate.Count == 0) return;

            // Tripwire: reaching here should be impossible once the keep-map-open guards hold. Log
            // per-pawn so a future recurrence points at which teardown let the pawn through.
            foreach (Pawn pawn in toEvacuate)
            {
                LogUtil.Warning($"Player pawn '{pawn.LabelShortCap}' (downed={pawn.Downed}) was still on the " +
                    $"battle map at tile {map.Tile} as it was being removed — the keep-map-open mechanism " +
                    $"missed it. Delivering it home instead of destroying it; please report this as a teardown gap.");
            }

            List<Thing> goods = new List<Thing>(toEvacuate.Count);
            foreach (Pawn pawn in toEvacuate)
            {
                if (pawn.Spawned) pawn.DeSpawn();
                goods.Add(pawn);
            }
            DeliveryEvent.CreateDeliveryEvent(goods, map.Tile);
        }
    }
}
