using RimWorld;
using System.Collections.Generic;
using Verse;

namespace FactionColonies.util
{
    public static class CraftUtil
    {
        public static bool CanCraftItem(ThingDef thing, bool includeSingleUse = false)
        {
            bool canCraft = true;
            if (WeaponBlockedForCrafting(thing)) return false;
            if (thing.recipeMaker != null)
            {
                if (thing.recipeMaker.researchPrerequisites != null)
                {
                    foreach (ResearchProjectDef research in thing.recipeMaker.researchPrerequisites)
                    {
                        if (!(Find.ResearchManager.GetProgress(research) >= research.baseCost))
                        {
                            //research is not good
                            canCraft = false;
                        }
                    }
                }

                if (thing.recipeMaker.researchPrerequisite != null)
                {
                    if (!(Find.ResearchManager.GetProgress(thing.recipeMaker.researchPrerequisite) >=
                          thing.recipeMaker.researchPrerequisite.baseCost))
                    {
                        //research is not good
                        canCraft = false;
                    }
                }
            }
            else
            {
                if (FindFC.TechLevel < thing.techLevel)
                {
                    canCraft = false;
                }
            }

            if (thing.thingSetMakerTags != null && thing.thingSetMakerTags.Contains("SingleUseWeapon") &&
                !includeSingleUse)
            {
                canCraft = false;
            }

            // Honor research gating declared directly on the ThingDef (BuildableDef.researchPrerequisites),
            // not just on recipeMaker. Items like VFE-Pirates warcaskets are foundry-built (null recipeMaker)
            // and gate access via the def's own researchPrerequisites, so they would otherwise slip through.
            if (!thing.IsResearchFinished)
            {
                canCraft = false;
            }

            return canCraft;
        }
        public static bool WeaponBlockedForMercs(ThingDef thing)
        {
            if (!thing.IsWeapon) return false;
            if (thing.weaponTags is null) return false;
            return thing.weaponTags.Contains("FCWeaponBlocklist_Merc");
        }
        public static bool WeaponBlockedForCrafting(ThingDef thing)
        {
            if (!thing.IsWeapon) return false;
            if (thing.weaponTags is null) return false;
            return thing.weaponTags.Contains("FCWeaponBlocklist_Craft");
        }

        public static bool ThingHasQuality(ThingDef thing)
        {
            return thing.HasComp<CompQuality>();
        }
        public static bool ThingIsStuffable(ThingDef thing)
        {
            return thing.MadeFromStuff;
        }
        /// <summary>
        /// For the given <paramref name="thing"/>, returns a list of valid stuff ThingDefs.
        /// </summary>
        /// <param name="thing">ThingDef to retrieve a list of stuff for.</param>
        /// <param name="filterList">List of possible things to use for stuff.</param>
        /// <returns>The list of ThingDefs that can be used to stuff the given <paramref name="thing"/>. Returns an empty list if <paramref name="thing"/> is not stuffable.</returns>
        public static List<ThingDef> GetThingStuffs(ThingDef thing, List<ThingDef> filterList)
        {
            List<ThingDef> list = new List<ThingDef>();
            if (ThingIsStuffable(thing) && filterList.Count > 0)
            {
                foreach (ThingDef possible in filterList)
                {
                    if (possible.IsStuff && possible.stuffProps.CanMake(thing))
                    {
                        list.Add(possible);
                    }
                }
            }
            return list;
        }

        public static float ThingValue(ThingQualityTuple thing)
        {
            return ThingValue(thing.thingDef, thing.stuffDef, thing.quality);
        }

        public static float ThingValueNoQual(ThingDef thing, ThingDef stuff)
        {
            return ThingValue(thing, stuff, QualityCategory.Normal);
        }
        public static float ThingValue(ThingDef thing, ThingDef stuff, QualityCategory quality)
        {
            float value;
            if (ThingHasQuality(thing))
            {
                value = StatDefOf.MarketValue.Worker.GetValue(StatRequest.For(thing, stuff, quality));
            }
            else
            {
                if (stuff != null && ThingIsStuffable(thing))
                {
                    value = StatWorker_MarketValue.CalculatedBaseMarketValue(thing, stuff);
                }
                else
                {
                    value = thing.BaseMarketValue;
                }
            }
            // Prevent shenanigans
            if (value <= 0)
            {
                value = 10;
            }
            return value;
        }
    }
}
