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
        // ==================== TEMPORARY AUTO-LOGIN CONFIGURATION ====================
        // Set to true to enable auto-login for testing, false to disable
        private static readonly bool ENABLE_AUTO_LOGIN = true;
        private const string TEST_SERVER_URL = "http://192.168.1.3:8096";
        private const string TEST_USERNAME = "Samsung";
        private const string TEST_PASSWORD = "2233";
        // ============================================================================

        private bool _navigated;
        private ThreadingTimer _fallbackTimer;
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

            Add(label);

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

            // ==================== TEMPORARY AUTO-LOGIN FEATURE ====================
            if (ENABLE_AUTO_LOGIN)
            {
                await PerformAutoLogin();
                return;
            }
            // =====================================================================

            if (AppState.TryRestoreFullSession())
            {
                if (!_navigated)
                {
                    _navigated = true;
                    _fallbackTimer?.Dispose();
                    NavigationService.Navigate(
                        new HomeLoadingScreen(),
                        addToStack: false
                    );
                }
                return;
            }

            if (AppState.TryRestoreServer())
            {
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

        private async Task PerformAutoLogin()
        {
            try
            {
                AppState.Jellyfin.Connect(TEST_SERVER_URL);
                AppState.SaveServer(TEST_SERVER_URL);

                var result = await AppState.Jellyfin.AuthenticateAsync(
                    TEST_USERNAME,
                    TEST_PASSWORD
                );
                AppState.AccessToken = result.accessToken;
                AppState.UserId = result.userId;
                AppState.Username = TEST_USERNAME;
                AppState.Jellyfin.SetAuthToken(result.accessToken);
                AppState.Jellyfin.SetUserId(result.userId);
                try
                {
                    await AppState.Jellyfin.PostCapabilitiesAsync();
                }
                catch
                {
                }


                AppState.SaveSession(
                    TEST_SERVER_URL,
                    result.accessToken,
                    result.userId,
                    TEST_USERNAME
                );

                if (!_navigated)
                {
                    _navigated = true;
                    _fallbackTimer?.Dispose();
                    NavigationService.ClearStack();
                    NavigationService.Navigate(
                        new HomeLoadingScreen(),
                        addToStack: false
                    );
                }
            }
            catch (Exception)
            {
                // Fall back to normal flow if auto-login fails
                LoadNormalFlow();
            }
        }

        private void LoadNormalFlow()
        {
            if (AppState.TryRestoreFullSession())
            {
                if (!_navigated)
                {
                    _navigated = true;
                    _fallbackTimer?.Dispose();
                    NavigationService.Navigate(
                        new HomeLoadingScreen(),
                        addToStack: false
                    );
                }
                return;
            }

            if (AppState.TryRestoreServer())
            {
                if (!_navigated)
                {
                    NavigationService.Navigate(
                        new LoadingScreen("Fetching users..."),
                        addToStack: false
                    );
                }

                // Fetch users in background
                Task.Run(async () =>
                {
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
                            Tizen.Applications.CoreApplication.Post(() =>
                            {
                                NavigationService.Navigate(
                                    new UserSelectScreen(users),
                                    addToStack: false
                                );
                            });
                        }
                    }
                    catch
                    {
                        if (!_navigated)
                        {
                            _navigated = true;
                            _fallbackTimer?.Dispose();
                            Tizen.Applications.CoreApplication.Post(() =>
                            {
                                NavigationService.Navigate(
                                    new ServerSetupScreen(),
                                    addToStack: false
                                );
                            });
                        }
                    }
                });
                return;
            }

            if (!_navigated)
            {
                _navigated = true;
                _fallbackTimer?.Dispose();
                NavigationService.Navigate(
                    new ServerSetupScreen(),
                    addToStack: false
                );
            }
        }
    }
}
