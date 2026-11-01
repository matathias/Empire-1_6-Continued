using RimWorld;
using Verse;

namespace FactionColonies
{
    [DefOf]
    public static class FCLetterDefOf
    {
        public static LetterDef FCBattleReportLetterPositive;
        public static LetterDef FCBattleReportLetterNegative;
        public static LetterDef FCBattleReportLetterNeutral;

        static FCLetterDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FCLetterDefOf));
        }
    }
}
