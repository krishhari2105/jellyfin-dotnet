using System;
using System.Net.Http.Headers;

namespace JellyfinTizen.Core
{
    internal static class AuthenticationHeaderManager
    {
        internal const string AuthorizationHeaderName = "Authorization";
        internal const string EmbyAuthorizationHeaderName = "X-Emby-Authorization";
        internal const string EmbyTokenHeaderName = "X-Emby-Token";
        internal const string MediaBrowserTokenHeaderName = "X-MediaBrowser-Token";

        internal static void Apply(HttpRequestHeaders headers, string authorizationValue)
        {
            if (headers == null)
                throw new ArgumentNullException(nameof(headers));

            Remove(headers);

            if (string.IsNullOrWhiteSpace(authorizationValue))
                return;

            headers.TryAddWithoutValidation(AuthorizationHeaderName, authorizationValue);
        }

        internal static void Remove(HttpRequestHeaders headers)
        {
            if (headers == null)
                throw new ArgumentNullException(nameof(headers));

            headers.Remove(AuthorizationHeaderName);
            headers.Remove(EmbyAuthorizationHeaderName);
            headers.Remove(EmbyTokenHeaderName);
            headers.Remove(MediaBrowserTokenHeaderName);
        }
    }
}
