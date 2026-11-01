using RimWorld.Planet;
using UnityEngine;

namespace FactionColonies
{
    /// <summary>
    /// Allows external mods to display entries in Empire's military tab alongside settlements.
    /// Entries appear as simplified cards with name, military level, status, and an auto-defend toggle.
    /// Register implementations via <see cref="MilitaryTabRegistry"/>.
    /// </summary>
    public interface IMilitaryTabEntry
    {
        WorldObject WorldObject { get; }
        string Name { get; }
        int MilitaryLevel { get; }
        bool AutoDefend { get; set; }
        bool IsUnderAttack { get; }
        bool IsBusy { get; }
        string StatusLabel { get; }
        Color AccentColor { get; }
    }
}
