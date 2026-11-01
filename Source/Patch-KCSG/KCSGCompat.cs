using HarmonyLib;
using KCSG;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace FactionColonies.KCSG
{
    /// <summary>
    /// Compatibility patch for Vanilla Expanded Framework (KCSG module).
    /// This assembly is only loaded when VEF is active (via LoadFolders.xml).
    ///
    /// Fixes IndexOutOfRangeException in KCSG's SettlementGenUtils.BuildingPlacement.CanPlaceAt
    /// where roof clearance checks don't verify cells are within map bounds.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class KCSGCompatInit
    {
        static KCSGCompatInit()
        {
            new Harmony("com.Matathias.Empire.KCSG").PatchAll(Assembly.GetExecutingAssembly());
            LogUtil.MessageForce("KCSG compatibility module loaded.");
        }
    }

    [HarmonyPatch(typeof(SettlementGenUtils.BuildingPlacement))]
    [HarmonyPatch("CanPlaceAt")]
    public static class Patch_CanPlaceAt
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var roofedMethod = AccessTools.Method(typeof(GridsUtility), "Roofed",
                new[] { typeof(IntVec3), typeof(Map) });
            var safeMethod = AccessTools.Method(typeof(Patch_CanPlaceAt), "RoofedSafe");

            var codes = instructions.ToList();
            int replaced = 0;
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(roofedMethod))
                {
                    codes[i].operand = safeMethod;
                    replaced++;
                }
            }

            if (replaced == 0)
                LogUtil.Warning("KCSG compat: Could not find Roofed call to patch in CanPlaceAt");

            return codes;
        }

        public static bool RoofedSafe(IntVec3 c, Map map)
        {
            return c.InBounds(map) && c.Roofed(map);
        }
    }
}
