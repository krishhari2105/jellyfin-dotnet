using System;
using System.Threading;
using System.Threading.Tasks;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Core;

using ThreadingTimer = System.Threading.Timer;

namespace JellyfinTizen.Screens
{
    public class StartupScreen : ScreenBase
    {
        private bool _navigated;
        private ThreadingTimer _fallbackTimer;
        private TextLabel _status;
        private bool _loaded;

        public StartupScreen()
        {
        }

        public override void OnShow()
        {
            if (_loaded)
                return;

            _loaded = true;
            Load();
        }

        private async void Load()
        {
            var label = new TextLabel("Loading...")
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                PointSize = 44,
                TextColor = Color.White
            };

            _status = new TextLabel("")
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightSpecification = 50,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                PointSize = 20,
                TextColor = new Color(1, 1, 1, 0.6f),
                PositionY = Window.Default.Size.Height - 120
            };

            Add(label);
            Add(_status);

            UpdateStatus("Startup: init");

            // Safety fallback if network calls hang for any reason.
            _fallbackTimer = new ThreadingTimer(_ =>
            {
                if (_navigated)
                    return;

                _navigated = true;
                try
                {
                    Tizen.Applications.CoreApplication.Post(() =>
                    {
                        UpdateStatus("Startup: fallback -> ServerSetup");
                        NavigationService.Navigate(
                            new ServerSetupScreen(),
                            addToStack: false
                        );
                    });
                }
                catch
                {
                    NavigationService.Navigate(
                        new ServerSetupScreen(),
                        addToStack: false
                    );
                }
            }, null, 12000, Timeout.Infinite);

            if (AppState.TryRestoreFullSession())
            {
                if (!_navigated)
                {
                    _navigated = true;
                    _fallbackTimer?.Dispose();
                    UpdateStatus("Startup: session ok -> HomeLoading");
                    NavigationService.Navigate(
                        new HomeLoadingScreen(),
                        addToStack: false
                    );
                }
                return;
            }

            if (AppState.TryRestoreServer())
            {
                UpdateStatus("Startup: server ok -> users");
                if (!_navigated)
                {
                    NavigationService.Navigate(
                        new LoadingScreen("Fetching users..."),
                        addToStack: false
                    );
                }

                try
                {
                    var users = await WithTimeout(
                        AppState.Jellyfin.GetPublicUsersAsync(),
                        10000
                    );
                    if (!_navigated)
                    {
                        _navigated = true;
                        _fallbackTimer?.Dispose();
                        UpdateStatus("Startup: users ok -> UserSelect");
                        NavigationService.Navigate(
                            new UserSelectScreen(users),
                            addToStack: false
                        );
                    }
                }
                catch
                {
                    if (!_navigated)
                    {
                        _navigated = true;
                        _fallbackTimer?.Dispose();
                        UpdateStatus("Startup: users failed -> ServerSetup");
                        NavigationService.Navigate(
                            new ServerSetupScreen(),
                            addToStack: false
                        );
                    }
                }
                return;
            }

            if (!_navigated)
            {
                _navigated = true;
                _fallbackTimer?.Dispose();
                UpdateStatus("Startup: no server -> ServerSetup");
                NavigationService.Navigate(
                    new ServerSetupScreen(),
                    addToStack: false
                );
            }
        }

        private static async Task<T> WithTimeout<T>(Task<T> task, int timeoutMs)
        {
            var completed = await Task.WhenAny(task, Task.Delay(timeoutMs));
            if (completed != task)
                throw new TimeoutException("Startup network request timed out.");

            return await task;
        }

        private void UpdateStatus(string text)
        {
            try
            {
                Tizen.Applications.CoreApplication.Post(() =>
                {
                    if (_status != null)
                        _status.Text = text;
                });
            }
            catch
            {
                if (_status != null)
                    _status.Text = text;
            }
        }
    }
}
