namespace FactionColonies
{
    /// <summary>
    /// Lifecycle phase of a <see cref="MilitaryOperation"/>.
    /// <para>Scheduled — created but not yet acting (e.g. defensive 24h warning window).</para>
    /// <para>Traveling — squad in transit toward target.</para>
    /// <para>Engaged — battle in progress (auto-resolving or manual map). Mid-battle / post-battle
    /// "player still on map" state lives on <see cref="BattlefieldContext"/>, not on the op.</para>
    /// <para>CooldownPending — awaiting cooldown event to fire.</para>
    /// <para>Resolved — terminal; manager has unregistered the op.</para>
    /// </summary>
    public enum MilitaryOperationPhase
    {
        Scheduled = 0,
        Traveling = 1,
        Engaged = 2,
        CooldownPending = 3,
        Resolved = 4
    }

    /// <summary>
    /// Side of an op a participant occupies. Indexed lookups (e.g. <c>GetOpsForSettlement</c>)
    /// can filter by side to answer questions like "is this settlement attacking or defending?".
    /// </summary>
    public enum ParticipantSide
    {
        Aggressor = 0,
        Defender = 1
    }
}
