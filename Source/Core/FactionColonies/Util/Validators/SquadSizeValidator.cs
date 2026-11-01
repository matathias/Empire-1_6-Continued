using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Built-in <see cref="ISquadAssignmentValidator"/>. Rejects assignment when the squad's
    /// occupied slot count exceeds the target settlement's <see cref="WorldSettlementFC.MaxSquadSize"/>.
    /// <para>Counts any merc with a non-blank effective loadout — covers personalized mercs
    /// (loadout pool reference deleted, gear lives in <see cref="Mercenary.ownedLoadout"/>)
    /// and freshly-hired mercs alike. Empty placeholder slots awaiting Fill are excluded.</para>
    /// </summary>
    public class SquadSizeValidator : ISquadAssignmentValidator
    {
        public bool CanAssign(WorldSettlementFC settlement, MercenarySquadFC squad, out string reason)
        {
            reason = null;
            if (settlement is null || squad?.mercenaries is null) return true;
            int size = 0;
            foreach (Mercenary m in squad.mercenaries)
            {
                if (m is null) continue;
                if (m.IsEmptySlot) continue;
                MilUnitFC effective = m.EffectiveLoadout;
                if (effective is null || effective.isBlank) continue;
                size++;
            }
            int max = settlement.MaxSquadSize;
            if (size > max)
            {
                reason = "FCSquadSizeExceeded".Translate(settlement.Name, size, max);
                return false;
            }
            return true;
        }
    }
}
