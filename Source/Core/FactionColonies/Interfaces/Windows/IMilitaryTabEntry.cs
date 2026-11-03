using UnityEngine;

namespace FactionColonies;

/// <summary>
/// Allows external mods to display entries in Empire's military tab alongside settlements.
/// Entries appear as simplified cards with name, military level, status, and an auto-defend toggle.
/// Register implementations via <see cref="MilitaryTabRegistry"/>.
/// </summary>
public interface IMilitaryTabEntry
{
    WorldObject WorldObject => null;
    string Name => null;
    int MilitaryLevel => 0;
    bool AutoDefend { get => false; set { } }
    bool IsUnderAttack => false;
    bool IsBusy => false;
    string StatusLabel => null;
    Color AccentColor => Color.white;
}
