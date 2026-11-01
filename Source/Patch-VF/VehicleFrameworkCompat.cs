using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld.Planet;
using Vehicles;
using Vehicles.World;
using Verse;

namespace FactionColonies.VF
{
    [StaticConstructorOnStartup]
    public static class VehicleFrameworkCompatInit
    {
        static VehicleFrameworkCompatInit()
        {
            new Harmony("com.Matathias.Empire.VF").PatchAll(Assembly.GetExecutingAssembly());
            LogUtil.MessageForce("Vehicle Framework patched");
        }
    }

    /// <summary>
    /// Prefix on CaravanDefend to handle VehicleCaravan pawns correctly.
    /// VehicleCaravan stores passengers inside VehicleRoleHandlers, not in the
    /// base caravan pawn list. Without this patch, vehicles and their passengers
    /// are never spawned onto the defense map.
    /// </summary>
    [HarmonyPatch(typeof(WorldObjectComp_SettlementMilitary))]
    [HarmonyPatch("CaravanDefend")]
    public static class Patch_CaravanDefend_Vehicle
    {
        public static bool Prefix(WorldObjectComp_SettlementMilitary __instance, Caravan caravan)
        {
            if (!(caravan is VehicleCaravan vehicleCaravan))
                return true;

            VehicleCaravanDefend(__instance, vehicleCaravan);
            return false;
        }

        private static void VehicleCaravanDefend(
            WorldObjectComp_SettlementMilitary comp,
            VehicleCaravan vehicleCaravan)
        {
            // Snapshot vehicles and dismounted pawns from the caravan.
            List<VehiclePawn> vehicles = vehicleCaravan.VehiclesListForReading.ListFullCopy();
            List<Pawn> dismounted = vehicleCaravan.DismountedPawnsListForReading.ListFullCopy();

            // Build two pawn lists: one with lord assignment (dismounted + passengers),
            // one without (vehicles). VehiclePawns are excluded from the lord because
            // VF's vehicle job system conflicts with lord duty assignments
            // (LordJob_ColonistsIdle), causing infinite job loops.
            var lordPawns = new List<Pawn>();
            var vehiclePawns = new List<Pawn>();
            foreach (VehiclePawn vehicle in vehicles)
            {
                vehiclePawns.Add(vehicle);
                lordPawns.AddRange(vehicle.AllPawnsAboard);
            }
            lordPawns.AddRange(dismounted);

            // Register non-vehicle pawns with the defense system (lord + defenders).
            // The lord setup callback runs in a deferred LongEventHandler queue,
            // so by the time it fires the pawns are already spawned on the map.
            comp.AddToDefenceFromList(lordPawns, vehicleCaravan.Tile);

            // Track vehicles in defenders, but skip the lord.
            comp.AddToDefenceFromList(vehiclePawns, vehicleCaravan.Tile, assignToLord: false);

            Map map = comp.Map;
            IntVec3 enterCell = BattlefieldContext.FindNearEdgeCell(map);

            // Spawn vehicles BEFORE destroying the caravan. VehicleCaravan.Destroy()
            // explicitly calls vehicle.Destroy() on every VehiclePawn still inside,
            // so we must get them out first. Spawning removes them from caravan.pawns.
            foreach (VehiclePawn vehicle in vehicles)
            {
                IntVec3 loc = CellFinder.RandomSpawnCellForPawnNear(enterCell, map);
                GenSpawn.Spawn(vehicle, loc, map, Rot4.Random);
            }

            // Spawn dismounted pawns normally.
            foreach (Pawn pawn in dismounted)
            {
                IntVec3 loc = CellFinder.RandomSpawnCellForPawnNear(enterCell, map);
                GenSpawn.Spawn(pawn, loc, map, Rot4.Random);
            }

            // Destroy the caravan last. By now caravan.pawns should be empty, so
            // VehicleCaravan.Destroy()'s cleanup loop has nothing to destroy.
            if (!vehicleCaravan.Destroyed)
            {
                if (vehicleCaravan.PawnsListForReading.Count > 0)
                    LogUtil.Error("VF compat: VehicleCaravan still has " + vehicleCaravan.PawnsListForReading.Count
                                  + " pawns after spawning. Destroying anyway — some pawns may be lost.");
                vehicleCaravan.Destroy();
            }
        }
    }
}
