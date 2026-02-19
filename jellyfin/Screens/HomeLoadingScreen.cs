using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Core;
using JellyfinTizen.UI;

using ThreadingTimer = System.Threading.Timer;

namespace JellyfinTizen.Screens
{
    public class HomeLoadingScreen : ScreenBase, IKeyHandler
    {
        private bool _navigated;
        private ThreadingTimer _fallbackTimer;
        public HomeLoadingScreen()
        {
            Load();
        }

        private async void Load()
        {
            var label = new TextLabel("Loading libraries...")
            {
                WidthResizePolicy = ResizePolicyType.FillToParent,
                HeightResizePolicy = ResizePolicyType.FillToParent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                PointSize = UiTheme.HomeLoadingText,
                TextColor = UiTheme.TextPrimary
            };

            Add(label);

            _fallbackTimer = new ThreadingTimer(_ =>
            {
                if (_navigated)
                    return;

                _navigated = true;
                NavigateOnUiThread(() =>
                {
                    NavigationService.Navigate(
                        new ServerSetupScreen(),
                        addToStack: false
                    );
                });
            }, null, 25000, Timeout.Infinite);

            try
            {
                var libs = await WithTimeout(
                    AppState.Jellyfin.GetLibrariesAsync(AppState.UserId),
                    20000
                );

                var rows = await BuildHomeRowsAsync(libs);

                if (!_navigated)
                {
                    _navigated = true;
                    _fallbackTimer?.Dispose();
                    NavigationService.ClearStack();
                    NavigationService.Navigate(
                        new HomeScreen(rows),
                        addToStack: false
                    );
                }
            }
            catch (Exception ex)
            {
                if (_navigated)
                    return;

                _navigated = true;
                _fallbackTimer?.Dispose();

                if (!IsSessionExpired(ex))
                {
                    NavigateOnUiThread(() =>
                    {
                        NavigationService.Navigate(
                            new ServerSetupScreen(),
                            addToStack: false
                        );
                    });
                    return;
                }

                AppState.ClearSession(clearServer: false);
                NavigateOnUiThread(() =>
                {
                    NavigationService.Navigate(
                        new LoadingScreen("Session expired. Please sign in."),
                        addToStack: false
                    );
                });

                try
                {
                    var users = await WithTimeout(
                        AppState.Jellyfin.GetPublicUsersAsync(),
                        12000
                    );
                    NavigateOnUiThread(() =>
                    {
                        NavigationService.Navigate(
                            new UserSelectScreen(users),
                            addToStack: false
                        );
                    });
                }
                catch
                {
                    NavigateOnUiThread(() =>
                    {
                        NavigationService.Navigate(
                            new ServerSetupScreen(),
                            addToStack: false
                        );
                    });
                }
            }
        }

        private async Task<List<JellyfinTizen.Models.HomeRowData>> BuildHomeRowsAsync(List<JellyfinTizen.Models.JellyfinLibrary> libs)
        {
            var rows = new List<JellyfinTizen.Models.HomeRowData>();

            var apiKey = Uri.EscapeDataString(AppState.AccessToken);
            var serverUrl = AppState.Jellyfin.ServerUrl;

            var librariesRow = new JellyfinTizen.Models.HomeRowData
            {
                Title = "Libraries",
                Kind = JellyfinTizen.Models.HomeRowKind.Libraries
            };

            foreach (var lib in libs)
            {
                var imageUrl = lib.HasPrimaryImage
                    ? $"{serverUrl}/Items/{lib.Id}/Images/Primary?maxWidth=900&quality=85&api_key={apiKey}"
                    : null;

                librariesRow.Items.Add(new JellyfinTizen.Models.HomeItemData
                {
                    Title = lib.Name,
                    ImageUrl = imageUrl,
                    Library = lib
                });
            }

            if (librariesRow.Items.Count > 0)
                rows.Add(librariesRow);

            var tvLib = libs.Find(l => l.CollectionType == "tvshows");
            if (tvLib != null)
            {
                var nextUp = await WithTimeout(
                    AppState.Jellyfin.GetNextUpAsync(tvLib.Id, 20),
                    10000
                );

                if (nextUp.Count > 0)
                {
                    var nextUpRow = new JellyfinTizen.Models.HomeRowData
                    {
                        Title = "Next Up",
                        Kind = JellyfinTizen.Models.HomeRowKind.NextUp
                    };

                    foreach (var item in nextUp)
                    {
                        var imageUrl = GetThumbOrBackdropUrl(item, serverUrl, apiKey, 420);

                        nextUpRow.Items.Add(new JellyfinTizen.Models.HomeItemData
                        {
                            Title = item.Name,
                            Subtitle = item.SeriesName,
                            ImageUrl = imageUrl,
                            Media = item
                        });
                    }

                    rows.Add(nextUpRow);
                }
            }

            var continueWatching = await WithTimeout(
                AppState.Jellyfin.GetContinueWatchingAsync(20),
                10000
            );

            if (continueWatching.Count > 0)
            {
                var continueRow = new JellyfinTizen.Models.HomeRowData
                {
                    Title = "Continue Watching",
                    Kind = JellyfinTizen.Models.HomeRowKind.ContinueWatching
                };

                foreach (var item in continueWatching)
                {
                    var imageUrl = GetThumbOrBackdropUrl(item, serverUrl, apiKey, 420);

                    continueRow.Items.Add(new JellyfinTizen.Models.HomeItemData
                    {
                        Title = item.Name,
                        Subtitle = item.SeriesName,
                        ImageUrl = imageUrl,
                        Media = item
                    });
                }

                rows.Add(continueRow);
            }

            foreach (var lib in libs)
            {
                var includeTypes = lib.CollectionType == "tvshows"
                    ? "Series"
                    : "Movie";

                var recent = await WithTimeout(
                    AppState.Jellyfin.GetRecentlyAddedAsync(lib.Id, includeTypes, 20),
                    10000
                );

                if (recent.Count == 0)
                    continue;

                var recentRow = new JellyfinTizen.Models.HomeRowData
                {
                    Title = $"Recently Added - {lib.Name}",
                    Kind = JellyfinTizen.Models.HomeRowKind.RecentlyAdded
                };

                foreach (var item in recent)
                {
                    var imageUrl =
                        $"{serverUrl}/Items/{item.Id}/Images/Primary/0?maxWidth=320&quality=90&api_key={apiKey}";

                    recentRow.Items.Add(new JellyfinTizen.Models.HomeItemData
                    {
                        Title = item.Name,
                        Subtitle = item.SeriesName,
                        ImageUrl = imageUrl,
                        Media = item
                    });
                }

                rows.Add(recentRow);
            }

            return rows;
        }

        private static string GetThumbOrBackdropUrl(
            JellyfinTizen.Models.JellyfinMovie item,
            string serverUrl,
            string apiKey,
            int maxWidth)
        {
            if (item.HasThumb)
                return $"{serverUrl}/Items/{item.Id}/Images/Thumb/0?maxWidth={maxWidth}&quality=90&api_key={apiKey}";

            if (item.HasBackdrop)
                return $"{serverUrl}/Items/{item.Id}/Images/Backdrop/0?maxWidth={maxWidth}&quality=85&api_key={apiKey}";

            if (item.HasPrimary)
                return $"{serverUrl}/Items/{item.Id}/Images/Primary/0?maxWidth={maxWidth}&quality=90&api_key={apiKey}";

            return null;
        }

        private static async Task<T> WithTimeout<T>(Task<T> task, int timeoutMs)
        {
            var completed = await Task.WhenAny(task, Task.Delay(timeoutMs));
            if (completed != task)
                throw new TimeoutException("Home load network request timed out.");

            return await task;
        }

        private static bool IsSessionExpired(Exception ex)
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
            if (message.IndexOf("401", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("403", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("unauthorized", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("forbidden", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }

        private static void NavigateOnUiThread(Action navigationAction)
        {
            if (navigationAction == null)
                return;

            try
            {
                Tizen.Applications.CoreApplication.Post(navigationAction);
            }
            catch
            {
                navigationAction();
            }
        }

        public void HandleKey(AppKey key)
        {
            if (key != AppKey.Back || _navigated)
                return;

            _navigated = true;
            _fallbackTimer?.Dispose();
            NavigateOnUiThread(() =>
            {
                NavigationService.Navigate(
                    new ServerSetupScreen(),
                    addToStack: false
                );
            });
        }

    }
}
