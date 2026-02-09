using System;
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

            // Remove OLD headers to prevent duplicates
            if (_http.DefaultRequestHeaders.Contains("X-MediaBrowser-Token"))
                _http.DefaultRequestHeaders.Remove("X-MediaBrowser-Token");
            
            if (_http.DefaultRequestHeaders.Contains("X-Emby-Authorization"))
                _http.DefaultRequestHeaders.Remove("X-Emby-Authorization");

            // ADD NEW Consolidated Header (The Standard Way)
            // This combines Client, Device, Version, AND Token into one string.
            _http.DefaultRequestHeaders.Add(
                "X-Emby-Authorization",
                $"MediaBrowser Client=\"JellyfinTizen\", Device=\"SamsungTV\", DeviceId=\"tizen-tv\", Version=\"1.0\", Token=\"{token}\""
            );
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
            var options = new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            };
            
            var json = JsonSerializer.Serialize(body, options);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync(ServerUrl + path, content);
            
            if (!response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"HTTP {response.StatusCode}: {responseContent} for path {path}");
                
                var httpEx = new HttpRequestException($"HTTP {response.StatusCode}: {responseContent}");
                httpEx.Data["ResponseContent"] = responseContent;
                throw httpEx;
            }
            
            return await response.Content.ReadAsStringAsync();
        }

        public async Task PostAsync(string path)
        {
            var response = await _http.PostAsync(ServerUrl + path, null);
            
            if (!response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"HTTP {response.StatusCode}: {responseContent} for path {path}");
                
                var httpEx = new HttpRequestException($"HTTP {response.StatusCode}: {responseContent}");
                httpEx.Data["ResponseContent"] = responseContent;
                throw httpEx;
            }
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

        public async Task<List<MediaStream>> GetSubtitleStreamsAsync(string itemId)
        {
            var json = await GetAsync($"/Users/{UserId}/Items/{itemId}?Fields=MediaStreams");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var list = new List<MediaStream>();

            if (root.TryGetProperty("MediaStreams", out var streams))
            {
                foreach (var stream in streams.EnumerateArray())
                {
                    var type = stream.TryGetProperty("Type", out var t) ? t.GetString() : "";
                    if (type == "Subtitle")
                    {
                        list.Add(new MediaStream
                        {
                            Index = stream.GetProperty("Index").GetInt32(),
                            Type = type,
                            Language = stream.TryGetProperty("Language", out var l) ? l.GetString() : "Unknown",
                            DisplayTitle = stream.TryGetProperty("DisplayTitle", out var d) ? d.GetString() : "Unknown",
                            Codec = stream.TryGetProperty("Codec", out var c) ? c.GetString() : null,
                            IsExternal = stream.TryGetProperty("IsExternal", out var e) && e.GetBoolean()
                        });
                    }
                }
            }
            return list;
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

        public async Task PostCapabilitiesAsync()
        {
            try
            {
                // Use the Builder to get the clean profile
                var profile = ProfileBuilder.BuildTizenProfile();
                
                // The server expects a root "Capabilities" property
                var caps = new
                {
                    Capabilities = new ClientCapabilitiesDto
                    {
                        DeviceProfile = profile,
                        SupportedCommands = new List<string> { "Play", "Browse", "GoHome", "GoBack" }
                    }
                };

                // Log the capabilities being sent for debugging
                var options = new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                };
                var json = JsonSerializer.Serialize(caps, options);
                Console.WriteLine("Sending capabilities: " + json);

                // This URL tells Jellyfin "Here is what I can do"
                await PostAsync("/Sessions/Capabilities/Full", caps);
                Console.WriteLine("Successfully sent capabilities");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send capabilities: {ex.Message}");
                if (ex is HttpRequestException httpEx)
                {
                    Console.WriteLine($"HTTP Status: {httpEx.StatusCode}");
                    if (httpEx.Data.Contains("ResponseContent"))
                    {
                        Console.WriteLine($"Response content: {httpEx.Data["ResponseContent"]}");
                    }
                }
                // Rethrow to allow caller to handle if needed
                throw;
            }
        }

        public async Task ReportPlaybackStartAsync(PlaybackProgressInfo info)
        {
            info.EventName = "TimeUpdate";
            await PostAsync("/Sessions/Playing", info);
        }

        public async Task ReportPlaybackProgressAsync(PlaybackProgressInfo info)
        {
            info.EventName = "TimeUpdate";
            await PostAsync("/Sessions/Playing/Progress", info);
        }

        public async Task ReportPlaybackStoppedAsync(PlaybackProgressInfo info)
        {
            info.EventName = "Stop";
            await PostAsync("/Sessions/Playing/Stopped", info);
        }

        public async Task<PlaybackInfoResponse> GetPlaybackInfoAsync(string itemId, int? subtitleStreamIndex = null, bool forceBurnIn = false)
        {
            // We send the UserId and DeviceProfile so the server knows who is asking and what we can play
            var body = new 
            { 
                UserId = UserId,
                AutoOpenLiveStream = true,
                DeviceProfile = ProfileBuilder.BuildTizenProfile(forceBurnIn),
                // Always include subtitle stream index, even if null (will be handled properly by server)
                SubtitleStreamIndex = subtitleStreamIndex
            };

            var json = await PostAsync($"/Items/{itemId}/PlaybackInfo", body);
            
            // Simple deserialization
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            var response = new PlaybackInfoResponse
            {
                PlaySessionId = root.GetProperty("PlaySessionId").GetString(),
                MediaSources = new List<MediaSourceInfo>()
            };

            if (root.TryGetProperty("MediaSources", out var sources))
            {
                foreach (var src in sources.EnumerateArray())
                {
                    var ms = new MediaSourceInfo
                    {
                        Id = src.GetProperty("Id").GetString(),
                        SupportsDirectPlay = src.GetProperty("SupportsDirectPlay").GetBoolean(),
                        SupportsTranscoding = src.GetProperty("SupportsTranscoding").GetBoolean(),
                        // TranscodingUrl is only present if transcoding is needed/possible
                        TranscodingUrl = src.TryGetProperty("TranscodingUrl", out var tUrl) ? tUrl.GetString() : null,
                        MediaStreams = new List<MediaStream>()
                    };

                    if (src.TryGetProperty("MediaStreams", out var streams))
                    {
                        foreach (var s in streams.EnumerateArray())
                        {
                            var mediaStream = new MediaStream
                            {
                                Index = s.GetProperty("Index").GetInt32(),
                                Type = s.GetProperty("Type").GetString(),
                                Language = s.TryGetProperty("Language", out var l) ? l.GetString() : null,
                                DisplayTitle = s.TryGetProperty("DisplayTitle", out var d) ? d.GetString() : null,
                                Codec = s.TryGetProperty("Codec", out var c) ? c.GetString() : null,
                                IsExternal = s.TryGetProperty("IsExternal", out var e) && e.GetBoolean()
                            };
                            
                            // Log all subtitle streams for debugging
                            if (mediaStream.Type == "Subtitle")
                            {
                                Console.WriteLine($"Found subtitle stream: Index={mediaStream.Index}, External={mediaStream.IsExternal}, Codec={mediaStream.Codec}, Language={mediaStream.Language}, DisplayTitle={mediaStream.DisplayTitle}");
                            }
                            
                            ms.MediaStreams.Add(mediaStream);
                        }
                    }
                    response.MediaSources.Add(ms);
                }
            }

            return response;
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