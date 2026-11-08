using UnityEngine;

namespace FactionColonies;

/// <summary>
/// Modal picker that lists the player-controllable mechanoids the player has researched
/// (<see cref="FactionCache.UnlockedControllableMechKinds"/>) that a mechanitor merc can be
/// assigned. Each row shows the mech's bandwidth cost and silver cost.
/// Adding a mech routes through <see cref="MilUnitFC.AddMech"/>, which hard-blocks the add when it
/// would exceed the unit's available bandwidth. Stays open so several mechs can be queued; a live
/// "used / total bandwidth" header reflects each add (every add regenerates the preview pawn used
/// to read the bandwidth stat). Biotech-only — the Mechs tab only opens it when Biotech is active.
/// </summary>
public class FCWindow_MechPicker : FCWindow_SearchablePicker<PawnKindDef>
{
    private readonly Func<MilUnitFC> getDisplayUnit;
    private readonly Func<MilUnitFC> getEditTarget;
    private readonly int group;

    public override Vector2 InitialSize => new(480f, 600f);

    public FCWindow_MechPicker(Func<MilUnitFC> getDisplayUnit, Func<MilUnitFC> getEditTarget, int group)
    {
        this.getDisplayUnit = getDisplayUnit;
        this.getEditTarget = getEditTarget;
        this.group = group;
    }

    // Names the group the picked mech will be added to.
    protected override string Title => "fcPickMech".Translate() + " — " + "fcMechGroup".Translate(group + 1);
    protected override string EmptyText => "fcNoMechsAvailable".Translate();
    protected override bool PicksOnClick => true;

    // Bandwidth header: "used / total"
    protected override bool DrawHeader(Rect rect)
    {
        MilUnitFC displayUnit = getDisplayUnit?.Invoke();
        float used = displayUnit?.UsedMechBandwidth ?? 0f;
        float total = displayUnit?.TotalMechBandwidth ?? 0f;
        if (used > total + 0.0001f) GUI.color = ColorLibrary.RedReadable;
        UIUtil.ClampedLabel(rect, "fcMechBandwidth".Translate(used.ToString("0.#"), total.ToString("0.#")));
        return true;
    }

    // Only mechs the player has actually unlocked through research.
    protected override List<PawnKindDef> Items() => FactionCache.UnlockedControllableMechKinds
        .OrderBy(m => m.label ?? m.defName, StringComparer.OrdinalIgnoreCase)
        .ToList();

    protected override string SearchText(PawnKindDef mech) => mech.label ?? mech.defName;

    protected override void DrawRow(Rect row, PawnKindDef mech)
    {
        float bandwidth = mech.race.GetStatValueAbstract(StatDefOf.BandwidthCost);
        double cost = MilUnitFC.MechUnitCost(mech);
        DrawThingRow(row, mech.race, mech.LabelCap, $"${cost:F0}", $"BW {bandwidth:0.#}", costInset: 60f);
    }

    // Adds to this picker's group; hard-blocks on bandwidth.
    protected override void Pick(PawnKindDef mech) => getEditTarget?.Invoke()?.AddMech(mech, group);
}
