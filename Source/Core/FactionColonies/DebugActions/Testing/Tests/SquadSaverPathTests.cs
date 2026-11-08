using System.IO;

namespace FactionColonies;

/* Template file names come from player-entered names. These pure tests check that every name maps
   to a file directly inside its template folder, and that names that were already valid keep
   mapping to the same file. Only characters every OS rejects ('/' and NUL) are used as invalid
   input, so the tests hold on Windows, Linux and macOS alike. */
public static class SquadSaverPathTests
{
    [EmpireTest("Military", GameState = TestGameState.Any)]
    public static void SafeFileName_ValidName_Unchanged()
    {
        foreach (string name in new[] { "Alpha Squad", "Riflemen-2", "Unit_3", "Group 7 (Reserve)" })
            TestAssert.AreEqual(name, FactionColoniesMilitary.SafeFileName(name), $"'{name}' is already a valid file name");
    }

    [EmpireTest("Military", GameState = TestGameState.Any)]
    public static void SafeFileName_InvalidCharacters_Replaced()
    {
        TestAssert.AreEqual("a_b", FactionColoniesMilitary.SafeFileName("a/b"), "A path separator must be replaced");
        TestAssert.AreEqual("a_b", FactionColoniesMilitary.SafeFileName("a\0b"), "A NUL character must be replaced");
        string sanitized = FactionColoniesMilitary.SafeFileName("x:y*z?\"<>|\\");
        TestAssert.IsTrue(sanitized.IndexOfAny(Path.GetInvalidFileNameChars()) < 0,
            $"'{sanitized}' still holds a character this OS rejects in file names");
    }

    [EmpireTest("Military", GameState = TestGameState.Any)]
    public static void SafeFileName_NullOrEmpty_PassesThrough()
    {
        TestAssert.IsNull(FactionColoniesMilitary.SafeFileName(null));
        TestAssert.AreEqual("", FactionColoniesMilitary.SafeFileName(""));
    }

    [EmpireTest("Military", GameState = TestGameState.Any)]
    public static void TemplatePaths_StayInsideTheirFolder()
    {
        foreach (string name in new[] { "../escape", "../../etc/passwd", "a/b/c", "..", "plain" })
        {
            AssertDirectlyIn(FactionColoniesMilitary.EmpireMilitarySquadFolder, FactionColoniesMilitary.GetSquadPath(name), name);
            AssertDirectlyIn(FactionColoniesMilitary.EmpireMilitaryUnitFolder, FactionColoniesMilitary.GetUnitPath(name), name);
            AssertDirectlyIn(FactionColoniesMilitary.EmpireMilitaryFireSupportFolder, FactionColoniesMilitary.GetFireSupportPath(name), name);
        }
    }

    private static void AssertDirectlyIn(string folder, string path, string name)
    {
        string parent = Path.GetDirectoryName(Path.GetFullPath(path));
        TestAssert.AreEqual(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), parent,
            $"Template '{name}' resolved to {path}, outside {folder}");
        TestAssert.IsTrue(path.EndsWith(".xml"), $"Template '{name}' should keep the .xml extension");
    }
}
