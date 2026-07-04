using RimWorld.Planet;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Base for offensive handlers (raid / capture / enslave). Adds the manual-battle seam:
    /// when the target resolves to a map-gennable enemy settlement, resolution is delegated to
    /// the tile's <see cref="BattlefieldContext.StartOffense"/>, which itself re-checks the
    /// manual-offense setting and the shared concurrent-map cap and falls back to auto-resolve
    /// when appropriate. Subclasses keep their own <c>ApplyResult</c> (loot / capture / enslave)
    /// unchanged; handlers that extend the raw <see cref="MilitaryJobHandler"/> stay auto-only.
    /// </summary>
    public abstract class MilitaryJobHandler_Offensive : MilitaryJobHandler
    {
        /// <summary>
        /// True only when the op's target is a live, non-player, non-Empire settlement whose map
        /// we can generate. The auto/manual/cap decision is made later inside StartOffense
        /// (mirrors StartDefense), so OnEventFired's exception fallback to OnAutoResolve stays
        /// correct even when this returns true.
        /// </summary>
        public override bool ResolvesManually(MilitaryOperation op)
        {
            if (op is null) return false;
            if (!op.targetTile.Valid) return false;
            Settlement target = op.targetObject as Settlement
                ?? Find.WorldObjects.SettlementAt(op.targetTile);
            if (target?.Faction is null) return false;
            if (target.Faction.IsPlayer || FindFC.IsEmpireFaction(target.Faction)) return false;
            return true;
        }

        public override void OnManualResolve(MilitaryOperation op)
        {
            if (op is null) return;
            MilitaryOperationManager mgr = FindFC.MilitaryManager;
            if (mgr is null) { op.BeginAutoResolveProgress(); return; }
            mgr.GetOrCreateBattlefield(op.targetTile).StartOffense(op);
        }

        /// <summary>
        /// When true, a player VICTORY tears the offense map down immediately with no loot-linger.
        /// Capture overrides this: its ApplyResult destroys the host Settlement (the live map's
        /// parent) and builds an Empire colony there, so lingering the map for physical looting
        /// would destroy a live map under the player.
        /// </summary>
        public virtual bool SkipLootLingerOnWin => false;
    }
}
