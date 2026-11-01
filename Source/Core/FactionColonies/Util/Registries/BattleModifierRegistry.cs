using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;

namespace FactionColonies
{
    /// <summary>
    /// Holds the three lists of power-modifying contributors. Implementations register
    /// here at mod init; <see cref="WorldComponent_EnemyPower"/> is the only sanctioned
    /// caller of the Invoke methods (it owns the lifecycle of cached entries and the
    /// engagement-time hook).
    /// </summary>
    public static class BattleModifierRegistry
    {
        private static readonly RegistryList<IFactionPowerModifier> _factionList = new RegistryList<IFactionPowerModifier>();
        private static readonly RegistryList<ISettlementPowerModifier> _settlementList = new RegistryList<ISettlementPowerModifier>();
        private static readonly RegistryList<IBattleModifier> _battleList = new RegistryList<IBattleModifier>();

        /* === Registration === */

        internal static void Register(IFactionPowerModifier modifier) => _factionList.Register(modifier);
        internal static void Register(ISettlementPowerModifier modifier) => _settlementList.Register(modifier);
        internal static void Register(IBattleModifier modifier) => _battleList.Register(modifier);

        internal static void Unregister(IFactionPowerModifier modifier) => _factionList.Unregister(modifier);
        internal static void Unregister(ISettlementPowerModifier modifier) => _settlementList.Unregister(modifier);
        internal static void Unregister(IBattleModifier modifier) => _battleList.Unregister(modifier);

        internal static void ClearAll()
        {
            _factionList.ClearAll();
            _settlementList.ClearAll();
            _battleList.ClearAll();
        }

        public static IReadOnlyList<IFactionPowerModifier> FactionModifiers => _factionList.Items;
        public static IReadOnlyList<ISettlementPowerModifier> SettlementModifiers => _settlementList.Items;
        public static IReadOnlyList<IBattleModifier> BattleModifiers => _battleList.Items;

        /* === Invocation (worldcomp-only) === */

        /// <summary>
        /// Faction-level cache pass. Called by <see cref="WorldComponent_EnemyPower"/> after
        /// <c>ComputeFactionBaseline</c> populates a faction entry's level/efficiency.
        /// </summary>
        public static void InvokeFactionPowerModifiers(Faction faction, EnemyPower power)
            => RegistryDispatch.Each(_factionList.Items,
                m => m.ModifyFactionPower(faction, power),
                nameof(IFactionPowerModifier.ModifyFactionPower));

        /// <summary>
        /// Settlement-level cache pass. Called by <see cref="WorldComponent_EnemyPower"/> after
        /// a settlement entry is mirrored from its faction's baseline.
        /// </summary>
        public static void InvokeSettlementPowerModifiers(Settlement settlement, EnemyPower power)
            => RegistryDispatch.Each(_settlementList.Items,
                m => m.ModifySettlementPower(settlement, power),
                nameof(ISettlementPowerModifier.ModifySettlementPower));

        /// <summary>
        /// Attack-time pass. Called by <see cref="WorldComponent_EnemyPower"/>'s resolution
        /// helpers (ResolveDefenderForceForOp, ResolveDefenderBounds, ApplyBattleModifiers).
        /// </summary>
        public static void InvokeBattleModifiers(BattleForceContext ctx, MilitaryForce force, bool isAttacker)
            => RegistryDispatch.Each(_battleList.Items,
                m => m.ModifyForce(ctx, force, isAttacker),
                nameof(IBattleModifier.ModifyForce));
    }
}
