using RimWorld;
using Verse;

namespace FactionColonies
{
    [DefOf]
    public static class PColonyPawnKindDefOf
    {
        public static PawnKindDef PColony_Fighter;
        public static PawnKindDef PColony_Elite;
        public static PawnKindDef PColony_Leader;
        public static PawnKindDef PColony_Trader;
        public static PawnKindDef PColony_Guard;
        public static PawnKindDef PColony_Villager;

        static PColonyPawnKindDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PColonyPawnKindDefOf));
        }
    }
}
