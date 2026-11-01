namespace FactionColonies
{
    /// <summary>
    /// Optional interface for <see cref="CodexEntryDef"/> entries that need to display
    /// live game-state values (e.g., current ETL, prosperity breakdowns).
    /// <para>Implement this in a concrete class, then reference the class via
    /// <c>dynamicProvider</c> on the CodexEntryDef in XML. The Codex window
    /// instantiates the provider once and calls <see cref="GetDynamicContent"/>
    /// each time the entry is rendered.</para>
    /// </summary>
    public interface ICodexDynamicProvider
    {
        /// <summary>
        /// Returns a formatted string of live game-state information to display
        /// below the static entry text. Called at render time — values are always current.
        /// <para>Return null or empty to show nothing.</para>
        /// </summary>
        string GetDynamicContent(FactionFC faction);
    }
}
