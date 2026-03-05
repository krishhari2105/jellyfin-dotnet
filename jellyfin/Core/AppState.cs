using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace JellyfinTizen.Core
{
    public static class AppState
    {
        private const string KeyServerUrl = "jf_server_url";
        private const string KeyAccessToken = "jf_access_token";
        private const string KeyUserId = "jf_user_id";
        private const string KeyUsername = "jf_username";
        private const string KeyDeviceId = "jf_device_id";
        private const string KeyBurnInSubtitles = "jf_burn_in_subtitles";
        private const string KeyServersRegistry = "jf_servers_registry_v1";
        private const string KeyActiveServerUrl = "jf_active_server_url";
        private const int MaxStoredServersCount = 4;

        private static readonly object ServerRegistryLock = new();
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private static readonly List<StoredServerRecord> StoredServers = new();
        private static bool _serversLoaded;

        public static string ServerUrl { get; set; }
        public static string AccessToken { get; set; }
        public static string UserId { get; set; }
        public static string Username { get; set; }
        public static string DeviceId { get; private set; }

        public static JellyfinService Jellyfin { get; private set; }
        public static int MaxStoredServers => MaxStoredServersCount;

        public sealed class StoredServer
        {
            public string Url { get; init; }
            public string DisplayName { get; init; }
            public string HostLabel { get; init; }
            public string Username { get; init; }
            public bool HasSavedSession { get; init; }
            public bool IsActive { get; init; }
        }

        private sealed class StoredServerRecord
        {
            public string Url { get; set; }
            public string DisplayName { get; set; }
            public string AccessToken { get; set; }
            public string UserId { get; set; }
            public string Username { get; set; }
            public long LastUsedUtcTicks { get; set; }
            public bool IsActive { get; set; }
        }

        public static bool BurnInSubtitles
        {
            get
            {
                if (Tizen.Applications.Preference.Contains(KeyBurnInSubtitles))
                    return Tizen.Applications.Preference.Get<bool>(KeyBurnInSubtitles);
                return false;
            }
            set => Tizen.Applications.Preference.Set(KeyBurnInSubtitles, value);
        }

        public static void Init()
        {
            Jellyfin = new JellyfinService();
            DeviceId = GetOrCreateDeviceId();
            Jellyfin.DeviceId = DeviceId;
            EnsureServersLoaded();
        }

        public static IReadOnlyList<StoredServer> GetStoredServers()
        {
            EnsureServersLoaded();

            lock (ServerRegistryLock)
            {
                return StoredServers
                    .OrderByDescending(s => s.LastUsedUtcTicks)
                    .Select(s => new StoredServer
                    {
                        Url = s.Url,
                        DisplayName = ResolveServerDisplayName(s),
                        HostLabel = BuildHostLabel(s.Url),
                        Username = s.Username,
                        HasSavedSession = HasSession(s),
                        IsActive = s.IsActive
                    })
                    .ToList();
            }
        }

        public static bool HasStoredServers()
        {
            EnsureServersLoaded();
            lock (ServerRegistryLock)
            {
                return StoredServers.Count > 0;
            }
        }

        public static bool CanAddServer()
        {
            EnsureServersLoaded();
            lock (ServerRegistryLock)
            {
                return StoredServers.Count < MaxStoredServersCount;
            }
        }

        public static bool CanStoreServer(string serverUrl)
        {
            var normalized = NormalizeServerUrl(serverUrl);
            if (string.IsNullOrWhiteSpace(normalized))
                return false;

            EnsureServersLoaded();
            lock (ServerRegistryLock)
            {
                if (FindRecordByUrlInternal(normalized) != null)
                    return true;

                return StoredServers.Count < MaxStoredServersCount;
            }
        }

        public static bool ActivateServer(string serverUrl, bool includeSession)
        {
            var normalized = NormalizeServerUrl(serverUrl);
            if (string.IsNullOrWhiteSpace(normalized))
                return false;

            EnsureServersLoaded();
            lock (ServerRegistryLock)
            {
                var record = FindRecordByUrlInternal(normalized);
                if (record == null)
                    return false;

                SetActiveRecordInternal(record);
                ApplyRecordToRuntimeInternal(record, includeSession);
                PersistServerRegistryInternal();
                return true;
            }
        }

        public static bool TrySaveServer(string serverUrl, string displayName = null)
        {
            var normalized = NormalizeServerUrl(serverUrl);
            if (string.IsNullOrWhiteSpace(normalized))
                return false;

            EnsureServersLoaded();
            lock (ServerRegistryLock)
            {
                var record = FindRecordByUrlInternal(normalized);
                if (record == null)
                {
                    if (StoredServers.Count >= MaxStoredServersCount)
                        return false;

                    record = new StoredServerRecord
                    {
                        Url = normalized
                    };
                    StoredServers.Add(record);
                }

                var normalizedName = NormalizeServerName(displayName);
                if (!string.IsNullOrWhiteSpace(normalizedName))
                    record.DisplayName = normalizedName;

                record.LastUsedUtcTicks = DateTime.UtcNow.Ticks;
                SetActiveRecordInternal(record);
                ApplyRecordToRuntimeInternal(record, includeSession: false);
                PersistServerRegistryInternal();
                return true;
            }
        }

        public static bool RemoveServer(string serverUrl)
        {
            var normalized = NormalizeServerUrl(serverUrl);
            if (string.IsNullOrWhiteSpace(normalized))
                return false;

            EnsureServersLoaded();
            lock (ServerRegistryLock)
            {
                var record = FindRecordByUrlInternal(normalized);
                if (record == null)
                    return false;

                var removedActive = record.IsActive;
                StoredServers.Remove(record);

                NormalizeServerRegistryInternal();
                var nextActive = GetActiveRecordInternal();

                if (removedActive)
                {
                    if (nextActive != null)
                    {
                        ApplyRecordToRuntimeInternal(nextActive, includeSession: false);
                    }
                    else
                    {
                        ApplyRecordToRuntimeInternal(null, includeSession: false);
                    }
                }

                PersistServerRegistryInternal();
                return true;
            }
        }

        public static bool TrySaveSession(string serverUrl, string accessToken, string userId, string username)
        {
            var normalized = NormalizeServerUrl(serverUrl);
            if (string.IsNullOrWhiteSpace(normalized) ||
                string.IsNullOrWhiteSpace(accessToken) ||
                string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            EnsureServersLoaded();
            lock (ServerRegistryLock)
            {
                var record = FindRecordByUrlInternal(normalized);
                if (record == null)
                {
                    if (StoredServers.Count >= MaxStoredServersCount)
                        return false;

                    record = new StoredServerRecord
                    {
                        Url = normalized
                    };
                    StoredServers.Add(record);
                }

                record.AccessToken = accessToken.Trim();
                record.UserId = userId.Trim();
                record.Username = username?.Trim() ?? string.Empty;
                record.LastUsedUtcTicks = DateTime.UtcNow.Ticks;

                SetActiveRecordInternal(record);
                ApplyRecordToRuntimeInternal(record, includeSession: true);
                PersistServerRegistryInternal();
                return true;
            }
        }

        public static bool TryRestoreFullSession()
        {
            EnsureServersLoaded();
            lock (ServerRegistryLock)
            {
                var active = GetActiveRecordInternal();
                if (active == null || !HasSession(active))
                    return false;

                SetActiveRecordInternal(active);
                ApplyRecordToRuntimeInternal(active, includeSession: true);
                PersistServerRegistryInternal();
                return true;
            }
        }

        public static bool TryRestoreServer()
        {
            EnsureServersLoaded();
            lock (ServerRegistryLock)
            {
                var active = GetActiveRecordInternal();
                if (active == null)
                    return false;

                SetActiveRecordInternal(active);
                ApplyRecordToRuntimeInternal(active, includeSession: false);
                PersistServerRegistryInternal();
                return true;
            }
        }

        public static void SaveServer(string serverUrl)
        {
            _ = TrySaveServer(serverUrl);
        }

        public static void SaveSession(string serverUrl, string accessToken, string userId, string username)
        {
            _ = TrySaveSession(serverUrl, accessToken, userId, username);
        }

        public static void ClearSession(bool clearServer)
        {
            EnsureServersLoaded();

            lock (ServerRegistryLock)
            {
                AccessToken = null;
                UserId = null;
                Username = null;

                Jellyfin?.ClearAuthToken();
                Jellyfin?.SetUserId(null);

                var active = GetActiveRecordInternal();
                if (active != null)
                {
                    if (clearServer)
                    {
                        StoredServers.Remove(active);
                    }
                    else
                    {
                        active.AccessToken = null;
                        active.UserId = null;
                        active.Username = null;
                        active.LastUsedUtcTicks = DateTime.UtcNow.Ticks;
                    }
                }

                NormalizeServerRegistryInternal();
                var nextActive = GetActiveRecordInternal();
                if (nextActive != null)
                {
                    ApplyRecordToRuntimeInternal(nextActive, includeSession: false);
                }
                else
                {
                    ServerUrl = null;
                }

                PersistServerRegistryInternal();
            }
        }

        private static void RemovePreferenceIfExists(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            try
            {
                if (Tizen.Applications.Preference.Contains(key))
                    Tizen.Applications.Preference.Remove(key);
            }
            catch
            {
                // Ignore preference storage races/corruption while clearing session.
            }
        }

        private static void EnsureServersLoaded()
        {
            lock (ServerRegistryLock)
            {
                if (_serversLoaded)
                    return;

                _serversLoaded = true;
                StoredServers.Clear();

                var serializedRegistry = GetPreferenceString(KeyServersRegistry);
                if (!string.IsNullOrWhiteSpace(serializedRegistry))
                {
                    try
                    {
                        var records = JsonSerializer.Deserialize<List<StoredServerRecord>>(
                            serializedRegistry,
                            JsonOptions
                        );

                        if (records != null)
                        {
                            foreach (var record in records)
                            {
                                if (record == null)
                                    continue;

                                var normalizedUrl = NormalizeServerUrl(record.Url);
                                if (string.IsNullOrWhiteSpace(normalizedUrl))
                                    continue;

                                StoredServers.Add(new StoredServerRecord
                                {
                                    Url = normalizedUrl,
                                    DisplayName = NormalizeServerName(record.DisplayName),
                                    AccessToken = SanitizeValue(record.AccessToken),
                                    UserId = SanitizeValue(record.UserId),
                                    Username = SanitizeValue(record.Username),
                                    LastUsedUtcTicks = record.LastUsedUtcTicks,
                                    IsActive = record.IsActive
                                });
                            }
                        }
                    }
                    catch
                    {
                        // Ignore malformed registry and fallback to legacy keys.
                    }
                }

                var legacyUrl = NormalizeServerUrl(GetPreferenceString(KeyServerUrl));
                if (!string.IsNullOrWhiteSpace(legacyUrl))
                {
                    var record = FindRecordByUrlInternal(legacyUrl);
                    if (record == null)
                    {
                        record = new StoredServerRecord
                        {
                            Url = legacyUrl
                        };
                        StoredServers.Add(record);
                    }

                    record.AccessToken = record.AccessToken ?? SanitizeValue(GetPreferenceString(KeyAccessToken));
                    record.UserId = record.UserId ?? SanitizeValue(GetPreferenceString(KeyUserId));
                    record.Username = record.Username ?? SanitizeValue(GetPreferenceString(KeyUsername));
                    if (record.LastUsedUtcTicks <= 0)
                        record.LastUsedUtcTicks = DateTime.UtcNow.Ticks;
                }

                var activeUrl = NormalizeServerUrl(GetPreferenceString(KeyActiveServerUrl)) ?? legacyUrl;
                if (!string.IsNullOrWhiteSpace(activeUrl))
                {
                    var activeRecord = FindRecordByUrlInternal(activeUrl);
                    if (activeRecord != null)
                        activeRecord.IsActive = true;
                }

                NormalizeServerRegistryInternal();
                PersistServerRegistryInternal();
            }
        }

        private static string BuildHostLabel(string serverUrl)
        {
            if (Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri))
            {
                var host = uri.IsDefaultPort
                    ? uri.Host
                    : $"{uri.Host}:{uri.Port}";

                var path = uri.AbsolutePath?.TrimEnd('/');
                if (!string.IsNullOrWhiteSpace(path) && path != "/")
                    host += path;

                return host;
            }

            return serverUrl ?? string.Empty;
        }

        private static string ResolveServerDisplayName(StoredServerRecord record)
        {
            if (record == null)
                return string.Empty;

            var name = NormalizeServerName(record.DisplayName);
            if (!string.IsNullOrWhiteSpace(name))
                return name;

            return BuildHostLabel(record.Url);
        }

        private static bool HasSession(StoredServerRecord record)
        {
            return record != null &&
                   !string.IsNullOrWhiteSpace(record.AccessToken) &&
                   !string.IsNullOrWhiteSpace(record.UserId);
        }

        private static string NormalizeServerUrl(string serverUrl)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
                return null;

            return serverUrl.Trim().TrimEnd('/');
        }

        private static string SanitizeValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value.Trim();
        }

        private static string NormalizeServerName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return null;

            var cleaned = displayName.Trim();
            return cleaned.Length == 0 ? null : cleaned;
        }

        private static string GetPreferenceString(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            try
            {
                if (!Tizen.Applications.Preference.Contains(key))
                    return null;

                return Tizen.Applications.Preference.Get<string>(key);
            }
            catch
            {
                return null;
            }
        }

        private static void SetPreferenceString(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                return;

            try
            {
                Tizen.Applications.Preference.Set(key, value);
            }
            catch
            {
                // Ignore preference write failures.
            }
        }

        private static StoredServerRecord FindRecordByUrlInternal(string serverUrl)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
                return null;

            return StoredServers.Find(s =>
                string.Equals(s.Url, serverUrl, StringComparison.OrdinalIgnoreCase)
            );
        }

        private static StoredServerRecord GetActiveRecordInternal()
        {
            var active = StoredServers.FirstOrDefault(s => s.IsActive);
            if (active != null)
                return active;

            return StoredServers
                .OrderByDescending(s => s.LastUsedUtcTicks)
                .FirstOrDefault();
        }

        private static void SetActiveRecordInternal(StoredServerRecord activeRecord)
        {
            foreach (var record in StoredServers)
                record.IsActive = false;

            if (activeRecord == null)
                return;

            activeRecord.IsActive = true;
            activeRecord.LastUsedUtcTicks = DateTime.UtcNow.Ticks;
        }

        private static void ApplyRecordToRuntimeInternal(StoredServerRecord record, bool includeSession)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.Url))
            {
                ServerUrl = null;
                AccessToken = null;
                UserId = null;
                Username = null;
                Jellyfin?.ClearAuthToken();
                Jellyfin?.SetUserId(null);
                return;
            }

            ServerUrl = record.Url;
            Jellyfin.Connect(record.Url);

            if (includeSession && HasSession(record))
            {
                AccessToken = record.AccessToken;
                UserId = record.UserId;
                Username = record.Username ?? string.Empty;
                Jellyfin.SetAuthToken(record.AccessToken);
                Jellyfin.SetUserId(record.UserId);
                return;
            }

            AccessToken = null;
            UserId = null;
            Username = null;
            Jellyfin.ClearAuthToken();
            Jellyfin.SetUserId(null);
        }

        private static void NormalizeServerRegistryInternal()
        {
            if (StoredServers.Count == 0)
                return;

            var deduped = new Dictionary<string, StoredServerRecord>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in StoredServers)
            {
                if (source == null)
                    continue;

                var normalizedUrl = NormalizeServerUrl(source.Url);
                if (string.IsNullOrWhiteSpace(normalizedUrl))
                    continue;

                if (!deduped.TryGetValue(normalizedUrl, out var existing))
                {
                    deduped[normalizedUrl] = new StoredServerRecord
                    {
                        Url = normalizedUrl,
                        DisplayName = NormalizeServerName(source.DisplayName),
                        AccessToken = SanitizeValue(source.AccessToken),
                        UserId = SanitizeValue(source.UserId),
                        Username = SanitizeValue(source.Username),
                        LastUsedUtcTicks = source.LastUsedUtcTicks > 0
                            ? source.LastUsedUtcTicks
                            : DateTime.UtcNow.Ticks,
                        IsActive = source.IsActive
                    };
                    continue;
                }

                existing.LastUsedUtcTicks = Math.Max(existing.LastUsedUtcTicks, source.LastUsedUtcTicks);
                existing.IsActive = existing.IsActive || source.IsActive;

                if (string.IsNullOrWhiteSpace(existing.AccessToken))
                    existing.AccessToken = SanitizeValue(source.AccessToken);
                if (string.IsNullOrWhiteSpace(existing.UserId))
                    existing.UserId = SanitizeValue(source.UserId);
                if (string.IsNullOrWhiteSpace(existing.Username))
                    existing.Username = SanitizeValue(source.Username);
                if (string.IsNullOrWhiteSpace(existing.DisplayName))
                    existing.DisplayName = NormalizeServerName(source.DisplayName);
            }

            StoredServers.Clear();
            StoredServers.AddRange(
                deduped.Values
                    .OrderByDescending(s => s.LastUsedUtcTicks)
                    .Take(MaxStoredServersCount)
            );

            if (StoredServers.Count == 0)
                return;

            var active = StoredServers
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.LastUsedUtcTicks)
                .FirstOrDefault();

            foreach (var record in StoredServers)
                record.IsActive = false;

            if (active == null)
                active = StoredServers[0];

            active.IsActive = true;
        }

        private static void PersistServerRegistryInternal()
        {
            NormalizeServerRegistryInternal();

            try
            {
                var json = JsonSerializer.Serialize(StoredServers, JsonOptions);
                SetPreferenceString(KeyServersRegistry, json);
            }
            catch
            {
                // Ignore persistence errors and keep runtime state.
            }

            var active = GetActiveRecordInternal();
            if (active == null)
            {
                RemovePreferenceIfExists(KeyActiveServerUrl);
                RemovePreferenceIfExists(KeyServerUrl);
                RemovePreferenceIfExists(KeyAccessToken);
                RemovePreferenceIfExists(KeyUserId);
                RemovePreferenceIfExists(KeyUsername);
                return;
            }

            SetPreferenceString(KeyActiveServerUrl, active.Url);
            SetPreferenceString(KeyServerUrl, active.Url);

            if (HasSession(active))
            {
                SetPreferenceString(KeyAccessToken, active.AccessToken);
                SetPreferenceString(KeyUserId, active.UserId);

                if (!string.IsNullOrWhiteSpace(active.Username))
                    SetPreferenceString(KeyUsername, active.Username);
                else
                    RemovePreferenceIfExists(KeyUsername);
            }
            else
            {
                RemovePreferenceIfExists(KeyAccessToken);
                RemovePreferenceIfExists(KeyUserId);
                RemovePreferenceIfExists(KeyUsername);
            }
        }

        private static string GetOrCreateDeviceId()
        {
            try
            {
                if (Tizen.Applications.Preference.Contains(KeyDeviceId))
                {
                    var existing = Tizen.Applications.Preference.Get<string>(KeyDeviceId);
                    if (!string.IsNullOrWhiteSpace(existing))
                        return existing;
                }
            }
            catch
            {
                // Continue and generate a new id when preference read fails.
            }

            var generated = "tizen-" + Guid.NewGuid().ToString("N");

            try
            {
                Tizen.Applications.Preference.Set(KeyDeviceId, generated);
            }
            catch
            {
                // Return generated id even if persistence fails.
            }

            return generated;
        }

        public static string GetUserAvatarUrl(int size = 96)
        {
            if (size <= 0)
                size = 96;

            if (string.IsNullOrWhiteSpace(ServerUrl) ||
                string.IsNullOrWhiteSpace(UserId) ||
                string.IsNullOrWhiteSpace(AccessToken))
            {
                return null;
            }

            var apiKey = Uri.EscapeDataString(AccessToken);
            return
                $"{ServerUrl.TrimEnd('/')}/Users/{UserId}/Images/Primary" +
                $"?width={size}&height={size}" +
                $"&quality=95&v=2&api_key={apiKey}";
        }

        public static string GetItemLogoUrl(string itemId, int maxWidth = 900, int quality = 90)
        {
            if (maxWidth <= 0)
                maxWidth = 900;
            if (quality <= 0)
                quality = 90;
            quality = Math.Clamp(quality, 30, 100);

            if (string.IsNullOrWhiteSpace(itemId) ||
                string.IsNullOrWhiteSpace(ServerUrl) ||
                string.IsNullOrWhiteSpace(AccessToken))
            {
                return null;
            }

            var apiKey = Uri.EscapeDataString(AccessToken);
            return
                $"{ServerUrl.TrimEnd('/')}/Items/{itemId}/Images/Logo/0" +
                $"?maxWidth={maxWidth}&quality={quality}&api_key={apiKey}";
        }
    }
}
