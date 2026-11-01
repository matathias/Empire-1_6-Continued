using HarmonyLib;
using RimWorld.Planet;

namespace FactionColonies
{
    /// <summary>
    /// Triggers road network recalculation when non-Empire settlements are
    /// added, removed, or change faction on the world map.
    /// </summary>

    [HarmonyPatch(typeof(WorldObjectsHolder), nameof(WorldObjectsHolder.Add))]
    class WorldObjectAddPatch
    {
        public static void Postfix(WorldObject o)
        {
            if (o is Settlement)
                FindFC.RoadBuilder?.FlagUpdateRoadQueues();
        }
    }

    [HarmonyPatch(typeof(WorldObjectsHolder), nameof(WorldObjectsHolder.Remove))]
    class WorldObjectRemovePatch
    {
        public static void Postfix(WorldObject o)
        {
            if (o is Settlement)
                FindFC.RoadBuilder?.FlagUpdateRoadQueues();
        }
    }

    [HarmonyPatch(typeof(WorldObject), nameof(WorldObject.SetFaction))]
    class WorldObjectSetFactionPatch
    {
        public static void Postfix(WorldObject __instance)
        {
            if (__instance is Settlement)
                FindFC.RoadBuilder?.FlagUpdateRoadQueues();
        }
    }
}
