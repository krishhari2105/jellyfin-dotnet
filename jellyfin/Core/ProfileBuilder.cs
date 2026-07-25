using System.Collections.Generic;
using System.Linq;
using JellyfinTizen.Models;

namespace JellyfinTizen.Core
{
    public static class ProfileBuilder
    {
        public static DeviceProfile BuildTizenProfile(
            bool forceBurnIn = false,
            bool disableSubtitles = false,
            bool preferTsOnlyHls = false,
            bool preferTsHlsFirst = false,
            bool preferFmp4HlsFirst = false)
        {
            var runtime = TizenPlaybackCapabilities.Current;
            var tsVideoCodecList = new List<string> { "h264" };
            if (runtime.SupportsVc1)
                tsVideoCodecList.Add("vc1");
            tsVideoCodecList.Add("mpeg2video");
            if (runtime.SupportsHevcInMpegTsHls)
                tsVideoCodecList.Add("hevc");

            string tsVideoCodecs = string.Join(",", tsVideoCodecList);
            string fmp4VideoCodecs = runtime.SupportsAv1
                ? "h264,hevc,av1,vp9"
                : "h264,hevc,vp9";
            string directStreamVideoCodecs = runtime.SupportsAv1
                ? "h264,hevc,av1,vp9"
                : "h264,hevc,vp9";
            var broadVideoCodecList = new List<string>
            {
                "h264",
                "hevc",
                "mpeg2video",
                "msmpeg4v2",
                "vp8",
                "vp9"
            };
            if (runtime.SupportsVc1)
                broadVideoCodecList.Add("vc1");
            if (runtime.SupportsAv1)
                broadVideoCodecList.Add("av1");

            string broadVideoCodecs =
                string.Join(",", broadVideoCodecList);
            string webmVideoCodecs = runtime.SupportsAv1
                ? "vp8,vp9,av1"
                : "vp8,vp9";
            string m2tsVideoCodecs = runtime.SupportsVc1
                ? "h264,hevc,vc1,mpeg2video"
                : "h264,hevc,mpeg2video";
            string hlsAudioCodecs = runtime.SupportsEac3InHls
                ? "ac3,eac3,aac,mp3"
                : "ac3,aac,mp3";
            const int hlsSegmentLengthSeconds = 6;
            const int hlsMinimumSegments = 2;

            var transcodingProfiles = new List<TranscodingProfile>
            {
                // MPEG-TS is the primary AVPlay route. HEVC is included only on
                // firmware where the HLS MPEG-TS demuxer is known to accept it.
                new TranscodingProfile
                {
                    Container = "ts",
                    Type = "Video",
                    AudioCodec = hlsAudioCodecs,
                    VideoCodec = tsVideoCodecs,
                    Context = "Streaming",
                    Protocol = "hls",
                    MaxAudioChannels = runtime.MaxHlsAudioChannels,
                    MinSegments = hlsMinimumSegments,
                    SegmentLength = hlsSegmentLengthSeconds,
                    BreakOnNonKeyFrames = false,
                    EnableAudioVbrEncoding = false
                }
            };

            // fMP4 HLS is useful for copy-streaming AV1 and VP9, which do not fit
            // in MPEG-TS. Jellyfin can select this secondary profile by codec;
            // Emby-specific ordering is applied below when the source needs it.
            if (runtime.SupportsFmp4Hls)
            {
                transcodingProfiles.Add(new TranscodingProfile
                {
                    Container = "mp4",
                    Type = "Video",
                    AudioCodec = hlsAudioCodecs,
                    VideoCodec = fmp4VideoCodecs,
                    Context = "Streaming",
                    Protocol = "hls",
                    MaxAudioChannels = runtime.MaxHlsAudioChannels,
                    MinSegments = hlsMinimumSegments,
                    SegmentLength = hlsSegmentLengthSeconds,
                    BreakOnNonKeyFrames = false,
                    EnableAudioVbrEncoding = false
                });
            }

            // Progressive MP4 remains a non-HLS fallback for servers or media
            // combinations that cannot use the preferred segmented routes.
            transcodingProfiles.Add(new TranscodingProfile
            {
                Container = "mp4",
                Type = "Video",
                AudioCodec = hlsAudioCodecs,
                VideoCodec = fmp4VideoCodecs,
                Context = "Streaming",
                Protocol = "http",
                MaxAudioChannels = runtime.MaxHlsAudioChannels
            });

            transcodingProfiles.Add(new TranscodingProfile
            {
                Container = "mkv",
                Type = "Video",
                AudioCodec = "ac3,eac3,aac,mp3,ac4,mp2,pcm_s16le,pcm_s24le,aac_latm,opus,flac,vorbis",
                VideoCodec = broadVideoCodecs,
                Context = "Streaming",
                Protocol = "http"
            });

            var profile = new DeviceProfile
            {
                Name = "Samsung Smart TV",
                
                MaxStreamingBitrate = 120000000, // 120 Mbps limit
               
                CodecProfiles = new List<CodecProfile>
                {
                    new CodecProfile
                    {
                        Type = "Video",
                        Codec = "h264",
                        Conditions = new List<ProfileCondition>
                        {
                            new ProfileCondition { Condition = "EqualsAny", Property = "VideoProfile", Value = "high|main|baseline|constrained baseline|high 10", IsRequired = false },
                            new ProfileCondition { Condition = "LessThanEqual", Property = "VideoLevel", Value = "51", IsRequired = false }
                        }
                    },
                    new CodecProfile
                    {
                        Type = "Video",
                        Codec = "hevc",
                        Conditions = new List<ProfileCondition>
                        {
                            new ProfileCondition { Condition = "EqualsAny", Property = "VideoProfile", Value = "main|main 10", IsRequired = false },
                            new ProfileCondition { Condition = "LessThanEqual", Property = "VideoLevel", Value = "183", IsRequired = false }
                        }
                    },
                    new CodecProfile
                    {
                        Type = "Video",
                        Codec = "vp9",
                        Conditions = new List<ProfileCondition>
                        {
                            new ProfileCondition { Condition = "EqualsAny", Property = "VideoProfile", Value = "profile 0|profile 2", IsRequired = false }
                        }
                    },
                    // TrueHD is never a native Tizen playback target. This
                    // impossible condition makes the incompatibility explicit
                    // even when a server is permissive about container profiles.
                    new CodecProfile
                    {
                        Type = "VideoAudio",
                        Codec = "truehd",
                        Conditions = new List<ProfileCondition>
                        {
                            new ProfileCondition
                            {
                                Condition = "Equals",
                                Property = "AudioChannels",
                                Value = "0",
                                IsRequired = true
                            }
                        }
                    },
                  
                },

                DirectPlayProfiles = new List<DirectPlayProfile>
                {
                    // Keep direct-play rules container-specific. A single DirectPlayProfile is
                    // treated as a broad allow-list, so mixing unrelated containers/codecs here
                    // can make the server assume unsupported combinations are valid.
                    new DirectPlayProfile
                    {
                        Type = "Video",
                        Container = "mp4,m4v",
                        VideoCodec = broadVideoCodecs,
                        // MP4/ISOBMFF can carry VP9 and AV1, but direct play still depends on
                        // decoder support on the target TV model.
                        // DTS FIX: We intentionally omit "dts,dca" from every video profile.
                        // Jellyfin will see the file has DTS, see the active profile lacks it,
                        // and trigger audio transcoding while still allowing video copy.
                        AudioCodec = "aac,mp3,ac3,eac3,ac4,mp2,pcm_s16le,pcm_s24le,aac_latm,opus,flac,vorbis"
                    },
                    new DirectPlayProfile
                    {
                        Type = "Video",
                        Container = "mov",
                        VideoCodec = "h264",
                        AudioCodec = "aac,mp3,ac3,eac3,pcm_s16le,pcm_s24le"
                    },
                    new DirectPlayProfile
                    {
                        Type = "Video",
                        Container = "mkv",
                        VideoCodec = broadVideoCodecs,
                        AudioCodec = "aac,mp3,ac3,eac3,ac4,mp2,pcm_s16le,pcm_s24le,aac_latm,opus,flac,vorbis"
                    },
                    new DirectPlayProfile
                    {
                        Type = "Video",
                        Container = "webm",
                        VideoCodec = webmVideoCodecs,
                        AudioCodec = "vorbis,opus"
                    },
                    new DirectPlayProfile
                    {
                        Type = "Video",
                        Container = "ts,mpegts",
                        VideoCodec = tsVideoCodecs,
                        AudioCodec = "aac,mp3,ac3,eac3,mp2,pcm_s16le,pcm_s24le,aac_latm,opus,flac,vorbis"
                    },
                    new DirectPlayProfile
                    {
                        Type = "Video",
                        Container = "m2ts",
                        VideoCodec = m2tsVideoCodecs,
                        AudioCodec = "aac,mp3,ac3,eac3,mp2,pcm_s16le,pcm_s24le,aac_latm,flac"
                    },
                    new DirectPlayProfile
                    {
                        Type = "Video",
                        Container = "avi",
                        VideoCodec = "h264,mpeg4,msmpeg4,divx,xvid",
                        AudioCodec = "mp3,ac3,mp2,pcm_s16le"
                    },
                    new DirectPlayProfile
                    {
                        Type = "Video",
                        Container = "mpg,mpeg,vob,vro",
                        AudioCodec = "ac3,mp2,pcm_s16le"
                    },
                    new DirectPlayProfile
                    {
                        Type = "Video",
                        Container = "flv,3gp",
                        AudioCodec = "aac,mp3"
                    },
                    new DirectPlayProfile
                    {
                        Type = "Audio",
                        Container = "opus,mp3,aac,m4a,m4b,flac,webma,wma,wav,ogg"
                    },
                    new DirectPlayProfile
                    {
                        Type = "Audio",
                        Container = "webm",
                        AudioCodec = "opus,webma"
                    }
                },
                // Explicitly constrain remux/direct-stream outputs. TrueHD is
                // intentionally absent so Jellyfin selects an AC3-capable
                // transcoding profile while preserving a compatible video
                // stream such as 4K HEVC.
                DirectStreamProfiles = new List<DirectPlayProfile>
                {
                    new DirectPlayProfile
                    {
                        Type = "Video",
                        Container = "mp4",
                        VideoCodec = directStreamVideoCodecs,
                        AudioCodec = hlsAudioCodecs
                    }
                },
                TranscodingProfiles = transcodingProfiles,
                SubtitleProfiles = new List<SubtitleProfile>()
            };

            if (forceBurnIn)
            {
                // Force Burn-in (Encode) for everything
                profile.SubtitleProfiles.Add(new SubtitleProfile { Format = "srt", Method = "Encode" });
                profile.SubtitleProfiles.Add(new SubtitleProfile { Format = "subrip", Method = "Encode" });
                profile.SubtitleProfiles.Add(new SubtitleProfile { Format = "vtt", Method = "Encode" });
                profile.SubtitleProfiles.Add(new SubtitleProfile { Format = "ass", Method = "Encode" });
                profile.SubtitleProfiles.Add(new SubtitleProfile { Format = "ssa", Method = "Encode" });
                profile.SubtitleProfiles.Add(new SubtitleProfile { Format = "pgs", Method = "Encode" });
                profile.SubtitleProfiles.Add(new SubtitleProfile { Format = "pgssub", Method = "Encode" });
                profile.SubtitleProfiles.Add(new SubtitleProfile { Format = "mov_text", Method = "Encode" });
            }
            else if (disableSubtitles)
            {
                // Explicit subtitle-off negotiation mode:
                // advertise no subtitle delivery methods so server doesn't negotiate
                // subtitle burn-in while subtitles are disabled by user choice.
                profile.SubtitleProfiles = new List<SubtitleProfile>();
            }
            else
            {
                // External (Download) mode
                profile.SubtitleProfiles = new List<SubtitleProfile>
                {
                    new SubtitleProfile { Format = "vtt", Method = "External" },
                    new SubtitleProfile { Format = "srt", Method = "External" },
                    new SubtitleProfile { Format = "subrip", Method = "External" },
                    new SubtitleProfile { Format = "ass", Method = "External" },
                    new SubtitleProfile { Format = "ssa", Method = "External" },
                    // Keep Embed as secondary option for DirectPlay compatibility
                    new SubtitleProfile { Format = "vtt", Method = "Embed" },
                    new SubtitleProfile { Format = "srt", Method = "Embed" },
                    new SubtitleProfile { Format = "subrip", Method = "Embed" },
                    new SubtitleProfile { Format = "ass", Method = "Embed" },
                    new SubtitleProfile { Format = "ssa", Method = "Embed" },                 
                    new SubtitleProfile { Format = "mov_text", Method = "Embed" }
                };
            }

            // When forceBurnIn is true, use one deterministic H264/TS route.
            if (forceBurnIn)
            {
                profile.TranscodingProfiles = new List<TranscodingProfile>
                {
                    new TranscodingProfile
                    {
                        Container = "ts",
                        Type = "Video",
                        AudioCodec = hlsAudioCodecs,
                        VideoCodec = "h264",
                        Context = "Streaming",
                        Protocol = "hls",
                        MaxAudioChannels = runtime.MaxHlsAudioChannels,
                        MinSegments = hlsMinimumSegments,
                        SegmentLength = hlsSegmentLengthSeconds,
                        BreakOnNonKeyFrames = false,
                        EnableAudioVbrEncoding = false
                    }
                };
            }
            else if (preferTsOnlyHls)
            {
                profile.TranscodingProfiles = profile.TranscodingProfiles.FindAll(p =>
                    !string.Equals(p.Type, "Video", System.StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(p.Protocol, "hls", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Container, "ts", System.StringComparison.OrdinalIgnoreCase));
            }
            else if (preferFmp4HlsFirst)
            {
                profile.TranscodingProfiles =
                    PlaybackProfileOrdering.PreferFmp4HlsFirst(
                        profile.TranscodingProfiles);
            }
            else if (preferTsHlsFirst)
            {
                var preferredTsProfiles = profile.TranscodingProfiles
                    .Where(p =>
                        string.Equals(p.Type, "Video", System.StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(p.Protocol, "hls", System.StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(p.Container, "ts", System.StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (preferredTsProfiles.Count > 0)
                {
                    var remainingProfiles = profile.TranscodingProfiles
                        .Where(p => !preferredTsProfiles.Contains(p))
                        .ToList();
                    preferredTsProfiles.AddRange(remainingProfiles);
                    profile.TranscodingProfiles = preferredTsProfiles;
                }
            }

            TailscaleDebugLog.Add(
                "PlaybackProfile.Runtime: " +
                $"tizen={runtime.PlatformVersion:0.0},fallback={runtime.UsedFallback}," +
                $"fmp4Hls={runtime.SupportsFmp4Hls},hevcTsHls={runtime.SupportsHevcInMpegTsHls}," +
                $"eac3Hls={runtime.SupportsEac3InHls}," +
                $"preferFmp4={preferFmp4HlsFirst}," +
                $"order={string.Join(">", profile.TranscodingProfiles.Select(p => $"{p.Protocol}:{p.Container}"))}");

            return profile;
        }
    }
}
