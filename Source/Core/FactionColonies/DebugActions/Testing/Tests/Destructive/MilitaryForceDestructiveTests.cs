namespace FactionColonies;

/* DESTRUCTIVE: may create a real player settlement, hire and station a real mercenary squad
   (spending silver), or found a hostile settlement, so each force/power computation has live
   inputs. Nothing is reverted. */
public static class MilitaryForceDestructiveTests
{
    /// <summary>Returns a squad billeted at one of the faction's settlements, hiring and stationing
    /// one at a settlement when none is.</summary>
    private static MercenarySquadFC EnsureStationedSquad(FactionFC faction)
    {
        foreach (WorldSettlementFC s in faction.settlements)
        {
            MercenarySquadFC stationed = s.StationedSquads.FirstOrDefault(q => q?.settlement is not null);
            if (stationed is not null) return stationed;
        }

        WorldSettlementFC home = DestructiveTestUtil.EnsureSettlement();
        MercenarySquadFC squad = DestructiveTestUtil.EnsureHiredSquad();
        TestAssert.IsTrue(FindFC.Military.Assign(squad, home, true), $"Could not station a squad at {home.Name}");
        return squad;
    }

    [EmpireDestructiveTest("Destructive.MilitaryForce")]
    public static void CreateFromSettlement_IsFiniteAndPositive()
    {
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement(s => s.MilitaryComp is not null);

        MilitaryForce force = MilitaryForce.CreateMilitaryForceFromSettlement(settlement);

        TestAssert.IsFalse(double.IsNaN(force.forceRemaining), "forceRemaining should not be NaN");
        TestAssert.IsFalse(double.IsInfinity(force.forceRemaining), "forceRemaining should not be infinite");
        TestAssert.IsTrue(force.forceRemaining >= 0, $"forceRemaining should be >= 0, got {force.forceRemaining}");
        TestAssert.IsTrue(force.militaryLevel >= 0, "militaryLevel should be >= 0");
        TestAssert.GreaterThan(force.militaryEfficiency, 0, "militaryEfficiency should be > 0");
    }

    [EmpireDestructiveTest("Destructive.MilitaryForce")]
    public static void CreateFromSettlement_AttackingVsDefending_DifferByStats()
    {
        WorldSettlementFC settlement = DestructiveTestUtil.EnsureSettlement(s => s.MilitaryComp is not null);

        MilitaryForce attacking = MilitaryForce.CreateMilitaryForceFromSettlement(settlement, isAttacking: true);
        MilitaryForce defending = MilitaryForce.CreateMilitaryForceFromSettlement(settlement, isAttacking: false);

        // Both should be valid; they may differ if attack/defense stat bonuses differ
        TestAssert.IsFalse(double.IsNaN(attacking.forceRemaining), "Attacking force should not be NaN");
        TestAssert.IsFalse(double.IsNaN(defending.forceRemaining), "Defending force should not be NaN");
    }

    [EmpireDestructiveTest("Destructive.MilitaryForce")]
    public static void CreateFromSquad_ForceIsResolvedPlusDefendingBonus()
    {
        // A defending squad force = the squad's resolved power level + the defending-level bonus
        // (faction + settlement + squad scopes, aggregated by GetStatValue). The settlement scope
        // was intentionally excluded until militaryLevelBonusDefending became appliesToSettlements
        // (so a Military governor / cavern biome could feed defense). The invariant here is the
        // EXACT composition — in particular that the raw settlementMilitaryLevel is NOT added on top.
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        MercenarySquadFC squad = EnsureStationedSquad(faction);

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

    [EmpireDestructiveTest("Destructive.MilitaryForce")]
    public static void EnemyPower_BaselineReflectsTechLevel()
    {
        Settlement enemy = DestructiveTestUtil.EnsureHostileSettlement();
        WorldComponent_EnemyPower registry = TestAssert.NotNull(FindFC.EnemyPower, "No EnemyPower world component in a loaded game");

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
        double expectedLvlVar = (factionDef is not null && factionDef.levelVariance.HasValue)
            ? factionDef.levelVariance.Value : techLvlVar;
        double expectedEffVar = (factionDef is not null && factionDef.efficiencyVariance.HasValue)
            ? factionDef.efficiencyVariance.Value : techEffVar;
        TestAssert.AreEqual(expectedLvlVar, entry.levelVariance,
            "Level variance from def applied");
        TestAssert.AreEqual(expectedEffVar, entry.efficiencyVariance,
            "Efficiency variance from def applied");

        TestAssert.IsTrue(entry.MaxForceRemaining >= entry.MinForceRemaining,
            "Max bound must be >= min bound");
    }
}
