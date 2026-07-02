using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JellyfinTizen.Core
{
    public static class TailscaleDebugLog
    {
        private static readonly ConcurrentQueue<string> _entries = new();
        private const int MaxEntries = 60;

        public static event Action LogAdded;

        public static void Add(string message)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                string entry = $"{timestamp} | {message}";
                _entries.Enqueue(entry);
                
                while (_entries.Count > MaxEntries)
                {
                    _entries.TryDequeue(out _);
                }

                try { System.Console.WriteLine($"[TailscaleDebug] {entry}"); } catch { }

                LogAdded?.Invoke();
            }
            catch { }
        }

        public static string[] GetAllLines()
        {
            return _entries.ToArray();
        }

        public static string GetRecentLines(int count = 12)
        {
            var recent = _entries.ToArray();
            int start = Math.Max(0, recent.Length - count);
            var sb = new StringBuilder();
            
            for (int i = start; i < recent.Length; i++)
            {
                sb.AppendLine(recent[i]);
            }

            if (recent.Length == 0)
            {
                sb.AppendLine("(no logs yet)");
            }

            return sb.ToString();
        }
    }
}