using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Utility class for specialized value-to-string functions.
    /// </summary>
    public static class TextUtil
    {
        public static string FloorStat(double stat)
        {
            return Convert.ToString(Math.Floor((stat * 100)) / 100);
        }

        /// <summary>
        /// Takes an additive bonus and colorizes it: red for a negative bonus, green for a positive bonus.
        /// <para>By default, a bonus that is less than 0 is considered negative, while a bonus that is greater than 0 is considered positive. This can be reversed by passing in 'true' for the 'invert' parameter.</para>
        /// </summary>
        /// <param name="bonus">The numeric bonus to colorize</param>
        /// <param name="invert">If true, negative values are colorized as positive, and vice versa. Defaults to false</param>
        /// <param name="hardinvert">If true, the bonus is multiplied by -1 before being processed.</param>
        /// <param name="addPlusSign">If true, adds a "+" before positive values. Defaults to true</param>
        /// <returns></returns>
        public static string ColorizeAdditiveBonus(double bonus, bool invert = false, bool addPlusSign = true, bool hardinvert = false)
        {
            if (hardinvert)
            {
                bonus *= -1;
            }
            string baseBonus = Math.Round(bonus, 2).ToString();
            if (bonus > 0 && addPlusSign)
            {
                baseBonus = "+" + baseBonus;
            }

            if ((!invert && bonus < 0) || (invert && bonus > 0))
            {
                return baseBonus.Colorize(Color.red);
            }
            else
            {
                return baseBonus.Colorize(Color.green);
            }
        }
        public static string CleaveAtNewline(string input)
        {
            int newline = input.IndexOf('\n');
            if (newline == 0)
            {
                // If the first character in the string is a newline, then skip over it and return the next line of text.
                // If the newline is the only character in the string, though, then just return an empty string.
                if (input.Length > 1)
                {
                    return CleaveAtNewline(input.Substring(1, input.Length - 1));
                }
                else
                {
                    return string.Empty;
                }
            }
            if (newline > 0)
            {
                return input.Substring(0, newline);
            }
            return input;
        }
        /// <summary>
        /// Takes a multiplier bonus and colorizes it: red for a negative bonus, green for a positive bonus.
        /// <para>By default, a bonus that is less than 1 is considered negative, while a bonus that is greater than 1 is considered positive. This can be reversed by passing in 'true' for the 'invert' parameter.</para>
        /// </summary>
        /// <param name="bonus">The numeric bonus to colorize</param>
        /// <param name="invert">If true, values less than 1 are colorized as positive, and vice versa. Defaults to false</param>
        /// <param name="addXsign">If true, adds a "x" before the bonus. Defaults to true</param>
        /// <returns></returns>
        public static string ColorizeMultiplierBonus(double bonus, bool invert = false, bool addXsign = true)
        {
            string baseBonus = Math.Round(bonus, 2).ToString();
            if (addXsign)
            {
                baseBonus = "x" + baseBonus;
            }

            if ((!invert && bonus < 1) || (invert && bonus > 1))
            {
                return baseBonus.Colorize(Color.red);
            }
            else
            {
                return baseBonus.Colorize(Color.green);
            }
        }

        /// <summary>
        /// Builds a complete colored additive-bonus line of the form "&lt;color&gt;+X&lt;/color&gt;{separator}{label}".
        /// <para>The label is taken as a string so any TaggedString (Def.LabelCap, "x".Translate()) is flattened
        /// to text at the call boundary BEFORE the color is applied — this is what keeps the color alive. Building
        /// the colored value and a TaggedString in the same expression and then forcing the result back to a string
        /// (via AppendLine, a string accumulator/return, List&lt;string&gt;.Add, or "string + object") routes it
        /// through TaggedString's implicit string conversion, which calls StripTags() and silently removes the color.
        /// Returns a plain string; append it directly without mixing in more TaggedStrings.</para>
        /// </summary>
        public static string AdditiveBonusLine(double value, string label, string separator = " - ", bool invert = false, bool addPlusSign = true, bool hardinvert = false)
        {
            return ColorizeAdditiveBonus(value, invert, addPlusSign, hardinvert) + separator + label;
        }

        /// <summary>
        /// Builds a complete colored multiplier-bonus line of the form "&lt;color&gt;xX&lt;/color&gt;{separator}{label}".
        /// See <see cref="AdditiveBonusLine"/> for why the label is a string and the result must stay a plain string.
        /// </summary>
        public static string MultiplierBonusLine(double value, string label, string separator = " - ", bool invert = false, bool addXsign = true)
        {
            return ColorizeMultiplierBonus(value, invert, addXsign) + separator + label;
        }

        public static string ColorizeBonus(double bonus, double compare, bool invert = false)
        {
            string result = Math.Round(bonus, 2).ToString();
            if ((!invert && bonus > compare) || (invert && bonus < compare))
            {
                return result.Colorize(Color.green);
            }
            if ((!invert && bonus < compare) || (invert && bonus > compare))
            {
                return result.Colorize(Color.red);
            }

            return result;
        }

        public static string GetTownTitle(WorldSettlementFC settlement)
        {
            int level = settlement.settlementLevel <= 3 ? 1
                      : settlement.settlementLevel <= 6 ? 2
                      : 3;

            string resourceKey = "";
            double highest = -1;
            foreach (ResourceFC resource in settlement.Resources)
            {
                if (resource.rawTotalProduction > highest)
                {
                    highest = resource.rawTotalProduction;
                    resourceKey = resource.def.defName;
                }
            }

            string titleKey = (settlement.def as WorldSettlementDef)?.titleKey;
            if (titleKey != null)
            {
                string typeSpecificKey = "FCTitle_" + titleKey + "_" + resourceKey + "_" + level;
                if (typeSpecificKey.CanTranslate())
                    return typeSpecificKey.Translate();
            }

            return ("FCTitle_" + resourceKey + "_" + level).Translate();
        }

        public static string GetQualityLabelCap(QualityCategory? cat)
        {
            return cat is QualityCategory cat2 ? cat2.GetLabel().CapitalizeFirst() : $"({"FCSelect".Translate()})";
        }

        /* Period-average tooltip builders. The tooltip is purely an explanation of how the
         * averaged headline relates to the in-UI "Current Rate" subtitle — the values themselves
         * are visible on the UI, no point repeating them here. The "None" variant fires when no
         * samples have been taken yet (fresh settlement / just-reset post-tax). */
        public static string BuildPeriodAverageTooltip(bool hasAverage)
        {
            return hasAverage ? "FCPeriodAverageTooltipHas".Translate() : "FCPeriodAverageTooltipNone".Translate();
        }

        public static string BuildPeriodAverageFactionTooltip(bool hasAverage)
        {
            return hasAverage ? "FCPeriodAverageTooltipFaction".Translate() : "FCPeriodAverageTooltipNone".Translate();
        }

        /* Forward-looking projection tooltips for the daily-accrual model. */
        public static string BuildProjectedTooltip()
        {
            return "FCProjectedTooltip".Translate();
        }

        public static string BuildProjectedFactionTooltip()
        {
            return "FCProjectedTooltipFaction".Translate();
        }

        /// <summary>
        /// Converts the given string <paramref name="name"/> into a shorter version. The resulting string contains the first word and every uppercase char of the following words
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string ToShortName(string name)
        {
            IEnumerable<string> nameSplit = name.Split(' ').Where(str => !str.NullOrEmpty() && char.IsUpper(str[0]));

            if (nameSplit.EnumerableNullOrEmpty()) return name;

            string main = nameSplit.First();

            return nameSplit.Aggregate(main, (total, next) => total + ((main == next) ? ' ' : next[0]));
        }

        public static string FormatRange(double min, double max, string fmt)
        {
            return min == max
                ? min.ToString(fmt)
                : min.ToString(fmt) + "-" + max.ToString(fmt);
        }

        public static string GetDefModInfo(Def def)
        {
            if (def is null)
                return "???";
            
            return
                $"{def.label ?? "???"} [{def.defName ?? "??? defName"}] ({def.modContentPack?.PackageId ?? "???"}, {def.modContentPack?.Name ?? "???"})";
        }
    }
}
