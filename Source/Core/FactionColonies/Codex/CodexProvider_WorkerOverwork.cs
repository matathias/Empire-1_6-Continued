using System;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Dynamic provider that shows the worker overwork penalty formula
    /// with the current worker cost setting.
    /// </summary>
    public class CodexProvider_WorkerOverwork : ICodexDynamicProvider
    {
        public string GetDynamicContent(FactionFC faction)
        {
            int baseCost = FCSettings.workerCost;

            string result = "FCCodexWorkerParams".Translate() + "\n\n";
            result += "FCCodexWorkerBaseCost".Translate(baseCost) + "\n";
            result += "FCCodexWorkerFormula".Translate() + "\n";
            result += "FCCodexWorkerFormulaDesc".Translate() + "\n\n";

            // Show example: 5 workers over soft cap
            int exampleOverwork = 5;
            double penalty = 1.0 + (double)exampleOverwork / 20.0;
            double totalCost = baseCost * penalty;

            result += "FCCodexWorkerExample".Translate(
                exampleOverwork,
                Math.Round(penalty * 100 - 100, 0),
                Math.Round(totalCost, 0));

            return result;
        }
    }
}
