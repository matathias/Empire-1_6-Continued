namespace FactionColonies
{
    /// <summary>
    /// Defines resource availability and base production bonuses for biomes and settlement types.
    /// Unlike FCStatModifier, this determines WHETHER a resource exists at a location, not just its bonus.
    /// </summary>
    public class ResourceAvailability
    {
        public ResourceTypeDef resourceDef;
        public double additive = double.NaN;
        public double multiplier = 1;
    }
}