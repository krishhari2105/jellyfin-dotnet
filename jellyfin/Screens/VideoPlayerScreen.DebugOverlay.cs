using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using JellyfinTizen.Core;
using JellyfinTizen.Models;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace JellyfinTizen.Screens
{
    public partial class VideoPlayerScreen
    {
        private const int StreamDebugOverlayMaxEntries = 4;

        private View _streamDebugOverlay;
        private TextLabel _streamDebugOverlayLabel;
        private readonly List<string> _streamDebugEntries = new();
        private string _currentTranscodeReason = "Not available yet";
        private string _currentSanitizedStreamUrl = string.Empty;
        private string _currentJellyfinTranscodingUrl = "null";
        private bool _streamDebugOverlayVisible = DebugSwitches.EnablePlaybackDebugOverlay;

        private void CreateStreamDebugOverlay()
        {
            if (!DebugSwitches.EnablePlaybackDebugOverlay)
            {
                HideStreamDebugOverlay();
                return;
            }

            if (_streamDebugOverlay != null)
                return;

            int screenWidth = Window.Default.Size.Width;
            int overlayWidth = Math.Clamp(screenWidth - 120, 760, 1320);
            const int overlayHeight = 320;

            _streamDebugOverlay = new View
            {
                PositionX = 40,
                PositionY = 44,
                WidthSpecification = overlayWidth,
                HeightSpecification = overlayHeight,
                BackgroundColor = new Color(0f, 0f, 0f, 0.72f),
                CornerRadius = 10.0f,
                ClippingMode = ClippingModeType.ClipChildren
            };

            _streamDebugOverlayLabel = new TextLabel(string.Empty)
            {
                PositionX = 16,
                PositionY = 12,
                WidthSpecification = overlayWidth - 32,
                HeightSpecification = overlayHeight - 24,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word,
                PointSize = 20f,
                TextColor = new Color(0.92f, 0.98f, 1f, 0.96f),
                HorizontalAlignment = HorizontalAlignment.Begin,
                VerticalAlignment = VerticalAlignment.Top,
                EnableMarkup = false
            };

            _streamDebugOverlay.Add(_streamDebugOverlayLabel);
            Add(_streamDebugOverlay);

            if (!_streamDebugOverlayVisible)
                _streamDebugOverlay.Hide();

            RefreshStreamDebugOverlay();
        }

        private void ToggleStreamDebugOverlay()
        {
            if (!DebugSwitches.EnablePlaybackDebugOverlay)
            {
                HideStreamDebugOverlay();
                return;
            }

            _streamDebugOverlayVisible = !_streamDebugOverlayVisible;

            if (_streamDebugOverlay == null)
            {
                if (_streamDebugOverlayVisible)
                    CreateStreamDebugOverlay();
                return;
            }

            if (_streamDebugOverlayVisible)
            {
                _streamDebugOverlay.Show();
                _streamDebugOverlay.RaiseToTop();
                RefreshStreamDebugOverlay();
            }
            else
            {
                _streamDebugOverlay.Hide();
            }
        }

        private void CaptureStreamDebugEntry(string streamUrl, MediaSourceInfo mediaSource, bool forceTranscode, bool supportsDirectPlay, bool supportsTranscoding, bool hasTranscodeUrl)
        {
            if (!DebugSwitches.EnablePlaybackDebugOverlay)
            {
                HideStreamDebugOverlay();
                return;
            }

            _currentJellyfinTranscodingUrl = SanitizeDebugStreamUrl(mediaSource?.TranscodingUrl);
            if (string.IsNullOrWhiteSpace(_currentJellyfinTranscodingUrl))
                _currentJellyfinTranscodingUrl = "null";

            _currentSanitizedStreamUrl = SanitizeDebugStreamUrl(streamUrl);
            _currentTranscodeReason = BuildTranscodeReasonText(mediaSource, forceTranscode, supportsDirectPlay, supportsTranscoding, hasTranscodeUrl);

            string timestamp = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            string formattedUrl = FormatDebugStreamUrl(_currentSanitizedStreamUrl);
            string sourceType = hasTranscodeUrl ? "JellyfinTranscodingUrl" : "ManualMasterM3u8Fallback";
            _streamDebugEntries.Add($"{timestamp} | {_playMethod} | Source={sourceType}\nJellyfin TranscodingUrl: {_currentJellyfinTranscodingUrl}\nFinal Stream URL:\n{formattedUrl}");
            while (_streamDebugEntries.Count > StreamDebugOverlayMaxEntries)
                _streamDebugEntries.RemoveAt(0);

            RefreshStreamDebugOverlay();
        }

        private void RefreshStreamDebugOverlay()
        {
            if (!DebugSwitches.EnablePlaybackDebugOverlay)
            {
                HideStreamDebugOverlay();
                return;
            }

            if (!_streamDebugOverlayVisible || _streamDebugOverlayLabel == null)
                return;

            var sb = new StringBuilder();
            sb.Append("TEMP STREAM DEBUG  (MediaPrevious toggles)");
            sb.Append('\n');
            sb.Append("Method: ");
            sb.Append(string.IsNullOrWhiteSpace(_playMethod) ? "Unknown" : _playMethod);
            sb.Append('\n');
            sb.Append("Reason: ");
            sb.Append(string.IsNullOrWhiteSpace(_currentTranscodeReason) ? "Unknown" : _currentTranscodeReason);
            sb.Append('\n');
            sb.Append("Jellyfin TranscodingUrl: ");
            sb.Append(_currentJellyfinTranscodingUrl);
            sb.Append('\n');
            sb.Append("Session: ");
            sb.Append(string.IsNullOrWhiteSpace(_playSessionId) ? "-" : _playSessionId);
            sb.Append('\n');
            sb.Append("MediaSource: ");
            sb.Append(_currentMediaSource?.Id ?? "-");
            sb.Append("\nCurrent URL:\n");
            sb.Append(string.IsNullOrWhiteSpace(_currentSanitizedStreamUrl) ? "-" : FormatDebugStreamUrl(_currentSanitizedStreamUrl));
            sb.Append("\n\nRecent stream URL logs:");

            if (_streamDebugEntries.Count == 0)
            {
                sb.Append("\n  (waiting for playback start)");
            }
            else
            {
                for (int i = _streamDebugEntries.Count - 1; i >= 0; i--)
                {
                    int indexFromNewest = _streamDebugEntries.Count - i;
                    sb.Append('\n');
                    sb.Append('[');
                    sb.Append(indexFromNewest);
                    sb.Append("]\n");
                    sb.Append(_streamDebugEntries[i]);
                    if (i > 0)
                        sb.Append("\n");
                }
            }

            _streamDebugOverlayLabel.Text = sb.ToString();
            _streamDebugOverlay.RaiseToTop();
        }

        private void HideStreamDebugOverlay()
        {
            _streamDebugOverlayVisible = false;
            _streamDebugOverlay?.Hide();
            _streamDebugEntries.Clear();
        }

        private string BuildTranscodeReasonText(MediaSourceInfo mediaSource, bool forceTranscode, bool supportsDirectPlay, bool supportsTranscoding, bool hasTranscodeUrl)
        {
            var reasons = new List<string>();

            if (_overrideAudioIndex.HasValue)
                reasons.Add($"Audio stream override requested ({_overrideAudioIndex.Value})");

            if (_burnIn && _initialSubtitleIndex.HasValue)
                reasons.Add("Subtitle burn-in requested");

            if (mediaSource?.TranscodingReasons != null)
            {
                foreach (var reason in mediaSource.TranscodingReasons)
                {
                    if (!string.IsNullOrWhiteSpace(reason))
                        reasons.Add(HumanizeTranscodeReason(reason));
                }
            }

            if (string.Equals(_playMethod, "DirectPlay", StringComparison.OrdinalIgnoreCase))
            {
                if (reasons.Count == 0)
                    return "DirectPlay selected";

                var directPlayHints = string.Join(", ", reasons.Distinct(StringComparer.OrdinalIgnoreCase));
                return $"DirectPlay selected (hints: {directPlayHints})";
            }

            if (!supportsDirectPlay)
                reasons.Add("DirectPlay not supported");
            if (!supportsTranscoding && forceTranscode)
                reasons.Add("Client forced transcode");
            if (hasTranscodeUrl)
                reasons.Add("Server provided TranscodingUrl");
            else
                reasons.Add("Server TranscodingUrl was null (client fallback URL builder used)");
            if (reasons.Count == 0)
                reasons.Add("Server selected transcoding");

            return string.Join(", ", reasons.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static string HumanizeTranscodeReason(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string humanized = value.Trim().Replace('_', ' ');
            humanized = Regex.Replace(humanized, "([a-z])([A-Z])", "$1 $2");
            return humanized;
        }

        private static string SanitizeDebugStreamUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return string.Empty;

            string cleaned = url.Trim().Replace("\r", string.Empty).Replace("\n", string.Empty);
            return Regex.Replace(cleaned, "([?&](?:api_key|apikey|token)=)[^&]+", "$1<redacted>", RegexOptions.IgnoreCase);
        }

        private static string FormatDebugStreamUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return "(empty)";

            int queryStart = url.IndexOf('?');
            if (queryStart < 0 || queryStart == url.Length - 1)
                return url;

            string baseUrl = url.Substring(0, queryStart);
            string query = url.Substring(queryStart + 1);
            string[] parts = query.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return baseUrl;

            const int maxParamsToShow = 14;
            int shown = Math.Min(parts.Length, maxParamsToShow);

            var sb = new StringBuilder(baseUrl);
            for (int i = 0; i < shown; i++)
            {
                sb.Append('\n');
                sb.Append("    ");
                sb.Append(i == 0 ? "? " : "& ");
                sb.Append(parts[i]);
            }

            if (parts.Length > shown)
            {
                sb.Append('\n');
                sb.Append("    ... +");
                sb.Append(parts.Length - shown);
                sb.Append(" more query params");
            }

            return sb.ToString();
        }
    }
}
