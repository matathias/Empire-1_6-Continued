using HarmonyLib;
using RimWorld.Planet;
using System;
using System.Threading;

namespace FactionColonies
{
    /// <summary>
    /// Makes WorldPathPool thread-safe so FCRoadQueue can run A* pathfinding
    /// on a background thread. The lock is only held during pool operations;
    /// the actual A* computation runs completely lock-free.
    ///
    /// Note: The pool's leak detection (paths.Count > CaravansCount + 2) may
    /// fire an ErrorOnce when a background thread has borrowed a path, since
    /// the count temporarily exceeds the expected threshold. This is manageable;
    /// the orphaned path is GC'd and the ErrorOnce is suppressed after the
    /// first occurrence.
    /// </summary>
    [HarmonyPatch(typeof(WorldPathPool))]
    [HarmonyPatch("GetEmptyWorldPath")]
    class Patch_WorldPathPool_GetEmptyWorldPath
    {
        internal static readonly object PoolLock = new object();

        static void Prefix()
        {
            Monitor.Enter(PoolLock);
        }

        static Exception Finalizer(Exception __exception)
        {
            Monitor.Exit(PoolLock);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(WorldPath))]
    [HarmonyPatch("ReleaseToPool")]
    class Patch_WorldPath_ReleaseToPool
    {
        static void Prefix()
        {
            Monitor.Enter(Patch_WorldPathPool_GetEmptyWorldPath.PoolLock);
        }

        static Exception Finalizer(Exception __exception)
        {
            Monitor.Exit(Patch_WorldPathPool_GetEmptyWorldPath.PoolLock);
            return __exception;
        }
    }
}
