using System;

namespace JellyfinTizen.Core
{
    internal static class AuthorizationHeaderFormatter
    {
        public static string Build(
            string clientName,
            string deviceName,
            string deviceId,
            string clientVersion,
            string token,
            bool isEmby)
        {
            string header;
            if (isEmby)
            {
                header = "Emby Client=\"" + EscapeEmbyQuotedValue(clientName) + "\", " +
                         "Device=\"" + EscapeEmbyQuotedValue(deviceName) + "\", " +
                         "DeviceId=\"" + EscapeEmbyQuotedValue(deviceId) + "\", " +
                         "Version=\"" + EscapeEmbyQuotedValue(clientVersion) + "\"";

                if (!string.IsNullOrWhiteSpace(token))
                    header += ", Token=\"" + EscapeEmbyQuotedValue(token) + "\"";

                return header;
            }

            header = "MediaBrowser Client=\"" + EscapeMediaBrowserValue(clientName) + "\", " +
                     "Device=\"" + EscapeMediaBrowserValue(deviceName) + "\", " +
                     "DeviceId=\"" + EscapeMediaBrowserValue(deviceId) + "\", " +
                     "Version=\"" + EscapeMediaBrowserValue(clientVersion) + "\"";

            if (!string.IsNullOrWhiteSpace(token))
                header += ", Token=\"" + EscapeMediaBrowserValue(token) + "\"";

            return header;
        }

        private static string EscapeMediaBrowserValue(string value)
        {
            return Uri.EscapeDataString(value ?? string.Empty);
        }

        internal static string EscapeEmbyQuotedValue(string value)
        {
            return (value ?? string.Empty)
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }
    }
}
