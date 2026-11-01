using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Defines how to generate reward items for events. Each def configures ThingSetMakerParams
    /// for a specific category of reward (weapons, food, animals, etc.).
    /// <para>These are referenced by FCEventDef.randomThingRewardDef.</para>
    /// </summary>
    public class ResourceEventRewardDef : Def
    {
        /// <summary>Thing categories to allow in the filter.</summary>
        public List<ThingCategoryDef> thingCategoryAllowList = new List<ThingCategoryDef>();
        /// <summary>Individual things to allow in the filter.</summary>
        public List<ThingDef> thingAllowList = new List<ThingDef>();
        /// <summary>Individual things to block from the filter.</summary>
        public List<ThingDef> thingBlockList = new List<ThingDef>();
        /// <summary>Stuff categories to allow in the filter.</summary>
        public List<StuffCategoryDef> stuffCategoryAllowList = new List<StuffCategoryDef>();
        /// <summary>Stuff categories to block from the filter.</summary>
        public List<StuffCategoryDef> stuffCategoryBlockList = new List<StuffCategoryDef>();

        /// <summary>
        /// If true, value range is calculated as (valueBase * valueMinFactor, valueBase * valueMaxFactor).
        /// If false, value range is calculated as (valueBase + valueMinFlatOffset, valueBase + valueMaxFlatOffset).
        /// </summary>
        public bool useValueFactors = false;
        /// <summary>Flat offset added to valueBase for the minimum market value range. Used when useValueFactors is false.</summary>
        public float valueMinFlatOffset = -300f;
        /// <summary>Flat offset added to valueBase for the maximum market value range. Used when useValueFactors is false.</summary>
        public float valueMaxFlatOffset = 300f;
        /// <summary>Factor multiplied by valueBase for the minimum market value range. Used when useValueFactors is true.</summary>
        public float valueMinFactor = 0.5f;
        /// <summary>Factor multiplied by valueBase for the maximum market value range. Used when useValueFactors is true.</summary>
        public float valueMaxFactor = 2.0f;

        /// <summary>Count range for generated items.</summary>
        public IntRange countRange = new IntRange(1, 1);
        /// <summary>If true, a count range is applied to the ThingSetMakerParams. Some ThingSetMakers (like ThingSetMaker_Animals) don't use count ranges.</summary>
        public bool useCountRange = true;

        /// <summary>If true, applies the qualityGenerator to generated items.</summary>
        public bool useQualityGenerator = false;
        /// <summary>The quality generator to use when useQualityGenerator is true.</summary>
        public QualityGenerator qualityGenerator = QualityGenerator.Gift;

        /// <summary>
        /// Custom ThingSetMaker class to use. If null, ThingSetMaker_MarketValue is used.
        /// Must be a subclass of ThingSetMaker.
        /// </summary>
        public Type thingSetMakerClass;

        /// <summary>
        /// If true, overrides the tech level parameter with techLevel instead of using the player faction's tech level.
        /// </summary>
        public bool overrideTechLevel = false;
        /// <summary>Tech level override value. Only used when overrideTechLevel is true.</summary>
        public TechLevel techLevel = TechLevel.Undefined;

        /// <summary>
        /// Builds a ThingSetMaker and ThingSetMakerParams from this def's configuration and the given value base.
        /// </summary>
        /// <param name="valueBase">The base market value for reward generation.</param>
        /// <param name="thingSetMaker">The ThingSetMaker to use (output).</param>
        /// <returns>Configured ThingSetMakerParams.</returns>
        public ThingSetMakerParams BuildParams(double valueBase, out ThingSetMaker thingSetMaker)
        {
            if (thingSetMakerClass != null)
            {
                thingSetMaker = (ThingSetMaker)Activator.CreateInstance(thingSetMakerClass);
            }
            else
            {
                thingSetMaker = new ThingSetMaker_MarketValue();
            }

            ThingSetMakerParams param = new ThingSetMakerParams();

            // Value range
            if (useValueFactors)
            {
                param.totalMarketValueRange = new FloatRange(
                    (float)(valueBase * valueMinFactor),
                    (float)(valueBase * valueMaxFactor));
            }
            else
            {
                param.totalMarketValueRange = new FloatRange(
                    (float)(valueBase + valueMinFlatOffset),
                    (float)(valueBase + valueMaxFlatOffset));
            }

            // Tech level (assigned before the filter so ResourceFilterExtensions see the real level)
            if (overrideTechLevel)
            {
                param.techLevel = techLevel;
            }
            else
            {
                param.techLevel = FindFC.EmpireFaction.def.techLevel;
            }

            // Filter
            param.filter = new ThingFilter();
            foreach (ThingCategoryDef cat in thingCategoryAllowList)
            {
                param.filter.SetAllow(cat, true);
            }
            foreach (ThingDef thing in thingAllowList)
            {
                param.filter.SetAllow(thing, true);
            }
            foreach (StuffCategoryDef stuff in stuffCategoryAllowList)
            {
                param.filter.SetAllow(stuff, true);
            }
            foreach (ThingDef thing in thingBlockList)
            {
                param.filter.SetAllow(thing, false);
            }
            foreach (StuffCategoryDef stuff in stuffCategoryBlockList)
            {
                param.filter.SetAllow(stuff, false);
            }

            if (modExtensions != null)
            {
                foreach (ResourceFilterExtension ext in modExtensions.OfType<ResourceFilterExtension>())
                {
                    ext.SetFilter(param.filter, param.techLevel ?? TechLevel.Undefined);
                }
            }

            // Count range
            if (useCountRange)
            {
                param.countRange = countRange;
            }

            // Quality
            if (useQualityGenerator)
            {
                param.qualityGenerator = qualityGenerator;
            }

            return param;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string item in base.ConfigErrors())
            {
                yield return item;
            }
            if (thingSetMakerClass != null && !typeof(ThingSetMaker).IsAssignableFrom(thingSetMakerClass))
            {
                yield return $"thingSetMakerClass {thingSetMakerClass.Name} is not a subclass of ThingSetMaker for ResourceEventRewardDef {defName}";
            }
            if (thingCategoryAllowList.Count == 0 && thingAllowList.Count == 0 && stuffCategoryAllowList.Count == 0 && thingSetMakerClass == null)
            {
                yield return $"ResourceEventRewardDef {defName} has no allow lists and no custom thingSetMakerClass";
            }
        }
    }
}
