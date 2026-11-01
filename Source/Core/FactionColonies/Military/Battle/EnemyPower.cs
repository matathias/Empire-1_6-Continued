using RimWorld;
using System;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Cached, deterministic baseline of an enemy faction's or settlement's military power.
    /// Stored in <see cref="WorldComponent_EnemyPower"/> and refreshed periodically so that
    /// registered power modifiers (faction-level / settlement-level) propagate over time.
    ///
    /// <para>The squad-attack picker reads <see cref="MinForceRemaining"/>/<see cref="MaxForceRemaining"/>
    /// to display a stable range. Battle resolution calls <see cref="SampleBattleForce"/>,
    /// which performs a single RNG roll within the variance bounds — the actual force the
    /// player fights is guaranteed to fall inside the displayed range.</para>
    /// </summary>
    public class EnemyPower : IExposable
    {
        public double level;
        public double efficiency;
        public double levelVariance;
        public double efficiencyVariance;
        public int lastComputedTick = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref level, "level");
            Scribe_Values.Look(ref efficiency, "efficiency");
            Scribe_Values.Look(ref levelVariance, "levelVariance");
            Scribe_Values.Look(ref efficiencyVariance, "efficiencyVariance");
            Scribe_Values.Look(ref lastComputedTick, "lastComputedTick", -1);
        }

        public double MinLevel => Math.Max(1, level - levelVariance);
        public double MaxLevel => level + levelVariance;
        public double MinEfficiency => Math.Max(0.1, efficiency - efficiencyVariance);
        public double MaxEfficiency => efficiency + efficiencyVariance;

        public double MinForceRemaining => Math.Max(1, Math.Round(MinLevel * MinEfficiency));
        public double MaxForceRemaining => Math.Round(MaxLevel * MaxEfficiency);

        /// <summary>
        /// Returns the force at the lower or upper bound of the variance range. Used by the
        /// squad-attack window to display "estimated defender power: min - max" without
        /// advancing the global RNG.
        /// </summary>
        public MilitaryForce BuildBoundForce(Faction faction, bool max)
        {
            double l = max ? MaxLevel : MinLevel;
            double e = max ? MaxEfficiency : MinEfficiency;
            return new MilitaryForce(l, e, null, faction);
        }

        /// <summary>
        /// Single RNG roll within the variance bounds. Called at battle engagement to produce
        /// the force the simulation actually fights. Pass <paramref name="handicap"/> = true
        /// for AI-attacker contexts so the roll is capped by the early-game time grace
        /// (<see cref="ThreatScalingUtil.ComputeEarlyGameRaidCap"/>).
        /// </summary>
        public MilitaryForce SampleBattleForce(Faction faction, bool handicap = false)
        {
            double rolledLevel = Math.Max(1, level + MilitaryDeploymentUtil.RollVarianceOffset(levelVariance));
            double rolledEfficiency = Math.Max(0.1, efficiency + MilitaryDeploymentUtil.RollVarianceOffset(efficiencyVariance));
            if (handicap)
            {
                rolledLevel = Math.Min(rolledLevel,
                    ThreatScalingUtil.ComputeEarlyGameRaidCap(FindFC.FactionComp));
            }
            return new MilitaryForce(rolledLevel, rolledEfficiency, null, faction);
        }
    }
}
