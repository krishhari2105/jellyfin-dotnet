using System;
using System.Globalization;

namespace JellyfinTizen.Core
{
    /// <summary>
    /// Playback capabilities for this Tizen 9-only application.
    /// Runtime platform detection is retained for diagnostics, but it does not disable
    /// features known to work on the target television when the lookup is unavailable.
    /// </summary>
    public sealed class TizenPlaybackCapabilities
    {
        private const double DeclaredPlatformVersion = 9.0;
        private static readonly Lazy<TizenPlaybackCapabilities> CurrentCapabilities =
            new Lazy<TizenPlaybackCapabilities>(Detect);

        private TizenPlaybackCapabilities(double platformVersion, bool usedFallback)
        {
            PlatformVersion = platformVersion;
            UsedFallback = usedFallback;
        }

        public static TizenPlaybackCapabilities Current => CurrentCapabilities.Value;

        public double PlatformVersion { get; }

        public bool UsedFallback { get; }

        public bool SupportsFmp4Hls => true;

        public bool SupportsHevcInMpegTsHls => true;

        public bool SupportsEac3InHls => true;

        public bool SupportsAv1 => true;

        public bool SupportsVc1 => false;

        public int MaxHlsAudioChannels => 6;

        private static TizenPlaybackCapabilities Detect()
        {
            try
            {
                if (Tizen.System.Information.TryGetValue<string>(
                        "http://tizen.org/feature/platform.version",
                        out var rawVersion) &&
                    TryParsePlatformVersion(rawVersion, out var platformVersion))
                {
                    return new TizenPlaybackCapabilities(platformVersion, usedFallback: false);
                }
            }
            catch (Exception ex)
            {
                AppDebugLog.Add(
                    $"PlaybackProfile.RuntimeCapability: platform-version lookup failed ({ex.GetType().Name})");
            }

            return new TizenPlaybackCapabilities(DeclaredPlatformVersion, usedFallback: true);
        }

        private static bool TryParsePlatformVersion(string value, out double platformVersion)
        {
            platformVersion = 0;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            value = value.Trim();
            if (double.TryParse(
                    value,
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out platformVersion))
            {
                return platformVersion >= 2.0;
            }

            if (Version.TryParse(value, out var version))
            {
                platformVersion = version.Major + (version.Minor / 10.0);
                return platformVersion >= 2.0;
            }

            return false;
        }
    }
}
