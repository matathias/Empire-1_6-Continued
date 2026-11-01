namespace FactionColonies
{
    /// <summary>
    /// Event data passed to <see cref="IMercenarySquadListener.OnMercenaryDeath"/> when a mercenary is killed.
    /// Pure notification: listeners observe the death but do not gate any built-in replacement
    /// behavior (auto-replacement was removed by the strict-manual outfit refactor; refilling
    /// empty slots is now a player-driven action via <see cref="MercenarySquadFC.FillEmptySlots"/>).
    /// </summary>
    public class MercenaryDeathEvent
    {
        /// <summary>The mercenary that was killed. Still has its original pawn, loadout, and custom data at this point.</summary>
        public Mercenary Mercenary { get; private set; }

        /// <summary>The squad the mercenary belonged to.</summary>
        public MercenarySquadFC Squad { get; private set; }

        /// <summary>The settlement the squad is assigned to (may be null if squad has no settlement).</summary>
        public WorldSettlementFC Settlement { get; private set; }

        public MercenaryDeathEvent(Mercenary mercenary, MercenarySquadFC squad, WorldSettlementFC settlement)
        {
            Mercenary = mercenary;
            Squad = squad;
            Settlement = settlement;
        }
    }
}
