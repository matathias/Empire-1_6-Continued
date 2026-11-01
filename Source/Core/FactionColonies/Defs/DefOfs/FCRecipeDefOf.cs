using RimWorld;
using Verse;

namespace FactionColonies
{
    [DefOf]
    public static class FCRecipeDefOf
    {
        // Vanilla (Core) surgery that installs the death acidifier implant. Used to auto-apply the
        // acidifier to humanlike mercs through the game's own Recipe_InstallImplant worker.
        public static RecipeDef InstallDeathAcidifier;

        static FCRecipeDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FCRecipeDefOf));
        }
    }
}
