using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using JellyfinTizen.Core;

using ThreadingTimer = System.Threading.Timer;

namespace JellyfinTizen.Screens
{
    public class StartupScreen : ScreenBase
    {
        private bool _navigated;
        private ThreadingTimer _fallbackTimer;
        private bool _loaded;

        public StartupScreen()
        {
        }

        public override void OnShow()
        {
            if (_loaded)
            {
                NavigationService.ShowLoadingOverlay("Loading...");
                return;
            }

            _loaded = true;
            FireAndForget(LoadAsync(), nameof(LoadAsync));
        }

        public override void OnHide()
        {
            NavigationService.HideLoadingOverlay();
            _fallbackTimer?.Dispose();
            _fallbackTimer = null;
        }

        private async Task LoadAsync()
        {
            try
            {
                NavigationService.ShowLoadingOverlay("Loading...");

#if TAILSCALE
                // Detect if the active server uses Tailscale and wait for it to initialize
                try
                {
                    var active = AppState.GetStoredServers().FirstOrDefault(s => s.IsActive);
                    if (active != null && !string.IsNullOrWhiteSpace(active.Url))
                    {
                        if (Uri.TryCreate(active.Url, UriKind.Absolute, out var uri))
                        {
                            string host = uri.Host;
                            bool isTailscaleServer = AppState.IsTailscaleUrl(active.Url);

                            if (isTailscaleServer && AppState.TailscaleReadyTask != null)
                            {
                                // Wait for Tailscale daemon and proxy to be ready
                                await Task.WhenAny(AppState.TailscaleReadyTask, Task.Delay(10000));

                                // If Tailscale startup failed, don't wait for backend — skip to server entry
                                if (AppState.TailscaleStartupFailed || AppState.Tailscale == null)
                                {
                                    // Fall through to normal startup
                                }
                                else if (AppState.Tailscale != null)
                                {
                                    // Wait for Tailscale backend connection to be Running (online)
                                    await AppState.Tailscale.WaitForBackendRunningAsync(10000);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Tizen.Log.Warn("StartupScreen", $"Failed checking Tailscale status: {ex.Message}");
                }
#endif

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
                }, null, AppState.StartupFallbackTimeoutMs, Timeout.Infinite);

                if (await TryResumeSavedTokenSessionAsync())
                {
                    if (!_navigated)
                    {
                        _navigated = true;
                        _fallbackTimer?.Dispose();

#if TAILSCALE
                        // If the restored server is a Tailscale URL and not connected, go through Tailscale auth first
                        if (AppState.IsTailscaleUrl(AppState.ServerUrl) && !await AppState.IsTailscaleConnectedAsync())
                        {
                            NavigationService.Navigate(
                                new TailscaleAuthScreen(),
                                addToStack: false
                            );
                        }
                        else
#endif
                        {
                            NavigationService.Navigate(
                                new HomeLoadingScreen(),
                                addToStack: false
                            );
                        }
                    }
                    return;
                }

                if (AppState.TryRestoreServer())
                {
#if TAILSCALE
                    // If the restored server is a Tailscale URL and not connected, go through Tailscale auth first
                    if (AppState.IsTailscaleUrl(AppState.ServerUrl) && !await AppState.IsTailscaleConnectedAsync())
                    {
                        if (!_navigated)
                        {
                            _navigated = true;
                            _fallbackTimer?.Dispose();
                            NavigationService.Navigate(
                                new TailscaleAuthScreen(),
                                addToStack: false
                            );
                        }
                        return;
                    }
#endif

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
            catch
            {
                if (_navigated)
                    return;

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
#if TAILSCALE
            // Show Tailscale auth screen if daemon is running OR socket is reachable from a prior run
            if (AppState.Tailscale != null &&
                (AppState.Tailscale.IsRunning || AppState.Tailscale.IsSocketReachable))
            {
                return new TailscaleAuthScreen();
            }
#endif

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
