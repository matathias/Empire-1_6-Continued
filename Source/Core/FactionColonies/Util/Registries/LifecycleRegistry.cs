using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Dispatches settlement, military operation, mercenary squad, and research lifecycle events
    /// to participants implementing one or more of <see cref="ISettlementListener"/>,
    /// <see cref="IMilitaryOperationListener"/>, <see cref="IMercenarySquadListener"/>,
    /// <see cref="IResearchListener"/>. A participant implementing multiple listener interfaces
    /// is registered against all matching domain lists in a single <see cref="Register"/> call.
    /// </summary>
    public static class LifecycleRegistry
    {
        private static readonly RegistryList<ISettlementListener>        _settlement = new RegistryList<ISettlementListener>();
        private static readonly RegistryList<IMilitaryOperationListener> _militaryOp = new RegistryList<IMilitaryOperationListener>();
        private static readonly RegistryList<IMercenarySquadListener>    _mercSquad  = new RegistryList<IMercenarySquadListener>();
        private static readonly RegistryList<IResearchListener>          _research   = new RegistryList<IResearchListener>();

        internal static void Register(object participant)
        {
            if (participant is null) return;

            if (participant is ISettlementListener s)        _settlement.Register(s);
            if (participant is IMilitaryOperationListener m) _militaryOp.Register(m);
            if (participant is IMercenarySquadListener q)    _mercSquad.Register(q);
            if (participant is IResearchListener r)          _research.Register(r);

            bool implementsAny = participant is ISettlementListener
                || participant is IMilitaryOperationListener
                || participant is IMercenarySquadListener
                || participant is IResearchListener;
            if (!implementsAny)
            {
                LogUtil.Warning($"LifecycleRegistry.Register: {participant.GetType().Name} implements no listener interface; ignored");
            }
        }

        internal static void Unregister(object participant)
        {
            if (participant is null) return;
            if (participant is ISettlementListener s)        _settlement.Unregister(s);
            if (participant is IMilitaryOperationListener m) _militaryOp.Unregister(m);
            if (participant is IMercenarySquadListener q)    _mercSquad.Unregister(q);
            if (participant is IResearchListener r)          _research.Unregister(r);
        }

        internal static void ClearAll()
        {
            _settlement.ClearAll();
            _militaryOp.ClearAll();
            _mercSquad.ClearAll();
            _research.ClearAll();
        }

        /* Settlement */
        public static void InvokeOnSettlementCreated(WorldSettlementFC settlement)
        {
            RegistryDispatch.EachInvalidating(_settlement.Items,
                p => p.OnSettlementCreated(settlement),
                () => settlement.InvalidateStatCache(),
                nameof(ISettlementListener.OnSettlementCreated));
            settlement.InvalidateStatCache();
        }

        public static void InvokeOnSettlementRemoved(WorldSettlementFC settlement)
        {
            RegistryDispatch.EachInvalidating(_settlement.Items,
                p => p.OnSettlementRemoved(settlement),
                () => settlement.InvalidateStatCache(),
                nameof(ISettlementListener.OnSettlementRemoved));
            settlement.InvalidateStatCache();
        }

        public static void InvokeOnSettlementUpgraded(WorldSettlementFC settlement, int oldLevel, int newLevel)
        {
            RegistryDispatch.EachInvalidating(_settlement.Items,
                p => p.OnSettlementUpgraded(settlement, oldLevel, newLevel),
                () => settlement.InvalidateStatCache(),
                nameof(ISettlementListener.OnSettlementUpgraded));
            settlement.InvalidateStatCache();
        }

        public static void InvokeOnSettlementTypeChanged(WorldSettlementFC settlement, WorldSettlementDef oldDef, WorldSettlementDef newDef)
        {
            RegistryDispatch.EachInvalidating(_settlement.Items,
                p => p.OnSettlementTypeChanged(settlement, oldDef, newDef),
                () => settlement.InvalidateStatCache(),
                nameof(ISettlementListener.OnSettlementTypeChanged));
            settlement.InvalidateStatCache();
        }

        /* Building */
        public static void InvokeOnBuildingConstructed(WorldSettlementFC settlement, BuildingFCDef building, int slot)
        {
            RegistryDispatch.EachInvalidating(_settlement.Items,
                p => p.OnBuildingConstructed(settlement, building, slot),
                () => settlement.InvalidateStatCache(),
                nameof(ISettlementListener.OnBuildingConstructed));
            settlement.InvalidateStatCache();
        }

        public static void InvokeOnBuildingDeconstructed(WorldSettlementFC settlement, BuildingFCDef building, int slot)
        {
            RegistryDispatch.EachInvalidating(_settlement.Items,
                p => p.OnBuildingDeconstructed(settlement, building, slot),
                () => settlement.InvalidateStatCache(),
                nameof(ISettlementListener.OnBuildingDeconstructed));
            settlement.InvalidateStatCache();
        }

        /* Military Operation */

        public static void InvokeOnOperationCreated(MilitaryOperation op)
        {
            if (op is null) return;
            WorldSettlementFC settlement = op.aggressor?.homeSettlement ?? op.defender?.homeSettlement;
            RegistryDispatch.EachInvalidating(_militaryOp.Items,
                p => p.OnOperationCreated(op),
                () => { if (settlement is object) settlement.InvalidateStatCache(); },
                nameof(IMilitaryOperationListener.OnOperationCreated));
            if (settlement is object) settlement.InvalidateStatCache();
        }

        public static void InvokeOnOperationResolved(MilitaryOperation op)
        {
            if (op is null) return;
            WorldSettlementFC settlement = op.aggressor?.homeSettlement ?? op.defender?.homeSettlement;
            RegistryDispatch.EachInvalidating(_militaryOp.Items,
                p => p.OnOperationResolved(op),
                () => { if (settlement is object) settlement.InvalidateStatCache(); },
                nameof(IMilitaryOperationListener.OnOperationResolved));
            if (settlement is object) settlement.InvalidateStatCache();
        }

        public static void InvokeOnBattleResolved(MilitaryOperation op, bool victory, BattleResult result)
        {
            if (op is null) return;
            WorldSettlementFC settlement = op.aggressor?.homeSettlement ?? op.defender?.homeSettlement;
            RegistryDispatch.EachInvalidating(_militaryOp.Items,
                p => p.OnBattleResolved(op, victory, result),
                () => { if (settlement is object) settlement.InvalidateStatCache(); },
                nameof(IMilitaryOperationListener.OnBattleResolved));
            if (settlement is object) settlement.InvalidateStatCache();
        }

        /* Mercenary */
        public static void InvokeOnMercenaryDeath(MercenaryDeathEvent evt)
            => RegistryDispatch.Each(_mercSquad.Items, p => p.OnMercenaryDeath(evt), nameof(IMercenarySquadListener.OnMercenaryDeath));

        /* Squad lifecycle */
        public static void InvokeOnSquadHired(MercenarySquadFC squad)
        {
            if (squad is null) return;
            RegistryDispatch.Each(_mercSquad.Items, p => p.OnSquadHired(squad), nameof(IMercenarySquadListener.OnSquadHired));
        }

        public static void InvokeOnSquadDismissed(MercenarySquadFC squad)
        {
            if (squad is null) return;
            RegistryDispatch.Each(_mercSquad.Items, p => p.OnSquadDismissed(squad), nameof(IMercenarySquadListener.OnSquadDismissed));
        }

        public static void InvokeOnSquadUpgraded(MercenarySquadFC squad)
        {
            if (squad is null) return;
            RegistryDispatch.Each(_mercSquad.Items, p => p.OnSquadUpgraded(squad), nameof(IMercenarySquadListener.OnSquadUpgraded));
        }

        /* Research */
        public static void InvokeOnResearchCompleted(ResearchProjectDef project)
        {
            RegistryDispatch.EachInvalidating(_research.Items,
                p => p.OnResearchCompleted(project),
                () => FindFC.FactionComp?.InvalidateAllSettlementStatCaches(),
                nameof(IResearchListener.OnResearchCompleted));
            FindFC.FactionComp?.InvalidateAllSettlementStatCaches();
        }
    }
}
