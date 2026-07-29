using JellyfinTizen.Core;
using Xunit;

namespace JellyfinTizen.CacheHelper.Tests;

public class SubtitleDeliveryUrlBuilderTests
{
    [Fact]
    public void MissingDeliveryUrlUsesCanonicalSubtitleEndpoint()
    {
        string url = SubtitleDeliveryUrlBuilder.Build(
            "https://media.example.test/",
            "item id",
            "source/id",
            7,
            "srt");

        Assert.Equal(
            "https://media.example.test/Videos/item%20id/source%2Fid/Subtitles/7/0/Stream.srt",
            url);
    }

    [Fact]
    public void RelativeDeliveryUrlIsRebasedToServer()
    {
        string url = SubtitleDeliveryUrlBuilder.Build(
            "https://media.example.test/",
            "item",
            "source",
            3,
            "srt",
            "/Videos/item/source/Subtitles/3/0/Stream.ass?foo=bar");

        Assert.Equal(
            "https://media.example.test/Videos/item/source/Subtitles/3/0/Stream.ass?foo=bar",
            url);
    }

    [Fact]
    public void AbsoluteDeliveryUrlIsPreserved()
    {
        string url = SubtitleDeliveryUrlBuilder.Build(
            "https://media.example.test",
            "item",
            "source",
            3,
            "srt",
            "https://cdn.example.test/subtitle.vtt?signature=abc");

        Assert.Equal(
            "https://cdn.example.test/subtitle.vtt?signature=abc",
            url);
    }

    [Fact]
    public void FallbackCanBeDisabled()
    {
        string url = SubtitleDeliveryUrlBuilder.Build(
            "https://media.example.test",
            "item",
            "source",
            3,
            "srt",
            allowFallbackWhenMissingDelivery: false);

        Assert.Null(url);
    }
}
