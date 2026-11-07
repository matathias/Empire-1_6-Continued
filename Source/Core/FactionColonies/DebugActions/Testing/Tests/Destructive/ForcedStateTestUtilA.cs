namespace FactionColonies;

/// <summary>
/// Precondition-forcing helpers shared by the destructive stat, policy, tithe and building-upkeep
/// tests. Everything here mutates live game state.
/// </summary>
public static class ForcedStateTestUtilA
{
    /// <summary>
    /// Strips every faction-level stat source so <see cref="FactionFC.GetFactionStatValue"/> returns
    /// each stat's identity: clears policies and traits (which also empties the behavior cache),
    /// revokes all edicts, and removes faction-wide events that carry stat modifiers.
    /// </summary>
    public static void ForceCleanFactionStatSlate(FactionFC faction)
    {
        PolicyTestHelper.ClearAll(faction);
        FindFC.PolicyManager.RevokeAllEdicts();
        FindFC.EventManager.RemoveWhere(e => e?.def?.statModifiers is { Count: > 0 }
            && (e.settlementTraitLocations?.Count ?? 0) == 0);
        FindFC.PolicyManager.RebuildBehaviorCache();
        faction.InvalidateFactionStatCache();
    }

    /// <summary>True when the settlement's own (settlement-scope) value of every given stat is its identity.</summary>
    public static bool SettlementStatsAtIdentity(WorldSettlementFC settlement, params FCStatDef[] stats) =>
        stats.All(stat => Math.Abs(settlement.GetSettlementStatValue(stat) - stat.IdentityValue) < 1e-9);

    /// <summary>The stats that feed <c>SettlementBuildings.GetBuildingUpkeep</c>.</summary>
    public static readonly FCStatDef[] UpkeepStats =
    [
        FCStatDefOf.buildingUpkeepBase,
        FCStatDefOf.buildingUpkeepBase_Military,
        FCStatDefOf.buildingUpkeepBase_Civilian,
        FCStatDefOf.buildingUpkeepMultiplier,
        FCStatDefOf.buildingUpkeepMultiplier_Military,
        FCStatDefOf.buildingUpkeepMultiplier_Civilian,
    ];

    /// <summary>
    /// Returns a settlement whose building-upkeep transform is the identity once the faction slate
    /// is clean: it has a buildings comp and no settlement-scope upkeep modifiers. Clears the
    /// faction slate first and creates a fresh settlement when no existing one qualifies.
    /// The caller still owns <see cref="FCSettings.buildingUpkeepDifficultyMult"/>.
    /// </summary>
    public static WorldSettlementFC EnsureDefaultUpkeepSettlement(FactionFC faction)
    {
        ForceCleanFactionStatSlate(faction);
        return DestructiveTestUtil.EnsureSettlement(
            s => s.BuildingsComp is not null && SettlementStatsAtIdentity(s, UpkeepStats));
    }

    /// <summary>Runs <paramref name="action"/> with the building-upkeep difficulty factor set to
    /// <paramref name="value"/>, restoring the player's setting afterwards.</summary>
    public static void WithBuildingUpkeepDifficultyMult(float value, Action action)
    {
        float original = FCSettings.buildingUpkeepDifficultyMult;
        FCSettings.buildingUpkeepDifficultyMult = value;
        try { action(); }
        finally { FCSettings.buildingUpkeepDifficultyMult = original; }
    }

    /// <summary>
    /// Returns the first titheable resource of <paramref name="settlement"/>. Every settlement type
    /// ships titheable resources, so a missing one fails the test.
    /// </summary>
    public static ResourceFC RequireTitheableResource(WorldSettlementFC settlement) =>
        TestAssert.NotNull(settlement.Resources.FirstOrDefault(r => r.canTithe),
            $"Settlement '{settlement.Name}' has no titheable resource");

    /// <summary>Assigns workers to <paramref name="resource"/> until it has at least
    /// <paramref name="count"/>, failing the test if the settlement's worker cap prevents it.</summary>
    public static void EnsureWorkers(WorldSettlementFC settlement, ResourceFC resource, int count = 1)
    {
        int missing = count - resource.assignedWorkers;
        if (missing <= 0) return;
        TestAssert.IsTrue(settlement.IncreaseWorkers(resource, missing),
            $"Could not assign {missing} worker(s) to {resource.def?.defName} in '{settlement.Name}'");
        resource.SetDirtyCache();
    }
}
