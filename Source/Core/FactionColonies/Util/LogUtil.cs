namespace FactionColonies;

/// <summary>
/// Class for centrally managing logging.
/// - Adds a slug to the beginning of log messages
/// - Gives a central place for disabling/enabling verbose logging without recompiling
/// </summary>
public static class LogUtil
{
    private const string slug = "[Empire]";

    // When set true on a thread, all logging from this class on THAT thread is dropped. Thread-static so a
    // background worker can silence its (indirect) logging: RimWorld's Log.Error opens the dev log window
    // (a main-thread GUI op) and the log window enumerates the message queue unlocked, so an off-thread
    // Log call can throw or kill the worker. Only the setting thread is affected; the main thread logs
    // normally.
    [ThreadStatic] private static bool suppressThisThread;

    public static bool SuppressOnThisThread
    {
        get { return suppressThisThread; }
        set { suppressThisThread = value; }
    }

    /// <summary>True when <see cref="Message(string)"/> output would be logged on this thread.</summary>
    internal static bool DebugEnabled => !suppressThisThread && FCSettings.PrintDebug;

    /// <summary>Gates <see cref="Message(ref DebugLogHandler{DebugGate})"/> on <see cref="DebugEnabled"/>.</summary>
    public readonly struct DebugGate : IDebugLogGate
    {
        public static bool Enabled => DebugEnabled;
    }

    public static void Message(string message)
    {
        if (suppressThisThread) return;
        if (FCSettings.PrintDebug)
        {
            Log.Message($"{slug} {message}");
        }
    }

    /// <summary>
    /// Debug message from an interpolated string. The compiler picks this overload for every
    /// <c>Message($"...")</c> call; when debug logging is off, the string's holes are never evaluated
    /// and nothing is allocated. <see cref="Message(string)"/> stays for plain strings and for
    /// already-compiled submods that bind to it.
    /// </summary>
    public static void Message(ref DebugLogHandler<DebugGate> message)
    {
        if (!message.Enabled) return;
        Log.Message($"{slug} {message.ToStringAndClear()}");
    }
    /// <summary>
    /// Prints a non-warning, non-error message to the log even if the user has disabled Verbose Logging.
    /// </summary>
    /// <param name="message"></param>
    public static void MessageForce(string message)
    {
        if (suppressThisThread) return;
        Log.Message($"{slug} {message}");
    }
    public static void Warning(string message)
    {
        if (suppressThisThread) return;
        Log.Warning($"{slug}[WARN] {message}");
    }
    public static void Error(string message)
    {
        if (suppressThisThread) return;
        Log.Error($"{slug}[ERR] {message}");
    }
    public static void ErrorOnce(string message, int key)
    {
        if (suppressThisThread) return;
        Log.ErrorOnce($"{slug}[ERRONCE] {message}", key);
    }
    /// <summary>Logs <paramref name="message"/> once per session, keyed by the message text itself.</summary>
    public static void ErrorOnce(string message) => ErrorOnce(message, message.GetHashCode());
}
