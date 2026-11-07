using LudeonTK;

namespace FactionColonies;

/// <summary>
/// A <see cref="DebugActionAttribute"/> for actions that act on the Empire faction or its settlements.
/// The action is hidden from the debug menu until the player has created the Empire faction
/// (see <c>EmpireDebugActionVisibilityPatch</c>). RimWorld's debug-menu scan looks attributes up with
/// inheritance, so these are listed exactly like plain debug actions. Submods use this too.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class EmpireDebugActionAttribute(string category = null, string name = null, int displayPriority = 0,
    bool hideInSubMenu = false)
    : DebugActionAttribute(category, name, displayPriority: displayPriority, hideInSubMenu: hideInSubMenu);
