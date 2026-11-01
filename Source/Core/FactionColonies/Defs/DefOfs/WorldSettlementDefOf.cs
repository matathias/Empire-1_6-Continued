using RimWorld;
using Verse;

namespace FactionColonies
{
    [DefOf]
    public static class WorldSettlementDefOf
    {
        public static WorldSettlementDef WorldSettlementDef_Surface;
        [MayRequire("Ludeon.RimWorld.Odyssey")]
        public static WorldSettlementDef WorldSettlementDef_Orbital;
        static WorldSettlementDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(WorldSettlementDefOf));
        }
    }
}
