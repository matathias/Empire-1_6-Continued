namespace FactionColonies;

public static class RaidTargetRegistry
{
    private static readonly RegistryList<IRaidTarget> _list = new();

    internal static void Register(IRaidTarget target) => _list.Register(target);
    internal static void Unregister(IRaidTarget target) => _list.Unregister(target);
    internal static void ClearAll() => _list.ClearAll();
    /// <summary>Registered targets with a non-null <see cref="IRaidTarget.WorldObject"/> and a valid
    /// <see cref="IRaidTarget.Tile"/>; entries missing either are skipped (logged once).</summary>
    public static IReadOnlyList<IRaidTarget> Targets
        => _list.Valid(t => t.WorldObject is not null && t.Tile.Valid, "has a null WorldObject or an invalid Tile");

    /// <summary>
    /// Finds the <see cref="IRaidTarget"/> wrapping the given <see cref="WorldObject"/>, or null.
    /// </summary>
    public static IRaidTarget FindByWorldObject(WorldObject obj)
    {
        if (obj is null) return null;
        return RegistryDispatch.First(Targets, t => t.WorldObject == obj, nameof(IRaidTarget.WorldObject));
    }
}
