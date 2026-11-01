using RimWorld;
using Verse;

namespace FactionColonies
{
    [DefOf]
    public class FCOptionDefOf
    {
        static FCOptionDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FCOptionDefOf));
        }
    }
}
