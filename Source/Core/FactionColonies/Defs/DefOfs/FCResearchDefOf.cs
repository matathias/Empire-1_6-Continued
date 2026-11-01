using RimWorld;
using Verse;

namespace FactionColonies
{
    [DefOf]
    public static class FCResearchDefOf
    {
        public static ResearchProjectDef Hydroponics;

        static FCResearchDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FCResearchDefOf));
        }
    }
}
