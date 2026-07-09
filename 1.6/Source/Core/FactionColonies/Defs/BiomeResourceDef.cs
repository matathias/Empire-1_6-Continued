using RimWorld;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    public class BiomeResourceDef : Def
    {
        /* The SettlementDef should determine what resources are available to the settlement, not the biome.
         * Biomes should support all resources by default.
         * If a resource isn't specified in the BiomeResourceDef, then treat it as a default 1 additive, 1 multiplier resource. */
        public List<ResourceAvailability> resources = new List<ResourceAvailability>();
        public bool canSettle;
        public List<ResourceTypeDef> resourceBlockList = new List<ResourceTypeDef>();
        public List<FCStatModifier> statModifiers = new List<FCStatModifier>();

        /// <summary>
        /// Translation key for the biome's settlement description (e.g. "FCDescBorealForest").
        /// If null or empty, falls back to "FCDescUnknown".
        /// </summary>
        public string descriptionKey;

        public ResourceAvailability GetBiomeResource(ResourceTypeDef resourceTypeDef)
        {
            /* First check if the resource is even allowed in this biome */
            if (resourceBlockList.Contains(resourceTypeDef))
            {
                return null;
            }
            if (!resourceTypeDef.ResourceAllowedForBiome(this))
            {
                return null;
            }
            ResourceAvailability res = resources.Find((ResourceAvailability rb) => rb.resourceDef == resourceTypeDef);
            if (res == null)
            {
                /* If the resource isn't explicitly mentioned in the BiomeResourceDef, and it isn't on the blocklist (and this biome
                 * isn't on that resource's biome blocklist -- handled by the resourceAllowedForBiome check), then we'll add the resource
                 * to this biome with default production values. */
                /* Meant to let people add new resources without having to include that resource in *every* BiomeResourceDef */
                res = new ResourceAvailability
                {
                    resourceDef = resourceTypeDef,
                    additive = resourceTypeDef.defaultBiomeAdditive,
                    multiplier = resourceTypeDef.defaultBiomeMultiplier
                };
                resources.Add(res);
            }
            return res;
        }
        public override void ResolveReferences()
        {
            base.ResolveReferences();
            foreach (ResourceAvailability rb in resources)
            {
                if (rb.resourceDef is null) continue;
                if (double.IsNaN(rb.additive))
                {
                    rb.additive = rb.resourceDef.defaultBiomeAdditive;
                }
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string item in base.ConfigErrors())
            {
                yield return item;
            }
            if (label.NullOrEmpty())
            {
                yield return "BiomeResourceDef " + this.defName + " has a null or empty label";
            }
            for (int i = 0; i < resources.Count; i++)
            {
                if (resources[i].resourceDef is null)
                    yield return "BiomeResourceDef " + this.defName + " has a resources entry with a null resourceDef (unresolved defName?)";
            }
        }
    }
}
