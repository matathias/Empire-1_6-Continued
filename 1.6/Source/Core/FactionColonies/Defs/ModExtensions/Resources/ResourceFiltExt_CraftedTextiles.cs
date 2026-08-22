using RimWorld;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Allows a resource to produce fabric textiles that are made by a crafting recipe rather than
    /// harvested from a crop or produced by an organism. Each fabric is gated behind the research
    /// needed to craft it, so it only becomes available once the empire could actually make it.
    /// </summary>
    public class ResourceFilterExtension_CraftedTextiles : ResourceFilterExtension
    {
        /// <summary>
        /// Categories whose direct fabric members are considered (e.g. Textiles).
        /// </summary>
        public List<ThingCategoryDef> categories = new List<ThingCategoryDef>();

        /// <summary>
        /// Each fabric mapped to one research-prerequisite set per crafting recipe that produces it. A
        /// null or empty set means that producer is ungated. Built once (research-independent); the cheap
        /// <see cref="ResearchProjectDef.IsFinished"/> checks happen per call.
        /// </summary>
        private Dictionary<ThingDef, List<List<ResearchProjectDef>>> _craftedFabrics;
        private Dictionary<ThingDef, List<List<ResearchProjectDef>>> CraftedFabrics => _craftedFabrics ?? (_craftedFabrics = BuildCraftedFabrics());

        public override void SetFilter(ThingFilter filter, TechLevel techlevel, ResourceFC resource = null)
        {
            // A non-null resource signals the real production/tithe path; trade passes null.
            bool applyResearchGate = resource is object;

            foreach (KeyValuePair<ThingDef, List<List<ResearchProjectDef>>> fabric in CraftedFabrics)
            {
                // Keep spacer/ultra fabrics (synthread, hyperweave) out of low-tech empires
                if (applyResearchGate && techlevel < fabric.Key.techLevel) continue;
                if (applyResearchGate && !AnyProducerAvailable(fabric.Value)) continue;
                filter.SetAllow(fabric.Key, true);
            }
        }

        public override void SetFilterForCodex(ThingFilter filter, Dictionary<ThingDef, TitheRestrictionInfo> restrictions)
        {
            foreach (KeyValuePair<ThingDef, List<List<ResearchProjectDef>>> fabric in CraftedFabrics)
            {
                filter.SetAllow(fabric.Key, true);

                // Show a research requirement only when there is no ungated way to craft this fabric.
                List<ResearchProjectDef> requirement = MinimalRequirement(fabric.Value);
                if (requirement != null)
                {
                    TitheRestrictionInfo info = new TitheRestrictionInfo();
                    info.researchProjects = requirement;
                    restrictions[fabric.Key] = info;
                }
            }
        }

        /// <summary>Returns true if at least one producing recipe has all of its research prerequisites finished.</summary>
        private static bool AnyProducerAvailable(List<List<ResearchProjectDef>> producers)
        {
            foreach (List<ResearchProjectDef> prereqs in producers)
            {
                if (AllFinished(prereqs)) return true;
            }
            return false;
        }

        private static bool AllFinished(List<ResearchProjectDef> prereqs)
        {
            if (prereqs.NullOrEmpty()) return true;
            foreach (ResearchProjectDef proj in prereqs)
            {
                if (proj is object && !proj.IsFinished) return false;
            }
            return true;
        }

        /// <summary>
        /// The research requirement to display for a fabric: null if any producer is ungated (always
        /// craftable), otherwise the smallest prerequisite set among gated producers (the easiest to unlock).
        /// </summary>
        private static List<ResearchProjectDef> MinimalRequirement(List<List<ResearchProjectDef>> producers)
        {
            List<ResearchProjectDef> smallest = null;
            foreach (List<ResearchProjectDef> prereqs in producers)
            {
                if (prereqs.NullOrEmpty()) return null;   // an ungated producer exists
                if (smallest is null || prereqs.Count < smallest.Count) smallest = prereqs;
            }
            return smallest;
        }

        private Dictionary<ThingDef, List<List<ResearchProjectDef>>> BuildCraftedFabrics()
        {
            // The fabrics we care about: direct members of the configured categories (excludes the
            // Leathers/Wools subcategories, which are owned by other resources/extensions).
            HashSet<ThingDef> fabrics = new HashSet<ThingDef>();
            foreach (ThingCategoryDef category in categories)
            {
                if (category is null) continue;
                foreach (ThingDef child in category.childThingDefs)
                {
                    if (child is object) fabrics.Add(child);
                }
            }

            Dictionary<ThingDef, List<List<ResearchProjectDef>>> outputs = new Dictionary<ThingDef, List<List<ResearchProjectDef>>>();
            if (fabrics.Count == 0) return outputs;

            foreach (RecipeDef recipe in DefDatabase<RecipeDef>.AllDefs)
            {
                if (recipe.products.NullOrEmpty()) continue;

                foreach (ThingDefCountClass product in recipe.products)
                {
                    if (product?.thingDef is null || !fabrics.Contains(product.thingDef)) continue;

                    List<ResearchProjectDef> prereqs = new List<ResearchProjectDef>();
                    if (recipe.researchPrerequisite is object) prereqs.Add(recipe.researchPrerequisite);
                    if (!recipe.researchPrerequisites.NullOrEmpty()) prereqs.AddRange(recipe.researchPrerequisites);

                    List<List<ResearchProjectDef>> producers;
                    if (!outputs.TryGetValue(product.thingDef, out producers))
                    {
                        producers = new List<List<ResearchProjectDef>>();
                        outputs[product.thingDef] = producers;
                    }
                    producers.Add(prereqs);
                }
            }

            return outputs;
        }
    }
}
