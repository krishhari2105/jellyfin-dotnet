using System.Net;
using System.Net.Http;
using JellyfinTizen.Core;
using Xunit;

namespace JellyfinTizen.CacheHelper.Tests;

public class AuthenticationHeaderManagerTests
{
    private const string ClientName = "Jellyfin for Tizen";
    private const string DeviceName = "Samsung Smart TV";
    private const string DeviceId = "device-id";
    private const string Version = "2.0";

    [Fact]
    public async Task JellyfinAuthenticatedRequestSendsOnlyModernAuthorizationHeader()
    {
        using var capture = new HeaderCaptureHandler();
        using var client = new HttpClient(capture);

        ApplyAuthenticated(client, "jellyfin-token", isEmby: false);
        var headers = await SendAndCaptureAsync(client, capture);

        AssertSingleAuthorization(
            headers,
            "MediaBrowser",
            "Token=\"jellyfin-token\"");
    }

    [Fact]
    public async Task EmbyAuthenticatedUserSessionSendsOnlyModernAuthorizationHeader()
    {
        using var capture = new HeaderCaptureHandler();
        using var client = new HttpClient(capture);

        ApplyAuthenticated(client, "emby-token", isEmby: true);
        var headers = await SendAndCaptureAsync(client, capture);

        AssertSingleAuthorization(
            headers,
            "Emby",
            "Token=\"emby-token\"");
    }

    [Theory]
    [InlineData(false, "MediaBrowser")]
    [InlineData(true, "Emby")]
    public async Task LoginRequestSendsTokenlessServerSpecificIdentification(
        bool isEmby,
        string expectedScheme)
    {
        using var capture = new HeaderCaptureHandler();
        using var client = new HttpClient(capture);
        SeedAllAuthenticationHeaders(client);
        AuthenticationHeaderManager.Remove(client.DefaultRequestHeaders);

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.test/Users/AuthenticateByName");
        AuthenticationHeaderManager.Apply(
            request.Headers,
            Build(token: null, isEmby: isEmby));

        using var response = await client.SendAsync(request);
        var headers = capture.Headers;

        AssertSingleAuthorization(headers, expectedScheme, requiredFragment: null);
        Assert.DoesNotContain("Token=", headers["Authorization"].Single());
    }

    [Fact]
    public async Task SwitchingJellyfinToEmbyRemovesOldSchemeAndLegacyHeaders()
    {
        using var capture = new HeaderCaptureHandler();
        using var client = new HttpClient(capture);

        ApplyAuthenticated(client, "jellyfin-token", isEmby: false);
        SeedLegacyHeaders(client);
        ApplyAuthenticated(client, "emby-token", isEmby: true);
        var headers = await SendAndCaptureAsync(client, capture);

        AssertSingleAuthorization(headers, "Emby", "Token=\"emby-token\"");
        Assert.DoesNotContain("MediaBrowser", headers["Authorization"].Single());
        Assert.DoesNotContain("jellyfin-token", headers["Authorization"].Single());
    }

    [Fact]
    public async Task SwitchingEmbyToJellyfinRemovesOldSchemeAndLegacyHeaders()
    {
        using var capture = new HeaderCaptureHandler();
        using var client = new HttpClient(capture);

        ApplyAuthenticated(client, "emby-token", isEmby: true);
        SeedLegacyHeaders(client);
        ApplyAuthenticated(client, "jellyfin-token", isEmby: false);
        var headers = await SendAndCaptureAsync(client, capture);

        AssertSingleAuthorization(headers, "MediaBrowser", "Token=\"jellyfin-token\"");
        Assert.DoesNotContain("Emby ", headers["Authorization"].Single());
        Assert.DoesNotContain("emby-token", headers["Authorization"].Single());
    }

    [Fact]
    public async Task ClearingAuthenticationRemovesEverySupportedAuthenticationHeader()
    {
        using var capture = new HeaderCaptureHandler();
        using var client = new HttpClient(capture);
        SeedAllAuthenticationHeaders(client);

        AuthenticationHeaderManager.Remove(client.DefaultRequestHeaders);
        var headers = await SendAndCaptureAsync(client, capture);

        AssertNoAuthenticationHeaders(headers);
    }

    [Fact]
    public async Task ReapplyingTokenDoesNotDuplicateAuthorizationValues()
    {
        using var capture = new HeaderCaptureHandler();
        using var client = new HttpClient(capture);

        ApplyAuthenticated(client, "first-token", isEmby: false);
        ApplyAuthenticated(client, "second-token", isEmby: false);
        var headers = await SendAndCaptureAsync(client, capture);

        Assert.Single(headers["Authorization"]);
        Assert.Contains("Token=\"second-token\"", headers["Authorization"].Single());
        Assert.DoesNotContain("first-token", headers["Authorization"].Single());
    }

    [Fact]
    public async Task RequestSpecificAuthorizationOverridesDefaultWithoutDuplicateValues()
    {
        using var capture = new HeaderCaptureHandler();
        using var client = new HttpClient(capture);
        ApplyAuthenticated(client, "default-token", isEmby: false);

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/Subtitles/0");
        AuthenticationHeaderManager.Apply(
            request.Headers,
            Build("request-token", isEmby: false));

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var headers = capture.Headers;

        AssertSingleAuthorization(headers, "MediaBrowser", "Token=\"request-token\"");
        Assert.DoesNotContain("default-token", headers["Authorization"].Single());
    }

    [Theory]
    [InlineData(false, "MediaBrowser", "saved-jellyfin-token")]
    [InlineData(true, "Emby", "saved-emby-token")]
    public async Task SavedSessionReapplicationPreservesServerScheme(
        bool isEmby,
        string expectedScheme,
        string savedToken)
    {
        using var capture = new HeaderCaptureHandler();
        using var client = new HttpClient(capture);

        AuthenticationHeaderManager.Remove(client.DefaultRequestHeaders);
        ApplyAuthenticated(client, savedToken, isEmby);
        var headers = await SendAndCaptureAsync(client, capture);

        AssertSingleAuthorization(
            headers,
            expectedScheme,
            $"Token=\"{savedToken}\"");
    }

    private static void ApplyAuthenticated(HttpClient client, string token, bool isEmby)
    {
        AuthenticationHeaderManager.Apply(
            client.DefaultRequestHeaders,
            Build(token, isEmby));
    }

    private static string Build(string token, bool isEmby)
    {
        return AuthorizationHeaderFormatter.Build(
            ClientName,
            DeviceName,
            DeviceId,
            Version,
            token,
            isEmby);
    }

    private static async Task<IReadOnlyDictionary<string, string[]>> SendAndCaptureAsync(
        HttpClient client,
        HeaderCaptureHandler capture)
    {
        using var response = await client.GetAsync("https://example.test/System/Info");
        response.EnsureSuccessStatusCode();
        return capture.Headers;
    }

    private static void AssertSingleAuthorization(
        IReadOnlyDictionary<string, string[]> headers,
        string expectedScheme,
        string requiredFragment)
    {
        Assert.True(headers.TryGetValue("Authorization", out var values));
        Assert.Single(values);
        Assert.StartsWith(expectedScheme + " ", values.Single());
        if (requiredFragment != null)
            Assert.Contains(requiredFragment, values.Single());
        AssertNoLegacyHeaders(headers);
    }

    private static void AssertNoAuthenticationHeaders(
        IReadOnlyDictionary<string, string[]> headers)
    {
        Assert.False(headers.ContainsKey("Authorization"));
        AssertNoLegacyHeaders(headers);
    }

    private static void AssertNoLegacyHeaders(
        IReadOnlyDictionary<string, string[]> headers)
    {
        Assert.False(headers.ContainsKey("X-Emby-Authorization"));
        Assert.False(headers.ContainsKey("X-Emby-Token"));
        Assert.False(headers.ContainsKey("X-MediaBrowser-Token"));
    }

    private static void SeedAllAuthenticationHeaders(HttpClient client)
    {
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "MediaBrowser stale");
        SeedLegacyHeaders(client);
    }

    private static void SeedLegacyHeaders(HttpClient client)
    {
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Emby-Authorization", "Emby stale");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Emby-Token", "stale-emby-token");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-MediaBrowser-Token", "stale-jellyfin-token");
    }

    private sealed class HeaderCaptureHandler : HttpMessageHandler
    {
        internal IReadOnlyDictionary<string, string[]> Headers { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Headers = request.Headers.ToDictionary(
                header => header.Key,
                header => header.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request
            });
        }
    }
}
