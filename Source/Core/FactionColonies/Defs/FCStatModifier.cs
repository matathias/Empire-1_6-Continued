using System;
using System.Collections.Generic;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// A single stat modifier entry. Appears in lists on FCPolicyDef, BuildingFCDef, FCEventDef, etc.
    /// </summary>
    public class FCStatModifier
    {
        public FCStatDef stat;
        public double value;

        /// <summary>
        /// Yields ConfigError strings for any stat modifier with a null stat reference (unresolved defName in XML).
        /// </summary>
        public static IEnumerable<string> ConfigErrors(List<FCStatModifier> modifiers, string ownerDefName)
        {
            if (modifiers == null) yield break;
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i].stat == null)
                    yield return $"{ownerDefName}: statModifiers[{i}] has null stat (unresolved defName?)";
            }
        }

        /// <summary>
        /// Returns true if this modifier represents a beneficial effect.
        /// Accounts for inverted stats where lower values are better.
        /// </summary>
        public bool IsBeneficial()
        {
            if (stat == null) return false;
            if (stat.aggregation == FCStatAggregation.Multiplicative)
                return stat.invertedForDisplay ? value < 1.0 : value > 1.0;
            return stat.invertedForDisplay ? value < 0.0 : value > 0.0;
        }

        public double DisplayValue => stat.displayDivisor > 0 ? Math.Round(value / stat.displayDivisor, 1) : value;

        /// <summary>
        /// Builds a human-readable description string from a list of stat modifiers.
        /// Resource-linked stats use resource-specific translation keys; other stats use descriptionKey.
        /// </summary>
        public static TaggedString GetDescription(List<FCStatModifier> modifiers)
        {
            TaggedString desc = "";
            if (modifiers?.Count > 0)
            {
                foreach (FCStatModifier mod in modifiers)
                {
                    if (mod.stat == null) continue;

                    if (mod.stat.linkedResource != null)
                    {
                        if (mod.stat.aggregation == FCStatAggregation.Additive)
                            desc += "FCRTDproductionAdditive".Translate(TextUtil.ColorizeAdditiveBonus(mod.value), mod.stat.linkedResource.LabelCap) + "\n";
                        else
                            desc += "FCRTDproductionMultiplier".Translate(TextUtil.ColorizeMultiplierBonus(mod.value), mod.stat.linkedResource.LabelCap) + "\n";
                    }
                    else
                    {
                        if (mod.stat.descriptionKey.NullOrEmpty()) continue;
                        double displayValue = mod.DisplayValue;
                        if (mod.stat.aggregation == FCStatAggregation.Additive)
                            desc += mod.stat.descriptionKey.Translate(TextUtil.ColorizeAdditiveBonus(displayValue, mod.stat.invertedForDisplay)) + "\n";
                        else
                            desc += mod.stat.descriptionKey.Translate(TextUtil.ColorizeMultiplierBonus(displayValue, mod.stat.invertedForDisplay)) + "\n";
                    }
                }
            }
            return desc.Trim();
        }
    }
}