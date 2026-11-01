using Verse;

namespace FactionColonies.util
{
    /// <summary>
    /// Abstraction over random number generation to allow deterministic testing.
    /// </summary>
    public interface IRandProvider
    {
        int Range(int min, int maxExclusive);
    }

    /// <summary>
    /// Default implementation that delegates to RimWorld's Rand.Range.
    /// </summary>
    public class RimWorldRandProvider : IRandProvider
    {
        public int Range(int min, int maxExclusive) => Rand.Range(min, maxExclusive);
    }
}
