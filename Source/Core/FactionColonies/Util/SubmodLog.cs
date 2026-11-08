namespace FactionColonies;

/// <summary>A submod logger's gate: the mod's debug setting, plus the slug its log lines start with.</summary>
public interface ISubmodLogGate : IDebugLogGate
{
    static abstract string Slug { get; }
}

/// <summary>
/// Logging for a submod, prefixed with <typeparamref name="TGate"/>'s slug; debug messages follow
/// the gate's setting. Each submod wraps this in a small logger class (LogSC, LogUR, ...) so its call
/// sites stay short and debug <c>Message($"...")</c> calls still bind to the interpolated-string handler.
/// </summary>
public static class SubmodLog<TGate> where TGate : ISubmodLogGate
{
    public static void Message(string message)
    {
        if (TGate.Enabled)
            Log.Message($"{TGate.Slug} {message}");
    }

    /// <summary>
    /// Debug message from an interpolated string. When debug logging is off, the string's holes are
    /// never evaluated and nothing is allocated.
    /// </summary>
    public static void Message(ref DebugLogHandler<TGate> message)
    {
        if (!message.Enabled) return;
        Log.Message($"{TGate.Slug} {message.ToStringAndClear()}");
    }

    /// <summary>Logs even when the mod's debug logging is off.</summary>
    public static void MessageForce(string message) => Log.Message($"{TGate.Slug} {message}");

    public static void Warning(string message) => Log.Warning($"{TGate.Slug}[WARN] {message}");

    public static void Error(string message) => Log.Error($"{TGate.Slug}[ERR] {message}");
}
