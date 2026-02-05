using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using JellyfinTizen.Models;


namespace JellyfinTizen.Core
{
    public class JellyfinService
    {
        private HttpClient _http;

        public string ServerUrl { get; private set; }
        public string AccessToken { get; private set; }
        public string UserId { get; private set; }

        public void Connect(string serverUrl)
        {
            ServerUrl = serverUrl.TrimEnd('/');

            _http = new HttpClient();
            _http.Timeout = System.TimeSpan.FromSeconds(10);
            _http.DefaultRequestHeaders.Add(
                "X-Emby-Authorization",
                "MediaBrowser Client=\"JellyfinTizen\", Device=\"SamsungTV\", DeviceId=\"tizen-tv\", Version=\"1.0\""
            );
        }

        public void SetAuthToken(string token)
        {
            AccessToken = token;

            if (_http.DefaultRequestHeaders.Contains("X-MediaBrowser-Token"))
                _http.DefaultRequestHeaders.Remove("X-MediaBrowser-Token");

            _http.DefaultRequestHeaders.Add("X-MediaBrowser-Token", token);
        }

        public void SetUserId(string userId)
        {
            UserId = userId;
        }

        public void ClearAuthToken()
        {
            AccessToken = null;

            if (_http != null && _http.DefaultRequestHeaders.Contains("X-MediaBrowser-Token"))
                _http.DefaultRequestHeaders.Remove("X-MediaBrowser-Token");
        }


        // STEP 2 will use this
        public async Task<string> GetAsync(string path)
        {
            var response = await _http.GetAsync(ServerUrl + path);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> PostAsync(string path, object body)
        {
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync(ServerUrl + path, content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task PostAsync(string path)
        {
            var response = await _http.PostAsync(ServerUrl + path, null);
            response.EnsureSuccessStatusCode();
        }

        public async Task<List<JellyfinUser>> GetPublicUsersAsync()
        {
            var json = await GetAsync("/Users/Public");
            return JsonSerializer.Deserialize<List<JellyfinUser>>(json);
        }

        public async Task<(string accessToken, string userId)> AuthenticateAsync(
        string username,
        string password)
        {
            var body = new
            {
                Username = username,
                Pw = password
            };

            var json = await PostAsync("/Users/AuthenticateByName", body);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var token = root.GetProperty("AccessToken").GetString();
            var userId = root.GetProperty("User").GetProperty("Id").GetString();

            return (token, userId);
        }

        public async Task<List<JellyfinLibrary>> GetLibrariesAsync(string userId)
        {
            var json = await GetAsync($"/Users/{userId}/Views");

            using var doc = JsonDocument.Parse(json);
            var items = doc.RootElement.GetProperty("Items");

            var libraries = new List<JellyfinLibrary>();

            foreach (var item in items.EnumerateArray())
            {
                var collectionType = item.GetProperty("CollectionType").GetString();

                if (collectionType != "movies" && collectionType != "tvshows")
                    continue;
                
                var hasPrimary = false;
                if (item.TryGetProperty("ImageTags", out var imageTags) &&
                    imageTags.ValueKind == JsonValueKind.Object)
                {
                    if (imageTags.TryGetProperty("Primary", out _))
                        hasPrimary = true;
                }

                libraries.Add(new JellyfinLibrary
                {
                    Id = item.GetProperty("Id").GetString(),
                    Name = item.GetProperty("Name").GetString(),
                    CollectionType = collectionType,
                    HasPrimaryImage = hasPrimary
                });
            }

            return libraries;
        }

        public async Task<List<JellyfinMovie>> GetMoviesAsync(string libraryId)
        {
            return await GetLibraryItemsAsync(libraryId, "Movie");
        }

        public async Task<List<JellyfinMovie>> GetLibraryItemsAsync(string libraryId, string includeItemTypes)
        {
            var url =
                $"/Users/{UserId}/Items?ParentId={libraryId}" +
                $"&IncludeItemTypes={includeItemTypes}" +
                "&Recursive=true" +
                "&Fields=Overview,UserData,SeriesName,ImageTags,BackdropImageTags,IndexNumber,ParentIndexNumber,SeriesId,RunTimeTicks" +
                "&SortBy=SortName" +
                $"&UserId={UserId}";

            var json = await GetAsync(url);

            using var doc = JsonDocument.Parse(json);
            var items = doc.RootElement.GetProperty("Items");

            return ParseMediaItems(items);
        }

        public async Task<List<JellyfinMovie>> GetSeasonsAsync(string seriesId)
        {
            var url =
                $"/Users/{UserId}/Items?ParentId={seriesId}" +
                "&IncludeItemTypes=Season" +
                "&Recursive=false" +
                "&Fields=Overview,UserData,ImageTags,BackdropImageTags,IndexNumber,ParentIndexNumber,RunTimeTicks" +
                "&SortBy=IndexNumber" +
                $"&UserId={UserId}";

            var json = await GetAsync(url);

            using var doc = JsonDocument.Parse(json);
            var items = doc.RootElement.GetProperty("Items");

            return ParseMediaItems(items);
        }

        public async Task<List<JellyfinMovie>> GetEpisodesAsync(string seasonId)
        {
            var url =
                $"/Users/{UserId}/Items?ParentId={seasonId}" +
                "&IncludeItemTypes=Episode" +
                "&Recursive=true" +
                "&Fields=Overview,UserData,SeriesName,ImageTags,BackdropImageTags,IndexNumber,ParentIndexNumber,SeriesId,RunTimeTicks" +
                "&SortBy=IndexNumber" +
                $"&UserId={UserId}";

            var json = await GetAsync(url);

            using var doc = JsonDocument.Parse(json);
            var items = doc.RootElement.GetProperty("Items");

            return ParseMediaItems(items);
        }

        public async Task<List<JellyfinMovie>> GetRecentlyAddedAsync(string libraryId, string includeItemTypes, int limit)
        {
            var url =
                $"/Users/{UserId}/Items?ParentId={libraryId}" +
                $"&IncludeItemTypes={includeItemTypes}" +
                "&Recursive=true" +
                "&Fields=Overview,UserData,SeriesName,ImageTags,BackdropImageTags,IndexNumber,ParentIndexNumber,SeriesId,RunTimeTicks" +
                "&SortBy=DateCreated" +
                "&SortOrder=Descending" +
                $"&Limit={limit}" +
                $"&UserId={UserId}";

            var json = await GetAsync(url);

            using var doc = JsonDocument.Parse(json);
            var items = doc.RootElement.GetProperty("Items");

            return ParseMediaItems(items);
        }

        public async Task<List<JellyfinMovie>> GetNextUpAsync(string tvLibraryId, int limit)
        {
            var url =
                $"/Shows/NextUp?UserId={UserId}" +
                $"&ParentId={tvLibraryId}" +
                $"&Limit={limit}" +
                "&Fields=Overview,SeriesName,UserData,ImageTags,BackdropImageTags,IndexNumber,ParentIndexNumber,SeriesId,RunTimeTicks";

            var json = await GetAsync(url);

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Items", out var items))
                return new List<JellyfinMovie>();

            return ParseMediaItems(items);
        }

        public async Task<List<JellyfinMovie>> GetContinueWatchingAsync(int limit)
        {
            var url =
                $"/Users/{UserId}/Items?Filters=IsResumable" +
                "&IncludeItemTypes=Movie,Episode" +
                "&Recursive=true" +
                "&SortBy=DatePlayed" +
                "&SortOrder=Descending" +
                $"&Limit={limit}" +
                "&Fields=Overview,UserData,SeriesName,ImageTags,BackdropImageTags,IndexNumber,ParentIndexNumber,SeriesId,RunTimeTicks";

            var json = await GetAsync(url);

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Items", out var items))
                return new List<JellyfinMovie>();

            return ParseMediaItems(items);
        }

        public async Task<string> GetRandomItemWithBackdropIdAsync(string libraryId, string includeItemTypes)
        {
            var url =
                $"/Users/{UserId}/Items?ParentId={libraryId}" +
                $"&IncludeItemTypes={includeItemTypes}" +
                "&Recursive=true" +
                "&Fields=BackdropImageTags" + // Request backdrop info
                "&SortBy=Random" +
                "&Limit=20" + // Get a few to choose from
                $"&UserId={UserId}";

            var json = await GetAsync(url);

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Items", out var items))
                return null;

            foreach (var item in items.EnumerateArray())
            {
                if (item.TryGetProperty("BackdropImageTags", out var backdropTags) &&
                    backdropTags.ValueKind == JsonValueKind.Array &&
                    backdropTags.GetArrayLength() > 0)
                {
                    return item.GetProperty("Id").GetString();
                }
            }

            // Fallback: return first random item if no backdrop found in the sample
            foreach (var item in items.EnumerateArray())
            {
                return item.GetProperty("Id").GetString();
            }

            return null;
        }

        private List<JellyfinMovie> ParseMediaItems(JsonElement items)
        {
            var results = new List<JellyfinMovie>();

            foreach (var item in items.EnumerateArray())
            {
                var hasPrimary = false;
                var hasThumb = false;
                var hasBackdrop = false;

                if (item.TryGetProperty("ImageTags", out var imageTags) &&
                    imageTags.ValueKind == JsonValueKind.Object)
                {
                    if (imageTags.TryGetProperty("Primary", out _))
                        hasPrimary = true;

                    if (imageTags.TryGetProperty("Thumb", out _))
                        hasThumb = true;
                }

                if (item.TryGetProperty("BackdropImageTags", out var backdropTags) &&
                    backdropTags.ValueKind == JsonValueKind.Array &&
                    backdropTags.GetArrayLength() > 0)
                {
                    hasBackdrop = true;
                }

                results.Add(new JellyfinMovie
                {
                    Id = item.GetProperty("Id").GetString(),
                    Name = item.GetProperty("Name").GetString(),
                    Overview = item.TryGetProperty("Overview", out var o)
                        ? o.GetString()
                        : string.Empty,
                    PlaybackPositionTicks = item.TryGetProperty("UserData", out var userData) &&
                                             userData.TryGetProperty("PlaybackPositionTicks", out var ticks)
                        ? ticks.GetInt64()
                        : 0,
                    RunTimeTicks = item.TryGetProperty("RunTimeTicks", out var rt)
                        ? rt.GetInt64()
                        : 0,
                    SeriesName = item.TryGetProperty("SeriesName", out var s)
                        ? s.GetString()
                        : string.Empty,
                    ItemType = item.TryGetProperty("Type", out var t)
                        ? t.GetString()
                        : string.Empty,
                    SeriesId = item.TryGetProperty("SeriesId", out var si)
                        ? si.GetString()
                        : string.Empty,
                    IndexNumber = item.TryGetProperty("IndexNumber", out var i)
                        ? i.GetInt32()
                        : 0,
                    ParentIndexNumber = item.TryGetProperty("ParentIndexNumber", out var pi)
                        ? pi.GetInt32()
                        : 0,
                    HasPrimary = hasPrimary,
                    HasThumb = hasThumb,
                    HasBackdrop = hasBackdrop
                });
            }

            return results;
        }

        public async Task UpdatePlaybackPositionAsync(string itemId, long positionTicks)
        {
            var body = new
            {
                PlaybackPositionTicks = positionTicks
            };

            await PostAsync($"/Users/{UserId}/Items/{itemId}/UserData", body);
        }

        public async Task MarkAsPlayedAsync(string itemId)
        {
            await PostAsync($"/Users/{UserId}/PlayedItems/{itemId}");
        }

        public async Task<bool> GetIsAnamorphicAsync(string itemId)
        {
            var json = await GetAsync($"/Items/{itemId}?Fields=MediaSources");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("MediaSources", out var mediaSources) &&
                mediaSources.ValueKind == JsonValueKind.Array)
            {
                foreach (var source in mediaSources.EnumerateArray())
                {
                    if (source.TryGetProperty("IsAnamorphic", out var isAna) &&
                        isAna.ValueKind == JsonValueKind.True)
                    {
                        return true;
                    }

                    if (source.TryGetProperty("MediaStreams", out var streams) &&
                        streams.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var stream in streams.EnumerateArray())
                        {
                            if (stream.TryGetProperty("Type", out var type) &&
                                type.GetString() == "Video" &&
                                stream.TryGetProperty("IsAnamorphic", out var streamIsAna) &&
                                streamIsAna.ValueKind == JsonValueKind.True)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

    }
}
