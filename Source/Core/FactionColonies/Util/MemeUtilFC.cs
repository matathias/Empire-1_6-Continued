using RimWorld;
using Verse;

namespace FactionColonies
{
    /*-*-*-*-*/
    /* Ideology meme helpers for the empire faction.
     * The empire faction inherits the player's primary ideo (see ColonyUtil), so meme checks
     * read its PrimaryIdeo, falling back to the player faction's. All entry points are safe to
     * call without the Ideology DLC — they return false / the raw defName when it's absent. */
    /*-*-*-*-*/
    public static class MemeUtilFC
    {
        /// <summary>
        /// True if Ideology is active and the empire faction's primary ideo has the named meme.
        /// </summary>
        public static bool EmpireHasMeme(string memeDefName)
        {
            if (!ModsConfig.IdeologyActive || memeDefName.NullOrEmpty()) return false;

            MemeDef meme = DefDatabase<MemeDef>.GetNamedSilentFail(memeDefName);
            if (meme is null) return false;

            Ideo ideo = FindFC.EmpireFaction?.ideos?.PrimaryIdeo
                        ?? Faction.OfPlayer?.ideos?.PrimaryIdeo;
            return ideo?.HasMeme(meme) ?? false;
        }

        /// <summary>
        /// Display label for a meme defName (e.g. for the option requirement tag). Falls back to
        /// the raw defName when the meme can't be resolved (no Ideology / unknown name).
        /// </summary>
        public static string EmpireMemeLabel(string memeDefName)
        {
            if (memeDefName.NullOrEmpty()) return memeDefName;
            MemeDef meme = DefDatabase<MemeDef>.GetNamedSilentFail(memeDefName);
            return meme is object ? meme.LabelCap.ToString() : memeDefName;
        }
    }
}
