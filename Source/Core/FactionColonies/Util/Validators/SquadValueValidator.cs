using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Built-in <see cref="ISquadAssignmentValidator"/>. Rejects assignment when the squad's
    /// <see cref="SquadCostExtensions.DeploymentCost"/> exceeds the target settlement's max
    /// deploy cost.
    /// </summary>
    public class SquadValueValidator : ISquadAssignmentValidator
    {
        public bool CanAssign(WorldSettlementFC settlement, MercenarySquadFC squad, out string reason)
        {
            reason = null;
            if (settlement is null || squad is null) return true;
            if (squad.settlement == settlement) return true;

            if (MilitaryFC.SquadExceedsSettlementBudget(squad, settlement,
                out int squadDeploy, out int maxDeploy))
            {
                reason = "FCSquadValueExceeded".Translate(settlement.Name, squadDeploy, maxDeploy);
                return false;
            }
            return true;
        }
    }
}
