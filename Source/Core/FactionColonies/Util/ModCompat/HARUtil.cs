using Verse;

namespace FactionColonies.util
{
    /// <summary>
    /// Bridge interface implemented by Empire.HAR.dll (loaded only when Humanoid Alien Races is active).
    /// The implementation registers itself via [StaticConstructorOnStartup].
    /// </summary>
    public interface IHARBridge
    {
        bool CanRaceWearApparel(ThingDef race, ThingDef apparel);
        bool CanRaceUseWeapon(ThingDef race, ThingDef weapon);
    }

    /// <summary>
    /// Thin facade for Humanoid Alien Races compatibility.
    /// When HAR is active, Empire.HAR.dll registers an IHARBridge implementation
    /// via [StaticConstructorOnStartup]. All HAR interaction from the main assembly goes
    /// through this class. If the bridge is not registered, all methods return true (permissive).
    /// </summary>
    public static class HARUtil
    {
        /// <summary>
        /// Set by the Empire.HAR assembly on startup. Null when HAR is not loaded.
        /// </summary>
        public static IHARBridge Bridge { get; set; }

        /// <summary>
        /// Whether a given race can wear the specified apparel, according to HAR's race restrictions.
        /// Returns true when HAR is not loaded or race is null.
        /// </summary>
        public static bool CanRaceWearApparel(ThingDef race, ThingDef apparel)
        {
            if (Bridge == null || race == null || apparel == null) return true;
            return Bridge.CanRaceWearApparel(race, apparel);
        }

        /// <summary>
        /// Whether a given race can use the specified weapon, according to HAR's race restrictions.
        /// Returns true when HAR is not loaded or race is null.
        /// </summary>
        public static bool CanRaceUseWeapon(ThingDef race, ThingDef weapon)
        {
            if (Bridge == null || race == null || weapon == null) return true;
            return Bridge.CanRaceUseWeapon(race, weapon);
        }
    }
}
