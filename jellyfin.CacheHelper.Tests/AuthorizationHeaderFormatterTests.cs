using JellyfinTizen.Core;
using Xunit;

namespace JellyfinTizen.CacheHelper.Tests;

public class AuthorizationHeaderFormatterTests
{
    [Fact]
    public void JellyfinHeaderUrlEncodesMetadataAndToken()
    {
        var header = AuthorizationHeaderFormatter.Build(
            "Jellyfin for Tizen",
            "Samsung Smart TV",
            "device id",
            "2.0",
            "access token",
            isEmby: false);

        Assert.Equal(
            "MediaBrowser Client=\"Jellyfin%20for%20Tizen\", Device=\"Samsung%20Smart%20TV\", DeviceId=\"device%20id\", Version=\"2.0\", Token=\"access%20token\"",
            header);
    }

    [Fact]
    public void EmbyHeaderPreservesSpacesAndIncludesUserSessionToken()
    {
        var header = AuthorizationHeaderFormatter.Build(
            "Jellyfin for Tizen",
            "Samsung Smart TV",
            "device id",
            "2.0",
            "access token",
            isEmby: true);

        Assert.Equal(
            "Emby Client=\"Jellyfin for Tizen\", Device=\"Samsung Smart TV\", DeviceId=\"device id\", Version=\"2.0\", Token=\"access token\"",
            header);
        Assert.Contains("Token=\"access token\"", header);
    }

    [Fact]
    public void EmbyQuotedValuesEscapeSpecialCharactersAndRemoveLineBreaks()
    {
        var escaped = AuthorizationHeaderFormatter.EscapeEmbyQuotedValue("TV \"Room\"\\Main\r\nInjected");
        var header = AuthorizationHeaderFormatter.Build(
            null,
            "Samsung Smart TV",
            "device\r\nInjected",
            "2.0",
            null,
            isEmby: true);

        Assert.Equal("TV \\\"Room\\\"\\\\MainInjected", escaped);
        Assert.DoesNotContain('\r', escaped);
        Assert.DoesNotContain('\n', escaped);
        Assert.DoesNotContain('\r', header);
        Assert.DoesNotContain('\n', header);
        Assert.Contains("Client=\"\"", header);
    }

    [Theory]
    [InlineData(false, "MediaBrowser")]
    [InlineData(true, "Emby")]
    public void TokenlessHeaderUsesServerSpecificScheme(bool isEmby, string scheme)
    {
        var header = AuthorizationHeaderFormatter.Build(
            "Jellyfin for Tizen",
            "Samsung Smart TV",
            "device-id",
            "2.0",
            token: null,
            isEmby: isEmby);

        Assert.StartsWith(scheme + " ", header);
        Assert.DoesNotContain("Token=", header);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HeaderEscapesEveryQuotedValueIncludingToken(bool isEmby)
    {
        var header = AuthorizationHeaderFormatter.Build(
            "Client \"Name\"\\\r\n",
            "Device \"Name\"\\\r\n",
            "Device \"Id\"\\\r\n",
            "2.0 \"Beta\"\\\r\n",
            "Token \"Value\"\\\r\n",
            isEmby);

        var expected = isEmby
            ? "Emby Client=\"Client \\\"Name\\\"\\\\\", Device=\"Device \\\"Name\\\"\\\\\", " +
              "DeviceId=\"Device \\\"Id\\\"\\\\\", Version=\"2.0 \\\"Beta\\\"\\\\\", " +
              "Token=\"Token \\\"Value\\\"\\\\\""
            : "MediaBrowser Client=\"Client%20%22Name%22%5C%0D%0A\", " +
              "Device=\"Device%20%22Name%22%5C%0D%0A\", " +
              "DeviceId=\"Device%20%22Id%22%5C%0D%0A\", " +
              "Version=\"2.0%20%22Beta%22%5C%0D%0A\", " +
              "Token=\"Token%20%22Value%22%5C%0D%0A\"";

        Assert.Equal(expected, header);
    }
}
