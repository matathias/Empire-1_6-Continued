using Verse;

namespace FactionColonies
{
    /// <summary>
    /// One entry in a resource's ordered tithe priority list (index 0 = highest priority).
    /// Replaces the old unordered Dictionary&lt;ThingQualityTuple,int&gt;. No cap; never auto-pruned.
    /// </summary>
    public class TitheEntry : IExposable
    {
        public ThingQualityTuple thing;
        public int quantity;

        public TitheEntry() { }

        public TitheEntry(ThingQualityTuple thing, int quantity)
        {
            this.thing = thing;
            this.quantity = quantity;
        }

        public void ExposeData()
        {
            Scribe_Deep.Look(ref thing, "thing");
            Scribe_Values.Look(ref quantity, "quantity", 0);
        }
    }
}
