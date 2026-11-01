using RimWorld;
using Verse;

namespace FactionColonies
{
    [DefOf]
    public class ResourceTypeDefOf
    {
        public static ResourceTypeDef RTD_Food;
        public static ResourceTypeDef RTD_Weapons;
        public static ResourceTypeDef RTD_Apparel;
        public static ResourceTypeDef RTD_Animals;
        public static ResourceTypeDef RTD_Logging;
        public static ResourceTypeDef RTD_Mining;
        public static ResourceTypeDef RTD_Research;
        public static ResourceTypeDef RTD_Power;
        public static ResourceTypeDef RTD_Medicine;
        public static ResourceTypeDef RTD_Chemfuel;
        [MayRequire("Ludeon.RimWorld.Odyssey")]
        public static ResourceTypeDef RTD_Gravtech;

        static ResourceTypeDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ResourceTypeDefOf));
        }
    }
}
