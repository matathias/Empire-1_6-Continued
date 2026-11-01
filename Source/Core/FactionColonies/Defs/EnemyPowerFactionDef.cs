using RimWorld;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Per-FactionDef override of enemy power baseline. Each numeric field is optional: when
    /// set, it replaces the corresponding <see cref="EnemyPowerTechDef"/> value for factions of
    /// the matching <see cref="factionDef"/>. Missing fields fall through to the tech def.
    /// </summary>
    public class EnemyPowerFactionDef : Def
    {
        public FactionDef factionDef;

        public double? level;
        public double? efficiency;
        public double? levelVariance;
        public double? efficiencyVariance;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string err in base.ConfigErrors())
                yield return err;

            if (factionDef is null)
                yield return defName + ": factionDef is null (required key)";
            if (level.HasValue && level.Value <= 0)
                yield return defName + ": level must be > 0 when set";
            if (efficiency.HasValue && efficiency.Value <= 0)
                yield return defName + ": efficiency must be > 0 when set";
            if (levelVariance.HasValue && levelVariance.Value < 0)
                yield return defName + ": levelVariance must be >= 0 when set";
            if (efficiencyVariance.HasValue && efficiencyVariance.Value < 0)
                yield return defName + ": efficiencyVariance must be >= 0 when set";
        }
    }
}
