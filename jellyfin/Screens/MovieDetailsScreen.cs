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
                ? DetailsScreenHelpers.BuildEpisodeTitle(_mediaItem)
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
            base.OnHide();
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
            catch (Exception ex)
            {
                TailscaleDebugLog.Add($"MovieDetailsScreen: Failed to load media sources on load/retry: {ex.Message}");
                _mediaSourcesLoaded = false;
                _subtitleStreamsLoaded = false;
                throw;
            }
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
        protected override async void PlayMedia(JellyfinMovie media, int startPositionMs)
        {
            if (!_mediaSourcesLoaded)
            {
                try
                {
                    NavigationService.ShowReconnectOverlay("Retrieving media details...");
                    await LoadMediaSourcesAndSubtitlesAsync();
                }
                catch (Exception ex)
                {
                    TailscaleDebugLog.Add($"PlayMedia: Retry failed: {ex.Message}");
                }
                finally
                {
                    NavigationService.HideReconnectOverlay();
                }
            }

            if (!_mediaSourcesLoaded || _mediaSources == null || _mediaSources.Count == 0)
            {
                ShowErrorMessage("Server unreachable. Check connection and try again.");
                return;
            }

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
    }
}