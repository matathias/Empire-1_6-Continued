namespace FactionColonies;

/// <summary>
/// Base for policy behaviors built around one <see cref="CooldownAbility"/>: holds the cooldown,
/// saves it under <see cref="CooldownSaveKey"/>, and exposes it to the debug actions.
/// </summary>
public abstract class FCCooldownPolicyBehavior : FCPolicyBehavior
{
    protected CooldownAbility cooldown = new();

    /// <summary>Save node name of the cooldown. Must never change, or existing saves lose it.</summary>
    protected abstract string CooldownSaveKey { get; }

    public override void ExposeData()
    {
        Scribe_Deep.Look(ref cooldown, CooldownSaveKey);
        cooldown ??= new CooldownAbility();
    }

    // Debug accessors
    public bool DebugCooldownReady() => cooldown.IsReady;
    public float DebugCooldownDays() => cooldown.DaysRemaining;
    public void DebugResetCooldown() => cooldown.tickLastUsed = -1;
}
