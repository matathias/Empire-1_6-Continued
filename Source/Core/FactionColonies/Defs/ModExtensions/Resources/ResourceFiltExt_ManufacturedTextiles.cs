using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Allows a resource to produce manufactured leathers/wools: members of the given textile
    /// <see cref="categories"/> (e.g. Leathers, Wools) that are not produced directly by an organism.
    /// A leather/wool is considered organism-derived if it is some race's <c>leatherDef</c> or a
    /// shearable animal's <c>woolDef</c> — those belong to the animal resource. Everything else in
    /// the categories (e.g. patchleather, crafted from other leathers, plus modded composite textiles)
    /// is manufactured and belongs here.
    /// <para>Only ever *adds* allowances (never blocks), so it composes with the resource's own
    /// category allow lists. Direct-Textiles fabrics (cloth, devilstrand, synthread, hyperweave) are
    /// not under the Leathers/Wools subcategories and so are untouched.</para>
    /// </summary>
    public class ResourceFilterExtension_ManufacturedTextiles : ResourceFilterExtension
    {
        /// <summary>
        /// Textile categories whose non-organism-derived members are allowed (e.g. Leathers, Wools).
        /// </summary>
        public List<ThingCategoryDef> categories = new List<ThingCategoryDef>();

        private HashSet<ThingDef> _organismDerived;
        private HashSet<ThingDef> OrganismDerived => _organismDerived ?? (_organismDerived = BuildOrganismDerivedSet());

        public override void SetFilter(ThingFilter filter, TechLevel techlevel, ResourceFC resource = null)
        {
            HashSet<ThingDef> organismDerived = OrganismDerived;
            foreach (ThingCategoryDef category in categories)
            {
                if (category is null) continue;
                foreach (ThingDef descendant in category.DescendantThingDefs)
                {
                    if (!organismDerived.Contains(descendant))
                    {
                        filter.SetAllow(descendant, true);
                    }
                }
            }
        }

        private static HashSet<ThingDef> BuildOrganismDerivedSet()
        {
            HashSet<ThingDef> organismDerived = new HashSet<ThingDef>();
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
            {
                // Any race (animal or humanlike) contributes its leather; shearable animals their wool.
                if (def.race is null) continue;

                if (def.race.leatherDef is object) organismDerived.Add(def.race.leatherDef);

                CompProperties_Shearable shearable = def.GetCompProperties<CompProperties_Shearable>();
                if (shearable?.woolDef is object) organismDerived.Add(shearable.woolDef);
            }
            return organismDerived;
        }
    }
}
