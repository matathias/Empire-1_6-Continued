using Verse;

namespace FactionColonies.util
{
    /// <summary>
    /// Interface implemented by the Empire.GiddyUp assembly (loaded only when Giddy Up 2 is active).
    /// The implementation registers itself via [StaticConstructorOnStartup]. Keeping every typed GU2
    /// reference behind this interface lets the main assembly compile and run without Giddy Up 2.
    /// </summary>
    public interface IGiddyUpBridge
    {
        /// <summary>Whether the given animal kind can ever be ridden, per GU2's own mountable rules
        /// (settings + Mountable/NotMountable mod extensions). Used to filter the mount picker.</summary>
        bool IsMountable(PawnKindDef kind);

        /// <summary>Mounts the rider on the animal (issues GU2's Mount job), idempotent if the rider is
        /// already mounted. Used at battle/deploy spawn so the merc ends up mounted.</summary>
        void Mount(Pawn rider, Pawn mount);
    }

    /// <summary>
    /// Thin facade for Giddy Up 2 compatibility. When GU2 is active, Empire.GiddyUp.dll registers an
    /// <see cref="IGiddyUpBridge"/> implementation via [StaticConstructorOnStartup]. All GU2 interaction
    /// from the main assembly goes through this class — callers never need to know whether the bridge is
    /// present. If the bridge is not registered, every method gracefully no-ops or returns false.
    /// Whether the mount UI shows at all is gated separately by <see cref="FactionCompat.GiddyUp2Active"/>.
    /// </summary>
    public static class GiddyUpUtil
    {
        /// <summary>Set by the Empire.GiddyUp assembly on startup. Null when GU2 is not loaded.</summary>
        public static IGiddyUpBridge Bridge { get; set; }

        public static bool IsMountable(PawnKindDef kind) => Bridge?.IsMountable(kind) ?? false;

        public static void Mount(Pawn rider, Pawn mount) => Bridge?.Mount(rider, mount);
    }
}
