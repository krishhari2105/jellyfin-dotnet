using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using JellyfinTizen.Core;
using JellyfinTizen.Models;
using JellyfinTizen.UI;
using JellyfinTizen.Utils;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using Tizen.NUI.Components;

namespace JellyfinTizen.Screens
{
    public partial class VideoPlayerScreen
    {
        private const int PlaybackInfoOverlayLeftMargin = 44;
        private const int PlaybackInfoOverlayVerticalMargin = 72;
        private const int PlaybackInfoOverlayMinWidth = 460;
        private const int PlaybackInfoOverlayMaxWidth = 560;
        private const int PlaybackInfoScrollStepPx = 88;
        private const int PlaybackInfoLabelWidth = 176;
        private const int PlaybackInfoHorizontalPadding = 16;
        private const int PlaybackInfoSectionSpacing = 18;
        private const int PlaybackInfoRowHeight = 34;
        private const int PlaybackInfoWrappedRowHeight = 60;
        private const int PlaybackInfoLongValueWrapThreshold = 24;
        private const float PlaybackInfoSectionTitleSize = 24f;
        private const float PlaybackInfoRowLabelSize = 20f;
        private const float PlaybackInfoRowValueSize = 21f;
        private const float PlaybackInfoBodyTextSize = 20f;

        private View _playbackInfoOverlay;
        private ScrollableBase _playbackInfoScrollView;
        private View _playbackInfoListContainer;
        private bool _playbackInfoOverlayVisible;
        private Animation _playbackInfoOverlayAnimation;
        private int _playbackInfoOverlayBaseX;
        private int _playbackInfoOverlayHeight;
        private int _playbackInfoScrollTop;

        private void CreatePlaybackInfoOverlay()
        {
            int overlayWidth = Math.Clamp(Window.Default.Size.Width / 3, PlaybackInfoOverlayMinWidth, PlaybackInfoOverlayMaxWidth);
            _playbackInfoOverlayHeight = Math.Max(620, Window.Default.Size.Height - (PlaybackInfoOverlayVerticalMargin * 2));
            int overlayTop = Math.Max(36, (Window.Default.Size.Height - _playbackInfoOverlayHeight) / 2);
            _playbackInfoOverlayBaseX = PlaybackInfoOverlayLeftMargin;
            _playbackInfoOverlay = new View
            {
                WidthSpecification = overlayWidth,
                HeightSpecification = _playbackInfoOverlayHeight,
                BackgroundColor = MonochromeAuthFactory.PanelFallbackColor,
                PositionX = _playbackInfoOverlayBaseX - OverlaySlideDistance,
                PositionY = overlayTop,
                CornerRadius = MonochromeAuthFactory.PanelCornerRadius,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BorderlineWidth = MonochromeAuthFactory.PanelBorderWidth,
                BorderlineColor = MonochromeAuthFactory.PanelFallbackBorder,
                ClippingMode = ClippingModeType.ClipChildren,
                Opacity = 0.0f
            };

            _playbackInfoOverlay.Add(new TextLabel("Playback Info")
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = UiTheme.PlayerOverlayHeaderHeight,
                PointSize = UiTheme.PlayerOverlayHeader,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });

            _playbackInfoScrollView = new ScrollableBase
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = _playbackInfoOverlayHeight - UiTheme.PlayerOverlayHeaderHeight,
                PositionY = UiTheme.PlayerOverlayHeaderHeight,
                ScrollingDirection = ScrollableBase.Direction.Vertical,
                BackgroundColor = Color.Transparent
            };

            _playbackInfoListContainer = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding = new Size2D(0, 6)
                }
            };

            _playbackInfoScrollView.Add(_playbackInfoListContainer);
            _playbackInfoOverlay.Add(_playbackInfoScrollView);
            _playbackInfoOverlay.Hide();
            Add(_playbackInfoOverlay);
        }

        private void TogglePlaybackInfoOverlay()
        {
            if (_playbackInfoOverlayVisible)
            {
                HidePlaybackInfoOverlay();
                return;
            }

            ShowPlaybackInfoOverlay();
        }

        private void ShowPlaybackInfoOverlay()
        {
            if (_player == null)
                return;

            if (_playbackInfoOverlay == null)
                CreatePlaybackInfoOverlay();

            HideAudioOverlay();
            HideSubtitleOverlay();
            ExitSubtitleOffsetAdjustMode();

            bool wasVisible = _playbackInfoOverlayVisible;
            RebuildPlaybackInfoOverlayContent();
            _playbackInfoOverlay.Show();
            _playbackInfoOverlay.RaiseToTop();
            _playbackInfoOverlayVisible = true;

            if (!wasVisible)
            {
                _playbackInfoOverlay.PositionX = _playbackInfoOverlayBaseX - OverlaySlideDistance;
                _playbackInfoOverlay.Opacity = 0.0f;

                UiAnimator.Replace(
                    ref _playbackInfoOverlayAnimation,
                    UiAnimator.Start(
                        UiAnimator.PanelDurationMs,
                        animation =>
                        {
                            animation.AnimateTo(_playbackInfoOverlay, "PositionX", (float)_playbackInfoOverlayBaseX);
                            animation.AnimateTo(_playbackInfoOverlay, "Opacity", 1.0f);
                        }
                    )
                );
            }
            else
            {
                _playbackInfoOverlay.PositionX = _playbackInfoOverlayBaseX;
                _playbackInfoOverlay.Opacity = 1.0f;
            }
        }

        private void HidePlaybackInfoOverlay()
        {
            if (_playbackInfoOverlay == null)
                return;

            if (!_playbackInfoOverlayVisible)
            {
                _playbackInfoOverlay.Hide();
                return;
            }

            _playbackInfoOverlayVisible = false;

            UiAnimator.Replace(
                ref _playbackInfoOverlayAnimation,
                    UiAnimator.Start(
                        UiAnimator.PanelDurationMs,
                        animation =>
                        {
                            animation.AnimateTo(_playbackInfoOverlay, "PositionX", _playbackInfoOverlayBaseX - OverlaySlideDistance);
                            animation.AnimateTo(_playbackInfoOverlay, "Opacity", 0.0f);
                        },
                        () => _playbackInfoOverlay.Hide()
                    )
            );
        }

        private bool TryScrollPlaybackInfoOverlay(int direction)
        {
            if (!_playbackInfoOverlayVisible || _playbackInfoListContainer == null || _playbackInfoScrollView == null)
                return false;

            int viewportHeight = _playbackInfoScrollView.SizeHeight > 0
                ? (int)Math.Round(_playbackInfoScrollView.SizeHeight)
                : (_playbackInfoOverlayHeight - UiTheme.PlayerOverlayHeaderHeight);

            int contentHeight = _playbackInfoListContainer.SizeHeight > 0
                ? (int)Math.Round(_playbackInfoListContainer.SizeHeight)
                : 0;

            int maxTop = Math.Max(0, contentHeight - viewportHeight);
            _playbackInfoScrollTop = Math.Clamp(_playbackInfoScrollTop + (direction * PlaybackInfoScrollStepPx), 0, maxTop);
            _playbackInfoListContainer.PositionY = -_playbackInfoScrollTop;
            return true;
        }

        private void RebuildPlaybackInfoOverlayContent()
        {
            if (_playbackInfoListContainer == null)
                return;

            while (_playbackInfoListContainer.ChildCount > 0)
            {
                var child = _playbackInfoListContainer.GetChildAt(0);
                if (child == null)
                    break;

                _playbackInfoListContainer.Remove(child);
            }

            var videoStream = GetCurrentVideoStream();
            var audioStream = GetCurrentAudioStream();

            AddPlaybackInfoSection(
                "Playback Info",
                new[]
                {
                    ("Player", "Tizen Player"),
                    ("Play method", FormatPlaybackMethodLabel(_reportedPlayMethod)),
                    ("Protocol", ResolvePlaybackProtocolLabel())
                });

            AddPlaybackInfoSection(
                "Video Info",
                new[]
                {
                    ("Player dimensions", ResolveDisplayedVideoDimensionsLabel(videoStream)),
                    ("Video resolution", ResolveSourceResolutionLabel(videoStream))
                });

            if (ShouldShowTranscodeReasonsSection())
            {
                AddPlaybackInfoTextSection(
                    "Transcode Reasons",
                    ResolveTranscodeReasonsText());
            }

            if (ShouldShowDeliveredMediaInfo())
            {
                AddPlaybackInfoSection(
                    ResolveDeliveredMediaInfoTitle(),
                    new[]
                    {
                        ("Container", ResolveDeliveredContainerLabel()),
                        ("Video codec", ResolveDeliveredVideoCodecLabel(videoStream)),
                        ("Audio codec", ResolveDeliveredAudioCodecLabel(audioStream))
                    });
            }

            AddPlaybackInfoSection(
                "Original Media Info",
                new[]
                {
                    ("Container", ResolveContainerLabel()),
                    ("Size", FormatFileSize(_currentMediaSource?.Size)),
                    ("Bitrate", ResolveOverallBitrateLabel(videoStream, audioStream)),
                    ("Video codec", FormatCodecLabel(videoStream?.Codec, videoStream?.Profile)),
                    ("Video bitrate", ResolveVideoBitrateLabel(videoStream)),
                    ("Video range type", FormatValueOrDash(videoStream?.VideoRange)),
                    ("Audio codec", FormatCodecLabel(audioStream?.Codec, audioStream?.Profile)),
                    ("Audio bitrate", ResolveAudioBitrateLabel(audioStream)),
                    ("Audio channels", ResolveAudioChannelsLabel(audioStream)),
                    ("Audio sample rate", ResolveAudioSampleRateLabel(audioStream))
                });

            _playbackInfoScrollTop = 0;
            _playbackInfoListContainer.PositionY = 0;
        }

        private void AddPlaybackInfoSection(string title, IEnumerable<(string Label, string Value)> rows)
        {
            if (_playbackInfoListContainer == null)
                return;

            _playbackInfoListContainer.Add(new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = PlaybackInfoSectionSpacing
            });

            _playbackInfoListContainer.Add(new TextLabel(title)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PointSize = PlaybackInfoSectionTitleSize,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Begin,
                Padding = new Extents(PlaybackInfoHorizontalPadding, PlaybackInfoHorizontalPadding, 0, 0)
            });

            foreach (var row in rows)
                _playbackInfoListContainer.Add(CreatePlaybackInfoRow(row.Label, row.Value));
        }

        private void AddPlaybackInfoTextSection(string title, string bodyText)
        {
            if (_playbackInfoListContainer == null)
                return;

            _playbackInfoListContainer.Add(new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = PlaybackInfoSectionSpacing
            });

            _playbackInfoListContainer.Add(new TextLabel(title)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PointSize = PlaybackInfoSectionTitleSize,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Begin,
                Padding = new Extents(PlaybackInfoHorizontalPadding, PlaybackInfoHorizontalPadding, 0, 0)
            });

            _playbackInfoListContainer.Add(new TextLabel(FormatValueOrDash(bodyText))
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PointSize = PlaybackInfoBodyTextSize,
                TextColor = UiTheme.PlayerTextMuted,
                HorizontalAlignment = HorizontalAlignment.Begin,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word,
                Padding = new Extents(PlaybackInfoHorizontalPadding, PlaybackInfoHorizontalPadding, 0, 0)
            });
        }

        private View CreatePlaybackInfoRow(string labelText, string valueText)
        {
            string formattedValue = FormatValueOrDash(valueText);
            bool wrapValue = ShouldWrapPlaybackInfoValue(formattedValue);
            var row = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = wrapValue ? PlaybackInfoWrappedRowHeight : PlaybackInfoRowHeight,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal
                },
                Padding = new Extents(PlaybackInfoHorizontalPadding, PlaybackInfoHorizontalPadding, 0, 0)
            };

            row.Add(new TextLabel(labelText)
            {
                WidthSpecification = PlaybackInfoLabelWidth,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                PointSize = PlaybackInfoRowLabelSize,
                TextColor = UiTheme.PlayerTextMuted,
                HorizontalAlignment = HorizontalAlignment.Begin,
                VerticalAlignment = wrapValue ? VerticalAlignment.Top : VerticalAlignment.Center
            });

            row.Add(new TextLabel(formattedValue)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                PointSize = PlaybackInfoRowValueSize,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Begin,
                VerticalAlignment = wrapValue ? VerticalAlignment.Top : VerticalAlignment.Center,
                MultiLine = wrapValue,
                LineWrapMode = wrapValue ? LineWrapMode.Word : LineWrapMode.Character
            });

            return row;
        }

        private MediaStream GetCurrentVideoStream()
        {
            return _currentMediaSource?.MediaStreams?
                .FirstOrDefault(s => string.Equals(s.Type, "Video", StringComparison.OrdinalIgnoreCase));
        }

        private MediaStream GetCurrentAudioStream()
        {
            MediaStream selected = null;
            if (_overrideAudioIndex.HasValue)
            {
                selected = _currentMediaSource?.MediaStreams?
                    .FirstOrDefault(s =>
                        string.Equals(s.Type, "Audio", StringComparison.OrdinalIgnoreCase) &&
                        s.Index == _overrideAudioIndex.Value);
            }

            return selected ?? _currentMediaSource?.MediaStreams?
                .FirstOrDefault(s => string.Equals(s.Type, "Audio", StringComparison.OrdinalIgnoreCase));
        }

        private string ResolveDisplayedVideoDimensionsLabel(MediaStream videoStream)
        {
            if (!TryResolveVideoDimensions(videoStream, out var sourceWidth, out var sourceHeight))
                return "-";

            int screenWidth = Window.Default.Size.Width;
            int screenHeight = Window.Default.Size.Height;

            if (_useFullscreenAspectMode && _isAspectToggleVisible)
                return $"{screenWidth}x{screenHeight}";

            double aspect = sourceWidth / (double)sourceHeight;
            int fittedWidth = screenWidth;
            int fittedHeight = (int)Math.Round(fittedWidth / aspect);

            if (fittedHeight > screenHeight)
            {
                fittedHeight = screenHeight;
                fittedWidth = (int)Math.Round(fittedHeight * aspect);
            }

            return $"{Math.Max(1, fittedWidth)}x{Math.Max(1, fittedHeight)}";
        }

        private string ResolveSourceResolutionLabel(MediaStream videoStream)
        {
            return TryResolveVideoDimensions(videoStream, out var width, out var height)
                ? $"{width}x{height}"
                : "-";
        }

        private bool TryResolveVideoDimensions(MediaStream videoStream, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (videoStream?.Width > 0 && videoStream?.Height > 0)
            {
                width = videoStream.Width.Value;
                height = videoStream.Height.Value;
                return true;
            }

            var videoProperties = GetPlayerVideoProperties();
            if (TryGetRuntimeIntValue(videoProperties, out var runtimeWidth, "Width", "VideoWidth") &&
                TryGetRuntimeIntValue(videoProperties, out var runtimeHeight, "Height", "VideoHeight") &&
                runtimeWidth > 0 &&
                runtimeHeight > 0)
            {
                width = runtimeWidth;
                height = runtimeHeight;
                return true;
            }

            return false;
        }

        private string ResolveContainerLabel()
        {
            var container = _currentMediaSource?.Container;
            if (string.IsNullOrWhiteSpace(container))
                return "-";

            int commaIndex = container.IndexOf(',');
            return commaIndex > 0 ? container.Substring(0, commaIndex).Trim() : container.Trim();
        }

        private string ResolvePlaybackProtocolLabel()
        {
            string protocol = _currentMediaSource?.TranscodingSubProtocol;
            if (string.IsNullOrWhiteSpace(protocol))
                protocol = GetQueryParamValue(_currentMediaSource?.TranscodingUrl, "TranscodingSubProtocol");
            if (string.IsNullOrWhiteSpace(protocol))
                protocol = GetQueryParamValue(_currentMediaSource?.TranscodingUrl, "TranscodingProtocol");
            if (string.Equals(protocol, "hls", StringComparison.OrdinalIgnoreCase))
                return "HLS";

            if (!string.IsNullOrWhiteSpace(protocol))
                return protocol.Trim().ToUpperInvariant();

            if (LooksLikeHlsPlaylistUrl(_currentMediaSource?.TranscodingUrl))
                return "HLS";

            if (string.IsNullOrWhiteSpace(protocol))
            {
                try
                {
                    protocol = new Uri(AppState.Jellyfin?.ServerUrl ?? string.Empty).Scheme;
                }
                catch
                {
                    protocol = null;
                }
            }

            if (string.IsNullOrWhiteSpace(protocol))
                return "-";

            return protocol.Trim().ToUpperInvariant();
        }

        private string ResolveTranscodeReasonsText()
        {
            var lines = GetCollectedTranscodeReasonTokens()
                .Select(FormatServerTranscodeReason)
                .Where(line => !string.IsNullOrWhiteSpace(line) && !string.Equals(line, "-", StringComparison.Ordinal))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (string.Equals(_reportedPlayMethod, "DirectStream", StringComparison.OrdinalIgnoreCase))
                lines.Add("Direct streaming");

            var distinctLines = lines
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return distinctLines.Count > 0
                ? string.Join("\n", distinctLines)
                : "-";
        }

        private bool ShouldShowTranscodeReasonsSection()
        {
            if (string.Equals(_reportedPlayMethod, "DirectPlay", StringComparison.OrdinalIgnoreCase))
                return false;

            return !string.Equals(ResolveTranscodeReasonsText(), "-", StringComparison.Ordinal);
        }

        private bool ShouldShowDeliveredMediaInfo()
        {
            return !string.Equals(_reportedPlayMethod, "DirectPlay", StringComparison.OrdinalIgnoreCase) &&
                   !string.IsNullOrWhiteSpace(_currentMediaSource?.TranscodingUrl);
        }

        private string ResolveDeliveredMediaInfoTitle()
        {
            return string.Equals(_reportedPlayMethod, "DirectStream", StringComparison.OrdinalIgnoreCase)
                ? "Direct Stream Info"
                : "Transcode Info";
        }

        private string ResolveDeliveredContainerLabel()
        {
            string container = _currentMediaSource?.TranscodingContainer;
            if (string.IsNullOrWhiteSpace(container))
                container = GetPrimaryQueryValue("Container");
            if (string.IsNullOrWhiteSpace(container))
                container = GetPrimaryQueryValue("TranscodeContainer");
            if (string.IsNullOrWhiteSpace(container))
                container = GetPrimaryQueryValue("SegmentContainer");
            if (string.IsNullOrWhiteSpace(container))
                container = GetPrimaryQueryValue("MediaContainer");

            if (string.IsNullOrWhiteSpace(container))
            {
                string extension = GetUrlPathExtension(_currentMediaSource?.TranscodingUrl);
                if (string.Equals(extension, ".m3u8", StringComparison.OrdinalIgnoreCase))
                    return "hls";
                if (string.Equals(extension, ".mp4", StringComparison.OrdinalIgnoreCase))
                    return "mp4";
                if (string.Equals(extension, ".ts", StringComparison.OrdinalIgnoreCase))
                    return "ts";
                if (string.Equals(extension, ".mkv", StringComparison.OrdinalIgnoreCase))
                    return "mkv";
                return "-";
            }

            return container.Trim().ToLowerInvariant();
        }

        private string ResolveDeliveredVideoCodecLabel(MediaStream originalVideoStream)
        {
            string runtimeCodec = ResolveRuntimeVideoCodecLabel();
            string originalLabel = FormatCodecLabel(originalVideoStream?.Codec, null);
            string originalCodec = NormalizeCodecForCompare(originalVideoStream?.Codec);
            string runtimeNormalized = NormalizeCodecForCompare(runtimeCodec);

            if (!string.IsNullOrWhiteSpace(runtimeCodec) &&
                !string.Equals(runtimeCodec, "-", StringComparison.Ordinal))
            {
                if (ShouldReportCopiedVideoCodec() &&
                    !string.IsNullOrWhiteSpace(originalCodec) &&
                    string.Equals(runtimeNormalized, originalCodec, StringComparison.OrdinalIgnoreCase))
                {
                    return $"{runtimeCodec} (copy)";
                }

                return runtimeCodec;
            }

            if (ShouldReportCopiedVideoCodec() &&
                !string.IsNullOrWhiteSpace(originalLabel) &&
                !string.Equals(originalLabel, "-", StringComparison.Ordinal))
            {
                return $"{originalLabel} (copy)";
            }

            string delivered = GetSingleQueryCodecValue("VideoCodec");
            return string.IsNullOrWhiteSpace(delivered)
                ? "-"
                : FormatCodecLabel(delivered, null);
        }

        private string ResolveDeliveredAudioCodecLabel(MediaStream originalAudioStream)
        {
            var reasons = GetCollectedTranscodeReasonTokens();
            bool hasAudioAffectingReason = HasAudioAffectingReason(reasons);
            string runtimeCodec = ResolveRuntimeAudioCodecLabel();
            string originalLabel = FormatCodecLabel(originalAudioStream?.Codec, null);
            string originalCodec = NormalizeCodecForCompare(originalAudioStream?.Codec);
            string runtimeNormalized = NormalizeCodecForCompare(runtimeCodec);
            string delivered = GetSingleQueryCodecValue("AudioCodec");

            if (!string.IsNullOrWhiteSpace(runtimeCodec) &&
                !string.Equals(runtimeCodec, "-", StringComparison.Ordinal))
            {
                if (ShouldReportCopiedAudioCodec() &&
                    !string.IsNullOrWhiteSpace(originalCodec) &&
                    string.Equals(runtimeNormalized, originalCodec, StringComparison.OrdinalIgnoreCase))
                {
                    return $"{runtimeCodec} (copy)";
                }

                return runtimeCodec;
            }

            if (ShouldReportCopiedAudioCodec() &&
                !string.IsNullOrWhiteSpace(originalLabel) &&
                !string.Equals(originalLabel, "-", StringComparison.Ordinal))
            {
                return $"{originalLabel} (copy)";
            }

            if (hasAudioAffectingReason)
            {
                return string.IsNullOrWhiteSpace(delivered)
                    ? "-"
                    : FormatCodecLabel(delivered, null);
            }

            if (!string.IsNullOrWhiteSpace(delivered))
                return FormatCodecLabel(delivered, null);

            if (string.Equals(_reportedPlayMethod, "DirectStream", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(originalLabel) &&
                !string.Equals(originalLabel, "-", StringComparison.Ordinal))
            {
                return $"{originalLabel} (copy)";
            }

            return string.IsNullOrWhiteSpace(delivered)
                ? "-"
                : FormatCodecLabel(delivered, null);
        }

        private string GetPrimaryQueryValue(string key)
        {
            string value = GetQueryParamValue(_currentMediaSource?.TranscodingUrl, key);
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            int commaIndex = value.IndexOf(',');
            return (commaIndex >= 0 ? value.Substring(0, commaIndex) : value).Trim();
        }

        private string GetSingleQueryCodecValue(string key)
        {
            var values = GetCommaSeparatedQueryValues(key);
            return values.Count == 1 ? values[0] : string.Empty;
        }

        private string ResolveOverallBitrateLabel(MediaStream videoStream, MediaStream audioStream)
        {
            int? totalBitrate = _currentMediaSource?.Bitrate;

            if (!totalBitrate.HasValue)
            {
                int combined = 0;
                if (videoStream?.BitRate > 0)
                    combined += videoStream.BitRate.Value;
                if (audioStream?.BitRate > 0)
                    combined += audioStream.BitRate.Value;

                if (combined > 0)
                    totalBitrate = combined;
            }

            return FormatBitrate(totalBitrate);
        }

        private string ResolveVideoBitrateLabel(MediaStream videoStream)
        {
            int? bitrate = videoStream?.BitRate;
            if (!bitrate.HasValue &&
                TryGetRuntimeIntValue(GetPlayerVideoProperties(), out var runtimeBitrate, "BitRate", "Bitrate"))
            {
                bitrate = runtimeBitrate;
            }

            return FormatBitrate(bitrate);
        }

        private string ResolveAudioBitrateLabel(MediaStream audioStream)
        {
            int? bitrate = audioStream?.BitRate;
            if (!bitrate.HasValue &&
                TryGetRuntimeIntValue(GetPlayerAudioProperties(), out var runtimeBitrate, "BitRate", "Bitrate"))
            {
                bitrate = runtimeBitrate;
            }

            return FormatBitrate(bitrate);
        }

        private string ResolveAudioChannelsLabel(MediaStream audioStream)
        {
            if (!string.IsNullOrWhiteSpace(audioStream?.ChannelLayout))
                return audioStream.ChannelLayout.Trim();

            int? channels = audioStream?.Channels;
            if (!channels.HasValue &&
                TryGetRuntimeIntValue(GetPlayerAudioProperties(), out var runtimeChannels, "Channels", "ChannelCount"))
            {
                channels = runtimeChannels;
            }

            return channels.HasValue && channels.Value > 0
                ? channels.Value.ToString(CultureInfo.InvariantCulture)
                : "-";
        }

        private string ResolveAudioSampleRateLabel(MediaStream audioStream)
        {
            int? sampleRate = audioStream?.SampleRate;
            if (!sampleRate.HasValue &&
                TryGetRuntimeIntValue(GetPlayerAudioProperties(), out var runtimeSampleRate, "SampleRate"))
            {
                sampleRate = runtimeSampleRate;
            }

            return sampleRate.HasValue && sampleRate.Value > 0
                ? $"{sampleRate.Value.ToString(CultureInfo.InvariantCulture)} Hz"
                : "-";
        }

        private object GetPlayerVideoProperties()
        {
            try { return _player?.StreamInfo?.GetVideoProperties(); } catch { return null; }
        }

        private object GetPlayerAudioProperties()
        {
            try { return _player?.StreamInfo?.GetAudioProperties(); } catch { return null; }
        }

        private object GetPlayerStreamInfo()
        {
            try { return _player?.StreamInfo; } catch { return null; }
        }

        private static bool TryGetRuntimeIntValue(object source, out int value, params string[] propertyNames)
        {
            value = 0;
            if (source == null || propertyNames == null || propertyNames.Length == 0)
                return false;

            var type = source.GetType();
            foreach (var propertyName in propertyNames)
            {
                if (string.IsNullOrWhiteSpace(propertyName))
                    continue;

                var property = type.GetProperty(
                    propertyName,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property == null)
                    continue;

                var rawValue = property.GetValue(source);
                switch (rawValue)
                {
                    case int intValue:
                        value = intValue;
                        return true;
                    case long longValue when longValue >= int.MinValue && longValue <= int.MaxValue:
                        value = (int)longValue;
                        return true;
                    case short shortValue:
                        value = shortValue;
                        return true;
                    case byte byteValue:
                        value = byteValue;
                        return true;
                    case double doubleValue when doubleValue >= int.MinValue && doubleValue <= int.MaxValue:
                        value = (int)Math.Round(doubleValue);
                        return true;
                    case float floatValue when floatValue >= int.MinValue && floatValue <= int.MaxValue:
                        value = (int)Math.Round(floatValue);
                        return true;
                    case string textValue when int.TryParse(textValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                        value = parsed;
                        return true;
                }
            }

            return false;
        }

        private string ResolveRuntimeVideoCodecLabel()
        {
            string codec = TryGetRuntimeStringValue(GetPlayerStreamInfo(), "GetVideoCodec", "VideoCodec", "Codec");
            return FormatCodecLabel(codec, null);
        }

        private string ResolveRuntimeAudioCodecLabel()
        {
            string codec = TryGetRuntimeStringValue(GetPlayerStreamInfo(), "GetAudioCodec", "AudioCodec", "Codec");
            return FormatCodecLabel(codec, null);
        }

        private static string TryGetRuntimeStringValue(object source, params string[] memberNames)
        {
            if (source == null || memberNames == null || memberNames.Length == 0)
                return string.Empty;

            var type = source.GetType();
            foreach (var memberName in memberNames)
            {
                if (string.IsNullOrWhiteSpace(memberName))
                    continue;

                var method = type.GetMethod(
                    memberName,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase,
                    null,
                    Type.EmptyTypes,
                    null);
                if (method != null)
                {
                    var rawValue = method.Invoke(source, null);
                    var text = rawValue?.ToString();
                    if (!string.IsNullOrWhiteSpace(text))
                        return text.Trim();
                }

                var property = type.GetProperty(
                    memberName,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property == null)
                    continue;

                var propertyValue = property.GetValue(source)?.ToString();
                if (!string.IsNullOrWhiteSpace(propertyValue))
                    return propertyValue.Trim();
            }

            return string.Empty;
        }

        private List<string> GetCollectedTranscodeReasonTokens()
        {
            var reasons = _currentMediaSource?.TranscodingReasons?
                .Where(reason => !string.IsNullOrWhiteSpace(reason))
                .Select(reason => reason.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (reasons != null && reasons.Count > 0)
                return reasons;

            reasons = GetCommaSeparatedQueryValues("TranscodeReasons");
            if (reasons.Count > 0)
                return reasons;

            return GetCommaSeparatedQueryValues("TranscodingReasons");
        }

        private List<string> GetCommaSeparatedQueryValues(string key)
        {
            string value = GetQueryParamValue(_currentMediaSource?.TranscodingUrl, key);
            if (string.IsNullOrWhiteSpace(value))
                return new List<string>();

            return value
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private bool ShouldReportCopiedVideoCodec()
        {
            if (string.Equals(_reportedPlayMethod, "DirectStream", StringComparison.OrdinalIgnoreCase))
                return true;

            var reasons = GetCollectedTranscodeReasonTokens();
            if (reasons.Count == 0)
                return false;

            return !HasVideoAffectingReason(reasons);
        }

        private bool ShouldReportCopiedAudioCodec()
        {
            var reasons = GetCollectedTranscodeReasonTokens();
            if (reasons.Count > 0)
                return !HasAudioAffectingReason(reasons);

            return string.Equals(_reportedPlayMethod, "DirectStream", StringComparison.OrdinalIgnoreCase) &&
                   string.IsNullOrWhiteSpace(GetSingleQueryCodecValue("AudioCodec"));
        }

        private static bool HasVideoAffectingReason(IEnumerable<string> reasons)
        {
            foreach (string reason in reasons ?? Enumerable.Empty<string>())
            {
                string normalized = NormalizeReasonToken(reason);
                if (normalized.Contains("video") ||
                    normalized.Contains("subtitle") ||
                    normalized.Contains("anamorphic") ||
                    normalized.Contains("interlace") ||
                    normalized.Contains("bitdepth") ||
                    normalized.Contains("framerate") ||
                    normalized.Contains("level"))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAudioAffectingReason(IEnumerable<string> reasons)
        {
            foreach (string reason in reasons ?? Enumerable.Empty<string>())
            {
                if (NormalizeReasonToken(reason).Contains("audio"))
                    return true;
            }

            return false;
        }

        private static string NormalizeReasonToken(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return string.Empty;

            return reason
                .Trim()
                .Replace("_", string.Empty)
                .Replace(" ", string.Empty)
                .ToLowerInvariant();
        }

        private static bool LooksLikeHlsPlaylistUrl(string url)
        {
            return string.Equals(GetUrlPathExtension(url), ".m3u8", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetUrlPathExtension(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return string.Empty;

            try
            {
                string path = url;
                int queryIndex = path.IndexOf('?');
                if (queryIndex >= 0)
                    path = path.Substring(0, queryIndex);

                return System.IO.Path.GetExtension(path) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string FormatServerTranscodeReason(string reason)
        {
            string formatted = FormatTranscodeReason(reason);
            if (string.IsNullOrWhiteSpace(formatted) || string.Equals(formatted, "-", StringComparison.Ordinal))
                return "-";

            string lower = formatted.ToLowerInvariant();
            return char.ToUpperInvariant(lower[0]) + lower.Substring(1);
        }

        private static string NormalizeCodecForCompare(string codec)
        {
            if (string.IsNullOrWhiteSpace(codec))
                return string.Empty;

            string normalizedVideo = NormalizeVideoCodec(codec);
            if (!string.IsNullOrWhiteSpace(normalizedVideo))
                return normalizedVideo;

            return NormalizeAudioCodec(codec);
        }

        private static string FormatTranscodeReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return "-";

            var chars = new List<char>(reason.Length + 8);
            char previous = '\0';
            foreach (char current in reason.Trim())
            {
                bool insertSpace =
                    chars.Count > 0 &&
                    char.IsUpper(current) &&
                    (char.IsLower(previous) || char.IsDigit(previous));

                if (insertSpace)
                    chars.Add(' ');

                chars.Add(current == '_' ? ' ' : current);
                previous = current;
            }

            var formatted = new string(chars.ToArray()).Trim();
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(formatted.ToLowerInvariant());
        }

        private static string FormatPlaybackMethodLabel(string method)
        {
            if (string.IsNullOrWhiteSpace(method))
                return "-";

            return method.Trim() switch
            {
                "DirectPlay" => "Direct Play",
                "DirectStream" => "Direct Stream",
                _ => method.Trim()
            };
        }

        private static string FormatCodecLabel(string codec, string profile)
        {
            string normalizedCodec = NormalizeCodecDisplay(codec);
            string normalizedProfile = NormalizeCodecProfileDisplay(normalizedCodec, profile);

            if (string.IsNullOrWhiteSpace(normalizedCodec) && string.IsNullOrWhiteSpace(normalizedProfile))
                return "-";

            if (string.IsNullOrWhiteSpace(normalizedProfile))
                return normalizedCodec;

            if (string.Equals(normalizedCodec, "TrueHD", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(normalizedProfile, "Atmos", StringComparison.OrdinalIgnoreCase))
            {
                return "TrueHD + Atmos";
            }

            if (string.Equals(normalizedCodec, "EAC3", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(normalizedProfile, "Atmos", StringComparison.OrdinalIgnoreCase))
            {
                return "EAC3 + Atmos";
            }

            if (string.Equals(normalizedCodec, "AC3", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(normalizedProfile, "Atmos", StringComparison.OrdinalIgnoreCase))
            {
                return "AC3 + Atmos";
            }

            return $"{normalizedCodec} {normalizedProfile}".Trim();
        }

        private static string FormatBitrate(int? bitrate)
        {
            if (!bitrate.HasValue || bitrate.Value <= 0)
                return "-";

            double value = bitrate.Value;
            if (value >= 1_000_000d)
                return $"{value / 1_000_000d:0.0} Mbps";
            if (value >= 1_000d)
                return $"{value / 1_000d:0} kbps";

            return $"{value:0} bps";
        }

        private static string FormatFileSize(long? sizeBytes)
        {
            if (!sizeBytes.HasValue || sizeBytes.Value <= 0)
                return "-";

            double size = sizeBytes.Value;
            string[] units = { "B", "KiB", "MiB", "GiB", "TiB" };
            int unitIndex = 0;
            while (size >= 1024d && unitIndex < units.Length - 1)
            {
                size /= 1024d;
                unitIndex++;
            }

            string format = unitIndex == 0 ? "0" : "0.0";
            return $"{size.ToString(format, CultureInfo.InvariantCulture)} {units[unitIndex]}";
        }

        private static string FormatValueOrDash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        }

        private static bool ShouldWrapPlaybackInfoValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "-", StringComparison.Ordinal))
                return false;

            return value.Length > PlaybackInfoLongValueWrapThreshold ||
                   value.Contains(" + ", StringComparison.Ordinal) ||
                   value.Contains("/", StringComparison.Ordinal);
        }

        private static string NormalizeCodecDisplay(string codec)
        {
            if (string.IsNullOrWhiteSpace(codec))
                return string.Empty;

            return codec.Trim().ToLowerInvariant() switch
            {
                "h264" or "avc" or "x264" => "H264",
                "h265" or "hevc" or "x265" => "HEVC",
                "aac" => "AAC",
                "ac3" => "AC3",
                "eac3" or "e-ac-3" => "EAC3",
                "truehd" => "TrueHD",
                "dca" or "dts" => "DTS",
                "dts-hd ma" or "dtshd_ma" or "dtshdma" => "DTS-HD MA",
                "dts-hd hra" or "dtshd_hra" or "dtshdhra" => "DTS-HD HRA",
                "flac" => "FLAC",
                "mp3" => "MP3",
                "opus" => "Opus",
                _ => codec.Trim().ToUpperInvariant()
            };
        }

        private static string NormalizeCodecProfileDisplay(string normalizedCodec, string profile)
        {
            if (string.IsNullOrWhiteSpace(profile))
                return string.Empty;

            string trimmed = profile.Trim();
            string normalized = trimmed.ToLowerInvariant();

            if (normalized == "lc")
                return "LC";

            if (normalized.Contains("dolby atmos"))
                return "Atmos";

            if (string.Equals(normalizedCodec, "TrueHD", StringComparison.OrdinalIgnoreCase) &&
                normalized.Contains("dolby truehd"))
            {
                return normalized.Contains("atmos") ? "Atmos" : string.Empty;
            }

            if (string.Equals(normalizedCodec, "EAC3", StringComparison.OrdinalIgnoreCase) &&
                normalized.Contains("dolby digital plus"))
            {
                return normalized.Contains("atmos") ? "Atmos" : string.Empty;
            }

            if (string.Equals(normalizedCodec, "AC3", StringComparison.OrdinalIgnoreCase) &&
                normalized.Contains("dolby digital"))
            {
                return normalized.Contains("atmos") ? "Atmos" : string.Empty;
            }

            string titleCase = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(trimmed.ToLowerInvariant());
            if (string.Equals(titleCase, normalizedCodec, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            return titleCase;
        }
    }
}
