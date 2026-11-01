using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Declares which items a unit may carry in its inventory, as a BLOCKLIST over every ThingDef.
    /// The unit designer's inventory picker starts from the full ThingDef pool and removes the union
    /// of every FCInventoryCategoryDef's <see cref="excludeCategories"/> (expanded to all descendant
    /// ThingDefs) and <see cref="excludeThings"/>; <see cref="categories"/>/<see cref="things"/> then
    /// force-allow items back (allow wins over exclude). Submods restrict the pool simply by shipping
    /// their own FCInventoryCategoryDef (or PatchOperation-ing the base one) — no code changes
    /// required, and items a mod adds under any category appear automatically.
    /// </summary>
    public class FCInventoryCategoryDef : Def
    {
        /// <summary>Force-allow whole categories — every descendant ThingDef is re-admitted to the
        /// pool even if an <see cref="excludeCategories"/>/<see cref="excludeThings"/> entry would
        /// remove it. Allow wins over exclude. The base pool is already every ThingDef, so this is
        /// only needed to re-admit a child of a broadly-blocked subtree.</summary>
        public List<ThingCategoryDef> categories = new List<ThingCategoryDef>();

        /// <summary>Individual items to force-allow. Wins over excludes (un-block override).</summary>
        public List<ThingDef> things = new List<ThingDef>();

        /// <summary>Whole categories to block — every descendant ThingDef is removed from the
        /// all-ThingDef base pool, unless re-admitted by <see cref="categories"/>/<see cref="things"/>.</summary>
        public List<ThingCategoryDef> excludeCategories = new List<ThingCategoryDef>();

        /// <summary>Individual items to block, unless re-admitted by <see cref="categories"/>/<see cref="things"/>.</summary>
        public List<ThingDef> excludeThings = new List<ThingDef>();
    }
}
