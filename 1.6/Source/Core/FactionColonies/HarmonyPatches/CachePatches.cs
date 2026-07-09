using HarmonyLib;
using System;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Centralized invalidation for all Empire static caches and registries.
    /// Called from Harmony postfixes on both <see cref="Game.Dispose"/> and <see cref="Game.ClearCaches"/>.
    /// Submods register their own cache-clearing callbacks via <see cref="RegisterCacheInvalidator"/>.
    /// </summary>
    public static class EmpireCacheUtil
    {
        private static readonly Dictionary<string, Action> _invalidators = new Dictionary<string, Action>();

        /// <summary>
        /// Registers a cache invalidation callback that will be invoked during <see cref="InvalidateAll"/>.
        /// If a callback with the same key already exists, it is replaced.
        /// </summary>
        public static void RegisterCacheInvalidator(string key, Action callback)
        {
            _invalidators[key] = callback;
        }

        /// <summary>Removes a previously registered cache invalidation callback.</summary>
        public static void UnregisterCacheInvalidator(string key)
        {
            _invalidators.Remove(key);
        }

        public static void InvalidateAll()
        {
            FactionCache.InvalidateCache();
            FindFC.Invalidate();

            EmpireRegistry.ClearAll();

            SettlementTypeExtension_Orbital.InvalidateCache();
            FactionDefDescriptionPatch.Invalidate();
            SettlementCaptureTracker.Reset();

            foreach (KeyValuePair<string, Action> entry in _invalidators)
            {
                try { entry.Value(); }
                catch (Exception e) { LogUtil.Error($"Cache invalidator '{entry.Key}' threw: {e}"); }
            }
        }
    }

    [HarmonyPatch(typeof(Game), "Dispose")]
    class CachePatches
    {
        public static void Postfix()
        {
            EmpireCacheUtil.InvalidateAll();
        }
    }

    /// <summary>
    /// Game.ClearCaches is called at the start of Game.LoadGame() and
    /// Page_SelectScenario.BeginScenarioConfiguration(). This ensures Empire's
    /// caches are invalidated when starting a new game or loading a save, not
    /// just on Game.Dispose(), which doesn't fire when backing out of the new
    /// game flow.
    /// </summary>
    [HarmonyPatch(typeof(Game), nameof(Game.ClearCaches))]
    class ClearCachesPatch
    {
        public static void Postfix()
        {
            EmpireCacheUtil.InvalidateAll();
        }
    }
}
