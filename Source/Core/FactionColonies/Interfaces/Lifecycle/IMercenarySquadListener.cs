namespace FactionColonies
{
    /// <summary>
    /// Lifecycle hook for mercenary squad events — individual merc deaths plus squad-level
    /// hire/dismiss/upgrade. Register implementations via <see cref="LifecycleRegistry"/>.
    /// </summary>
    public interface IMercenarySquadListener
    {
        /// <summary>
        /// Called when a mercenary is killed. Notification only. No built-in replacement
        /// behavior is gated by listeners (auto-replacement was removed; refilling empty
        /// slots is a player-driven action via <see cref="MercenarySquadFC.FillEmptySlots"/>).
        /// </summary>
        void OnMercenaryDeath(MercenaryDeathEvent evt);
        /// <summary>
        /// Called immediately after a fresh <see cref="MercenarySquadFC"/> is hired (silver paid,
        /// squad created from a template, added to <c>mercenarySquads</c>) and before the player
        /// has assigned it to a settlement. <c>squad.settlement</c> is null at this point.
        /// </summary>
        void OnSquadHired(MercenarySquadFC squad);
        /// <summary>
        /// Called when a squad is dismissed by the player. The squad has been removed from
        /// <c>mercenarySquads</c>.
        /// </summary>
        void OnSquadDismissed(MercenarySquadFC squad);
        /// <summary>
        /// Called after a squad's loadout is brought up to its source template via
        /// <see cref="SquadUpgradeUtil.UpgradeToTemplate"/>. Silver has already been paid.
        /// </summary>
        void OnSquadUpgraded(MercenarySquadFC squad);
    }
}
