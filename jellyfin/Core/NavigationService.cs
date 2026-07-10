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

            if (_currentScreen is LoadingScreen)
            {
                var immediate = screenFactory();
                Navigate(immediate, addToStack);
                return;
            }

            try
            {
                var loadingScreen = new LoadingScreen(message);
                var shownAt = DateTime.UtcNow;
                Navigate(loadingScreen, addToStack: addToStack);

                await Task.Yield();

                ScreenBase target;
                try
                {
                    target = screenFactory();
                }
                catch
                {
                    if (ReferenceEquals(_currentScreen, loadingScreen))
                        NavigateBack();
                    return;
                }

                var elapsedMs = (DateTime.UtcNow - shownAt).TotalMilliseconds;
                if (elapsedMs < minDisplayMs)
                {
                    await Task.Delay((int)(minDisplayMs - elapsedMs));
                }

                if (!ReferenceEquals(_currentScreen, loadingScreen))
                    return;

                Navigate(target, addToStack: false);
            }
            catch
            {
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

        public static void NotifyAppTerminating()
        {
            try
            {
                if (_window != null)
                {
                    _window.InterceptKeyEvent -= OnInterceptKeyEvent;
                }
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
            
            try { _currentScreen.ShowDebugOverlayPublic(); } catch { }
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

    }
}
