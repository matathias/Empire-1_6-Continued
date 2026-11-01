using System;
using System.Collections.Generic;

namespace FactionColonies
{
    public class TestFailedException : Exception
    {
        public TestFailedException(string message) : base(message) { }
    }

    public class TestSkippedException : Exception
    {
        public TestSkippedException(string reason) : base(reason) { }
    }

    public static class TestAssert
    {
        public static void Skip(string reason)
        {
            throw new TestSkippedException(reason);
        }

        public static void AreEqual(double expected, double actual, double tolerance = 0.001, string message = null)
        {
            if (Math.Abs(expected - actual) > tolerance)
                throw new TestFailedException(
                    $"Expected {expected}, got {actual}" + FormatMessage(message));
        }

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
            if (obj != null)
                throw new TestFailedException(message ?? $"Expected null, got {obj}");
        }

        public static void IsNotNull(object obj, string message = null)
        {
            if (obj == null)
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
            message != null ? $" -- {message}" : "";
    }
}
