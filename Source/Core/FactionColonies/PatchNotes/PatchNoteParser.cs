using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FactionColonies;

/*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*

    Reads patch notes written in a small, line-based subset of markdown.

    _mod.md (settings shared by all of a mod's entries):
        # <mod name>
        - Banner: <texture path>
        ## Links
        - [<tooltip>](<url>) icon=<texture path>
        ## Notes
        <one note per line>

    v<major>.<minor>.md (the entries):
        ## <major>.<minor>.<patch> - <label>
        - Date: yyyy-MM-dd
        - Type: Hotfix | Patch | Minor | Major
        - Authors: <name>, <name>
        <description paragraphs; lines are joined with a newline, a blank line starts a new paragraph>
        ### Changes
        - <one change per bullet; indented lines continue the bullet>
        ### Notes
        <optional; one note per line; replaces the mod-wide notes for this entry>

    In both files: "<!-- -->" comments are skipped, a line starting with a backslash has it
    removed (so text can start with "#" or "-"), and **bold** / *italic* become Unity rich text.

*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*/

public static class PatchNoteParser
{
    private static readonly Regex EntryHeading = new(@"^##\s+(\d+)\.(\d+)\.(\d+)\s+-\s+(.+?)\s*$");
    private static readonly Regex MetadataLine = new(@"^-\s*(Date|Type|Authors)\s*:\s*(.*?)\s*$", RegexOptions.IgnoreCase);
    private static readonly Regex BannerLine = new(@"^-\s*Banner\s*:\s*(.*?)\s*$", RegexOptions.IgnoreCase);
    private static readonly Regex LinkLine = new(@"^-\s*\[(.*?)\]\(([^)\s]+)\)(?:\s+icon=(\S+))?\s*$");
    private static readonly Regex Bold = new(@"\*\*(?=\S)(.+?)(?<=\S)\*\*");
    private static readonly Regex Italic = new(@"(?<![\*\w])\*(?=\S)(.+?)(?<=\S)\*(?![\*\w])");

    private enum Section { None, Metadata, Description, Changes, Notes, Links, Skip }

    /// <summary>
    /// Parses a mod's _mod.md. Problems are added to <paramref name="warnings"/>.
    /// </summary>
    public static PatchNoteModInfo ParseModInfo(string text, string fileName, string modId, ModContentPack pack, List<string> warnings)
    {
        PatchNoteModInfo info = new(modId, pack);
        Section section = Section.None;
        string[] lines = SplitLines(text);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd();
            string trimmed = line.Trim();
            if (trimmed.Length == 0) continue;

            if (trimmed.StartsWith("# "))
            {
                info.displayName = trimmed.Substring(2).Trim();
                section = Section.None;
                continue;
            }
            if (trimmed.StartsWith("## "))
            {
                string heading = trimmed.Substring(3).Trim();
                if (heading.Equals("Links", StringComparison.OrdinalIgnoreCase)) section = Section.Links;
                else if (heading.Equals("Notes", StringComparison.OrdinalIgnoreCase)) section = Section.Notes;
                else
                {
                    warnings.Add($"{fileName}:{i + 1}: unknown section '{heading}' (expected Links or Notes)");
                    section = Section.Skip;
                }
                continue;
            }

            switch (section)
            {
                case Section.None:
                    Match banner = BannerLine.Match(trimmed);
                    if (banner.Success) info.bannerImagePath = banner.Groups[1].Value;
                    else warnings.Add($"{fileName}:{i + 1}: unexpected line '{trimmed}'");
                    break;
                case Section.Links:
                    Match link = LinkLine.Match(trimmed);
                    if (link.Success)
                    {
                        info.links.Add(new PatchNoteModInfo.Link
                        {
                            tooltip = FormatInline(link.Groups[1].Value),
                            url = link.Groups[2].Value,
                            iconPath = link.Groups[3].Value
                        });
                        if (!link.Groups[3].Success)
                            warnings.Add($"{fileName}:{i + 1}: link has no icon=<texture path>");
                    }
                    else warnings.Add($"{fileName}:{i + 1}: expected a link like '- [tooltip](url) icon=path', got '{trimmed}'");
                    break;
                case Section.Notes:
                    info.notes.Add(FormatText(line));
                    break;
            }
        }

        return info;
    }

    /// <summary>
    /// Parses the entries in one v*.md file. Broken entries are skipped and reported in <paramref name="warnings"/>.
    /// </summary>
    public static List<PatchNote> Parse(string text, string fileName, PatchNoteModInfo mod, List<string> warnings)
    {
        List<PatchNote> result = [];
        HashSet<int> seenVersions = [];
        string[] lines = SplitLines(text);

        PatchNote current = null;
        int currentLine = 0;
        bool currentBroken = false;
        Section section = Section.None;
        StringBuilder description = new();
        bool paragraphBreak = false;

        void Finish()
        {
            if (current is null) return;
            current.description = description.ToString();

            string where = $"{fileName}:{currentLine}";
            if (current.releaseDate == default)
            {
                warnings.Add($"{where}: entry {current.VersionNumber} has no valid '- Date: yyyy-MM-dd' line; skipped");
                currentBroken = true;
            }
            if (current.patchNoteType == PatchNoteType.Undefined)
            {
                warnings.Add($"{where}: entry {current.VersionNumber} has no valid '- Type:' line (Hotfix, Patch, Minor or Major); skipped");
                currentBroken = true;
            }
            if (current.changeLines.Count == 0)
            {
                warnings.Add($"{where}: entry {current.VersionNumber} has no '### Changes' bullets; skipped");
                currentBroken = true;
            }
            if (current.authors.Count == 0)
                warnings.Add($"{where}: entry {current.VersionNumber} has no authors");
            if (!currentBroken && !seenVersions.Add(current.VersionSortKey))
            {
                warnings.Add($"{where}: duplicate entry {current.VersionNumber}; skipped");
                currentBroken = true;
            }

            if (!currentBroken) result.Add(current);
            current = null;
        }

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd();
            string trimmed = line.Trim();

            if (trimmed.StartsWith("## ") || trimmed == "##")
            {
                Finish();
                section = Section.Skip;
                Match heading = EntryHeading.Match(trimmed);
                if (!heading.Success)
                {
                    warnings.Add($"{fileName}:{i + 1}: expected an entry heading like '## 1.2.3 - Label', got '{trimmed}'; skipped");
                    continue;
                }
                if (!TryParseVersionPart(heading.Groups[1].Value, out int maj)
                    || !TryParseVersionPart(heading.Groups[2].Value, out int min)
                    || !TryParseVersionPart(heading.Groups[3].Value, out int pat))
                {
                    warnings.Add($"{fileName}:{i + 1}: version numbers must be between 0 and 999, got '{trimmed}'; skipped");
                    continue;
                }

                current = new PatchNote(mod)
                {
                    major = maj,
                    minor = min,
                    patch = pat,
                    label = FormatInline(heading.Groups[4].Value)
                };
                currentLine = i + 1;
                currentBroken = false;
                section = Section.Metadata;
                description.Clear();
                paragraphBreak = false;
                continue;
            }

            // File title, or lines outside any entry
            if (trimmed.StartsWith("# ") || current is null) continue;

            if (trimmed.StartsWith("###"))
            {
                string heading = trimmed.TrimStart('#').Trim();
                if (heading.Equals("Changes", StringComparison.OrdinalIgnoreCase)) section = Section.Changes;
                else if (heading.Equals("Notes", StringComparison.OrdinalIgnoreCase))
                {
                    section = Section.Notes;
                    current.notes ??= [];
                }
                else
                {
                    warnings.Add($"{fileName}:{i + 1}: unknown section '{heading}' (expected Changes or Notes); its content is ignored");
                    section = Section.Skip;
                }
                continue;
            }

            if (section == Section.Metadata)
            {
                if (trimmed.Length == 0) continue;
                Match meta = MetadataLine.Match(trimmed);
                if (meta.Success)
                {
                    ApplyMetadata(current, meta.Groups[1].Value, meta.Groups[2].Value, $"{fileName}:{i + 1}", warnings);
                    continue;
                }
                section = Section.Description;
            }

            switch (section)
            {
                case Section.Description:
                    if (trimmed.Length == 0)
                    {
                        paragraphBreak = description.Length > 0;
                        break;
                    }
                    if (description.Length > 0) description.Append(paragraphBreak ? "\n\n" : "\n");
                    description.Append(FormatText(trimmed));
                    paragraphBreak = false;
                    break;
                case Section.Changes:
                    if (trimmed.Length == 0) break;
                    if (trimmed.StartsWith("- ") || trimmed == "-")
                        current.changeLines.Add(FormatText(trimmed.Substring(1).Trim()));
                    else if (current.changeLines.Count > 0)
                        current.changeLines[current.changeLines.Count - 1] += " " + FormatText(trimmed);
                    else
                        current.changeLines.Add(FormatText(trimmed));
                    break;
                case Section.Notes:
                    if (trimmed.Length > 0) current.notes.Add(FormatText(trimmed));
                    break;
            }
        }
        Finish();

        return result;
    }

    private static void ApplyMetadata(PatchNote note, string key, string value, string where, List<string> warnings)
    {
        switch (key.ToLowerInvariant())
        {
            case "date":
                if (!DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out note.releaseDate))
                    warnings.Add($"{where}: date '{value}' is not in yyyy-MM-dd format");
                break;
            case "type":
                note.patchNoteType = value.ToLowerInvariant() switch
                {
                    "hotfix" => PatchNoteType.Hotfix,
                    "patch" => PatchNoteType.Patch,
                    "minor" => PatchNoteType.Minor,
                    "major" => PatchNoteType.Major,
                    _ => PatchNoteType.Undefined
                };
                if (note.patchNoteType == PatchNoteType.Undefined)
                    warnings.Add($"{where}: type '{value}' is not one of Hotfix, Patch, Minor, Major");
                break;
            case "authors":
                note.authors.AddRange(value.Split(',').Select(a => a.Trim()).Where(a => a.Length > 0));
                break;
        }
    }

    /// <summary>VersionSortKey packs each part into 3 digits, so parts above 999 would collide.</summary>
    private static bool TryParseVersionPart(string s, out int value)
        => int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value <= 999;

    /// <summary>Splits into lines and blanks out "&lt;!-- --&gt;" comments, keeping line numbers intact.</summary>
    private static string[] SplitLines(string text)
    {
        string[] lines = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        bool inComment = false;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            StringBuilder kept = new();
            int pos = 0;
            while (pos < line.Length)
            {
                if (inComment)
                {
                    int end = line.IndexOf("-->", pos, StringComparison.Ordinal);
                    if (end < 0) { pos = line.Length; break; }
                    pos = end + 3;
                    inComment = false;
                }
                else
                {
                    int start = line.IndexOf("<!--", pos, StringComparison.Ordinal);
                    if (start < 0) { kept.Append(line, pos, line.Length - pos); break; }
                    kept.Append(line, pos, start - pos);
                    pos = start + 4;
                    inComment = true;
                }
            }
            lines[i] = kept.ToString();
        }
        return lines;
    }

    /// <summary>Removes a leading escape backslash, then applies inline formatting.</summary>
    private static string FormatText(string text)
    {
        text = text.Trim();
        if (text.StartsWith("\\")) text = text.Substring(1);
        return FormatInline(text);
    }

    /// <summary>Converts **bold** and *italic* to Unity rich text.</summary>
    public static string FormatInline(string text)
    {
        if (text.IndexOf('*') < 0) return text;
        text = Bold.Replace(text, "<b>$1</b>");
        return Italic.Replace(text, "<i>$1</i>");
    }
}
