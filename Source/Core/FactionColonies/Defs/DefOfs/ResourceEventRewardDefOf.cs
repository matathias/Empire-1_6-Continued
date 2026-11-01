using RimWorld;
using Verse;

namespace FactionColonies
{
    [DefOf]
    public class ResourceEventRewardDefOf
    {
        public static ResourceEventRewardDef RERD_Food;
        public static ResourceEventRewardDef RERD_Weapons;
        public static ResourceEventRewardDef RERD_Apparel;
        public static ResourceEventRewardDef RERD_Armor;
        public static ResourceEventRewardDef RERD_Animals;
        public static ResourceEventRewardDef RERD_Logging;
        public static ResourceEventRewardDef RERD_Mining;
        public static ResourceEventRewardDef RERD_Drugs;

        static ResourceEventRewardDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ResourceEventRewardDefOf));
        }
    }
}
