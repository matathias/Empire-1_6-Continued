using System;

namespace FactionColonies
{
    /// <summary>
    /// Marks a public static method as an Empire test, discovered by reflection in
    /// <see cref="EmpireTestRunner"/>. The default tier is non-destructive: such tests must
    /// leave game state as they found it (pure math, def validation, or snapshot/restore).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class EmpireTestAttribute : Attribute
    {
        public string Category { get; }

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
    /// game (guard preconditions with <c>TestAssert.Skip</c>, wrap volatile calls in
    /// <c>TestAssert.DoesNotThrow</c>, and assert internal-consistency invariants afterward).
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
}
