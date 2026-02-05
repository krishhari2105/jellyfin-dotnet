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
    public class SeriesDetailsScreen : ScreenBase, IKeyHandler
    {
        private const int PosterWidth = 420;
        private const int PosterHeight = 630;
        private readonly JellyfinMovie _series;
        private View _infoColumn;
        private View _seasonsView;
        private List<JellyfinMovie> _seasons;
        private readonly List<View> _seasonViews = new();
        private int _seasonIndex = -1;
        private bool _isSeasonViewFocused;

        public SeriesDetailsScreen(JellyfinMovie series)
        {
            _series = series;
            var root = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent
            };

            var apiKey = Uri.EscapeDataString(AppState.AccessToken);
            var serverUrl = AppState.Jellyfin.ServerUrl;

            var backdropUrl =
                $"{serverUrl}/Items/{_series.Id}/Images/Backdrop/0" +
                "?maxWidth=1920&quality=90" +
                $"&api_key={apiKey}";

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
                $"{serverUrl}/Items/{_series.Id}/Images/Primary/0" +
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

            var title = new TextLabel(_series.Name)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                PointSize = 64,
                TextColor = Color.White,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word
            };

            var overview = new TextLabel(
                string.IsNullOrEmpty(_series.Overview)
                    ? "No overview available."
                    : _series.Overview
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
            if (_seasons == null)
            {
                _ = LoadSeasonsAsync();
            }
            else if (_seasonViews.Count > 0)
            {
                _isSeasonViewFocused = true;

                // FIX: Restore the PREVIOUS index instead of forcing 0.
                // If _seasonIndex is -1 (first run), it defaults to 0.
                int targetIndex = (_seasonIndex >= 0 && _seasonIndex < _seasonViews.Count)
                    ? _seasonIndex
                    : 0;

                FocusSeason(targetIndex);
            }
        }

        private async Task LoadSeasonsAsync()
        {
            var loading = new TextLabel("Loading seasons...")
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                PointSize = 32,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _infoColumn.Add(loading);

            _seasons = await AppState.Jellyfin.GetSeasonsAsync(_series.Id);
            _infoColumn.Remove(loading);

            _seasonsView = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical
                }
            };

            var seasonsScrollView = new ScrollableBase
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent
            };
            seasonsScrollView.Add(_seasonsView);

            foreach (var season in _seasons)
            {
                var seasonView = new View
                {
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightSpecification = 80,
                    Focusable = true,
                    Padding = new Extents(50, 20, 20, 20),
                    CornerRadius = 12.0f,
                    Margin = new Extents(0, 0, 0, 12)
                };

                var seasonLabel = new TextLabel(season.Name)
                {
                    PointSize = 30,
                    TextColor = Color.White,
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightResizePolicy = ResizePolicyType.FillToParent,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Begin,
                    Padding = new Extents(30, 20, 0, 0)
                };

                seasonView.Add(seasonLabel);
                _seasonsView.Add(seasonView);
                _seasonViews.Add(seasonView);
            }

            _infoColumn.Add(seasonsScrollView);
            _isSeasonViewFocused = true;
            FocusSeason(0);
        }

        public void HandleKey(AppKey key)
        {
            if (_isSeasonViewFocused)
            {
                HandleSeasonKey(key);
                return;
            }

            switch (key)
            {
                case AppKey.Down:
                    if (_seasons != null)
                    {
                        _isSeasonViewFocused = true;
                        FocusSeason(0);
                    }
                    break;
                case AppKey.Back:
                    NavigationService.NavigateBack();
                    break;
            }
        }

        private void HandleSeasonKey(AppKey key)
        {
            switch (key)
            {
                case AppKey.Up:
                    MoveSeasonFocus(-1);
                    break;
                case AppKey.Down:
                    MoveSeasonFocus(1);
                    break;
                case AppKey.Enter:
                    NavigationService.Navigate(
                        new SeasonDetailsScreen(_seasons[_seasonIndex])
                    );
                    break;
                case AppKey.Back:
                    NavigationService.NavigateBack();
                    break;
            }
        }

        private void MoveSeasonFocus(int delta)
        {
            if (_seasonViews.Count == 0) return;
            var newIndex = Math.Clamp(_seasonIndex + delta, 0, _seasonViews.Count - 1);
            FocusSeason(newIndex);
        }

        private void FocusSeason(int index)
        {
            // 1. Un-highlight old
            if (_seasonIndex >= 0 && _seasonIndex < _seasonViews.Count)
            {
                _seasonViews[_seasonIndex].BackgroundColor = Color.Transparent;
                _seasonViews[_seasonIndex].Scale = Vector3.One;
            }

            // 2. Update Index
            _seasonIndex = index;

            // 3. Highlight new
            if (_seasonIndex >= 0 && _seasonIndex < _seasonViews.Count)
            {
                var view = _seasonViews[_seasonIndex];
                view.BackgroundColor = new Color(0.2f, 0.2f, 0.2f, 1);
                view.Scale = Vector3.One;

                // CRITICAL: Use Post to ensure this runs AFTER the layout pass.
                // This fixes the "Blue Outline" being on the wrong item.
                Tizen.Applications.CoreApplication.Post(() =>
                {
                    FocusManager.Instance.SetCurrentFocusView(view);
                });
            }
        }

    }
}