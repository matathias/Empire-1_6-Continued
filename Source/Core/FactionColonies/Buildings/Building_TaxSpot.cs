using RimWorld;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    public class Building_TaxSpot : Building
    {
        private bool isActiveTaxDeliverySpot = false;

        public bool IsActiveTaxDeliverySpot
        {
            get => isActiveTaxDeliverySpot;
            set
            {
                if (value && isActiveTaxDeliverySpot != value)
                {
                    // Disable other tax spots when enabling this one
                    DisableOtherTaxSpots();
                    // Warn if no allowed pack animal can reach this new delivery biome.
                    AnimalBiomeUtil.WarnIfDeliveryBiomeUncovered(Map?.Biome, "FCAnimalBiomeLetterDescTaxSpot");
                }
                isActiveTaxDeliverySpot = value;
            }
        }

        private void DisableOtherTaxSpots()
        {
            // Find all other tax spots and disable their delivery function
            foreach (Map map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;

                foreach (Building building in map.listerBuildings.allBuildingsColonist)
                {
                    if (building is Building_TaxSpot otherTaxSpot && otherTaxSpot != this)
                    {
                        otherTaxSpot.isActiveTaxDeliverySpot = false;
                    }
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref isActiveTaxDeliverySpot, "isActiveTaxDeliverySpot", false);
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
                    defaultLabel = "FCTaxSpotGizmoLabel".Translate(),
                    defaultDesc = isActiveTaxDeliverySpot
                        ? "FCTaxSpotGizmoDescActive".Translate()
                        : "FCTaxSpotGizmoDescInactive".Translate(FindFC.EmpireName),
                    icon = TexLoad.iconTrade, // Using existing trade icon
                    isActive = () => isActiveTaxDeliverySpot,
                    toggleAction = () =>
                    {
                        IsActiveTaxDeliverySpot = !IsActiveTaxDeliverySpot;
                        if (IsActiveTaxDeliverySpot)
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
                };
            }
        }

        public override string GetInspectString()
        {
            string baseString = base.GetInspectString();
            string statusString = isActiveTaxDeliverySpot
                ? "FCTaxSpotInspectActive".Translate()
                : "FCTaxSpotInspectInactive".Translate();

            return string.IsNullOrEmpty(baseString)
                ? statusString
                : baseString + "\n" + statusString;
        }
    }
}

