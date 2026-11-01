using AlienRace;
using FactionColonies.util;
using Verse;

namespace FactionColonies.HAR
{
    /// <summary>
    /// Registers the HAR bridge on game startup.
    /// This class only exists in Empire.HAR.dll, which is only loaded when HAR is active
    /// (via LoadFolders.xml conditional loading).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HARInit
    {
        static HARInit()
        {
            HARUtil.Bridge = new HARBridge();
            LogUtil.MessageForce("Humanoid Alien Races compatibility module loaded.");
        }
    }

    /// <summary>
    /// Direct HAR API implementation of IHARBridge.
    /// Uses direct calls to <see cref="RaceRestrictionSettings"/> static methods.
    /// </summary>
    public class HARBridge : IHARBridge
    {
        public bool CanRaceWearApparel(ThingDef race, ThingDef apparel)
        {
            if (race == null || apparel == null) return true;
            return RaceRestrictionSettings.CanWear(apparel, race);
        }

        public bool CanRaceUseWeapon(ThingDef race, ThingDef weapon)
        {
            if (race == null || weapon == null) return true;
            return RaceRestrictionSettings.CanEquip(weapon, race);
        }
    }
}
