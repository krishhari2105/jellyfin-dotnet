using System;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Utils;

namespace JellyfinTizen.UI
{
    public sealed class AppleTvLoadingVisual
    {
        private readonly View _root;
        private readonly View _panel;
        private readonly View _loaderSweep;
        private readonly View[] _loaderDots = new View[3];
        private readonly TextLabel _messageLabel;
        private Animation _sweepAnimation;
        private Animation _dotsAnimation;
        private Animation _panelAnimation;
        private bool _isAnimating;
        private int _dotIndex;
        private readonly int _trackTravel;

        private const int PanelMaxWidth = 760;
        private const int PanelMinWidth = 560;
        private const int PanelHeight = 280;
        private const int PanelHorizontalMargin = 220;
        private const int TrackHeight = 8;
        private const int SweepWidth = 150;
        private const int DotSize = 12;
        private const int DotGap = 14;
        private const float DotIdleOpacity = 0.46f;

        public AppleTvLoadingVisual(string message)
        {
            int screenWidth = Window.Default.Size.Width;
            int screenHeight = Window.Default.Size.Height;
            int panelWidth = Math.Min(PanelMaxWidth, Math.Max(PanelMinWidth, screenWidth - PanelHorizontalMargin));

            _root = UiFactory.CreateAtmosphericBackground();

            _panel = new View
            {
                WidthSpecification = panelWidth,
                HeightSpecification = PanelHeight,
                PositionX = (screenWidth - panelWidth) / 2,
                PositionY = (screenHeight - PanelHeight) / 2,
                BackgroundColor = MonochromeAuthFactory.PanelFallbackColor,
                CornerRadius = MonochromeAuthFactory.PanelCornerRadius,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BorderlineWidth = MonochromeAuthFactory.PanelBorderWidth,
                BorderlineColor = MonochromeAuthFactory.PanelFallbackBorder
            };

            var panelTopLine = new View
            {
                WidthSpecification = panelWidth - 70,
                HeightSpecification = 2,
                PositionX = 35,
                PositionY = 20,
                BackgroundColor = new Color(1f, 1f, 1f, 0.34f)
            };

            var panelBottomLine = new View
            {
                WidthSpecification = panelWidth - 70,
                HeightSpecification = 2,
                PositionX = 35,
                PositionY = PanelHeight - 22,
                BackgroundColor = new Color(1f, 1f, 1f, 0.24f)
            };

            _messageLabel = new TextLabel(string.IsNullOrWhiteSpace(message) ? "Loading..." : message)
            {
                WidthSpecification = panelWidth - 80,
                HeightSpecification = 74,
                PositionX = 40,
                PositionY = 66,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                PointSize = 28.0f,
                TextColor = new Color(1f, 1f, 1f, 0.94f),
                MultiLine = true,
                LineWrapMode = LineWrapMode.Word
            };

            var subtitle = new TextLabel("Please wait")
            {
                WidthSpecification = panelWidth - 120,
                HeightSpecification = 44,
                PositionX = 60,
                PositionY = 122,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                PointSize = 20.0f,
                TextColor = new Color(1f, 1f, 1f, 0.70f)
            };

            int trackWidth = panelWidth - 220;
            var loaderTrack = new View
            {
                WidthSpecification = trackWidth,
                HeightSpecification = TrackHeight,
                PositionX = (panelWidth - trackWidth) / 2,
                PositionY = 174,
                CornerRadius = TrackHeight / 2.0f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BackgroundColor = new Color(1f, 1f, 1f, 0.20f),
                ClippingMode = ClippingModeType.ClipChildren
            };

            _loaderSweep = new View
            {
                WidthSpecification = SweepWidth,
                HeightSpecification = TrackHeight,
                PositionX = -SweepWidth,
                PositionY = 0,
                CornerRadius = TrackHeight / 2.0f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BackgroundColor = Color.White,
                Opacity = 0.50f
            };
            loaderTrack.Add(_loaderSweep);
            _trackTravel = trackWidth + SweepWidth;

            int dotsRowWidth = (DotSize * _loaderDots.Length) + (DotGap * (_loaderDots.Length - 1));
            var dotsRow = new View
            {
                WidthSpecification = dotsRowWidth,
                HeightSpecification = DotSize,
                PositionX = (panelWidth - dotsRowWidth) / 2,
                PositionY = 202
            };

            for (int i = 0; i < _loaderDots.Length; i++)
            {
                var dot = new View
                {
                    WidthSpecification = DotSize,
                    HeightSpecification = DotSize,
                    PositionX = i * (DotSize + DotGap),
                    PositionY = 0,
                    CornerRadius = DotSize / 2.0f,
                    CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                    BackgroundColor = Color.White,
                    Opacity = DotIdleOpacity,
                    Scale = Vector3.One
                };
                _loaderDots[i] = dot;
                dotsRow.Add(dot);
            }

            _panel.Add(panelTopLine);
            _panel.Add(_messageLabel);
            _panel.Add(subtitle);
            _panel.Add(loaderTrack);
            _panel.Add(dotsRow);
            _panel.Add(panelBottomLine);

            _root.Add(_panel);
        }

        public View Root => _root;

        public void SetMessage(string message)
        {
            _messageLabel.Text = string.IsNullOrWhiteSpace(message) ? "Loading..." : message;
        }

        public void Start()
        {
            if (_isAnimating)
                return;

            _isAnimating = true;
            _dotIndex = 0;
            StartPanelBreathing(grow: true);
            StartLoaderSweep();
            StartDotPulse();
        }

        public void Stop()
        {
            _isAnimating = false;
            UiAnimator.StopAndDispose(ref _sweepAnimation);
            UiAnimator.StopAndDispose(ref _dotsAnimation);
            UiAnimator.StopAndDispose(ref _panelAnimation);

            _panel.Scale = Vector3.One;
            _loaderSweep.PositionX = -SweepWidth;
            _loaderSweep.Opacity = 0.50f;
            for (int i = 0; i < _loaderDots.Length; i++)
            {
                _loaderDots[i].Scale = Vector3.One;
                _loaderDots[i].Opacity = DotIdleOpacity;
            }
        }

        private void StartPanelBreathing(bool grow)
        {
            if (!_isAnimating)
                return;

            var targetScale = grow ? new Vector3(1.012f, 1.012f, 1f) : Vector3.One;
            UiAnimator.Replace(
                ref _panelAnimation,
                UiAnimator.Start(
                    grow ? 900 : 980,
                    animation => { animation.AnimateTo(_panel, "Scale", targetScale); },
                    () =>
                    {
                        if (_isAnimating)
                            StartPanelBreathing(!grow);
                    }
                )
            );
        }

        private void StartLoaderSweep()
        {
            if (!_isAnimating)
                return;

            _loaderSweep.PositionX = -SweepWidth;
            _loaderSweep.Opacity = 0.50f;
            UiAnimator.Replace(
                ref _sweepAnimation,
                UiAnimator.Start(
                    960,
                    animation =>
                    {
                        animation.AnimateTo(_loaderSweep, "PositionX", _trackTravel);
                        animation.AnimateTo(_loaderSweep, "Opacity", 0.94f);
                    },
                    () =>
                    {
                        _loaderSweep.Opacity = 0.50f;
                        if (_isAnimating)
                            StartLoaderSweep();
                    }
                )
            );
        }

        private void StartDotPulse()
        {
            if (!_isAnimating || _loaderDots.Length == 0)
                return;

            int activeIndex = _dotIndex % _loaderDots.Length;
            var activeDot = _loaderDots[activeIndex];

            UiAnimator.Replace(
                ref _dotsAnimation,
                UiAnimator.Start(
                    180,
                    animation =>
                    {
                        animation.AnimateTo(activeDot, "Scale", new Vector3(1.30f, 1.30f, 1f));
                        animation.AnimateTo(activeDot, "Opacity", 1.0f);
                    },
                    () =>
                    {
                        UiAnimator.Replace(
                            ref _dotsAnimation,
                            UiAnimator.Start(
                                240,
                                animation =>
                                {
                                    animation.AnimateTo(activeDot, "Scale", Vector3.One);
                                    animation.AnimateTo(activeDot, "Opacity", DotIdleOpacity);
                                },
                                () =>
                                {
                                    _dotIndex++;
                                    if (_isAnimating)
                                        StartDotPulse();
                                }
                            )
                        );
                    }
                )
            );
        }
    }
}
