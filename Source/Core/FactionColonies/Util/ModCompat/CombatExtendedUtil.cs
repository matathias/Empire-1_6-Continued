using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Interface implemented by the Empire.CE assembly (loaded only when Combat Extended is active).
    /// The implementation registers itself via [StaticConstructorOnStartup].
    /// </summary>
    public interface ICombatExtendedBridge
    {
        void UpdateInventory(Pawn pawn);
        bool LaunchFireSupportProjectile(ThingDef ammoDef, Map map, IntVec3 source, IntVec3 target);
        bool IsIndirectFireAmmo(ThingDef ammoDef);
    }

    /// <summary>
    /// Thin facade for Combat Extended compatibility.
    /// When CE is active, Empire.CE.dll registers an ICombatExtendedBridge implementation
    /// via [StaticConstructorOnStartup]. All CE interaction from the main assembly goes
    /// through this class — callers never need to know whether the bridge is present.
    /// If the bridge is not registered, all methods gracefully no-op or return false.
    /// </summary>
    public static class CombatExtendedUtil
    {
        /// <summary>
        /// Set by the Empire.CE assembly on startup. Null when CE is not loaded.
        /// </summary>
        public static ICombatExtendedBridge Bridge { get; set; }

        /// <summary>
        /// Whether Combat Extended is in the active mod list.
        /// </summary>
        public static bool IsCELoaded => FactionCompat.CombatExtendedActive;

        public static void UpdateInventory(Pawn pawn)
            => Bridge?.UpdateInventory(pawn);

        public static bool LaunchFireSupportProjectile(ThingDef ammoDef, Map map, IntVec3 source, IntVec3 target)
            => Bridge?.LaunchFireSupportProjectile(ammoDef, map, source, target) ?? false;

        /// <summary>
        /// Whether the given ThingDef is compatible with indirect fire (mortar/howitzer).
        /// Returns true when CE is not loaded (vanilla shells always pass).
        /// </summary>
        public static bool IsIndirectFireAmmo(ThingDef ammoDef)
            => Bridge?.IsIndirectFireAmmo(ammoDef) ?? true;
    }
}
