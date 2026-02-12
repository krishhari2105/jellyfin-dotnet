using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Core;
using JellyfinTizen.Utils;

namespace JellyfinTizen.Screens
{
    public class LoadingScreen : ScreenBase, IKeyHandler
    {
        private readonly View _spinnerRing;
        private readonly View _centerContainer;
        private Animation _spinnerAnimation;

        public LoadingScreen(string message)
        {
            int screenWidth = Window.Default.Size.Width;
            int screenHeight = Window.Default.Size.Height;
            _centerContainer = new View
            {
                WidthSpecification = 520,
                HeightSpecification = 240,
                PositionX = (screenWidth - 520) / 2,
                PositionY = (screenHeight - 240) / 2
            };

            _spinnerRing = new View
            {
                WidthSpecification = 92,
                HeightSpecification = 92,
                PositionX = (520 - 92) / 2,
                PositionY = 24,
                CornerRadius = 46.0f,
                BackgroundColor = Color.Transparent,
                BorderlineWidth = 4.0f,
                BorderlineColor = new Color(1f, 1f, 1f, 0.78f),
                Opacity = 0.42f,
                Scale = Vector3.One
            };

            var label = new TextLabel(message)
            {
                WidthSpecification = 520,
                HeightSpecification = 72,
                PositionY = 136,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                PointSize = 28,
                TextColor = new Color(1f, 1f, 1f, 0.9f)
            };

            _centerContainer.Add(_spinnerRing);
            _centerContainer.Add(label);
            Add(_centerContainer);
        }

        public override void OnShow()
        {
            StartSpinnerPulse();
        }

        public override void OnHide()
        {
            UiAnimator.StopAndDispose(ref _spinnerAnimation);
            _spinnerRing.Scale = Vector3.One;
            _spinnerRing.Opacity = 0.42f;
        }

        private void StartSpinnerPulse()
        {
            UiAnimator.Replace(
                ref _spinnerAnimation,
                UiAnimator.Start(
                    360,
                    animation =>
                    {
                        animation.AnimateTo(_spinnerRing, "Scale", new Vector3(1.1f, 1.1f, 1f));
                        animation.AnimateTo(_spinnerRing, "Opacity", 1.0f);
                    },
                    () =>
                    {
                        UiAnimator.Replace(
                            ref _spinnerAnimation,
                            UiAnimator.Start(
                                380,
                                animation =>
                                {
                                    animation.AnimateTo(_spinnerRing, "Scale", Vector3.One);
                                    animation.AnimateTo(_spinnerRing, "Opacity", 0.42f);
                                },
                                StartSpinnerPulse
                            )
                        );
                    }
                )
            );
        }

        public void HandleKey(AppKey key)
        {
            if (key == AppKey.Back)
            {
                NavigationService.NavigateBack();
            }
        }
    }
}
