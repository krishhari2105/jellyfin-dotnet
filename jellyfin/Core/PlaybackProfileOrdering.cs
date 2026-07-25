using System;
using System.Collections.Generic;
using JellyfinTizen.Models;

namespace JellyfinTizen.Core
{
    /// <summary>
    /// Server-specific playback profile ordering rules.
    /// </summary>
    public static class PlaybackProfileOrdering
    {
        public static bool ShouldPreferFmp4Hls(
            bool isEmby,
            string sourceVideoCodec)
        {
            if (!isEmby || string.IsNullOrWhiteSpace(sourceVideoCodec))
                return false;

            string normalizedCodec =
                sourceVideoCodec.Trim().ToLowerInvariant();
            return string.Equals(
                       normalizedCodec,
                       "av1",
                       StringComparison.Ordinal) ||
                   string.Equals(
                       normalizedCodec,
                       "vp9",
                       StringComparison.Ordinal);
        }

        public static List<TranscodingProfile> PreferFmp4HlsFirst(
            IEnumerable<TranscodingProfile> profiles)
        {
            var fmp4Profiles = new List<TranscodingProfile>();
            var otherProfiles = new List<TranscodingProfile>();

            if (profiles == null)
                return otherProfiles;

            foreach (var profile in profiles)
            {
                if (profile != null &&
                    string.Equals(
                        profile.Type,
                        "Video",
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        profile.Protocol,
                        "hls",
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        profile.Container,
                        "mp4",
                        StringComparison.OrdinalIgnoreCase))
                {
                    fmp4Profiles.Add(profile);
                }
                else
                {
                    otherProfiles.Add(profile);
                }
            }

            fmp4Profiles.AddRange(otherProfiles);
            return fmp4Profiles;
        }
    }
}
