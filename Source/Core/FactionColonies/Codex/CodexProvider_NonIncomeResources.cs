using System;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Dynamic provider for the Non-Income Resources codex entry. Shows the faction's
    /// current Research pool total and Power output, read live from the faction-wide
    /// resource pools. Research and Power are pool resources, so their accumulated values
    /// live on FactionFC rather than per settlement.
    ///
    /// Each line is only emitted once its pool actually exists (pools are created at the
    /// first tax collection), so opening the codex on a fresh game shows a friendly
    /// placeholder instead of triggering GetResourcePoolValue's "no pool" warning.
    /// </summary>
    public class CodexProvider_NonIncomeResources : ICodexDynamicProvider
    {
        public string GetDynamicContent(FactionFC faction)
        {
            if (faction is null || faction.resourcePools is null) return null;

            string lines = "";

            if (faction.resourcePools.Exists(p => p.resource == ResourceTypeDefOf.RTD_Research))
                lines += "FCCodexNonIncomeResearchPool".Translate(
                    Math.Round(faction.GetResourcePoolValue(ResourceTypeDefOf.RTD_Research))) + "\n";

            if (faction.resourcePools.Exists(p => p.resource == ResourceTypeDefOf.RTD_Power))
                lines += "FCCodexNonIncomePowerOutput".Translate(
                    Math.Round(faction.GetResourcePoolValue(ResourceTypeDefOf.RTD_Power))) + "\n";

            if (lines.Length == 0)
                return "FCCodexNonIncomeNoPools".Translate();

            return "FCCodexNonIncomeHeader".Translate() + "\n\n" + lines.TrimEnd();
        }
    }
}
