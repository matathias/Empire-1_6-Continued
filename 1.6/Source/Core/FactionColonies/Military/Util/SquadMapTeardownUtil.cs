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
    }
}
