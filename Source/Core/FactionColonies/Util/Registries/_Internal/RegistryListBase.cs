namespace FactionColonies;

/// <summary>
/// Tracks every <see cref="RegistryList{T}"/> so their contents can be captured and put back as a
/// whole. Tests that call <see cref="EmpireRegistry.ClearAll"/> use this to give the live game its
/// registrations back: per-game participants (FactionFC, submod world components) only register on
/// load, so a cleared registry would otherwise stay empty for the rest of the session.
/// </summary>
internal abstract class RegistryListBase
{
    private static readonly List<RegistryListBase> all = [];

    protected RegistryListBase()
    {
        all.Add(this);
    }

    internal abstract object Capture();

    internal abstract void Restore(object state);

    /// <summary>The contents of every registry list, for <see cref="RestoreAll"/>.</summary>
    internal static List<(RegistryListBase list, object state)> CaptureAll()
    {
        List<(RegistryListBase, object)> snapshot = new(all.Count);
        foreach (RegistryListBase list in all)
            snapshot.Add((list, list.Capture()));
        return snapshot;
    }

    internal static void RestoreAll(List<(RegistryListBase list, object state)> snapshot)
    {
        if (snapshot is null) return;
        foreach ((RegistryListBase list, object state) in snapshot)
            list.Restore(state);
    }
}
