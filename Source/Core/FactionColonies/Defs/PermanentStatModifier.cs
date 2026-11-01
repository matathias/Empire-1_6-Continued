using Verse;

namespace FactionColonies
{
    /// <summary>
    /// A stat modifier that persists permanently on a settlement, surviving event expiry and save/load.
    /// Applied by events with <see cref="FCEventDef.permanentStatModifiers"/> and serialized
    /// directly on <see cref="WorldSettlementFC"/>.
    /// </summary>
    public class PermanentStatModifier : IExposable
    {
        public FCStatDef stat;
        public double value;
        public string sourceId;
        public string sourceLabel;

        /// <summary>Deep copy — used when seeding design modifiers onto squad/unit instances.</summary>
        public PermanentStatModifier Clone() =>
            new PermanentStatModifier { stat = stat, value = value, sourceId = sourceId, sourceLabel = sourceLabel };

        public void ExposeData()
        {
            Scribe_Defs.Look(ref stat, "stat");
            Scribe_Values.Look(ref value, "value");
            Scribe_Values.Look(ref sourceId, "sourceId");
            Scribe_Values.Look(ref sourceLabel, "sourceLabel");
        }
    }
}
