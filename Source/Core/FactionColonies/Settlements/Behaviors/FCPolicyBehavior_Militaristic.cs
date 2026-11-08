namespace FactionColonies;

public class FCPolicyBehavior_Militaristic : FCCooldownPolicyBehavior
{
    protected override string CooldownSaveKey => "extraSquadCooldown";

    public override void PostInitialize()
    {
        var ext = Ext<FCPolicyBehaviorExt_Militaristic>();
        cooldown.SetCooldown(GenDate.TicksPerDay * ext.extraSquadCooldownDays);
    }

    public override void OnEnacted(FactionFC faction)
    {
        foreach (WorldSettlementFC settlement in faction.settlements)
        {
            TryPlaceBuilding(settlement);
        }
    }

    public override void OnSettlementCreated(FactionFC faction, WorldSettlementFC settlement)
    {
        TryPlaceBuilding(settlement);
    }

    private void TryPlaceBuilding(WorldSettlementFC settlement)
    {
        WorldObjectComp_SettlementBuildings buildingsComp = settlement.BuildingsComp;
        if (buildingsComp is null) return;

        BuildingFCDef building = Ext<FCPolicyBehaviorExt_Militaristic>().autoPlaceBuilding;
        if (building is null) return;
        if (!building.CanBeBuiltForSettlementType(settlement.settlementDef)) return;
        if (buildingsComp.HasBuilding(building)) return;

        int slots = buildingsComp.NumBuildingSlots;
        for (int i = 0; i < slots; i++)
        {
            if (buildingsComp.BuildingSlotIsEmpty(i))
            {
                settlement.ConstructBuilding(building, i);
                return;
            }
        }
    }

    public override void OnSquadDeployed(FactionFC faction, MilitaryOperation op, WorldSettlementFC settlement, bool isExtraSquad)
    {
        if (isExtraSquad)
            cooldown.Use();
    }

    public override IEnumerable<FloatMenuOption> GetExtraDeploymentOptions(
        FactionFC faction, WorldSettlementFC settlement, WorldObjectComp_SettlementMilitary milComp)
    {
        if (!cooldown.IsReady)
        {
            Messages.Message("FCXDaysToRedeploy".Translate(
                Math.Round(cooldown.DaysRemaining, 1)), MessageTypeDefOf.RejectInput);
            yield break;
        }

        // Use any stationed squad's outfit as the cost basis for the extra squad — the
        // copy uses CallinExtraForces which itself reads the primary stationed squad.
        MilSquadFC referenceOutfit = settlement?.PrimaryStationedSquad?.outfit;
        if (referenceOutfit is null)
            yield break;

        var ext = Ext<FCPolicyBehaviorExt_Militaristic>();
        int cost = (int)Math.Round(referenceOutfit.UpdateEquipmentTotalCost() * ext.extraSquadCostFraction);
        yield return new FloatMenuOption("FCDeploySecondarySquad".Translate(cost), delegate
        {
            if (PaymentUtil.GetSilver() >= cost)
            {
                List<FloatMenuOption> deploymentOptions =
                [
                    new FloatMenuOption("FCWalkIntoMapDeploymentOption".Translate(), delegate
                    {
                        MilitaryDeploymentUtil.CallinExtraForces(settlement, false, cost);
                        Find.WindowStack.currentlyDrawnWindow.Close();
                    })
                ];

                if (!FCSettings.medievalTechOnly &&
                    (FactionCache.TechTransportPods?.IsFinished ?? false))
                {
                    deploymentOptions.Add(new FloatMenuOption("FCDropPodDeploymentOption".Translate(), delegate
                    {
                        MilitaryDeploymentUtil.CallinExtraForces(settlement, true, cost);
                        Find.WindowStack.currentlyDrawnWindow.Close();
                    }));
                }

                Find.WindowStack.Add(new FloatMenu(deploymentOptions));
            }
            else
            {
                Messages.Message("FCNotEnoughSilverToDeploySquad".Translate(), MessageTypeDefOf.RejectInput);
            }
        });
    }
}
