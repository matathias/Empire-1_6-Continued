using Verse;

namespace FactionColonies
{
    public class TaxBreakData : IExposable
    {
        public int startTick;
        public bool enabled;

        public void ExposeData()
        {
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref enabled, "enabled");
        }
    }
}
