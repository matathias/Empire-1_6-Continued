using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    public class Building_CapitalSpot : Building
    {
        private bool isActiveCapitalSpot = false;
        private PlanetTile lastKnownTile = PlanetTile.Invalid; // Track the last known tile location

        public bool IsActiveCapitalSpot
        {
            get => isActiveCapitalSpot;
            set
            {
                if (value && isActiveCapitalSpot != value)
                {
                    // Disable other capital spots when enabling this one
                    DisableOtherCapitalSpots();
                    // Set this location as the empire capital
                    SetAsEmpireCapital();
                }
                isActiveCapitalSpot = value;
            }
        }

        private void DisableOtherCapitalSpots()
        {
            // Find all other capital spots and disable them
            foreach (Map map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;

                foreach (Building building in map.listerBuildings.allBuildingsColonist)
                {
                    if (building is Building_CapitalSpot otherCapitalSpot && otherCapitalSpot != this)
                    {
                        otherCapitalSpot.isActiveCapitalSpot = false;
                    }
                }
            }
        }

        private void SetAsEmpireCapital()
        {
            FactionFC faction = FindFC.FactionComp;
            if (faction != null && Map != null)
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
                LogUtil.Error($"Capital Building: Failed to set capital - faction={faction != null}, Map={Map != null}");
            }
        }

        // Check if the map tile has changed and update capital location accordingly
        private void UpdateCapitalLocationIfMoved()
        {
            if (isActiveCapitalSpot && Map != null)
            {
                PlanetTile currentTile = Map.Parent.Tile;

                // Initialize lastKnownTile if it's not set (shouldn't happen but just in case)
                if (!lastKnownTile.Valid)
                {
                    lastKnownTile = currentTile;
                    LogUtil.Message($"Capital Spot Debug: Initialized lastKnownTile to {currentTile}");
                }

                LogUtil.Message($"Capital Spot Debug: Active={isActiveCapitalSpot}, CurrentTile={currentTile}, LastKnown={lastKnownTile}");

                if (lastKnownTile != currentTile)
                {
                    // The gravship has moved! Update the capital location
                    FactionFC faction = FindFC.FactionComp;
                    if (faction != null)
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
            else if (isActiveCapitalSpot)
            {
                LogUtil.Warning($"Capital Spot Debug: Active but Map is null!");
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref isActiveCapitalSpot, "isActiveCapitalSpot", false);
            Scribe_Values.Look(ref lastKnownTile, "lastKnownTile", PlanetTile.Invalid);

            // After loading, if this is the active capital spot but lastKnownTile is uninitialized, set it
            if (Scribe.mode == LoadSaveMode.PostLoadInit && isActiveCapitalSpot && !lastKnownTile.Valid && Map != null)
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

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (Faction.OfPlayer.IsPlayer)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "FCCapitalSpotGizmoLabel".Translate(FindFC.EmpireTitle.CapitalizeFirst()),
                    defaultDesc = isActiveCapitalSpot
                        ? "FCCapitalSpotGizmoDescActive".Translate(FindFC.EmpireName)
                        : "FCCapitalSpotGizmoDescInactive".Translate(FindFC.EmpireName),
                    icon = TexLoad.iconCustomize, // Using existing customize icon
                    isActive = () => isActiveCapitalSpot,
                    toggleAction = () =>
                    {
                        bool wasActive = IsActiveCapitalSpot;
                        IsActiveCapitalSpot = !IsActiveCapitalSpot;

                        LogUtil.Message($"Capital Building: Toggle from {wasActive} to {IsActiveCapitalSpot}");

                        if (IsActiveCapitalSpot)
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
                };
            }
        }

        public override string GetInspectString()
        {
            string baseString = base.GetInspectString();
            string statusString = isActiveCapitalSpot
                ? "FCCapitalSpotInspectActive".Translate(FindFC.EmpireTitle.CapitalizeFirst())
                : "FCCapitalSpotInspectInactive".Translate();

            return string.IsNullOrEmpty(baseString)
                ? statusString
                : baseString + "\n" + statusString;
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            // If this was the active capital spot and it's being destroyed, clear the capital.
            // Skip on WillReplace (gravship launch repositions this same instance) so the capital
            // survives the flight; TickRare re-syncs the tile after landing.
            if (isActiveCapitalSpot && mode != DestroyMode.WillReplace)
            {
                FactionFC faction = FindFC.FactionComp;
                if (faction != null)
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
}

