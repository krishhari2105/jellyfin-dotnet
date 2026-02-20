using System;
using System.Diagnostics;

namespace JellyfinTizen.Utils
{
    public static class PerfTrace
    {
        public static bool Enabled { get; set; } = false;

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
