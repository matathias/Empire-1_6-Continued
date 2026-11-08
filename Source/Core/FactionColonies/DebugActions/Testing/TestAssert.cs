namespace FactionColonies;

public class TestFailedException : Exception
{
    public TestFailedException(string message) : base(message) { }
}

public static class TestAssert
{
    /// <summary>Fails unconditionally. Use when a test cannot force its own starting conditions:
    /// tests never skip (only the runner does, on a game-state mismatch), so setup that cannot be
    /// built is a failure the test author must see.</summary>
    public static void Fail(string message)
    {
        throw new TestFailedException(message);
    }

    /// <summary>Returns <paramref name="obj"/> after asserting it is non-null; for one-line setup
    /// such as <c>var s = TestAssert.NotNull(DestructiveTestUtil.EnsureSettlement(), "...")</c>.</summary>
    public static T NotNull<T>(T obj, string message) where T : class
    {
        if (obj is null) throw new TestFailedException(message);
        return obj;
    }

    public static void AreEqual(double expected, double actual, double tolerance = 0.001, string message = null)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new TestFailedException(
                $"Expected {expected}, got {actual}" + FormatMessage(message));
    }

    /// <summary>
    /// Numeric comparison for a call written as <c>AreEqual(-120, someFloat, "message")</c>: without
    /// this overload the string argument rules out the tolerance overload, and the call would bind to
    /// the <see cref="object"/> one, which compares a boxed int with a boxed float and never matches.
    /// </summary>
    public static void AreEqual(double expected, double actual, string message) =>
        AreEqual(expected, actual, 0.001, message);

    public static void AreEqual(int expected, int actual, string message = null)
    {
        if (expected != actual)
            throw new TestFailedException(
                $"Expected {expected}, got {actual}" + FormatMessage(message));
    }

    public static void AreEqual(object expected, object actual, string message = null)
    {
        if (!Equals(expected, actual))
            throw new TestFailedException(
                $"Expected {expected}, got {actual}" + FormatMessage(message));
    }

    public static void IsTrue(bool condition, string message = null)
    {
        if (!condition)
            throw new TestFailedException(message ?? "Expected true, got false");
    }

    public static void IsFalse(bool condition, string message = null)
    {
        if (condition)
            throw new TestFailedException(message ?? "Expected false, got true");
    }

    public static void IsNull(object obj, string message = null)
    {
        if (obj is not null)
            throw new TestFailedException(message ?? $"Expected null, got {obj}");
    }

    public static void IsNotNull(object obj, string message = null)
    {
        if (obj is null)
            throw new TestFailedException(message ?? "Expected non-null, got null");
    }

    public static void GreaterThan(double value, double threshold, string message = null)
    {
        if (value <= threshold)
            throw new TestFailedException(
                $"Expected > {threshold}, got {value}" + FormatMessage(message));
    }

    public static void LessThanOrEqual(double value, double threshold, string message = null)
    {
        if (value > threshold)
            throw new TestFailedException(
                $"Expected <= {threshold}, got {value}" + FormatMessage(message));
    }

    public static void LessThan(double value, double threshold, string message = null)
    {
        if (value >= threshold)
            throw new TestFailedException(
                $"Expected < {threshold}, got {value}" + FormatMessage(message));
    }

    public static void Throws<T>(Action action, string message = null) where T : Exception
    {
        try
        {
            action();
            throw new TestFailedException(
                message ?? $"Expected {typeof(T).Name} but no exception was thrown");
        }
        catch (T) { /* expected */ }
        catch (TestFailedException) { throw; }
        catch (Exception ex)
        {
            throw new TestFailedException(
                $"Expected {typeof(T).Name} but got {ex.GetType().Name}" + FormatMessage(message));
        }
    }

    public static void DoesNotThrow(Action action, string message = null)
    {
        try { action(); }
        catch (Exception ex)
        {
            throw new TestFailedException(
                $"Expected no exception but got {ex.GetType().Name}: {ex.Message}" + FormatMessage(message));
        }
    }

    public static void Contains<T>(IEnumerable<T> collection, T item, string message = null)
    {
        foreach (T element in collection)
            if (Equals(element, item)) return;
        throw new TestFailedException(
            $"Collection does not contain {item}" + FormatMessage(message));
    }

    public static void IsEmpty<T>(IEnumerable<T> collection, string message = null)
    {
        IEnumerator<T> enumerator = collection.GetEnumerator();
        if (enumerator.MoveNext())
            throw new TestFailedException(message ?? "Expected empty collection but it has elements");
    }

    public static void IsNotEmpty<T>(IEnumerable<T> collection, string message = null)
    {
        IEnumerator<T> enumerator = collection.GetEnumerator();
        if (!enumerator.MoveNext())
            throw new TestFailedException(message ?? "Expected non-empty collection but it is empty");
    }

    private static string FormatMessage(string message) =>
        message is not null ? $" -- {message}" : "";
}
