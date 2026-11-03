namespace FactionColonies;

public static class MilitaryTabRegistry
{
    private static readonly RegistryList<IMilitaryTabEntry> _list = new();

    internal static void Register(IMilitaryTabEntry entry) => _list.Register(entry);
    internal static void Unregister(IMilitaryTabEntry entry) => _list.Unregister(entry);
    internal static void ClearAll() => _list.ClearAll();
    /// <summary>Registered entries with a non-null <see cref="IMilitaryTabEntry.WorldObject"/>; entries
    /// without one are skipped (logged once).</summary>
    public static IReadOnlyList<IMilitaryTabEntry> Entries
        => _list.Valid(e => e.WorldObject is not null, "has a null WorldObject");
}
