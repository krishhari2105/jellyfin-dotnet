using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tizen.Applications;
using Tizen.NUI;
using JellyfinTizen.Screens;
using JellyfinTizen.Utils;

namespace JellyfinTizen.Core
{
    public static class NavigationService
    {
        private enum TransitionProfile
        {
            Slide,
            CinematicPlayer
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
        private static readonly Vector3 CinematicIncomingStartScale = new(1.035f, 1.035f, 1f);
        private static readonly Vector3 CinematicIncomingBackStartScale = new(0.965f, 0.965f, 1f);
        private static readonly Vector3 CinematicOutgoingForwardEndScale = new(0.94f, 0.94f, 1f);
        private static readonly Vector3 CinematicOutgoingBackEndScale = new(1.04f, 1.04f, 1f);
        private const int CinematicTransitionDurationMs = 210;
        private const float CinematicForwardIncomingShiftFactor = 0.16f;
        private const float CinematicForwardOutgoingShiftFactor = 0.22f;
        private const float CinematicForwardOutgoingHeavyShiftFactor = 0.14f;
        private const float CinematicBackIncomingShiftFactor = 0.22f;
        private const float CinematicBackIncomingHeavyShiftFactor = 0.14f;
        private const float CinematicBackOutgoingShiftFactor = 0.18f;
        private const float CinematicBackOutgoingHeavyShiftFactor = 0.14f;

        private static Window _window;
        private static ScreenBase _currentScreen;
        private static readonly Stack<ScreenBase> _stack = new();
        private static Animation _screenTransitionAnimation;
        private static bool _isTransitioning;

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

            if (!(_currentScreen is IKeyHandler handler))
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
                Application.Current?.Exit();
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
                Application.Current?.Exit();
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
                Application.Current?.Exit();
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
            var profile = UseCinematicPlayerProfile(outgoing, incoming)
                ? TransitionProfile.CinematicPlayer
                : TransitionProfile.Slide;
            return new TransitionSpec(
                profile == TransitionProfile.CinematicPlayer ? CinematicTransitionDurationMs : UiAnimator.ScreenDurationMs,
                GetSlideDistance(),
                deferIncomingOnShow: incomingIsVideoPlayer,
                deferOutgoingOnHide: outgoingIsVideoPlayer,
                profile: profile);
        }

        private static bool ShouldAnimateTransition(ScreenBase outgoing, ScreenBase incoming, bool animated)
        {
            return animated && UseFullTransitionProfile(outgoing, incoming);
        }

        private static bool UseFullTransitionProfile(ScreenBase outgoing, ScreenBase incoming)
        {
            return UseCinematicPlayerProfile(outgoing, incoming);
        }

        private static bool UseCinematicPlayerProfile(ScreenBase outgoing, ScreenBase incoming)
        {
            return (outgoing is MovieDetailsScreen && incoming is VideoPlayerScreen) ||
                   (outgoing is VideoPlayerScreen && incoming is MovieDetailsScreen) ||
                   (outgoing is EpisodeDetailsScreen && incoming is VideoPlayerScreen) ||
                   (outgoing is VideoPlayerScreen && incoming is EpisodeDetailsScreen);
        }

        private static void PrepareIncomingForNavigate(ScreenBase incoming, float slide, TransitionProfile profile)
        {
            if (profile == TransitionProfile.CinematicPlayer)
            {
                incoming.PositionX = slide * CinematicForwardIncomingShiftFactor;
                incoming.Opacity = 0.0f;
                incoming.Scale = CinematicIncomingStartScale;
                return;
            }

            incoming.PositionX = slide;
            incoming.Opacity = 0.0f;
            incoming.Scale = UnitScale;
        }

        private static void PrepareIncomingForNavigateBack(ScreenBase incoming, float slide, TransitionProfile profile)
        {
            if (profile == TransitionProfile.CinematicPlayer)
            {
                bool incomingHasHeavyLogoTitle = HasHeavyLogoTitle(incoming);
                incoming.PositionX = -slide * (incomingHasHeavyLogoTitle
                    ? CinematicBackIncomingHeavyShiftFactor
                    : CinematicBackIncomingShiftFactor);
                incoming.Opacity = 0.0f;
                incoming.Scale = incomingHasHeavyLogoTitle ? UnitScale : CinematicIncomingBackStartScale;
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
            if (profile == TransitionProfile.CinematicPlayer)
            {
                bool outgoingHasHeavyLogoTitle = HasHeavyLogoTitle(outgoing);
                animation.AnimateTo(incoming, "PositionX", 0.0f);
                animation.AnimateTo(incoming, "Opacity", 1.0f);
                animation.AnimateTo(incoming, "Scale", UnitScale);
                animation.AnimateTo(outgoing, "PositionX", -slide * (outgoingHasHeavyLogoTitle
                    ? CinematicForwardOutgoingHeavyShiftFactor
                    : CinematicForwardOutgoingShiftFactor));
                animation.AnimateTo(outgoing, "Opacity", 0.0f);
                if (!outgoingHasHeavyLogoTitle)
                    animation.AnimateTo(outgoing, "Scale", CinematicOutgoingForwardEndScale);
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
            if (profile == TransitionProfile.CinematicPlayer)
            {
                bool incomingHasHeavyLogoTitle = HasHeavyLogoTitle(incoming);
                animation.AnimateTo(incoming, "PositionX", 0.0f);
                animation.AnimateTo(incoming, "Opacity", 1.0f);
                if (!incomingHasHeavyLogoTitle)
                    animation.AnimateTo(incoming, "Scale", UnitScale);
                animation.AnimateTo(outgoing, "PositionX", slide * (incomingHasHeavyLogoTitle
                    ? CinematicBackOutgoingHeavyShiftFactor
                    : CinematicBackOutgoingShiftFactor));
                animation.AnimateTo(outgoing, "Opacity", 0.0f);
                animation.AnimateTo(outgoing, "Scale", CinematicOutgoingBackEndScale);
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

        private static bool HasHeavyLogoTitle(ScreenBase screen)
        {
            return screen is MovieDetailsScreen movieDetails && movieDetails.UsesImageLogoTitle;
        }
    }
}
