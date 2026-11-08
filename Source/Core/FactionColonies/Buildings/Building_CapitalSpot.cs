using UnityEngine;

namespace FactionColonies;

public class Building_CapitalSpot : Building_ExclusiveSpot<Building_CapitalSpot>
{
    private PlanetTile lastKnownTile = PlanetTile.Invalid; // Track the last known tile location

    public bool IsActiveCapitalSpot
    {
        get => isActive;
        set => SetActive(value);
    }

    protected override string ActiveSaveKey => "isActiveCapitalSpot";

    protected override string GizmoLabel => "FCCapitalSpotGizmoLabel".Translate(FindFC.EmpireTitle.CapitalizeFirst());

    protected override string GizmoDesc => isActive
        ? "FCCapitalSpotGizmoDescActive".Translate(FindFC.EmpireName)
        : "FCCapitalSpotGizmoDescInactive".Translate(FindFC.EmpireName);

    protected override Texture2D GizmoIcon => TexLoad.iconCustomize; // Using existing customize icon

    protected override string InspectStatus => isActive
        ? "FCCapitalSpotInspectActive".Translate(FindFC.EmpireTitle.CapitalizeFirst())
        : "FCCapitalSpotInspectInactive".Translate();

    // Set this location as the empire capital
    protected override void OnActivated() => SetAsEmpireCapital();

    // Turning the active spot off removes its capital, the same as destroying it.
    protected override void OnDeactivated() => ClearEmpireCapital();

    protected override void OnToggledByPlayer(bool wasActive)
    {
        LogUtil.Message($"Capital Building: Toggle from {wasActive} to {isActive}");

        if (isActive)
        {
            Messages.Message(
                "FCCapitalEstablished".Translate(Map.Parent.LabelCap, FindFC.EmpireTitle.CapitalizeFirst(), FindFC.EmpireTitle),
                MessageTypeDefOf.PositiveEvent
            );
        }
        else
        {
            Messages.Message(
                "FCCapitalSeatDisabled".Translate(FindFC.EmpireTitle.CapitalizeFirst()),
                MessageTypeDefOf.NeutralEvent
            );
        }
    }

    private void SetAsEmpireCapital()
    {
        FactionFC faction = FindFC.FactionComp;
        if (faction is not null && Map is not null)
        {
            PlanetTile newTile = Map.Parent.Tile;
            faction.capitalLocation = newTile;
            lastKnownTile = newTile;

            LogUtil.Message($"Capital Building: Set Empire capital to tile {newTile}");

            // Warn if no allowed pack animal can reach this new capital's biome.
            AnimalBiomeUtil.WarnIfDeliveryBiomeUncovered(Map.Biome, "FCAnimalBiomeLetterDescCapital");
        }
        else
        {
            LogUtil.Error($"Capital Building: Failed to set capital - faction={faction is not null}, Map={Map is not null}");
        }
    }

    private void ClearEmpireCapital()
    {
        FactionFC faction = FindFC.FactionComp;
        if (faction is null) return;
        // Only clear the capital if it still points at this spot's tile.
        PlanetTile spotTile = Map?.Parent?.Tile ?? lastKnownTile;
        if (spotTile.Valid && faction.capitalLocation == spotTile)
        {
            faction.capitalLocation = PlanetTile.Invalid;
            LogUtil.Message($"Capital Building: Cleared Empire capital at tile {spotTile}");
        }
    }

    // Check if the map tile has changed and update capital location accordingly
    private void UpdateCapitalLocationIfMoved()
    {
        if (isActive && Map is not null)
        {
            PlanetTile currentTile = Map.Parent.Tile;

            // Initialize lastKnownTile if it's not set (shouldn't happen but just in case)
            if (!lastKnownTile.Valid)
            {
                lastKnownTile = currentTile;
                LogUtil.Message($"Capital Spot Debug: Initialized lastKnownTile to {currentTile}");
            }

            LogUtil.Message($"Capital Spot Debug: Active={isActive}, CurrentTile={currentTile}, LastKnown={lastKnownTile}");

            if (lastKnownTile != currentTile)
            {
                // The gravship has moved! Update the capital location
                FactionFC faction = FindFC.FactionComp;
                if (faction is not null)
                {
                    PlanetTile oldCapital = faction.capitalLocation;
                    faction.capitalLocation = currentTile;
                    lastKnownTile = currentTile;

                    LogUtil.Message($"Empire capital location updated from {oldCapital} to {currentTile} (gravship moved)");

                    Find.LetterStack.ReceiveLetter(
                        "FCCapitalRelocatedLabel".Translate(FindFC.EmpireTitle.CapitalizeFirst()),
                        "FCCapitalRelocatedDesc".Translate(FindFC.EmpireName),
                        LetterDefOf.NeutralEvent
                    );

                    // Warn if no allowed pack animal can reach the relocated capital's biome.
                    AnimalBiomeUtil.WarnIfDeliveryBiomeUncovered(Map.Biome, "FCAnimalBiomeLetterDescCapital");
                }
                else
                {
                    LogUtil.Error("Capital Spot Debug: FactionFC component not found!");
                }
            }
        }
        else if (isActive)
        {
            LogUtil.Warning($"Capital Spot Debug: Active but Map is null!");
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref lastKnownTile, "lastKnownTile", PlanetTile.Invalid);

        // After loading, if this is the active capital spot but lastKnownTile is uninitialized, set it
        if (Scribe.mode == LoadSaveMode.PostLoadInit && isActive && !lastKnownTile.Valid && Map is not null)
        {
            lastKnownTile = Map.Parent.Tile;
        }
    }

    public override void TickRare()
    {
        base.TickRare();

        // TickRare runs every 250 ticks automatically, perfect for our needs
        UpdateCapitalLocationIfMoved();
    }

    public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
    {
        // If this was the active capital spot and it's being destroyed, clear the capital.
        // Skip on WillReplace (gravship launch repositions this same instance) so the capital
        // survives the flight; TickRare re-syncs the tile after landing.
        if (isActive && mode != DestroyMode.WillReplace)
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction is not null)
            {
                faction.capitalLocation = PlanetTile.Invalid;
                Messages.Message(
                    "FCCapitalLost".Translate(FindFC.EmpireTitle.CapitalizeFirst()),
                    MessageTypeDefOf.NegativeEvent
                );
            }
        }
        base.DeSpawn(mode);
    }
}

