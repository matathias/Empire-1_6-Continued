using RimWorld;
using Verse;

namespace FactionColonies
{
    [DefOf]
    public class FCPolicyDefOf
    {
        //Faction Traits
        public static FCPolicyDef empty;

        public static FCPolicyDef resilient;
        public static FCPolicyDef raiders;
        public static FCPolicyDef defenseInDepth;
        public static FCPolicyDef industrious;
        public static FCPolicyDef roadBuilders;
        public static FCPolicyDef mercantile;
        public static FCPolicyDef innovative;
        public static FCPolicyDef lucky;

        //Policies
        public static FCPolicyDef militaristic;
        public static FCPolicyDef pacifist;
        public static FCPolicyDef authoritarian;
        public static FCPolicyDef egalitarian;
        public static FCPolicyDef isolationist;
        public static FCPolicyDef expansionist;
        public static FCPolicyDef technocratic;
        public static FCPolicyDef feudal;
        public static FCPolicyDef slaver;

        static FCPolicyDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FCPolicyDefOf));
        }
    }
}
