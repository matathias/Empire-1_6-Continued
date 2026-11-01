using RimWorld;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    public abstract class ResourceResearchRestriction
    {
        public TechLevel minTechLevel = TechLevel.Undefined;
        public TechLevel maxTechLevel = TechLevel.Undefined;
        public List<ResearchProjectDef> researchProjectDefs = new List<ResearchProjectDef>();
        public bool restrictByRecipe = true;
        public bool restrictByThingTechLevel = true;

        public bool hasResearchDefs => (researchProjectDefs.Count > 0);
        public bool hasDefinedMinTechLevel => (minTechLevel != TechLevel.Undefined);
        public bool hasDefinedMaxTechLevel => (maxTechLevel != TechLevel.Undefined);
        public bool hasDefinedTechLevel => hasDefinedMinTechLevel || hasDefinedMaxTechLevel;
        public virtual bool noRequirements => (!hasResearchDefs && !hasDefinedTechLevel && !restrictByRecipe && !restrictByThingTechLevel);
        public virtual bool SatisfiesTechRequirements(TechLevel techlevel)
        {
            if (noRequirements)
                return true;

            if (hasDefinedTechLevel)
            {
                bool satisfiesMinLevel = !hasDefinedMinTechLevel || techlevel >= minTechLevel;
                bool satisfiesMaxLevel = !hasDefinedMaxTechLevel || techlevel <= maxTechLevel;
                bool satisfiesDefTechLevel = satisfiesMinLevel && satisfiesMaxLevel;
                if (!satisfiesDefTechLevel)
                {
                    return false;
                }
            }

            if (hasResearchDefs)
            {
                foreach (ResearchProjectDef projectDef in researchProjectDefs)
                {
                    if (!projectDef.IsFinished)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        public virtual void SetFilter(ThingFilter filter, TechLevel techlevel)
        {
        }
        // Used to be part of CraftUtil.CanCraftItem. Now, we do this check as part of the overall filtering process
        public static bool ThingAllowedByRecipe(ThingDef thing)
        {
            if (thing.recipeMaker != null)
            {
                if (thing.recipeMaker.researchPrerequisites != null)
                {
                    foreach (ResearchProjectDef research in thing.recipeMaker.researchPrerequisites)
                    {
                        if (!research.IsFinished)
                        {
                            //research is not good
                            return false;
                        }
                    }
                }

                if (thing.recipeMaker.researchPrerequisite != null)
                {
                    if (!thing.recipeMaker.researchPrerequisite.IsFinished)
                    {
                        //research is not good
                        return false;
                    }
                }
            }
            return true;
        }
        // Used to be part of CraftUtil.CanCraftItem. Now, we do this check as part of the overall filtering process
        public static bool ThingAllowedByThingTechLevel(ThingDef thing, TechLevel techlevel)
        {
            if (techlevel < thing.techLevel)
            {
                return false;
            }
            return true;
        }
    }
    public class ResourceThingDefRestriction : ResourceResearchRestriction
    {
        public ThingDef thingDef;

        public override bool SatisfiesTechRequirements(TechLevel techlevel)
        {
            if (noRequirements)
                return true;

            if (!base.SatisfiesTechRequirements(techlevel))
            {
                return false;
            }

            if (restrictByRecipe)
            {
                if (!ThingAllowedByRecipe(thingDef))
                {
                    return false;
                }
            }

            if (restrictByThingTechLevel)
            {
                if (!ThingAllowedByThingTechLevel(thingDef, techlevel))
                {
                    return false;
                }
            }

            return true;
        }
        public override void SetFilter(ThingFilter filter, TechLevel techlevel)
        {
            if (SatisfiesTechRequirements(techlevel))
            {
                filter.SetAllow(thingDef, true);
            }
            else
            {
                /* Thing specifications override Category specifications. So if a category already allowed the thing,
                 * but the Thing's own requirements aren't satisfied, then we need to disallow it. */
                filter.SetAllow(thingDef, false);
            }
        }
    }
    public class ResourceThingCategoryDefRestriction : ResourceResearchRestriction
    {
        public ThingCategoryDef thingCategoryDef;

        /* We don't override SatisfiesTechRequirements here because the base class's checks are enough for the ThingCategoryDef.
         * All further checks are on the things listed within the category. That logic has to be handled in SetFiler. */
        public override void SetFilter(ThingFilter filter, TechLevel techlevel)
        {
            if (noRequirements)
            {
                filter.SetAllow(thingCategoryDef, true);
                return;
            }

            if (SatisfiesTechRequirements(techlevel))
            {
                filter.SetAllow(thingCategoryDef, true);
            }
            else
            {
                filter.SetAllow(thingCategoryDef, false);
                return;
            }

            foreach (ThingDef thingDef in thingCategoryDef.DescendantThingDefs)
            {
                bool allowed = true;
                if (restrictByRecipe)
                {
                    allowed &= ThingAllowedByRecipe(thingDef);
                }
                if (restrictByThingTechLevel)
                {
                    allowed &= ThingAllowedByThingTechLevel(thingDef, techlevel);
                }
                filter.SetAllow(thingDef, allowed);
            }
        }
    }
}