using RimWorld.Planet;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Allows external mods to register world objects as raid targets for Empire's military system.
    /// Registered targets are included in the attack target pool alongside Empire settlements,
    /// receive the same 24-hour warning, and auto-resolve via the per-round battle engine.
    /// Register implementations via <see cref="RaidTargetRegistry"/>.
    /// </summary>
    public interface IRaidTarget
    {
        /// <summary>The world object this target wraps (for serialization and <see cref="LookTargets"/>).</summary>
        WorldObject WorldObject { get; }
        string Name { get; }
        PlanetTile Tile { get; }
        /// <summary>Virtual military level used for targeting weight and auto-defend comparison.</summary>
        int MilitaryLevel { get; }
        /// <summary>Set by the attack system to prevent duplicate attacks. Cleared on resolution.</summary>
        bool IsUnderAttack { get; set; }
        void OnRaidWon(BattleResult result);
        void OnRaidLost(BattleResult result);
    }
}
