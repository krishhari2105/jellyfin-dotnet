using System;

namespace JellyfinTizen.Core
{
    public static class AppState
    {
        private const string KeyServerUrl = "jf_server_url";
        private const string KeyAccessToken = "jf_access_token";
        private const string KeyUserId = "jf_user_id";
        private const string KeyUsername = "jf_username";
        private const string KeyBurnInSubtitles = "jf_burn_in_subtitles";

        public static string ServerUrl { get; set; }
        public static string AccessToken { get; set; }
        public static string UserId { get; set; }
        public static string Username { get; set; }

        public static JellyfinService Jellyfin { get; private set; }

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
        }

        public static bool TryRestoreFullSession()
        {
            try
            {
                if (!Tizen.Applications.Preference.Contains(KeyServerUrl) ||
                    !Tizen.Applications.Preference.Contains(KeyAccessToken) ||
                    !Tizen.Applications.Preference.Contains(KeyUserId))
                {
                    return false;
                }

                var serverUrl = Tizen.Applications.Preference.Get<string>(KeyServerUrl);
                var accessToken = Tizen.Applications.Preference.Get<string>(KeyAccessToken);
                var userId = Tizen.Applications.Preference.Get<string>(KeyUserId);
                var username = Tizen.Applications.Preference.Contains(KeyUsername)
                    ? Tizen.Applications.Preference.Get<string>(KeyUsername)
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(serverUrl) ||
                    string.IsNullOrWhiteSpace(accessToken) ||
                    string.IsNullOrWhiteSpace(userId))
                {
                    return false;
                }

                ServerUrl = serverUrl;
                AccessToken = accessToken;
                UserId = userId;
                Username = username;

                Jellyfin.Connect(serverUrl);
                Jellyfin.SetAuthToken(accessToken);
                Jellyfin.SetUserId(userId);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryRestoreServer()
        {
            try
            {
                if (!Tizen.Applications.Preference.Contains(KeyServerUrl))
                    return false;

                var serverUrl = Tizen.Applications.Preference.Get<string>(KeyServerUrl);
                if (string.IsNullOrWhiteSpace(serverUrl))
                    return false;

                ServerUrl = serverUrl;
                Jellyfin.Connect(serverUrl);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void SaveServer(string serverUrl)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
                return;

            ServerUrl = serverUrl;
            Tizen.Applications.Preference.Set(KeyServerUrl, serverUrl);
        }

        public static void SaveSession(string serverUrl, string accessToken, string userId, string username)
        {
            if (string.IsNullOrWhiteSpace(serverUrl) ||
                string.IsNullOrWhiteSpace(accessToken) ||
                string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            SaveServer(serverUrl);
            AccessToken = accessToken;
            UserId = userId;
            Username = username;

            Tizen.Applications.Preference.Set(KeyAccessToken, accessToken);
            Tizen.Applications.Preference.Set(KeyUserId, userId);

            if (!string.IsNullOrWhiteSpace(username))
                Tizen.Applications.Preference.Set(KeyUsername, username);
        }

        public static void ClearSession(bool clearServer)
        {
            AccessToken = null;
            UserId = null;
            Username = null;

            Jellyfin?.ClearAuthToken();
            Jellyfin?.SetUserId(null);

            RemovePreferenceIfExists(KeyAccessToken);
            RemovePreferenceIfExists(KeyUserId);
            RemovePreferenceIfExists(KeyUsername);

            if (clearServer)
            {
                ServerUrl = null;
                RemovePreferenceIfExists(KeyServerUrl);
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

        public static string GetItemLogoUrl(string itemId, int maxWidth = 900)
        {
            if (maxWidth <= 0)
                maxWidth = 900;

            if (string.IsNullOrWhiteSpace(itemId) ||
                string.IsNullOrWhiteSpace(ServerUrl) ||
                string.IsNullOrWhiteSpace(AccessToken))
            {
                return null;
            }

            var apiKey = Uri.EscapeDataString(AccessToken);
            return
                $"{ServerUrl.TrimEnd('/')}/Items/{itemId}/Images/Logo/0" +
                $"?maxWidth={maxWidth}&quality=95&api_key={apiKey}";
        }
    }
}
