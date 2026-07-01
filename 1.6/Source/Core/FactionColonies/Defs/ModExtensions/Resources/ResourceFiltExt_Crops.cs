using RimWorld;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Allows a resource to produce the harvested output of any sowable, non-tree crop, so long as
    /// that output does not fall under one of the <see cref="blockedCategories"/> (which belong to
    /// another resource). This broadens the resource beyond a fixed item list, automatically
    /// capturing modded crops that yield non-food items (e.g. cotton -> cloth, devilstrand -> devilstrand cloth).
    /// <para>Only ever *adds* allowances (never blocks), so it composes with whatever the resource's
    /// own category allow lists already permit (e.g. the Foods category continues to cover food crops).</para>
    /// <para>Trees are skipped so wood stays with its own resource.</para>
    /// </summary>
    public class ResourceFilterExtension_Crops : ResourceFilterExtension
    {
        /// <summary>
        /// Harvest outputs falling under any of these thing categories are excluded, keeping this
        /// resource mutually exclusive with whichever resource owns those categories (e.g. Medicine/Drugs).
        /// </summary>
        public List<ThingCategoryDef> blockedCategories = new List<ThingCategoryDef>();

        private HashSet<ThingDef> _cropOutputs;
        private HashSet<ThingDef> CropOutputs => _cropOutputs ?? (_cropOutputs = BuildCropOutputSet());

        public override void SetFilter(ThingFilter filter, TechLevel techlevel, ResourceFC resource = null)
        {
            foreach (ThingDef output in CropOutputs)
            {
                filter.SetAllow(output, true);
            }
        }

        private HashSet<ThingDef> BuildCropOutputSet()
        {
            HashSet<ThingDef> blocked = new HashSet<ThingDef>();
            foreach (ThingCategoryDef category in blockedCategories)
            {
                if (category is null) continue;
                foreach (ThingDef descendant in category.DescendantThingDefs)
                {
                    blocked.Add(descendant);
                }
            }

            HashSet<ThingDef> outputs = new HashSet<ThingDef>();
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
            {
                PlantProperties plant = def.plant;
                if (plant is null) continue;
                if (!plant.Sowable) continue;   // wild plants aren't crops
                if (plant.IsTree) continue;     // wood belongs to the logging resource

                ThingDef harvest = plant.harvestedThingDef;
                if (harvest is null) continue;
                if (blocked.Contains(harvest)) continue;   // owned by another resource (e.g. Medicine/Drugs)

                outputs.Add(harvest);
            }

            return outputs;
        }
    }
}
