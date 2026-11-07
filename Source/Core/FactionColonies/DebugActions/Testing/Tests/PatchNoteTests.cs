namespace FactionColonies;

/// <summary>
/// Covers <see cref="PatchNoteParser"/> and <see cref="PatchNote"/> formatting. All input is inline markdown.
/// </summary>
public static class PatchNoteTests
{
    private const string FullEntry = """
        # Test v1.6

        ## 1.6.4 - Minor Fixes & Features
        - Date: 2026-07-02
        - Type: Minor
        - Authors: Alice, Bob, Carol

        First paragraph.
        Second line of the first paragraph.

        Second paragraph.

        ### Changes
        - First change
        - Second change
          continues here
        """;

    private static PatchNoteModInfo TestMod()
    {
        PatchNoteModInfo mod = new("test.mod") { displayName = "Test Mod" };
        mod.notes.Add("Mod-wide note");
        return mod;
    }

    private static List<PatchNote> Parse(string text, out List<string> warnings)
    {
        warnings = [];
        return PatchNoteParser.Parse(text, "test.md", TestMod(), warnings);
    }

    [EmpireTest("PatchNote")]
    public static void Parse_FullEntry_ReadsEveryField()
    {
        List<PatchNote> notes = Parse(FullEntry, out List<string> warnings);

        TestAssert.IsEmpty(warnings, $"no warnings expected, got: {string.Join(" | ", warnings)}");
        TestAssert.AreEqual(1, notes.Count, "one entry");
        PatchNote note = notes[0];
        TestAssert.AreEqual(1, note.Major);
        TestAssert.AreEqual(6, note.Minor);
        TestAssert.AreEqual(4, note.Patch);
        TestAssert.AreEqual("Minor Fixes & Features", note.label);
        TestAssert.AreEqual("Minor Fixes & Features 1.6.4", note.ShortTitle);
        TestAssert.AreEqual(new DateTime(2026, 7, 2), note.ReleaseDate);
        TestAssert.AreEqual(PatchNoteType.Minor, note.GetPatchNoteType);
        TestAssert.AreEqual("Alice, Bob and Carol", note.AuthorsFormatted);
        TestAssert.AreEqual("First paragraph.\nSecond line of the first paragraph.\n\nSecond paragraph.", note.Description);
        TestAssert.AreEqual(2, note.changeLines.Count, "two change lines");
        TestAssert.AreEqual("First change", note.changeLines[0]);
        TestAssert.AreEqual("Second change continues here", note.changeLines[1], "indented line continues the bullet");
    }

    [EmpireTest("PatchNote")]
    public static void Parse_MultipleEntries_AllRead()
    {
        string text = FullEntry + "\n\n" + """
            ## 1.6.3 - Older
            - Date: 2026-06-30
            - Type: Hotfix
            - Authors: Alice

            ### Changes
            - A fix
            """;
        List<PatchNote> notes = Parse(text, out List<string> warnings);

        TestAssert.IsEmpty(warnings, $"no warnings expected, got: {string.Join(" | ", warnings)}");
        TestAssert.AreEqual(2, notes.Count, "two entries");
        TestAssert.AreEqual(3, notes[1].Patch);
        TestAssert.AreEqual(PatchNoteType.Hotfix, notes[1].GetPatchNoteType);
        TestAssert.AreEqual("", notes[1].Description, "an entry without a description has an empty one");
    }

    [EmpireTest("PatchNote")]
    public static void Parse_NotesSection_ReplacesModNotes()
    {
        List<PatchNote> withoutOwn = Parse(FullEntry, out _);
        TestAssert.AreEqual("Mod-wide note", withoutOwn[0].AdditionalNotesFormatted, "mod-wide notes apply by default");

        List<PatchNote> withOwn = Parse(FullEntry + "\n### Notes\nEntry note one\nEntry note two\n", out List<string> warnings);
        TestAssert.IsEmpty(warnings, $"no warnings expected, got: {string.Join(" | ", warnings)}");
        TestAssert.AreEqual("Entry note one\nEntry note two", withOwn[0].AdditionalNotesFormatted, "entry notes replace mod-wide notes");
    }

    [EmpireTest("PatchNote")]
    public static void Parse_BadVersionDateOrType_SkippedWithWarning()
    {
        string badVersion = FullEntry.Replace("## 1.6.4 -", "## 1.6 -");
        string badDate = FullEntry.Replace("2026-07-02", "02/07/2026");
        string badType = FullEntry.Replace("- Type: Minor", "- Type: Huge");
        string noChanges = FullEntry.Substring(0, FullEntry.IndexOf("### Changes", StringComparison.Ordinal));

        foreach ((string name, string text) in new[] { ("version", badVersion), ("date", badDate), ("type", badType), ("changes", noChanges) })
        {
            List<PatchNote> notes = Parse(text, out List<string> warnings);
            TestAssert.AreEqual(0, notes.Count, $"bad {name}: entry is skipped");
            TestAssert.IsNotEmpty(warnings, $"bad {name}: a warning is reported");
        }
    }

    [EmpireTest("PatchNote")]
    public static void Parse_DuplicateVersion_SecondSkipped()
    {
        List<PatchNote> notes = Parse(FullEntry + "\n\n" + FullEntry.Replace("Minor Fixes", "Duplicate"), out List<string> warnings);

        TestAssert.AreEqual(1, notes.Count, "duplicate is skipped");
        TestAssert.AreEqual("Minor Fixes & Features", notes[0].label, "the first entry is kept");
        TestAssert.AreEqual(1, warnings.Count, "one warning for the duplicate");
    }

    [EmpireTest("PatchNote")]
    public static void Parse_CommentsAndEscapes_Handled()
    {
        string text = FullEntry.Replace("Second paragraph.", "\\- not a bullet <!-- hidden -->\n<!--\n## 9.9.9 - Commented out\n-->")
            + "\n- \\# literal hash";
        List<PatchNote> notes = Parse(text, out List<string> warnings);

        TestAssert.IsEmpty(warnings, $"no warnings expected, got: {string.Join(" | ", warnings)}");
        TestAssert.AreEqual(1, notes.Count, "the commented-out heading is not an entry");
        TestAssert.IsTrue(notes[0].Description.EndsWith("\n\n- not a bullet"), $"escape removed, comment stripped: '{notes[0].Description}'");
        TestAssert.AreEqual("# literal hash", notes[0].changeLines[2]);
    }

    [EmpireTest("PatchNote")]
    public static void FormatInline_BoldAndItalic_ConvertedButStrayAsterisksKept()
    {
        TestAssert.AreEqual("it <i>is</i> needed", PatchNoteParser.FormatInline("it *is* needed"));
        TestAssert.AreEqual("<b>bold</b> and <i>italic</i>", PatchNoteParser.FormatInline("**bold** and *italic*"));
        TestAssert.AreEqual("def x * <budget>", PatchNoteParser.FormatInline("def x * <budget>"), "spaced asterisk is literal");
        TestAssert.AreEqual("*Registry -> EmpireRegistry", PatchNoteParser.FormatInline("*Registry -> EmpireRegistry"), "unpaired asterisk is literal");
        TestAssert.AreEqual("a*b*c", PatchNoteParser.FormatInline("a*b*c"), "asterisks inside a word are literal");
    }

    [EmpireTest("PatchNote")]
    public static void ParseModInfo_ReadsBannerLinksAndNotes()
    {
        const string text = """
            # Test Mod

            - Banner: UI/Banners/Test

            ## Links
            - [Join the discord!](https://discord.gg/abc) icon=GUI/Buttons/discordlogo
            - [Report issues](https://github.com/x/y/tree/dev) icon=GUI/Buttons/githublogo

            ## Notes
            Please raise any issues you find.
            """;
        List<string> warnings = [];
        PatchNoteModInfo info = PatchNoteParser.ParseModInfo(text, "_mod.md", "test.mod", null, warnings);

        TestAssert.IsEmpty(warnings, $"no warnings expected, got: {string.Join(" | ", warnings)}");
        TestAssert.AreEqual("Test Mod", info.ModName, "without a pack, the title is the mod name");
        TestAssert.AreEqual("UI/Banners/Test", info.bannerImagePath);
        TestAssert.AreEqual(2, info.links.Count, "two links");
        TestAssert.AreEqual("https://discord.gg/abc", info.Urls[0]);
        TestAssert.AreEqual("Report issues", info.Tooltips[1]);
        TestAssert.AreEqual("GUI/Buttons/githublogo", info.links[1].iconPath);
        TestAssert.AreEqual(1, info.notes.Count, "one note");
    }

    // Regression: the authors getter used to call List.Pop() unconditionally, which throws on an empty list.
    [EmpireTest("PatchNote")]
    public static void AuthorsFormatted_EmptyAuthors_ReturnsEmptyAndDoesNotThrow()
    {
        PatchNote note = new(TestMod());

        string result = null;
        TestAssert.DoesNotThrow(() => { result = note.AuthorsFormatted; },
            "AuthorsFormatted must not throw on an empty authors list");
        TestAssert.IsTrue(result.NullOrEmpty(),
            $"AuthorsFormatted on empty authors should be empty, got '{result}'");
    }
}
