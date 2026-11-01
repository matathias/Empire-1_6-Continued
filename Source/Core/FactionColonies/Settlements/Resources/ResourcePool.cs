using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    public class ResourcePool : IExposable
    {
        public ResourceTypeDef resource;
        public double pool;
        public ResourcePool()
        {
        }
        public IEnumerable<FloatMenuOption> GetFactionMenuFloatMenuOptions()
        {
            return resource.GetFactionMenuFloatMenuOptions(this);
        }
        public void ExposeData()
        {
            Scribe_Defs.Look(ref resource, "resource");
            Scribe_Values.Look(ref pool, "pointpool");
        }
    }
}