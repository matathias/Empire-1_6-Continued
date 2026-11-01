using RimWorld;
using Verse;

namespace FactionColonies
{
    [DefOf]
    public static class FCHediffDefOf
    {
        public static HediffDef FC_CombatEfficiency_Buff;
        public static HediffDef FC_CombatEfficiency_Debuff;

        static FCHediffDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FCHediffDefOf));
        }
    }
}
