using RimWorld;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Used to define filters for ResourceTypes that require more in-depth logic than Thing or ThingCategory allow/blocklists can provide.
    /// <para>NOTE: ResourceFilterExtension run *after* the thing and thingCategory allow/block lists have been applied.</para>
    /// </summary>
    public abstract class ResourceFilterExtension : DefModExtension
    {
        /// <summary>
        /// Sets the given filter for the given tech level.
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="techlevel"></param>
        public virtual void SetFilter(ThingFilter filter, TechLevel techlevel, ResourceFC resource = null)
        {
        }
        /// <summary>
        /// Codex variant of <see cref="SetFilter"/>: allow every item this extension can contribute,
        /// ignoring research/tech gates, and record any per-item tech/research requirements into
        /// <paramref name="restrictions"/> so the codex can display them.
        /// <para>Defaults to <see cref="SetFilter"/> at max tech level with no restriction info, which
        /// is correct for extensions that don't gate their items.</para>
        /// </summary>
        public virtual void SetFilterForCodex(ThingFilter filter, Dictionary<ThingDef, TitheRestrictionInfo> restrictions)
        {
            SetFilter(filter, TechLevel.Archotech, null);
        }
        /// <summary>
        /// Retrieves the ThingSetMaker associated with this resource.
        /// <para>Resources use ThingSetMaker_MarketValue by default. This function only needs to be specified if you want to use a different ThingSetMaker.</para>
        /// </summary>
        /// <param name="tlevel">Output parameter. This techlevel will be used if GetThingSetMaker returns non-null.</param>
        /// <returns></returns>
        public virtual ThingSetMaker GetThingSetMaker(out TechLevel tlevel, ResourceFC resource = null)
        {
            tlevel = TechLevel.Undefined;
            return null;
        }
        /// <summary>
        /// Returns a list of generated things that satisfy the given <paramref name="thingDef"/>, <paramref name="quality"/>, and <paramref name="stuffDef"/>.
        /// 
        /// <para>quality can be ignored if the thingDef does not have a quality comp, and stuffDef can be ignored if the thingDef is not stuffable.</para>
        /// </summary>
        /// <param name="thingDef">ThingDef of the thing(s) to generate.</param>
        /// <param name="quality">QualityCategory of the thing(s) to generate. Can be ignored if the thingDef does not have a quality comp.</param>
        /// <param name="stuffDef">ThingDef of the stuff for this thing. Can be ignored if the thingDef is not stuffable.</param>
        /// <param name="quantity">The number of things to generate.</param>
        /// <returns></returns>
        public virtual List<Thing> GenerateSpecificThings(ThingDef thingDef, int quantity, QualityCategory quality = QualityCategory.Normal, ThingDef stuffDef = null, ResourceFC resource = null)
        {
            return null;
        }
    }
}
