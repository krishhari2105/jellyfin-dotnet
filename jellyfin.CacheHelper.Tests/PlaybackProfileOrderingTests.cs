using System.Collections.Generic;
using System.Text.Json;
using JellyfinTizen.Core;
using JellyfinTizen.Models;
using Xunit;

namespace JellyfinTizen.CacheHelper.Tests;

public class PlaybackProfileOrderingTests
{
    [Theory]
    [InlineData("av1")]
    [InlineData("AV1")]
    [InlineData(" vp9 ")]
    public void EmbyAv1AndVp9PreferFmp4(string codec)
    {
        Assert.True(
            PlaybackProfileOrdering.ShouldPreferFmp4Hls(
                isEmby: true,
                codec));
    }

    [Theory]
    [InlineData("h264")]
    [InlineData("hevc")]
    [InlineData(null)]
    public void EmbyTsCompatibleCodecsKeepDefaultOrder(string codec)
    {
        Assert.False(
            PlaybackProfileOrdering.ShouldPreferFmp4Hls(
                isEmby: true,
                codec));
    }

    [Theory]
    [InlineData("av1")]
    [InlineData("vp9")]
    public void JellyfinKeepsDefaultOrder(string codec)
    {
        Assert.False(
            PlaybackProfileOrdering.ShouldPreferFmp4Hls(
                isEmby: false,
                codec));
    }

    [Fact]
    public void Fmp4ProfilesMoveFirstAndOtherOrderIsPreserved()
    {
        var tsHls = CreateProfile("ts", "hls");
        var progressiveMp4 = CreateProfile("mp4", "http");
        var fmp4Hls = CreateProfile("mp4", "hls");
        var mkvHttp = CreateProfile("mkv", "http");

        var reordered =
            PlaybackProfileOrdering.PreferFmp4HlsFirst(
                new List<TranscodingProfile>
                {
                    tsHls,
                    progressiveMp4,
                    fmp4Hls,
                    mkvHttp
                });

        Assert.Collection(
            reordered,
            profile => Assert.Same(fmp4Hls, profile),
            profile => Assert.Same(tsHls, profile),
            profile => Assert.Same(progressiveMp4, profile),
            profile => Assert.Same(mkvHttp, profile));
    }

    [Fact]
    public void OptionalTranscodingFieldsUseServerCompatibleJsonTypes()
    {
        var hlsProfile = new TranscodingProfile
        {
            Type = "Video",
            Container = "mp4",
            Protocol = "hls",
            MinSegments = 2,
            SegmentLength = 6,
            MaxAudioChannels = 6,
            BreakOnNonKeyFrames = false,
            EnableAudioVbrEncoding = false
        };
        var httpProfile = new TranscodingProfile
        {
            Type = "Video",
            Container = "mp4",
            Protocol = "http"
        };
        var json = JsonSerializer.Serialize(
            new[] { hlsProfile, httpProfile },
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

        using var document = JsonDocument.Parse(json);
        var hlsJson = document.RootElement[0];
        Assert.Equal(
            JsonValueKind.Number,
            hlsJson.GetProperty("minSegments").ValueKind);
        Assert.Equal(2, hlsJson.GetProperty("minSegments").GetInt32());
        Assert.Equal(
            JsonValueKind.Number,
            hlsJson.GetProperty("segmentLength").ValueKind);
        Assert.Equal(6, hlsJson.GetProperty("segmentLength").GetInt32());
        Assert.Equal(
            JsonValueKind.Number,
            hlsJson.GetProperty("maxAudioChannels").ValueKind);
        Assert.Equal(
            JsonValueKind.False,
            hlsJson.GetProperty("breakOnNonKeyFrames").ValueKind);

        var httpJson = document.RootElement[1];
        Assert.False(httpJson.TryGetProperty("minSegments", out _));
        Assert.False(httpJson.TryGetProperty("segmentLength", out _));
        Assert.False(httpJson.TryGetProperty("maxAudioChannels", out _));
        Assert.False(httpJson.TryGetProperty(
            "breakOnNonKeyFrames",
            out _));
        Assert.False(httpJson.TryGetProperty(
            "enableAudioVbrEncoding",
            out _));
    }

    private static TranscodingProfile CreateProfile(
        string container,
        string protocol)
    {
        return new TranscodingProfile
        {
            Type = "Video",
            Container = container,
            Protocol = protocol
        };
    }
}
