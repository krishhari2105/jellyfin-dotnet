using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Tizen.Applications;
using Tizen.Common;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using NColor = Tizen.NUI.Color;
using JellyfinTizen.Screens;
using JellyfinTizen.UI;
using JellyfinTizen.Utils;

namespace JellyfinTizen.Core
{
    public static class NavigationService
    {
        // P/Invoke signatures for native key grabbing via EFL extension
        [DllImport("libefl-extension.so.0", EntryPoint = "eext_win_keygrab_set")]
        private static extern bool eext_win_keygrab_set(IntPtr window, string key);

        [DllImport("libefl-extension.so.0", EntryPoint = "eext_win_keygrab_unset")]
        private static extern bool eext_win_keygrab_unset(IntPtr window, string key);


        public static readonly Dictionary<string, bool> GrabbedKeys = new();
        private static readonly List<string> _keyDebugLogs = new();

        public static void DebugLog(string message)
        {
            if (!DebugSwitches.EnableVerboseDebugLogging)
                return;

            string msg = $"{DateTime.Now:HH:mm:ss} | {message}";
            Tizen.Log.Info("Jellyfin", msg);
            lock (_keyDebugLogs)
            {
                _keyDebugLogs.Add(msg);
                if (_keyDebugLogs.Count > 40)
                    _keyDebugLogs.RemoveAt(0);
            }
        }

        public static IReadOnlyList<string> GetDebugLogs()
        {
            lock (_keyDebugLogs)
            {
                return _keyDebugLogs.ToArray();
            }
        }

        private const int ExitPopupWidth = 660;
        private const int ExitPopupButtonHeight = 64;

        private static Window _window;
        private static ScreenBase _currentScreen;
        private static readonly Stack<ScreenBase> _stack = new();
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
            // Intercept media key events to suppress "Not Available" system toast
            _window.InterceptKeyEvent += OnInterceptKeyEvent;

            // Remove the default blue focus border globally
            FocusManager.Instance.FocusIndicator = null;
        }

        private static void OnKeyEvent(object sender, Window.KeyEventArgs e)
        {
            if (e.Key.State != Key.StateType.Down)
                return;

            string keyName = e.Key.KeyPressedName;

            // Skip media keys here because they are handled in InterceptKeyEvent
            bool isMediaOrGrabbedKey = keyName switch
            {
                "XF86PlayBack" or "MediaPlayPause" or
                "XF86AudioPlay" or "MediaPlay" or
                "XF86AudioPause" or "MediaPause" or
                "XF86AudioStop" or "MediaStop" or
                "XF86AudioNext" or "MediaNext" or
                "XF86AudioPrev" or "MediaPrevious" or
                "XF86AudioRewind" or "MediaRewind" or
                "XF86AudioForward" or "MediaFastForward" or
                "XF86Red" or "ColorF0Red" => true,
                _ => false
            };

            int keyDec = e.Key.KeyCode;
            DebugLog($"OnKeyEvent received: {keyName}, KeyCode: {keyDec} (isMedia={isMediaOrGrabbedKey})");

            if (isMediaOrGrabbedKey)
                return;

            // 2. Map the standard navigation Key
            var key = keyName switch
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
                _ => AppKey.Unknown
            };

            if (key == AppKey.Unknown)
                return;

            if (HandleExitConfirmationKey(key))
                return;

            if (!(_currentScreen is IKeyHandler handler))
                return;

            // 3. Execute Logic
            DebugLog($"Standard key pressed: {keyName}");
            handler.HandleKey(key);
        }

        private static bool OnInterceptKeyEvent(object source, Window.KeyEventArgs e)
        {
            if (e.Key.State != Key.StateType.Down)
                return false;

            string keyName = e.Key.KeyPressedName;
            int keyDec = e.Key.KeyCode;
            DebugLog($"OnInterceptKeyEvent received: {keyName}, KeyCode: {keyDec}");

            // Check if it is a media key or red key that we grabbed
            bool isMediaOrGrabbedKey = keyName switch
            {
                "XF86PlayBack" or "MediaPlayPause" or
                "XF86AudioPlay" or "MediaPlay" or
                "XF86AudioPause" or "MediaPause" or
                "XF86AudioStop" or "MediaStop" or
                "XF86AudioNext" or "MediaNext" or
                "XF86AudioPrev" or "MediaPrevious" or
                "XF86AudioRewind" or "MediaRewind" or
                "XF86AudioForward" or "MediaFastForward" or
                "XF86Red" or "ColorF0Red" => true,
                _ => false
            };

            if (!isMediaOrGrabbedKey)
                return false; // Let standard keys propagate to KeyEvent / standard handlers

            // Map the media key
            var key = keyName switch
            {
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
                "XF86Red" => AppKey.Red,
                "ColorF0Red" => AppKey.Red,
                _ => AppKey.Unknown
            };

            DebugLog($"OnInterceptKeyEvent mapped {keyName} -> {key}, currentScreen: {(_currentScreen == null ? "null" : _currentScreen.GetType().Name)}");

            if (key == AppKey.Unknown)
                return false;

            bool isExitActive = HandleExitConfirmationKey(key);
            DebugLog($"HandleExitConfirmationKey returned: {isExitActive}");
            if (isExitActive)
                return true;

            if (_currentScreen is IKeyHandler handler)
            {
                try
                {
                    handler.HandleKey(key);
                    DebugLog($"Media key {key} handled by screen, allowing system toast (returning false)");
                }
                catch (Exception ex)
                {
                    DebugLog($"ERROR inside handler.HandleKey for {key}: {ex.Message}");
                }
            }
            else
            {
                DebugLog($"currentScreen is NOT IKeyHandler!");
            }

            return false;
        }



        public static void Navigate(ScreenBase screen, bool addToStack = true)
        {
            if (_window == null || screen == null)
                return;

            NavigateImmediate(screen, addToStack);
        }

        public static void NavigateWithLoading(
            Func<ScreenBase> screenFactory,
            string message = "Loading...",
            bool addToStack = true,
            int minDisplayMs = 220)
        {
            _ = NavigateWithLoadingAsync(screenFactory, message, addToStack, minDisplayMs);
        }

        private static async Task NavigateWithLoadingAsync(
            Func<ScreenBase> screenFactory,
            string message,
            bool addToStack,
            int minDisplayMs)
        {
            if (_window == null || screenFactory == null)
                return;

            // If already on a LoadingScreen, skip the overlay and navigate immediately.
            if (_currentScreen is LoadingScreen)
            {
                var immediate = screenFactory();
                Navigate(immediate, addToStack);
                return;
            }

            try
            {
                // Show the shared loading overlay on top of the current screen instead of
                // navigating to a separate LoadingScreen. This way, when the target screen's
                // OnShow() also calls ShowLoadingOverlay, the already-spinning overlay is
                // reused (just raised to top) — no animation reset, no stutter.
                ShowLoadingOverlay(message);
                var shownAt = DateTime.UtcNow;
                var generationAtShow = _loadingOverlayGeneration;

                await Task.Yield();

                ScreenBase target;
                try
                {
                    target = screenFactory();
                }
                catch
                {
                    HideLoadingOverlay();
                    return;
                }

                var elapsedMs = (DateTime.UtcNow - shownAt).TotalMilliseconds;
                if (elapsedMs < minDisplayMs)
                {
                    await Task.Delay((int)(minDisplayMs - elapsedMs));
                }

                // Navigate to the target screen. If the target's OnShow calls
                // ShowLoadingOverlay, the existing spinner continues spinning seamlessly
                // and the target screen is responsible for calling HideLoadingOverlay when
                // its async loading completes.
                Navigate(target, addToStack: addToStack);

                // If the target's OnShow did NOT call ShowLoadingOverlay (e.g. SeriesDetailsScreen),
                // the overlay is still showing from our call above — hide it now so it doesn't
                // get stuck on screen. We detect re-show by checking if the generation counter
                // is still the same (meaning the target didn't re-show it).
                if (_loadingOverlayGeneration == generationAtShow && _loadingVisual != null)
                {
                    HideLoadingOverlay();
                }
            }
            catch
            {
                HideLoadingOverlay();
            }
        }

        public static void NavigateBack()
        {
            if (_stack.Count == 0)
            {
                ShowExitConfirmation();
                return;
            }

            NavigateBackImmediate();
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

        public static void NotifyAppPaused()
        {
            NotifyPlaybackLifecycle(PlaybackStopReason.AppPaused, removeKeyHandler: false);
        }

        public static void NotifyAppTerminating()
        {
            NotifyPlaybackLifecycle(PlaybackStopReason.AppTerminating, removeKeyHandler: true);
        }

        private static void NotifyPlaybackLifecycle(
            PlaybackStopReason reason,
            bool removeKeyHandler)
        {
            try
            {
                if (_currentScreen is IPlaybackLifecycleHandler playbackLifecycle)
                {
                    // Request first so VideoPlayerScreen closes the report gate with the
                    // lifecycle-specific reason. OnHide joins that same task and performs
                    // its bounded wait before disposing the native player.
                    _ = playbackLifecycle.RequestPlaybackStop(reason);
                }
            }
            catch (Exception ex)
            {
                Tizen.Log.Error(
                    "PlaybackLifecycle",
                    $"Unable to request lifecycle stop reason={reason} error={ex.GetType().Name}");
            }

            try
            {
                _currentScreen?.OnHide();
            }
            catch (Exception ex)
            {
                Tizen.Log.Error(
                    "PlaybackLifecycle",
                    $"Unable to hide current screen reason={reason} error={ex.GetType().Name}");
            }
            finally
            {
                if (removeKeyHandler && _window != null)
                {
                    _window.InterceptKeyEvent -= OnInterceptKeyEvent;
                }
            }
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

            try { _currentScreen.ShowDebugOverlayPublic(); } catch { }
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

            // Restored screens have all images already loaded, so the per-image
            // ResourceReady fades never re-fire. Play a lightweight fade-from-black
            // transition to match the forward-navigation feel.
            //
            // We fade a single solid-black leaf overlay (opaque -> transparent) on
            // top of everything, rather than animating the restored screen's own
            // Opacity, for two reasons:
            //   1. Performance: animating a heavyweight screen subtree's Opacity
            //      forces DALi to render the whole tree (grids of cached posters,
            //      backdrops, logos, ...) into an offscreen buffer every frame for
            //      alpha compositing, which stutters/hangs on TV GPU hardware.
            //      A leaf overlay needs no offscreen compositing, so it is cheap.
            //   2. Consistency: several screens (Home, Movie/Episode details) show
            //      the opaque full-screen loading overlay as the first statement of
            //      OnShow(). Animating the screen's Opacity happened *behind* that
            //      overlay and was therefore invisible, making the transition look
            //      instant on those screens. A top-most overlay fades consistently
            //      regardless of what OnShow() puts on screen.
            PlayBackNavigationFade();

            try { _currentScreen.ShowDebugOverlayPublic(); } catch { }
        }

        // Lightweight fade-from-black transition used by backward navigation. See the
        // rationale in NavigateBackImmediate. The overlay is a single solid-black leaf
        // view (no children) kept topmost, so its Opacity animation is cheap and always
        // visible above whatever OnShow() attaches (including the loading overlay).
        private static View _backFadeOverlay;
        private static Animation _backFadeAnimation;

        private static void PlayBackNavigationFade()
        {
            if (_window == null)
                return;

            if (_backFadeOverlay == null)
            {
                _backFadeOverlay = new View
                {
                    WidthResizePolicy = ResizePolicyType.FillToParent,
                    HeightResizePolicy = ResizePolicyType.FillToParent,
                    BackgroundColor = new NColor(0f, 0f, 0f, 1f),
                    // Never participate in D-pad focus / key routing.
                    Focusable = false
                };
            }

            // Cancel any in-flight fade first. StopAndDispose stops the previous
            // animation (whose onFinished removes the overlay); we then re-add it
            // below, so the ordering stays correct even under rapid back presses.
            UiAnimator.StopAndDispose(ref _backFadeAnimation);

            _backFadeOverlay.Opacity = 1.0f;
            try { _window.Remove(_backFadeOverlay); } catch { }
            _window.Add(_backFadeOverlay);
            _backFadeOverlay.RaiseToTop();

            _backFadeAnimation = UiAnimator.AnimateTo(
                _backFadeOverlay,
                "Opacity",
                0.0f,
                UiAnimator.FadeInDurationMs,
                () =>
                {
                    try { _window?.Remove(_backFadeOverlay); } catch { }
                });
        }

        private static void EnsureExitConfirmationPopupCreated()
        {
            if (_window == null || _exitConfirmationOverlay != null)
                return;

            _exitConfirmationOverlay = new View
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                BackgroundColor = NColor.Transparent
            };

            _exitConfirmationPanel = MonochromeAuthFactory.CreatePanel(width: ExitPopupWidth, yOffset: 0);
            _exitConfirmationPanel.Padding = new Extents(42, 42, 34, 34);
            _exitConfirmationPanel.BackgroundColor = new NColor(7f / 255f, 13f / 255f, 28f / 255f, 1.0f);
            _exitConfirmationPanel.BorderlineColor = new NColor(1f, 1f, 1f, 0.28f);
            if (_exitConfirmationPanel.Layout is LinearLayout panelLayout)
                panelLayout.CellPadding = new Size2D(0, 14);

            var title = MonochromeAuthFactory.CreateTitle("Exit Jellyfin?");
            title.PointSize = 36f;
            var subtitle = MonochromeAuthFactory.CreateSubtitle("Do you want to close the app?");
            subtitle.PointSize = 21f;
            subtitle.TextColor = new NColor(1f, 1f, 1f, 0.72f);

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

            _exitStayButton = CreateExitConfirmationButton("Stay");
            _exitConfirmButton = CreateExitConfirmationButton("Exit");
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

        private static View CreateExitConfirmationButton(string text)
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
                TextColor = NColor.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            button.Add(label);
            MonochromeAuthFactory.SetButtonFocusState(button, focused: false);
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
                MonochromeAuthFactory.SetButtonFocusState(_exitConfirmationButtons[i], focused);
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

        private static void ResetScreenTransform(ScreenBase screen)
        {
            if (screen == null)
                return;

            try
            {
                screen.PositionX = 0.0f;
                screen.PositionY = 0.0f;
                screen.Opacity = 1.0f;
                screen.Scale = Vector3.One;
            }
            catch { }
        }

        private static View _reconnectOverlay;

        public static void ShowReconnectOverlay(string message)
        {
            if (_window == null) return;

            Tizen.Applications.CoreApplication.Post(() =>
            {
                if (_reconnectOverlay == null)
                {
                    _reconnectOverlay = new View
                    {
                        WidthResizePolicy = ResizePolicyType.FillToParent,
                        HeightResizePolicy = ResizePolicyType.FillToParent,
                        BackgroundColor = new NColor(0f, 0f, 0f, 0.75f)
                    };

                    var panel = MonochromeAuthFactory.CreatePanel(width: 600, yOffset: 0);
                    panel.Padding = new Extents(40, 40, 30, 30);
                    panel.BackgroundColor = new NColor(7f / 255f, 13f / 255f, 28f / 255f, 1.0f);
                    panel.BorderlineColor = new NColor(1f, 1f, 1f, 0.2f);
                    if (panel.Layout is LinearLayout panelLayout)
                        panelLayout.CellPadding = new Size2D(0, 14);

                    var title = MonochromeAuthFactory.CreateTitle("Tailscale Connection");
                    title.PointSize = 32f;

                    var subtitle = MonochromeAuthFactory.CreateSubtitle(message);
                    subtitle.PointSize = 20f;
                    subtitle.TextColor = new NColor(1f, 1f, 1f, 0.7f);

                    panel.Add(title);
                    panel.Add(subtitle);
                    _reconnectOverlay.Add(panel);
                }

                try { _window.Remove(_reconnectOverlay); } catch { }
                _window.Add(_reconnectOverlay);
                _reconnectOverlay.Show();
                _reconnectOverlay.RaiseToTop();
            });
        }

        public static void HideReconnectOverlay()
        {
            if (_reconnectOverlay == null) return;
            Tizen.Applications.CoreApplication.Post(() =>
            {
                _reconnectOverlay.Hide();
                try { _window.Remove(_reconnectOverlay); } catch { }
            });
        }

        // Modular full-screen loading overlay reusing the SAME design language as
        // LoadingScreen (the AppleTvLoadingVisual shown for "Loading library...",
        // "Loading items...", etc.), so any screen can show a consistent full-screen loading
        // state in-place (without navigating to a separate LoadingScreen). Show/Hide are the
        // in-place equivalents of navigating to a LoadingScreen and back.
        //
        // ARCHITECTURE: The spinner visual is a persistent singleton — it is created once
        // and kept alive (and spinning) across show/hide cycles. HideLoadingOverlay merely
        // removes the root view from the window; ShowLoadingOverlay re-adds it. This way
        // the rotation animation continues from its current angle, eliminating the visible
        // "jump to 0°" stutter that occurred when the spinner was destroyed and recreated.
        private static View _loadingOverlay;
        private static AppleTvLoadingVisual _loadingVisual;
        private static bool _loadingOverlayAttached;
        // Generation counter incremented on every ShowLoadingOverlay call. Used by
        // NavigateWithLoadingAsync to detect whether the target screen re-claimed
        // the overlay (by calling ShowLoadingOverlay in its OnShow).
        private static int _loadingOverlayGeneration;

        public static void ShowLoadingOverlay(string message)
        {
            if (_window == null) return;

            // Runs synchronously on the UI thread (called as the first statement of a
            // screen's OnShow). Attaching immediately — rather than via CoreApplication.Post
            // — guarantees the overlay paints in the same frame the screen is (re)shown, so a
            // re-shown cached screen's stale child views never paint underneath it first.
            // (HideLoadingOverlay keeps its Post because it is invoked from post-await
            // continuations that may resume off the UI thread.)

            // Every call bumps the generation so NavigateWithLoadingAsync can track
            // whether the target screen re-claimed the overlay.
            _loadingOverlayGeneration++;

            // If the persistent spinner already exists, just (re-)attach and raise.
            if (_loadingVisual != null && _loadingOverlay != null)
            {
                if (!_loadingOverlayAttached)
                {
                    _window.Add(_loadingOverlay);
                    _loadingOverlayAttached = true;
                }
                _loadingOverlay.RaiseToTop();
                _loadingVisual.Start(); // no-op if already animating
                return;
            }

            // Clean up any partial state (e.g. overlay without visual or vice-versa)
            if (_loadingVisual != null || _loadingOverlay != null)
            {
                try { _loadingVisual?.Stop(); } catch { }
                try { if (_loadingOverlayAttached && _loadingOverlay != null) _window.Remove(_loadingOverlay); } catch { }
                try { _loadingVisual?.Dispose(); } catch { }
                _loadingVisual = null;
                _loadingOverlay = null;
                _loadingOverlayAttached = false;
            }

            _loadingVisual = new AppleTvLoadingVisual();
            _loadingOverlay = _loadingVisual.Root;
            _window.Add(_loadingOverlay);
            _loadingOverlayAttached = true;
            _loadingOverlay.RaiseToTop();
            _loadingVisual.Start();
        }

        public static void HideLoadingOverlay()
        {
            if (!_loadingOverlayAttached || _loadingOverlay == null) return;

            // Capture the generation at the time of this hide request. The deferred
            // callback only detaches if no new ShowLoadingOverlay has been called since
            // (which would have bumped the generation). This prevents a stale hide from
            // a previous screen's OnHide() from removing an overlay that the next screen's
            // OnShow() has already re-claimed.
            var generationAtHide = _loadingOverlayGeneration;

            // Detach via Post (callers often invoke from off-UI-thread continuations).
            // The spinner visual is NOT stopped or disposed — it stays alive so the next
            // ShowLoadingOverlay can re-attach it without resetting the animation.
            Tizen.Applications.CoreApplication.Post(() =>
            {
                if (!_loadingOverlayAttached || _loadingOverlay == null) return;
                // If ShowLoadingOverlay was called after this hide was requested,
                // the overlay belongs to the new caller — don't detach it.
                if (_loadingOverlayGeneration != generationAtHide) return;
                try { _window.Remove(_loadingOverlay); } catch { }
                _loadingOverlayAttached = false;
            });
        }

    }
}

