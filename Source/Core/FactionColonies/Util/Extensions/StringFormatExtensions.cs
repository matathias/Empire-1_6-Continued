using System;
using Verse;

namespace FactionColonies.util
{
    /// <summary>
    /// String extensions for player-facing text formatting.
    /// <para><see cref="Bold"/>/<see cref="Italic"/> wrap code-authored window text in rich-text tags.
    /// <see cref="Format"/> resolves Def-authored markup at render time: faction tokens
    /// ({FACTION}, {FACTION_TITLE}) and BBCode-style emphasis ([b]/[i]). Emphasis must be authored as
    /// [b]/[i] rather than raw &lt;b&gt;/&lt;i&gt; because RimWorld's Def XML parser would treat the
    /// angle-bracket tags as child nodes (see DirectXmlToObject) and mangle the field.</para>
    /// </summary>
    public static class StringFormatExtensions
    {
        /* Code-side emphasis wrappers -- emit rich text directly for window text. Null-safe. */
        public static string Bold(this string s) => s.NullOrEmpty() ? s : "<b>" + s + "</b>";

        public static string Italic(this string s) => s.NullOrEmpty() ? s : "<i>" + s + "</i>";

        /// <summary>
        /// Resolves every player-facing token in Def-authored text: {FACTION}/{FACTION_TITLE}
        /// substitution and [b]/[i] emphasis markup. Null-safe. Called at render time so faction
        /// names/titles are always current.
        /// </summary>
        public static string Format(this string s)
        {
            if (s.NullOrEmpty()) return s;
            s = ResolveFactionName(s);
            s = ResolveFactionTitle(s);
            s = ResolveEmphasis(s);
            return s;
        }

        /* Per-token helpers. Each early-outs when its token is absent so markup-free text
         * (the common case) allocates nothing. */
        private static string ResolveFactionName(string s)
        {
            return s.IndexOf("{FACTION}", StringComparison.Ordinal) >= 0
                ? s.Replace("{FACTION}", FindFC.EmpireName)
                : s;
        }

        private static string ResolveFactionTitle(string s)
        {
            return s.IndexOf("{FACTION_TITLE}", StringComparison.Ordinal) >= 0
                ? s.Replace("{FACTION_TITLE}", FindFC.EmpireTitle)
                : s;
        }

        private static string ResolveEmphasis(string s)
        {
            if (s.IndexOf('[') < 0) return s;
            return s.Replace("[b]", "<b>").Replace("[/b]", "</b>")
                    .Replace("[i]", "<i>").Replace("[/i]", "</i>");
        }
    }
}
