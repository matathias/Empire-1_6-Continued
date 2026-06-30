namespace FactionColonies
{
    /// <summary>
    /// Unified registration entry point for every per-domain registry under
    /// <see cref="util.Registries"/>. Probes <paramref name="participant"/> for each
    /// supported interface (or class, in <see cref="BuildingFilter"/>'s case) and forwards
    /// it to the matching domain registry. Submods that implement multiple interfaces
    /// can register once instead of once per registry.
    /// <para>The per-domain typed <c>Register(IFoo)</c> methods remain available for code
    /// that prefers explicit registration; this facade is purely additive.</para>
    /// <para>Excluded: <see cref="MilitaryWindowRegistry"/> — its slot-keyed registration
    /// API (<c>Register(slot, factory)</c>) is shape-incompatible with the object-probe
    /// pattern and is invoked directly.</para>
    /// </summary>
    public static class EmpireRegistry
    {
        public static void Register(object participant)
        {
            if (participant is null) return;
            bool any = false;

            /* Lifecycle (LifecycleRegistry.Register accepts object and probes 4 sub-interfaces;
               it emits its own "no listener interface" warning, so we only call it when we
               know there's at least one match — and we don't double-warn here). */
            if (participant is ISettlementListener
                || participant is IMilitaryOperationListener
                || participant is IMercenarySquadListener
                || participant is IResearchListener)
            {
                LifecycleRegistry.Register(participant);
                any = true;
            }

            /* Validators */
            if (participant is IDefenseValidator dv)            { DefenseValidatorRegistry.Register(dv);   any = true; }
            if (participant is ISquadAssignmentValidator sav)   { SquadAssignmentRegistry.Register(sav);   any = true; }
            if (participant is ISettlementFoundingValidator fv) { FoundingValidatorRegistry.Register(fv);  any = true; }

            /* Modifiers / providers / contributors */
            if (participant is IBattleModifier bm)              { BattleModifierRegistry.Register(bm);     any = true; }
            if (participant is IFactionPowerModifier fpm)       { BattleModifierRegistry.Register(fpm);    any = true; }
            if (participant is ISettlementPowerModifier spm)    { BattleModifierRegistry.Register(spm);    any = true; }
            if (participant is ISilverPaymentModifier spy)      { SilverPaymentRegistry.Register(spy);     any = true; }
            if (participant is ISquadPowerModifier sp)          { SquadPowerRegistry.Register(sp);         any = true; }
            if (participant is IRaidWeightProvider rw)          { RaidWeightRegistry.Register(rw);         any = true; }
            if (participant is IThreatScalingContributor ts)    { ThreatScalingRegistry.Register(ts);      any = true; }
            if (participant is IMercAutoTendProvider mat)       { MercAutoTendRegistry.Register(mat);      any = true; }

            /* Tax cycle */
            if (participant is ITaxTickParticipant tt)          { TaxTickRegistry.Register(tt);            any = true; }
            if (participant is IDailyAccrualParticipant da)     { DailyAccrualRegistry.Register(da);       any = true; }
            if (participant is ITaxDeliveryInterceptor td)      { TaxDeliveryRegistry.Register(td);        any = true; }

            /* UI / external-target collections */
            if (participant is IMainTabWindowOverview mt)       { MainTableRegistry.Register(mt);          any = true; }
            if (participant is ISettlementWindowButton swb)     { SettlementButtonRegistry.Register(swb);  any = true; }
            if (participant is ISquadInspectionSection sis)     { SquadInspectionRegistry.Register(sis);   any = true; }
            if (participant is IRaidTarget rt)                  { RaidTargetRegistry.Register(rt);         any = true; }
            if (participant is IAutoDefender ad)                { AutoDefenderRegistry.Register(ad);       any = true; }
            if (participant is IMilitaryTabEntry mte)           { MilitaryTabRegistry.Register(mte);       any = true; }
            if (participant is IRoadNodeProvider rnp)           { RoadNodeProviderRegistry.Register(rnp);  any = true; }
            if (participant is BuildingFilter bf)               { BuildingFilterRegistry.Register(bf);     any = true; }
            if (participant is IAnimalPickerFilter apf)         { AnimalPickerFilterRegistry.Register(apf); any = true; }

            if (!any)
            {
                LogUtil.Warning($"EmpireRegistry.Register: {participant.GetType().Name} matched no registry; ignored");
            }
        }

        public static void Unregister(object participant)
        {
            if (participant is null) return;

            if (participant is ISettlementListener
                || participant is IMilitaryOperationListener
                || participant is IMercenarySquadListener
                || participant is IResearchListener)
            {
                LifecycleRegistry.Unregister(participant);
            }

            if (participant is IDefenseValidator dv)            DefenseValidatorRegistry.Unregister(dv);
            if (participant is ISquadAssignmentValidator sav)   SquadAssignmentRegistry.Unregister(sav);
            if (participant is ISettlementFoundingValidator fv) FoundingValidatorRegistry.Unregister(fv);
            if (participant is IBattleModifier bm)              BattleModifierRegistry.Unregister(bm);
            if (participant is IFactionPowerModifier fpm)       BattleModifierRegistry.Unregister(fpm);
            if (participant is ISettlementPowerModifier spm)    BattleModifierRegistry.Unregister(spm);
            if (participant is ISilverPaymentModifier spy)      SilverPaymentRegistry.Unregister(spy);
            if (participant is ISquadPowerModifier sp)          SquadPowerRegistry.Unregister(sp);
            if (participant is IRaidWeightProvider rw)          RaidWeightRegistry.Unregister(rw);
            if (participant is IThreatScalingContributor ts)    ThreatScalingRegistry.Unregister(ts);
            if (participant is IMercAutoTendProvider mat)       MercAutoTendRegistry.Unregister(mat);
            if (participant is ITaxTickParticipant tt)          TaxTickRegistry.Unregister(tt);
            if (participant is IDailyAccrualParticipant da)     DailyAccrualRegistry.Unregister(da);
            if (participant is ITaxDeliveryInterceptor td)      TaxDeliveryRegistry.Unregister(td);
            if (participant is IMainTabWindowOverview mt)       MainTableRegistry.Unregister(mt);
            if (participant is ISettlementWindowButton swb)     SettlementButtonRegistry.Unregister(swb);
            if (participant is ISquadInspectionSection sis)     SquadInspectionRegistry.Unregister(sis);
            if (participant is IRaidTarget rt)                  RaidTargetRegistry.Unregister(rt);
            if (participant is IAutoDefender ad)                AutoDefenderRegistry.Unregister(ad);
            if (participant is IMilitaryTabEntry mte)           MilitaryTabRegistry.Unregister(mte);
            if (participant is IRoadNodeProvider rnp)           RoadNodeProviderRegistry.Unregister(rnp);
            if (participant is BuildingFilter bf)               BuildingFilterRegistry.Unregister(bf);
            if (participant is IAnimalPickerFilter apf)         AnimalPickerFilterRegistry.Unregister(apf);
        }

        /// <summary>
        /// Clears every facade-managed registry. Called from
        /// <see cref="EmpireCacheUtil.InvalidateAll"/> on game dispose / ClearCaches.
        /// <see cref="MilitaryWindowRegistry"/> is not facade-managed; its <c>ClearAll</c>
        /// (if needed) is called separately.
        /// </summary>
        public static void ClearAll()
        {
            LifecycleRegistry.ClearAll();
            DefenseValidatorRegistry.ClearAll();
            SquadAssignmentRegistry.ClearAll();
            FoundingValidatorRegistry.ClearAll();
            BattleModifierRegistry.ClearAll();
            SilverPaymentRegistry.ClearAll();
            SquadPowerRegistry.ClearAll();
            RaidWeightRegistry.ClearAll();
            ThreatScalingRegistry.ClearAll();
            MercAutoTendRegistry.ClearAll();
            TaxTickRegistry.ClearAll();
            DailyAccrualRegistry.ClearAll();
            TaxDeliveryRegistry.ClearAll();
            MainTableRegistry.ClearAll();
            SettlementButtonRegistry.ClearAll();
            SquadInspectionRegistry.ClearAll();
            RaidTargetRegistry.ClearAll();
            AutoDefenderRegistry.ClearAll();
            MilitaryTabRegistry.ClearAll();
            RoadNodeProviderRegistry.ClearAll();
            BuildingFilterRegistry.ClearAll();
            AnimalPickerFilterRegistry.ClearAll();
        }
    }
}
