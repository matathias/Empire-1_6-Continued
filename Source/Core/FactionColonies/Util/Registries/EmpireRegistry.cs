namespace FactionColonies;

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
    // Participants re-registered after every ClearAll; see RegisterPersistent.
    private static readonly List<object> persistent = [];

    /// <summary>
    /// Registers <paramref name="participant"/> and keeps it registered for the rest of the session:
    /// <see cref="ClearAll"/> (which runs on every new game and load) re-registers it afterwards.
    /// For app-lifetime participants registered at startup, such as a submod's singletons.
    /// <see cref="Unregister"/> drops it for good.
    /// </summary>
    public static void RegisterPersistent(object participant)
    {
        if (participant is null) return;
        if (!persistent.Contains(participant))
            persistent.Add(participant);
        Register(participant);
    }

    /* One row per facade-managed registry: how to clear it, and each participant type it accepts
     * with that type's register/unregister calls. Register and Unregister walk the rows in order;
     * ClearAll clears each registry once. */
    private sealed class RegistryRow
    {
        public readonly Action clear;
        public readonly Binding[] bindings;

        public RegistryRow(Action clear, params Binding[] bindings)
        {
            this.clear = clear;
            this.bindings = bindings;
        }
    }

    private readonly struct Binding
    {
        public readonly Func<object, bool> matches;
        public readonly Action<object> register;
        public readonly Action<object> unregister;

        public Binding(Func<object, bool> matches, Action<object> register, Action<object> unregister)
        {
            this.matches = matches;
            this.register = register;
            this.unregister = unregister;
        }
    }

    private static Binding Bind<T>(Action<T> register, Action<T> unregister) where T : class
        => new(p => p is T, p => register((T)p), p => unregister((T)p));

    private static RegistryRow Row<T>(Action<T> register, Action<T> unregister, Action clear) where T : class
        => new(clear, Bind(register, unregister));

    private static readonly RegistryRow[] rows =
    [
        /* Lifecycle (LifecycleRegistry.Register accepts object and probes 4 sub-interfaces;
           it emits its own "no listener interface" warning, so it is only called when there
           is at least one match). */
        new(LifecycleRegistry.ClearAll, new Binding(
            p => p is ISettlementListener || p is IMilitaryOperationListener
                || p is IMercenarySquadListener || p is IResearchListener,
            LifecycleRegistry.Register, LifecycleRegistry.Unregister)),

        /* Validators */
        Row<IDefenseValidator>(DefenseValidatorRegistry.Register, DefenseValidatorRegistry.Unregister, DefenseValidatorRegistry.ClearAll),
        Row<ISquadAssignmentValidator>(SquadAssignmentRegistry.Register, SquadAssignmentRegistry.Unregister, SquadAssignmentRegistry.ClearAll),
        Row<ISettlementFoundingValidator>(FoundingValidatorRegistry.Register, FoundingValidatorRegistry.Unregister, FoundingValidatorRegistry.ClearAll),

        /* Modifiers / providers / contributors */
        new(BattleModifierRegistry.ClearAll,
            Bind<IBattleModifier>(BattleModifierRegistry.Register, BattleModifierRegistry.Unregister),
            Bind<IFactionPowerModifier>(BattleModifierRegistry.Register, BattleModifierRegistry.Unregister),
            Bind<ISettlementPowerModifier>(BattleModifierRegistry.Register, BattleModifierRegistry.Unregister)),
        Row<ISilverPaymentModifier>(SilverPaymentRegistry.Register, SilverPaymentRegistry.Unregister, SilverPaymentRegistry.ClearAll),
        Row<ISquadPowerModifier>(SquadPowerRegistry.Register, SquadPowerRegistry.Unregister, SquadPowerRegistry.ClearAll),
        Row<IRaidWeightProvider>(RaidWeightRegistry.Register, RaidWeightRegistry.Unregister, RaidWeightRegistry.ClearAll),
        Row<IThreatScalingContributor>(ThreatScalingRegistry.Register, ThreatScalingRegistry.Unregister, ThreatScalingRegistry.ClearAll),
        Row<IMercAutoTendProvider>(MercAutoTendRegistry.Register, MercAutoTendRegistry.Unregister, MercAutoTendRegistry.ClearAll),

        /* Tax cycle */
        Row<ITaxTickParticipant>(TaxTickRegistry.Register, TaxTickRegistry.Unregister, TaxTickRegistry.ClearAll),
        Row<IDailyAccrualParticipant>(DailyAccrualRegistry.Register, DailyAccrualRegistry.Unregister, DailyAccrualRegistry.ClearAll),
        Row<ITaxDeliveryInterceptor>(TaxDeliveryRegistry.Register, TaxDeliveryRegistry.Unregister, TaxDeliveryRegistry.ClearAll),

        /* UI / external-target collections */
        Row<IMainTabWindowOverview>(MainTableRegistry.Register, MainTableRegistry.Unregister, MainTableRegistry.ClearAll),
        Row<ISettlementWindowButton>(SettlementButtonRegistry.Register, SettlementButtonRegistry.Unregister, SettlementButtonRegistry.ClearAll),
        Row<ISquadInspectionSection>(SquadInspectionRegistry.Register, SquadInspectionRegistry.Unregister, SquadInspectionRegistry.ClearAll),
        Row<IRaidTarget>(RaidTargetRegistry.Register, RaidTargetRegistry.Unregister, RaidTargetRegistry.ClearAll),
        Row<IAutoDefender>(AutoDefenderRegistry.Register, AutoDefenderRegistry.Unregister, AutoDefenderRegistry.ClearAll),
        Row<IMilitaryTabEntry>(MilitaryTabRegistry.Register, MilitaryTabRegistry.Unregister, MilitaryTabRegistry.ClearAll),
        Row<IRoadNodeProvider>(RoadNodeProviderRegistry.Register, RoadNodeProviderRegistry.Unregister, RoadNodeProviderRegistry.ClearAll),
        Row<BuildingFilter>(BuildingFilterRegistry.Register, BuildingFilterRegistry.Unregister, BuildingFilterRegistry.ClearAll),
        Row<IAnimalPickerFilter>(AnimalPickerFilterRegistry.Register, AnimalPickerFilterRegistry.Unregister, AnimalPickerFilterRegistry.ClearAll),
    ];

    public static void Register(object participant)
    {
        if (participant is null) return;
        bool any = false;

        foreach (RegistryRow row in rows)
        {
            foreach (Binding binding in row.bindings)
            {
                if (!binding.matches(participant)) continue;
                binding.register(participant);
                any = true;
            }
        }

        if (!any)
        {
            LogUtil.Warning($"EmpireRegistry.Register: {participant.GetType().Name} matched no registry; ignored");
        }
    }

    public static void Unregister(object participant)
    {
        if (participant is null) return;
        persistent.Remove(participant);

        foreach (RegistryRow row in rows)
        {
            foreach (Binding binding in row.bindings)
            {
                if (binding.matches(participant)) binding.unregister(participant);
            }
        }
    }

    /// <summary>
    /// Clears every facade-managed registry, then re-registers the participants added with
    /// <see cref="RegisterPersistent"/>. Called from
    /// <see cref="EmpireCacheUtil.InvalidateAll"/> on game dispose / ClearCaches.
    /// <see cref="MilitaryWindowRegistry"/> is not facade-managed; its <c>ClearAll</c>
    /// (if needed) is called separately.
    /// </summary>
    public static void ClearAll()
    {
        foreach (RegistryRow row in rows)
            row.clear();

        foreach (object participant in persistent)
            Register(participant);
    }
}
