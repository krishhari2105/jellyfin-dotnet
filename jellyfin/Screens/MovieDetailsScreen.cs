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
    public class MovieDetailsScreen : DetailsScreenBase
    {
        private const int PosterWidth = 420;
        private const int PosterHeight = 630;
        private const float EpisodeFocusScale = 1.03f;
        private const int FixedTopContentHeight = 500;
        private const int FixedOverviewViewportHeight = 240;
        private const int OverviewScrollStepPx = 70;
        private const int OverviewScrollTailPx = 28;
        private const int TitleLogoMaxWidth = 720;
        private const int TitleLogoQuality = 76;
        private const int TitleLogoDisplayWidth = 720;
        private const int TitleLogoDisplayHeight = 136;
        private readonly JellyfinMovie _mediaItem;
        private View _episodesView;
        private List<JellyfinMovie> _episodes;
        private readonly List<View> _episodeViews = new();
        private int _episodeIndex = -1;
        private bool _isEpisodeViewFocused;
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

        public bool UsesImageLogoTitle =>
            _mediaItem != null &&
            !_mediaItem.IsEpisode &&
            _mediaItem.HasLogo;

        public MovieDetailsScreen(JellyfinMovie movie) : base(movie)
        {
            _mediaItem = movie;
            var root = UiFactory.CreateAtmosphericBackground();
            var apiKey = Uri.EscapeDataString(AppState.AccessToken);
            var serverUrl = AppState.Jellyfin.ServerUrl;
            var backdropUrl = JellyfinImageUrlBuilder.BuildBackdropUrl(
                _mediaItem,
                serverUrl,
                apiKey,
                maxWidth: 1920,
                fallbackBackdropItemId: _mediaItem.IsEpisode ? _mediaItem.SeriesId : null);
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
            var posterUrl =
                $"{serverUrl}/Items/{_mediaItem.Id}/Images/Primary/0" +
                $"?maxWidth={PosterWidth}&quality=75&api_key={apiKey}";
            posterUrl = AppState.RewriteImageUrlForTailscale(posterUrl);
            var posterFrame = new View
            {
                WidthSpecification = PosterWidth,
                HeightSpecification = PosterHeight,
                BackgroundColor = UiTheme.DetailsPosterSurface,
                CornerRadius = 16.0f,
                ClippingMode = ClippingModeType.ClipChildren
            };
            var poster = new ImageView
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                ResourceUrl = posterUrl,
                PreMultipliedAlpha = false
            };
            posterFrame.Add(poster);
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
            var titleText = _mediaItem.IsEpisode
                ? BuildEpisodeTitle(_mediaItem)
                : _mediaItem.Name;
            var title = CreateDetailsTitleView(titleText);
            _metadataContainer = CreateMetadataView();
            var overviewText = string.IsNullOrEmpty(_mediaItem.Overview)
                ? "No overview available."
                : _mediaItem.Overview;
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
            topContent.Add(title);
            topContent.Add(_metadataContainer);
            topContent.Add(_overviewViewport);
            topContentViewport.Add(topContent);
            _infoColumn.Add(topContentViewport);
            UpdateMetadataView();
            if (_mediaItem.IsPlayableVideo)
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
            content.Add(posterFrame);
            content.Add(_infoColumn);
            root.Add(backdrop);
            root.Add(dimOverlay);
            root.Add(content);
            Add(root);
            NormalizeSelectionStateForCurrentMediaSource();
        }
        public override void OnShow()
        {
            if (_mediaItem.IsSeries)
            {
                _ = LoadEpisodesAsync();
            }
            else
            {
                if (!_mediaSourcesLoaded || !_subtitleStreamsLoaded)
                    _ = LoadMediaSourcesAndSubtitlesAsync();
                if (_buttons.Count > 0)
                    FocusButton(0);
                RunOnUiThread(RefreshOverviewScrollBounds);
                ScheduleActionButtonReflow();
            }
        }

        public override void OnHide()
        {
            UiAnimator.StopAndDisposeAll(_focusAnimations);
            HideSelectionPanel();
        }

        public override void HandleKey(AppKey key)
        {
            if (HandleSelectionPanelKey(key))
                return;

            if (_isEpisodeViewFocused)
            {
                HandleEpisodeKey(key);
                return;
            }
            switch (key)
            {
                case AppKey.Left:
                    MoveFocus(-1);
                    break;
                case AppKey.Right:
                    MoveFocus(1);
                    break;
                case AppKey.Up:
                    if (!_mediaItem.IsSeries)
                        ScrollOverview(-OverviewScrollStepPx);
                    break;
                case AppKey.Enter:
                    ActivateFocusedButton();
                    break;
                case AppKey.Back:
                    NavigationService.NavigateBack();
                    break;
                case AppKey.Down:
                    if (_mediaItem.IsSeries && _episodes != null)
                    {
                        _isEpisodeViewFocused = true;
                        FocusEpisode(0);
                    }
                    else if (!_mediaItem.IsSeries)
                    {
                        ScrollOverview(OverviewScrollStepPx);
                    }
                    break;
            }
        }

        private View CreateDetailsTitleView(string fallbackText)
        {
            // Episodes should always keep textual title.
            if (!UsesImageLogoTitle)
                return CreateDetailsTitleLabel(fallbackText);

            var logoUrl = AppState.GetItemLogoUrl(_mediaItem.Id, TitleLogoMaxWidth, TitleLogoQuality);
            if (string.IsNullOrWhiteSpace(logoUrl))
                return CreateDetailsTitleLabel(fallbackText);

            var logoContainer = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = TitleLogoDisplayHeight,
                ClippingMode = ClippingModeType.ClipChildren,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            };

            var logo = new ImageView
            {
                WidthSpecification = TitleLogoDisplayWidth,
                HeightSpecification = TitleLogoDisplayHeight,
                ResourceUrl = logoUrl,
                PreMultipliedAlpha = false,
                FittingMode = FittingModeType.ShrinkToFit,
                SamplingMode = SamplingModeType.Linear
            };

            logoContainer.Add(logo);
            return logoContainer;
        }

        private static TextLabel CreateDetailsTitleLabel(string text)
        {
            return new TextLabel(text)
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
        private async Task LoadEpisodesAsync()
        {
            var loading = new TextLabel("Loading episodes...")
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                PointSize = 32,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _infoColumn.Add(loading);
            _episodes = await AppState.Jellyfin.GetEpisodesAsync(_mediaItem.Id, lightweight: true);
            _infoColumn.Remove(loading);
            _episodesView = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical
                }
            };
            var episodesScrollView = new ScrollableBase
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent
            };
            episodesScrollView.Add(_episodesView);

            int currentSeason = -1;
            foreach (var episode in _episodes)
            {
                if (episode.ParentIndexNumber != currentSeason)
                {
                    currentSeason = episode.ParentIndexNumber;
                    var seasonHeader = new TextLabel($"Season {currentSeason}")
                    {
                        PointSize = 40,
                        TextColor = Color.White,
                        WidthResizePolicy = ResizePolicyType.FillToParent,
                        Margin = new Extents(0, 0, 20, 10)
                    };
                    _episodesView.Add(seasonHeader);
                }
                var episodeView = new View
                {
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightSpecification = 80,
                    Focusable = true,
                    Padding = new Extents(10, 10, 10, 10),
                    CornerRadius = 24.0f,
                    Margin = new Extents(0, 0, 0, 12)
                };
                var episodeLabel = new TextLabel($"{episode.IndexNumber}. {episode.Name}")
                {
                    PointSize = 30,
                    TextColor = Color.White,
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightResizePolicy = ResizePolicyType.FillToParent,
                    VerticalAlignment = VerticalAlignment.Center
                };
                episodeView.Add(episodeLabel);
                _episodesView.Add(episodeView);
                _episodeViews.Add(episodeView);
            }
            _infoColumn.Add(episodesScrollView);
            _isEpisodeViewFocused = true;
            FocusEpisode(0);
        }

        private async Task LoadMediaSourcesAndSubtitlesAsync()
        {
            if (_mediaSourcesLoaded && _subtitleStreamsLoaded)
                return;

            try
            {
                var playbackInfo = await AppState.Jellyfin.GetPlaybackInfoAsync(_mediaItem.Id, subtitleHandlingDisabled: true);
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
                    Id = _mediaItem.Id,
                    Name = "Default"
                });
            }

            _mediaSourcesLoaded = true;
            _selectedMediaSourceIndex = Math.Clamp(_selectedMediaSourceIndex, 0, _mediaSources.Count - 1);

            // Extract subtitle streams from selected media source to avoid redundant API call
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

            // Recompute on each input so late layout updates can expand the reachable range.
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

        private void HandleEpisodeKey(AppKey key)
        {
            switch (key)
            {
                case AppKey.Up:
                    MoveEpisodeFocus(-1);
                    break;
                case AppKey.Down:
                    MoveEpisodeFocus(1);
                    break;
                case AppKey.Enter:
                    PlayMedia(_episodes[_episodeIndex], 0);
                    break;
                case AppKey.Back:
                    NavigationService.NavigateBack();
                    break;
            }
        }
        private void MoveEpisodeFocus(int delta)
        {
            if (_episodeViews.Count == 0) return;
            var newIndex = Math.Clamp(_episodeIndex + delta, 0, _episodeViews.Count - 1);
            FocusEpisode(newIndex);
        }
        private void FocusEpisode(int index)
        {
            if (_episodeIndex >= 0 && _episodeIndex < _episodeViews.Count)
            {
                _episodeViews[_episodeIndex].BackgroundColor = Color.Transparent;
                SetScaleInstant(_episodeViews[_episodeIndex], Vector3.One);
            }
            _episodeIndex = index;
            if (_episodeIndex >= 0 && _episodeIndex < _episodeViews.Count)
            {
                // Lighter grey for better visibility, no border to keep corners rounded
                _episodeViews[_episodeIndex].BackgroundColor = new Color(0.35f, 0.35f, 0.35f, 1.0f);
                SetScaleInstant(_episodeViews[_episodeIndex], new Vector3(EpisodeFocusScale, EpisodeFocusScale, 1f));
                FocusManager.Instance.SetCurrentFocusView(_episodeViews[_episodeIndex]);
            }
        }

        protected override JellyfinMovie GetMediaItem() => _mediaItem;
        protected override int ThumbnailWidthForLayout => PosterWidth;
        protected override bool UseFallbackForResolution => true;
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

            var summaryText = DetailsScreenHelpers.BuildSummaryText(_mediaItem);
            _metadataSummaryLabel.Text = string.IsNullOrWhiteSpace(summaryText) ? " " : summaryText;

            if (_mediaItem.CommunityRating.HasValue && _mediaItem.CommunityRating.Value > 0)
            {
                _metadataRatingLabel.Text = _mediaItem.CommunityRating.Value.ToString("0.0", CultureInfo.InvariantCulture);
                _metadataRatingGroup.Show();
            }
            else
            {
                _metadataRatingGroup.Hide();
            }

            var tags = DetailsScreenHelpers.BuildTechnicalTags(GetSelectedMediaSource(), useFallbackForResolution: true);
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
    }
}