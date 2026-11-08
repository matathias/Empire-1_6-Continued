using System.Text;

namespace FactionColonies;

public static class FoundingValidatorRegistry
{
    private static readonly RegistryList<ISettlementFoundingValidator> _list = new();
    private static readonly List<string> _emptyDescriptions = [];

    internal static void Register(ISettlementFoundingValidator validator) => _list.Register(validator);
    internal static void Unregister(ISettlementFoundingValidator validator) => _list.Unregister(validator);
    internal static void ClearAll() => _list.ClearAll();
    public static IReadOnlyList<ISettlementFoundingValidator> Validators => _list.Items;

    /// <summary>
    /// Returns true if all registered validators allow settlement founding.
    /// Appends rejection reasons to <paramref name="reason"/>. Not short-circuiting:
    /// every validator runs so all rejection reasons accumulate.
    /// </summary>
    public static bool CanFound(PlanetTile tile, WorldSettlementDef type, StringBuilder reason, float costMultiplier = 1f)
    {
        bool allowed = true;
        RegistryDispatch.Each(_list.Items, validator =>
        {
            if (!validator.CanFoundSettlement(tile, type, out string r, costMultiplier))
            {
                if (reason is not null && !r.NullOrEmpty())
                {
                    if (reason.Length > 0) reason.AppendLine();
                    reason.Append(r);
                }
                allowed = false;
            }
        }, nameof(ISettlementFoundingValidator.CanFoundSettlement));
        return allowed;
    }

    /// <summary>
    /// Collects all non-null additional cost descriptions from registered validators.
    /// </summary>
    public static List<string> GetCostDescriptions(PlanetTile tile, WorldSettlementDef type, float costMultiplier = 1f)
    {
        if (_list.Count == 0) return _emptyDescriptions;
        List<string> descriptions = [];
        RegistryDispatch.Each(_list.Items, validator =>
        {
            string desc = validator.GetAdditionalCostDescription(tile, type, costMultiplier);
            if (!desc.NullOrEmpty()) descriptions.Add(desc);
        }, nameof(ISettlementFoundingValidator.GetAdditionalCostDescription));
        return descriptions;
    }

    /// <summary>
    /// Notifies all validators that a settlement was successfully submitted for founding.
    /// Called after silver payment in DoFoundSettlement().
    /// </summary>
    public static void NotifyFounded(PlanetTile tile, WorldSettlementDef type, float costMultiplier = 1f)
        => RegistryDispatch.Each(_list.Items,
            v => v.OnSettlementFounded(tile, type, costMultiplier),
            nameof(ISettlementFoundingValidator.OnSettlementFounded));

    /// <summary>
    /// Notifies all validators that a founding reported through <see cref="NotifyFounded"/> failed
    /// before its settlement was created, so they can undo its side effects.
    /// </summary>
    public static void NotifyFoundingCancelled(PlanetTile tile, WorldSettlementDef type)
        => RegistryDispatch.Each(_list.Items,
            v => v.OnFoundingCancelled(tile, type),
            nameof(ISettlementFoundingValidator.OnFoundingCancelled));
}
