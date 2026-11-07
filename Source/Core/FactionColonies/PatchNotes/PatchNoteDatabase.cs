using System.IO;

namespace FactionColonies;

/// <summary>
/// All patch notes of the running mods. A mod takes part by shipping PatchNotes/_mod.md at its root;
/// its entries are read from the v*.md files next to it. Loaded on first access.
/// </summary>
public static class PatchNoteDatabase
{
    public const string FolderName = "PatchNotes";
    public const string ModInfoFileName = "_mod.md";
    public const string EntryFilePattern = "v*.md";

    private static Dictionary<string, List<PatchNote>> notesByMod;

    private static Dictionary<string, List<PatchNote>> NotesByMod => notesByMod ??= LoadAll();

    /// <summary>The ids of every mod that has patch notes.</summary>
    public static IEnumerable<string> ModIds => NotesByMod.Keys;

    /// <summary>A mod's patch notes, newest first. Empty if the mod has none.</summary>
    public static List<PatchNote> ForMod(string modId)
        => modId is not null && NotesByMod.TryGetValue(modId, out List<PatchNote> notes) ? notes : [];

    /// <summary>A mod's newest patch note, or null if it has none.</summary>
    public static PatchNote LatestForMod(string modId) => ForMod(modId).FirstOrDefault();

    private static Dictionary<string, List<PatchNote>> LoadAll()
    {
        Dictionary<string, List<PatchNote>> result = new(StringComparer.OrdinalIgnoreCase);

        foreach (ModContentPack pack in LoadedModManager.RunningModsListForReading)
        {
            string folder = Path.Combine(pack.RootDir, FolderName);
            string modInfoPath = Path.Combine(folder, ModInfoFileName);
            if (!File.Exists(modInfoPath)) continue;

            // PackageIdPlayerFacing has no "_steam" postfix, so Steam and local copies share an id
            string modId = pack.PackageIdPlayerFacing.ToLowerInvariant();
            if (result.ContainsKey(modId)) continue;

            List<string> warnings = [];
            List<PatchNote> notes = LoadMod(pack, folder, modInfoPath, modId, warnings);
            foreach (string warning in warnings)
                LogUtil.Warning($"Patch notes for {pack.Name}: {warning}");

            result[modId] = notes;
        }

        return result;
    }

    private static List<PatchNote> LoadMod(ModContentPack pack, string folder, string modInfoPath, string modId, List<string> warnings)
    {
        List<PatchNote> notes = [];
        if (!TryRead(modInfoPath, warnings, out string modInfoText)) return notes;
        PatchNoteModInfo info = PatchNoteParser.ParseModInfo(modInfoText, ModInfoFileName, modId, pack, warnings);

        string[] files = Directory.GetFiles(folder, EntryFilePattern);
        Array.Sort(files, StringComparer.Ordinal);
        HashSet<int> seenVersions = [];
        foreach (string file in files)
        {
            if (!TryRead(file, warnings, out string text)) continue;
            string fileName = Path.GetFileName(file);
            foreach (PatchNote note in PatchNoteParser.Parse(text, fileName, info, warnings))
            {
                if (seenVersions.Add(note.VersionSortKey))
                    notes.Add(note);
                else
                    warnings.Add($"{fileName}: entry {note.VersionNumber} also appears in another file; skipped");
            }
        }

        notes.SortByDescending(note => note.VersionSortKey);
        return notes;
    }

    private static bool TryRead(string path, List<string> warnings, out string text)
    {
        try
        {
            text = File.ReadAllText(path);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            warnings.Add($"could not read {Path.GetFileName(path)}: {e.Message}");
            text = null;
            return false;
        }
    }
}
