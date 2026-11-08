using UnityEngine;

namespace FactionColonies;

public class Building_TaxSpot : Building_ExclusiveSpot<Building_TaxSpot>
{
    public bool IsActiveTaxDeliverySpot
    {
        get => isActive;
        set => SetActive(value);
    }

    protected override string ActiveSaveKey => "isActiveTaxDeliverySpot";

    protected override string GizmoLabel => "FCTaxSpotGizmoLabel".Translate();

    protected override string GizmoDesc => isActive
        ? "FCTaxSpotGizmoDescActive".Translate()
        : "FCTaxSpotGizmoDescInactive".Translate(FindFC.EmpireName);

    protected override Texture2D GizmoIcon => TexLoad.iconTrade; // Using existing trade icon

    protected override string InspectStatus => isActive
        ? "FCTaxSpotInspectActive".Translate()
        : "FCTaxSpotInspectInactive".Translate();

    protected override void OnActivated()
    {
        // Warn if no allowed pack animal can reach this new delivery biome.
        AnimalBiomeUtil.WarnIfDeliveryBiomeUncovered(Map?.Biome, "FCAnimalBiomeLetterDescTaxSpot");
    }

    protected override void OnToggledByPlayer(bool wasActive)
    {
        if (isActive)
        {
            Messages.Message(
                "FCTaxSpotEnabledMessage".Translate(),
                MessageTypeDefOf.PositiveEvent
            );
        }
        else
        {
            Messages.Message(
                "FCTaxSpotDisabledMessage".Translate(),
                MessageTypeDefOf.NeutralEvent
            );
        }
    }
}
