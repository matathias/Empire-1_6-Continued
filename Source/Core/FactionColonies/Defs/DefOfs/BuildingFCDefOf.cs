using RimWorld;
using Verse;

namespace FactionColonies
{
    [DefOf]
    public class BuildingFCDefOf
    {
        public static BuildingFCDef Empty;
        public static BuildingFCDef Construction;
        public static BuildingFCDef artilleryOutpost;
        [MayRequire("Ludeon.RimWorld.Royalty")]
        public static BuildingFCDef shuttlePort;

        static BuildingFCDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(BuildingFCDefOf));
    }
}
