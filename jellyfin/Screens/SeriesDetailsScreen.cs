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
    public class SeriesDetailsScreen : ScreenBase, IKeyHandler
    {
        private const int PosterWidth = 420;
        private const int PosterHeight = 630;
        private const int SeasonCardWidth = 260;
        private const int SeasonCardHeight = 390;
        private const int PreferredSeasonCardTextHeight = 96;
        private const int SeasonCardSpacing = UiTheme.LibraryCardSpacing;
        private const int FocusBorder = 4;
        private const int FocusPad = UiTheme.HomeFocusPad;
        private const int SeasonRowTopInset = FocusPad;
        private const float FocusScale = UiTheme.MediaCardFocusScale;
        private const int TitleLogoMaxWidth = 720;
        private const int TitleLogoQuality = 76;
        private const int TitleLogoDisplayWidth = 720;
        private const int TitleLogoDisplayHeight = 136;

        private readonly JellyfinMovie _series;
        private View _infoColumn;
        private View _seasonViewport;
        private View _seasonRowContainer;
        private List<JellyfinMovie> _seasons;
        private readonly List<View> _seasonViews = new();
        private int _seasonIndex = -1;
        private bool _isSeasonViewFocused;
        private int _seasonCardTextHeight = PreferredSeasonCardTextHeight;

        public SeriesDetailsScreen(JellyfinMovie series)
        {
            _series = series;
            var root = UiFactory.CreateAtmosphericBackground();

            var apiKey = Uri.EscapeDataString(AppState.AccessToken);
            var serverUrl = AppState.Jellyfin.ServerUrl;

            var backdropUrl = JellyfinImageUrlBuilder.BuildBackdropUrl(
                _series,
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

            var posterUrl =
                $"{serverUrl}/Items/{_series.Id}/Images/Primary/0" +
                $"?maxWidth={PosterWidth}&quality=95&api_key={apiKey}";
            posterUrl = AppState.RewriteImageUrlForTailscale(posterUrl);

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

            var title = _series.HasLogo
                ? CreateLogoTitle(_series)
                : CreateTextTitle(_series.Name);

            var metadata = BuildMetadataRow(_series);
            var overviewViewport = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 260,
                ClippingMode = ClippingModeType.ClipChildren
            };
            var overview = new TextLabel(
                string.IsNullOrEmpty(_series.Overview)
                    ? "No overview available."
                    : _series.Overview
            )
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.UseNaturalSize,
                PointSize = 32,
                TextColor = UiTheme.DetailsOverviewText,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word
            };
            overviewViewport.Add(overview);

            _infoColumn.Add(title);
            _infoColumn.Add(metadata);
            _infoColumn.Add(overviewViewport);

            content.Add(posterFrame);
            content.Add(_infoColumn);

            root.Add(backdrop);
            root.Add(dimOverlay);
            root.Add(content);

            Add(root);
        }

        private View CreateLogoTitle(JellyfinMovie series)
        {
            var logoUrl = AppState.GetItemLogoUrl(series.Id, TitleLogoMaxWidth, TitleLogoQuality);
            if (string.IsNullOrWhiteSpace(logoUrl))
                return CreateTextTitle(series.Name);

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

        private static TextLabel CreateTextTitle(string text)
        {
            return new TextLabel(text)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                PointSize = 64,
                TextColor = Color.White,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word
            };
        }

        private static View BuildMetadataRow(JellyfinMovie media)
        {
            var row = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 40,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    CellPadding = new Size2D(18, 0)
                }
            };

            var parts = new List<string>();
            if (media.ProductionYear > 0)
                parts.Add(media.ProductionYear.ToString());
            var runtime = FormatRuntime(media.RunTimeTicks);
            if (!string.IsNullOrWhiteSpace(runtime))
                parts.Add(runtime);
            if (!string.IsNullOrWhiteSpace(media.OfficialRating))
                parts.Add(media.OfficialRating.Trim());

            var label = new TextLabel(string.Join("  ", parts))
            {
                WidthResizePolicy = ResizePolicyType.UseNaturalSize,
                HeightResizePolicy = ResizePolicyType.UseNaturalSize,
                PointSize = 28,
                TextColor = new Color(0.88f, 0.88f, 0.88f, 1f),
                VerticalAlignment = VerticalAlignment.Center
            };
            row.Add(label);
            return row;
        }

        private static string FormatRuntime(long ticks)
        {
            if (ticks <= 0) return null;
            var totalMinutes = (int)(TimeSpan.FromTicks(ticks).TotalMinutes);
            if (totalMinutes <= 0) return null;
            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;
            if (hours <= 0) return $"{totalMinutes}m";
            if (minutes == 0) return $"{hours}h";
            return $"{hours}h {minutes}m";
        }

        public override void OnShow()
        {
            if (_seasons == null || _seasons.Count == 0)
                _ = LoadSeasonsAsync();
        }

        private async Task LoadSeasonsAsync()
        {
            try
            {
                var seasonList = await AppState.Jellyfin.GetSeasonsAsync(_series.Id);
                _seasons = seasonList ?? new List<JellyfinMovie>();
                _seasonCardTextHeight = CalculateSeasonCardTextHeight(_seasons);
                BuildSeasonRow();
                if (_seasonViews.Count > 0)
                {
                    _isSeasonViewFocused = true;
                    FocusSeason(0);
                }
            }
            catch
            {
                _seasons = new List<JellyfinMovie>();
            }
        }

        private int CalculateSeasonCardTextHeight(List<JellyfinMovie> seasons)
        {
            if (seasons == null || seasons.Count == 0)
                return PreferredSeasonCardTextHeight;

            int maxTextHeight = PreferredSeasonCardTextHeight;
            foreach (var season in seasons)
            {
                if (season == null) continue;
                maxTextHeight = Math.Max(
                    maxTextHeight,
                    MediaCardFactory.GetRecommendedTextHeight(
                        SeasonCardWidth,
                        PreferredSeasonCardTextHeight,
                        season.Name,
                        null,
                        (int)UiTheme.MediaCardTitle,
                        (int)UiTheme.MediaCardSubtitle));
            }
            return maxTextHeight;
        }

        private void BuildSeasonRow()
        {
            if (_seasons == null || _seasons.Count == 0) return;

            var cardHeight = SeasonCardHeight + _seasonCardTextHeight;

            var title = new TextLabel("Seasons")
            {
                WidthResizePolicy = ResizePolicyType.UseNaturalSize,
                HeightResizePolicy = ResizePolicyType.UseNaturalSize,
                Margin = new Extents((ushort)FocusPad, 0, 0, 0),
                PointSize = 32,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Begin
            };

            _seasonViewport = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = cardHeight + (FocusPad * 2),
                ClippingMode = ClippingModeType.ClipChildren
            };

            _seasonRowContainer = new View
            {
                PositionX = FocusPad,
                PositionY = SeasonRowTopInset,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    CellPadding = new Size2D(SeasonCardSpacing, 0)
                }
            };

            foreach (var season in _seasons)
            {
                var card = CreateSeasonCard(season);
                _seasonViews.Add(card);
                _seasonRowContainer.Add(card);
            }

            _seasonViewport.Add(_seasonRowContainer);
            _infoColumn.Add(title);
            _infoColumn.Add(_seasonViewport);
        }

        private View CreateSeasonCard(JellyfinMovie season)
        {
            var apiKey = Uri.EscapeDataString(AppState.AccessToken);
            var serverUrl = AppState.Jellyfin.ServerUrl;
            string imageUrl = null;

            if (season.HasThumb)
            {
                imageUrl =
                    $"{serverUrl}/Items/{season.Id}/Images/Thumb/0" +
                    $"?maxWidth={SeasonCardWidth}&quality=90&api_key={apiKey}";
                imageUrl = AppState.RewriteImageUrlForTailscale(imageUrl);
            }
            else if (season.HasPrimary)
            {
                imageUrl =
                    $"{serverUrl}/Items/{season.Id}/Images/Primary/0" +
                    $"?maxWidth={SeasonCardWidth}&quality=90&api_key={apiKey}";
                imageUrl = AppState.RewriteImageUrlForTailscale(imageUrl);
            }

            return MediaCardFactory.CreateImageCard(
                SeasonCardWidth,
                SeasonCardHeight,
                _seasonCardTextHeight,
                season.Name,
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
            if (_isSeasonViewFocused)
            {
                HandleSeasonKey(key);
                return;
            }

            if (key == AppKey.Down && _seasonViews.Count > 0)
            {
                _isSeasonViewFocused = true;
                FocusSeason(0);
                return;
            }

            if (key == AppKey.Back)
                NavigationService.NavigateBack();
        }

        private void HandleSeasonKey(AppKey key)
        {
            switch (key)
            {
                case AppKey.Left:
                    MoveSeasonFocus(-1);
                    break;
                case AppKey.Right:
                    MoveSeasonFocus(1);
                    break;
                case AppKey.Enter:
                    if (_seasonIndex >= 0 && _seasonIndex < _seasons.Count)
                        NavigationService.NavigateWithLoading(
                            () => new SeasonDetailsScreen(_seasons[_seasonIndex]),
                            "Loading season..."
                        );
                    break;
                case AppKey.Back:
                    _isSeasonViewFocused = false;
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
            if (_seasonViews.Count == 0) return;

            if (_seasonIndex >= 0 && _seasonIndex < _seasonViews.Count)
                ApplySeasonFocus(_seasonViews[_seasonIndex], false);

            _seasonIndex = Math.Clamp(index, 0, _seasonViews.Count - 1);
            ApplySeasonFocus(_seasonViews[_seasonIndex], true);
            ScrollSeasonsIfNeeded();
        }

        private static void ApplySeasonFocus(View card, bool focused)
        {
            var frame = MediaCardFocus.GetCardFrame(card);
            var scaleTarget = frame ?? card;
            scaleTarget.Scale = focused ? new Vector3(FocusScale, FocusScale, 1f) : Vector3.One;
            if (frame != null)
            {
                frame.PositionZ = focused ? 20 : 0;
                card.PositionZ = 0;
            }
            else
            {
                card.PositionZ = focused ? 20 : 0;
            }

            if (focused)
                MediaCardFocus.ApplyFrameFocus(frame, UiTheme.MediaCardFocusBorder);
            else
                MediaCardFocus.ClearFrameFocus(frame);
        }

        private void ScrollSeasonsIfNeeded()
        {
            if (_seasonRowContainer == null || _seasonViewport == null || _seasonViews.Count == 0)
                return;

            if (_seasonIndex == 0)
            {
                _seasonRowContainer.PositionX = FocusPad;
                return;
            }

            var offset = -_seasonRowContainer.PositionX;
            var viewportWidth = _seasonViewport.SizeWidth;
            var focused = _seasonViews[_seasonIndex];

            var left = focused.PositionX;
            var right = left + SeasonCardWidth;

            var visibleLeft = offset;
            var visibleRight = offset + viewportWidth;
            var targetX = _seasonRowContainer.PositionX;

            if (right > visibleRight)
                targetX -= (right - visibleRight + SeasonCardSpacing);
            else if (left < visibleLeft)
                targetX += (visibleLeft - left + SeasonCardSpacing);

            _seasonRowContainer.PositionX = targetX;
        }
    }
}