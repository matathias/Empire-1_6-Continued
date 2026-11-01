using RimWorld;

namespace FactionColonies
{
    /// <summary>
    /// Cache-time, faction-level modifier. Mutates the cached <see cref="EnemyPower"/> baseline
    /// after <see cref="WorldComponent_EnemyPower"/> derives it from tech level + ETL +
    /// threat adaptation, and BEFORE any settlement entry mirrors it. Use for faction-wide
    /// effects (e.g. a Diplomacy submod that weakens a faction whose leader is sick).
    /// <para>Pure transformation: read <paramref name="faction"/>, mutate <paramref name="power"/>.
    /// No side effects.</para>
    /// </summary>
    public interface IFactionPowerModifier
    {
        void ModifyFactionPower(Faction faction, EnemyPower power);
    }
}
