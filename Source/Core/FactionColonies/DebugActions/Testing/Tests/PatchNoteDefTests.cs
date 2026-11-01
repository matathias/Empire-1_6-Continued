using Verse;

namespace FactionColonies
{
    /// <summary>
    /// Covers <see cref="PatchNoteDef.AuthorsFormatted"/>. Regression: the getter used to call
    /// List.Pop() unconditionally, which throws ArgumentOutOfRangeException on an empty authors
    /// list (reachable because the empty-authors ConfigError only warns, it does not block load).
    /// </summary>
    public static class PatchNoteDefTests
    {
        [EmpireTest("PatchNote")]
        public static void AuthorsFormatted_EmptyAuthors_ReturnsEmptyAndDoesNotThrow()
        {
            // A fresh def has an empty (non-null) authors list from its field initializer.
            var def = new PatchNoteDef();

            string result = null;
            TestAssert.DoesNotThrow(() => { result = def.AuthorsFormatted; },
                "AuthorsFormatted must not throw on an empty authors list");
            TestAssert.IsTrue(result.NullOrEmpty(),
                $"AuthorsFormatted on empty authors should be empty, got '{result}'");
        }
    }
}
