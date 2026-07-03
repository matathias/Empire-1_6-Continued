using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        /// <summary>
        /// Tag-aware replacement for <see cref="Verse.Text.ClampTextWithEllipsis"/>. Truncates <paramref name="text"/>
        /// to <paramref name="rect"/>'s width (minus <paramref name="margin"/>) with a trailing "...", but cuts on
        /// visible characters only and leaves rich-text markup intact — a colorized string keeps its color and never
        /// leaks a half-eaten or unclosed <c>&lt;color&gt;</c> tag onto the screen. Vanilla cuts the raw string
        /// character-by-character, which mangles tags. Any tags still open at the cut point are closed after the ellipsis.
        /// </summary>
        /// <param name="rect">The rect the text must fit within.</param>
        /// <param name="text">The (possibly rich-text) string to clamp.</param>
        /// <param name="margin">Extra width reserved on the right, subtracted from the rect width. Defaults to 0 (use the
        /// full width); vanilla's equivalent bakes in a fixed 13px cushion.</param>
        public static string ClampWithEllipsis(Rect rect, string text, float margin = 0f)
        {
            float maxWidth = rect.width - margin;

            // CalcSize strips tags internally, so this measures the VISIBLE width. Short-circuit anything that fits
            // (including colored strings) unchanged.
            if (text.NullOrEmpty() || Text.CalcSize(text).x <= maxWidth)
            {
                return text;
            }

            // Find how many visible characters fit alongside the ellipsis, measuring on the stripped text.
            string plain = text.StripTags();
            int visibleFit = plain.Length;
            while (visibleFit > 0 && Text.CalcSize(plain.Substring(0, visibleFit) + "...").x > maxWidth)
            {
                visibleFit--;
            }

            // Rebuild the raw string up to that many visible characters, copying tags through verbatim and tracking
            // which are still open so we can close them after the ellipsis.
            List<string> openTags = new List<string>();
            StringBuilder sb = new StringBuilder(text.Length);
            int visibleEmitted = 0;
            int i = 0;
            while (i < text.Length && visibleEmitted < visibleFit)
            {
                char c = text[i];
                if (c == '<')
                {
                    int close = text.IndexOf('>', i);
                    if (close < 0)
                    {
                        // Malformed tail with no closing '>': treat the rest as a single visible chunk and bail.
                        break;
                    }
                    string tag = text.Substring(i, close - i + 1); // includes the surrounding < >
                    sb.Append(tag);
                    string inner = tag.Substring(1, tag.Length - 2); // strip < and >
                    if (inner.StartsWith("/"))
                    {
                        if (openTags.Count > 0) openTags.RemoveAt(openTags.Count - 1);
                    }
                    else if (!inner.EndsWith("/")) // ignore self-closing tags
                    {
                        // Tag name is up to the first space or '=' (e.g. "color=#FF0000FF" -> "color").
                        int cut = inner.IndexOfAny(new[] { ' ', '=' });
                        openTags.Add(cut >= 0 ? inner.Substring(0, cut) : inner);
                    }
                    i = close + 1;
                    continue;
                }

                sb.Append(c);
                visibleEmitted++;
                i++;
            }

            sb.Append("...");
            for (int t = openTags.Count - 1; t >= 0; t--)
            {
                sb.Append("</").Append(openTags[t]).Append(">");
            }
            return sb.ToString();
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
