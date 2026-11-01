using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FactionColonies
{
    public class FCPolicyBehavior_Mercantile : FCPolicyBehavior
    {
        private int nextCaravanTick;

        public override void OnEnacted(FactionFC faction)
        {
            ScheduleNextCaravan();
        }

        public override void Tick(FactionFC faction)
        {
            if (nextCaravanTick > Find.TickManager.TicksGame) return;

            // Don't send caravans while the Empire is too unhappy/disloyal/restless; retry later.
            if (faction.ShouldSuppressCaravans())
            {
                ScheduleNextCaravan(true);
                return;
            }

            Map map = faction.TaxMap;
            if (map is null)
            {
                ScheduleNextCaravan(true);
                return;
            }

            try
            {
                IncidentWorker_TraderCaravanArrival worker = new IncidentWorker_TraderCaravanArrival();
                worker.def = IncidentDefOf.TraderCaravanArrival;
                IncidentParms parms =
                    StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, map);
                parms.faction = FindFC.EmpireFaction;
                // Policy-driven trader: bypass storyteller throttling and third-party
                // CanFireNow patches that suppress trader caravans (e.g. RimWar's restrictEvents).
                parms.forced = true;

                if (!worker.CanFireNow(parms))
                {
                    LogUtil.Warning($"Mercantile trader blocked by CanFireNow | colonists on map: {map.mapPawns.FreeColonistsSpawnedCount}, " +
                        $"trader kinds: {parms.faction?.def?.caravanTraderKinds?.Count ?? -1}" +
                        BuildPawnGroupMakerDiagnostic(parms.faction?.def, map, parms));
                    ScheduleNextCaravan(true);
                    return;
                }

                RCellFinder.TryFindRandomPawnEntryCell(out parms.spawnCenter, map, CellFinder.EdgeRoadChance_Friendly);
                parms.spawnRotation = Rot4.FromAngleFlat((map.Center - parms.spawnCenter).AngleFlat);

                bool success = false;
                if (parms.spawnCenter.IsValid)
                {
                    success = worker.TryExecute(parms);
                }
                else
                {
                    LogUtil.Warning("Mercantile - Spawn Center not valid" +
                        BuildPawnGroupMakerDiagnostic(parms.faction?.def, map, parms));
                }

                if (!success)
                {
                    LogUtil.Warning($"Mercantile trader failed to spawn | trader kinds: {parms.faction?.def?.caravanTraderKinds?.Count ?? -1}, " +
                        $"colonists on map: {map.mapPawns.FreeColonistsSpawnedCount}" +
                        BuildPawnGroupMakerDiagnostic(parms.faction?.def, map, parms));
                }

                ScheduleNextCaravan(!success);
            }
            catch (Exception e)
            {
                // Reschedule on the retry cadence so we don't leave nextCaravanTick stale and spam every tick.
                LogUtil.Error($"Mercantile trader threw, possibly a third-party Harmony patch on incident workers. Rescheduling. {e}");
                ScheduleNextCaravan(true);
            }
        }

        /* Diagnostic dump for the Trader pawnGroupMaker selection path. Captures everything
           vanilla PawnGroupKindWorker_Trader.CanGenerateFrom inspects so we can disambiguate
           empty-traders, biome-rejected-carriers, and points-out-of-range failure modes from
           a single warning line. Exception-safe: never masks the real failure. */
        private static string BuildPawnGroupMakerDiagnostic(FactionDef def, Map map, IncidentParms parms)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                List<PawnGroupMaker> list = def?.pawnGroupMakers;
                sb.Append(" | makers=").Append(list is null ? "null" : list.Count.ToString());
                if (list is null) return sb.ToString();

                foreach (PawnGroupMaker m in list)
                {
                    sb.Append(" [").Append(m.kindDef?.defName ?? "null").Append(":");
                    sb.Append("opt=").Append(m.options?.Count ?? 0);
                    sb.Append(",trd=").Append(m.traders?.Count ?? 0);
                    sb.Append(",grd=").Append(m.guards?.Count ?? 0);
                    sb.Append(",car=").Append(m.carriers?.Count ?? 0);
                    sb.Append(",max=").Append(m.maxTotalPoints);
                    sb.Append("]");
                }

                PawnGroupMaker trader = list.FirstOrDefault(m => m.kindDef == PawnGroupKindDefOf.Trader);
                if (trader is null)
                {
                    sb.Append(" | NO_TRADER_MAKER");
                }
                else
                {
                    /* Build the same PawnGroupMakerParms the real call path uses
                       (IncidentWorker_NeutralGroup.SpawnPawns -> GetDefaultPawnGroupMakerParms)
                       so the reported minPoints matches what CanGenerateFrom actually sees. */
                    PawnGroupMakerParms pgmp = parms is object
                        ? IncidentParmsUtility.GetDefaultPawnGroupMakerParms(PawnGroupKindDefOf.Trader, parms, ensureCanGenerateAtLeastOnePawn: true)
                        : null;
                    sb.Append(" | trader.minPoints=").Append(trader.MinPointsToGenerateAnything(def, pgmp));
                    sb.Append(" parms.points=").Append(parms?.points ?? -1f);

                    PlanetTile tile = map?.Tile ?? PlanetTile.Invalid;
                    sb.Append(" tile=").Append(tile);
                    BiomeDef biome = tile.Valid ? Find.WorldGrid[tile].PrimaryBiome : null;
                    sb.Append(" biome=").Append(biome?.defName ?? "null");

                    if (biome is object && trader.carriers is object)
                    {
                        int pass = 0;
                        List<string> passNames = new List<string>(5);
                        List<string> failNames = new List<string>(5);
                        foreach (PawnGenOption c in trader.carriers)
                        {
                            bool ok = biome.IsPackAnimalAllowed(c.kind.race);
                            if (ok)
                            {
                                pass++;
                                if (passNames.Count < 5) passNames.Add(c.kind.defName);
                            }
                            else if (failNames.Count < 5)
                            {
                                failNames.Add(c.kind.defName);
                            }
                        }
                        sb.Append(" biome-pass=").Append(pass).Append("/").Append(trader.carriers.Count);
                        sb.Append(" passEx=[").Append(string.Join(",", passNames.ToArray())).Append("]");
                        sb.Append(" failEx=[").Append(string.Join(",", failNames.ToArray())).Append("]");
                    }
                }

                FactionFC fc = FindFC.FactionComp;
                sb.Append(" | xenoFilter=").Append(fc?.xenotypeFilter is null ? "null" : "set");
                if (fc?.animalFilter is null)
                {
                    sb.Append(" animalFilter=null");
                }
                else
                {
                    sb.Append(" animalFilter.packCount=").Append(fc.animalFilter.AllowedPackAnimals?.Count ?? -1);
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return " | diag-threw:" + ex.GetType().Name + ":" + ex.Message;
            }
        }

        private void ScheduleNextCaravan(bool failCase = false)
        {
            var ext = Ext<FCPolicyBehaviorExt_Mercantile>();
            float days;
            if (failCase)
            {
                days = ext.caravanRetryDays;
            }
            else
            {
                days = Rand.RangeInclusive(ext.caravanMinDays, ext.caravanMaxDays);
            }
            nextCaravanTick = Find.TickManager.TicksGame + (int)(days * GenDate.TicksPerDay);
            LogUtil.Message($"Next Mercantile Caravan set to arrive {days} days from now (on tick {nextCaravanTick})");
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref nextCaravanTick, "nextCaravanTick");
        }

        // Debug accessors
        public int DebugNextCaravanTick() => nextCaravanTick;
        public void DebugResetNextCaravan() => nextCaravanTick = Find.TickManager.TicksGame;
    }
}
