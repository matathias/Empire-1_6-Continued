using RimWorld;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Per-TechLevel default enemy power baseline. One def per <see cref="TechLevel"/> value
    /// supplies the level/efficiency/variance values that <see cref="WorldComponent_EnemyPower"/>
    /// uses as the starting baseline for every faction at that tech level.
    /// </summary>
    public class EnemyPowerTechDef : Def
    {
        public TechLevel techLevel = TechLevel.Undefined;

        public double level = 1.0;
        public double efficiency = 1.0;
        public double levelVariance = 2.0;
        public double efficiencyVariance = 0.0;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string err in base.ConfigErrors())
                yield return err;

            if (level <= 0)
                yield return defName + ": level must be > 0";
            if (efficiency <= 0)
                yield return defName + ": efficiency must be > 0";
            if (levelVariance < 0)
                yield return defName + ": levelVariance must be >= 0";
            if (efficiencyVariance < 0)
                yield return defName + ": efficiencyVariance must be >= 0";
        }
    }
}
