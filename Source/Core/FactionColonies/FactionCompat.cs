using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// A class for tracking whether certain other mods are active. Useful for dynamic UI rendering and stuff.
    /// </summary>
    public static class FactionCompat
    {
        private static bool isGiddyUp2Active = false;
        private static bool isVPEActive = false;
        private static bool isCombatExtendedActive = false;

        public static bool GiddyUp2Active => isGiddyUp2Active;
        public static bool VPEActive => isVPEActive;
        public static bool CombatExtendedActive => isCombatExtendedActive;

        public static void CheckForMods()
        {
            isGiddyUp2Active = ModActive("MemeGoddess.GiddyUp");
            isVPEActive = ModActive("VanillaExpanded.VPsycastsE");
            isCombatExtendedActive = ModActive("CETeam.CombatExtended");

            LogUtil.Message($"[FactionCompat] Giddy Up 2 Active: {GiddyUp2Active}");
            LogUtil.Message($"[FactionCompat] Vanilla Psycasts Expanded Active: {VPEActive}");
            LogUtil.Message($"[FactionCompat] Combat Extended Active: {CombatExtendedActive}");
        }

        private static bool ModActive(string packageId)
        {
            string target = packageId.ToLower();
            return LoadedModManager.RunningMods.Any(mod => mod.PackageId.ToLower() == target);
        }
    }
}
