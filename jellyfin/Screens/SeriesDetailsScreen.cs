using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Core;
using JellyfinTizen.Models;
using JellyfinTizen.Utils;

namespace JellyfinTizen.Screens
{
    public class SeriesDetailsScreen : ScreenBase, IKeyHandler
    {
        private const int PosterWidth = 420;
        private const int PosterHeight = 630;
        private const int SeasonCardWidth = 260;
        private const int SeasonCardHeight = 390;
        private const int SeasonCardTextHeight = 80;
        private const int SeasonCardSpacing = 24;
        private const int FocusBorder = 4;
        private const int FocusPad = 20;
        private const float FocusScale = 1.14f;

        // Jellyfin Blue (#00A4DC)
        private readonly Color _focusBorderColor = new Color(0.0f, 0.64f, 0.86f, 0.45f);
        private readonly JellyfinMovie _series;
        private View _infoColumn;
        private View _seasonViewport;
        private View _seasonRowContainer;
        private List<JellyfinMovie> _seasons;
        private readonly List<View> _seasonViews = new();
        private readonly Dictionary<View, Animation> _focusAnimations = new();
        private int _seasonIndex = -1;
        private bool _isSeasonViewFocused;
        private Animation _seasonScrollAnimation;

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
                BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f),
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

            var title = CreateDetailsTitleView(_series.Name);

            var overview = new TextLabel(
                string.IsNullOrEmpty(_series.Overview)
                    ? "No overview available."
                    : _series.Overview
            )
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 260,
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

        private View CreateDetailsTitleView(string fallbackText)
        {
            if (!_series.HasLogo)
                return CreateDetailsTitleLabel(fallbackText);

            var logoUrl = AppState.GetItemLogoUrl(_series.Id, 960);
            if (string.IsNullOrWhiteSpace(logoUrl))
                return CreateDetailsTitleLabel(fallbackText);

            var logoContainer = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 140,
                ClippingMode = ClippingModeType.ClipChildren
            };

            var logo = new ImageView
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                ResourceUrl = logoUrl,
                PreMultipliedAlpha = false,
                FittingMode = FittingModeType.ShrinkToFit,
                SamplingMode = SamplingModeType.BoxThenLanczos
            };

            logoContainer.Add(logo);
            return logoContainer;
        }

        private static TextLabel CreateDetailsTitleLabel(string text)
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

        public override void OnHide()
        {
            UiAnimator.StopAndDispose(ref _seasonScrollAnimation);
            UiAnimator.StopAndDisposeAll(_focusAnimations);
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

            var seasonsTitle = new TextLabel("Seasons")
            {
                WidthResizePolicy = ResizePolicyType.UseNaturalSize,
                HeightResizePolicy = ResizePolicyType.UseNaturalSize,
                PointSize = 32,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Begin,
                Ellipsis = false
            };

            var cardHeight = SeasonCardHeight + SeasonCardTextHeight;

            _seasonViewport = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = cardHeight + (FocusPad * 2),
                ClippingMode = ClippingModeType.ClipChildren
            };

            _seasonRowContainer = new View
            {
                PositionY = FocusPad,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    CellPadding = new Size2D(SeasonCardSpacing, 0)
                }
            };

            _seasonViews.Clear();

            foreach (var season in _seasons)
            {
                var card = CreateSeasonCard(season);
                _seasonViews.Add(card);
                _seasonRowContainer.Add(card);
            }

            _seasonViewport.Add(_seasonRowContainer);
            _infoColumn.Add(seasonsTitle);
            _infoColumn.Add(_seasonViewport);
            _isSeasonViewFocused = true;
            FocusSeason(0);
        }

        private View CreateSeasonCard(JellyfinMovie season)
        {
            var wrapper = new View
            {
                WidthSpecification = SeasonCardWidth,
                HeightSpecification = SeasonCardHeight + SeasonCardTextHeight,
                Focusable = true,
                BackgroundColor = Color.Transparent,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical
                }
            };

            var frame = new View
            {
                Name = "CardFrame",
                WidthSpecification = SeasonCardWidth,
                HeightSpecification = SeasonCardHeight,
                CornerRadius = 16.0f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                ClippingMode = ClippingModeType.ClipChildren,
                BackgroundColor = Color.Transparent,
                Padding = new Extents(FocusBorder, FocusBorder, FocusBorder, FocusBorder),
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal
                }
            };

            var inner = new View
            {
                Name = "CardInner",
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                CornerRadius = 12.0f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                ClippingMode = ClippingModeType.ClipChildren,
                BackgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f)
            };

            var content = new View
            {
                Name = "CardContent",
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                ClippingMode = ClippingModeType.ClipChildren
            };

            var apiKey = Uri.EscapeDataString(AppState.AccessToken);
            var serverUrl = AppState.Jellyfin.ServerUrl;
            string posterUrl = null;

            if (season.HasPrimary)
            {
                posterUrl =
                    $"{serverUrl}/Items/{season.Id}/Images/Primary/0" +
                    $"?maxWidth={SeasonCardWidth}&quality=90&api_key={apiKey}";
            }
            else if (season.HasThumb)
            {
                posterUrl =
                    $"{serverUrl}/Items/{season.Id}/Images/Thumb/0" +
                    $"?maxWidth={SeasonCardWidth}&quality=90&api_key={apiKey}";
            }
            else if (season.HasBackdrop)
            {
                posterUrl =
                    $"{serverUrl}/Items/{season.Id}/Images/Backdrop/0" +
                    $"?maxWidth={SeasonCardWidth}&quality=85&api_key={apiKey}";
            }

            if (!string.IsNullOrEmpty(posterUrl))
            {
                var image = new ImageView
                {
                    Name = "CardImage",
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightResizePolicy = ResizePolicyType.FillToParent,
                    ResourceUrl = posterUrl,
                    PreMultipliedAlpha = false
                };
                content.Add(image);
            }

            inner.Add(content);
            frame.Add(inner);

            var textContainer = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = SeasonCardTextHeight,
                BackgroundColor = Color.Transparent,
                Padding = new Extents(8, 8, 12, 0),
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Vertical
                }
            };

            var title = new TextLabel(season.Name ?? string.Empty)
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PointSize = 26,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word
            };

            textContainer.Add(title);
            wrapper.Add(frame);
            wrapper.Add(textContainer);
            return wrapper;
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
                case AppKey.Left:
                    MoveSeasonFocus(-1);
                    break;
                case AppKey.Right:
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
            if (_seasonViews.Count == 0) return;

            if (_seasonIndex >= 0 && _seasonIndex < _seasonViews.Count)
                ApplySeasonFocus(_seasonViews[_seasonIndex], false);

            _seasonIndex = Math.Clamp(index, 0, _seasonViews.Count - 1);
            ApplySeasonFocus(_seasonViews[_seasonIndex], true);
            ScrollSeasonsIfNeeded();

            Tizen.Applications.CoreApplication.Post(() =>
            {
                FocusManager.Instance.SetCurrentFocusView(_seasonViews[_seasonIndex]);
            });
        }

        private void ApplySeasonFocus(View card, bool focused)
        {
            var frame = GetCardFrame(card);
            var content = GetCardContent(card);

            if (content != null)
                AnimateCardScale(content, focused ? new Vector3(FocusScale, FocusScale, 1f) : Vector3.One);

            card.Scale = Vector3.One;
            card.PositionZ = focused ? 20 : 0;

            if (frame != null)
            {
                frame.CornerRadius = 16.0f;
                if (focused)
                {
                    frame.BackgroundColor = _focusBorderColor;
                    frame.BoxShadow = new Shadow(8.0f, new Color(0.0f, 0.64f, 0.86f, 0.25f), new Vector2(0, 0));
                }
                else
                {
                    frame.BackgroundColor = Color.Transparent;
                    frame.BoxShadow = null;
                }
            }
        }

        private void AnimateCardScale(View content, Vector3 targetScale)
        {
            if (content == null)
            {
                return;
            }

            if (_focusAnimations.TryGetValue(content, out var existing))
            {
                UiAnimator.StopAndDispose(ref existing);
                _focusAnimations.Remove(content);
            }

            var animation = UiAnimator.Start(
                UiAnimator.FocusDurationMs,
                anim => anim.AnimateTo(content, "Scale", targetScale),
                () => _focusAnimations.Remove(content)
            );

            _focusAnimations[content] = animation;
        }

        private void ScrollSeasonsIfNeeded()
        {
            if (_seasonRowContainer == null || _seasonViewport == null || _seasonViews.Count == 0)
                return;

            if (_seasonIndex == 0)
            {
                AnimateSeasonRowTo(0);
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

            AnimateSeasonRowTo(targetX);
        }

        private void AnimateSeasonRowTo(float targetX)
        {
            if (_seasonRowContainer == null)
            {
                return;
            }

            if (Math.Abs(targetX - _seasonRowContainer.PositionX) < 0.5f)
            {
                return;
            }

            UiAnimator.Replace(
                ref _seasonScrollAnimation,
                UiAnimator.AnimateTo(_seasonRowContainer, "PositionX", targetX, UiAnimator.ScrollDurationMs)
            );
        }

        private View GetCardFrame(View card)
        {
            foreach (var child in card.Children)
            {
                if (child.Name == "CardFrame")
                    return child;
            }
            return null;
        }

        private View GetCardContent(View card)
        {
            foreach (var child in card.Children)
            {
                if (child.Name == "CardFrame")
                {
                    foreach (var frameChild in child.Children)
                    {
                        if (frameChild.Name == "CardInner")
                        {
                            foreach (var innerChild in frameChild.Children)
                            {
                                if (innerChild.Name == "CardContent")
                                    return innerChild;
                            }
                        }
                    }
                }
            }
            return null;
        }

    }
}
