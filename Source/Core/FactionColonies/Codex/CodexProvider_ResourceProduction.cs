using System;
using System.Linq;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Dynamic provider that shows the three-stage resource production cascade
    /// with example numbers from the first settlement that has workers assigned.
    /// </summary>
    public class CodexProvider_ResourceProduction : ICodexDynamicProvider
    {
        public string GetDynamicContent(FactionFC faction)
        {
            if (!faction.settlements.Any())
                return "FCCodexResNoSettlements".Translate();

            // Find first settlement with assigned workers on any resource
            WorldSettlementFC example = faction.settlements
                .FirstOrDefault(s => s.Resources.Any(r => r.assignedWorkers > 0));

            if (example is null)
                return "FCCodexResNoWorkers".Translate();

            // Find first resource with workers
            ResourceFC res = example.Resources.First(r => r.assignedWorkers > 0);

            double addBase = res.productionBase;
            double mult = res.productionMult;
            int workers = res.assignedWorkers;
            double totalProd = res.rawTotalProduction;
            double prosperity = example.prosperity;

            string result = "FCCodexResExample".Translate(example.Name, res.label) + "\n\n";
            result += "FCCodexResStage1".Translate(Math.Round(addBase, 2)) + "\n";
            result += "FCCodexResStage2".Translate(Math.Round(mult, 2), Math.Round(prosperity, 0)) + "\n";
            result += "FCCodexResStage3".Translate(workers, Math.Round(totalProd, 2)) + "\n\n";
            result += "FCCodexResSilver".Translate(
                FCSettings.silverPerResource,
                Math.Round(totalProd, 2));

            return result;
        }
    }
}
