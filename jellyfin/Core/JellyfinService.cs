using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
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
                "MediaBrowser Client=\"Jellyfin for Tizen\", Device=\"Samsung TV\", DeviceId=\"tizen-tv\", Version=\"1.0\""
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
                $"MediaBrowser Client=\"Jellyfin for Tizen\", Device=\"Samsung TV\", DeviceId=\"tizen-tv\", Version=\"1.0\", Token=\"{token}\""
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

        public async Task<(string userId, string username)> GetCurrentUserAsync()
        {
            var json = await GetAsync("/Users/Me");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var userId = root.TryGetProperty("Id", out var idProp)
                ? idProp.GetString()
                : null;
            var username = root.TryGetProperty("Name", out var nameProp)
                ? nameProp.GetString()
                : null;

            return (userId, username);
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
                "&Fields=Overview,UserData,SeriesName,ImageTags,BackdropImageTags,IndexNumber,ParentIndexNumber,SeriesId,RunTimeTicks,ProductionYear,OfficialRating,CommunityRating" +
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
                "&Fields=Overview,UserData,ImageTags,BackdropImageTags,IndexNumber,ParentIndexNumber,RunTimeTicks,ProductionYear,OfficialRating,CommunityRating" +
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
                "&Fields=Overview,UserData,SeriesName,ImageTags,BackdropImageTags,IndexNumber,ParentIndexNumber,SeriesId,RunTimeTicks,ProductionYear,OfficialRating,CommunityRating" +
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
                "&Fields=Overview,UserData,SeriesName,ImageTags,BackdropImageTags,IndexNumber,ParentIndexNumber,SeriesId,RunTimeTicks,ProductionYear,OfficialRating,CommunityRating" +
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
                "&Fields=Overview,SeriesName,UserData,ImageTags,BackdropImageTags,IndexNumber,ParentIndexNumber,SeriesId,RunTimeTicks,ProductionYear,OfficialRating,CommunityRating";

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
                "&Fields=Overview,UserData,SeriesName,ImageTags,BackdropImageTags,IndexNumber,ParentIndexNumber,SeriesId,RunTimeTicks,ProductionYear,OfficialRating,CommunityRating";

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
                var hasLogo = false;

                if (item.TryGetProperty("ImageTags", out var imageTags) &&
                    imageTags.ValueKind == JsonValueKind.Object)
                {
                    if (imageTags.TryGetProperty("Primary", out _))
                        hasPrimary = true;

                    if (imageTags.TryGetProperty("Thumb", out _))
                        hasThumb = true;

                    if (imageTags.TryGetProperty("Logo", out _))
                        hasLogo = true;
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
                    ProductionYear = item.TryGetProperty("ProductionYear", out var py) && py.TryGetInt32(out var productionYear)
                        ? productionYear
                        : 0,
                    OfficialRating = item.TryGetProperty("OfficialRating", out var officialRating)
                        ? officialRating.GetString()
                        : string.Empty,
                    CommunityRating = item.TryGetProperty("CommunityRating", out var communityRating) && communityRating.ValueKind == JsonValueKind.Number
                        ? communityRating.GetDouble()
                        : null,
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
                    HasBackdrop = hasBackdrop,
                    HasLogo = hasLogo
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
                            DeliveryUrl = stream.TryGetProperty("DeliveryUrl", out var du) ? du.GetString() : null,
                            IsExternal = stream.TryGetProperty("IsExternal", out var e) && e.GetBoolean(),
                            Width = null,
                            Height = null,
                            VideoRange = null,
                            Channels = null,
                            ChannelLayout = null
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

            // This URL tells Jellyfin "Here is what we can do"
            await PostAsync("/Sessions/Capabilities/Full", caps);
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
            bool hasExplicitSubtitleSelection = subtitleStreamIndex.HasValue && subtitleStreamIndex.Value >= 0;
            bool forceServerTranscode = forceBurnIn && hasExplicitSubtitleSelection;
            int? effectiveSubtitleStreamIndex = forceBurnIn && !hasExplicitSubtitleSelection
                ? -1
                : subtitleStreamIndex;

            // We send the UserId and DeviceProfile so the server knows who is asking and what we can play
            var body = new 
            { 
                UserId = UserId,
                AutoOpenLiveStream = true,
                DeviceProfile = ProfileBuilder.BuildTizenProfile(forceBurnIn),
                EnableDirectPlay = !forceServerTranscode,
                EnableDirectStream = !forceServerTranscode,
                EnableTranscoding = true,
                // Always include subtitle stream index, even if null (will be handled properly by server)
                SubtitleStreamIndex = effectiveSubtitleStreamIndex
            };

            var json = await PostAsync($"/Items/{itemId}/PlaybackInfo", body);
            
            // Simple deserialization
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            var response = new PlaybackInfoResponse
            {
                PlaySessionId = root.TryGetProperty("PlaySessionId", out var playSessionIdProp)
                    ? playSessionIdProp.GetString()
                    : null,
                MediaSources = new List<MediaSourceInfo>()
            };

            if (root.TryGetProperty("MediaSources", out var sources) && sources.ValueKind == JsonValueKind.Array)
            {
                foreach (var src in sources.EnumerateArray())
                {
                    var ms = new MediaSourceInfo
                    {
                        Id = TryGetString(src, "Id") ?? itemId,
                        Name = TryGetString(src, "Name"),
                        SupportsDirectPlay = TryGetBool(src, "SupportsDirectPlay", out var supportsDirectPlay) && supportsDirectPlay,
                        SupportsTranscoding = TryGetBool(src, "SupportsTranscoding", out var supportsTranscoding) && supportsTranscoding,
                        // TranscodingUrl is only present if transcoding is needed/possible
                        TranscodingUrl = TryGetString(src, "TranscodingUrl"),
                        MediaStreams = new List<MediaStream>()
                    };

                    if (src.TryGetProperty("MediaStreams", out var streams) && streams.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var s in streams.EnumerateArray())
                        {
                            var type = TryGetString(s, "Type");
                            if (string.IsNullOrWhiteSpace(type))
                                continue;

                            var mediaStream = new MediaStream
                            {
                                Index = TryGetInt32(s, "Index", out var indexValue) ? indexValue : -1,
                                Type = type,
                                Language = TryGetString(s, "Language"),
                                DisplayTitle = TryGetString(s, "DisplayTitle"),
                                Codec = TryGetString(s, "Codec"),
                                DeliveryUrl = TryGetString(s, "DeliveryUrl"),
                                IsExternal = s.TryGetProperty("IsExternal", out var e) && e.GetBoolean(),
                                Width = s.TryGetProperty("Width", out var widthProp) && widthProp.TryGetInt32(out var widthValue)
                                    ? widthValue
                                    : null,
                                Height = s.TryGetProperty("Height", out var heightProp) && heightProp.TryGetInt32(out var heightValue)
                                    ? heightValue
                                    : null,
                                VideoRange = s.TryGetProperty("VideoRange", out var videoRangeProp)
                                    ? videoRangeProp.GetString()
                                    : null,
                                Channels = s.TryGetProperty("Channels", out var channelsProp) && channelsProp.TryGetInt32(out var channelsValue)
                                    ? channelsValue
                                    : null,
                                ChannelLayout = s.TryGetProperty("ChannelLayout", out var channelLayoutProp)
                                    ? channelLayoutProp.GetString()
                                    : null
                            };
                            
                            ms.MediaStreams.Add(mediaStream);
                        }
                    }
                    response.MediaSources.Add(ms);
                }
            }

            return response;
        }

        public async Task<TrickplayInfo> GetTrickplayInfoAsync(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            var json = await GetAsync($"/Items/{itemId}?Fields=Trickplay");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("Trickplay", out var trickplayRoot) ||
                trickplayRoot.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var variants = new List<TrickplayInfo>();
            foreach (var profile in trickplayRoot.EnumerateObject())
            {
                if (TryParseTrickplayVariant(profile.Value, out var directVariant))
                {
                    variants.Add(directVariant);
                    continue;
                }

                if (profile.Value.ValueKind != JsonValueKind.Object)
                    continue;

                foreach (var nested in profile.Value.EnumerateObject())
                {
                    if (TryParseTrickplayVariant(nested.Value, out var nestedVariant))
                        variants.Add(nestedVariant);
                }
            }

            if (variants.Count == 0)
                return null;

            var preferred = variants
                .Where(v => v.Width >= 240 && v.Width <= 640)
                .OrderBy(v => Math.Abs(v.Width - 320))
                .FirstOrDefault();

            return preferred ?? variants.OrderBy(v => v.Width).FirstOrDefault();
        }

        private static bool TryParseTrickplayVariant(JsonElement element, out TrickplayInfo variant)
        {
            variant = null;
            if (element.ValueKind != JsonValueKind.Object)
                return false;

            if (!TryGetInt32Loose(element, "Width", out var width) || width <= 0)
                return false;

            if (!TryGetInt32Loose(element, "Height", out var height))
                height = 0;

            if (!TryGetInt32Loose(element, "TileWidth", out var tileWidth) || tileWidth <= 0)
                return false;

            if (!TryGetInt32Loose(element, "TileHeight", out var tileHeight) || tileHeight <= 0)
                return false;

            if (!TryGetInt32Loose(element, "Interval", out var intervalMs) || intervalMs <= 0)
                return false;

            TryGetInt32Loose(element, "ThumbnailCount", out var thumbnailCount);
            TryGetInt32Loose(element, "Bandwidth", out var bandwidth);

            variant = new TrickplayInfo
            {
                Width = width,
                Height = height,
                TileWidth = tileWidth,
                TileHeight = tileHeight,
                ThumbnailCount = thumbnailCount,
                IntervalMs = intervalMs,
                Bandwidth = bandwidth
            };

            return true;
        }

        private static string TryGetString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var prop))
                return null;

            if (prop.ValueKind != JsonValueKind.String)
                return null;

            return prop.GetString();
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
                    if (TryGetBool(source, "IsAnamorphic", out var sourceAnamorphic) && sourceAnamorphic)
                    {
                        return true;
                    }

                    var sourceAspect = TryGetAspectRatio(source, "AspectRatio");

                    if (source.TryGetProperty("MediaStreams", out var streams) &&
                        streams.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var stream in streams.EnumerateArray())
                        {
                            if (stream.TryGetProperty("Type", out var type) &&
                                type.GetString() == "Video")
                            {
                                if (TryGetBool(stream, "IsAnamorphic", out var streamAnamorphic) && streamAnamorphic)
                                    return true;

                                if (TryGetInt32(stream, "Width", out var width) &&
                                    TryGetInt32(stream, "Height", out var height) &&
                                    width > 0 &&
                                    height > 0)
                                {
                                    var storageAspect = (double)width / height;
                                    var displayAspect = TryGetAspectRatio(stream, "AspectRatio") ?? sourceAspect;

                                    if (displayAspect.HasValue)
                                    {
                                        // If display aspect meaningfully differs from stored pixel aspect,
                                        // this is anamorphic-like behavior even if explicit flag is missing.
                                        if (Math.Abs(displayAspect.Value - storageAspect) > 0.04)
                                            return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static bool TryGetBool(JsonElement element, string propertyName, out bool value)
        {
            value = false;
            if (!element.TryGetProperty(propertyName, out var prop))
                return false;
            if (prop.ValueKind != JsonValueKind.True && prop.ValueKind != JsonValueKind.False)
                return false;

            value = prop.GetBoolean();
            return true;
        }

        private static bool TryGetInt32(JsonElement element, string propertyName, out int value)
        {
            value = 0;
            if (!element.TryGetProperty(propertyName, out var prop))
                return false;
            if (prop.ValueKind != JsonValueKind.Number)
                return false;

            return prop.TryGetInt32(out value);
        }

        private static bool TryGetInt32Loose(JsonElement element, string propertyName, out int value)
        {
            value = 0;
            if (!element.TryGetProperty(propertyName, out var prop))
                return false;

            if (prop.ValueKind == JsonValueKind.Number)
                return prop.TryGetInt32(out value);

            if (prop.ValueKind == JsonValueKind.String)
                return int.TryParse(prop.GetString(), out value);

            return false;
        }

        private static double? TryGetAspectRatio(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var prop))
                return null;

            if (prop.ValueKind != JsonValueKind.String)
                return null;

            var text = prop.GetString();
            if (string.IsNullOrWhiteSpace(text))
                return null;

            return ParseAspectRatio(text.Trim());
        }

        private static double? ParseAspectRatio(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            text = text.Trim();

            int sep = text.IndexOf(':');
            if (sep < 0)
                sep = text.IndexOf('/');

            if (sep > 0 && sep < text.Length - 1)
            {
                var left = text.Substring(0, sep).Trim();
                var right = text.Substring(sep + 1).Trim();

                if (double.TryParse(left, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var a) &&
                    double.TryParse(right, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var b) &&
                    b > 0)
                {
                    return a / b;
                }
            }

            if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ratio) &&
                ratio > 0)
            {
                return ratio;
            }

            return null;
        }

    }

}
