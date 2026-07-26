using JellyfinTizen.Core;
using Xunit;

namespace JellyfinTizen.CacheHelper.Tests;

public class SensitiveDataRedactorTests
{
    [Fact]
    public void AuthenticationFailureDiagnosticsDoNotExposeTokenValues()
    {
        const string token = "fake-secret-token";
        var authorization = AuthorizationHeaderFormatter.Build(
            "Jellyfin for Tizen",
            "Samsung Smart TV",
            "device-id",
            "2.0",
            token,
            isEmby: true);

        DebugSwitches.EnableVerboseDebugLogging = true;
        try
        {
            AppDebugLog.Add(
                $"Authentication failed; Authorization: {authorization}; " +
                $"url=https://example.test/Users/Me?api_key={token}; " +
                $"X-Emby-Token={token}");

            var latest = AppDebugLog.GetAllLines().Last();
            Assert.DoesNotContain(token, latest);
            Assert.DoesNotContain(authorization, latest);
            Assert.Contains("<redacted>", latest);
        }
        finally
        {
            DebugSwitches.EnableVerboseDebugLogging = false;
        }
    }

    [Fact]
    public void EncodedUrlDoesNotExposeQueryToken()
    {
        const string token = "encoded-secret-token";
        var message =
            "https%3A%2F%2Fexample.test%2Fimage%3F" +
            $"api_key%3D{token}%26quality%3D50";

        var redacted = SensitiveDataRedactor.Redact(message);

        Assert.DoesNotContain(token, redacted);
        Assert.Contains("%3Credacted%3E", redacted);
    }
}
