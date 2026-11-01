using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FactionColonies
{

    public class MilitaryFireSupport : IExposable, ILoadReferenceable
    {
        public int loadID = -1;
        public string name;
        public float totalCost;
        public int timeRunning;
        public int ticksTillEnd;
        public float accuracy = 15;
        public string fireSupportType;
        public Map map;
        public IntVec3 location;
        public int startupTime;
        public IntVec3 sourceLocation;
        public List<ThingDef> projectiles;

        public bool ShouldBeOver => ticksTillEnd <= Find.TickManager.TicksGame;

        public MilitaryFireSupport()
        {
        }

        public MilitaryFireSupport(string fireSupportType, Map map, IntVec3 location, int ticksTillEnd, int startupTime,
            float accuracy, List<ThingDef> projectiles = null, int settlementTile = -1)
        {
            this.fireSupportType = fireSupportType;
            this.ticksTillEnd = Find.TickManager.TicksGame + ticksTillEnd + startupTime;
            this.accuracy = accuracy;
            this.map = map;
            this.location = location;
            this.startupTime = startupTime;
            if (settlementTile >= 0)
            {
                Rot4 direction = Find.WorldGrid.GetRotFromTo(map.Tile, settlementTile);
                sourceLocation = CellFinder.RandomEdgeCell(direction, map);
            }
            else
            {
                sourceLocation = CellFinder.RandomEdgeCell(map);
            }
            this.projectiles = projectiles;
        }

        public string GetUniqueLoadID()
        {
            return $"MilitaryFireSupport_{loadID}";
        }

        public void SetLoadID()
        {
            loadID = FindFC.Military.NextMilitaryFireSupportId();
        }

        public static float CalculateAccuracyCostPercentage(float accuracy)
        {
            return (float)Math.Round((Math.Max(0, 15 - accuracy) / 15) * 100);
        }

        public static float CalculateTotalCost(float accuracy, IEnumerable<float> projectileMarketValues)
        {
            float cost = 0;
            float accuracyMult = 1 + CalculateAccuracyCostPercentage(accuracy) / 100;
            foreach (float marketValue in projectileMarketValues)
            {
                cost += marketValue * 1.5f * accuracyMult;
            }
            return (float)Math.Round(cost);
        }

        public float ReturnAccuracyCostPercentage()
        {
            return CalculateAccuracyCostPercentage(accuracy);
        }

        public float ReturnTotalCost(WorldSettlementFC settlement = null)
        {
            totalCost = CalculateTotalCost(accuracy, projectiles.Select(def => def.BaseMarketValue));
            if (settlement != null)
                totalCost *= (float)settlement.GetStatValue(FCStatDefOf.fireSupportCostMultiplier);
            return totalCost;
        }

        public void Delete()
        {
            FindFC.Military.fireSupportDefs.Remove(this);
        }

        public ThingDef ExpendProjectile()
        {
            if (!projectiles.Any()) return null;
            ThingDef projectile = projectiles[0];
            projectiles.RemoveAt(0);
            return projectile;
        }

        public List<ThingDef> ReturnFireSupportOptions()
        {
            // return list of thingdefs that can be used as fire support
            ThingSetMaker thingSetMaker = new ThingSetMaker_Count();
            ThingSetMakerParams param = new ThingSetMakerParams();
            param.filter = new ThingFilter();
            param.techLevel = FindFC.TechLevel;

            param.filter.SetAllow(DefDatabase<ThingCategoryDef>.GetNamedSilentFail("MortarShells"), true);
            if (DefDatabase<ThingCategoryDef>.GetNamedSilentFail("AmmoShells") != null)
                param.filter.SetAllow(DefDatabase<ThingCategoryDef>.GetNamedSilentFail("AmmoShells"), true);
            List<ThingDef> list = thingSetMaker.AllGeneratableThingsDebug(param).ToList();

            // When CE is loaded, filter out direct-fire cannon shells (90mm, 37mm, etc.)
            // and only keep shells whose AmmoSetDef has isMortarAmmoSet = true.
            if (CombatExtendedUtil.IsCELoaded)
            {
                list = list.Where(def => CombatExtendedUtil.IsIndirectFireAmmo(def)).ToList();
            }

            return list;
        }

        private bool ShouldFire => (timeRunning % 15) == 0 && timeRunning >= startupTime;

        private IntVec3 SemiRandomSpawnCenter => (from x in GenRadial.RadialCellsAround(location, accuracy, true) where x.InBounds(map) select x).RandomElementByWeight(x => new SimpleCurve { new CurvePoint(0f, 1f), new CurvePoint(accuracy, 0.1f) }.Evaluate(x.DistanceTo(location)));

        /// <summary>
        /// Launch a fire support projectile using CE's ballistic system.
        /// Returns true on success, false if caller should fall back to vanilla.
        /// </summary>
        private bool DoCombatExtendedLaunch(ThingDef ammoDef, IntVec3 spawnCenter)
        {
            return CombatExtendedUtil.LaunchFireSupportProjectile(ammoDef, map, sourceLocation, spawnCenter);
        }

        public void Process()
        {
            if (fireSupportType == "fireSupport")
            {
                if (ShouldFire)
                {
                    IntVec3 spawnCenter = SemiRandomSpawnCenter;
                    ThingDef ammoDef = ExpendProjectile();
                    if (ammoDef == null) return;

                    if (CombatExtendedUtil.IsCELoaded)
                    {
                        if (!DoCombatExtendedLaunch(ammoDef, spawnCenter))
                        {
                            // CE launch failed, fall back to vanilla
                            LaunchVanillaProjectile(ammoDef, spawnCenter);
                        }
                    }
                    else
                    {
                        LaunchVanillaProjectile(ammoDef, spawnCenter);
                    }
                }
            }

            timeRunning++;
        }

        private void LaunchVanillaProjectile(ThingDef ammoDef, IntVec3 spawnCenter)
        {
            ThingDef def = ammoDef.projectileWhenLoaded;
            if (def is null)
            {
                LogUtil.Warning($"Fire support ammo {ammoDef.defName} has no projectileWhenLoaded defined");
                return;
            }
            LocalTargetInfo info = new LocalTargetInfo(spawnCenter);
            Projectile projectile = (Projectile)GenSpawn.Spawn(def, sourceLocation, map);
            projectile.Launch(null, info, info, ProjectileHitFlags.All);
        }


        public void ExposeData()
        {
            Scribe_Values.Look(ref loadID, "loadID");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref timeRunning, "timeRunning");
            Scribe_Values.Look(ref ticksTillEnd, "ticksTillEnd");
            Scribe_Values.Look(ref accuracy, "accuracy");
            Scribe_Values.Look(ref fireSupportType, "fireSupportType");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref location, "location");
            Scribe_Values.Look(ref sourceLocation, "sourceLocation");
            Scribe_Values.Look(ref startupTime, "startupTime");
            Scribe_Collections.Look(ref projectiles, "projectiles", LookMode.Def);
        }
    }
}