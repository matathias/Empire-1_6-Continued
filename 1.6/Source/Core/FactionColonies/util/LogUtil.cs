using System;
using Verse;

namespace FactionColonies
{
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

        public static void Message(string message)
        {
            if (suppressThisThread) return;
            if (FCSettings.PrintDebug)
            {
                Log.Message($"{slug} {message}");
            }
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
    }
}
