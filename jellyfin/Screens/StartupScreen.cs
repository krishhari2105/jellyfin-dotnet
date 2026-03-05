using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using JellyfinTizen.Core;
using JellyfinTizen.UI;

using ThreadingTimer = System.Threading.Timer;

namespace JellyfinTizen.Screens
{
    public class StartupScreen : ScreenBase
    {
        private bool _navigated;
        private ThreadingTimer _fallbackTimer;
        private AppleTvLoadingVisual _loadingVisual;
        private bool _loaded;

        public StartupScreen()
        {
        }

        public override void OnShow()
        {
            if (_loaded)
            {
                _loadingVisual?.Start();
                return;
            }

            _loaded = true;
            Load();
        }

        public override void OnHide()
        {
            _loadingVisual?.Stop();
            _fallbackTimer?.Dispose();
            _fallbackTimer = null;
        }

        private async void Load()
        {
            if (_loadingVisual == null)
            {
                _loadingVisual = new AppleTvLoadingVisual("Loading...");
                Add(_loadingVisual.Root);
            }
            else
            {
                _loadingVisual.SetMessage("Loading...");
            }

            _loadingVisual.Start();

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
                            CreateServerEntryScreen(),
                            addToStack: false
                        );
                    });
                }
                catch
                {
                    NavigationService.Navigate(
                        CreateServerEntryScreen(),
                        addToStack: false
                    );
                }
            }, null, 12000, Timeout.Infinite);

            if (await TryResumeSavedTokenSessionAsync())
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
                            CreateServerEntryScreen(),
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
                    CreateServerEntryScreen(),
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

        private static ScreenBase CreateServerEntryScreen()
        {
            return AppState.HasStoredServers()
                ? new ServerPickerScreen()
                : new ServerSetupScreen();
        }

        private async Task<bool> TryResumeSavedTokenSessionAsync()
        {
            try
            {
                if (!AppState.TryRestoreFullSession())
                    return false;

                if (string.IsNullOrWhiteSpace(AppState.UserId) ||
                    string.IsNullOrWhiteSpace(AppState.Username))
                {
                    var me = await WithTimeout(AppState.Jellyfin.GetCurrentUserAsync(), 10000);
                    var userId = string.IsNullOrWhiteSpace(AppState.UserId)
                        ? me.userId
                        : AppState.UserId;
                    var username = AppState.Username;
                    if (string.IsNullOrWhiteSpace(username))
                        username = me.username;

                    if (string.IsNullOrWhiteSpace(userId))
                        return false;

                    AppState.SaveSession(
                        AppState.ServerUrl,
                        AppState.AccessToken,
                        userId,
                        username ?? string.Empty
                    );
                    AppState.Jellyfin.SetUserId(userId);
                }
                return true;
            }
            catch (Exception ex)
            {
                if (IsUnauthorized(ex))
                    AppState.ClearSession(clearServer: false);

                return false;
            }
        }

        private static bool IsUnauthorized(Exception ex)
        {
            if (ex is HttpRequestException httpEx)
            {
                if (httpEx.StatusCode == HttpStatusCode.Unauthorized ||
                    httpEx.StatusCode == HttpStatusCode.Forbidden)
                {
                    return true;
                }
            }

            var message = ex?.Message ?? string.Empty;
            return message.IndexOf("401", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("403", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("unauthorized", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("forbidden", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
