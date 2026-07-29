using System;

namespace JellyfinTizen.Core
{
    public static class SubtitleDeliveryUrlBuilder
    {
        public static string Build(
            string serverUrl,
            string itemId,
            string mediaSourceId,
            int subtitleIndex,
            string extension,
            string deliveryUrl = null,
            bool allowFallbackWhenMissingDelivery = true)
        {
            string normalizedServerUrl = serverUrl?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(normalizedServerUrl))
                return null;

            if (!string.IsNullOrWhiteSpace(deliveryUrl))
            {
                string normalizedUrl = NormalizeDeliveryUrl(deliveryUrl);
                string absoluteUrl;

                if (normalizedUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    // Some server versions expose their HTTP subtitle endpoint as a
                    // pseudo file URL. Preserve its query while rebasing it to the server.
                    string raw = normalizedUrl.Substring("file://".Length).TrimStart('/');
                    int queryIndex = raw.IndexOf('?');
                    string path = queryIndex >= 0 ? raw.Substring(0, queryIndex) : raw;
                    string query = queryIndex >= 0 ? raw.Substring(queryIndex + 1) : string.Empty;

                    if (!path.StartsWith("Videos/", StringComparison.OrdinalIgnoreCase))
                        path = $"Videos/{path}";

                    absoluteUrl = $"{normalizedServerUrl}/{path}";
                    if (!string.IsNullOrWhiteSpace(query))
                        absoluteUrl = $"{absoluteUrl}?{query.TrimStart('?', '&')}";
                }
                else if (Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var absoluteUri) &&
                         (absoluteUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                          absoluteUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
                {
                    absoluteUrl = absoluteUri.ToString();
                }
                else
                {
                    absoluteUrl = $"{normalizedServerUrl}/{normalizedUrl.TrimStart('/')}";
                }

                return NormalizeDeliveryUrl(absoluteUrl);
            }

            if (!allowFallbackWhenMissingDelivery ||
                string.IsNullOrWhiteSpace(itemId) ||
                string.IsNullOrWhiteSpace(mediaSourceId))
            {
                return null;
            }

            string normalizedExtension = string.IsNullOrWhiteSpace(extension)
                ? "srt"
                : extension.Trim().TrimStart('.');

            return
                $"{normalizedServerUrl}/Videos/{Uri.EscapeDataString(itemId)}/" +
                $"{Uri.EscapeDataString(mediaSourceId)}/Subtitles/{subtitleIndex}/0/" +
                $"Stream.{Uri.EscapeDataString(normalizedExtension)}";
        }

        public static string NormalizeDeliveryUrl(string deliveryUrl)
        {
            if (string.IsNullOrWhiteSpace(deliveryUrl))
                return string.Empty;

            string normalized = deliveryUrl.Trim().Replace("\r", "").Replace("\n", "");
            return normalized
                .Replace("%3F", "?", StringComparison.OrdinalIgnoreCase)
                .Replace("%26", "&", StringComparison.OrdinalIgnoreCase)
                .Replace("%3D", "=", StringComparison.OrdinalIgnoreCase)
                .Replace("??", "?")
                .Replace("?&", "?");
        }
    }
}
