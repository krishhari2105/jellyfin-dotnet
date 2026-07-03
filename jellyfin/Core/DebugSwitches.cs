namespace JellyfinTizen.Core
{
    public static class DebugSwitches
    {
        // Keep disabled in normal playback; enable manually for debug sessions.
        public static bool EnablePlaybackDebugOverlay { get; set; } = true;

        // Keep verbose logs disabled during normal use; enable manually when diagnosing input/Tailscale issues.
        public static bool EnableVerboseDebugLogging { get; set; } = true;
    }
}
