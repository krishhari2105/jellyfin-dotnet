using System;
using System.Text.RegularExpressions;

namespace JellyfinTizen.Core
{
    internal static class SensitiveDataRedactor
    {
        private static readonly Regex AuthorizationHeaderRegex = new(
            @"(\b(?:Authorization|X-Emby-Authorization)\s*[:=]\s*)[^\r\n;]+",
            RegexOptions.IgnoreCase);

        private static readonly Regex LegacyTokenHeaderRegex = new(
            @"(\b(?:X-Emby-Token|X-MediaBrowser-Token)\s*[:=]\s*)[^,;\s]+",
            RegexOptions.IgnoreCase);

        private static readonly Regex NamedTokenRegex = new(
            @"(\b(?:accessToken|apiKey)\s*[:=]\s*)[^,;\s]+",
            RegexOptions.IgnoreCase);

        private static readonly Regex QuotedTokenRegex = new(
            @"(\bToken\s*=\s*"")[^""]*("")",
            RegexOptions.IgnoreCase);

        private static readonly Regex QueryTokenRegex = new(
            @"([?&](?:api_key|apikey|token|X-Emby-Token)=)[^&\s]*",
            RegexOptions.IgnoreCase);

        private static readonly Regex EncodedQueryTokenRegex = new(
            @"((?:%3F|%26)(?:api_key|apikey|token|X-Emby-Token)%3D)(?:(?!%26)[^\s])*",
            RegexOptions.IgnoreCase);

        internal static string Redact(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            var redacted = AuthorizationHeaderRegex.Replace(value, "$1<redacted>");
            redacted = LegacyTokenHeaderRegex.Replace(redacted, "$1<redacted>");
            redacted = NamedTokenRegex.Replace(redacted, "$1<redacted>");
            redacted = QuotedTokenRegex.Replace(redacted, "$1<redacted>$2");
            redacted = QueryTokenRegex.Replace(redacted, "$1<redacted>");
            return EncodedQueryTokenRegex.Replace(redacted, "$1%3Credacted%3E");
        }
    }
}
