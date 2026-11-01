using Verse;

namespace FactionColonies
{
    public class ProductionBonus : IExposable
    {
        public double value;
        public TaggedString desc;
        public ProductionBonus()
        {
        }

        public ProductionBonus(double value, TaggedString desc)
        {
            this.value = value;
            this.desc = desc;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref value, "value");
            Scribe_Values.Look(ref desc, "desc");
        }
    }
}