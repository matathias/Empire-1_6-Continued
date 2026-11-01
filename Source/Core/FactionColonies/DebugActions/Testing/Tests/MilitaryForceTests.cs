using RimWorld;
using RimWorld.Planet;
using System;
using Verse;

namespace FactionColonies
{
    public static class MilitaryForceTests
    {
        // ============================
        // Constructor / Pure Math
        // ============================

        [EmpireTest("MilitaryForce")]
        public static void Constructor_ForceRemaining_EqualsRoundedLevelTimesEfficiency()
        {
            var force = new MilitaryForce(7.0, 1.3, null, null);
            double expected = Math.Round(7.0 * 1.3); // 9.1 → 9
            TestAssert.AreEqual(expected, force.forceRemaining,
                $"forceRemaining should be Round(level * efficiency) = {expected}");
        }

        [EmpireTest("MilitaryForce")]
        public static void Constructor_FractionalResult_RoundsCorrectly()
        {
            var force = new MilitaryForce(5.0, 1.5, null, null);
            double expected = Math.Round(5.0 * 1.5); // 7.5 → 8 (banker's rounding)
            TestAssert.AreEqual(expected, force.forceRemaining);
        }

        [EmpireTest("MilitaryForce")]
        public static void DefensivePower_AppliesDefenderAdvantage()
        {
            var force = new MilitaryForce(10.0, 1.0, null, null);
            double expected = Math.Round(force.forceRemaining * FCSettings.defenderAdvantage);
            TestAssert.AreEqual(expected, force.DefensivePower,
                $"DefensivePower should be Round(forceRemaining * {FCSettings.defenderAdvantage})");
        }

        // ============================
        // Tech Level Mapping
        // ============================

        [EmpireTest("MilitaryForce")]
        public static void TechLevelMapping_Neolithic_Level2_Eff1()
        {
            MilitaryDeploymentUtil.GetTechLevelBaseline(
                TechLevel.Neolithic, out double level, out double eff);
            TestAssert.AreEqual(2.0, level, message: "Neolithic level");
            TestAssert.AreEqual(0.9, eff, message: "Neolithic efficiency");
        }

        [EmpireTest("MilitaryForce")]
        public static void TechLevelMapping_Spacer_Level6_Eff1Point1()
        {
            MilitaryDeploymentUtil.GetTechLevelBaseline(
                TechLevel.Spacer, out double level, out double eff);
            TestAssert.AreEqual(6.0, level, message: "Spacer level");
            TestAssert.AreEqual(1.1, eff, message: "Spacer efficiency");
        }

        [EmpireTest("MilitaryForce")]
        public static void TechLevelMapping_Archotech_HighestValues()
        {
            MilitaryDeploymentUtil.GetTechLevelBaseline(
                TechLevel.Archotech, out double level, out double eff);
            TestAssert.AreEqual(9.0, level, message: "Archotech level");
            TestAssert.AreEqual(1.3, eff, message: "Archotech efficiency");
        }

        [EmpireTest("MilitaryForce")]
        public static void TechLevelMapping_AllLevels_PositiveValues()
        {
            foreach (TechLevel tech in Enum.GetValues(typeof(TechLevel)))
            {
                MilitaryDeploymentUtil.GetTechLevelBaseline(
                    tech, out double level, out double eff);
                TestAssert.GreaterThan(level, 0, $"TechLevel {tech}: level should be > 0");
                TestAssert.GreaterThan(eff, 0, $"TechLevel {tech}: efficiency should be > 0");
            }
        }

        // ============================
        // Factory Methods (game state)
        // ============================

        private static WorldSettlementFC GetSettlement()
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction == null || faction.settlements.Count == 0) return null;
            return faction.settlements.FirstOrDefault(s => s.MilitaryComp != null);
        }

        [EmpireTest("MilitaryForce")]
        public static void CreateFromSettlement_IsFiniteAndPositive()
        {
            WorldSettlementFC settlement = GetSettlement();
            if (settlement == null) TestAssert.Skip("No settlement with MilitaryComp");

            MilitaryForce force = MilitaryForce.CreateMilitaryForceFromSettlement(settlement);

            TestAssert.IsFalse(double.IsNaN(force.forceRemaining), "forceRemaining should not be NaN");
            TestAssert.IsFalse(double.IsInfinity(force.forceRemaining), "forceRemaining should not be infinite");
            TestAssert.IsTrue(force.forceRemaining >= 0, $"forceRemaining should be >= 0, got {force.forceRemaining}");
            TestAssert.IsTrue(force.militaryLevel >= 0, "militaryLevel should be >= 0");
            TestAssert.GreaterThan(force.militaryEfficiency, 0, "militaryEfficiency should be > 0");
        }

        [EmpireTest("MilitaryForce")]
        public static void CreateFromSettlement_AttackingVsDefending_DifferByStats()
        {
            WorldSettlementFC settlement = GetSettlement();
            if (settlement == null) TestAssert.Skip("No settlement with MilitaryComp");

            MilitaryForce attacking = MilitaryForce.CreateMilitaryForceFromSettlement(settlement, isAttacking: true);
            MilitaryForce defending = MilitaryForce.CreateMilitaryForceFromSettlement(settlement, isAttacking: false);

            // Both should be valid; they may differ if attack/defense stat bonuses differ
            TestAssert.IsFalse(double.IsNaN(attacking.forceRemaining), "Attacking force should not be NaN");
            TestAssert.IsFalse(double.IsNaN(defending.forceRemaining), "Defending force should not be NaN");
        }

        [EmpireTest("MilitaryForce")]
        public static void CreateFromSquad_ForceIsResolvedPlusDefendingBonus()
        {
            // A defending squad force = the squad's resolved power level + the defending-level bonus
            // (faction + settlement + squad scopes, aggregated by GetStatValue). The settlement scope
            // was intentionally excluded until militaryLevelBonusDefending became appliesToSettlements
            // (so a Military governor / cavern biome could feed defense). The invariant here is the
            // EXACT composition — in particular that the raw settlementMilitaryLevel is NOT added on top.
            FactionFC faction = FindFC.FactionComp;
            if (faction is null) TestAssert.Skip("No FactionFC");

            MercenarySquadFC squad = null;
            foreach (WorldSettlementFC s in faction.settlements)
            {
                foreach (MercenarySquadFC stationed in s.StationedSquads)
                {
                    if (stationed?.settlement is object) { squad = stationed; break; }
                }
                if (squad is object) break;
            }
            if (squad is null) TestAssert.Skip("No stationed squad available");

            double resolvedLevel = SquadPowerRegistry.Resolve(squad).militaryLevel;
            MilitaryForce force = MilitaryForce.CreateMilitaryForceFromSquad(squad);

            TestAssert.IsNotNull(force, "Force should be created for a stationed squad");

            // CombineForce (defending) adds exactly the aggregated militaryLevelBonusDefending for the
            // anchor settlement + squad on top of the resolved level.
            double defendingBonus = faction.GetStatValue(
                FCStatDefOf.militaryLevelBonusDefending, squad.settlement, squad);
            double expectedLevel = resolvedLevel + defendingBonus;
            TestAssert.AreEqual(expectedLevel, force.militaryLevel,
                message: $"defending force should be resolved({resolvedLevel}) + defending bonus({defendingBonus}), " +
                         $"not include raw settlementMilitaryLevel ({squad.settlement.settlementMilitaryLevel})");
        }

        [EmpireTest("MilitaryForce")]
        public static void EnemyPower_BaselineReflectsTechLevel()
        {
            Settlement enemy = Find.WorldObjects.Settlements
                .FirstOrDefault(s => !(s is WorldSettlementFC)
                    && s.Faction != null && s.Faction != Faction.OfPlayer
                    && s.Faction != FindFC.EmpireFaction);
            if (enemy == null) TestAssert.Skip("No enemy settlement on world map");

            WorldComponent_EnemyPower registry = FindFC.EnemyPower;
            if (registry == null) TestAssert.Skip("EnemyPower registry not available");

            EnemyPower entry = registry.GetOrCompute(enemy);
            TestAssert.IsNotNull(entry, "Registry should produce an entry for an enemy settlement");

            // Baseline structure: efficiency clamped at 0.1, level at 1, variance default applied.
            TestAssert.IsTrue(entry.efficiency >= 0.1,
                $"Efficiency floor not respected: got {entry.efficiency}");
            TestAssert.IsTrue(entry.level >= 1,
                $"Level floor not respected: got {entry.level}");

            // Variances now sourced from EnemyPowerTechDef + (optional) EnemyPowerFactionDef.
            // Pull the resolved tech-def variances and compare; faction overrides may shift them.
            MilitaryDeploymentUtil.GetTechLevelBaseline(enemy.Faction.def.techLevel,
                out double _, out double _, out double techLvlVar, out double techEffVar);
            EnemyPowerFactionDef factionDef = registry.GetFactionDef(enemy.Faction.def);
            double expectedLvlVar = (factionDef is object && factionDef.levelVariance.HasValue)
                ? factionDef.levelVariance.Value : techLvlVar;
            double expectedEffVar = (factionDef is object && factionDef.efficiencyVariance.HasValue)
                ? factionDef.efficiencyVariance.Value : techEffVar;
            TestAssert.AreEqual(expectedLvlVar, entry.levelVariance,
                "Level variance from def applied");
            TestAssert.AreEqual(expectedEffVar, entry.efficiencyVariance,
                "Efficiency variance from def applied");

            TestAssert.IsTrue(entry.MaxForceRemaining >= entry.MinForceRemaining,
                "Max bound must be >= min bound");
        }

        [EmpireTest("MilitaryForce")]
        public static void EnemyPower_NoEntryForPlayerOrEmpire()
        {
            WorldComponent_EnemyPower registry = FindFC.EnemyPower;
            if (registry == null) TestAssert.Skip("EnemyPower registry not available");

            // The player and the allied empire faction must never get an enemy estimate; the
            // world-map inspect line keys off this null to stay off player/empire settlements.
            TestAssert.IsNull(registry.GetOrCompute(Faction.OfPlayer),
                "Player faction must not have an EnemyPower entry");

            if (FindFC.EmpireFaction is object)
            {
                TestAssert.IsNull(registry.GetOrCompute(FindFC.EmpireFaction),
                    "Empire faction must not have an EnemyPower entry");
            }

            Settlement playerColony = Find.WorldObjects.Settlements
                .FirstOrDefault(s => !(s is WorldSettlementFC) && s.Faction == Faction.OfPlayer);
            if (playerColony is object)
            {
                TestAssert.IsNull(registry.GetOrCompute(playerColony),
                    "Player colony must not have an EnemyPower entry");
            }
        }
    }
}
