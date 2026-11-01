using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Dynamic provider that shows the current battle simulation parameters:
    /// efficiency damping formula, defender advantage, and the live per-tech-level
    /// efficiency baselines (read from EnemyPowerTechDef so they stay correct after
    /// balance changes instead of being hard-coded in the entry prose).
    /// </summary>
    public class CodexProvider_BattleSimulation : ICodexDynamicProvider
    {
        public string GetDynamicContent(FactionFC faction)
        {
            double damping = FCSettings.efficiencyDamping;
            double defAdv = FCSettings.defenderAdvantage;

            // Example: show how damping compresses a 2.0 efficiency
            double exampleRaw = 2.0;
            double dampened = 1.0 + (exampleRaw - 1.0) * damping;

            string result = "FCCodexBattleParams".Translate() + "\n\n";
            result += "FCCodexBattleDefAdv".Translate(Math.Round(defAdv, 2), Math.Round((defAdv - 1.0) * 100, 0)) + "\n";
            result += "FCCodexBattleDamping".Translate(Math.Round(damping, 2)) + "\n\n";
            result += "FCCodexBattleDampExample".Translate(
                Math.Round(exampleRaw, 1),
                Math.Round(dampened, 2));

            // Live per-tech-level efficiency table, ordered weakest to strongest.
            // Skip the Undefined placeholder tier (not a real faction tech level).
            List<EnemyPowerTechDef> techDefs = DefDatabase<EnemyPowerTechDef>.AllDefsListForReading
                .Where(d => d.techLevel != TechLevel.Undefined)
                .OrderBy(d => d.level)
                .ToList();
            if (techDefs.Any())
            {
                result += "\n\n" + "FCCodexBattleEffHeader".Translate();
                foreach (EnemyPowerTechDef d in techDefs)
                {
                    result += "\n  " + "FCCodexBattleEffRow".Translate(
                        d.techLevel.ToStringHuman().CapitalizeFirst(),
                        Math.Round(d.efficiency, 2));
                }
            }

            return result;
        }
    }
}
