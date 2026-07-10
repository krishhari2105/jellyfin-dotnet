using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Models;

namespace JellyfinTizen.Screens
{
    public static class DetailsScreenHelpers
    {
        // Dolby chip constants (used by both screens)
        public const string DolbyAudioChipPrefix = "__DOLBY_AUDIO__:";
        public const string DolbyVisionChipToken = "__DOLBY_VISION_ICON__";

        // Action button constants (identical in both MovieDetailsScreen and EpisodeDetailsScreen)
        public const int ActionButtonHeight = 70;
        public const int ActionButtonRowGap = 28;
        public const int SecondaryActionButtonWidth = 240;
        public const int PlayActionButtonWidth = 176;
        public const int IconActionButtonWidth = 122;
        public const int ActionButtonIconLabelGap = 6;
        public const int PlayActionButtonIconSize = 46;
        public const int AudioActionButtonIconSize = 36;
        public const int SubtitleActionButtonIconSize = 34;
        public const int DetailsHorizontalPadding = 90;
        public const int DetailsColumnGap = 60;

        // Action button layout helpers
        public static View CreateButtonRow()
        {
            return new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 100,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    CellPadding = new Size2D(42, 0)
                }
            };
        }

        public static int EstimateActionButtonWidth(View button)
        {
            if (button == null)
                return 0;

            int actualWidth = (int)Math.Round(button.SizeWidth);
            if (actualWidth > 0)
                return actualWidth;

            int specifiedWidth = (int)Math.Round((double)(float)button.WidthSpecification);
            if (specifiedWidth > 0)
                return specifiedWidth;

            string buttonText = FindActionButtonText(button);
            bool hasIcon = ContainsActionButtonIcon(button);
            int iconWidth = hasIcon ? 46 : 0;
            int textWidth = EstimateActionButtonTextWidth(buttonText);
            int contentGap = hasIcon && !string.IsNullOrWhiteSpace(buttonText) ? 14 : 0;
            int paddingWidth = 68;
            int estimatedWidth = paddingWidth + iconWidth + contentGap + textWidth;
            return Math.Clamp(estimatedWidth, 180, 620);
        }

        public static int EstimateActionButtonTextWidth(string text)
        {
            int length = string.IsNullOrWhiteSpace(text) ? 0 : text.Trim().Length;
            if (length <= 0)
                return 0;

            return Math.Clamp(40 + (length * 15), 80, 360);
        }

        public static string FindActionButtonText(View view)
        {
            if (view == null)
                return null;

            if (view is TextLabel label && !string.IsNullOrWhiteSpace(label.Text))
                return label.Text;

            uint childCount = view.ChildCount;
            for (uint i = 0; i < childCount; i++)
            {
                if (view.GetChildAt(i) is View child)
                {
                    string childText = FindActionButtonText(child);
                    if (!string.IsNullOrWhiteSpace(childText))
                        return childText;
                }
            }

            return null;
        }

        public static bool ContainsActionButtonIcon(View view)
        {
            if (view == null)
                return false;

            if (view is ImageView)
                return true;

            uint childCount = view.ChildCount;
            for (uint i = 0; i < childCount; i++)
            {
                if (view.GetChildAt(i) is View child && ContainsActionButtonIcon(child))
                    return true;
            }

            return false;
        }

        public static void ClearRowChildren(View row)
        {
            while (row != null && row.ChildCount > 0)
            {
                var child = row.GetChildAt(0);
                row.Remove(child);
            }
        }

        public static bool HasChild(View parent, View child)
        {
            if (parent == null || child == null)
                return false;

            foreach (var existing in parent.Children)
            {
                if (ReferenceEquals(existing, child))
                    return true;
            }

            return false;
        }

        // Media source/stream helpers
        public static int? ResolveDefaultAudioStreamIndex(List<MediaStream> audioStreams)
        {
            if (audioStreams == null || audioStreams.Count == 0)
                return null;

            var defaultStream = audioStreams.FirstOrDefault(s => s.IsDefault);
            return (defaultStream ?? audioStreams[0]).Index;
        }

        public static string GetMediaSourceDisplayName(MediaSourceInfo source, int fallbackIndex)
        {
            if (source == null)
                return $"Source {fallbackIndex}";
            if (!string.IsNullOrWhiteSpace(source.Name))
                return source.Name.Trim();
            return $"Source {fallbackIndex}";
        }

        public static int TicksToMs(long ticks)
        {
            if (ticks <= 0)
                return 0;
            var ms = ticks / 10000;
            return (int)Math.Clamp(ms, 0, int.MaxValue);
        }

        public static string BuildSummaryText(JellyfinMovie media)
        {
            if (media == null)
                return string.Empty;

            var parts = new List<string>();

            if (media.ProductionYear > 0)
                parts.Add(media.ProductionYear.ToString(CultureInfo.InvariantCulture));

            var runtime = FormatRuntimeForMetadata(media.RunTimeTicks);
            if (!string.IsNullOrWhiteSpace(runtime))
                parts.Add(runtime);

            if (!string.IsNullOrWhiteSpace(media.OfficialRating))
                parts.Add(media.OfficialRating.Trim());

            return string.Join("  ", parts);
        }

        public static string FormatRuntimeForMetadata(long ticks)
        {
            if (ticks <= 0)
                return null;

            var totalMinutes = (int)Math.Round(TimeSpan.FromTicks(ticks).TotalMinutes, MidpointRounding.AwayFromZero);
            if (totalMinutes <= 0)
                return null;

            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;

            if (hours <= 0)
                return $"{totalMinutes}m";
            if (minutes == 0)
                return $"{hours}h";

            return $"{hours}h {minutes}m";
        }

        public static List<string> BuildTechnicalTags(MediaSourceInfo source, bool useFallbackForResolution = false)
        {
            var tags = new List<string>();
            if (source?.MediaStreams == null || source.MediaStreams.Count == 0)
                return tags;

            MediaStream videoStream = null;
            MediaStream audioStream = null;

            foreach (var stream in source.MediaStreams)
            {
                if (stream == null)
                    continue;

                if (videoStream == null &&
                    string.Equals(stream.Type, "Video", StringComparison.OrdinalIgnoreCase))
                {
                    videoStream = stream;
                }
                else if (audioStream == null &&
                         string.Equals(stream.Type, "Audio", StringComparison.OrdinalIgnoreCase))
                {
                    audioStream = stream;
                }
            }

            AddMetadataTag(tags, GetResolutionTag(videoStream, useFallbackForResolution));
            AddMetadataTag(tags, GetVideoCodecTag(videoStream?.Codec));
            AddMetadataTag(tags, GetDolbyVisionChipTag(videoStream) ?? GetHdrTag(videoStream));
            AddMetadataTag(tags, GetAudioCodecTag(audioStream));
            AddMetadataTag(tags, GetDolbyAudioChipTag(audioStream) ?? GetAudioChannelTag(audioStream));

            while (tags.Count > 5)
                tags.RemoveAt(tags.Count - 1);

            return tags;
        }

        public static string GetResolutionTag(MediaStream stream)
        {
            return GetResolutionTag(stream, useFallback: false);
        }

        public static string GetResolutionTagWithFallback(MediaStream stream)
        {
            return GetResolutionTag(stream, useFallback: true);
        }

        private static string GetResolutionTag(MediaStream stream, bool useFallback)
        {
            if (stream == null)
                return null;

            var width = stream.Width.GetValueOrDefault();
            var height = stream.Height.GetValueOrDefault();

            if (width >= 3800 || height >= 2000)
                return "4K";
            if (width >= 1900 || height >= 1000)
                return "1080p";
            if (width >= 1200 || height >= 700)
                return "HD";

            if (useFallback)
            {
                var description = GetStreamSearchText(stream);
                if (description.Contains("2160"))
                    return "4K";
                if (description.Contains("1080"))
                    return "1080p";
                if (description.Contains("720"))
                    return "HD";
            }

            return null;
        }

        public static string GetVideoCodecTag(string codec)
        {
            if (string.IsNullOrWhiteSpace(codec))
                return null;

            var normalized = codec.Trim().ToLowerInvariant();

            if (normalized.Contains("hevc") || normalized.Contains("h265") || normalized.Contains("x265"))
                return "HEVC";
            if (normalized.Contains("h264") || normalized.Contains("avc") || normalized.Contains("x264"))
                return "H.264";
            if (normalized.Contains("av1"))
                return "AV1";
            if (normalized.Contains("vp9"))
                return "VP9";

            return codec.Trim().ToUpperInvariant();
        }

        public static string GetHdrTag(MediaStream stream)
        {
            var text = GetStreamSearchText(stream);
            if (string.IsNullOrWhiteSpace(text))
                return null;

            if (text.Contains("hdr10+"))
                return "HDR10+";
            if (text.Contains("hdr10"))
                return "HDR10";
            if (text.Contains("hlg"))
                return "HLG";
            if (text.Contains("hdr"))
                return "HDR";

            return null;
        }

        public static string GetAudioCodecTag(MediaStream stream)
        {
            if (stream == null)
                return null;

            var text = GetStreamSearchText(stream);
            var codec = stream.Codec?.ToLowerInvariant() ?? string.Empty;

            if (text.Contains("dolby digital plus") || text.Contains("eac3") || codec.Contains("eac3"))
                return "Dolby Digital+";
            if (text.Contains("dolby digital") || codec == "ac3" || codec.Contains("ac3"))
                return "Dolby Digital";
            if (text.Contains("truehd") || codec.Contains("truehd"))
                return "TrueHD";
            if (text.Contains("dts") || codec.Contains("dts"))
                return "DTS";
            if (text.Contains("aac") || codec.Contains("aac"))
                return "AAC";
            if (text.Contains("flac") || codec.Contains("flac"))
                return "FLAC";
            if (text.Contains("opus") || codec.Contains("opus"))
                return "Opus";
            if (text.Contains("mp3") || codec.Contains("mp3"))
                return "MP3";

            if (!string.IsNullOrWhiteSpace(stream.Codec))
                return stream.Codec.Trim().ToUpperInvariant();

            return null;
        }

        public static string GetDolbyVisionChipTag(MediaStream stream)
        {
            var text = GetStreamSearchText(stream);
            if (string.IsNullOrWhiteSpace(text))
                return null;

            if (text.Contains("dolby vision") || text.Contains("dovi") || text.Contains("dvhe"))
                return DolbyVisionChipToken;

            return null;
        }

        public static string GetDolbyAudioChipTag(MediaStream stream)
        {
            if (stream == null)
                return null;

            var text = GetStreamSearchText(stream);
            var codec = stream.Codec?.ToLowerInvariant() ?? string.Empty;
            bool isDolbyAudio = text.Contains("dolby") ||
                                codec.Contains("eac3") ||
                                codec.Contains("ac3") ||
                                codec.Contains("truehd");
            if (!isDolbyAudio)
                return null;

            if (text.Contains("atmos"))
                return $"{DolbyAudioChipPrefix}Atmos";

            var channels = GetAudioChannelTag(stream);
            if (channels == "5.1" || channels == "7.1")
                return $"{DolbyAudioChipPrefix}{channels}";

            return null;
        }

        public static string GetAudioChannelTag(MediaStream stream)
        {
            if (stream == null)
                return null;

            if (!string.IsNullOrWhiteSpace(stream.ChannelLayout))
            {
                var layout = stream.ChannelLayout.ToLowerInvariant();

                if (layout.Contains("7.1"))
                    return "7.1";
                if (layout.Contains("6.1"))
                    return "6.1";
                if (layout.Contains("5.1"))
                    return "5.1";
                if (layout.Contains("2.0") || layout.Contains("stereo"))
                    return "2.0";
                if (layout.Contains("1.0") || layout.Contains("mono"))
                    return "1.0";
            }

            if (stream.Channels.HasValue && stream.Channels.Value > 0)
            {
                return stream.Channels.Value switch
                {
                    8 => "7.1",
                    7 => "6.1",
                    6 => "5.1",
                    2 => "2.0",
                    1 => "1.0",
                    _ => $"{stream.Channels.Value}.0"
                };
            }

            var text = GetStreamSearchText(stream);
            if (text.Contains("7.1"))
                return "7.1";
            if (text.Contains("6.1"))
                return "6.1";
            if (text.Contains("5.1"))
                return "5.1";
            if (text.Contains("2.0") || text.Contains("stereo"))
                return "2.0";

            return null;
        }

        public static string GetStreamSearchText(MediaStream stream)
        {
            if (stream == null)
                return string.Empty;

            return $"{stream.DisplayTitle} {stream.VideoRange} {stream.ChannelLayout} {stream.Codec}".ToLowerInvariant();
        }

        public static void AddMetadataTag(List<string> tags, string value)
        {
            if (tags == null || string.IsNullOrWhiteSpace(value))
                return;

            var normalized = value.Trim();
            foreach (var existing in tags)
            {
                if (string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            tags.Add(normalized);
        }

        public static string FormatSubtitleStreamOption(MediaStream stream)
        {
            if (stream == null)
                return "Unknown";

            string lang = string.IsNullOrWhiteSpace(stream.Language) ? "UNKNOWN" : stream.Language.ToUpperInvariant();
            string title = string.IsNullOrWhiteSpace(stream.DisplayTitle) ? $"Sub {stream.Index}" : stream.DisplayTitle;
            string label = $"{lang} | {title}";
            if (stream.IsExternal)
                label += " (Ext)";
            return label;
        }

        public static string FormatAudioStreamOption(MediaStream stream)
        {
            if (stream == null)
                return "UNKNOWN | AUDIO";

            string lang = string.IsNullOrWhiteSpace(stream.Language) ? "UNKNOWN" : stream.Language.ToUpperInvariant();
            string codec = string.IsNullOrWhiteSpace(stream.Codec) ? "AUDIO" : stream.Codec.ToUpperInvariant();
            return $"{lang} | {codec}";
        }

        public static string FormatAudioButtonLabel(MediaStream stream)
        {
            if (stream == null)
                return "Default";

            string codec = string.IsNullOrWhiteSpace(stream.Codec) ? "Audio" : stream.Codec.ToUpperInvariant();
            string lang = NormalizeLanguageLabel(stream.Language);
            return string.Equals(lang, "Unknown", StringComparison.Ordinal)
                ? codec
                : $"{lang} | {codec}";
        }

        public static string NormalizeLanguageLabel(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
                return "Unknown";

            string trimmed = language.Trim();
            if (trimmed.Length == 1)
                return trimmed.ToUpperInvariant();

            return char.ToUpper(trimmed[0], CultureInfo.InvariantCulture) + trimmed.Substring(1);
        }

        public static string BuildEpisodeTitle(JellyfinMovie episode)
        {
            if (episode == null)
                return string.Empty;

            if (episode.ParentIndexNumber > 0 && episode.IndexNumber > 0)
                return $"S{episode.ParentIndexNumber}:E{episode.IndexNumber} - {episode.Name}";

            if (episode.IndexNumber > 0)
                return $"E{episode.IndexNumber} - {episode.Name}";

            return episode.Name ?? string.Empty;
        }
    }
}