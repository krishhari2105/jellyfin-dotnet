using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using JellyfinTizen.Core;
using JellyfinTizen.UI;
using JellyfinTizen.Utils;

using ThreadingTimer = System.Threading.Timer;

namespace JellyfinTizen.Screens
{
    public class HomeLoadingScreen : ScreenBase, IKeyHandler
    {
        private const int RecentPerLibraryLimit = 4;
        private const int RecentFetchConcurrency = 4;

        private bool _navigated;
        private ThreadingTimer _fallbackTimer;
        private static readonly SemaphoreSlim RecentFetchGate = new(RecentFetchConcurrency, RecentFetchConcurrency);
        private static readonly ConcurrentDictionary<string, string> ImageUrlCache = new();

        private sealed class RecentLibraryResult
        {
            public int Index;
            public List<JellyfinTizen.Models.HomeItemData> Items;
        }

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
                var loadTimer = PerfTrace.Start();
                var libs = await WithTimeout(
                    AppState.Jellyfin.GetLibrariesAsync(AppState.UserId),
                    20000
                );
                PerfTrace.End("HomeLoadingScreen.Load.GetLibraries", loadTimer);

                var rowsTimer = PerfTrace.Start();
                var rows = await BuildHomeRowsAsync(libs);
                PerfTrace.End("HomeLoadingScreen.Load.BuildHomeRows", rowsTimer);

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
            var rows = new List<JellyfinTizen.Models.HomeRowData>(libs.Count + 3);

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
                    ? BuildCachedImageUrl(
                        $"library-primary:{lib.Id}:760:72:{apiKey}",
                        () => $"{serverUrl}/Items/{lib.Id}/Images/Primary?maxWidth=760&quality=72&api_key={apiKey}")
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
            var nextUpTask = tvLib != null
                ? WithTimeout(AppState.Jellyfin.GetNextUpAsync(tvLib.Id, 20), 10000)
                : Task.FromResult(new List<JellyfinTizen.Models.JellyfinMovie>());
            var continueTask = WithTimeout(
                AppState.Jellyfin.GetContinueWatchingAsync(20),
                10000
            );

            await Task.WhenAll(nextUpTask, continueTask);

            var nextUp = nextUpTask.Result;
            if (nextUp.Count > 0)
            {
                var nextUpRow = new JellyfinTizen.Models.HomeRowData
                {
                    Title = "Next Up",
                    Kind = JellyfinTizen.Models.HomeRowKind.NextUp
                };

                foreach (var item in nextUp)
                {
                    var imageUrl = GetThumbOrBackdropUrl(item, serverUrl, apiKey, 360);

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

            var continueWatching = continueTask.Result;
            if (continueWatching.Count > 0)
            {
                var continueRow = new JellyfinTizen.Models.HomeRowData
                {
                    Title = "Continue Watching",
                    Kind = JellyfinTizen.Models.HomeRowKind.ContinueWatching
                };

                foreach (var item in continueWatching)
                {
                    var imageUrl = GetThumbOrBackdropUrl(item, serverUrl, apiKey, 360);

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

            var recentRow = new JellyfinTizen.Models.HomeRowData
            {
                Title = "Recently Added",
                Kind = JellyfinTizen.Models.HomeRowKind.RecentlyAdded
            };

            var recentTasks = new List<Task<RecentLibraryResult>>(libs.Count);
            for (int i = 0; i < libs.Count; i++)
            {
                recentTasks.Add(BuildRecentItemsForLibraryAsync(libs[i], i, serverUrl, apiKey));
            }

            var recentResults = await Task.WhenAll(recentTasks);
            Array.Sort(recentResults, (a, b) => a.Index.CompareTo(b.Index));
            foreach (var result in recentResults)
            {
                if (result?.Items == null || result.Items.Count == 0)
                    continue;

                recentRow.Items.AddRange(result.Items);
            }

            if (recentRow.Items.Count > 0)
                rows.Add(recentRow);

            return rows;
        }

        private async Task<RecentLibraryResult> BuildRecentItemsForLibraryAsync(
            JellyfinTizen.Models.JellyfinLibrary lib,
            int index,
            string serverUrl,
            string apiKey)
        {
            await RecentFetchGate.WaitAsync();
            try
            {
                var includeTypes = lib.CollectionType == "tvshows"
                    ? "Series"
                    : "Movie";

                var timer = PerfTrace.Start();
                var recent = await WithTimeout(
                    AppState.Jellyfin.GetRecentlyAddedAsync(lib.Id, includeTypes, RecentPerLibraryLimit),
                    10000
                );
                PerfTrace.End($"HomeLoadingScreen.RecentFetch.{lib.Name}", timer);

                var items = new List<JellyfinTizen.Models.HomeItemData>(recent.Count);
                foreach (var item in recent)
                {
                    var imageUrl = BuildCachedImageUrl(
                        $"recent-primary:{item.Id}:280:78:{apiKey}",
                        () => $"{serverUrl}/Items/{item.Id}/Images/Primary/0?maxWidth=280&quality=78&api_key={apiKey}");

                    items.Add(new JellyfinTizen.Models.HomeItemData
                    {
                        Title = item.Name,
                        Subtitle = string.IsNullOrWhiteSpace(item.SeriesName) ? lib.Name : item.SeriesName,
                        ImageUrl = imageUrl,
                        Media = item
                    });
                }

                return new RecentLibraryResult
                {
                    Index = index,
                    Items = items
                };
            }
            finally
            {
                RecentFetchGate.Release();
            }
        }

        private static string GetThumbOrBackdropUrl(
            JellyfinTizen.Models.JellyfinMovie item,
            string serverUrl,
            string apiKey,
            int maxWidth)
        {
            if (item.HasThumb)
                return BuildCachedImageUrl(
                    $"thumb:{item.Id}:{maxWidth}:82:{apiKey}",
                    () => $"{serverUrl}/Items/{item.Id}/Images/Thumb/0?maxWidth={maxWidth}&quality=82&api_key={apiKey}");

            if (item.HasBackdrop)
                return BuildCachedImageUrl(
                    $"backdrop:{item.Id}:{maxWidth}:78:{apiKey}",
                    () => $"{serverUrl}/Items/{item.Id}/Images/Backdrop/0?maxWidth={maxWidth}&quality=78&api_key={apiKey}");

            if (item.HasPrimary)
                return BuildCachedImageUrl(
                    $"primary:{item.Id}:{maxWidth}:82:{apiKey}",
                    () => $"{serverUrl}/Items/{item.Id}/Images/Primary/0?maxWidth={maxWidth}&quality=82&api_key={apiKey}");

            return null;
        }

        private static string BuildCachedImageUrl(string key, Func<string> factory)
        {
            if (string.IsNullOrWhiteSpace(key) || factory == null)
                return null;

            return ImageUrlCache.GetOrAdd(key, _ => factory());
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
