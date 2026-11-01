using System.Collections.Generic;

namespace FactionColonies
{
    /// <summary>Registry of <see cref="ISquadInspectionSection"/>s rendered below the per-pawn
    /// rows in <see cref="Dialog_SquadInspection"/>. Submods register sections from their
    /// firstTick / FinalizeInit hook; cleared on game dispose / load via <see cref="EmpireCacheUtil.InvalidateAll"/>.</summary>
    public static class SquadInspectionRegistry
    {
        // Stored alongside a registration index so we can stable-sort by (Order, regIndex).
        private struct Entry
        {
            public ISquadInspectionSection section;
            public int regIndex;
        }
        private static readonly List<Entry> _entries = new List<Entry>();
        private static int _nextRegIndex = 0;
        private static List<ISquadInspectionSection> _sortedCache;

        internal static void Register(ISquadInspectionSection section)
        {
            if (section is null) return;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].section == section) return;
            }
            _entries.Add(new Entry { section = section, regIndex = _nextRegIndex++ });
            _sortedCache = null;
        }

        internal static void Unregister(ISquadInspectionSection section)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].section == section)
                {
                    _entries.RemoveAt(i);
                    _sortedCache = null;
                    return;
                }
            }
        }

        internal static void ClearAll()
        {
            _entries.Clear();
            _nextRegIndex = 0;
            _sortedCache = null;
        }

        public static IReadOnlyList<ISquadInspectionSection> Sections
        {
            get
            {
                if (_sortedCache == null)
                {
                    List<Entry> copy = new List<Entry>(_entries);
                    copy.Sort((a, b) =>
                    {
                        int byOrder = a.section.Order.CompareTo(b.section.Order);
                        if (byOrder != 0) return byOrder;
                        return a.regIndex.CompareTo(b.regIndex);
                    });
                    List<ISquadInspectionSection> result = new List<ISquadInspectionSection>(copy.Count);
                    for (int i = 0; i < copy.Count; i++) result.Add(copy[i].section);
                    _sortedCache = result;
                }
                return _sortedCache;
            }
        }
    }
}
