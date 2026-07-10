using System;
using System.Diagnostics;
using JellyfinTizen.Core;

namespace JellyfinTizen.Utils
{
    public static class PerfTrace
    {
        /// <summary>
        /// Returns true if performance tracing is enabled.
        /// Controlled by DebugSwitches.EnableVerboseDebugLogging to keep a single debug switch.
        /// </summary>
        public static bool Enabled => DebugSwitches.EnableVerboseDebugLogging;

        public static long Start()
        {
            if (!Enabled)
                return 0;

            return Stopwatch.GetTimestamp();
        }

        public static void End(string label, long startTimestamp)
        {
            if (!Enabled || startTimestamp == 0)
                return;

            var elapsedMs = (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
            Debug.WriteLine($"[PERF] {label}: {elapsedMs:F1} ms");
        }

        public static void Mark(string message)
        {
            if (!Enabled)
                return;

            Debug.WriteLine($"[PERF] {message}");
        }
    }
}
