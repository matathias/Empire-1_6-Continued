using RimWorld;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Allows a resource to produce the harvested output of any crop a settlement could sow in a
    /// growing zone (mirroring the base game's Command_SetPlantToGrow / PlantUtility sowability test),
    /// so long as that output does not fall under one of the <see cref="blockedCategories"/> (which
    /// belong to another resource). This broadens the resource beyond a fixed item list, automatically
    /// capturing crops that yield non-food items (e.g. cotton -> cloth, devilstrand -> devilstrand cloth).
    /// <para>Only ever *adds* allowances (never blocks), so it composes with whatever the resource's
    /// own category allow lists already permit (e.g. the Foods category continues to cover food crops).</para>
    /// <para>A plant qualifies if it is field-sowable or hydroponics-sowable; trees, wild-only, and
    /// permanent-darkness plants are excluded. Hydroponic-only crops additionally require the hydroponics research
    /// (which unlocks the basin needed to grow them).</para>
    /// <para>During actual production (tithe generation, signalled by a non-null <c>resource</c>), a crop's
    /// output is only offered if the player has unlocked at least one crop that produces said output — the
    /// empire can only produce what it can sow. Trade and codex contexts pass a null
    /// resource and are left ungated (buy anything / show the full reference list).</para>
    /// </summary>
    public class ResourceFilterExtension_Crops : ResourceFilterExtension
    {
        /// <summary>
        /// Harvest outputs falling under any of these thing categories are excluded, keeping this
        /// resource mutually exclusive with whichever resource owns those categories (e.g. Medicine/Drugs).
        /// </summary>
        public List<ThingCategoryDef> blockedCategories = new List<ThingCategoryDef>();

        /// <summary>
        /// Each crop output mapped to one sow-research prerequisite set per producing plant. A null or
        /// empty set means that producer is ungated. Built once (research-independent); the cheap
        /// <see cref="ResearchProjectDef.IsFinished"/> checks happen per call.
        /// </summary>
        private Dictionary<ThingDef, List<List<ResearchProjectDef>>> _cropOutputs;
        private Dictionary<ThingDef, List<List<ResearchProjectDef>>> CropOutputs => _cropOutputs ?? (_cropOutputs = BuildCropOutputs());

        public override void SetFilter(ThingFilter filter, TechLevel techlevel, ResourceFC resource = null)
        {
            // A non-null resource signals the real production/tithe path; trade passes null.
            bool applyResearchGate = resource is object;

            foreach (KeyValuePair<ThingDef, List<List<ResearchProjectDef>>> crop in CropOutputs)
            {
                if (applyResearchGate && !AnyProducerAvailable(crop.Value)) continue;
                filter.SetAllow(crop.Key, true);
            }
        }

        public override void SetFilterForCodex(ThingFilter filter, Dictionary<ThingDef, TitheRestrictionInfo> restrictions)
        {
            foreach (KeyValuePair<ThingDef, List<List<ResearchProjectDef>>> crop in CropOutputs)
            {
                filter.SetAllow(crop.Key, true);

                // Show a research requirement only when there is no ungated way to grow this output.
                List<ResearchProjectDef> requirement = MinimalRequirement(crop.Value);
                if (requirement != null)
                {
                    TitheRestrictionInfo info = new TitheRestrictionInfo();
                    info.researchProjects = requirement;
                    restrictions[crop.Key] = info;
                }
            }
        }

        /// <summary>Returns true if at least one producing plant has all of its sow-research prerequisites finished.</summary>
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
        /// The research requirement to display for an output: null if any producer is ungated (always
        /// growable), otherwise the smallest prerequisite set among gated producers (the easiest to unlock).
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

        private Dictionary<ThingDef, List<List<ResearchProjectDef>>> BuildCropOutputs()
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

            Dictionary<ThingDef, List<List<ResearchProjectDef>>> outputs = new Dictionary<ThingDef, List<List<ResearchProjectDef>>>();
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
            {
                PlantProperties plant = def.plant;
                if (def.category != ThingCategory.Plant || plant is null) continue;

                // Replicate the base game's "can be sown in a growing zone" determination
                // (Command_SetPlantToGrow / PlantUtility.CanSowOnGrower + IsPlantAvailable), which
                // can't be called directly here because those need a concrete grower/Map and Empire
                // settlements are abstract world objects.
                bool ground = plant.sowTags != null && plant.sowTags.Contains("Ground");   // field crop
                bool hydro = plant.sowTags != null && plant.sowTags.Contains("Hydroponic"); // hydroponics crop
                if (!ground && !hydro) continue;             // not sowable in a zone or basin
                if (plant.IsTree) continue;                  // wood belongs to the logging resource
                if (plant.mustBeWildToSow) continue;         // can't be deliberately sown
                if (plant.mustBePermanentDarknessToSow) continue; // needs permanent darkness; not a field crop

                ThingDef harvest = plant.harvestedThingDef;
                if (harvest is null) continue;
                if (blocked.Contains(harvest)) continue;   // owned by another resource (e.g. Medicine/Drugs)

                // Effective sow-research prerequisites for this producer. A hydroponic-only crop also
                // requires the hydroponics research (which unlocks the basin needed to grow it).
                List<ResearchProjectDef> prereqs;
                if (ground)
                {
                    prereqs = plant.sowResearchPrerequisites;
                }
                else
                {
                    prereqs = new List<ResearchProjectDef>();
                    if (!plant.sowResearchPrerequisites.NullOrEmpty()) prereqs.AddRange(plant.sowResearchPrerequisites);
                    prereqs.Add(FCResearchDefOf.Hydroponics);
                }

                List<List<ResearchProjectDef>> producers;
                if (!outputs.TryGetValue(harvest, out producers))
                {
                    producers = new List<List<ResearchProjectDef>>();
                    outputs[harvest] = producers;
                }
                producers.Add(prereqs);
            }

            return outputs;
        }
    }
}
