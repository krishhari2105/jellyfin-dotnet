using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tizen.Applications;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Screens;
using JellyfinTizen.UI;
using JellyfinTizen.Utils;

namespace JellyfinTizen.Core
{
    public static class NavigationService
    {
        private enum TransitionProfile
        {
            Slide,
            BlackScreen
        }

        private readonly struct TransitionSpec
        {
            public TransitionSpec(
                int durationMs,
                float slideDistance,
                bool deferIncomingOnShow,
                bool deferOutgoingOnHide,
                TransitionProfile profile)
            {
                DurationMs = durationMs;
                SlideDistance = slideDistance;
                DeferIncomingOnShow = deferIncomingOnShow;
                DeferOutgoingOnHide = deferOutgoingOnHide;
                Profile = profile;
            }

            public int DurationMs { get; }
            public float SlideDistance { get; }
            public bool DeferIncomingOnShow { get; }
            public bool DeferOutgoingOnHide { get; }
            public TransitionProfile Profile { get; }
        }

        private static readonly Vector3 UnitScale = new(1f, 1f, 1f);
        private const int BlackScreenTransitionDurationMs = 180;
        private const int ExitPopupWidth = 660;
        private const int ExitPopupButtonHeight = 64;

        private static Window _window;
        private static ScreenBase _currentScreen;
        private static readonly Stack<ScreenBase> _stack = new();
        private static Animation _screenTransitionAnimation;
        private static bool _isTransitioning;
        private static View _exitConfirmationOverlay;
        private static View _exitConfirmationPanel;
        private static View _exitStayButton;
        private static View _exitConfirmButton;
        private static readonly List<View> _exitConfirmationButtons = new();
        private static int _exitConfirmationFocusIndex;
        private static bool _exitConfirmationVisible;

        public static void Init(Window window)
        {
            _window = window;
            // standard NUI event - this works!
            _window.KeyEvent += OnKeyEvent; 

            // Remove the default blue focus border globally
            FocusManager.Instance.FocusIndicator = null;
        }

        private static void OnKeyEvent(object sender, Window.KeyEventArgs e)
        {
            if (e.Key.State != Key.StateType.Down)
                return;

            if (_isTransitioning)
                return;

            // 2. Map the Key
            var key = e.Key.KeyPressedName switch
            {
                // Standard Nav
                "Up" => AppKey.Up,
                "Down" => AppKey.Down,
                "Left" => AppKey.Left,
                "Right" => AppKey.Right,
                "Return" => AppKey.Enter,
                "Enter" => AppKey.Enter,
                "Select" => AppKey.Enter,
                "Back" => AppKey.Back,
                "BackSpace" => AppKey.Back,
                "Escape" => AppKey.Back,
                "XF86Back" => AppKey.Back,
                "XF86Exit" => AppKey.Back,

                // --- MEDIA KEYS (These WILL trigger actions despite the toast) ---
                "XF86PlayBack" => AppKey.MediaPlayPause, 
                "MediaPlayPause" => AppKey.MediaPlayPause,
                "XF86AudioPlay" => AppKey.MediaPlay,
                "MediaPlay" => AppKey.MediaPlay,
                "XF86AudioPause" => AppKey.MediaPause,
                "MediaPause" => AppKey.MediaPause,
                "XF86AudioStop" => AppKey.MediaStop,
                "MediaStop" => AppKey.MediaStop,
                "XF86AudioNext" => AppKey.MediaNext,
                "MediaNext" => AppKey.MediaNext,
                "XF86AudioPrev" => AppKey.MediaPrevious,
                "MediaPrevious" => AppKey.MediaPrevious,
                "XF86AudioRewind" => AppKey.MediaRewind,
                "MediaRewind" => AppKey.MediaRewind,
                "XF86AudioForward" => AppKey.MediaFastForward,
                "MediaFastForward" => AppKey.MediaFastForward,
                // -------------------------------------------------------------

                _ => AppKey.Unknown
            };

            if (HandleExitConfirmationKey(key))
                return;

            if (!(_currentScreen is IKeyHandler handler))
                return;

            // 3. Execute Logic
            handler.HandleKey(key);
        }

        public static void Navigate(ScreenBase screen, bool addToStack = true, bool animated = true)
        {
            if (_window == null || screen == null)
                return;

            if (_currentScreen == null || _isTransitioning)
            {
                NavigateImmediate(screen, addToStack);
                return;
            }

            var outgoing = _currentScreen;
            var incoming = screen;
            if (!ShouldAnimateTransition(outgoing, incoming, animated))
            {
                NavigateImmediate(screen, addToStack);
                return;
            }

            var transition = GetTransitionSpec(outgoing, incoming);
            var slide = transition.SlideDistance;

            _isTransitioning = true;

            if (!transition.DeferOutgoingOnHide)
                outgoing.OnHide();
            if (addToStack) _stack.Push(outgoing);

            PrepareIncomingForNavigate(incoming, slide, transition.Profile);
            _window.Add(incoming);
            _currentScreen = incoming;
            if (!transition.DeferIncomingOnShow)
                incoming.OnShow();

            UiAnimator.Replace(
                ref _screenTransitionAnimation,
                UiAnimator.Start(
                    transition.DurationMs,
                    animation =>
                    {
                        ConfigureNavigateAnimation(animation, incoming, outgoing, slide, transition.Profile);
                    },
                    () =>
                    {
                        ResetScreenTransform(incoming);
                        if (transition.DeferOutgoingOnHide)
                        {
                            try { outgoing.OnHide(); } catch { }
                        }
                        CleanupOutgoingAfterNavigate(outgoing, addToStack);
                        if (transition.DeferIncomingOnShow)
                        {
                            try { incoming.OnShow(); } catch { }
                        }
                        _isTransitioning = false;
                        _screenTransitionAnimation = null;
                    }
                )
            );
        }

        public static async void NavigateWithLoading(
            Func<ScreenBase> screenFactory,
            string message = "Loading...",
            bool addToStack = true,
            bool animated = true,
            int minDisplayMs = 220)
        {
            if (_window == null || screenFactory == null)
                return;

            if (_currentScreen is LoadingScreen)
            {
                var immediate = screenFactory();
                Navigate(immediate, addToStack, animated);
                return;
            }

            var loadingScreen = new LoadingScreen(message);
            var shownAt = DateTime.UtcNow;
            Navigate(loadingScreen, addToStack: addToStack, animated: false);

            await Task.Yield();

            ScreenBase target;
            try
            {
                target = screenFactory();
            }
            catch
            {
                if (ReferenceEquals(_currentScreen, loadingScreen))
                    NavigateBack(animated: false);
                return;
            }

            var elapsedMs = (DateTime.UtcNow - shownAt).TotalMilliseconds;
            if (elapsedMs < minDisplayMs)
            {
                await Task.Delay((int)(minDisplayMs - elapsedMs));
            }

            if (!ReferenceEquals(_currentScreen, loadingScreen))
                return;

            Navigate(target, addToStack: false, animated: animated);
        }

        public static void NavigateBack(bool animated = true)
        {
            if (_stack.Count == 0)
            {
                ShowExitConfirmation();
                return;
            }

            if (_currentScreen == null || _isTransitioning)
            {
                NavigateBackImmediate();
                return;
            }

            var incomingCandidate = _stack.Peek();
            if (!ShouldAnimateTransition(_currentScreen, incomingCandidate, animated))
            {
                NavigateBackImmediate();
                return;
            }

            var outgoing = _currentScreen;
            var incoming = _stack.Pop();
            var transition = GetTransitionSpec(outgoing, incoming);
            var slide = transition.SlideDistance;

            if (incoming == null)
            {
                ShowExitConfirmation();
                return;
            }

            _isTransitioning = true;

            if (!transition.DeferOutgoingOnHide)
                outgoing.OnHide();

            PrepareIncomingForNavigateBack(incoming, slide, transition.Profile);
            _window.Add(incoming);
            _currentScreen = incoming;
            if (!transition.DeferIncomingOnShow)
                incoming.OnShow();

            UiAnimator.Replace(
                ref _screenTransitionAnimation,
                UiAnimator.Start(
                    transition.DurationMs,
                    animation =>
                    {
                        ConfigureNavigateBackAnimation(animation, incoming, outgoing, slide, transition.Profile);
                    },
                    () =>
                    {
                        ResetScreenTransform(incoming);
                        if (transition.DeferOutgoingOnHide)
                        {
                            try { outgoing.OnHide(); } catch { }
                        }
                        try { _window.Remove(outgoing); } catch { }
                        try { outgoing.Dispose(); } catch { }
                        if (transition.DeferIncomingOnShow)
                        {
                            try { incoming.OnShow(); } catch { }
                        }
                        _isTransitioning = false;
                        _screenTransitionAnimation = null;
                    }
                )
            );
        }

        public static void ClearStack()
        {
            while (_stack.Count > 0)
            {
                var screen = _stack.Pop();
                try { screen?.OnHide(); _window?.Remove(screen); screen?.Dispose(); } catch { }
            }
        }

        public static void HandleBack()
        {
            if (_currentScreen is IKeyHandler handler) handler.HandleKey(AppKey.Back);
        }

        public static void NotifyAppTerminating()
        {
            try
            {
                _currentScreen?.OnHide();
            }
            catch { }
        }

        private static void NavigateImmediate(ScreenBase screen, bool addToStack)
        {
            if (_currentScreen != null)
            {
                _currentScreen.OnHide();
                _window.Remove(_currentScreen);
                if (addToStack)
                {
                    ResetScreenTransform(_currentScreen);
                    _stack.Push(_currentScreen);
                }
                else _currentScreen.Dispose();
            }

            _currentScreen = screen;
            ResetScreenTransform(_currentScreen);
            _window.Add(_currentScreen);
            _currentScreen.OnShow();
        }

        private static void NavigateBackImmediate()
        {
            if (_stack.Count == 0)
            {
                ShowExitConfirmation();
                return;
            }

            _currentScreen.OnHide();
            _window.Remove(_currentScreen);
            _currentScreen.Dispose();
            _currentScreen = _stack.Pop();
            if (_currentScreen == null) return;
            ResetScreenTransform(_currentScreen);
            _window.Add(_currentScreen);
            _currentScreen.OnShow();
        }

        private static void EnsureExitConfirmationPopupCreated()
        {
            if (_window == null || _exitConfirmationOverlay != null)
                return;

            _exitConfirmationOverlay = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                BackgroundColor = Color.Transparent
            };

            _exitConfirmationPanel = MonochromeAuthFactory.CreatePanel(width: ExitPopupWidth, yOffset: 0);
            _exitConfirmationPanel.Padding = new Extents(42, 42, 34, 34);
            _exitConfirmationPanel.BackgroundColor = new Color(7f / 255f, 13f / 255f, 28f / 255f, 1.0f);
            _exitConfirmationPanel.BorderlineColor = new Color(1f, 1f, 1f, 0.28f);
            if (_exitConfirmationPanel.Layout is LinearLayout panelLayout)
                panelLayout.CellPadding = new Size2D(0, 14);

            var title = MonochromeAuthFactory.CreateTitle("Exit Jellyfin?");
            title.PointSize = 36f;
            var subtitle = MonochromeAuthFactory.CreateSubtitle("Do you want to close the app?");
            subtitle.PointSize = 21f;
            subtitle.TextColor = new Color(1f, 1f, 1f, 0.72f);

            var buttonRow = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    CellPadding = new Size2D(14, 0)
                }
            };

            _exitStayButton = CreateExitConfirmationButton("Stay", primary: false);
            _exitConfirmButton = CreateExitConfirmationButton("Exit", primary: true);
            _exitConfirmationButtons.Clear();
            _exitConfirmationButtons.Add(_exitStayButton);
            _exitConfirmationButtons.Add(_exitConfirmButton);

            buttonRow.Add(_exitStayButton);
            buttonRow.Add(_exitConfirmButton);
            _exitConfirmationPanel.Add(title);
            _exitConfirmationPanel.Add(subtitle);
            _exitConfirmationPanel.Add(buttonRow);
            _exitConfirmationOverlay.Add(_exitConfirmationPanel);
            _exitConfirmationOverlay.Hide();
            _window.Add(_exitConfirmationOverlay);
        }

        private static View CreateExitConfirmationButton(string text, bool primary)
        {
            var button = new View
            {
                WidthResizePolicy = ResizePolicyType.FitToChildren,
                HeightSpecification = ExitPopupButtonHeight,
                Padding = new Extents(30, 30, 8, 8),
                Focusable = true,
                CornerRadius = ExitPopupButtonHeight / 2.0f,
                CornerRadiusPolicy = VisualTransformPolicyType.Absolute,
                BorderlineWidth = 2.0f,
                ClippingMode = ClippingModeType.ClipChildren,
                Layout = new LinearLayout
                {
                    LinearOrientation = LinearLayout.Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            var label = new TextLabel(text)
            {
                WidthResizePolicy = ResizePolicyType.FitToChildren,
                HeightResizePolicy = ResizePolicyType.FitToChildren,
                PointSize = 25f,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            button.Add(label);
            MonochromeAuthFactory.SetButtonFocusState(button, primary, focused: false);
            return button;
        }

        private static void ShowExitConfirmation()
        {
            if (_window == null)
            {
                Application.Current?.Exit();
                return;
            }

            EnsureExitConfirmationPopupCreated();
            if (_exitConfirmationOverlay == null)
            {
                Application.Current?.Exit();
                return;
            }

            _exitConfirmationVisible = true;
            try { _window.Remove(_exitConfirmationOverlay); } catch { }
            try { _window.Add(_exitConfirmationOverlay); } catch { }
            SetExitConfirmationFocus(0);
            _exitConfirmationOverlay.Show();
            _exitConfirmationOverlay.RaiseToTop();
            _exitConfirmationPanel?.RaiseToTop();
        }

        private static void HideExitConfirmation()
        {
            _exitConfirmationVisible = false;
            _exitConfirmationOverlay?.Hide();
        }

        private static bool HandleExitConfirmationKey(AppKey key)
        {
            if (!_exitConfirmationVisible)
                return false;

            switch (key)
            {
                case AppKey.Left:
                case AppKey.Up:
                    MoveExitConfirmationFocus(-1);
                    break;
                case AppKey.Right:
                case AppKey.Down:
                    MoveExitConfirmationFocus(1);
                    break;
                case AppKey.Enter:
                    ActivateExitConfirmationSelection();
                    break;
                case AppKey.Back:
                    HideExitConfirmation();
                    break;
            }

            return true;
        }

        private static void MoveExitConfirmationFocus(int delta)
        {
            if (_exitConfirmationButtons.Count == 0)
                return;

            SetExitConfirmationFocus(Math.Clamp(_exitConfirmationFocusIndex + delta, 0, _exitConfirmationButtons.Count - 1));
        }

        private static void SetExitConfirmationFocus(int index)
        {
            if (_exitConfirmationButtons.Count == 0)
                return;

            _exitConfirmationFocusIndex = Math.Clamp(index, 0, _exitConfirmationButtons.Count - 1);
            for (int i = 0; i < _exitConfirmationButtons.Count; i++)
            {
                bool focused = i == _exitConfirmationFocusIndex;
                bool primary = ReferenceEquals(_exitConfirmationButtons[i], _exitConfirmButton);
                MonochromeAuthFactory.SetButtonFocusState(_exitConfirmationButtons[i], primary, focused);
                _exitConfirmationButtons[i].Scale = Vector3.One;
            }

            FocusManager.Instance.SetCurrentFocusView(_exitConfirmationButtons[_exitConfirmationFocusIndex]);
        }

        private static void ActivateExitConfirmationSelection()
        {
            if (ReferenceEquals(_exitConfirmationButtons[_exitConfirmationFocusIndex], _exitConfirmButton))
            {
                try
                {
                    _currentScreen?.OnHide();
                }
                catch
                {
                }

                Application.Current?.Exit();
                return;
            }

            HideExitConfirmation();
        }

        private static void CleanupOutgoingAfterNavigate(ScreenBase outgoing, bool addToStack)
        {
            try { _window.Remove(outgoing); } catch { }

            if (addToStack)
            {
                try
                {
                    outgoing.PositionX = 0.0f;
                    outgoing.PositionY = 0.0f;
                    outgoing.Opacity = 1.0f;
                    outgoing.Scale = UnitScale;
                }
                catch { }
            }
            else
            {
                try { outgoing.Dispose(); } catch { }
            }
        }

        private static float GetSlideDistance()
        {
            if (_window == null)
                return UiAnimator.ScreenSlideDistance;

            return Math.Max(UiAnimator.ScreenSlideDistance, _window.Size.Width * 0.08f);
        }

        private static TransitionSpec GetTransitionSpec(ScreenBase outgoing, ScreenBase incoming)
        {
            bool outgoingIsVideoPlayer = outgoing is VideoPlayerScreen;
            bool incomingIsVideoPlayer = incoming is VideoPlayerScreen;
            var profile = UseBlackScreenProfile(outgoing, incoming)
                ? TransitionProfile.BlackScreen
                : TransitionProfile.Slide;
            return new TransitionSpec(
                profile == TransitionProfile.BlackScreen ? BlackScreenTransitionDurationMs : UiAnimator.ScreenDurationMs,
                GetSlideDistance(),
                deferIncomingOnShow: incomingIsVideoPlayer,
                deferOutgoingOnHide: outgoingIsVideoPlayer,
                profile: profile);
        }

        private static bool ShouldAnimateTransition(ScreenBase outgoing, ScreenBase incoming, bool animated)
        {
            if (!animated)
                return false;

            // On lower-end TVs, even lightweight fades around player attach can hitch.
            // Prefer an instant swap for details <-> player navigation.
            if (UseBlackScreenProfile(outgoing, incoming))
                return false;

            return UseFullTransitionProfile(outgoing, incoming);
        }

        private static bool UseFullTransitionProfile(ScreenBase outgoing, ScreenBase incoming)
        {
            return UseBlackScreenProfile(outgoing, incoming);
        }

        private static bool UseBlackScreenProfile(ScreenBase outgoing, ScreenBase incoming)
        {
            return (outgoing is MovieDetailsScreen && incoming is VideoPlayerScreen) ||
                   (outgoing is VideoPlayerScreen && incoming is MovieDetailsScreen) ||
                   (outgoing is EpisodeDetailsScreen && incoming is VideoPlayerScreen) ||
                   (outgoing is VideoPlayerScreen && incoming is EpisodeDetailsScreen);
        }

        private static void PrepareIncomingForNavigate(ScreenBase incoming, float slide, TransitionProfile profile)
        {
            if (profile == TransitionProfile.BlackScreen)
            {
                incoming.PositionX = 0.0f;
                incoming.Opacity = 0.0f;
                incoming.Scale = UnitScale;
                return;
            }

            incoming.PositionX = slide;
            incoming.Opacity = 0.0f;
            incoming.Scale = UnitScale;
        }

        private static void PrepareIncomingForNavigateBack(ScreenBase incoming, float slide, TransitionProfile profile)
        {
            if (profile == TransitionProfile.BlackScreen)
            {
                incoming.PositionX = 0.0f;
                incoming.Opacity = 0.0f;
                incoming.Scale = UnitScale;
                return;
            }

            incoming.PositionX = -slide * 0.6f;
            incoming.Opacity = 0.0f;
            incoming.Scale = UnitScale;
        }

        private static void ConfigureNavigateAnimation(
            Animation animation,
            ScreenBase incoming,
            ScreenBase outgoing,
            float slide,
            TransitionProfile profile)
        {
            if (profile == TransitionProfile.BlackScreen)
            {
                // For details -> player, fade in the incoming black screen instead of
                // fading out the heavy details tree to keep the transition smooth.
                if (incoming is VideoPlayerScreen)
                {
                    animation.AnimateTo(incoming, "Opacity", 1.0f);
                }
                else
                {
                    animation.AnimateTo(outgoing, "Opacity", 0.0f);
                }
                return;
            }

            animation.AnimateTo(incoming, "PositionX", 0.0f);
            animation.AnimateTo(incoming, "Opacity", 1.0f);
            animation.AnimateTo(outgoing, "PositionX", -slide * 0.6f);
            animation.AnimateTo(outgoing, "Opacity", 0.0f);
        }

        private static void ConfigureNavigateBackAnimation(
            Animation animation,
            ScreenBase incoming,
            ScreenBase outgoing,
            float slide,
            TransitionProfile profile)
        {
            if (profile == TransitionProfile.BlackScreen)
            {
                if (incoming is VideoPlayerScreen)
                {
                    animation.AnimateTo(incoming, "Opacity", 1.0f);
                }
                else
                {
                    animation.AnimateTo(outgoing, "Opacity", 0.0f);
                }
                return;
            }

            animation.AnimateTo(incoming, "PositionX", 0.0f);
            animation.AnimateTo(incoming, "Opacity", 1.0f);
            animation.AnimateTo(outgoing, "PositionX", slide);
            animation.AnimateTo(outgoing, "Opacity", 0.0f);
        }

        private static void ResetScreenTransform(ScreenBase screen)
        {
            if (screen == null)
                return;

            try
            {
                screen.PositionX = 0.0f;
                screen.PositionY = 0.0f;
                screen.Opacity = 1.0f;
                screen.Scale = UnitScale;
            }
            catch { }
        }

    }
}
