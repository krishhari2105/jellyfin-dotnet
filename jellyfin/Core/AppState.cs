using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using JellyfinTizen.Screens;

namespace JellyfinTizen.Core
{
    public static class AppState
    {
        public static Task TailscaleReadyTask { get; private set; }

        public static bool TailscaleStartupFailed { get; private set; } = false;
        private const string KeyServerUrl = "jf_server_url";
        private const string KeyAccessToken = "jf_access_token";
        private const string KeyUserId = "jf_user_id";
        private const string KeyUsername = "jf_username";
        private const string KeyDeviceId = "jf_device_id";
        private const string KeyServersRegistry = "jf_servers_registry_v1";
        private const string KeyActiveServerUrl = "jf_active_server_url";
        private const int MaxStoredServersCount = 4;
        private const int DefaultApiTimeoutSeconds = 20;

        private static readonly object ServerRegistryLock = new();
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private static readonly List<StoredServerRecord> StoredServers = new();
        private static bool _serversLoaded;

        public static string ServerUrl { get; set; }
        public static string AccessToken { get; set; }
        public static string UserId { get; set; }
        public static string Username { get; set; }
        public static string DeviceId { get; private set; }
        public static bool BurnInSubtitles { get; set; }
        public static bool ForceTsTranscoding { get; set; }

        public static JellyfinService Jellyfin { get; private set; }
        public static TailscaleService Tailscale { get; private set; }
        public static TailscaleProxyService TailscaleProxy { get; private set; }
        public static HttpClient HttpClient { get; private set; }
        public static int MaxStoredServers => MaxStoredServersCount;
        public static List<JellyfinTizen.Models.JellyfinUser> CachedPublicUsers { get; set; }
        public static TailscaleConnectionMonitor ConnectionMonitor { get; private set; }

        public sealed class StoredServer
        {
            public string Url { get; init; }
            public string DisplayName { get; init; }
            public string HostLabel { get; init; }
            public string Username { get; init; }
            public bool HasSavedSession { get; init; }
            public bool IsActive { get; init; }
            public bool IsEmby { get; init; }
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
            public bool IsEmby { get; set; }
        }

        public static void Init()
        {
            HttpClient = CreateHttpClient();
            Jellyfin = new JellyfinService(HttpClient);
            DeviceId = GetOrCreateDeviceId();
            Jellyfin.DeviceId = DeviceId;
            BurnInSubtitles = false;
            ForceTsTranscoding = false;

            // Create Tailscale service instance (UI will show it if binary is present)
            Tailscale = new TailscaleService();
            ConnectionMonitor = new TailscaleConnectionMonitor();

            // Initialize lifecycle state machine
            AppLifecycle.Transition(AppLifecycleState.NotStarted, AppLifecycleState.ProcessLaunch);
            AppLifecycle.Transition(AppLifecycleState.ProcessLaunch, AppLifecycleState.TailscaleStaging);

            // Start tailscaled in background - don't block app initialization
            // If it fails, the UI will show the error state
            TailscaleReadyTask = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await Tailscale.StageAndStart();
                    TailscaleStartupFailed = false;

                    _ = AppLifecycle.TryTransitionAsync(AppLifecycleState.TailscaleStaging, AppLifecycleState.TailscaleStagingSucceeded);

                    bool reachable = Tailscale.IsRunning || Tailscale.IsSocketReachable;
                    if (reachable)
                    {
                        // Wait for daemon to be ready to handle local API calls
                        try { await Tailscale.WaitForReadyAsync(); } catch { }

                        _ = AppLifecycle.TryTransitionAsync(AppLifecycleState.TailscaleStagingSucceeded, AppLifecycleState.ProxyStarting);

                        try
                        {
                            TailscaleProxyService.LocalProxyPort = TailscaleService.GetFreePort(8123);
                            TailscaleProxy = new TailscaleProxyService(HttpClient);
                            TailscaleProxy.Start();

                            bool listenerReady = await AppLifecycle.WaitForProxyListenerReadyAsync(
                                TailscaleProxyService.LocalProxyPort,
                                TimeSpan.FromSeconds(5));

                            if (listenerReady)
                            {
                                _ = AppLifecycle.TryTransitionAsync(AppLifecycleState.ProxyStarting, AppLifecycleState.ProxyListeningConfirmed);
                            }
                            else
                            {
                                Tizen.Log.Warn("AppState", "Tailscale proxy listener not ready after 5s");
                                TailscaleProxy = null;
                                _ = AppLifecycle.TryTransitionAsync(AppLifecycleState.ProxyStarting, AppLifecycleState.ProxyStartFailed);
                            }
                        }
                        catch (Exception ex)
                        {
                            Tizen.Log.Warn("AppState", $"Failed to start Tailscale proxy: {ex.Message}");
                            TailscaleProxy = null;
                            _ = AppLifecycle.TryTransitionAsync(AppLifecycleState.ProxyStarting, AppLifecycleState.ProxyStartFailed);
                        }
                    }
                    else
                    {
                        _ = AppLifecycle.TryTransitionAsync(AppLifecycleState.TailscaleStagingSucceeded, AppLifecycleState.TailscaleStagingFailed);
                    }
                }
                catch (Exception ex)
                {
                    Tizen.Log.Warn("AppState", $"Tailscale not available: {ex.Message}");
                    TailscaleStartupFailed = true;
                    _ = AppLifecycle.TryTransitionAsync(AppLifecycleState.TailscaleStaging, AppLifecycleState.TailscaleStagingFailed);
                }
            });

            EnsureServersLoaded();

            Jellyfin.UnauthorizedDetected += (s, e) =>
            {
                ClearSession(clearServer: false);
                Tizen.Applications.CoreApplication.Post(() =>
                {
                    NavigationService.Navigate(new StartupScreen(), addToStack: false);
                });
            };
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
                        IsActive = s.IsActive,
                        IsEmby = s.IsEmby
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

        public static bool TrySaveServer(string serverUrl, string displayName = null, bool isEmby = false)
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

                record.IsEmby = isEmby;

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
                !IsValidGuidValue(userId))
            {
                TailscaleDebugLog.Add("AppState.TrySaveSession rejected session with invalid user id.");
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
                CachedPublicUsers = null;

                Jellyfin?.ClearAuthToken();
                Jellyfin?.SetUserId(null);

                try
                {
                    TailscaleProxyService.ClearCache();
                    JellyfinTizen.Utils.CacheHelper.Clear();
                }
                catch { }

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
                                    IsActive = record.IsActive,
                                    IsEmby = record.IsEmby
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
                   IsValidGuidValue(record.UserId);
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
            Jellyfin.IsEmby = record.IsEmby;
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
                        IsActive = source.IsActive,
                        IsEmby = source.IsEmby
                    };
                    continue;
                }

                existing.LastUsedUtcTicks = Math.Max(existing.LastUsedUtcTicks, source.LastUsedUtcTicks);
                existing.IsActive = existing.IsActive || source.IsActive;
                existing.IsEmby = existing.IsEmby || source.IsEmby;

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
                    if (IsValidGuidValue(existing))
                        return existing;

                    TailscaleDebugLog.Add($"Replacing invalid Jellyfin device id: {existing}");
                }
            }
            catch
            {
                // Continue and generate a new id when preference read fails.
            }

            var generated = Guid.NewGuid().ToString("N");

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

        private static bool IsValidGuidValue(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && Guid.TryParse(value.Trim(), out _);
        }

        public static async Task<bool> IsTailscaleConnectedAsync()
        {
            try
            {
                if (Tailscale == null)
                    return false;

                // Quick sync check - if we can get status and Online is true or BackendState is Running
                var status = await Tailscale.GetStatusAsync();
                var backendState = status?["BackendState"]?.ToString();
                var online = status?["Self"]?["Online"]?.GetValue<bool>() ?? false;

                return online || string.Equals(backendState, "Running", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsTailscaleUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url) ||
                !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return false;
            }

            string host = uri.Host;
            return host.StartsWith("100.", StringComparison.OrdinalIgnoreCase) ||
                   host.StartsWith("127.0.", StringComparison.OrdinalIgnoreCase) ||
                   host.StartsWith("fd", StringComparison.OrdinalIgnoreCase) ||
                   host.Equals("localhost-tailscaled", StringComparison.OrdinalIgnoreCase);
        }

        public static string RewriteServerUrlForTailscale(string serverUrl)
        {
            // Now handled transparently by TailscaleWebProxy on the HttpClient
            return serverUrl;
        }

        public static string RewriteImageUrlForTailscale(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            try
            {
                var uri = new Uri(url);
                bool isTailscale = IsTailscaleUrl(url);

                if (isTailscale)
                {
                    // Rewrite if proxy exists OR if Tailscale is running (proxy may still be initializing).
                    // This avoids race conditions where images load before the proxy is fully ready.
                    bool canProxy = TailscaleProxy != null ||
                                    (Tailscale != null && (Tailscale.IsRunning || Tailscale.IsSocketReachable));

                    if (canProxy)
                    {
                        string proxied = $"{TailscaleProxyService.LocalProxyUrl}/proxy?url={Uri.EscapeDataString(url)}";
                        TailscaleDebugLog.Add($"Rewrote image URL for Tailscale: {url} -> {proxied}");
                        return proxied;
                    }
                }
            }
            catch (Exception ex)
            {
                Tizen.Log.Warn("AppState", $"Failed to rewrite image URL: {ex.Message}");
            }

            return url;
        }

        public static async void OnAppResumed()
        {
            if (AppLifecycle.IsResuming)
            {
                TailscaleDebugLog.Add("OnAppResumed: resume already in progress, skipping.");
                return;
            }

            AppLifecycleState state = AppLifecycleState.NotStarted;
            int attempts = 0;

            var initialState = AppLifecycle.State;
            if (initialState != AppLifecycleState.Suspended)
            {
                TailscaleDebugLog.Add($"OnAppResumed: expected Suspended, actual={initialState}; aborting resume");
                await AppLifecycle.TryTransitionAsync(initialState, AppLifecycleState.ResumeFailed);
                return;
            }

            if (!await AppLifecycle.TryTransitionAsync(AppLifecycleState.Suspended, AppLifecycleState.Resuming))
            {
                TailscaleDebugLog.Add($"OnAppResumed: failed to transition Suspended->Resuming, actual={AppLifecycle.State}");
                return;
            }

            TailscaleDebugLog.Add("App resumed. Checking Tailscale and proxy service status...");

            try
            {
                TailscaleProxyService.ClearCache();
                JellyfinTizen.Utils.CacheHelper.Clear();
                TailscaleDebugLog.Add("Cleared image and API caches on resume.");
            }
            catch (Exception ex)
            {
                TailscaleDebugLog.Add($"Error clearing caches on resume: {ex.Message}");
            }

            // 2. Check if Tailscale needs restart
            bool tailscaleRunning = Tailscale != null && (Tailscale.IsRunning || Tailscale.IsSocketReachable);

            if (!tailscaleRunning)
            {
                state = AppLifecycle.State;
                if (!await AppLifecycle.TryTransitionAsync(AppLifecycleState.Resuming, AppLifecycleState.ResumeRestartingTailscale))
                {
                    TailscaleDebugLog.Add($"OnAppResumed: expected Resuming, actual={state}; aborting resume");
                    await AppLifecycle.TryTransitionAsync(state, AppLifecycleState.ResumeFailed);
                    return;
                }

                try
                {
                    await Tailscale.StageAndStart();
                    TailscaleStartupFailed = false;
                    TailscaleDebugLog.Add("Tailscale restarted on resume");
                }
                catch (Exception ex)
                {
                    TailscaleDebugLog.Add($"Error restarting Tailscale on resume: {ex.Message}");
                    TailscaleStartupFailed = true;
                }

                var nextStage = TailscaleStartupFailed
                    ? AppLifecycleState.ResumeTailscaleStagingFailed
                    : AppLifecycleState.ResumeTailscaleStagingSucceeded;

                state = AppLifecycle.State;
                if (!await AppLifecycle.TryTransitionAsync(AppLifecycleState.ResumeRestartingTailscale, nextStage))
                {
                    TailscaleDebugLog.Add($"OnAppResumed: expected ResumeRestartingTailscale, actual={state}; aborting resume");
                    await AppLifecycle.TryTransitionAsync(state, AppLifecycleState.ResumeFailed);
                    return;
                }
            }
            else
            {
                state = AppLifecycle.State;
                if (!await AppLifecycle.TryTransitionAsync(AppLifecycleState.Resuming, AppLifecycleState.ResumeProxyStarting))
                {
                    TailscaleDebugLog.Add($"OnAppResumed: expected Resuming, actual={state}; aborting resume");
                    await AppLifecycle.TryTransitionAsync(state, AppLifecycleState.ResumeFailed);
                    return;
                }
            }

            // 3. Start proxy (side effect FIRST, then transition on success)
            if (Tailscale != null && (Tailscale.IsRunning || Tailscale.IsSocketReachable))
            {
                try
                {
                    TailscaleDebugLog.Add("Starting Tailscale proxy on resume...");
                    TailscaleProxyService.LocalProxyPort = TailscaleService.GetFreePort(8123);

                    if (TailscaleProxy != null)
                    {
                        TailscaleProxy.Stop();
                        TailscaleProxy.Dispose();
                    }
                    TailscaleProxy = new TailscaleProxyService(HttpClient);
                    TailscaleProxy.Start();

                    bool listenerReady = await AppLifecycle.WaitForProxyListenerReadyAsync(
                        TailscaleProxyService.LocalProxyPort,
                        TimeSpan.FromSeconds(5));

                    if (!listenerReady)
                        throw new TimeoutException("Proxy listener not ready after Start()");
                }
                catch (Exception ex)
                {
                    TailscaleDebugLog.Add($"Error starting proxy on resume: {ex.Message}");

                    state = AppLifecycle.State;
                    if (!await AppLifecycle.TryTransitionAsync(AppLifecycleState.ResumeProxyStarting, AppLifecycleState.ResumeProxyStartFailed))
                    {
                        TailscaleDebugLog.Add($"OnAppResumed: expected ResumeProxyStarting, actual={state}; aborting resume");
                        await AppLifecycle.TryTransitionAsync(state, AppLifecycleState.ResumeFailed);
                    }
                    return;
                }

                state = AppLifecycle.State;
                if (!await AppLifecycle.TryTransitionAsync(AppLifecycleState.ResumeProxyStarting, AppLifecycleState.ResumeProxyListeningConfirmed))
                {
                    TailscaleDebugLog.Add($"OnAppResumed: expected ResumeProxyStarting, actual={state}; aborting resume");
                    await AppLifecycle.TryTransitionAsync(state, AppLifecycleState.ResumeFailed);
                    return;
                }
            }
            else
            {
                state = AppLifecycle.State;
                if (!await AppLifecycle.TryTransitionAsync(AppLifecycleState.Resuming, AppLifecycleState.ResumeCompleted))
                {
                    TailscaleDebugLog.Add($"OnAppResumed: expected Resuming, actual={state}; aborting resume");
                    await AppLifecycle.TryTransitionAsync(state, AppLifecycleState.ResumeFailed);
                }
                return;
            }

            // 4. If server URL is Tailscale-based, wait for tailnet reconnection
            if (IsTailscaleUrl(ServerUrl))
            {
                state = AppLifecycle.State;
                if (!await AppLifecycle.TryTransitionAsync(AppLifecycleState.ResumeProxyListeningConfirmed, AppLifecycleState.ResumeWaitingForTailnet))
                {
                    TailscaleDebugLog.Add($"OnAppResumed: expected ResumeProxyListeningConfirmed, actual={state}; aborting resume");
                    await AppLifecycle.TryTransitionAsync(state, AppLifecycleState.ResumeFailed);
                    return;
                }

                NavigationService.ShowReconnectOverlay("Re-establishing Tailscale connection...");

                bool connected = false;
                try
                {
                    attempts = 0;
                    while (attempts < 15)
                    {
                        try
                        {
                            if (await IsTailscaleConnectedAsync())
                            {
                                TailscaleStartupFailed = false;
                                connected = true;
                                break;
                            }
                        }
                        catch { }
                        await Task.Delay(1000);
                        attempts++;
                    }
                }
                finally
                {
                    NavigationService.HideReconnectOverlay();
                }

                TailscaleDebugLog.Add($"Tailscale reconnection status after resume: connected={connected} (attempts={attempts})");

                var tailnetNext = connected
                    ? AppLifecycleState.ResumeTailnetReconnected
                    : AppLifecycleState.ResumeTailnetTimeout;

                state = AppLifecycle.State;
                if (!await AppLifecycle.TryTransitionAsync(AppLifecycleState.ResumeWaitingForTailnet, tailnetNext))
                {
                    TailscaleDebugLog.Add($"OnAppResumed: expected ResumeWaitingForTailnet, actual={state}; aborting resume");
                    await AppLifecycle.TryTransitionAsync(state, AppLifecycleState.ResumeFailed);
                    return;
                }
            }
            else
            {
                state = AppLifecycle.State;
                if (!await AppLifecycle.TryTransitionAsync(AppLifecycleState.ResumeProxyListeningConfirmed, AppLifecycleState.ResumeCompleted))
                {
                    TailscaleDebugLog.Add($"OnAppResumed: expected ResumeProxyListeningConfirmed, actual={state}; aborting resume");
                    await AppLifecycle.TryTransitionAsync(state, AppLifecycleState.ResumeFailed);
                }
                return;
            }

            // 5. ResumeCompleted -> restore to pre-suspend state (or its phase entry)
            var resumeTarget = AppLifecycle.GetResumeTargetState();
            var phaseEntry = AppLifecycle.GetPhaseEntryState(resumeTarget);

            state = AppLifecycle.State;
            if (!await AppLifecycle.TryTransitionAsync(AppLifecycleState.ResumeCompleted, phaseEntry))
            {
                TailscaleDebugLog.Add($"OnAppResumed: expected ResumeCompleted, actual={state}; aborting resume");
                await AppLifecycle.TryTransitionAsync(state, AppLifecycleState.ResumeFailed);
            }
        }

        public static bool? TryGetAspectMode(string itemId, string mediaSourceId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            string key = $"jf_aspect_{itemId}_{mediaSourceId ?? "default"}";
            string value = GetPreferenceString(key);
            if (bool.TryParse(value, out bool isFullscreen))
                return isFullscreen;

            return null;
        }

        public static void SetAspectMode(string itemId, string mediaSourceId, bool isFullscreen)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return;

            string key = $"jf_aspect_{itemId}_{mediaSourceId ?? "default"}";
            SetPreferenceString(key, isFullscreen.ToString());
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
            var url =
                $"{ServerUrl.TrimEnd('/')}/Users/{UserId}/Images/Primary" +
                $"?width={size}&height={size}" +
                $"&quality=95&v=2&api_key={apiKey}";

            return RewriteImageUrlForTailscale(url);
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
            var url =
                $"{ServerUrl.TrimEnd('/')}/Items/{itemId}/Images/Logo/0" +
                $"?maxWidth={maxWidth}&quality={quality}&api_key={apiKey}";

            return RewriteImageUrlForTailscale(url);
        }

        public static int PlayerBufferInitialMs { get; set; } = 6000;
        public static int PlayerBufferResumeMs { get; set; } = 4000;
        public static int TailscaleSocketWaitSeconds { get; set; } = 30;
        public static int StartupFallbackTimeoutMs { get; set; } = 12000;
        public static int HomeLoadingFallbackTimeoutMs { get; set; } = 25000;

        public static void Shutdown()
        {
            try
            {
                ConnectionMonitor?.Dispose();
            }
            catch { }
        }

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                Proxy = new TailscaleWebProxy(),
                UseProxy = true
            };
            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(DefaultApiTimeoutSeconds)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("JellyfinTizen/2.0");
            return client;
        }
    }
}
