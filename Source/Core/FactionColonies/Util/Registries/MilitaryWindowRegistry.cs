namespace FactionColonies;

public static class MilitaryWindowRegistry
{
    public delegate MilitaryWindow WindowFactory(MilitaryFC mfc, FactionFC faction);

    private static WindowFactory _unitsFactory;
    private static WindowFactory _squadsFactory;
    private static WindowFactory _fireSupportFactory;

    private static string _unitsRegistrant;
    private static string _squadsRegistrant;
    private static string _fireSupportRegistrant;

    /* Registration */

    public static void Register(MilitaryWindowSlot slot, WindowFactory factory, string modName = null)
    {
        if (factory is null) throw new ArgumentNullException(nameof(factory));

        switch (slot)
        {
            case MilitaryWindowSlot.Units:
                if (_unitsFactory is not null)
                    LogUtil.Warning($"MilitaryWindowRegistry: Units window already registered by '{_unitsRegistrant ?? "unknown"}', being overwritten by '{modName ?? "unknown"}'.");
                _unitsFactory = factory;
                _unitsRegistrant = modName;
                break;
            case MilitaryWindowSlot.Squads:
                if (_squadsFactory is not null)
                    LogUtil.Warning($"MilitaryWindowRegistry: Squads window already registered by '{_squadsRegistrant ?? "unknown"}', being overwritten by '{modName ?? "unknown"}'.");
                _squadsFactory = factory;
                _squadsRegistrant = modName;
                break;
            case MilitaryWindowSlot.FireSupport:
                if (_fireSupportFactory is not null)
                    LogUtil.Warning($"MilitaryWindowRegistry: FireSupport window already registered by '{_fireSupportRegistrant ?? "unknown"}', being overwritten by '{modName ?? "unknown"}'.");
                _fireSupportFactory = factory;
                _fireSupportRegistrant = modName;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(slot), slot, null);
        }
    }

    public static void Unregister(MilitaryWindowSlot slot)
    {
        switch (slot)
        {
            case MilitaryWindowSlot.Units:
                _unitsFactory = null;
                _unitsRegistrant = null;
                break;
            case MilitaryWindowSlot.Squads:
                _squadsFactory = null;
                _squadsRegistrant = null;
                break;
            case MilitaryWindowSlot.FireSupport:
                _fireSupportFactory = null;
                _fireSupportRegistrant = null;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(slot), slot, null);
        }
    }

    public static void ClearAll()
    {
        _unitsFactory = null;
        _squadsFactory = null;
        _fireSupportFactory = null;
        _unitsRegistrant = null;
        _squadsRegistrant = null;
        _fireSupportRegistrant = null;
    }

    /* Creation */

    public static MilitaryWindow Create(MilitaryWindowSlot slot, MilitaryFC mfc, FactionFC faction)
    {
        return slot switch
        {
            MilitaryWindowSlot.Units => _unitsFactory is not null
                ? _unitsFactory(mfc, faction)
                : new DesignUnitsWindow(mfc, faction),
            MilitaryWindowSlot.Squads => _squadsFactory is not null
                ? _squadsFactory(mfc, faction)
                : new DesignSquadsWindow(mfc),
            MilitaryWindowSlot.FireSupport => _fireSupportFactory is not null
                ? _fireSupportFactory(mfc, faction)
                : new FireSupportWindow(mfc),
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
        };
    }

    /* Shorthand creation */

    public static MilitaryWindow CreateUnits(MilitaryFC mfc, FactionFC faction)
        => Create(MilitaryWindowSlot.Units, mfc, faction);

    public static MilitaryWindow CreateSquads(MilitaryFC mfc, FactionFC faction)
        => Create(MilitaryWindowSlot.Squads, mfc, faction);

    public static MilitaryWindow CreateFireSupport(MilitaryFC mfc, FactionFC faction)
        => Create(MilitaryWindowSlot.FireSupport, mfc, faction);
}
