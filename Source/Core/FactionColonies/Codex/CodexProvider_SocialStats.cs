using System;
using System.Linq;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Dynamic provider listing each settlement's live social stats
    /// (happiness / loyalty / unrest) and whether morale has locked offensive deploys.
    /// </summary>
    public class CodexProvider_SocialStats : ICodexDynamicProvider
    {
        public string GetDynamicContent(FactionFC faction)
        {
            if (!faction.settlements.Any())
                return "FCCodexSocialNoSettlements".Translate();

            string result = "FCCodexSocialHeader".Translate() + "\n\n";
            foreach (WorldSettlementFC s in faction.settlements)
            {
                result += s.Name + ":\n";
                result += "  " + "FCCodexSocialHappiness".Translate(Math.Round(s.happiness, 1)) + "\n";
                result += "  " + "FCCodexSocialLoyalty".Translate(Math.Round(s.loyalty, 1)) + "\n";
                result += "  " + "FCCodexSocialUnrest".Translate(Math.Round(s.unrest, 1)) + "\n";
                result += "  " + "FCCodexSocialProsperity".Translate(Math.Round(s.prosperity, 1)) + "\n";
                if (s.TryGetSquadDeploymentBlock(out string _))
                    result += "  " + "FCCodexSocialDeployLocked".Translate() + "\n";
                result += "\n";
            }
            return result.TrimEnd();
        }
    }
}
