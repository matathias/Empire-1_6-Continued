using RimWorld;
using Verse;

namespace FactionColonies
{
    [DefOf]
    public class FCEventCategoryDefOf
    {
        public static FCEventCategoryDef EC_Settlement;
        public static FCEventCategoryDef EC_Construction;
        public static FCEventCategoryDef EC_Economy;
        public static FCEventCategoryDef EC_Policy;
        public static FCEventCategoryDef EC_Military;
        public static FCEventCategoryDef EC_Social;
        public static FCEventCategoryDef EC_Other;

        static FCEventCategoryDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FCEventCategoryDefOf));
        }
    }
}
