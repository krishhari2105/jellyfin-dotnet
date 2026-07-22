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
    public void EmbyHeaderPreservesSpacesAndKeepsTokenSeparate()
    {
        var header = AuthorizationHeaderFormatter.Build(
            "Jellyfin for Tizen",
            "Samsung Smart TV",
            "device id",
            "2.0",
            "access token",
            isEmby: true);

        Assert.Equal(
            "Emby Client=\"Jellyfin for Tizen\", Device=\"Samsung Smart TV\", DeviceId=\"device id\", Version=\"2.0\"",
            header);
        Assert.DoesNotContain("Token=", header);
        Assert.DoesNotContain("access token", header);
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
}
