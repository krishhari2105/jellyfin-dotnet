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
        private const int EpisodeThumbWidth = 640;
        private const int EpisodeThumbHeight = 360;
        private const float SeriesTitlePointSize = 28f;
        private readonly JellyfinMovie _episode;

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
            var episodeTitleText = DetailsScreenHelpers.BuildEpisodeTitle(_episode);
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
            // Litefin-style: always re-fetch server-truth resume state on every OnShow.
            _ = RefreshResumeStateFromServerAsync();
            if (_buttons.Count > 0)
                FocusButton(0);
            RunOnUiThread(RefreshOverviewScrollBounds);
            ScheduleActionButtonReflow();
        }

        public override void OnHide()
        {
            UiAnimator.StopAndDisposeAll(_focusAnimations);
            HideSelectionPanel();
            base.OnHide();
        }

        private async Task LoadMediaSourcesAndSubtitlesAsync()
        {
            if (_mediaSourcesLoaded && _subtitleStreamsLoaded)
                return;

            try
            {
                var playbackInfo = await AppState.Jellyfin.GetPlaybackInfoAsync(_episode.Id, subtitleHandlingDisabled: true);
                _mediaSources = playbackInfo?.MediaSources ?? new List<MediaSourceInfo>();

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
                // Re-derive Resume button state from the authoritative
                // GetMediaItem().PlaybackPositionTicks BEFORE repainting, so this
                // network-fetch-completion path cannot stomp an optimistic local update with
                // stale field state. RebuildActionButtons is only the dumb repaint step.
                ReconcileResumeButtonFromMediaItem();
                RebuildActionButtons(includeVersionButton: _mediaSources.Count > 1);
                UpdateVersionButtonText();
                UpdateMetadataView();
                ScheduleActionButtonReflow();

                if (_buttons.Count > 0)
                    FocusButton(_buttonIndex);
            }
            catch (Exception ex)
            {
                TailscaleDebugLog.Add($"EpisodeDetailsScreen: Failed to load media sources on load/retry: {ex.Message}");
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

                    // Re-derive Resume button state from the current authoritative
                    // mediaItem.PlaybackPositionTicks before rebuilding, so this reflow does
                    // not repaint stale button state.
                    ReconcileResumeButtonFromMediaItem();

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

        protected override int ThumbnailWidthForLayout => EpisodeThumbWidth;

        protected override bool UseFallbackForResolution => false;

        protected override JellyfinMovie GetMediaItem() => _episode;

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