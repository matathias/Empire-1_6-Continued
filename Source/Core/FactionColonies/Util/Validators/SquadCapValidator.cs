using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Built-in <see cref="ISquadAssignmentValidator"/>. Rejects assignment when the target
    /// settlement is already at its <see cref="WorldSettlementFC.SquadCap"/>.
    /// <para>An "already-stationed" squad reassigning within its current settlement is a no-op
    /// and must not be rejected — count it once, not twice.</para>
    /// </summary>
    public class SquadCapValidator : ISquadAssignmentValidator
    {
        public bool CanAssign(WorldSettlementFC settlement, MercenarySquadFC squad, out string reason)
        {
            reason = null;
            if (settlement is null || squad is null) return true;
            // Squad already at this settlement: it's a no-op reassign, not a new occupant.
            if (squad.settlement == settlement) return true;
            int cap = settlement.SquadCap;
            int current = settlement.StationedSquads.Count;
            if (current >= cap)
            {
                reason = "FCSquadCapReached".Translate(settlement.Name, current, cap);
                return false;
            }
            return true;
        }
    }
}
