namespace FactionColonies;

/// <summary>
/// The program state an Empire test must run in. Running a test in the wrong state is the ONLY
/// reason a test may be skipped; every other precondition must be forced by the test itself.
/// </summary>
public enum TestGameState
{
    /// <summary>Needs a loaded game. Skipped on the main menu.</summary>
    Playing,
    /// <summary>Needs the main menu (no game running). Skipped while a game is loaded.</summary>
    MainMenu,
    /// <summary>Touches no game state (pure math, def validation). Never skipped.</summary>
    Any,
}

/// <summary>
/// Marks a public static method as an Empire test, discovered by reflection in
/// <see cref="EmpireTestRunner"/>. The default tier is non-destructive: such tests must
/// leave game state as they found it (pure math, def validation, or snapshot/restore).
/// <para>Tests never skip themselves. The runner skips a test only when the program state does not
/// match <see cref="GameState"/>. A test that needs anything else (a settlement, a squad, silver, a
/// pawn, a clean policy slate) must create it, which makes it a destructive test.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class EmpireTestAttribute : Attribute
{
    public string Category { get; }

    /// <summary>The program state this test needs; the runner skips the test in any other state.</summary>
    public TestGameState GameState { get; set; } = TestGameState.Playing;

    /// <summary>
    /// Package id of a DLC or mod the test is about (e.g. <c>"Ludeon.RimWorld.Odyssey"</c>), or null.
    /// Content that cannot be forced into existence is not a precondition: without that package the
    /// test is not part of the suite at all (not run, not counted), the same way a compat patch's
    /// tests do not exist when its mod is absent.
    /// </summary>
    public string RequiresPackageId { get; set; }

    /// <summary>
    /// False for the standard tier. Overridden true by <see cref="EmpireDestructiveTestAttribute"/>.
    /// The runner uses this to keep destructive tests out of "Run All Tests" / the normal
    /// category browser, and to gate them behind a save-first confirmation.
    /// </summary>
    public virtual bool Destructive => false;

    public EmpireTestAttribute(string category = "General") => Category = category;
}

/// <summary>
/// Marks a public static method as a DESTRUCTIVE Empire test. Destructive tests intentionally
/// mutate (and may thrash) live game state. They are not cleaned up after.
/// The contract is: a destructive test may leave the game messy, but it must NEVER crash the
/// game (force preconditions with the <c>DestructiveTestUtil.Ensure*</c> helpers, wrap volatile
/// calls in <c>TestAssert.DoesNotThrow</c>, and assert internal-consistency invariants afterward).
/// <para>Distinct attribute (rather than a ctor flag) so the dangerous surface is obvious at
/// the call site and enumerable with <c>grep EmpireDestructiveTest</c>. Because it derives from
/// <see cref="EmpireTestAttribute"/>, the runner's existing reflection discovery finds it
/// unchanged.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class EmpireDestructiveTestAttribute : EmpireTestAttribute
{
    public override bool Destructive => true;

    public EmpireDestructiveTestAttribute(string category = "General") : base(category) { }
}
