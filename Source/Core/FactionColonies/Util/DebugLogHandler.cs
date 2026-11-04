using System.Runtime.CompilerServices;

namespace FactionColonies;

/// <summary>
/// Says whether debug logging is on for one logger. Each logger declares a small struct implementing
/// this (Empire's is <see cref="LogUtil.DebugGate"/>; submods point theirs at their own debug setting)
/// and takes a <see cref="DebugLogHandler{TGate}"/> of that struct.
/// </summary>
public interface IDebugLogGate
{
    static abstract bool Enabled { get; }
}

/// <summary>
/// Interpolated-string handler behind debug <c>Message($"...")</c> overloads such as
/// <see cref="LogUtil.Message(ref DebugLogHandler{LogUtil.DebugGate})"/>. When <typeparamref name="TGate"/>
/// reports logging off, the constructor reports <c>shouldAppend = false</c>, so the compiler skips every
/// Append call: the holes are never evaluated and no string is built. Holes must therefore not have side
/// effects the game relies on. When logging is on, formatting is delegated to
/// <see cref="DefaultInterpolatedStringHandler"/>, so the output is identical to an ordinary interpolated
/// string.
/// </summary>
/// <typeparam name="TGate">The logger's gate, which decides whether the message is built at all.</typeparam>
[InterpolatedStringHandler]
public ref struct DebugLogHandler<TGate> where TGate : IDebugLogGate
{
    private DefaultInterpolatedStringHandler inner;

    /// <summary>True when the message will be logged; false means nothing was appended.</summary>
    public bool Enabled { get; }

    public DebugLogHandler(int literalLength, int formattedCount, out bool shouldAppend)
    {
        Enabled = shouldAppend = TGate.Enabled;
        inner = Enabled ? new DefaultInterpolatedStringHandler(literalLength, formattedCount) : default;
    }

    public void AppendLiteral(string value) => inner.AppendLiteral(value);

    public void AppendFormatted<T>(T value) => inner.AppendFormatted(value);
    public void AppendFormatted<T>(T value, string format) => inner.AppendFormatted(value, format);
    public void AppendFormatted<T>(T value, int alignment) => inner.AppendFormatted(value, alignment);
    public void AppendFormatted<T>(T value, int alignment, string format) => inner.AppendFormatted(value, alignment, format);

    public void AppendFormatted(ReadOnlySpan<char> value) => inner.AppendFormatted(value);
    public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string format = null) => inner.AppendFormatted(value, alignment, format);

    public void AppendFormatted(string value) => inner.AppendFormatted(value);
    public void AppendFormatted(string value, int alignment = 0, string format = null) => inner.AppendFormatted(value, alignment, format);

    public void AppendFormatted(object value, int alignment = 0, string format = null) => inner.AppendFormatted(value, alignment, format);

    /// <summary>Returns the built message and releases the pooled buffer. Only valid when <see cref="Enabled"/>.</summary>
    public string ToStringAndClear() => inner.ToStringAndClear();
}
