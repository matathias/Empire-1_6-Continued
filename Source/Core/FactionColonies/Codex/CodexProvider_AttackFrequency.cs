using System;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Dynamic provider that shows attack frequency scaling
    /// based on current settlement count.
    /// </summary>
    public class CodexProvider_AttackFrequency : ICodexDynamicProvider
    {
        public string GetDynamicContent(FactionFC faction)
        {
            int count = faction.settlements.Count;
            IntRange range = FCSettings.minMaxDaysTillMilitaryAction;

            string result = "FCCodexFreqParams".Translate() + "\n\n";
            result += "FCCodexFreqRange".Translate(range.min, range.max) + "\n";
            result += "FCCodexFreqCount".Translate(count) + "\n\n";

            // Show frequency curve points, read straight from the live curve so this
            // never drifts when the curve is rebalanced.
            result += "FCCodexFreqCurve".Translate() + "\n";
            bool currentShown = false;
            foreach (CurvePoint point in ThreatScalingUtil.FrequencyCurve)
            {
                int settlements = (int)point.x;
                bool isCurrent = settlements == count;
                if (isCurrent) currentShown = true;
                string marker = isCurrent ? " <--" : "";
                result += "  " + "FCCodexFreqPoint".Translate(settlements, Math.Round(point.y, 2)) + marker + "\n";
            }

            // Ensure the "you are here" row always renders, even when the current
            // settlement count falls between the sampled curve points.
            if (!currentShown)
            {
                double currentFreq = ThreatScalingUtil.FrequencyCurve.Evaluate(count);
                result += "  " + "FCCodexFreqPoint".Translate(count, Math.Round(currentFreq, 2)) + " <--\n";
            }

            return result;
        }
    }
}
