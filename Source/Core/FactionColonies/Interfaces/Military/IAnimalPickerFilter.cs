using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Lets submods gate which animal kinds appear in the military unit designer's
    /// companion-animal and mount pickers (<see cref="FCWindow_AnimalPicker"/> /
    /// <see cref="FCWindow_MountPicker"/>). All registered filters must allow a kind
    /// for it to be offered (AND semantics); with no filters registered every kind is
    /// allowed, so the base behavior is unchanged when nothing plugs in.
    /// <para>Register via <see cref="AnimalPickerFilterRegistry"/> (or the
    /// <see cref="EmpireRegistry"/> facade).</para>
    /// </summary>
    public interface IAnimalPickerFilter
    {
        /// <summary>
        /// Returns whether <paramref name="animal"/> may be offered in the unit-designer
        /// pickers. Called per-kind during the picker's redraw, so keep it cheap (back it
        /// with a cached set). Return true to allow.
        /// </summary>
        bool IsAnimalAllowed(PawnKindDef animal);
    }
}
