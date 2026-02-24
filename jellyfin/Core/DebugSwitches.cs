namespace JellyfinTizen.Core
{
    public static class DebugSwitches
    {
        // Global switch for temporary playback debug capture + overlay.
        // Set true only while actively debugging playback URL/decision issues.
        public static bool EnablePlaybackDebugOverlay { get; set; } = false;
    }
}
