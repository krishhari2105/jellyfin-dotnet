using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using Tizen.NUI.Components;
using JellyfinTizen.Core;
using JellyfinTizen.Models;
using JellyfinTizen.UI;
using JellyfinTizen.Utils;
using IOPath = System.IO.Path;

namespace JellyfinTizen.Screens
{
    public class EpisodeDetailsScreen : DetailsScreenBase
    {
        private const int FixedTopContentHeight = 500;
        private const int FixedOverviewViewportHeight = 240;
        private const int OverviewScrollStepPx = 70;
        private const int OverviewScrollTailPx = 28;
        private const int EpisodeThumbWidth = 640;
        private const int EpisodeThumbHeight = 360;
        private const float SeriesTitlePointSize = 28f;
        private readonly JellyfinMovie _episode;
        private View _metadataContainer;
        private View _metadataSummaryRow;
        private TextLabel _metadataSummaryLabel;
        private View _metadataRatingGroup;
        private TextLabel _metadataRatingLabel;
        private View _metadataTagRow;
        private View _overviewViewport;
        private TextLabel _overviewLabel;
        private int _overviewScrollOffset;
        private int _overviewMaxScroll;

        public EpisodeDetailsScreen(
            JellyfinMovie episode,
            List<MediaStream> prefetchedSubtitleStreams = null,
            List<MediaSourceInfo> prefetchedMediaSources = null) : base(episode)
        {
            _episode = episode;
            if (prefetchedSubtitleStreams != null)
                _subtitleStreams = prefetchedSubtitleStreams;
            if (prefetchedMediaSources != null)
                _mediaSources = prefetchedMediaSources;
            _subtitleStreamsLoaded = prefetchedSubtitleStreams != null;
            _mediaSourcesLoaded = prefetchedMediaSources != null;
            var root = UiFactory.CreateAtmosphericBackground();
            var apiKey = Uri.EscapeDataString(AppState.AccessToken);
            var serverUrl = AppState.Jellyfin.ServerUrl;
            var backdropUrl = JellyfinImageUrlBuilder.BuildBackdropUrl(
                _episode,
                serverUrl,
                apiKey,
                maxWidth: 1920);
            bool hasBackdropImage = !string.IsNullOrWhiteSpace(backdropUrl);
            var backdrop = new ImageView
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                ResourceUrl = backdropUrl,
                PreMultipliedAlpha = false
            };
            var dimOverlay = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                BackgroundColor = hasBackdropImage ? UiTheme.DetailsBackdropDim : Color.Transparent
            };
            var content = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                Padding = new Extents(90, 90, 80, 80),
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    CellPadding = new Size2D(60, 0)
                }
            };

            var thumbUrl =
                _episode.IsEpisode && _episode.HasPrimary
                    ? $"{serverUrl}/Items/{_episode.Id}/Images/Primary/0?maxWidth={EpisodeThumbWidth}&quality=75&api_key={apiKey}"
                    : _episode.HasThumb
                        ? $"{serverUrl}/Items/{_episode.Id}/Images/Thumb/0?maxWidth={EpisodeThumbWidth}&quality=75&api_key={apiKey}"
                        : _episode.HasBackdrop
                            ? $"{serverUrl}/Items/{_episode.Id}/Images/Backdrop/0?maxWidth={EpisodeThumbWidth}&quality=70&api_key={apiKey}"
                            : $"{serverUrl}/Items/{_episode.Id}/Images/Primary/0?maxWidth={EpisodeThumbWidth}&quality=75&api_key={apiKey}";
            thumbUrl = AppState.RewriteImageUrlForTailscale(thumbUrl);

            var thumbFrame = new View
            {
                WidthSpecification = EpisodeThumbWidth,
                HeightSpecification = EpisodeThumbHeight,
                BackgroundColor = UiTheme.DetailsPosterSurface,
                CornerRadius = 16.0f,
                ClippingMode = ClippingModeType.ClipChildren
            };

            var thumb = new ImageView
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                ResourceUrl = thumbUrl,
                PreMultipliedAlpha = false
            };
            thumbFrame.Add(thumb);

            _infoColumn = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding = new Size2D(0, 26)
                }
            };

            var seriesTitleText = _episode.SeriesName;
            var episodeTitleText = BuildEpisodeTitle(_episode);
            var seriesTitle = new TextLabel(seriesTitleText)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PointSize = SeriesTitlePointSize,
                TextColor = new Color(1f, 1f, 1f, 0.72f),
                MultiLine = false,
                Ellipsis = true
            };
            var episodeTitle = new TextLabel(episodeTitleText)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PointSize = 56,
                TextColor = Color.White,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word,
                Ellipsis = false,
                VerticalAlignment = VerticalAlignment.Top
            };
            _metadataContainer = CreateMetadataView();
            var overviewText = string.IsNullOrEmpty(_episode.Overview)
                ? "No overview available."
                : _episode.Overview;
            var topContentViewport = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = FixedTopContentHeight,
                ClippingMode = ClippingModeType.ClipChildren
            };
            var topContent = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding = new Size2D(0, 26)
                }
            };
            _overviewViewport = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = FixedOverviewViewportHeight,
                ClippingMode = ClippingModeType.ClipChildren
            };
            _overviewLabel = new TextLabel(overviewText)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PointSize = 31,
                TextColor = UiTheme.DetailsOverviewText,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word,
                Ellipsis = false,
                VerticalAlignment = VerticalAlignment.Top
            };
            _overviewViewport.Add(_overviewLabel);
            topContent.Add(seriesTitle);
            topContent.Add(episodeTitle);
            topContent.Add(_metadataContainer);
            topContent.Add(_overviewViewport);
            topContentViewport.Add(topContent);
            _infoColumn.Add(topContentViewport);
            UpdateMetadataView();
            if (_episode.IsPlayableVideo)
            {
                _buttonGroup = new View
                {
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightResizePolicy = ResizePolicyType.FitToChildren,
                    Layout = new LinearLayout
                    {
                        LinearOrientation = LinearLayout.Orientation.Vertical,
                        CellPadding = new Size2D(0, 14)
                    },
                    Margin = new Extents(0, 0, 34, 0)
                };

                _buttonRowTop = DetailsScreenHelpers.CreateButtonRow();
                _buttonRowBottom = DetailsScreenHelpers.CreateButtonRow();
                _buttonGroup.Add(_buttonRowTop);
                _buttonGroup.Add(_buttonRowBottom);

                _playButton = CreateActionButton("Play", isPrimary: true, iconFile: "play.svg", width: DetailsScreenHelpers.PlayActionButtonWidth, iconSize: DetailsScreenHelpers.PlayActionButtonIconSize);

                if (_resumeAvailable)
                {
                    _resumeButton = CreateActionButton("Resume", isPrimary: false, iconFile: "resume.svg", width: null, iconSize: DetailsScreenHelpers.PlayActionButtonIconSize);
                }

                _audioButton = CreateActionButton(string.Empty, isPrimary: false, iconFile: "audio.svg", width: DetailsScreenHelpers.IconActionButtonWidth, iconSize: DetailsScreenHelpers.AudioActionButtonIconSize);
                _subtitleButton = CreateActionButton(string.Empty, isPrimary: false, iconFile: "sub.svg", width: DetailsScreenHelpers.IconActionButtonWidth, iconSize: DetailsScreenHelpers.SubtitleActionButtonIconSize);
                _versionButton = CreateActionButton("Default", isPrimary: false);
                RebuildActionButtons(includeVersionButton: false);
                _infoColumn.Add(_buttonGroup);
            }
            content.Add(thumbFrame);
            content.Add(_infoColumn);
            root.Add(backdrop);
            root.Add(dimOverlay);
            root.Add(content);
            Add(root);
            NormalizeSelectionStateForCurrentMediaSource();
        }
        public override void OnShow()
        {
            if (!_mediaSourcesLoaded || !_subtitleStreamsLoaded)
                _ = LoadMediaSourcesAndSubtitlesAsync();
            if (_buttons.Count > 0)
                FocusButton(0);
            RunOnUiThread(RefreshOverviewScrollBounds);
            ScheduleActionButtonReflow();
        }

		public override void OnHide()
		{
			UiAnimator.StopAndDisposeAll(_focusAnimations);
			HideSelectionPanel();
		}

        private static string BuildEpisodeTitle(JellyfinMovie episode)
        {
            if (episode == null)
                return string.Empty;

            if (episode.ParentIndexNumber > 0 && episode.IndexNumber > 0)
                return $"S{episode.ParentIndexNumber}:E{episode.IndexNumber} - {episode.Name}";

            if (episode.IndexNumber > 0)
                return $"E{episode.IndexNumber} - {episode.Name}";

            return episode.Name ?? string.Empty;
        }
        private async Task LoadMediaSourcesAndSubtitlesAsync()
        {
            if (_mediaSourcesLoaded && _subtitleStreamsLoaded)
                return;

            try
            {
                var playbackInfo = await AppState.Jellyfin.GetPlaybackInfoAsync(_episode.Id, subtitleHandlingDisabled: true);
                _mediaSources = playbackInfo?.MediaSources ?? new List<MediaSourceInfo>();
            }
            catch
            {
                _mediaSources = new List<MediaSourceInfo>();
            }

            if (_mediaSources.Count == 0)
            {
                _mediaSources.Add(new MediaSourceInfo
                {
                    Id = _episode.Id,
                    Name = "Default"
                });
            }

            _mediaSourcesLoaded = true;
            _selectedMediaSourceIndex = Math.Clamp(_selectedMediaSourceIndex, 0, _mediaSources.Count - 1);

            var currentSource = _mediaSources[_selectedMediaSourceIndex];
            _subtitleStreams = currentSource?.MediaStreams?
                .Where(s => s.Type == "Subtitle")
                .ToList() ?? new List<MediaStream>();
            _subtitleStreamsLoaded = true;

            NormalizeSelectionStateForCurrentMediaSource();
            RebuildActionButtons(includeVersionButton: _mediaSources.Count > 1);
            UpdateVersionButtonText();
            UpdateMetadataView();
            ScheduleActionButtonReflow();

            if (_buttons.Count > 0)
                FocusButton(_buttonIndex);
        }

        private void ScheduleActionButtonReflow()
        {
            if (_actionButtonReflowScheduled || _buttonGroup == null)
                return;

            _actionButtonReflowScheduled = true;
            RunOnUiThread(() =>
            {
                RunOnUiThread(() =>
                {
                    _actionButtonReflowScheduled = false;
                    if (_buttonGroup == null)
                        return;

                    RebuildActionButtons(includeVersionButton: _mediaSources.Count > 1);
                    if (_buttons.Count > 0)
                        FocusButton(_buttonIndex);
                });
            });
        }

        public override void HandleKey(AppKey key)
        {
            if (HandleSelectionPanelKey(key))
                return;

            switch (key)
            {
                case AppKey.Left:
                    MoveFocus(-1);
                    break;
                case AppKey.Right:
                    MoveFocus(1);
                    break;
                case AppKey.Up:
                    ScrollOverview(-OverviewScrollStepPx);
                    break;
                case AppKey.Enter:
                    ActivateFocusedButton();
                    break;
                case AppKey.Back:
                    NavigationService.NavigateBack();
                    break;
                case AppKey.Down:
                    ScrollOverview(OverviewScrollStepPx);
                    break;
            }
        }

        private void RefreshOverviewScrollBounds()
        {
            if (_overviewViewport == null || _overviewLabel == null)
                return;

            int viewportHeight = (int)Math.Round(_overviewViewport.SizeHeight);
            if (viewportHeight <= 0)
                viewportHeight = FixedOverviewViewportHeight;

            int viewportWidth = (int)Math.Round(_overviewViewport.SizeWidth);
            int measuredHeight = (int)Math.Round(_overviewLabel.SizeHeight);
            int estimatedHeight = EstimateOverviewContentHeight(viewportWidth);
            int contentHeight = Math.Max(viewportHeight, Math.Max(measuredHeight, estimatedHeight));

            _overviewMaxScroll = Math.Max(0, contentHeight - viewportHeight + OverviewScrollTailPx);
            _overviewScrollOffset = Math.Clamp(_overviewScrollOffset, 0, _overviewMaxScroll);
            _overviewLabel.PositionY = -_overviewScrollOffset;
        }

        private void ScrollOverview(int delta)
        {
            if (_overviewLabel == null)
                return;

            RefreshOverviewScrollBounds();

            int nextOffset = Math.Clamp(_overviewScrollOffset + delta, 0, _overviewMaxScroll);
            if (nextOffset == _overviewScrollOffset)
                return;

            _overviewScrollOffset = nextOffset;
            _overviewLabel.PositionY = -_overviewScrollOffset;
        }

        private int EstimateOverviewContentHeight(int viewportWidth)
        {
            string text = _overviewLabel?.Text ?? string.Empty;
            if (string.IsNullOrEmpty(text))
                return 0;

            float pointSize = _overviewLabel.PointSize > 0 ? _overviewLabel.PointSize : 31f;
            int safeWidth = viewportWidth > 0 ? viewportWidth : 960;
            int charsPerLine = Math.Max(12, (int)Math.Floor(safeWidth / Math.Max(1f, pointSize * 0.56f)));
            int lineCount = 0;

            foreach (var paragraph in text.Split('\n'))
            {
                if (paragraph.Length == 0)
                {
                    lineCount += 1;
                    continue;
                }

                lineCount += (int)Math.Ceiling(paragraph.Length / (double)charsPerLine);
            }

            int lineHeight = (int)Math.Ceiling(pointSize * 1.55f);
            return Math.Max(0, lineCount * lineHeight);
        }

        protected override int ThumbnailWidthForLayout => EpisodeThumbWidth;

        protected override bool UseFallbackForResolution => false;

        protected override JellyfinMovie GetMediaItem() => _episode;

        protected override void PlayMedia(JellyfinMovie media, int startPositionMs)
        {
            NavigationService.Navigate(
                new VideoPlayerScreen(
                    media,
                    startPositionMs,
                    _selectedSubtitleIndex,
                    AppState.BurnInSubtitles,
                    GetSelectedMediaSourceId(),
                    GetEffectiveSelectedAudioIndex(),
                    GetSelectedSubtitleCodec()
                )
            );
        }

        private View CreateMetadataView()
        {
            var container = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 114,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical,
                    CellPadding = new Size2D(0, 10)
                }
            };

            _metadataSummaryRow = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 38,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    CellPadding = new Size2D(18, 0)
                }
            };

            _metadataSummaryLabel = new TextLabel
            {
                WidthResizePolicy = ResizePolicyType.UseNaturalSize,
                HeightResizePolicy = ResizePolicyType.UseNaturalSize,
                PointSize = 28,
                TextColor = new Color(0.88f, 0.88f, 0.88f, 1f),
                VerticalAlignment = VerticalAlignment.Center
            };

            var ratingStar = new TextLabel("★")
            {
                WidthResizePolicy = ResizePolicyType.UseNaturalSize,
                HeightResizePolicy = ResizePolicyType.UseNaturalSize,
                PointSize = 32,
                TextColor = new Color(0.95f, 0.78f, 0.29f, 1f),
                VerticalAlignment = VerticalAlignment.Center
            };

            _metadataRatingLabel = new TextLabel
            {
                WidthResizePolicy = ResizePolicyType.UseNaturalSize,
                HeightResizePolicy = ResizePolicyType.UseNaturalSize,
                PointSize = 28,
                TextColor = new Color(0.92f, 0.92f, 0.92f, 1f),
                VerticalAlignment = VerticalAlignment.Center
            };

            _metadataRatingGroup = new View
            {
                WidthResizePolicy = ResizePolicyType.FitToChildren,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    CellPadding = new Size2D(8, 0)
                }
            };

            _metadataRatingGroup.Add(ratingStar);
            _metadataRatingGroup.Add(_metadataRatingLabel);

            _metadataSummaryRow.Add(_metadataSummaryLabel);
            _metadataSummaryRow.Add(_metadataRatingGroup);

            _metadataTagRow = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 56,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    CellPadding = new Size2D(12, 0),
                    HorizontalAlignment = HorizontalAlignment.Begin
                }
            };

            container.Add(_metadataSummaryRow);
            container.Add(_metadataTagRow);
            return container;
        }

        private void UpdateMetadataView()
        {
            if (_metadataContainer == null || _metadataSummaryLabel == null || _metadataTagRow == null)
                return;

            var summaryText = BuildSummaryText(_episode);
            _metadataSummaryLabel.Text = string.IsNullOrWhiteSpace(summaryText) ? " " : summaryText;

            if (_episode.CommunityRating.HasValue && _episode.CommunityRating.Value > 0)
            {
                _metadataRatingLabel.Text = _episode.CommunityRating.Value.ToString("0.0", CultureInfo.InvariantCulture);
                _metadataRatingGroup.Show();
            }
            else
            {
                _metadataRatingGroup.Hide();
            }

            var tags = DetailsScreenHelpers.BuildTechnicalTags(GetSelectedMediaSource(), useFallbackForResolution: false);
            RebuildMetadataTags(tags);

            _metadataSummaryRow.Show();
            _metadataTagRow.Show();
            _metadataContainer.Show();
        }

        private void RebuildMetadataTags(List<string> tags)
        {
            DisposeRowChildren(_metadataTagRow);

            if (tags == null || tags.Count == 0)
                return;

            foreach (var tag in tags)
            {
                var chip = CreateMetadataChip(tag);
                _metadataTagRow.Add(chip);
            }
        }

        private static void DisposeRowChildren(View row)
        {
            while (row != null && row.ChildCount > 0)
            {
                var child = row.GetChildAt(0);
                row.Remove(child);
                try { child.Dispose(); } catch { }
            }
        }

        private static View CreateMetadataChip(string text)
        {
            bool isDolbyVisionChip =
                string.Equals(text, DetailsScreenHelpers.DolbyVisionChipToken, StringComparison.Ordinal) ||
                string.Equals(text?.Trim(), "Dolby Vision", StringComparison.OrdinalIgnoreCase);
            bool isDolbyAudioChip = !isDolbyVisionChip &&
                !string.IsNullOrWhiteSpace(text) &&
                text.StartsWith(DetailsScreenHelpers.DolbyAudioChipPrefix, StringComparison.Ordinal);
            string chipLabelText = isDolbyAudioChip
                ? text.Substring(DetailsScreenHelpers.DolbyAudioChipPrefix.Length).Trim()
                : text;

            var chip = new View
            {
                WidthResizePolicy = ResizePolicyType.FitToChildren,
                HeightSpecification = 48,
                BackgroundColor = UiTheme.DetailsChipSurface,
                CornerRadius = 12.0f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                ClippingMode = ClippingModeType.ClipChildren,
                Padding = new Extents(12, 12, 8, 8),
                Margin = new Extents(0, 0, 2, 2),
                BorderlineWidth = 1.0f,
                BorderlineColor = new Color(1f, 1f, 1f, 0.14f),
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            bool hasIcon = false;
            if (isDolbyVisionChip || isDolbyAudioChip)
            {
                string iconFile = isDolbyVisionChip ? "dolby_vision.svg" : "dolby_audio.svg";
                string iconPath = IOPath.Combine(Tizen.Applications.Application.Current.DirectoryInfo.SharedResource, iconFile);
                if (File.Exists(iconPath))
                {
                    int iconWidth = isDolbyVisionChip ? 124 : 24;
                    int iconHeight = isDolbyVisionChip ? 22 : 20;
                    chip.Add(new ImageView
                    {
                        WidthSpecification = iconWidth,
                        HeightSpecification = iconHeight,
                        ResourceUrl = iconPath,
                        PreMultipliedAlpha = false,
                        FittingMode = FittingModeType.ShrinkToFit,
                        SamplingMode = SamplingModeType.BoxThenLanczos,
                        Margin = isDolbyVisionChip ? new Extents(0, 0, 0, 0) : new Extents(0, 8, 0, 0)
                    });
                    hasIcon = true;
                }
            }

            if (isDolbyVisionChip)
            {
                chip.Padding = new Extents(10, 10, 8, 8);
                if (hasIcon)
                    return chip;
                chipLabelText = "Dolby Vision";
            }

            var label = new TextLabel(chipLabelText)
            {
                WidthResizePolicy = ResizePolicyType.UseNaturalSize,
                HeightResizePolicy = ResizePolicyType.UseNaturalSize,
                PointSize = 20,
                TextColor = new Color(0.98f, 0.98f, 0.98f, 1f),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            label.SetFontStyle(new Tizen.NUI.Text.FontStyle { Weight = FontWeightType.Bold });

            chip.Add(label);
            return chip;
        }

        private static string BuildSummaryText(JellyfinMovie media)
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

        private static string FormatRuntimeForMetadata(long ticks)
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
    }
}