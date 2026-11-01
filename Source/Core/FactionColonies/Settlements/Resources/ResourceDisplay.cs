using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// A small class meant for use with FactionFC to display faction-level resource production totals.
    /// </summary>
    public class ResourceDisplay : IExposable
    {
        public ResourceTypeDef resourceDef;
        private double cachedAmount = 0;
        private bool dirtyCachedAmount = true;
        public double amount
        {
            get
            {
                if (dirtyCachedAmount)
                {
                    FactionFC factionFC = FindFC.FactionComp;
                    double resource = 0;
                    for (int k = 0; k < factionFC.settlements.Count; k++)
                    {
                        resource += factionFC.settlements[k].GetResource(resourceDef)?.rawTotalProduction ?? 0;
                    }

                    cachedAmount = resource;
                    dirtyCachedAmount = false;
                }
                return cachedAmount;
            }
        }

        public Texture2D Icon => resourceDef?.Icon ?? TexLoad.questionmark;
        public string label => resourceDef?.LabelCap ?? "";
        public ResourceDisplay()
        {
        }
        public ResourceDisplay(ResourceTypeDef def)
        {
            resourceDef = def;
        }
        public void ExposeData()
        {
            Scribe_Defs.Look(ref resourceDef, "resourcedef");
        }
        public void SetDirtyCache()
        {
            dirtyCachedAmount = true;
        }
        public int CompareForUI(ResourceDisplay compareDef)
        {
            if (compareDef == null)
            {
                return -2;
            }
            if (compareDef.resourceDef == null)
            {
                return -1;
            }
            if (this.resourceDef == null)
            {
                return 1;
            }
            return ResourceTypeDef.SortForUI(this.resourceDef, compareDef.resourceDef);
        }
        public static int SortForUI(ResourceDisplay a, ResourceDisplay b)
        {
            return a.CompareForUI(b);
        }
    }
}