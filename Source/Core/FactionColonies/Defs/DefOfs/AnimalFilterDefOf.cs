using RimWorld;
using Verse;

namespace FactionColonies
{
    [DefOf]
    public static class AnimalFilterDefOf
    {
        // Combat
        public static PawnKindDef Husky;
        public static PawnKindDef Wolf_Timber;
        public static PawnKindDef Wolf_Arctic;
        public static PawnKindDef Lynx;
        public static PawnKindDef Cougar;

        // Pack
        public static PawnKindDef Muffalo;
        public static PawnKindDef Dromedary;
        public static PawnKindDef Horse;
        public static PawnKindDef Donkey;
        public static PawnKindDef Elephant;
        public static PawnKindDef Yak;

        static AnimalFilterDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(AnimalFilterDefOf));
        }
    }
}
