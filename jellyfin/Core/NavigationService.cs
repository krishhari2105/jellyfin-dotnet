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
            _window.KeyEvent += OnKeyEvent;
        }

        public static void Navigate(ScreenBase screen, bool addToStack = true)
        {
            if (_currentScreen != null)
            {
                _currentScreen.OnHide();
                _window.Remove(_currentScreen);
                if (addToStack)
                {
                    _stack.Push(_currentScreen);
                }
                else
                {
                    _currentScreen.Dispose();
                }
            }

            _currentScreen = screen;
            _window.Add(_currentScreen);
            _currentScreen.OnShow();
        }

        public static void NavigateBack()
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
            if (_currentScreen == null)
                return;
            _window.Add(_currentScreen);
            _currentScreen.OnShow();
        }

        public static void ClearStack()
        {
            while (_stack.Count > 0)
            {
                var screen = _stack.Pop();
                try
                {
                    screen?.OnHide();
                    _window?.Remove(screen);
                    screen?.Dispose();
                }
                catch
                {
                    // ignore cleanup errors
                }
            }
        }

        public static void HandleBack()
        {
            if (_currentScreen is IKeyHandler handler)
            {
                handler.HandleKey(AppKey.Back);
            }
        }




        private static void OnKeyEvent(object sender, Window.KeyEventArgs e)

        {
            if (e.Key.State != Key.StateType.Down)
                return;

            if (!(_currentScreen is IKeyHandler handler))
                return;

            if (LogKeyEvents)
                Console.WriteLine($"KeyPressedName={e.Key.KeyPressedName}, State={e.Key.State}");

            var key = e.Key.KeyPressedName switch
            {
                "Up" => AppKey.Up,
                "Down" => AppKey.Down,
                "Left" => AppKey.Left,
                "Right" => AppKey.Right,
                "Return" => AppKey.Enter,
                "Enter" => AppKey.Enter,
                "Back" => AppKey.Back,
                "BackSpace" => AppKey.Back,
                "Escape" => AppKey.Back,
                "XF86Back" => AppKey.Back,
                "XF86Home" => AppKey.Back,
                _ => AppKey.Unknown
            };

            handler.HandleKey(key);
        }

    }
}
