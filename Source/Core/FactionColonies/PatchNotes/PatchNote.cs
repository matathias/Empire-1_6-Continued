using UnityEngine;

namespace FactionColonies;

/// <summary>
/// One patch note entry, parsed from a mod's PatchNotes/v*.md files by <see cref="PatchNoteParser"/>.
/// </summary>
public class PatchNote
{
    public readonly PatchNoteModInfo mod;

    public int major;
    public int minor;
    public int patch;
    public string label = "";
    public string description = "";
    public DateTime releaseDate;
    public PatchNoteType patchNoteType = PatchNoteType.Undefined;
    public readonly List<string> changeLines = [];
    public readonly List<string> authors = [];

    /// <summary>This entry's own notes. Null means the mod-wide notes from _mod.md apply.</summary>
    public List<string> notes;

    public PatchNote(PatchNoteModInfo mod) => this.mod = mod;

    public string ModId => mod.modId;

    /// <summary>Full title, e.g. "[Empire] Backend Cleanup 1.6.0"</summary>
    public string Title => $"[{ModName}] {label} {VersionNumber}";

    /// <summary>Title without the mod name, e.g. "Backend Cleanup 1.6.0"</summary>
    public string ShortTitle => $"{label} {VersionNumber}";

    public string ModName => mod.ModName;

    public string Description => description;

    /// <summary>The version formatted like "1.6.4"</summary>
    public string VersionNumber => Version.ToString();

    public FCVersion Version => new(major, minor, patch);

    public int Major => major;

    public int Minor => minor;

    public int Patch => patch;

    /// <summary>Integer sort key encoding the full version for correct ordering.</summary>
    public int VersionSortKey => major * 1000000 + minor * 1000 + patch;

    public PatchNoteType GetPatchNoteType => patchNoteType;

    public DateTime ReleaseDate => releaseDate;

    public List<string> Links => mod.Urls;

    /// <summary>The tooltip for each link button, in the same order as <see cref="Links"/>.</summary>
    public List<string> LinkButtonToolTips => mod.Tooltips;

    public List<Texture2D> LinkButtonImages => mod.LinkButtonImages;

    public Texture2D BannerImage => mod.BannerImage;

    /// <summary>The change lines as a bulleted list, one per line.</summary>
    public string PatchNotesFormatted => string.Join("\n", changeLines.Select(line => "• " + line));

    public List<string> AdditionalNotes => notes ?? mod.notes;

    public string AdditionalNotesFormatted => string.Join("\n", AdditionalNotes);

    /// <summary>
    /// The authors as "name0, name1, ..., nameN-1 and nameN", or just the name if there is one author.
    /// </summary>
    public string AuthorsFormatted
    {
        get
        {
            if (authors.NullOrEmpty()) return "";
            if (authors.Count == 1) return authors[0];
            return $"{string.Join(", ", authors.Take(authors.Count - 1))} and {authors[authors.Count - 1]}";
        }
    }

    public string CompactBodyString
    {
        get
        {
            string result = description + "\n\nChanges:\n" + PatchNotesFormatted + "\n\nContributors: " + AuthorsFormatted;
            if (!AdditionalNotes.NullOrEmpty())
                result += "\n" + AdditionalNotesFormatted;
            return result;
        }
    }

    /// <summary>Returns true if this patch note's version is newer than the given version.</summary>
    public bool IsNewerThan(FCVersion version) => Version.IsNewerThan(version);
}
