using System;
using System.Collections.Generic;
using Tizen.Applications;
using Tizen.NUI;
using JellyfinTizen.Screens;

namespace JellyfinTizen.Core
{
    public static class NavigationService
    {
        private static Window _window;
        private static ScreenBase _currentScreen;
        private static readonly Stack<ScreenBase> _stack = new();
        private const bool LogKeyEvents = true;

        public static void Init(Window window)
        {
            _window = window;
            // standard NUI event - this works!
            _window.KeyEvent += OnKeyEvent; 
        }

        private static void OnKeyEvent(object sender, Window.KeyEventArgs e)
        {
            if (e.Key.State != Key.StateType.Down)
                return;

            // 1. Log Raw Key to Screen (So you know it works)
            if (_currentScreen is VideoPlayerScreen player)
            {
                player.Log($"[NUI RAW] {e.Key.KeyPressedName}");
            }

            if (!(_currentScreen is IKeyHandler handler))
                return;

            if (LogKeyEvents)
                Console.WriteLine($"KeyPressedName={e.Key.KeyPressedName}");

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

        // ... (Keep Navigate, NavigateBack, ClearStack, HandleBack as they were) ...
        
        public static void Navigate(ScreenBase screen, bool addToStack = true)
        {
            if (_currentScreen != null)
            {
                _currentScreen.OnHide();
                _window.Remove(_currentScreen);
                if (addToStack) _stack.Push(_currentScreen);
                else _currentScreen.Dispose();
            }
            _currentScreen = screen;
            _window.Add(_currentScreen);
            _currentScreen.OnShow();
        }

        public static void NavigateBack()
        {
            if (_stack.Count == 0) { Application.Current?.Exit(); return; }
            _currentScreen.OnHide();
            _window.Remove(_currentScreen);
            _currentScreen.Dispose();
            _currentScreen = _stack.Pop();
            if (_currentScreen == null) return;
            _window.Add(_currentScreen);
            _currentScreen.OnShow();
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
    }
}