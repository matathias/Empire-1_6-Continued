namespace FactionColonies;

/// <summary>
/// Scorched Earth doctrine behavior: on defensive victory, each occupied building slot
/// has a chance of being destroyed as collateral damage.
/// </summary>
public class FCPolicyBehavior_ScorchedEarth : FCPolicyBehavior
{
    public override void OnBattleResolved(FactionFC faction, WorldSettlementFC settlement, MilitaryJobDef job, bool victory, BattleResult result)
    {
        if (!victory) return;
        if (job != MilitaryJobDefOf.DefendFriendlySettlement) return;
        if (settlement?.BuildingsComp is null) return;

        var ext = Ext<FCPolicyBehaviorExt_ScorchedEarth>();
        List<int> damaged = [];
        for (int k = 0; k < settlement.BuildingsComp.NumBuildingSlots; k++)
        {
            if (!settlement.BuildingsComp.BuildingSlotIsBuilding(k)) continue;
            if (Rand.RangeInclusive(0, ext.damageRollMax) >= ext.damageThreshold)
            {
                damaged.Add(k);
            }
        }

        if (damaged.Count == 0) return;

        // Demolish dependent buildings before the buildings they require (same order as LoseBattle)
        damaged = BuildingDemolitionUtil.DependentsFirst(settlement.BuildingsComp, damaged);

        string msg = "FCScorchedEarthCollateralHeader".Translate(settlement.Name);
        foreach (int k in damaged)
        {
            msg += "\n  - " + "FCBuildingDestroyedInRaid".Translate(settlement.BuildingsComp.BuildingLabel(k));
            settlement.DeconstructBuilding(k);
        }

        Find.LetterStack.ReceiveLetter(
            "FCScorchedEarthCollateralTitle".Translate(),
            msg,
            LetterDefOf.NegativeEvent,
            new LookTargets(settlement));
    }
}
