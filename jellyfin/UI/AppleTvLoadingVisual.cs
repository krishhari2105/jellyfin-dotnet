using System;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Utils;

namespace JellyfinTizen.UI
{
    /// <summary>
    /// Lightweight rotating-ring loader matching litefin's "simple loader":
    /// 48px circle, 3px track at 10% white, accent-colored quarter arc,
    /// 1s linear infinite rotation.
    /// </summary>
    public sealed class AppleTvLoadingVisual : IDisposable
    {
        private readonly View _root;
        private readonly View _spinnerContainer;
        private readonly View _bgRing;
        private readonly View _clippingContainer;
        private readonly View _accentRing;
        private Animation _rotationAnimation;
        private bool _isAnimating;

        private const int SpinnerSize = 48;
        private const float RingThickness = 3f;
        private const int SpinDurationMs = 1000;

        public AppleTvLoadingVisual(string message)
        {
            int screenWidth = Window.Default.Size.Width;
            int screenHeight = Window.Default.Size.Height;

            _root = UiFactory.CreateAtmosphericBackground();

            // Rotation happens on this container; pivot MUST be center,
            // otherwise NUI rotates around the top-left corner.
            _spinnerContainer = new View
            {
                WidthSpecification = SpinnerSize,
                HeightSpecification = SpinnerSize,
                ParentOrigin = ParentOrigin.TopLeft,
                PivotPoint = Tizen.NUI.PivotPoint.Center,
                PositionUsesPivotPoint = true,
                PositionX = screenWidth / 2f,
                PositionY = screenHeight / 2f,
            };

            // Faint background track: 3px ring, white @ 10% (litefin simple-loader).
            // Relative 0.5 corner radius guarantees a true circle regardless of size.
            _bgRing = new View
            {
                WidthSpecification = SpinnerSize,
                HeightSpecification = SpinnerSize,
                PositionX = 0,
                PositionY = 0,
                CornerRadius = 0.5f,
                CornerRadiusPolicy = VisualTransformPolicyType.Relative,
                BackgroundColor = Color.Transparent,
                BorderlineWidth = RingThickness,
                BorderlineColor = new Color(1f, 1f, 1f, 0.1f)
            };

            // Top-right quadrant clip → quarter arc, equivalent to CSS border-top-color.
            _clippingContainer = new View
            {
                WidthSpecification = SpinnerSize / 2,
                HeightSpecification = SpinnerSize / 2,
                PositionX = SpinnerSize / 2,
                PositionY = 0,
                BackgroundColor = Color.Transparent,
                ClippingMode = ClippingModeType.ClipChildren
            };

            // Accent arc: full ring, clipped to the quadrant above.
            _accentRing = new View
            {
                WidthSpecification = SpinnerSize,
                HeightSpecification = SpinnerSize,
                PositionX = -SpinnerSize / 2,
                PositionY = 0,
                CornerRadius = 0.5f,
                CornerRadiusPolicy = VisualTransformPolicyType.Relative,
                BackgroundColor = Color.Transparent,
                BorderlineWidth = RingThickness,
                BorderlineColor = Color.White
            };

            _clippingContainer.Add(_accentRing);
            _spinnerContainer.Add(_bgRing);
            _spinnerContainer.Add(_clippingContainer);
            _root.Add(_spinnerContainer);
        }

        public View Root => _root;

        public void SetMessage(string message)
        {
            // No-op: message text is removed in this simplified layout
        }

        public void Start()
        {
            if (_isAnimating)
                return;

            _isAnimating = true;

            // NUI orientations are quaternions: AnimateTo 360° == identity == no motion.
            // Spin via two relative 180° AnimateBy segments (180° is quaternion-unambiguous),
            // looped, linear — matches CSS "rotation 1s linear infinite".
            var halfTurn = new Rotation(new Radian(new Degree(180f)), Vector3.ZAxis);
            _rotationAnimation = UiAnimator.Start(
                SpinDurationMs,
                animation =>
                {
                    animation.Looping = true;
                    animation.DefaultAlphaFunction = new AlphaFunction(AlphaFunction.BuiltinFunctions.Linear);
                    animation.AnimateBy(_spinnerContainer, "Orientation", halfTurn, 0, SpinDurationMs / 2);
                    animation.AnimateBy(_spinnerContainer, "Orientation", halfTurn, SpinDurationMs / 2, SpinDurationMs);
                }
            );
        }

        public void Stop()
        {
            _isAnimating = false;
            UiAnimator.StopAndDispose(ref _rotationAnimation);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
