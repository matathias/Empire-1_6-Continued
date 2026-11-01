using RimWorld.Planet;

namespace FactionColonies
{
    /// <summary>
    /// Cache-time, settlement-level modifier. Runs after a settlement entry is mirrored from
    /// its faction's baseline. Use for settlement-attribute-derived effects — e.g. reading
    /// a settlement's <c>CompViralSpread</c> or <c>RimWarSettlementComp</c> to write a
    /// settlement-specific level. Cached, so the squad-attack window's displayed range and
    /// the actual battle agree.
    /// <para>Pure transformation: read <paramref name="settlement"/>, mutate
    /// <paramref name="power"/>. No side effects.</para>
    /// </summary>
    public interface ISettlementPowerModifier
    {
        void ModifySettlementPower(Settlement settlement, EnemyPower power);
    }
}
