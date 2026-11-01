using System;
using System.Collections.Generic;
using Verse;
using FactionColonies.util;

namespace FactionColonies
{
    /// <summary>The four first-class social stats a situation condition can read.</summary>
    public enum SocialStatType
    {
        Loyalty,
        Unrest,
        Happiness,
        Prosperity
    }

    public enum FCThresholdComparison
    {
        Below,
        Above
    }

    /// <summary>A single "{stat} below/above {threshold}" test over one social stat.</summary>
    public class FCSocialClause
    {
        public SocialStatType stat = SocialStatType.Loyalty;
        public FCThresholdComparison comparison = FCThresholdComparison.Above;
        public float threshold = 0f;

        public bool Satisfied(double value)
        {
            return comparison == FCThresholdComparison.Below ? value < threshold : value > threshold;
        }
    }

    /// <summary>A small purpose-built combinator over <see cref="FCSocialClause"/>s, combined All/Any.</summary>
    public class FCSocialConditionSet
    {
        public FCRequirementMode mode = FCRequirementMode.All;
        public List<FCSocialClause> clauses = new List<FCSocialClause>();

        public bool Satisfied(Func<SocialStatType, double> read)
        {
            if (clauses == null || clauses.Count == 0) return true;
            if (mode == FCRequirementMode.Any)
            {
                foreach (FCSocialClause c in clauses)
                    if (c != null && c.Satisfied(read(c.stat))) return true;
                return false;
            }
            foreach (FCSocialClause c in clauses)
                if (c != null && !c.Satisfied(read(c.stat))) return false;
            return true;
        }
    }

    /// <summary>
    /// Built-in faction-scoped condition over the four faction-average social stats. Configure
    /// <c>spawnWhen</c> (drives ShouldSpawn) and optionally <c>advanceWhen</c> (drives ShouldAdvance;
    /// defaults to spawnWhen when omitted).
    /// </summary>
    public class FCSituationSocialFactionCondition : FCSituationFactionCondition
    {
        public FCSocialConditionSet spawnWhen = new FCSocialConditionSet();
        public FCSocialConditionSet advanceWhen;

        private static double Read(FactionFC faction, SocialStatType stat)
        {
            switch (stat)
            {
                case SocialStatType.Loyalty: return faction.averageLoyalty;
                case SocialStatType.Unrest: return faction.averageUnrest;
                case SocialStatType.Happiness: return faction.averageHappiness;
                case SocialStatType.Prosperity: return faction.averageProsperity;
                default: return 0d;
            }
        }

        public override bool ShouldSpawn(FactionFC faction) =>
            spawnWhen != null && spawnWhen.Satisfied(s => Read(faction, s));

        public override bool ShouldAdvance(FactionFC faction) =>
            (advanceWhen ?? spawnWhen).Satisfied(s => Read(faction, s));
    }

    /// <summary>
    /// Built-in settlement-scoped condition over a single settlement's four social stats. See
    /// <see cref="FCSituationSocialFactionCondition"/> for the spawnWhen / advanceWhen split.
    /// </summary>
    public class FCSituationSocialSettlementCondition : FCSituationSettlementCondition
    {
        public FCSocialConditionSet spawnWhen = new FCSocialConditionSet();
        public FCSocialConditionSet advanceWhen;

        private static double Read(WorldSettlementFC settlement, SocialStatType stat)
        {
            switch (stat)
            {
                case SocialStatType.Loyalty: return settlement.loyalty;
                case SocialStatType.Unrest: return settlement.unrest;
                case SocialStatType.Happiness: return settlement.happiness;
                case SocialStatType.Prosperity: return settlement.prosperity;
                default: return 0d;
            }
        }

        public override bool ShouldSpawn(FactionFC faction, WorldSettlementFC settlement) =>
            settlement != null && spawnWhen != null && spawnWhen.Satisfied(s => Read(settlement, s));

        public override bool ShouldAdvance(FactionFC faction, WorldSettlementFC settlement) =>
            settlement != null && (advanceWhen ?? spawnWhen).Satisfied(s => Read(settlement, s));
    }
}
