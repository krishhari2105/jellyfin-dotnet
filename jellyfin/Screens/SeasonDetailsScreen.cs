using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Core;
using JellyfinTizen.Models;
using JellyfinTizen.UI;
using JellyfinTizen.Utils;

namespace JellyfinTizen.Screens
{
    public class SeasonDetailsScreen : ScreenBase, IKeyHandler
    {
        private const int PosterWidth = 420;
        private const int PosterHeight = 630;
        private const int EpisodeCardWidth = 420;
        private const int EpisodeCardHeight = 236;
        private const int EpisodeCardTextHeight = 90;
        private const int EpisodeCardSpacing = 30;
        private const int FocusBorder = 4;
        private const int FocusPad = 20;
        private const float FocusScale = 1.14f;

        private readonly Color _focusBorderColor = UiTheme.MediaCardFocusFill;
        private readonly JellyfinMovie _season;
        private View _infoColumn;
        private View _episodeViewport;
        private View _episodeRowContainer;
        private List<JellyfinMovie> _episodes;
        private readonly List<View> _episodeViews = new();
        private int _episodeIndex = -1;
        private bool _isEpisodeViewFocused;
        private Animation _episodeScrollAnimation;

        public SeasonDetailsScreen(JellyfinMovie season)
        {
            _season = season;
            var root = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent
            };

            var apiKey = Uri.EscapeDataString(AppState.AccessToken);
            var serverUrl = AppState.Jellyfin.ServerUrl;

            // Use season backdrop if available, otherwise try to use series backdrop
            var backdropUrl = _season.HasBackdrop
                ? $"{serverUrl}/Items/{_season.Id}/Images/Backdrop/0?maxWidth=1920&quality=90&api_key={apiKey}"
                : $"{serverUrl}/Items/{_season.SeriesId}/Images/Backdrop/0?maxWidth=1920&quality=90&api_key={apiKey}";

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
                BackgroundColor = UiTheme.DetailsBackdropDim
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
                $"{serverUrl}/Items/{_season.Id}/Images/Primary/0" +
                $"?maxWidth={PosterWidth}&quality=95&api_key={apiKey}";

            var posterFrame = new View
            {
                WidthSpecification = PosterWidth,
                HeightSpecification = PosterHeight,
                BackgroundColor = UiTheme.DetailsPosterSurface,
                CornerRadius = 12.0f
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

            var title = new TextLabel(_season.Name)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                PointSize = 64,
                TextColor = Color.White,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word
            };

            var overviewViewport = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 260,
                ClippingMode = ClippingModeType.ClipChildren
            };

            var overview = new TextLabel(
                string.IsNullOrEmpty(_season.Overview)
                    ? "No overview available."
                    : _season.Overview
            )
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.UseNaturalSize,
                PointSize = 32,
                TextColor = UiTheme.DetailsOverviewText,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word,
                Ellipsis = false
            };

            overviewViewport.Add(overview);

            _infoColumn.Add(title);
            _infoColumn.Add(overviewViewport);

            content.Add(posterFrame);
            content.Add(_infoColumn);

            root.Add(backdrop);
            root.Add(dimOverlay);
            root.Add(content);

            Add(root);
        }

        public override void OnShow()
        {
            if (_episodes == null)
            {
                _ = LoadEpisodesAsync();
            }
            else if (_episodeViews.Count > 0)
            {
                _isEpisodeViewFocused = true;

                // FIX: Restore previous index
                int targetIndex = (_episodeIndex >= 0 && _episodeIndex < _episodeViews.Count)
                    ? _episodeIndex
                    : 0;

                FocusEpisode(targetIndex);
            }
        }

        public override void OnHide()
        {
            UiAnimator.StopAndDispose(ref _episodeScrollAnimation);
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

            _episodes = await AppState.Jellyfin.GetEpisodesAsync(_season.Id);
            _infoColumn.Remove(loading);

            var episodesTitle = new TextLabel("Episodes")
            {
                WidthResizePolicy = ResizePolicyType.UseNaturalSize,
                HeightResizePolicy = ResizePolicyType.UseNaturalSize,
                Margin = new Extents((ushort)FocusPad, 0, 0, 0),
                PointSize = 32,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Begin,
                Ellipsis = false
            };

            var cardHeight = EpisodeCardHeight + EpisodeCardTextHeight;

            _episodeViewport = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = cardHeight + (FocusPad * 2),
                ClippingMode = ClippingModeType.ClipChildren
            };

            _episodeRowContainer = new View
            {
                PositionX = FocusPad,
                PositionY = FocusPad,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    CellPadding = new Size2D(EpisodeCardSpacing, 0)
                }
            };

            _episodeViews.Clear();

            foreach (var episode in _episodes)
            {
                var card = CreateEpisodeCard(episode);
                _episodeViews.Add(card);
                _episodeRowContainer.Add(card);
            }

            _episodeViewport.Add(_episodeRowContainer);
            _infoColumn.Add(episodesTitle);
            _infoColumn.Add(_episodeViewport);
            _isEpisodeViewFocused = true;
            FocusEpisode(0);
        }

        private View CreateEpisodeCard(JellyfinMovie episode)
        {
            var apiKey = Uri.EscapeDataString(AppState.AccessToken);
            var serverUrl = AppState.Jellyfin.ServerUrl;
            string imageUrl = null;

            if (episode.HasThumb)
            {
                imageUrl =
                    $"{serverUrl}/Items/{episode.Id}/Images/Thumb/0" +
                    $"?maxWidth={EpisodeCardWidth}&quality=90&api_key={apiKey}";
            }
            else if (episode.HasPrimary)
            {
                imageUrl =
                    $"{serverUrl}/Items/{episode.Id}/Images/Primary/0" +
                    $"?maxWidth={EpisodeCardWidth}&quality=90&api_key={apiKey}";
            }
            else if (episode.HasBackdrop)
            {
                imageUrl =
                    $"{serverUrl}/Items/{episode.Id}/Images/Backdrop/0" +
                    $"?maxWidth={EpisodeCardWidth}&quality=85&api_key={apiKey}";
            }

            var episodeNumber = episode.IndexNumber > 0 ? $"E{episode.IndexNumber} - " : string.Empty;
            return MediaCardFactory.CreateImageCard(
                EpisodeCardWidth,
                EpisodeCardHeight,
                EpisodeCardTextHeight,
                $"{episodeNumber}{episode.Name}",
                subtitle: null,
                imageUrl: imageUrl,
                out _,
                focusBorder: FocusBorder,
                titlePoint: (int)UiTheme.MediaCardTitle,
                subtitlePoint: (int)UiTheme.MediaCardSubtitle
            );
        }

        public void HandleKey(AppKey key)
        {
            if (_isEpisodeViewFocused)
            {
                HandleEpisodeKey(key);
                return;
            }

            switch (key)
            {
                case AppKey.Down:
                    if (_episodes != null)
                    {
                        _isEpisodeViewFocused = true;
                        FocusEpisode(0);
                    }
                    break;
                case AppKey.Back:
                    NavigationService.NavigateBack();
                    break;
            }
        }

        private void HandleEpisodeKey(AppKey key)
        {
            switch (key)
            {
                case AppKey.Left:
                    MoveEpisodeFocus(-1);
                    break;
                case AppKey.Right:
                    MoveEpisodeFocus(1);
                    break;
                case AppKey.Enter:
                    NavigationService.NavigateWithLoading(
                        () => new EpisodeDetailsLoadingScreen(_episodes[_episodeIndex]),
                        "Loading episode..."
                    );
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
            if (_episodeViews.Count == 0) return;

            if (_episodeIndex >= 0 && _episodeIndex < _episodeViews.Count)
                ApplyEpisodeFocus(_episodeViews[_episodeIndex], false);

            _episodeIndex = Math.Clamp(index, 0, _episodeViews.Count - 1);
            ApplyEpisodeFocus(_episodeViews[_episodeIndex], true);
            ScrollEpisodesIfNeeded();

            Tizen.Applications.CoreApplication.Post(() =>
            {
                FocusManager.Instance.SetCurrentFocusView(_episodeViews[_episodeIndex]);
            });
        }

        private void ApplyEpisodeFocus(View card, bool focused)
        {
            var frame = MediaCardFocus.GetCardFrame(card);
            var content = MediaCardFocus.GetCardContent(card);

            if (content != null)
                content.Scale = focused ? new Vector3(FocusScale, FocusScale, 1f) : Vector3.One;

            card.Scale = Vector3.One;
            card.PositionZ = focused ? 20 : 0;

            if (focused)
                MediaCardFocus.ApplyFrameFocus(frame, _focusBorderColor, UiTheme.MediaCardFocusBorder, lightweight: false);
            else
                MediaCardFocus.ClearFrameFocus(frame);
        }

        private void ScrollEpisodesIfNeeded()
        {
            if (_episodeRowContainer == null || _episodeViewport == null || _episodeViews.Count == 0)
                return;

            if (_episodeIndex == 0)
            {
                AnimateEpisodeRowTo(FocusPad);
                return;
            }

            var offset = -_episodeRowContainer.PositionX;
            var viewportWidth = _episodeViewport.SizeWidth;
            var focused = _episodeViews[_episodeIndex];

            var left = focused.PositionX;
            var right = left + EpisodeCardWidth;

            var visibleLeft = offset;
            var visibleRight = offset + viewportWidth;
            var targetX = _episodeRowContainer.PositionX;

            if (right > visibleRight)
                targetX -= (right - visibleRight + EpisodeCardSpacing);
            else if (left < visibleLeft)
                targetX += (visibleLeft - left + EpisodeCardSpacing);

            AnimateEpisodeRowTo(targetX);
        }

        private void AnimateEpisodeRowTo(float targetX)
        {
            if (_episodeRowContainer == null)
            {
                return;
            }

            if (Math.Abs(targetX - _episodeRowContainer.PositionX) < 0.5f)
            {
                return;
            }

            UiAnimator.Replace(
                ref _episodeScrollAnimation,
                UiAnimator.AnimateTo(_episodeRowContainer, "PositionX", targetX, UiAnimator.ScrollDurationMs)
            );
        }

    }
}
