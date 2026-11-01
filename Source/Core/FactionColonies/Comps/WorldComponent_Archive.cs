using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/
    /* WorldComponent_Archive                                                      */
    /*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/

    /// <summary>
    /// World-level archive of historical Empire data the player may want to review later.
    /// Currently holds: post-battle <see cref="BattleResult"/> reports. Future expansion
    /// (event archive, diplomacy archive, etc.) can live here as additional collections
    /// without a second world component.
    /// </summary>
    public class WorldComponent_Archive : WorldComponent
    {
        /* -*- Battle archive -*- */
        private List<BattleResult> battleArchive = new List<BattleResult>();
        private int nextReportId = 1;

        public WorldComponent_Archive(World world) : base(world) { }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref battleArchive, "battleArchive", LookMode.Deep);
            Scribe_Values.Look(ref nextReportId, "nextReportId", 1);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (battleArchive is null) battleArchive = new List<BattleResult>();
                if (nextReportId < 1) nextReportId = 1;
            }
        }

        /// <summary>
        /// Insert <paramref name="result"/> into the battle archive, assigning a stable
        /// <see cref="BattleResult.reportId"/> and stamping <see cref="BattleResult.recordedTick"/>
        /// to the current game tick. Evicts the oldest entry if the archive is at the
        /// user-configured cap (<see cref="FCSettings.battleArchiveMaxEntries"/>). When
        /// <see cref="FCSettings.battleArchiveUnlimited"/> is set, the cap is ignored and
        /// nothing is ever evicted. If the user later turns the cap back on, the next
        /// insertion trims the archive back down to the cap.
        /// </summary>
        /// <returns>The id assigned to the inserted report, or 0 if <paramref name="result"/> was null.</returns>
        public int RecordBattleReport(BattleResult result)
        {
            if (result is null) return 0;

            result.reportId = nextReportId++;
            result.recordedTick = Find.TickManager.TicksGame;

            if (!FCSettings.battleArchiveUnlimited)
            {
                int cap = System.Math.Max(1, FCSettings.battleArchiveMaxEntries);
                // Evict oldest until we're at cap-1 (we're about to add one).
                while (battleArchive.Count >= cap)
                    battleArchive.RemoveAt(0);
            }

            battleArchive.Add(result);
            return result.reportId;
        }

        /// <summary>
        /// Returns true and sets <paramref name="result"/> if a report with id <paramref name="id"/>
        /// is still in the archive; false otherwise (evicted, never recorded, etc).
        /// </summary>
        public bool TryGetBattleReport(int id, out BattleResult result)
        {
            if (id > 0)
            {
                for (int i = battleArchive.Count - 1; i >= 0; i--)
                {
                    BattleResult r = battleArchive[i];
                    if (r is object && r.reportId == id)
                    {
                        result = r;
                        return true;
                    }
                }
            }
            result = null;
            return false;
        }

        /// <summary>
        /// Newest-first enumeration of archived battle reports. Snapshot — safe to mutate
        /// the archive while iterating.
        /// </summary>
        public IEnumerable<BattleResult> RecentBattleReports
        {
            get
            {
                for (int i = battleArchive.Count - 1; i >= 0; i--)
                    yield return battleArchive[i];
            }
        }

        public int BattleReportCount => battleArchive.Count;

        /// <summary>Convenience accessor for callers that don't want to handle null.</summary>
        public static WorldComponent_Archive Get()
        {
            return Find.World?.GetComponent<WorldComponent_Archive>();
        }
    }
}
