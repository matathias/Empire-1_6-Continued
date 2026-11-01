using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Dynamic provider listing the faction's currently selected policies, traits, and edicts.
    /// </summary>
    public class CodexProvider_PoliciesEdicts : ICodexDynamicProvider
    {
        public string GetDynamicContent(FactionFC faction)
        {
            PolicyManager pm = FindFC.PolicyManager;
            if (pm is null) return null;

            string result = "FCCodexPolicyPolicies".Translate() + "\n";
            result += FormatList(pm.policies) + "\n\n";
            result += "FCCodexPolicyTraits".Translate() + "\n";
            result += FormatList(pm.factionTraits) + "\n\n";
            result += "FCCodexPolicyEdicts".Translate() + "\n";
            result += FormatList(new List<FCPolicy>(pm.edicts.Values));
            return result;
        }

        private static string FormatList(List<FCPolicy> list)
        {
            if (list == null) return "  " + "FCCodexPolicyNone".Translate();
            string s = "";
            foreach (FCPolicy p in list)
            {
                if (p?.def is null || p.def == FCPolicyDefOf.empty) continue;
                s += "  " + p.def.LabelCap + "\n";
            }
            if (s.Length == 0) return "  " + "FCCodexPolicyNone".Translate();
            return s.TrimEnd();
        }
    }
}
