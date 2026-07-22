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
            if (isEmby)
            {
                return "Emby Client=\"" + EscapeEmbyQuotedValue(clientName) + "\", " +
                       "Device=\"" + EscapeEmbyQuotedValue(deviceName) + "\", " +
                       "DeviceId=\"" + EscapeEmbyQuotedValue(deviceId) + "\", " +
                       "Version=\"" + EscapeEmbyQuotedValue(clientVersion) + "\"";
            }

            var header = "MediaBrowser Client=\"" + Uri.EscapeDataString(clientName) + "\", " +
                         "Device=\"" + Uri.EscapeDataString(deviceName) + "\", " +
                         "DeviceId=\"" + Uri.EscapeDataString(deviceId) + "\", " +
                         "Version=\"" + Uri.EscapeDataString(clientVersion) + "\"";

            if (!string.IsNullOrEmpty(token))
                header += ", Token=\"" + Uri.EscapeDataString(token) + "\"";

            return header;
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
