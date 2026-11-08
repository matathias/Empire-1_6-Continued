namespace FactionColonies;

/// <summary>Registry of <see cref="ISquadInspectionSection"/>s rendered below the per-pawn
/// rows in <see cref="Dialog_SquadInspection"/>. Submods register sections from their
/// firstTick / FinalizeInit hook; cleared on game dispose / load via <see cref="EmpireCacheUtil.InvalidateAll"/>.</summary>
public static class SquadInspectionRegistry
{
    private static readonly RegistryList<ISquadInspectionSection> _sections = new();
    private static List<ISquadInspectionSection> _sortedCache;
    private static int _sortedVersion;

    internal static void Register(ISquadInspectionSection section) => _sections.Register(section);

    internal static void Unregister(ISquadInspectionSection section) => _sections.Unregister(section);

    internal static void ClearAll() => _sections.ClearAll();

    /// <summary>Sections sorted by <see cref="ISquadInspectionSection.Order"/>; equal orders keep
    /// registration order (the backing list is in registration order and the sort is stable).</summary>
    public static IReadOnlyList<ISquadInspectionSection> Sections
    {
        get
        {
            if (_sortedCache is null || _sortedVersion != _sections.Version)
            {
                _sortedCache = _sections.Items.OrderBy(s => s.Order).ToList();
                _sortedVersion = _sections.Version;
            }
            return _sortedCache;
        }
    }
}
