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
        private const int ImageUrlCacheMaxEntries = 2500;

        // Simple thread-safe LRU cache using ConcurrentDictionary + LinkedList
        // LinkedList tracks access order (oldest at First, newest at Last)
        // Single lock covers entire GetOrAdd operation to prevent race conditions
        private sealed class LruCache
        {
            private readonly ConcurrentDictionary<string, LinkedListNode<CacheEntry>> _dict = new();
            private readonly LinkedList<CacheEntry> _lru = new();
            private readonly int _maxSize;
            private readonly object _lruLock = new();

            private sealed class CacheEntry
            {
                public string Key;
                public string Value;
            }

            public LruCache(int maxSize) => _maxSize = maxSize;

            public string GetOrAdd(string key, Func<string> factory)
            {
                lock (_lruLock)
                {
                    if (_dict.TryGetValue(key, out var node))
                    {
                        _lru.Remove(node);
                        _lru.AddLast(node);
                        return node.Value.Value;
                    }

                    var value = factory();
                    var entry = new CacheEntry { Key = key, Value = value };
                    var newNode = new LinkedListNode<CacheEntry>(entry);

                    if (_dict.Count >= _maxSize)
                    {
                        var oldest = _lru.First;
                        _lru.RemoveFirst();
                        _dict.TryRemove(oldest.Value.Key, out _);
                    }

                    _lru.AddLast(newNode);
                    _dict[key] = newNode;
                    return value;
                }
            }

            public void Clear() { _dict.Clear(); lock (_lruLock) _lru.Clear(); }
        }

        private bool _navigated;
        private bool _loaded;
        private ThreadingTimer _fallbackTimer;
        private AppleTvLoadingVisual _loadingVisual;

        // Single static LRU cache instance (initialized on first access)
        private static readonly Lazy<LruCache> ImageUrlCache = new(() => new LruCache(ImageUrlCacheMaxEntries));

        public HomeLoadingScreen()
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
            FireAndForget(LoadAsync(), nameof(LoadAsync));
        }

        public override void OnHide()
        {
            _loadingVisual?.Stop();
            _loadingVisual?.Dispose();
            _fallbackTimer?.Dispose();
            _fallbackTimer = null;
        }

        private async Task LoadAsync()
        {
            if (_loadingVisual == null)
            {
                _loadingVisual = new AppleTvLoadingVisual("Loading libraries...");
                Add(_loadingVisual.Root);
            }
            else
            {
                _loadingVisual.SetMessage("Loading libraries...");
            }

            _loadingVisual.Start();

            _fallbackTimer = new ThreadingTimer(_ =>
            {
                if (_navigated)
                    return;

                _navigated = true;
                NavigateOnUiThread(() =>
                {
                    NavigationService.Navigate(
                        CreateServerEntryScreen(),
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
                            CreateServerEntryScreen(),
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
                            CreateServerEntryScreen(),
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
                            $"library-primary:{lib.Id}:760:60:{apiKey}",
                            () => AppState.RewriteImageUrlForTailscale($"{serverUrl}/Items/{lib.Id}/Images/Primary/0?maxWidth=760&quality=60&api_key={apiKey}"))
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

            var tvLib = libs.Find(l => l.IsTvShows);
            var nextUpTask = tvLib != null
                ? WithTimeout(AppState.Jellyfin.GetNextUpAsync(tvLib.Id, 20), 10000)
                : Task.FromResult(new List<JellyfinTizen.Models.JellyfinMovie>());
            var continueTask = WithTimeout(
                AppState.Jellyfin.GetContinueWatchingAsync(20),
                10000
            );

            await Task.WhenAll(nextUpTask, continueTask);

            var nextUp = await nextUpTask;
            if (nextUp.Count > 0)
            {
                var nextUpRow = new JellyfinTizen.Models.HomeRowData
                {
                    Title = "Next Up",
                    Kind = JellyfinTizen.Models.HomeRowKind.NextUp
                };

                foreach (var item in nextUp)
                {
                    var imageUrl = GetThumbOrBackdropUrl(item, serverUrl, apiKey, 280);
                    imageUrl = BuildCachedImageUrl(
                        $"nextup:{item.Id}:280:70:{apiKey}",
                        () => AppState.RewriteImageUrlForTailscale(imageUrl.Replace("quality=82", "quality=70")));
                    var episodePrefix = FormatEpisodeCode(item);
                    var title = string.IsNullOrWhiteSpace(episodePrefix)
                        ? item.Name
                        : $"{episodePrefix} - {item.Name}";

                    nextUpRow.Items.Add(new JellyfinTizen.Models.HomeItemData
                    {
                        Title = title,
                        Subtitle = item.SeriesName,
                        ImageUrl = imageUrl,
                        Media = item
                    });
                }

                rows.Add(nextUpRow);
            }

            var continueWatching = await continueTask;
            if (continueWatching.Count > 0)
            {
                var continueRow = new JellyfinTizen.Models.HomeRowData
                {
                    Title = "Continue Watching",
                    Kind = JellyfinTizen.Models.HomeRowKind.ContinueWatching
                };

                foreach (var item in continueWatching)
                {
                    var imageUrl = GetThumbOrBackdropUrl(item, serverUrl, apiKey, 280);
                    imageUrl = BuildCachedImageUrl(
                        $"continue:{item.Id}:280:70:{apiKey}",
                        () => AppState.RewriteImageUrlForTailscale(imageUrl.Replace("quality=82", "quality=70")));

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

            try
            {
                var timer = PerfTrace.Start();
                var recent = await WithTimeout(
                    AppState.Jellyfin.GetRecentlyAddedGlobalAsync("Movie,Series", 20),
                    10000
                );
                PerfTrace.End("HomeLoadingScreen.RecentFetch.Global", timer);

                foreach (var item in recent)
                {
                    var imageUrl = BuildCachedImageUrl(
                        $"recent-primary:{item.Id}:280:70:{apiKey}",
                        () => AppState.RewriteImageUrlForTailscale($"{serverUrl}/Items/{item.Id}/Images/Primary/0?maxWidth=280&quality=70&api_key={apiKey}"));

                    recentRow.Items.Add(new JellyfinTizen.Models.HomeItemData
                    {
                        Title = item.Name,
                        Subtitle = item.SeriesName,
                        ImageUrl = imageUrl,
                        Media = item
                    });
                }
            }
            catch (Exception ex)
            {
                Tizen.Log.Error("Jellyfin", $"Failed to fetch recently added items globally: {ex.Message}");
            }

            if (recentRow.Items.Count > 0)
                rows.Add(recentRow);

            return rows;
        }

        private static string GetThumbOrBackdropUrl(
            JellyfinTizen.Models.JellyfinMovie item,
            string serverUrl,
            string apiKey,
            int maxWidth)
        {
            if (item.HasThumb)
                return BuildCachedImageUrl(
                    $"thumb:{item.Id}:{maxWidth}:70:{apiKey}",
                    () => AppState.RewriteImageUrlForTailscale($"{serverUrl}/Items/{item.Id}/Images/Thumb/0?maxWidth={maxWidth}&quality=70&api_key={apiKey}"));

            if (item.HasBackdrop)
                return BuildCachedImageUrl(
                    $"backdrop:{item.Id}:{maxWidth}:65:{apiKey}",
                    () => AppState.RewriteImageUrlForTailscale($"{serverUrl}/Items/{item.Id}/Images/Backdrop/0?maxWidth={maxWidth}&quality=65&api_key={apiKey}"));

            if (item.HasPrimary)
                return BuildCachedImageUrl(
                    $"primary:{item.Id}:{maxWidth}:70:{apiKey}",
                    () => AppState.RewriteImageUrlForTailscale($"{serverUrl}/Items/{item.Id}/Images/Primary/0?maxWidth={maxWidth}&quality=70&api_key={apiKey}"));

            return null;
        }

        private static string FormatEpisodeCode(JellyfinTizen.Models.JellyfinMovie item)
        {
            if (item == null)
                return string.Empty;

            if (item.ParentIndexNumber > 0 && item.IndexNumber > 0)
                return $"S{item.ParentIndexNumber}:E{item.IndexNumber}";

            if (item.IndexNumber > 0)
                return $"E{item.IndexNumber}";

            return string.Empty;
        }

        private static string BuildCachedImageUrl(string key, Func<string> factory)
        {
            if (string.IsNullOrWhiteSpace(key) || factory == null)
                return null;

            return ImageUrlCache.Value.GetOrAdd(key, factory);
        }

        private static async Task<T> WithTimeout<T>(Task<T> task, int timeoutMs)
        {
            var completed = await Task.WhenAny(task, Task.Delay(timeoutMs));
            if (completed != task)
                throw new TimeoutException("Home load network request timed out.");

            return await task;
        }

        private static ScreenBase CreateServerEntryScreen()
        {
            return AppState.HasStoredServers()
                ? new ServerPickerScreen()
                : new ServerSetupScreen();
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
                    CreateServerEntryScreen(),
                    addToStack: false
                );
            });
        }

    }
}
