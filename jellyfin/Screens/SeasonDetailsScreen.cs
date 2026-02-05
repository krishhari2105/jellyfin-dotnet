using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using Tizen.NUI.Components;
using JellyfinTizen.Core;
using JellyfinTizen.Models;

namespace JellyfinTizen.Screens
{
    public class SeasonDetailsScreen : ScreenBase, IKeyHandler
    {
        private const int PosterWidth = 420;
        private const int PosterHeight = 630;
        private readonly JellyfinMovie _season;
        private View _infoColumn;
        private View _episodesView;
        private List<JellyfinMovie> _episodes;
        private readonly List<View> _episodeViews = new();
        private int _episodeIndex = -1;
        private bool _isEpisodeViewFocused;

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
                BackgroundColor = new Color(0, 0, 0, 0.65f)
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
                BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f)
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

            var overview = new TextLabel(
                string.IsNullOrEmpty(_season.Overview)
                    ? "No overview available."
                    : _season.Overview
            )
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 360,
                PointSize = 32,
                TextColor = new Color(0.85f, 0.85f, 0.85f, 1f),
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word
            };

            _infoColumn.Add(title);
            _infoColumn.Add(overview);

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

            foreach (var episode in _episodes)
            {
                var episodeView = new View
                {
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightSpecification = 80,
                    Focusable = true,
                    Padding = new Extents(50, 20, 20, 20),
                    CornerRadius = 12.0f,
                    Margin = new Extents(0, 0, 0, 12)
                };

                var episodeLabel = new TextLabel($"{episode.IndexNumber}. {episode.Name}")
                {
                    PointSize = 30,
                    TextColor = Color.White,
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightResizePolicy = ResizePolicyType.FillToParent,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Begin,
                    Padding = new Extents(30, 20, 0, 0)
                };

                episodeView.Add(episodeLabel);
                _episodesView.Add(episodeView);
                _episodeViews.Add(episodeView);
            }

            _infoColumn.Add(episodesScrollView);
            _isEpisodeViewFocused = true;
            FocusEpisode(0);
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
                case AppKey.Up:
                    MoveEpisodeFocus(-1);
                    break;
                case AppKey.Down:
                    MoveEpisodeFocus(1);
                    break;
                case AppKey.Enter:
                    NavigationService.Navigate(
                        new EpisodeDetailsScreen(_episodes[_episodeIndex])
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
            if (_episodeIndex >= 0 && _episodeIndex < _episodeViews.Count)
            {
                _episodeViews[_episodeIndex].BackgroundColor = Color.Transparent;
                _episodeViews[_episodeIndex].Scale = Vector3.One;
            }

            _episodeIndex = index;

            if (_episodeIndex >= 0 && _episodeIndex < _episodeViews.Count)
            {
                var view = _episodeViews[_episodeIndex];
                view.BackgroundColor = new Color(0.2f, 0.2f, 0.2f, 1);
                view.Scale = Vector3.One;

                // CRITICAL: Sync Tizen focus
                Tizen.Applications.CoreApplication.Post(() =>
                {
                    FocusManager.Instance.SetCurrentFocusView(view);
                });
            }
        }
        
    }
}