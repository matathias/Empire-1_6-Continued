namespace FactionColonies;

public static class TestUtil
{
    /// <summary>
    /// Runs <paramref name="body"/> with a setting (or any other global) temporarily set to
    /// <paramref name="value"/>, then restores the value it had before, even if the body throws.
    /// Typical use: <c>TestUtil.WithOverride(() => FCSettings.x, v => FCSettings.x = v, 1f, () => { ... })</c>.
    /// </summary>
    public static void WithOverride<T>(Func<T> get, Action<T> set, T value, Action body)
    {
        T original = get();
        set(value);
        try { body(); }
        finally { set(original); }
    }
}
