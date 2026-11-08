namespace FactionColonies;

/// <summary>
/// Shared steps for removing a conquered enemy settlement from the world: destroying it and
/// defeating its faction once it has no settlements left. Used by the capture and raze outcomes,
/// both the immediate (auto-resolve) path and the deferred one that runs after a manual battle map
/// is torn down.
/// </summary>
public static class SettlementFateUtil
{
    /// <summary>Destroys <paramref name="target"/> and returns whether it is really gone. Mod-protected
    /// settlements (Empire's own WorldSettlementFC, or third-party protected settlements) might
    /// Harmony-patch Destroy to a no-op. Both common shapes are detected: a prefix returning false
    /// leaves Destroyed false, and a postfix that re-adds the object leaves Destroyed true while the
    /// same instance still resolves at <paramref name="tile"/>.</summary>
    public static bool TryDestroySettlement(Settlement target, PlanetTile tile)
    {
        target.Destroy();
        return target.Destroyed && Find.WorldObjects.SettlementAt(tile) != target;
    }

    /// <summary>Marks <paramref name="faction"/> defeated when no settlement on the world belongs
    /// to it any more.</summary>
    public static void MarkDefeatedIfNoSettlementsLeft(Faction faction)
    {
        if (faction is null) return;
        if (!Find.WorldObjects.Settlements.Any(s => s.Faction is not null && s.Faction == faction))
            faction.defeated = true;
    }
}
