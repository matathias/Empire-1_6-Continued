namespace FactionColonies;

/* DESTRUCTIVE: faction-wide profit and production sanity. Creates a transient settlement when the
   player has none (an empty faction would pass vacuously). Not reverted. */
public static class FactionDestructiveTests
{
    [EmpireDestructiveTest("Destructive.Faction")]
    public static void Faction_TotalProfit_MatchesSettlementSum()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        DestructiveTestUtil.EnsureSettlement();

        double sumIncome = faction.settlements.Sum(s => s.GetTotalIncome());
        double sumUpkeep = faction.settlements.Sum(s => s.GetTotalUpkeep());
        int edictUpkeep = FindFC.PolicyManager.GetEdictUpkeep();
        double expectedProfit = sumIncome - sumUpkeep - edictUpkeep;

        TestAssert.AreEqual(expectedProfit, faction.profit, tolerance: 1.0,
            message: $"Faction profit ({faction.profit}) should match sum of settlement profits minus edict upkeep ({expectedProfit})");
        DestructiveTestUtil.AssertEmpireInvariants(faction, "Faction_TotalProfit_MatchesSettlementSum");
    }

    [EmpireDestructiveTest("Destructive.Faction")]
    public static void Faction_SettlementTitheIncome_IsNonNegative()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        DestructiveTestUtil.EnsureSettlement();

        foreach (var settlement in faction.settlements)
        {
            foreach (var resource in settlement.Resources)
            {
                double tithe = resource.GetTitheIncome();
                TestAssert.IsTrue(tithe >= 0,
                    $"Tithe income for {resource.def?.defName ?? "null"} in {settlement.Name} should be >= 0, got {tithe}");
            }
        }
        DestructiveTestUtil.AssertEmpireInvariants(faction, "Faction_SettlementTitheIncome_IsNonNegative");
    }

    [EmpireDestructiveTest("Destructive.Faction")]
    public static void Faction_ResourceProduction_IsFinite()
    {
        FactionFC faction = DestructiveTestUtil.RequireFaction();
        DestructiveTestUtil.EnsureSettlement();

        foreach (var settlement in faction.settlements)
        {
            foreach (var resource in settlement.Resources)
            {
                double prod = resource.production;
                TestAssert.IsFalse(double.IsNaN(prod),
                    $"Production for {resource.def?.defName ?? "null"} in {settlement.Name} should not be NaN");
                TestAssert.IsFalse(double.IsInfinity(prod),
                    $"Production for {resource.def?.defName ?? "null"} in {settlement.Name} should not be infinite");
            }
        }
        DestructiveTestUtil.AssertEmpireInvariants(faction, "Faction_ResourceProduction_IsFinite");
    }
}
