using RimWorld;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Dynamic provider showing the Empire faction's current tech level, the research that
    /// unlocks the next tier, and whether tech is pinned to the player's own tech level.
    /// </summary>
    public class CodexProvider_TechProgression : ICodexDynamicProvider
    {
        public string GetDynamicContent(FactionFC faction)
        {
            string result = "FCCodexTechCurrent".Translate(faction.techLevel.ToStringHuman().CapitalizeFirst());
            if (FCSettings.mirrorPlayerTechLevel)
                result += "\n\n" + "FCCodexTechMirrorOn".Translate();
            else
                result += "\n\n" + "FCCodexTechNext".Translate(faction.ReturnNextTechToLevel());
            return result;
        }
    }
}
